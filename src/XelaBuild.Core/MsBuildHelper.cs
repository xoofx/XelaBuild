using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Locator;

namespace XelaBuild.Core;

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
        return MSBuildApp.Main(args);
    }

    public static void RegisterCustomMsBuild()
    {
        var latest = MSBuildLocator.QueryVisualStudioInstances().FirstOrDefault(x => x.Version.Major == 6);

        if (latest == null)
        {
            throw new InvalidOperationException($"No .NET 6.x SDKs found. Check installs:\n{string.Join("\n", MSBuildLocator.QueryVisualStudioInstances().Select(x => $"   {GetVisualStudioInstanceToString(x)}"))}");
        }

        Environment.SetEnvironmentVariable("MSBuildEnableWorkloadResolver", "false");
        var msbuildPath = Path.GetFullPath(Path.GetDirectoryName(typeof(MSBuildApp).Assembly.Location));

        // Try to load from 
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var check = Path.Combine(Path.GetDirectoryName(latest.MSBuildPath), name.Name);
            var path = check + ".dll";
            if (File.Exists(path))
            {
                return context.LoadFromAssemblyPath(path);
            }
            else
            {
                path = check + ".exe";
                if (File.Exists(path))
                {
                    return context.LoadFromAssemblyPath(path);
                }
            }

            return null;
        };
        
        foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
                 {
                     ["MSBUILD_EXE_PATH"] = Path.Combine(msbuildPath, "MSBuild.dll"),
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
    }

    private static string GetVisualStudioInstanceToString(VisualStudioInstance visualStudioInstance)
    {
        return $"SDK: {visualStudioInstance.Name} version: {visualStudioInstance.Version} type: {visualStudioInstance.DiscoveryType} msbuild: {visualStudioInstance.MSBuildPath} vs: {visualStudioInstance.VisualStudioRootPath}";
    }
}