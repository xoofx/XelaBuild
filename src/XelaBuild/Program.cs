using System;
using System.IO;
using XelaBuild;
using XelaBuild.Core;
using XelaBuild.Core.Helpers;

// Make sure that we are using our local copy of msbuild
MsBuildHelper.RegisterCustomMsBuild();

// Bug in msbuild: https://github.com/dotnet/msbuild/pull/7013
// MSBuild is trying to relaunch this process (instead of using dotnet), so we protect our usage here
// Also, if `dotnet.exe` is not 2 folders above msbuild.dll, as it is not the case in our local build, then it will use this exe as the msbuild server process
if (MsBuildHelper.IsCommandLineArgsForMsBuild(args))
{
    var exitCode = MsBuildHelper.Run(args);
    Environment.Exit(exitCode);
    return;
}

// Override msbuild targets to use our special targets file to inject our tasks
Environment.SetEnvironmentVariable("CustomAfterMicrosoftCommonTargets", Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "XelaBuild.targets")));

if (args.Length != 1 || !args[0].EndsWith(".sln"))
{
    Console.WriteLine("xelabuild [path_to_solution.sln]");
    Environment.Exit(1);
    return;
}

var solutionPath = Path.Combine(Environment.CurrentDirectory, args[0]);

// BEGIN
// ------------------------------------------------------------------------------------------------------------------------
Benchmarker.Run(solutionPath);

// This need to run in a separate method to allow msbuild to load the .NET assemblies before in MsBuildHelper.RegisterCustomMsBuild.
