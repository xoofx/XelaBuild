using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Locator;

namespace BuildProcess;

public static class BuildProcessApp
{
    public static int Run(string[] args)
    {
        var msbuildPath = RegisterCustomMsBuild();
        var assemblyPath = Path.Combine(msbuildPath, "MSBuild.dll");

        var assembly = Assembly.LoadFile(assemblyPath);

        var type = assembly.GetType("Microsoft.Build.CommandLine.MSBuildApp");

        var mainMethod = type.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        return (int)mainMethod.Invoke(null, new object[] { args });
    }

    public static string RegisterCustomMsBuild()
    {
        var latest = MSBuildLocator.QueryVisualStudioInstances().First(x => x.Version.Major == 6);

        Environment.SetEnvironmentVariable("MSBuildEnableWorkloadResolver", "false");

        // Custom registration with our custom msbuild
#if DEBUG
        var msbuildPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\..\..\dotnet\msbuild\artifacts\bin\MSBuild\Debug\net6.0\");
#else
        var msbuildPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\..\..\dotnet\msbuild\artifacts\bin\MSBuild\Release\net6.0\");
#endif
        if (!Directory.Exists(msbuildPath))
        {
            throw new InvalidOperationException($"folder {msbuildPath} does not exist");
        }

        // Copy any existing file from current SDK to the local msbuild
        foreach (var file in Directory.EnumerateFiles(latest.MSBuildPath))
        {
            var destFile = Path.Combine(msbuildPath, Path.GetFileName(file));
            if (!File.Exists(destFile))
            {
                File.Copy(file, destFile);
            }
        }
        
        foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
                 {
                     ["MSBUILD_EXE_PATH"] = msbuildPath + "MSBuild.dll",
                     ["MSBuildExtensionsPath"] = msbuildPath,
                     ["MSBuildSDKsPath"] = latest.MSBuildPath + "Sdks"
                 })
        {
            Environment.SetEnvironmentVariable(keyValuePair.Key, keyValuePair.Value);
        }

        MSBuildLocator.RegisterMSBuildPath(new[]
        {
            msbuildPath,
        });
        return msbuildPath;
    }


}