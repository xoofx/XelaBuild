using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    private readonly string _indexCacheFilePath;
    private ProjectGraph _projectGraph;
    private DateTime _solutionLastWriteTimeWhenRead;
    private CachedProjectGroup _cachedProjectGroup;

    internal ProjectGroup(Builder builder, IReadOnlyDictionary<string, string> globalProperties)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (globalProperties == null) throw new ArgumentNullException(nameof(globalProperties));

        var properties = new Dictionary<string, string>(globalProperties)
        {
            ["UnityBuildServer"] = "true",
            ["IsGraphBuild"] = "true", // Make this upfront to include it in the cache file names
            //["RestoreRecursive"] = "false",
        };

        _projectStates = new Dictionary<string, ProjectState>();
        _projectCollection = new ProjectCollection(properties, null, null, ToolsetDefinitionLocations.Default, builder.MaxNodeCount, false, true, builder.ProjectCollectionRootElementCache);
        _builder = builder;

        var hashPostFix = HashHelper.Hash128(properties);
        _indexCacheFilePath = Path.Combine(_builder.Config.GlobalCacheFolder, $"{properties["Configuration"]}-{properties["Platform"]}-index-{hashPostFix}.cache");
    }

    public int Count => _projectCollection.LoadedProjects.Count;

    public bool Restored { get; private set; }

    public ProjectCollection ProjectCollection => _projectCollection;

    public ProjectGraph ProjectGraph => _projectGraph;

    public IEnumerable<ProjectState> Projects
    {
        get
        {
            foreach (var projectNode in _projectGraph.ProjectNodesTopologicallySorted)
            {
                yield return this.FindProjectState(projectNode);
            }
        }
    }

    public string IndexCacheFilePath => _indexCacheFilePath;

    public ProjectState FindProjectState(string projectPath)
    {
        if (projectPath == null) throw new ArgumentNullException(nameof(projectPath));
        _projectStates.TryGetValue(projectPath, out var projectState);
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
        //var indexCacheFileInfo = FileUtilities.GetFileInfoNoThrow(IndexCacheFilePath);
        //if (indexCacheFileInfo == null || _solutionLastWriteTimeWhenRead > indexCacheFileInfo.LastWriteTimeUtc)
        //{
        //    // Delete the previous cache file if we need to recompute it anyway
        //    if (indexCacheFileInfo != null && indexCacheFileInfo.Exists)
        //    {
        //        indexCacheFileInfo.Delete();
        //    }
        //    return new ProjectGroupState(ProjectGroupStatus.Restore);
        //}

        // Here the cache file exists, we can load it.
        // var cachedProjectGroup = CachedProjectGroup.ReadFromFile(IndexCacheFilePath);

        //InitializeFromSolution();
        //WriteCachedProjectGroupFromProjectInstance();

        //_builder.Restore(this);

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

    private ProjectInstance CreateProjectInstance(string projectPath, IDictionary<string, string> globalProperties, ProjectCollection projectCollection)
    {
        // Always normalize the path (as we use it for mapping)
        projectPath = FileUtilities.NormalizePath(projectPath);

        // Need to lock as ProjectGraph can call this callback from multiple threads
        ProjectState projectState;
        lock (_projectStates)
        {
            if (!_projectStates.TryGetValue(projectPath, out projectState))
            {
                projectState = new ProjectState(this);
                _projectStates[projectPath] = projectState;
            }
        }

        //var project = new Project(projectPath, globalProperties, projectCollection.DefaultToolsVersion, projectCollection);
        //projectState.ProjectInstance = project.CreateProjectInstance(); //new ProjectInstance(project.Xml, globalProperties, project.ToolsVersion, projectCollection);

        var xml = ProjectRootElement.Open(projectPath, projectCollection);
        var instance = new ProjectInstance(xml, globalProperties, projectCollection.DefaultToolsVersion, null, projectCollection, ProjectLoadSettings.RecordEvaluatedItemElements);
        projectState.InitializeFromProjectInstance(instance, xml.LastWriteTimeWhenRead);

        if (!projectState.Restored)
        {
            Restored = false;
        }
        //var instance = projectState.ProjectInstance;
        //var check = instance.ExpandString("$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)");

        return projectState.ProjectInstance;
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

public record ProjectGroupState(ProjectGroupStatus Status)
{
    public IReadOnlyCollection<ProjectGraphNode> ProjectsToBuild { get; init; }
}
