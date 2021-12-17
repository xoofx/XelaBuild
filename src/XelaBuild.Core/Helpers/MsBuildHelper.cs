using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Locator;

namespace XelaBuild.Core.Helpers;

/// <summary>
/// Helper class to deal with a local copy of msbuild but also to allow to run MsBuild as a node
/// </summary>
public static class MsBuildHelper
{
    public static void RegisterCustomMsBuild()
    {
        // "C:\Program Files\dotnet\dotnet.exe"
        var processFileName = Process.GetCurrentProcess().MainModule?.FileName;
        var folder = processFileName is null ? null : Path.GetDirectoryName(processFileName);
        var dotnetFolder = GetDotnet6Folder(folder);

        // Otherwise try to get it from ProgramFiles
        if (dotnetFolder == null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            dotnetFolder = GetDotnet6Folder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"));
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
                     ["MSBuildSDKsPath"] = Path.Combine(dotnetFolder, "Sdks")
                 })
        {
            Environment.SetEnvironmentVariable(keyValuePair.Key, keyValuePair.Value);
        }

        MSBuildLocator.RegisterMSBuildPath(new[]
        {
            msbuildPath,
        });
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