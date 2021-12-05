﻿using System;
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
    internal readonly int MaxNodeCount = 10;

    private readonly BuildManager _buildManager;
    private readonly string _rootProjectPath;
    private readonly string _buildFolder;
    private readonly CacheFolder _cacheFolder;

    public Builder(string rootProjectOrSln, string buildFolder = null)
    {
        if (rootProjectOrSln == null) throw new ArgumentNullException(nameof(rootProjectOrSln));

        ProjectCollectionRootElementCache = new ProjectCollectionRootElementCache(true, true);

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
    }

    public string RootProjectPath => _rootProjectPath;

    public CacheFolder CacheFolder => _cacheFolder;

    internal ProjectCollectionRootElementCache ProjectCollectionRootElementCache { get; }

    internal BuildManager BuildManager => _buildManager;

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
    
    public IReadOnlyDictionary<ProjectGraphNode, BuildResult> BuildRootOnlyWithParallelCache(ProjectGroup group, params string[] targets)
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

        // If we ask for building, cache the results
        if (targetNames.Contains("Build"))
        {
            projectCacheFilePathDelegate = GetResultsCacheFilePath;
            parameters.IsolateProjects = true;
        }

        _buildManager.BeginBuild(parameters);
        try
        {
            var graphBuildRequest = new GraphBuildRequestData(group.ProjectGraph, targetNames, null, BuildRequestDataFlags.None, new [] { startingNode }, direction, projectCacheFilePathDelegate);
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
}