// See https://aka.ms/new-console-template for more information

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BenchBuild;
using BuildProcess;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.Build.Locator;

// Bug in msbuild: https://github.com/dotnet/msbuild/pull/7013
// MSBuild is trying to relaunch this process (instead of using dotnet), so we protect our usage here
if (args.Length > 0 && args.Any(x => x.StartsWith("/nodemode") || x.StartsWith("/nologo")))
{
    var exitCode = BuildProcessApp.Run(args);
    Environment.Exit(exitCode);
    return;
}

// BEGIN
// ------------------------------------------------------------------------------------------------------------------------
//foreach (var instance in MSBuildLocator.QueryVisualStudioInstances())
//{
//    Console.WriteLine($"{instance.Name} {instance.Version} {instance.VisualStudioRootPath} {instance.MSBuildPath}");
//}
BuildProcessApp.RegisterCustomMsBuild();
//MSBuildLocator.RegisterInstance(latest);

// ------------------------------------------------------------------------------------------------------------------------
DumpHeader("Generate Projects");
var rootProject = ProjectGenerator.Generate();
Console.WriteLine($"RootProject {rootProject}");

RunBenchmark(rootProject);

static void RunBenchmark(string rootProject)
{
    var rootFolder = Path.GetDirectoryName(Path.GetDirectoryName(rootProject));
    // ------------------------------------------------------------------------------------------------------------------------
    DumpHeader("Load Projects");
    var clock = Stopwatch.StartNew();
    var builder = new Builder(rootProject)
    {
        UseGraph = true
    };
    Console.WriteLine($"Time to load: {clock.Elapsed.TotalMilliseconds}ms");

    // ------------------------------------------------------------------------------------------------------------------------
    DumpHeader("Restore Projects");
    clock.Restart();
    builder.BasicBuild("Restore");
    Console.WriteLine($"=== Time to Restore {builder.Count} projects: {clock.Elapsed.TotalMilliseconds}ms");

    if (Debugger.IsAttached)
    {
        Console.WriteLine("Press key to attach to msbuild");
        Console.ReadLine();
    }

    // ------------------------------------------------------------------------------------------------------------------------
    DumpHeader("Build caches");
    clock.Restart();
    //Environment.SetEnvironmentVariable("MSBUILDDEBUGONSTART", "2");
    var graph = builder.BuildCache();
    Console.WriteLine($"=== Time to Build Cache {clock.Elapsed.TotalMilliseconds}ms");

    int index = 0;
    Builder.MaxMsBuildNodeCount = 10;
    const int runCount = 5;
    // ------------------------------------------------------------------------------------------------------------------------
    foreach (var (kind, prepare, build) in new (string, Action, Func<Dictionary<ProjectGraphNode, BuildResult>>)[]
            {
            ("Build All (Clean)",
                () => builder.BasicBuild("Clean"),
                () => builder.BuildParallelWithCache(graph, "Build")
            ),
            ("Build Root - No Changes",
                null,
                () => builder.BuildRootOnlyWithParallelCache(graph, "Build")
            ),
            ("Build Root - 1 C# file changed in root", 
                () => System.IO.File.SetLastWriteTimeUtc(Path.Combine(rootFolder, "LibRoot", "LibRootClass.cs"), DateTime.UtcNow),
                () => builder.BuildRootOnlyWithParallelCache(graph, "Build")
            ),
            ("Build All - 1 C# file changed in leaf", 
                () => File.WriteAllText(Path.Combine(rootFolder, "LibLeaf", "LibLeafClass.cs"), $@"namespace LibLeaf;
public static class LibLeafClass {{
    public static void Run() {{
        // empty
    }}
    public static void Change{index}() {{ }}
}}
"),
                () => builder.BuildParallelWithCache(graph, "Build")
            )
            })
    {

        DumpHeader(kind);

        for (int i = 0; i < runCount; i++)
        {
            prepare?.Invoke();

            clock.Restart();

            var results = build();

            Console.WriteLine($"[{i}] Time to build {results.Count} projects: {clock.Elapsed.TotalMilliseconds}ms");
        }

        index++;
    }
}

// END
// **************************************************************

static void DumpHeader(string header)
{
    Console.WriteLine("============================================================================");
    Console.WriteLine(header);
    Console.WriteLine("****************************************************************************");
}