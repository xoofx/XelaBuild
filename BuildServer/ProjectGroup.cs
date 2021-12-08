using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

namespace BuildServer;

/// <summary>
/// Simple hosting of BuildManager from msbuild
/// </summary>
public class ProjectGroup : IDisposable
{
    private readonly ProjectCollection _projectCollection;
    private readonly Builder _builder;
    private ProjectGraph _projectGraph;

    internal ProjectGroup(Builder builder, IReadOnlyDictionary<string, string> globalProperties)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (globalProperties == null) throw new ArgumentNullException(nameof(globalProperties));

        var properties = new Dictionary<string, string>(globalProperties)
        {
            ["IsGraphBuild"] = "true" // Make this upfront to include it in the cache file names
        };

        _projectCollection = new ProjectCollection(properties, null, null, ToolsetDefinitionLocations.Default, builder.MaxNodeCount, false, true, builder.ProjectCollectionRootElementCache);
        _builder = builder;
    }

    public int Count => _projectCollection.LoadedProjects.Count;

    public ProjectCollection ProjectCollection => _projectCollection;

    public ProjectGraph ProjectGraph => _projectGraph;

    public Project FindProject(string projectPath)
    {
        if (projectPath == null) throw new ArgumentNullException(nameof(projectPath));
        return _projectCollection.LoadedProjects.FirstOrDefault(x => x.FullPath == projectPath);
    }
        
    internal void InitializeGraph()
    {
        // Initialize the project graph
        var parallelism = 8;
        var entryPoints = _builder.Provider.GetProjectPaths().Select(x => new ProjectGraphEntryPoint(x, _projectCollection.GlobalProperties));
        _projectGraph = new ProjectGraph(entryPoints, _projectCollection, CreateProjectInstance, parallelism, CancellationToken.None);
    }

    private ProjectInstance CreateProjectInstance(string projectPath, IDictionary<string, string> globalProperties, ProjectCollection projectCollection)
    {
        // Don't use projectCollection.LoadProject it is locking more projectCollection
        var project = new Project(projectPath, globalProperties, projectCollection.DefaultToolsVersion, projectCollection);
        var instance = new ProjectInstance(project.Xml, globalProperties, project.ToolsVersion, projectCollection);
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