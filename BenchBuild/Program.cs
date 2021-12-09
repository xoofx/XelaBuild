using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;using System.Runtime.CompilerServices;
using BenchBuild;
using BuildServer;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

// Bug in msbuild: https://github.com/dotnet/msbuild/pull/7013
// MSBuild is trying to relaunch this process (instead of using dotnet), so we protect our usage here
// Also, if `dotnet.exe` is not 2 folders above msbuild.dll, as it is the case in our local build, then it will use this exe has the msbuild server process
if (MsBuildHelper.IsCommandLineArgsForMsBuild(args))
{
    var exitCode = MsBuildHelper.Run(args);
    Environment.Exit(exitCode);
    return;
}

// BEGIN
// ------------------------------------------------------------------------------------------------------------------------
// Make sure that we are using our local copy of msbuild
MsBuildHelper.RegisterCustomMsBuild();

// ------------------------------------------------------------------------------------------------------------------------
// generate testing projects if necessary
var rootProject = Path.Combine(Environment.CurrentDirectory, "projects", "LibRoot", "LibRoot.csproj");
DumpHeader("Generate Projects");
rootProject = ProjectGenerator.Generate();

RunBenchmark(rootProject);

// This need to run in a separate method to allow msbuild to load the .NET assemblies before in MsBuildHelper.RegisterCustomMsBuild.
static void RunBenchmark(string rootProject)
{
    var rootFolder = Path.GetDirectoryName(Path.GetDirectoryName(rootProject));
    
    // ------------------------------------------------------------------------------------------------------------------------
    var clock = Stopwatch.StartNew();

    DumpHeader("Load Projects and graph");
    clock.Restart();
    //var provider = ProjectsProvider.FromList(Directory.EnumerateFiles(rootFolder, "*.csproj", SearchOption.AllDirectories), Path.Combine(rootFolder, "build"));
    var provider = ProjectsProvider.FromList(new[] { rootProject }, Path.Combine(rootFolder, "build"));
    using var builder = new Builder(provider);
    var group = builder.LoadProjectGroup(ConfigurationHelper.Release());
    Console.WriteLine($"Time to load and evaluate {group.Count} projects: {clock.Elapsed.TotalMilliseconds}ms");

    //clock.Restart();
    //var countReload = 100;
    //for (int i = 0; i < countReload; i++)
    //{
    //    var libroot = group.ProjectCollection.LoadedProjects.First(x => x.FullPath.Contains("LibRoot"));
    //    group.ReloadProject(libroot);
    //}
    //Console.WriteLine($"Time to reload {clock.Elapsed.TotalMilliseconds / countReload}ms");
    //return;

    if (Debugger.IsAttached)
    {
        Console.WriteLine("Press key to attach to msbuild");
        Console.ReadLine();
    }

    int index = 0;
    const int runCount = 5;
    // ------------------------------------------------------------------------------------------------------------------------
    foreach (var (kind, prepare, build) in new (string, Action, Func<IReadOnlyDictionary<ProjectGraphNode, BuildResult>>)[]
            {
            ("Load All Projects",
                null,
                () =>
                {
                    builder.LoadProjectGroup(ConfigurationHelper.Release());
                    return null;
                }),
            ("Restore All",
                null,
                () => builder.RunRootOnly(group, "Restore")
            ),
            ("Build All (Clean)",
                () => builder.Run(group, "Clean"),
                () => builder.Run(group, "Build")
            ),
            ("Build All - No changes",
                null,
                () => builder.Run(group, "Build")
            ),
            ("Build Root - No Changes",
                null,
                () => builder.RunRootOnly(group, "Build")
            ),
            ("Build Root - 1 C# file changed in root", 
                () => System.IO.File.SetLastWriteTimeUtc(Path.Combine(rootFolder, "LibRoot", "LibRootClass.cs"), DateTime.UtcNow),
                () => builder.RunRootOnly(group, "Build")
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
                () => builder.Run(group, "Build")
            )
            })
    {

        DumpHeader(kind);

        for (int i = 0; i < runCount; i++)
        {
            prepare?.Invoke();

            clock.Restart();

            var results = build();
            ResultsHelper.Verify(results);

            Console.WriteLine($"[{i}] Time to build {results?.Count ?? 0} projects: {clock.Elapsed.TotalMilliseconds}ms");
        }

        index++;
    }
}

// END
// **************************************************************

static void DumpHeader(string header)
{
    if (DumpHeaderState.DumpHeaderCount > 0)
    {
        Console.WriteLine();
    }
    DumpHeaderState.DumpHeaderCount++;
    Console.WriteLine("============================================================================");
    Console.WriteLine(header);
    Console.WriteLine("****************************************************************************");
}

static class DumpHeaderState
{
    public static int DumpHeaderCount = 0;
}

