using System;
using System.IO;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using XelaBuild.Core.Helpers;

namespace XelaBuild.Core;

public class ProjectState
{
    internal ProjectState(ProjectGroup group)
    {
        Group = group;
        ProjectStateHash = new ProjectStateHash();
    }

    public readonly ProjectGroup Group;

    //public Project Project;
    
    public ProjectInstance ProjectInstance;

    public ProjectGraphNode ProjectGraphNode;

    public BuildResult LastResult;

    public DateTime LastResultTime;

    public ProjectStateHash ProjectStateHash { get; }

    public string GetBuildResultCacheFilePath()
    {
        return Path.Combine(ProjectInstance.GetPropertyValue("IntermediateOutputPath"), $"{Path.GetFileName(ProjectInstance.FullPath)}.BuildResult.cache");
    }

    public string GetRestoreResultCacheFilePath()
    {
        return Path.Combine(ProjectInstance.GetPropertyValue("IntermediateOutputPath"), $"{Path.GetFileName(ProjectInstance.FullPath)}.RestoreResult.cache");
    }

    /*
    public bool CheckNeedRestore()
    {
        HashAndDateTime hashAndDateTime = default;
        var restoreSuccess = string.Equals(Project.GetPropertyValue("RestoreSuccess") ?? string.Empty, "true",
            StringComparison.InvariantCultureIgnoreCase);
        if (!restoreSuccess)
        {
            return true;
        }
        var packageFoldersString = Project.GetPropertyValue("NuGetPackageFolders");
        if (string.IsNullOrEmpty(packageFoldersString)) return default;
        var packageFolders = packageFoldersString.Split(';');

        hashAndDateTime = new HashAndDateTime(SpookyHash.SpookyConst, SpookyHash.SpookyConst, DateTime.MinValue);

        var packageReferences = Project.GetItems("PackageReference");
        foreach (var packageReference in packageReferences)
        {
            var packageName = packageReference.EvaluatedInclude;
            if (string.IsNullOrEmpty(packageName)) return false;
            var packageNameLower = packageName.ToLower();
            var versionString = packageReference.GetMetadataValue("Version");
            if (string.IsNullOrEmpty(versionString)) return false;

            if (!VersionRange.TryParse(versionString, out VersionRange versionRange))
            {
                // Invalid version
                return true;
            }

            string packageFolderFound = null;
            NuGetVersion versionFound = null;
            foreach (var packageFolder in packageFolders)
            {
                var nugetPackageDir = Path.Combine(packageFolder, packageNameLower);

                if (!Directory.Exists(nugetPackageDir)) continue;

                var versions = Directory.EnumerateDirectories(nugetPackageDir).Select(NuGetVersion.Parse);
                var bestVersion = versionRange.FindBestMatch(versions);
                if (bestVersion != null)
                {
                    packageFolderFound = nugetPackageDir;
                    versionFound = bestVersion;
                    break;
                }
            }

            if (versionFound == null)
            {
                return true;
            }
            
            var packageFile = Path.Combine(packageFolderFound, versionFound.OriginalVersion, $"{packageNameLower}.{versionFound.OriginalVersion}.nupkg");
            if (!File.Exists(packageFile))
            {
                return true;
            }

            HashFile(packageFile, ref hashAndDateTime);
        }

        if (hashAndDateTime != ProjectStateHash.HashPackageReferences)
        {
            // Update current hash before the restore
            var isNew = ProjectStateHash.HashPackageReferences == default;
            ProjectStateHash.HashPackageReferences = hashAndDateTime;
            return !isNew;
        }

        return false;
    }

    public bool CheckNeedReloadProject()
    {
        var hashAndDateTime = new HashAndDateTime(SpookyHash.SpookyConst, SpookyHash.SpookyConst, DateTime.MinValue);

        HashFile(Project.FullPath, Project.Xml.LastWriteTimeWhenRead, ref hashAndDateTime);

        foreach (var import in Project.Imports)
        {
            HashFile(import.ImportedProject.FullPath, import.ImportedProject.LastWriteTimeWhenRead, ref hashAndDateTime);
        }

        if (hashAndDateTime != ProjectStateHash.HashProjectAndTargetFiles)
        {
            // Update current hash before the restore
            var isNew = ProjectStateHash.HashProjectAndTargetFiles == default;
            ProjectStateHash.HashProjectAndTargetFiles = hashAndDateTime;
            return !isNew;
        }

        return false;
    }

    public bool CheckNeedRecompile()
    {
        var hashAndDateTime = new HashAndDateTime(SpookyHash.SpookyConst, SpookyHash.SpookyConst, DateTime.MinValue);

        foreach (var compileItem in Project.GetItems("Compile"))
        {
            HashItem(compileItem, ref hashAndDateTime);
        }

        foreach (var compileItem in Project.GetItems("Content"))
        {
            HashItem(compileItem, ref hashAndDateTime);
        }

        foreach (var compileItem in Project.GetItems("Reference"))
        {
            HashItem(compileItem, ref hashAndDateTime);
        }

        foreach (var compileItem in Project.GetItems("Analyzer"))
        {
            HashItem(compileItem, ref hashAndDateTime);
        }

        // Transitive hashing
        foreach (var project in ProjectGraphNode.ProjectReferences)
        {
            var otherProjectState = Group.FindProjectState(project.ProjectInstance.FullPath);
            hashAndDateTime = hashAndDateTime.Combine(otherProjectState.ProjectStateHash.HashGlobal);
        }

        if (hashAndDateTime != ProjectStateHash.HashCompileAndContentItems)
        {
            var isNew = ProjectStateHash.HashCompileAndContentItems == default;
            ProjectStateHash.HashCompileAndContentItems = hashAndDateTime;
            return !isNew;
        }

        return false;
    }

    private void HashItem(ProjectItem item, ref HashAndDateTime hashAndDateTime)
    {
        var filePath = item.EvaluatedInclude;
        var fileInfo = FileUtilities.GetFileInfoNoThrow(filePath);
        if (fileInfo == null) return;
        HashFile(filePath, fileInfo.LastWriteTimeUtc, ref hashAndDateTime);
    }



    private static void HashFile(string filePath, ref HashAndDateTime hashAndDateTime)
    {
        HashFile(filePath, File.GetLastWriteTimeUtc(filePath), ref hashAndDateTime);
    }

    private static void HashFile(string filePath, DateTime latestTime, ref HashAndDateTime hashAndDateTime)
    {
        SpookyHash.Hash128(filePath, out var hash1, out var hash2);
        var packageHashAndDateTime = new HashAndDateTime(hash1, hash2, latestTime);
        hashAndDateTime = hashAndDateTime.Combine(packageHashAndDateTime);
    }
    */
}

public class ProjectStateHash
{
    public HashAndDateTime HashPackageReferences;
    public HashAndDateTime HashProjectAndTargetFiles;
    public HashAndDateTime HashCompileAndContentItems;
    public HashAndDateTime HashProjectReferences;
    public HashAndDateTime HashAssemblyReferences;
    public HashAndDateTime HashGlobal;

    public void Update()
    {
        var hasher = new SpookyHash();
        hasher.Init();

        var maxModifiedTime = DateTime.MinValue;
        HashPackageReferences.UpdateToHash(ref hasher, ref maxModifiedTime);
        HashProjectAndTargetFiles.UpdateToHash(ref hasher, ref maxModifiedTime);
        HashCompileAndContentItems.UpdateToHash(ref hasher, ref maxModifiedTime);
        HashProjectReferences.UpdateToHash(ref hasher, ref maxModifiedTime);
        HashAssemblyReferences.UpdateToHash(ref hasher, ref maxModifiedTime);

        hasher.Final(out ulong hash1, out ulong hash2);
        HashGlobal = new HashAndDateTime(hash1, hash2, maxModifiedTime);
    }
}


public record HashAndDateTime(ulong Hash1, ulong Hash2, DateTime MaxModifiedTime)
{
    public void Write(BinaryWriter writer)
    {
        writer.Write(Hash1);
        writer.Write(Hash2);
        writer.Write(MaxModifiedTime.Ticks);
    }

    public HashAndDateTime Combine(HashAndDateTime hashAndDateTime)
    {
        return new HashAndDateTime(Hash1 ^ hashAndDateTime.Hash1, Hash2 ^ hashAndDateTime.Hash2,
            MaxModifiedTime > hashAndDateTime.MaxModifiedTime ? MaxModifiedTime : hashAndDateTime.MaxModifiedTime);
    }

    internal void UpdateToHash(ref SpookyHash hasher, ref DateTime maxModifiedTime)
    {
        hasher.Update(Hash1);
        hasher.Update(Hash2);
        hasher.Update(MaxModifiedTime.Ticks);
        if (MaxModifiedTime > maxModifiedTime)
        {
            maxModifiedTime = MaxModifiedTime;
        }
    }
}




