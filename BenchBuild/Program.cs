// See https://aka.ms/new-console-template for more information

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BenchBuild;
using BuildProcess;
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
    builder.Build("Restore");
    Console.WriteLine($"=== Time to Restore {builder.Count} projects: {clock.Elapsed.TotalMilliseconds}ms");

    // ------------------------------------------------------------------------------------------------------------------------
    DumpHeader("Build caches");
    clock.Restart();
    var graph = builder.BuildCache();
    Console.WriteLine($"=== Time to Build Cache {clock.Elapsed.TotalMilliseconds}ms");

    // ------------------------------------------------------------------------------------------------------------------------
    foreach (var (index, kind) in new (int, string)[]
            {
//            (0, "Build All (Clean)"), 
            (3, "Build All - No Changes"),
            (1, "Build All - 1 C# file changed in root"),
//            (2, "Build All - 1 C# file changed in leaf"),
            })
    {

        DumpHeader(kind);

        for (int i = 0; i < 1; i++)
        {
            if (index == 1)
            {
                System.IO.File.SetLastWriteTimeUtc(Path.Combine(rootFolder, "LibRoot", "LibRootClass.cs"), DateTime.UtcNow);
            }
            else if (index == 2)
            {
                File.WriteAllText(Path.Combine(rootFolder, "LibLeaf", "LibLeafClass.cs"), $@"namespace LibLeaf;
public static class LibLeafClass {{
    public static void Run() {{
        // empty
    }}
    public static void Change{i}() {{ }}
}}
");
                //System.IO.File.SetLastWriteTimeUtc(Path.Combine(rootFolder, "LibLeaf", "LibLeafClass.cs"), DateTime.UtcNow);
            }
            else if (index == 0)
            {
                // Full clean before a build all
                builder.Build("Clean");
            }
            clock.Restart();
            //builder.Build("Build", true);
            builder.BuildProjectWithCache(graph, "Build");
            Console.WriteLine($"[{i}] Time to build {builder.Count} projects: {clock.Elapsed.TotalMilliseconds}ms");
        }
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