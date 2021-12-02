using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Experimental.ProjectCache;
using Microsoft.Build.Logging;
using UnityProjectCachePluginExtension;

namespace BenchBuild;

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;

/// <summary>
/// Simple hosting of BuildManager from msbuild
/// </summary>
class Builder
{
    private readonly ProjectCollection _collectionForRestore;
    private readonly ProjectCollection _collectionForGraph;
    private readonly EvaluationContext _context;
    private readonly Dictionary<string, string> _globalProperties;
    private readonly Dictionary<string, string> _globalPropertiesForGraph;
    private string _rootProjectPath;
    private readonly HashSet<ProjectGraphNode> _visitedNodes;
    private readonly string _rootFolder;
    private readonly string _buildFolder;
    private readonly string _cacheFolder;
    private readonly BuildResultCaching _buildResultCaching;
    private Dictionary<Dictionary<string, string>, ProjectCollection> _globalPropertiesToProjectCollection = new(DictionaryComparer.Instance);

    public Builder(string rootProject)
    {
        _globalProperties = new Dictionary<string, string>()
        {
            { "Configuration", "Release" },
            { "Platform", "AnyCPU" },
            { "UnityBuildProcess", "true"}
        };

        _globalPropertiesForGraph = new Dictionary<string, string>(_globalProperties)
        {
            { "IsGraphBuild", "true" } // enforce IsGraphBuild for this collection
        };

        _visitedNodes = new HashSet<ProjectGraphNode>();
        _collectionForRestore = GetOrCreateProjectCollection(_globalProperties);
        _collectionForGraph = GetOrCreateProjectCollection(_globalPropertiesForGraph);
        _context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        _rootProjectPath = rootProject;
        _rootFolder = Path.GetDirectoryName(Path.GetDirectoryName(_rootProjectPath));
        _buildFolder = DirectoryHelper.EnsureDirectory(Path.Combine(_rootFolder, "build"));
        _cacheFolder = DirectoryHelper.EnsureDirectory(Path.Combine(_buildFolder, "caches"));
        _buildResultCaching = new BuildResultCaching(_cacheFolder);

        // Clear the cache on disk
        _buildResultCaching.ClearCaches();
    }

    public bool UseGraph { get; set; }

    public int Count => _collectionForRestore.LoadedProjects.Count;

    private ProjectGraph CreateGraph(string rootProjectFile)
    {
        return CreateGraph(rootProjectFile, _collectionForGraph);
    }

    private ProjectGraph CreateGraph(string rootProjectFile, ProjectCollection collection)
    {
        var clock = Stopwatch.StartNew();
        var projectGraph = new ProjectGraph(new []{ new ProjectGraphEntryPoint(rootProjectFile, collection.GlobalProperties)}, collection, CreateProjectInstance);
        clock.Restart();
        return projectGraph;
    }

    public ProjectGraph BuildCache()
    {
        var graph = CreateGraph(_rootProjectPath);

        var mapNodeToTargets = graph.GetTargetLists(null);

        var result = BuildParallelWithCache(graph, _collectionForGraph, mapNodeToTargets);

        //var root = graph.GraphRoots.First();

        //foreach(var node in graph.ProjectNodesTopologicallySorted)
        //{
        //    var targets = mapNodeToTargets[node].ToArray();
        //    BuildProjectWithCache(_collectionForGraph, node, node == root, targets);
        //}

        return graph;
    }

    private string GetBuildCache(ProjectInstance instance)
    {
        // TODO: should take into account the hash of the properties (or use sub folders for some properties e.g like Configuration)
        var projectFileCache = Path.GetFileName(instance.FullPath) + ".cache";
        var cacheFile = Path.Combine(DirectoryHelper.EnsureDirectory(_cacheFolder), projectFileCache);
        return cacheFile;
    }

    private ProjectCollection GetOrCreateProjectCollection(Dictionary<string, string> properties)
    {
        lock (_globalPropertiesToProjectCollection)
        {
            if (!_globalPropertiesToProjectCollection.TryGetValue(properties, out var projectCollection))
            {
                // TODO: ProjectCollection doesn't share ProjectElementRootCache https://github.com/dotnet/msbuild/issues/7107
                projectCollection = new ProjectCollection(properties);
                _globalPropertiesToProjectCollection.Add(properties, projectCollection);
            }
            return projectCollection;
        }
    }

    public void BuildProjectWithCache(ProjectGraph graph, params string[] targets)
    {
        BuildProjectWithCache(graph, _collectionForGraph, graph.GraphRoots.First(), targets);
    }

    private BuildParameters CreateParameters(ProjectGraph graph, ProjectCollection projectCollection)
    {
        var parameters = new BuildParameters(projectCollection)
        {
            Loggers = new List<ILogger>()
            {
                new ConsoleLogger(LoggerVerbosity.Normal),
                new BinaryLogger() { Parameters = "msbuild.binlog"}
            },
            DisableInProcNode = true,
            EnableNodeReuse = true,
            MaxNodeCount = 10,
            IsolateProjects = true,
        };

        //// We don't store the cache for the root project
        //if (node.ReferencingProjects.Count == 0)
        //{
        //    parameters.OutputResultsCacheFile = GetBuildCache(node.ProjectInstance);
        //}

        //if (node.ProjectReferences.Count > 0)
        //{
        //    parameters.InputResultsCacheFiles = node.ProjectReferences.Select(x => GetBuildCache(x.ProjectInstance)).ToArray();
        //}

        // ReSharper disable once InconsistentlySynchronizedField
        //parameters.ProjectCacheDescriptor = ProjectCacheDescriptor.FromInstance(_buildResultCaching, null, graph, null);
        return parameters;
    }

    private void BuildProjectWithCache(ProjectGraph graph, ProjectCollection parentCollection, ProjectGraphNode node, params string[] targets)
    {
        var parameters = CreateParameters(graph, parentCollection);

        using var manager = new BuildManager();
        manager.BeginBuild(parameters);
        try
        {
            Console.WriteLine($"Building {node.ProjectInstance.FullPath}");
            var clock = Stopwatch.StartNew();

            var request = new BuildRequestData(node.ProjectInstance, targets);
            clock.Restart();

            var submission = manager.PendBuildRequest(request);
            var result = submission.Execute();
        }
        finally
        {
            manager.EndBuild();
        }
    }

    public Dictionary<ProjectGraphNode, BuildResult> BuildParallelWithCache(ProjectGraph projectGraph, params string[] targets)
    {
        var targetsPerNode = projectGraph.GetTargetLists(targets);
        return BuildParallelWithCache(projectGraph, _collectionForGraph, targetsPerNode);
    }

    private Dictionary<ProjectGraphNode, BuildResult> BuildParallelWithCache(ProjectGraph projectGraph, ProjectCollection projectCollection, IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> targetsPerNode)
    {
        // NOTE: code adapted from BuildManager.cs
        // Copyright (c) Microsoft. All rights reserved.
        // Licensed under the MIT license. See LICENSE file in the project root for full license information.

        var waitHandle = new AutoResetEvent(true);
        var graphBuildStateLock = new object();

        var blockedNodes = new HashSet<ProjectGraphNode>(projectGraph.ProjectNodes);
        var finishedNodes = new HashSet<ProjectGraphNode>(projectGraph.ProjectNodes.Count);
        var buildingNodes = new Dictionary<BuildSubmission, ProjectGraphNode>();
        var resultsPerNode = new Dictionary<ProjectGraphNode, BuildResult>(projectGraph.ProjectNodes.Count);
        Exception submissionException = null;

        // Build node in //
        using var buildManager = new BuildManager();
        var parameters = CreateParameters(projectGraph, projectCollection);
        parameters.MaxNodeCount = 10;
        buildManager.BeginBuild(parameters);

        try
        {
            while (blockedNodes.Count > 0 || buildingNodes.Count > 0)
            {
                waitHandle.WaitOne();

                // When a cache plugin is present, ExecuteSubmission(BuildSubmission) executes on a separate thread whose exceptions do not get observed.
                // Observe them here to keep the same exception flow with the case when there's no plugins and ExecuteSubmission(BuildSubmission) does not run on a separate thread.
                if (submissionException != null)
                {
                    throw submissionException;
                }

                lock (graphBuildStateLock)
                {
                    var unblockedNodes = blockedNodes
                        .Where(node => node.ProjectReferences.All(projectReference => finishedNodes.Contains(projectReference)))
                        .ToList();

                    foreach (var node in unblockedNodes)
                    {
                        var targetList = targetsPerNode[node];
                        if (targetList.Count == 0)
                        {
                            // An empty target list here means "no targets" instead of "default targets", so don't even build it.
                            finishedNodes.Add(node);
                            blockedNodes.Remove(node);

                            waitHandle.Set();

                            continue;
                        }

                        var request = new BuildRequestData(node.ProjectInstance, targetList.ToArray());

                        // Make sure that the existing result is deleted before (re) building it
                        var buildProjectKey = BuildResultCaching.GetProjectBuildKeyFromBuildRequest(request);
                        _buildResultCaching.DeleteResult(buildProjectKey);

                        // TODO Tack onto the existing submission instead of pending a whole new submission for every node
                        // Among other things, this makes BuildParameters.DetailedSummary produce a summary for each node, which is not desirable.
                        // We basically want to submit all requests to the scheduler all at once and describe dependencies by requests being blocked by other requests.
                        // However today the scheduler only keeps track of MSBuild nodes being blocked by other MSBuild nodes, and MSBuild nodes haven't been assigned to the graph nodes yet.
                        var innerBuildSubmission = buildManager.PendBuildRequest(request);
                        buildingNodes.Add(innerBuildSubmission, node);
                        blockedNodes.Remove(node);
                        var result = innerBuildSubmission.Execute();

                        lock (graphBuildStateLock)
                        {
                            if (submissionException == null && result.Exception != null)
                            {
                                submissionException = result.Exception;
                            }

                            ProjectGraphNode finishedNode = buildingNodes[innerBuildSubmission];

                            finishedNodes.Add(finishedNode);
                            buildingNodes.Remove(innerBuildSubmission);

                            resultsPerNode.Add(finishedNode, innerBuildSubmission.BuildResult);

                            // Save the results only for projects that have references to it
                            if (node.ReferencingProjects.Count > 0)
                            {
                                _buildResultCaching.AddAndSaveResult(buildProjectKey, innerBuildSubmission.BuildResult);
                            }
                        }

                        waitHandle.Set();
                    }
                }
            }
        } 
        finally
        {
            buildManager.EndBuild();
        }


        return resultsPerNode;
    }


    public void Build(string target, bool useCache = false)
    {
        //const string buildCache = "build.cache";

        var parameters = new BuildParameters();
        parameters.Loggers = new List<ILogger>()
        {
            new ConsoleLogger(LoggerVerbosity.Minimal),
            new BinaryLogger() { Parameters = "msbuild.binlog"}
        }; //InputResultsCacheFiles = new []{ buildCache },
        parameters.ResetCaches = true; // should it be true? (doesn't affect anything in this benchmark)

        if (UseGraph)
        {
            parameters.DisableInProcNode = true;
            parameters.EnableNodeReuse = true;
            parameters.MaxNodeCount = 10;
        }
        else
        {
            parameters.DisableInProcNode = false;
        }
        //parameters.DisableInProcNode = false;

        ProjectGraph projectGraph = null;
        if (UseGraph)
        {
            projectGraph = CreateGraph(_rootProjectPath);
            if (useCache)
            {
                parameters.IsolateProjects = true;
                parameters.InputResultsCacheFiles = projectGraph.ProjectNodes.Where(x => !x.ProjectInstance.FullPath.Contains("LibRoot")).Select(x => GetBuildCache(x.ProjectInstance)).ToArray();
            }
        }

        using var manager = new BuildManager();
        manager.BeginBuild(parameters);
        try
        {
            if (UseGraph)
            {
                //// Not working
                //parameters.IsolateProjects = true;
                //if (File.Exists("project.cache"))
                //{
                //    parameters.InputResultsCacheFiles = new[] { buildCache };
                //}
                //else
                //{
                //    parameters.OutputResultsCacheFile = buildCache;
                //}

                //var request = new GraphBuildRequestData(projectGraph, new List<string>() { target }, null, BuildRequestDataFlags.ProvideProjectStateAfterBuild);
                if (useCache)
                {
                    var request = new BuildRequestData(projectGraph.GraphRoots.First().ProjectInstance, new [] { target });
                    var submission = manager.PendBuildRequest(request);
                    var result = submission.Execute();
                }
                else
                {
                    var request = new GraphBuildRequestData(projectGraph, new List<string>() { target });

                    var submission = manager.PendBuildRequest(request);
                    var result = submission.Execute();
                }
            }
            else
            {
                // try to see how much it takes to compile a single project without building project references
                _globalProperties["BuildProjectReferences"] = "false";

                var clock = Stopwatch.StartNew();
                var request = new BuildRequestData(CreateProjectInstance(_rootProjectPath, _globalProperties, _collectionForRestore), new[] { target });
                clock.Restart();

                var submission = manager.PendBuildRequest(request);
                var result = submission.Execute();
            }

            //// graph needs to have BuildRequestDataFlags.ProvideProjectStateAfterBuild);
            //foreach (var projectResult in result.ResultsByNode.Values)
            //{
            //    var projectAfterBuild = projectResult.ProjectStateAfterBuild;
            //    Console.WriteLine($"{projectAfterBuild.FullPath} Items: {projectAfterBuild.Items.Count}");
            //    foreach (var element in projectAfterBuild.Items)
            //    {
            //        if (element.ItemType.StartsWith("_") || !element.EvaluatedInclude.EndsWith(".cs")) continue;
            //        Console.WriteLine($"-> {element.ItemType} {element.EvaluatedInclude}");
            //    }
            //}
        }
        finally
        {
            manager.EndBuild();
        }
    }

    private static ProjectInstance CreateProjectInstance(string projectPath, Dictionary<string, string> globalproperties, ProjectCollection projectcollection)
    {
        return new ProjectInstance(projectPath, globalproperties, projectcollection.DefaultToolsVersion, projectcollection);
    }


    private class DictionaryComparer : IEqualityComparer<Dictionary<string, string>>
    {
        public static readonly DictionaryComparer Instance = new DictionaryComparer();

        public bool Equals(Dictionary<string, string> x, Dictionary<string, string> y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;

            // not efficient but enough for testing
            foreach (var pair in x)
            {
                if (!y.TryGetValue(pair.Key, out var otherValue) || string.CompareOrdinal(otherValue, pair.Value) != 0)
                {
                    return false;
                }
            }

            foreach (var pair in y)
            {
                if (!x.TryGetValue(pair.Key, out var otherValue) || string.CompareOrdinal(otherValue, pair.Value) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(Dictionary<string, string> obj)
        {
            var hash = obj.Count.GetHashCode();
            foreach (var pair in obj)
            {
                hash = HashCode.Combine(hash, pair.Value, pair.Key);
            }
            return hash;
        }
    }

}