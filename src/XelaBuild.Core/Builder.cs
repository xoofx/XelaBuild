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
public partial class Builder : IDisposable
{
#if DEBUG
    internal readonly int MaxNodeCount = 1;
#else
    // don't take all CPUs to let some space for the other processes (e.g VBCSCompiler)
    // Ideally, we should schedule as wide as the maximum of number of projects we could schedule concurrently
    // capped by the processor count...
    internal readonly int MaxNodeCount = Math.Max(1, Environment.ProcessorCount / 2);
#endif

    private readonly BuildManager _buildManager;
    private readonly List<ProjectGroup> _groups;
    private ProjectCollection _emptyCollection;

    public Builder(BuildConfiguration config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));

        // Initialize the logger
        InitializeLoggers(out _log);

        // Setup env variable used by XelaBuild.targets
        Environment.SetEnvironmentVariable("XelaBuildCacheDir", config.GlobalCacheFolder);
        // Override msbuild targets to use our special targets file to inject our tasks
        Environment.SetEnvironmentVariable("CustomAfterMicrosoftCommonTargets", Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Builder).Assembly.Location), "XelaBuild.targets")));

        ProjectCollectionRootElementCache = new ProjectCollectionRootElementCache(true, true);

        _groups = new List<ProjectGroup>();

        // By default for the build folder:
        // - if we have a solution output to the `build` folder in the same folder than the solution
        // - if we have a project file to the `build` folder in 2 folders above the project (so usually same level than the solution)

        _buildManager = new BuildManager();

        Verbosity = LoggerVerbosity.Normal;

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

    public BuildConfiguration Config { get; }

    public LoggerVerbosity Verbosity { get; set; }

    internal ProjectCollectionRootElementCache ProjectCollectionRootElementCache { get; }

    internal BuildManager BuildManager => _buildManager;
    
    public void Initialize(params IReadOnlyDictionary<string, string>[] arrayOfGlobalProperties)
    {
        foreach (var properties in arrayOfGlobalProperties)
        {
            CreateProjectGroup(properties);
        }

        LoadCachedBuildResults();
    }

    public ProjectGroup CreateProjectGroup(IReadOnlyDictionary<string, string> properties)
    {
        var group = new ProjectGroup(this, properties);
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

    public GraphBuildResult Run(ProjectGroup group, params string[] targets)
    {
        return Run(group, group.ProjectGraph.GraphRoots.First(), targets, ProjectGraphNodeDirection.Down);
    }
    
    public GraphBuildResult RunRootOnly(ProjectGroup group, params string[] targets)
    {
        return Run(group, group.ProjectGraph.GraphRoots.First(), targets, ProjectGraphNodeDirection.Current);
    }

    public GraphBuildResult Run(ProjectGroup group, ProjectGraphNode startingNode, 
                                                                             IList<string> targetNames,
                                                                             ProjectGraphNodeDirection direction = ProjectGraphNodeDirection.Down)
                                                                             
    {
        // Build node in //
        var parameters = CreateParameters(group.ProjectCollection);

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
            return result;
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
        return RestoreSolution(new ProjectGroup[] {group})[group];
    }
    
    public Dictionary<ProjectGroup, BuildResult> RunSolution(string target, params ProjectGroup[] groups)
    {
        _emptyCollection ??= new ProjectCollection(new Dictionary<string, string>(), null, null,
            ToolsetDefinitionLocations.Default, 1, false, true, ProjectCollectionRootElementCache);

        var parameters = CreateParameters(_emptyCollection);

        var results = new Dictionary<ProjectGroup, BuildResult>();
        _buildManager.BeginBuild(parameters);
        try
        {
            if (groups.Length == 1)
            {
                var group = groups[0];
                var properties = new Dictionary<string, string>(group.ProjectCollection.GlobalProperties)
                {
                    ["Platform"] = "Any CPU"
                };
                properties.Remove("IsGraphBuild");
                var buildRequest = new BuildRequestData(Config.SolutionFilePath,
                    properties, null, new[] { target }, null);
                var submission = _buildManager.PendBuildRequest(buildRequest);
                var result = submission.Execute();
                results[group] = result;
            }
            else
            {

                foreach (var group in groups)
                {
                    var properties = new Dictionary<string, string>(group.ProjectCollection.GlobalProperties)
                    {
                        ["Platform"] = "Any CPU"
                    };
                    properties.Remove("IsGraphBuild");
                    var graphBuildRequest = new BuildRequestData(Config.SolutionFilePath,
                        properties, null, new[] { target }, null);
                    var submission = _buildManager.PendBuildRequest(graphBuildRequest);
                    submission.ExecuteAsync(buildSubmission =>
                    {
                        lock (results)
                        {
                            results[group] = buildSubmission.BuildResult;
                        }
                    }, null);
                }
            }
        }
        finally
        {
            _buildManager.EndBuild();
        }
        return results;
    }

    public Dictionary<ProjectGroup, BuildResult> BuildSolution(params ProjectGroup[] groups)
    {
        return RunSolution("Build", groups);
    }

    public Dictionary<ProjectGroup, BuildResult> RestoreSolution(params ProjectGroup[] groups)
    {
        return RunSolution("Restore", groups);
    }

    private BuildParameters CreateParameters(ProjectCollection projectCollection)
    {
        var loggers = new List<ILogger>();

        loggers.Add(new ConsoleLogger(Verbosity));
        //loggers.Add(new BinaryLogger() { Parameters = "msbuild.binlog", Verbosity = LoggerVerbosity.Diagnostic });

        var parameters = new BuildParameters(projectCollection)
        {
            Loggers = loggers,
            DisableInProcNode = true,
            EnableNodeReuse = true,
            MaxNodeCount = MaxNodeCount,
            ResetCaches = false, // We don't want the cache to be reset
            DiscardBuildResults = true, // But we don't want results to be stored,
            //ProjectLoadSettings = ProjectLoadSettings.RecordEvaluatedItemElements,
        };

        return parameters;
    }

    public void Dispose()
    {
        _buildManager?.Dispose();
    }
}