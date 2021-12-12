using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using XelaBuild.Core;

namespace XelaBuild;

public class Benchmarker
{
#if DEBUG
    const int RunCount = 1;
#else
    const int RunCount = 5;
#endif
    public static void Run(string rootProject)
    {
        var rootFolder = Path.GetDirectoryName(rootProject);

        // ------------------------------------------------------------------------------------------------------------------------
        var clock = Stopwatch.StartNew();

        DumpHeader("Load Projects and graph");
        clock.Restart();
        //var provider = ProjectsProvider.FromList(Directory.EnumerateFiles(rootFolder, "*.csproj", SearchOption.AllDirectories), Path.Combine(rootFolder, "build"));
        var provider = ProjectsProvider.FromList(new[] { rootProject }, Path.Combine(rootFolder, ".vs", Path.GetFileNameWithoutExtension(rootProject), "xelabuild"));

        // Setup env variable used by XelaBuild.targets
        Environment.SetEnvironmentVariable("XelaBuildCacheDir", provider.BuildFolder);
        
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

        int index = 0;
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
                () =>
                {
                    var result = builder.RunRootOnly(@group, "Restore");
#if DEBUG
                    Console.WriteLine("Press key to attach to msbuild");
                    Console.ReadLine();
#endif
                    return result;
                }),
            ("Build All (Clean)",
                null, //() => builder.Run(group, "Clean"),
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

            for (int i = 0; i < RunCount; i++)
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
}