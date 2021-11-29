// See https://aka.ms/new-console-template for more information

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BenchBuild;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging;

// BEGIN
// ------------------------------------------------------------------------------------------------------------------------
DumpHeader("Generate Projects");
var rootProject = ProjectGenerator.Generate();
Console.WriteLine($"RootProject {rootProject}");

// ------------------------------------------------------------------------------------------------------------------------
//foreach (var instance in MSBuildLocator.QueryVisualStudioInstances())
//{
//    Console.WriteLine($"{instance.Name} {instance.Version} {instance.VisualStudioRootPath} {instance.MSBuildPath}");
//}
var latest = MSBuildLocator.QueryVisualStudioInstances().First(x => x.Version.Major == 6);
MSBuildLocator.RegisterInstance(latest);

// ------------------------------------------------------------------------------------------------------------------------
DumpHeader("Load Projects");
var clock = Stopwatch.StartNew();
var builder = new Builder(rootProject);
Console.WriteLine($"Time to load: {clock.Elapsed.TotalMilliseconds}ms");

// ------------------------------------------------------------------------------------------------------------------------
DumpHeader("Restore Projects");
clock.Restart();
builder.Build("Restore");
Console.WriteLine($"=== Time to Restore {builder.Count} projects: {clock.Elapsed.TotalMilliseconds}ms");

// ------------------------------------------------------------------------------------------------------------------------
DumpHeader("Build Projects (Benchmark)");
for (int i = 0; i < 10; i++)
{
    //System.IO.File.SetLastWriteTimeUtc(@"C:\code\lunet\lunet\src\Lunet\Program.cs", DateTime.UtcNow);
    clock.Restart();
    builder.Build("Build");
    Console.WriteLine($"[{i}] Time to build {builder.Count} projects: {clock.Elapsed.TotalMilliseconds}ms (ProjectGraph: {builder.TimeProjectGraph}ms)");
}

// END
// **************************************************************

static void DumpHeader(string header)
{
    Console.WriteLine("============================================================================");
    Console.WriteLine(header);
    Console.WriteLine("****************************************************************************");
}

class Builder
{
    private readonly ProjectCollection _projectCollection;
    private readonly EvaluationContext _context;
    private readonly Dictionary<string, Project> _projects;
    private readonly Dictionary<string, string> _globalProperties;
    private string _rootProjectPath;

    public Builder(string rootProject)
    {
        _globalProperties = new Dictionary<string, string>()
        {
            { "Configuration", "Release" },
            { "Platform", "AnyCPU" },
            //{"BuildProjectReferences", "false"},
        };
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
        };

        var clock = Stopwatch.StartNew();
        var projectGraph = new ProjectGraph(_rootProjectPath, _projectCollection, ProjectInstanceFactory);
        TimeProjectGraph = clock.Elapsed.TotalMilliseconds;
        clock.Restart();

        using var manager = new BuildManager();

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

        manager.BeginBuild(parameters);
        try
        {
            var submission = manager.PendBuildRequest(request);
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
            manager.EndBuild();
        }
    }
}


