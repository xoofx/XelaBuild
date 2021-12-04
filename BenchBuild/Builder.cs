using System;
using System.IO;
using System.Linq;
using UnityProjectCachePluginExtension;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Logging;

namespace BenchBuild;

/// <summary>
/// Simple hosting of BuildManager from msbuild
/// </summary>
class Builder
{
    private const int MaxMsBuildNodeCount = 10;

    private readonly ProjectCollection _collectionForRestore;
    private readonly ProjectCollection _collectionForGraph;
    private readonly EvaluationContext _context;
    private readonly Dictionary<string, string> _globalProperties;
    private readonly Dictionary<string, string> _globalPropertiesForGraph;
    private readonly BuildManager _buildManager;
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

        _buildManager = new BuildManager();

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

    public ProjectGraph GetGraph()
    {
        return CreateGraph(_rootProjectPath);
    }

    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> BuildCache(ProjectGraph graph)
    {
        return Run(graph, "Build");
    }

    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> Run(ProjectGraph projectGraph, ProjectGraphNode startingNode, string[] targets, ProjectGraphNodeDirection direction = ProjectGraphNodeDirection.Down)
    {
        return Run(projectGraph, startingNode, _collectionForGraph, targets, direction);
    }

    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> Run(ProjectGraph projectGraph, params string[] targets)
    {
        return Run(projectGraph, projectGraph.GraphRoots.First(), targets);
    }

    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> BuildRootOnlyWithParallelCache(ProjectGraph projectGraph, params string[] targets)
    {
        return Run(projectGraph, projectGraph.GraphRoots.First(), targets, ProjectGraphNodeDirection.Current);
    }

    private IReadOnlyDictionary<ProjectGraphNode, BuildResult> Run(ProjectGraph projectGraph, 
                                                                             ProjectGraphNode startingNode, 
                                                                             ProjectCollection projectCollection, 
                                                                             IList<string> targetNames,
                                                                             ProjectGraphNodeDirection direction
                                                                             )
    {
        // Build node in //
        var parameters = CreateParameters(projectCollection);

        GraphBuildCacheFilePathDelegate projectCacheFilePathDelegate = null;

        // If we ask for building, cache the results
        if (targetNames.Contains("Build"))
        {
            projectCacheFilePathDelegate = GetResultsCacheFilePath;
            parameters.IsolateProjects = true;
        }

        _buildManager.BeginBuild(parameters);
        try
        {
            var graphBuildRequest = new GraphBuildRequestData(projectGraph, targetNames, null, BuildRequestDataFlags.None, new [] { startingNode }, direction, projectCacheFilePathDelegate);
            var submission = _buildManager.PendBuildRequest(graphBuildRequest);
            var result = submission.Execute();
            return result.ResultsByNode;
        } 
        finally
        {
            _buildManager.EndBuild();
        }
    }

    private string GetResultsCacheFilePath(ProjectGraphNode graphnode)
    {
        return _buildResultCaching.GetCacheFilePath(graphnode.ProjectInstance);
    }

    private string GetBuildCache(ProjectInstance instance)
    {
        return _buildResultCaching.GetCacheFilePath(instance);
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


    private BuildParameters CreateParameters(ProjectCollection projectCollection)
    {
        var parameters = new BuildParameters(projectCollection)
        {
            Loggers = new List<ILogger>()
            {
                //new ConsoleLogger(LoggerVerbosity.Minimal),
                //new BinaryLogger() { Parameters = "msbuild.binlog"}
            },
            DisableInProcNode = true,
            EnableNodeReuse = true,
            MaxNodeCount = MaxMsBuildNodeCount,
            ResetCaches = false, // We don't want the cache to be reset
            DiscardBuildResults = true, // But we don't want results to be stored
        };
        return parameters;
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