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
    private readonly ProjectGraph _projectGraph;

    public ProjectGroup(Builder builder, Dictionary<string, string> globalProperties)
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

        // Initialize the project graph
        // Seems that degreeOfParallelism doesn't change much when loading a project
        _projectGraph = new ProjectGraph(new[] { new ProjectGraphEntryPoint(_builder.RootProjectPath, _projectCollection.GlobalProperties) }, _projectCollection, CreateProjectInstance, 1, CancellationToken.None);
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

    private ProjectInstance CreateProjectInstance(string projectPath, Dictionary<string, string> globalProperties, ProjectCollection projectCollection)
    {
        Project project;
        lock (_projects)
        {
            _projects.TryGetValue(projectPath, out project);
        }

        if (project == null)
        {
            project = projectCollection.LoadProject(projectPath, globalProperties, projectCollection.DefaultToolsVersion);
            lock (_projects)
            {
                _projects[projectPath] = project;
            }
        }

        return _builder.BuildManager.GetProjectInstanceForBuild(project);
    }
}