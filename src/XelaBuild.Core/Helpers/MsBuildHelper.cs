using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Locator;

namespace XelaBuild.Core.Helpers;

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
        // "C:\Program Files\dotnet\dotnet.exe"
        var processFileName = Process.GetCurrentProcess().MainModule?.FileName;
        var folder = processFileName is null ? null : Path.GetDirectoryName(processFileName);
        var dotnetFolder = GetDotnet6Folder(folder);

        // Otherwise try to get it from ProgramFiles
        if (dotnetFolder == null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            dotnetFolder =
                GetDotnet6Folder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "dotnet"));
        }

        // Otherwise go to the much slower route through MSBuildLocator (which is launching the dotnet process)
        if (dotnetFolder == null)
        {
            var latest = MSBuildLocator.QueryVisualStudioInstances().FirstOrDefault(x => x.Version.Major == 6);

            if (latest == null)
            {
                throw new InvalidOperationException(
                    $"No .NET 6.x SDKs found. Check installs:\n{string.Join("\n", MSBuildLocator.QueryVisualStudioInstances().Select(x => $"   {GetVisualStudioInstanceToString(x)}"))}");
            }

            dotnetFolder = latest.MSBuildPath;
        }

        Environment.SetEnvironmentVariable("MSBuildEnableWorkloadResolver", "false");
        var msbuildPath = Path.GetFullPath(AppContext.BaseDirectory);
        foreach (var keyValuePair in new Dictionary<string, string>()
                 {
                     ["MSBUILD_EXE_PATH"] = Path.Combine(msbuildPath, "MSBuild.dll"),
                     ["MSBuildExtensionsPath"] = dotnetFolder,
                     ["MSBuildSDKsPath"] = Path.Combine(dotnetFolder, "Sdks"),
                     ["NuGetRestoreTargets"] = Path.Combine(dotnetFolder, "NuGet.targets")
                 })
        {
            Environment.SetEnvironmentVariable(keyValuePair.Key, keyValuePair.Value);
        }

        //MSBuildLocator.RegisterMSBuildPath();
        var msbuildSearchPaths = new[]
        {
            msbuildPath,
            dotnetFolder
        };

        var loadedAssemblies = new Dictionary<string, Assembly>();
        AssemblyLoadContext.Default.Resolving += DefaultOnResolving;

        Assembly? DefaultOnResolving(AssemblyLoadContext arg1, AssemblyName assemblyName)
        {
            Dictionary<string, Assembly> dictionary = loadedAssemblies;
            lock (dictionary) {
                if (loadedAssemblies.TryGetValue(assemblyName.FullName, out var assembly))
                    return assembly;
                foreach (string msbuildSearchPath in msbuildSearchPaths)
                {
                    string str = Path.Combine(msbuildSearchPath, assemblyName.Name + ".dll");
                    if (File.Exists(str))
                    {
                        assembly = Assembly.LoadFrom(str);
                        loadedAssemblies.Add(assemblyName.FullName, assembly);
                        return assembly;
                    }
                }

                return null;
            }
        }
    }

    private static string? GetDotnet6Folder(string? dotnetRoot)
    {
        if (dotnetRoot == null) return null;
        var folder = new DirectoryInfo(Path.Combine(dotnetRoot, "sdk", "6.0.100"));
        return folder.Exists ? folder.FullName : null;
    }

    private static string GetVisualStudioInstanceToString(VisualStudioInstance visualStudioInstance)
    {
        return $"SDK: {visualStudioInstance.Name} version: {visualStudioInstance.Version} type: {visualStudioInstance.DiscoveryType} msbuild: {visualStudioInstance.MSBuildPath} vs: {visualStudioInstance.VisualStudioRootPath}";
    }
}

