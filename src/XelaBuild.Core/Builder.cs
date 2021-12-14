using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Logging;

namespace XelaBuild.Core;

/// <summary>
/// Simple hosting of BuildManager from msbuild
/// </summary>
public class Builder : IDisposable
{
#if DEBUG
    internal readonly int MaxNodeCount = 1;
#else
    internal readonly int MaxNodeCount = 10;
#endif

    private readonly BuildManager _buildManager;
    private readonly List<ProjectGroup> _groups;

    public Builder(ProjectsProvider provider)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));

        ProjectCollectionRootElementCache = new ProjectCollectionRootElementCache(true, true);
        _groups = new List<ProjectGroup>();

        // By default for the build folder:
        // - if we have a solution output to the `build` folder in the same folder than the solution
        // - if we have a project file to the `build` folder in 2 folders above the project (so usually same level than the solution)

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

    internal ProjectCollectionRootElementCache ProjectCollectionRootElementCache { get; }

    internal BuildManager BuildManager => _buildManager;
    
    public void Initialize(params IReadOnlyDictionary<string, string>[] arrayOfGlobalProperties)
    {
        foreach (var properties in arrayOfGlobalProperties)
        {
            LoadProjectGroup(properties);
        }

        LoadCachedBuildResults();
    }

    public ProjectGroup LoadProjectGroup(IReadOnlyDictionary<string, string> properties)
    {
        var group = new ProjectGroup(this, properties);
        group.InitializeGraph();
        _groups.Add(group);
        return group;
    }

    private void LoadCachedBuildResults()
    {
        var cacheFiles = new List<string>();
        foreach (var group in _groups)
        {
            foreach (var project in group.Projects)
            {
                var cacheFile = project.GetBuildResultCacheFilePath();
                if (File.Exists(cacheFile))
                {
                    cacheFiles.Add(cacheFile);
                }
            }
        }
        var results = _buildManager.LoadCachedResults(cacheFiles.ToArray());

        // Attach results back to projects
        foreach (var pair in results)
        {
            foreach (var group in _groups)
            {
                var projectState = group.FindProjectState(pair.Key.ProjectFullPath);
                projectState.LastResultTime = File.GetLastWriteTimeUtc(projectState.GetBuildResultCacheFilePath());
                projectState.LastResult = pair.Value;
            }
        }
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
        //GraphBuildInputsDelegate projectGraphBuildInputs = null;

        var copyTargetNames = new List<string>(targetNames);

        // If we ask for building, cache the results
        if (copyTargetNames.Contains("Build"))
        {
            projectCacheFilePathDelegate = node => @group.FindProjectState(node).GetBuildResultCacheFilePath();
            parameters.IsolateProjects = true;
        }
        //else if (copyTargetNames.Contains("Restore"))
        //{
        //    copyTargetNames.Clear();
        //    copyTargetNames.Add("XelaRestore");
        //    projectCacheFilePathDelegate = node => @group.FindProjectState(node).GetRestoreResultCacheFilePath();
        //    projectGraphBuildInputs = node => node.ProjectReferences.SelectMany(GetTransitiveProjectReferences);
        //    parameters.IsolateProjects = true;
        //}
        
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

    private static IEnumerable<ProjectGraphNode> GetTransitiveProjectReferences(ProjectGraphNode node)
    {
        yield return node;

        foreach (var subNode in node.ProjectReferences)
        {
            foreach (var subNode1 in GetTransitiveProjectReferences(subNode))
            {
                yield return subNode1;
            }
        }
    }
    
    public BuildResult Restore(ProjectGroup group)
    {
        var properties = new Dictionary<string, string>(group.ProjectCollection.GlobalProperties)
        {
            ["Platform"] = "Any CPU",
            ["NuGetConsoleProcessFileName"] = Path.Combine(Path.GetDirectoryName(typeof(Builder).Assembly.Location), "XelaBuild.NuGetRestore.exe"),
            ["RestoreUseStaticGraphEvaluation"] = "true"
        };
        var collection = new ProjectCollection(properties);
        var parameters = CreateParameters(collection, LoggerVerbosity.Quiet);

        _buildManager.BeginBuild(parameters);
        try
        {
            var graphBuildRequest = new BuildRequestData(Provider.GetProjectPaths().First(), properties, null, new[] {"Restore"}, null);
            var submission = _buildManager.PendBuildRequest(graphBuildRequest);
            var result = submission.Execute();
            return result;
        }
        finally
        {
            _buildManager.EndBuild();
        }
    }

    private BuildParameters CreateParameters(ProjectCollection projectCollection, LoggerVerbosity? verbosity)
    {
        var loggers = new List<ILogger>();

        if (verbosity.HasValue)
        {
            loggers.Add(new ConsoleLogger(verbosity.Value));
        }
        else
        {
            loggers.Add(new ConsoleLogger(LoggerVerbosity.Quiet));
        }

        loggers.Add(new BinaryLogger() { Parameters = "msbuild.binlog", Verbosity = LoggerVerbosity.Diagnostic });

        var parameters = new BuildParameters(projectCollection)
        {
            Loggers = loggers,
            DisableInProcNode = true,
            EnableNodeReuse = true,
            MaxNodeCount = MaxNodeCount,
            ResetCaches = false, // We don't want the cache to be reset
            DiscardBuildResults = true, // But we don't want results to be stored,
            ProjectLoadSettings = ProjectLoadSettings.RecordEvaluatedItemElements,
        };

        return parameters;
    }

    public void Dispose()
    {
        _buildManager?.Dispose();
    }
}