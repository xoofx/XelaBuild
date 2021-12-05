using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Locator;

namespace BuildServer;

/// <summary>
/// Helper class to deal with a local copy of msbuild but also to allow to run MsBuild as a node
/// </summary>
public static class MsBuildHelper
{
    /// <summary>
    /// Returns true if the arguments are suggesting that it is msbuild launching a node
    /// </summary>
    /// <param name="args">The command line arguments</param>
    /// <returns><c>true</c> if it is msbuild launching a node; otherwise <c>false</c></returns>
    public static bool IsCommandLineArgsForMsBuild(string[] args)
    {
        // Bug in msbuild: https://github.com/dotnet/msbuild/pull/7013
        // MSBuild is trying to relaunch this process (instead of using dotnet), so we protect our usage here
        return args.Length > 0 && args.Any(x => x.StartsWith("/nodemode")) && args.Any(x => x.StartsWith("/nologo"));
    }

    /// <summary>
    /// Run msbuild.dll Main (act as if it was an msbuild node running)
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The exit code</returns>
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
        var latest = MSBuildLocator.QueryVisualStudioInstances().FirstOrDefault(x => x.Version.Major == 6);

        if (latest == null)
        {
            throw new InvalidOperationException($"No .NET 6.x SDKs found. Check installs:\n{string.Join("\n", MSBuildLocator.QueryVisualStudioInstances().Select(x => $"   {GetVisualStudioInstanceToString(x)}"))}");
        }

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


    private static string GetVisualStudioInstanceToString(VisualStudioInstance visualStudioInstance)
    {
        return $"SDK: {visualStudioInstance.Name} version: {visualStudioInstance.Version} type: {visualStudioInstance.DiscoveryType} msbuild: {visualStudioInstance.MSBuildPath} vs: {visualStudioInstance.VisualStudioRootPath}";
    }
}