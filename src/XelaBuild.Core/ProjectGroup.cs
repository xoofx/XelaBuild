using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Globbing;
using Microsoft.Build.Graph;
using XelaBuild.Core.Caching;
using XelaBuild.Core.Helpers;

namespace XelaBuild.Core;

/// <summary>
/// A group of projects to build for a specific configuration.
/// </summary>
public partial class ProjectGroup : IDisposable
{
    private readonly ProjectCollection _projectCollection;
    private readonly Builder _builder;
    private readonly Dictionary<string, ProjectState> _projectStates;
    private ProjectGraph? _projectGraph;
    private DateTime _solutionLastWriteTimeWhenRead;
    private CachedProjectGroup? _cachedProjectGroup;

    internal ProjectGroup(Builder builder, IReadOnlyDictionary<string, string> globalProperties)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (globalProperties == null) throw new ArgumentNullException(nameof(globalProperties));

        var properties = new Dictionary<string, string>(globalProperties)
        {
            ["IsGraphBuild"] = "true",
        };

        _projectStates = new Dictionary<string, ProjectState>();
        _projectCollection = new ProjectCollection(properties, null, null, ToolsetDefinitionLocations.Default, builder.MaxNodeCount, false, true, builder.ProjectCollectionRootElementCache);
        _builder = builder;

        var hashPostFix = HashHelper.Hash128(properties);
        IndexCacheFilePath = Path.Combine(_builder.Config.GlobalCacheFolder, $"{properties["Configuration"]}-{properties["Platform"]}-index-{hashPostFix}.cache");
    }

    public int Count => _projectCollection.LoadedProjects.Count;

    public bool Restored { get; private set; }

    public ProjectCollection ProjectCollection => _projectCollection;

    public ProjectGraph? ProjectGraph => _projectGraph;

    public IEnumerable<ProjectState> Projects
    {
        get
        {
            if (_projectGraph is null) yield break;
            foreach (var projectNode in _projectGraph.ProjectNodesTopologicallySorted)
            {
                yield return this.FindProjectState(projectNode);
            }
        }
    }

    public string IndexCacheFilePath { get; }

    public ProjectState FindProjectState(string projectPath)
    {
        if (projectPath == null) throw new ArgumentNullException(nameof(projectPath));
        if (!_projectStates.TryGetValue(projectPath, out var projectState))
        {
            throw new InvalidOperationException($"Unable to find project from state: {projectState}");
        }
        return projectState;
    }

    public ProjectGroupState Load()
    {
        var solutionFileInfo = FileUtilities.GetFileInfoNoThrow(_builder.Config.SolutionFilePath);
        if (solutionFileInfo == null)
        {
            return new ProjectGroupState(ProjectGroupStatus.ErrorSolutionFileNotFound);
        }

        // If the cache file does not exists or the solution changed
        // we need to reload it entirely
        var indexCacheFileInfo = FileUtilities.GetFileInfoNoThrow(IndexCacheFilePath);
        if (indexCacheFileInfo == null || _solutionLastWriteTimeWhenRead > indexCacheFileInfo.LastWriteTimeUtc)
        {
            // Delete the previous cache file if we need to recompute it anyway
            if (indexCacheFileInfo != null && indexCacheFileInfo.Exists)
            {
                indexCacheFileInfo.Delete();
            }
            return new ProjectGroupState(ProjectGroupStatus.Restore);
        }

        InitializeGraphFromCachedProjectGroup();
        
        return new ProjectGroupState(ProjectGroupStatus.Build);
    }


    public ProjectGroupState Build()
    {
        var results = _builder.Run(this, "Build");
        //var results = _builder.BuildSolution(this);
        return new ProjectGroupState(ProjectGroupStatus.NoChanges);
    }

    public ProjectGroupState Restore()
    {
        // A restore reset entirely the state of this instance
        Reset();
        var result = _builder.Restore(this);
        ProjectGroupState state;
        if (result.OverallResult == BuildResultCode.Success)
        {
            state = new ProjectGroupState(ProjectGroupStatus.Build);
            InitializeFromSolution();
            WriteCachedProjectGroupFromProjectInstance();
        }
        else
        {
            state = new ProjectGroupState(ProjectGroupStatus.Restore);
        }
        return state;
    }
    
    public void Reset()
    {
        _projectGraph = null;
        _cachedProjectGroup = null;
        _projectStates.Clear();
        _solutionLastWriteTimeWhenRead = DateTime.MinValue;
        if (_projectCollection.Count > 0)
        {
            _projectCollection.UnloadAllProjects();
        }
    }

    internal void InitializeFromSolution()
    {
        Reset();

        // Initialize the project graph
        var parallelism = 8;
        var entryPoint = new ProjectGraphEntryPoint(_builder.Config.SolutionFilePath, _projectCollection.GlobalProperties);
        // State the state of the group to restored by default
        // Will be evaluated by the ProjectGraph
        Restored = true;
        try
        {
            _solutionLastWriteTimeWhenRead = File.GetLastWriteTimeUtc(entryPoint.ProjectFile);
            _projectGraph = new ProjectGraph(new[] {entryPoint}, _projectCollection, CreateProjectInstance, parallelism, CancellationToken.None);
        }
        catch
        {
            Restored = false;
            throw;
        }

        // Group graph node with Project
        foreach (var graphNode in _projectGraph.ProjectNodes)
        {
            this.FindProjectState(graphNode).ProjectGraphNode = graphNode;
        }

        // Check
        bool hasErrors = false;
        foreach (var project in _projectCollection.LoadedProjects)
        {
            if (string.IsNullOrEmpty(project.GetPropertyValue("TargetFramework")))
            {
                Console.Error.WriteLine($"Error: the project {project.FullPath} is multi-targeting {project.GetPropertyValue("TargetFrameworks")}. Multi-targeting is currently not supported by this prototype");
                hasErrors = true;
            }
        }

        if (hasErrors)
        {
            throw new InvalidOperationException("Invalid error in project.");
        }
    }

    private ProjectInstance CreateProjectInstance(string solutionPath, IDictionary<string, string> globalProperties, ProjectCollection projectCollection)
    {
        // Always normalize the path (as we use it for mapping)
        solutionPath = FileUtilities.NormalizePath(solutionPath);

        // Need to lock as ProjectGraph can call this callback from multiple threads
        ProjectState projectState;
        lock (_projectStates)
        {
            if (!_projectStates.TryGetValue(solutionPath, out projectState!))
            {
                projectState = new ProjectState(this);
                _projectStates[solutionPath] = projectState;
            }
        }

        //var project = new Project(projectPath, globalProperties, projectCollection.DefaultToolsVersion, projectCollection);
        //projectState.ProjectInstance = project.CreateProjectInstance(); //new ProjectInstance(project.Xml, globalProperties, project.ToolsVersion, projectCollection);

        var xml = ProjectRootElement.Open(solutionPath, projectCollection);
        if (xml is null) throw new InvalidOperationException($"Unable to open solution {solutionPath}");
        var instance = new ProjectInstance(xml, globalProperties, projectCollection.DefaultToolsVersion, null, projectCollection, ProjectLoadSettings.RecordEvaluatedItemElements);
        projectState.InitializeFromProjectInstance(instance, xml.LastWriteTimeWhenRead);

        if (!projectState.Restored)
        {
            Restored = false;
        }
        //var instance = projectState.ProjectInstance;
        //var check = instance.ExpandString("$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)");

        return instance;
    }

    public (Project, ProjectInstance) ReloadProject(Project project)
    {
        ProjectCollection.UnloadProject(project);
        return (project, CreateProjectInstance(project.FullPath, project.GlobalProperties, ProjectCollection));
    }

    public void Dispose()
    {
        _projectCollection.UnloadAllProjects();
        _projectCollection.Dispose();
    }
}


public enum ProjectGroupStatus
{
    NoChanges,
    Restore,
    Build,
    ErrorSolutionFileNotFound,
}

public record struct ProjectGroupState
{
    public ProjectGroupState(ProjectGroupStatus status)
    {
        Status = status;
        ProjectsToBuild = Enumerable.Empty<ProjectGraphNode>();
    }

    public ProjectGroupState(ProjectGroupStatus status, IEnumerable<ProjectGraphNode> projectsToBuild)
    {
        Status = status;
        ProjectsToBuild = projectsToBuild;
    }

    public ProjectGroupStatus Status { get; }

    public IEnumerable<ProjectGraphNode> ProjectsToBuild { get; init; }

    public static implicit operator ProjectGroupState(ProjectGroupStatus status)
    {
        return new ProjectGroupState(status);
    }
}
