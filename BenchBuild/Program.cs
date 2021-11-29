// See https://aka.ms/new-console-template for more information

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BenchBuild;
using Microsoft.Build.Construction;
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