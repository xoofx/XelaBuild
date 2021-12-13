// See https://aka.ms/new-console-template for more information

using System;
using System.IO;
using System.Reflection;

var assembly = Assembly.LoadFile(Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "NuGet.Build.Tasks.Console.dll"));
assembly.EntryPoint.Invoke(null, new object[] { args });
