using System;
using XelaBuild.Core;
using XelaBuild.Core.Helpers;

//var clock = Stopwatch.StartNew();
MsBuildHelper.RegisterCustomMsBuild();
int exitCode;
if (MsBuildHelper.IsCommandLineArgsForMsBuild(args))
{
    exitCode = MsBuildHelper.Run(args);
}
else
{
    var builderApp = new BuilderApp();
    exitCode = builderApp.Run(args);
}
Environment.ExitCode = exitCode;
//Console.WriteLine($"Elapsed: {clock.Elapsed.TotalMilliseconds}ms");