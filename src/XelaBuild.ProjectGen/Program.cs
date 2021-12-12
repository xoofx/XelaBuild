using System;
using System.IO;
using XelaBuild.ProjectGen;

// Generate projects for benchmarking
if (args.Length != 1)
{
    Console.WriteLine($"Usage: {Path.GetFileName(typeof(Program).Assembly.Location)} [folder]");
    Environment.Exit(1);
}

var folder = Path.Combine(Environment.CurrentDirectory, args[0]);
if (!Directory.Exists(folder))
{
    Directory.CreateDirectory(folder);
}

ProjectGenerator.Generate(folder);