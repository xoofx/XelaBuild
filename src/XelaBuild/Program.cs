using System;
using XelaBuild.Core;
using XelaBuild.Core.Helpers;

//var clock = Stopwatch.StartNew();
MsBuildHelper.RegisterCustomMsBuild();
var builderApp = new BuilderApp();
var exitCode = builderApp.Run(args);
Environment.ExitCode = exitCode;
//Console.WriteLine($"Elapsed: {clock.Elapsed.TotalMilliseconds}ms");