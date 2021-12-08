using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
public class Builder : IDisposable
{
    internal readonly int MaxNodeCount = 10;

    private readonly BuildManager _buildManager;
    private readonly CacheFolder _cacheFolder;
    private readonly List<ProjectGroup> _groups;
    private BlockingCollection<Project> _projectsCreated;

    public Builder(ProjectsProvider provider)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));

        ProjectCollectionRootElementCache = new ProjectCollectionRootElementCache(true, true);
        _groups = new List<ProjectGroup>();

        // By default for the build folder:
        // - if we have a solution output to the `build` folder in the same folder than the solution
        // - if we have a project file to the `build` folder in 2 folders above the project (so usually same level than the solution)
        _cacheFolder = new CacheFolder(Path.Combine(Provider.BuildFolder, "caches"));

        _buildManager = new BuildManager();

        //var cacheFiles = _cacheFolder.ListCacheFiles().ToArray();
        //var results = _buildManager.LoadCachedResults(cacheFiles);
        //foreach (var pair in results)
        //{
        //    Console.WriteLine($"Results {pair.Value.ConfigurationId} {pair.Key.ProjectFullPath}");
        //    foreach (var resultPerTarget in pair.Value.ResultsByTarget)
        //    {
        //        foreach (var item in resultPerTarget.Value.Items)
        //        {
        //            Console.WriteLine($"   {resultPerTarget.Key} => {item.ItemSpec}");
        //        }
        //    }
        //}
    }

    public ProjectsProvider Provider { get; }

    public CacheFolder CacheFolder => _cacheFolder;

    internal ProjectCollectionRootElementCache ProjectCollectionRootElementCache { get; }

    internal BuildManager BuildManager => _buildManager;


    public ProjectGroup LoadProjectGroup(IReadOnlyDictionary<string, string> properties)
    {
        var group = new ProjectGroup(this, properties);
        group.InitializeGraph();
        _groups.Add(group);
        return group;
    }

    //public void DumpRootGlobs(ProjectGraph graph)
    //{

    //    var root = graph.GraphRoots.First();

    //    var project = Project.FromFile(root.ProjectInstance.FullPath, new ProjectOptions()
    //    {
    //        ToolsVersion = root.ProjectInstance.ToolsVersion,
    //        ProjectCollection = _projectCollection,
    //        EvaluationContext = _evaluationContext,
    //        GlobalProperties = _globalPropertiesForGraph,
    //    });

    //    var allGlobs = project.GetAllGlobs();

    //    foreach (var globResult in allGlobs)
    //    {
    //        Console.WriteLine($"{globResult.ItemElement.ItemType} includes: {string.Join(',', globResult.IncludeGlobs)} excludes: {string.Join(',', globResult.Excludes)} removes: {string.Join(',', globResult.Removes)}");
    //    }
    //}

    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> Run(ProjectGroup group, params string[] targets)
    {
        return Run( group, group.ProjectGraph.GraphRoots.First(), targets);
    }
    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> Run(ProjectGroup group, LoggerVerbosity verbosity, params string[] targets)
    {
        return Run(group, group.ProjectGraph.GraphRoots.First(), targets, ProjectGraphNodeDirection.Down, verbosity);
    }
    
    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> RunRootOnly(ProjectGroup group, params string[] targets)
    {
        return Run(group, group.ProjectGraph.GraphRoots.First(), targets, ProjectGraphNodeDirection.Current);
    }

    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> Run(ProjectGroup group, ProjectGraphNode startingNode, 
                                                                             IList<string> targetNames,
                                                                             ProjectGraphNodeDirection direction = ProjectGraphNodeDirection.Down, LoggerVerbosity? loggerVerbosity = null)
                                                                             
    {
        // Build node in //
        var parameters = CreateParameters(group.ProjectCollection, loggerVerbosity);

        GraphBuildCacheFilePathDelegate projectCacheFilePathDelegate = null;

        var copyTargetNames = new List<string>(targetNames);

        // If we ask for building, cache the results
        if (copyTargetNames.Contains("Build"))
        {
            projectCacheFilePathDelegate = GetResultsCacheFilePath;
            parameters.IsolateProjects = true;
            copyTargetNames.Add("CollectReferencePathWithRefAssemblies");
        }

        _buildManager.BeginBuild(parameters);
        try
        {
            var graphBuildRequest = new GraphBuildRequestData(group.ProjectGraph, copyTargetNames, null, BuildRequestDataFlags.None, new [] { startingNode }, direction, projectCacheFilePathDelegate);
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

    private BuildParameters CreateParameters(ProjectCollection projectCollection, LoggerVerbosity? verbosity)
    {
        var loggers = new List<ILogger>();

        if (verbosity.HasValue)
        {
            loggers.Add(new ConsoleLogger(verbosity.Value));
            // new BinaryLogger() { Parameters = "msbuild.binlog"}
        }
        else
        {
            loggers.Add(new ConsoleLogger(LoggerVerbosity.Quiet));
        }

        var parameters = new BuildParameters(projectCollection)
        {
            Loggers = loggers,
            DisableInProcNode = true,
            EnableNodeReuse = true,
            MaxNodeCount = MaxNodeCount,
            ResetCaches = false, // We don't want the cache to be reset
            DiscardBuildResults = true, // But we don't want results to be stored
        };

        return parameters;
    }

    public void Dispose()
    {
        _buildManager?.Dispose();
    }
}