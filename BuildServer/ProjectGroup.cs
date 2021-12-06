using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

namespace BuildServer;

/// <summary>
/// Simple hosting of BuildManager from msbuild
/// </summary>
public class ProjectGroup
{
    private readonly ProjectCollection _projectCollection;
    private readonly Dictionary<string, Project> _projects;
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

        _projects = new Dictionary<string, Project>();
        _projectCollection = new ProjectCollection(properties, null, null, ToolsetDefinitionLocations.Default, builder.MaxNodeCount, false, true, builder.ProjectCollectionRootElementCache);
        _builder = builder;
    }

    public int Count => _projectCollection.LoadedProjects.Count;

    public ProjectCollection ProjectCollection => _projectCollection;

    public ProjectGraph ProjectGraph => _projectGraph;

    public Project FindProject(string projectPath)
    {
        if (projectPath == null) throw new ArgumentNullException(nameof(projectPath));
        lock (_projects)
        {
            _projects.TryGetValue(projectPath, out var project);
            return project;
        }
    }
        
    internal void InitializeGraph()
    {
        // Initialize the project graph
        var parallelism = 8;
        var entryPoints = _builder.Provider.GetProjectPaths().Select(x => new ProjectGraphEntryPoint(x, _projectCollection.GlobalProperties));

        _projectGraph = new ProjectGraph(entryPoints, _projectCollection, CreateProjectInstance, parallelism, CancellationToken.None);
    }

    private ProjectInstance CreateProjectInstance(string projectPath, Dictionary<string, string> globalProperties, ProjectCollection projectCollection)
    {
        // Don't use projectCollection.LoadProject it is locking more projectCollection
        var project = new Project(projectPath, globalProperties, projectCollection.DefaultToolsVersion, projectCollection);
        lock (_projects)
        {
            _projects[projectPath] = project;
        }
        var instance = new ProjectInstance(project.Xml, globalProperties, project.ToolsVersion, projectCollection);
        return instance;
    }
}