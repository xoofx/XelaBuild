using System;
using System.IO;
using XelaBuild.Core.Helpers;

namespace XelaBuild.Core;

public record BuildConfiguration
{
    private BuildConfiguration(string solutionFilePath, string globalCacheFolder)
    {
        SolutionFilePath = solutionFilePath;
        GlobalCacheFolder = globalCacheFolder;
    }

    public string SolutionFilePath { get; }
    public string GlobalCacheFolder { get; }

    public static BuildConfiguration Create(string solutionFilePath)
    {
        solutionFilePath = Path.Combine(Environment.CurrentDirectory, solutionFilePath);
        solutionFilePath = FileUtilities.NormalizePath(solutionFilePath);
        var rootFolder = Path.GetDirectoryName(solutionFilePath);

        var vsFolder = Path.Combine(rootFolder, ".vs");
        var vsFolderInfo = new DirectoryInfo(vsFolder);
        if (!vsFolderInfo.Exists)
        {
            vsFolderInfo.Create();
            vsFolderInfo.Attributes |= FileAttributes.Hidden;
        }

        var globalCacheFolder = Path.Combine(vsFolder, Path.GetFileNameWithoutExtension(solutionFilePath), "xelabuild");
        DirectoryHelper.EnsureDirectory(globalCacheFolder);

        return new BuildConfiguration(solutionFilePath, globalCacheFolder);
    }
}

