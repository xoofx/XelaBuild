using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using Microsoft.Build.Logging;

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
    private Dictionary<Dictionary<string, string>, ProjectCollection> _globalPropertiesToProjectCollection = new(DictionaryComparer.Instance);

    public Builder(string rootProject)
    {
        _globalProperties = new Dictionary<string, string>()
        {
            { "Configuration", "Release" },
            { "Platform", "AnyCPU" },
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
        _buildFolder = EnsureDirectory(Path.Combine(_rootFolder, "build"));
        _cacheFolder = EnsureDirectory(Path.Combine(_buildFolder, "caches"));
    }

    public bool UseGraph { get; set; }

    public int Count => _collectionForRestore.LoadedProjects.Count;


    public double TimeProjectGraph { get; private set; }



    private ProjectGraph CreateGraph(string rootProjectFile)
    {
        return CreateGraph(rootProjectFile, _collectionForGraph);
    }

    private ProjectGraph CreateGraph(string rootProjectFile, ProjectCollection collection)
    {
        var clock = Stopwatch.StartNew();
        var projectGraph = new ProjectGraph(new []{ new ProjectGraphEntryPoint(rootProjectFile, collection.GlobalProperties)}, collection, CreateProjectInstance);
        TimeProjectGraph = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        return projectGraph;
    }

    public void PreBuildCaches()
    {
        var graph = CreateGraph(_rootProjectPath);
        foreach (var node in graph.GraphRoots)
        {
            PreBuildProject(_collectionForGraph, node, true);
        }
    }

    private static string EnsureDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return directory;
    }

    private string GetBuildCache(ProjectInstance instance)
    {
        // TODO: should take into account the hash of the properties (or use sub folders for some properties e.g like Configuration)
        var projectFileCache = Path.GetFileName(instance.FullPath) + ".cache";
        var cacheFile = Path.Combine(EnsureDirectory(_cacheFolder), projectFileCache);
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

    private void PreBuildProject(ProjectCollection parentCollection, ProjectGraphNode node, bool isRoot)
    {
        if (!_visitedNodes.Add(node)) return;

        foreach (var subNode in node.ProjectReferences)
        {
            PreBuildProject(parentCollection, subNode, false);
        }

        var parameters = new BuildParameters(parentCollection)
        {
            Loggers = new List<ILogger>()
            {
                new ConsoleLogger(LoggerVerbosity.Minimal)
            },
            DisableInProcNode = true,
            EnableNodeReuse = true,
            MaxNodeCount = 10,
            IsolateProjects = true,
        };

        // We don't store the cache for the root project
        if (!isRoot)
        {
            parameters.OutputResultsCacheFile = GetBuildCache(node.ProjectInstance);
        }

        if (node.ProjectReferences.Count > 0)
        {
            parameters.InputResultsCacheFiles = node.ProjectReferences.Select(x => GetBuildCache(x.ProjectInstance)).ToArray();
        }

        using var manager = new BuildManager();
        manager.BeginBuild(parameters);
        try
        {
            Console.WriteLine($"PreBuilding cache for {node.ProjectInstance.FullPath}");
            var clock = Stopwatch.StartNew();

            var graph = CreateGraph(node.ProjectInstance.FullPath);
            var request = new GraphBuildRequestData(graph, new[] { "Build", "GetTargetFrameworks", "GetNativeManifest" });
            TimeProjectGraph = clock.Elapsed.TotalMilliseconds;
            clock.Restart();

            var submission = manager.PendBuildRequest(request);
            var result = submission.Execute();
        }
        finally
        {
            manager.EndBuild();
        }
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
                TimeProjectGraph = clock.Elapsed.TotalMilliseconds;
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