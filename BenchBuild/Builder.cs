namespace BenchBuild;

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;

/// <summary>
/// Simple hosting of BuildManager from msbuild
/// </summary>
class Builder
{
    private readonly ProjectCollection _projectCollection;
    private readonly EvaluationContext _context;
    private readonly Dictionary<string, Project> _projects;
    private readonly Dictionary<string, string> _globalProperties;
    private readonly BuildManager _manager;
    private string _rootProjectPath;

    public Builder(string rootProject)
    {
        _globalProperties = new Dictionary<string, string>()
        {
            { "Configuration", "Release" },
            { "Platform", "AnyCPU" },
            //{"BuildProjectReferences", "false"},
        };
        _manager = new BuildManager();
        _projectCollection = new ProjectCollection(_globalProperties);
        _projects = new Dictionary<string, Project>();
        _context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        _rootProjectPath = rootProject;
    }

    private Project LoadProject(string projectPath, Dictionary<string, string> globalProperties)
    {
        lock (_projects)
        {
            if (!_projects.TryGetValue(projectPath, out var project))
            {
                var clock = Stopwatch.StartNew();
                project = Project.FromFile(projectPath, new ProjectOptions()
                {
                    GlobalProperties = globalProperties,
                    EvaluationContext = _context,
                    ProjectCollection = _projectCollection
                });
                _projects.Add(projectPath, project);
                //Console.WriteLine($"Project {projectPath} loaded in {clock.Elapsed.TotalMilliseconds}");
            }
            return project;
        }
    }

    private ProjectInstance ProjectInstanceFactory(string projectPath, Dictionary<string, string> globalproperties, ProjectCollection projectcollection)
    {
        var project = LoadProject(projectPath, globalproperties);
        // Copy global properties to local properties
        foreach (var prop in _globalProperties)
        {
            globalproperties[prop.Key] = prop.Value;
        }
        return new ProjectInstance(project.Xml, globalproperties, projectcollection.DefaultToolsVersion, projectcollection);
    }

    public int Count => _projectCollection.LoadedProjects.Count;


    public double TimeProjectGraph { get; private set; }


    public void Build(string target)
    {
        const string buildCache = "build.cache";

        var parameters = new BuildParameters
        {
            DisableInProcNode = false,
            Loggers = new List<ILogger>()
            {
                //new ConsoleLogger(LoggerVerbosity.Minimal)
            },
            //InputResultsCacheFiles = new []{ buildCache },
            ResetCaches = true, // should it be true? (doesn't affect anything in this benchmark)
        };

        var clock = Stopwatch.StartNew();
        var projectGraph = new ProjectGraph(_rootProjectPath, _projectCollection, ProjectInstanceFactory);
        TimeProjectGraph = clock.Elapsed.TotalMilliseconds;
        clock.Restart();

        //// Not working
        //parameters.IsolateProjects = true;
        //if (File.Exists("project.cache"))
        //{
        //    parameters.InputResultsCacheFiles = new[] { buildCache };
        //}
        //else
        //{
        //    parameters.OutputResultsCacheFile = buildCache;
        //}

        //var request = new GraphBuildRequestData(projectGraph, new List<string>() { target }, null, BuildRequestDataFlags.ProvideProjectStateAfterBuild);
        var request = new GraphBuildRequestData(projectGraph, new List<string>() { target });

        _manager.BeginBuild(parameters);
        try
        {
            var submission = _manager.PendBuildRequest(request);
            var result = submission.Execute();

            //// graph needs to have BuildRequestDataFlags.ProvideProjectStateAfterBuild);
            //foreach (var projectResult in result.ResultsByNode.Values)
            //{
            //    var projectAfterBuild = projectResult.ProjectStateAfterBuild;
            //    Console.WriteLine($"{projectAfterBuild.FullPath} Items: {projectAfterBuild.Items.Count}");
            //    foreach (var element in projectAfterBuild.Items)
            //    {
            //        if (element.ItemType.StartsWith("_") || !element.EvaluatedInclude.EndsWith(".cs")) continue;
            //        Console.WriteLine($"-> {element.ItemType} {element.EvaluatedInclude}");
            //    }
            //}
        }
        finally
        {
            _manager.EndBuild();
        }
    }
}