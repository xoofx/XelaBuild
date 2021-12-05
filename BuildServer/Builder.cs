using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Logging;

namespace BuildServer;

/// <summary>
/// Simple hosting of BuildManager from msbuild
/// </summary>
public class Builder
{
    private const int MaxMsBuildNodeCount = 10;

    private readonly ProjectCollection _projectCollection;
    private readonly EvaluationContext _evaluationContext;
    private readonly Dictionary<string, string> _globalProperties;
    private readonly Dictionary<string, string> _globalPropertiesForGraph;
    private readonly Dictionary<string, Project> _projects;
    private readonly ProjectGraph _projectGraph;
    private readonly BuildManager _buildManager;
    private readonly string _rootProjectPath;
    private readonly string _buildFolder;
    private readonly CacheFolder _cacheFolder;
    private readonly Dictionary<Dictionary<string, string>, ProjectCollection> _globalPropertiesToProjectCollection = new(DictionaryComparer.Instance);

    public Builder(string rootProjectOrSln, string buildFolder = null)
    {
        if (rootProjectOrSln == null) throw new ArgumentNullException(nameof(rootProjectOrSln));

        _globalProperties = new Dictionary<string, string>()
        {
            { "Configuration", "Release" },
            { "Platform", "AnyCPU" },
            { "IsGraphBuild", "true"} // Make this upfront to include it in the cache file names
        };

        _projects = new Dictionary<string, Project>();
        _projectCollection = GetOrCreateProjectCollection(_globalProperties);
        _evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        _rootProjectPath = rootProjectOrSln ?? throw new ArgumentNullException(nameof(rootProjectOrSln));

        // By default for the build folder:
        // - if we have a solution output to the `build` folder in the same folder than the solution
        // - if we have a project file to the `build` folder in 2 folders above the project (so usually same level than the solution)
        var defaultRootFolder = _rootProjectPath.EndsWith(".sln", StringComparison.InvariantCultureIgnoreCase)
            ? Path.GetDirectoryName(_rootProjectPath)
            : Path.GetDirectoryName(Path.GetDirectoryName(_rootProjectPath));

        _buildFolder = buildFolder ?? DirectoryHelper.EnsureDirectory(Path.Combine(defaultRootFolder, "build"));
        _cacheFolder = new CacheFolder(Path.Combine(_buildFolder, "caches"));

        _buildManager = new BuildManager();

        // Clear the cache on disk
        _cacheFolder.ClearCacheFolder();

        // Initialize the project graph
        _projectGraph = CreateProjectGraph();
    }

    public bool UseGraph { get; set; }

    public int Count => _projectCollection.LoadedProjects.Count;

    public ProjectCollection ProjectCollection => _projectCollection;

    public ProjectGraph ProjectGraph => _projectGraph;

    public Project FindProject(string projectPath)
    {
        if (projectPath == null) throw new ArgumentNullException(nameof(projectPath));
        _projects.TryGetValue(projectPath, out var project);
        return project;
    }

    public void DumpRootGlobs(ProjectGraph graph)
    {

        var root = graph.GraphRoots.First();

        var project = Project.FromFile(root.ProjectInstance.FullPath, new ProjectOptions()
        {
            ToolsVersion = root.ProjectInstance.ToolsVersion,
            ProjectCollection = _projectCollection,
            EvaluationContext = _evaluationContext,
            GlobalProperties = _globalPropertiesForGraph,
        });
        
        var allGlobs = project.GetAllGlobs();

        foreach (var globResult in allGlobs)
        {
            Console.WriteLine($"{globResult.ItemElement.ItemType} includes: {string.Join(',', globResult.IncludeGlobs)} excludes: {string.Join(',', globResult.Excludes)} removes: {string.Join(',', globResult.Removes)}");
        }
    }

    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> Run(ProjectGraphNode startingNode, string[] targets, ProjectGraphNodeDirection direction = ProjectGraphNodeDirection.Down, LoggerVerbosity? loggerVerbosity = null)
    {
        return Run(startingNode, _projectCollection, targets, direction, loggerVerbosity);
    }

    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> Run(params string[] targets)
    {
        return Run( _projectGraph.GraphRoots.First(), targets);
    }
    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> Run(LoggerVerbosity verbosity, params string[] targets)
    {
        return Run(_projectGraph.GraphRoots.First(), targets, ProjectGraphNodeDirection.Down, verbosity);
    }
    
    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> BuildRootOnlyWithParallelCache(params string[] targets)
    {
        return Run(_projectGraph.GraphRoots.First(), targets, ProjectGraphNodeDirection.Current);
    }

    private IReadOnlyDictionary<ProjectGraphNode, BuildResult> Run(ProjectGraphNode startingNode, 
                                                                             ProjectCollection projectCollection, 
                                                                             IList<string> targetNames,
                                                                             ProjectGraphNodeDirection direction,
                                                                             LoggerVerbosity? loggerVerbosity = null
                                                                             )
    {
        // Build node in //
        var parameters = CreateParameters(projectCollection, loggerVerbosity);

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
            var graphBuildRequest = new GraphBuildRequestData(_projectGraph, targetNames, null, BuildRequestDataFlags.None, new [] { startingNode }, direction, projectCacheFilePathDelegate);
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
        return _cacheFolder.GetCacheFilePath(graphnode.ProjectInstance);
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

    private BuildParameters CreateParameters(ProjectCollection projectCollection, LoggerVerbosity? verbosity)
    {
        var loggers = new List<ILogger>();

        if (verbosity.HasValue)
        {
            loggers.Add(new ConsoleLogger(verbosity.Value));
            // new BinaryLogger() { Parameters = "msbuild.binlog"}
        }

        var parameters = new BuildParameters(projectCollection)
        {
            Loggers = loggers,
            DisableInProcNode = true,
            EnableNodeReuse = true,
            MaxNodeCount = MaxMsBuildNodeCount,
            ResetCaches = false, // We don't want the cache to be reset
            DiscardBuildResults = true, // But we don't want results to be stored
        };

        return parameters;
    }

    private ProjectGraph CreateProjectGraph()
    {
        return CreateProjectGraph(_rootProjectPath);
    }

    private ProjectGraph CreateProjectGraph(string rootProjectFile)
    {
        return CreateProjectGraph(rootProjectFile, _projectCollection);
    }

    private ProjectGraph CreateProjectGraph(string rootProjectFile, ProjectCollection collection)
    {
        var clock = Stopwatch.StartNew();
        var projectGraph = new ProjectGraph(new[] { new ProjectGraphEntryPoint(rootProjectFile, collection.GlobalProperties) }, collection, CreateProjectInstance);
        clock.Restart();
        return projectGraph;
    }

    private ProjectInstance CreateProjectInstance(string projectPath, Dictionary<string, string> globalproperties, ProjectCollection projectCollection)
    {
        if (!_projects.TryGetValue(projectPath, out var project))
        {
            project = projectCollection.LoadProject(projectPath, globalproperties, projectCollection.DefaultToolsVersion);
            _projects[projectPath] = project;
        }

        return _buildManager.GetProjectInstanceForBuild(project);
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
            foreach (var pair in obj.OrderBy(x => x.Key))
            {
                hash = HashCode.Combine(hash, pair.Value, pair.Key);
            }
            return hash;
        }
    }
}