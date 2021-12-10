using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace BuildServer.Tasks;

public class CacheBuilder : Task
{
    [Required]
    public ITaskItem[] AssemblyReferences { get; set; }

    [Required]
    public string OutputCacheFolder { get; set; }

    [Required]
    public ITaskItem[] Analyzers { get; set; }
        
    [Output]
    public ITaskItem[] CacheFiles { get; set; }

    public override bool Execute()
    {
        // Make sure that the folder is created
        try
        {
            if (!Directory.Exists(OutputCacheFolder))
            {
                Directory.CreateDirectory(OutputCacheFolder);
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"Unable to create cache folder at {OutputCacheFolder}. Reason: {ex.Message}");
            return false;
        }

        // For AssemblyReferences:
        //   Discard: ProjectReference => ReferenceSourceTarget=ProjectReference
        //   FrameworkReference => FrameworkReferenceName
        //   PackageReference => NuGetPackageId
        //   else AssemblyReference
        //
        // For Analyzers
        //   FrameworkReference => FrameworkReferenceName
        //   PackageReference => NuGetPackageId
        //   else AssemblyReference
        var map = new Dictionary<AssemblyGroupKey, AssemblyGroup>();
        try
        {
            Process(map, AssemblyReferences);
            Process(map, Analyzers);
        }
        catch (Exception ex)
        {
            Log.LogError($"Error while processing cache builder items. Reason: {ex}");
            return false;
        }

        // Write cache files
        var cacheFiles = new List<TaskItem>();
        bool hasErrors = false;
        foreach (var pair in map)
        {
            var key = pair.Key;
            var group = pair.Value;

            var filePath = group.GetFilePath(key, OutputCacheFolder);
            try
            {
                    
                group.TryWriteToFile(filePath);
                cacheFiles.Add(new TaskItem(filePath));
            }
            catch (Exception ex)
            {
                Log.LogError($"Error writing cache file {filePath}");
                hasErrors = true;
            }
        }

        CacheFiles = cacheFiles.ToArray<ITaskItem>();

        return !hasErrors;
    }

    private static void Process(Dictionary<AssemblyGroupKey, AssemblyGroup> map, ITaskItem[] items)
    {
        foreach (var assemblyRef in items)
        {
            // Discard ProjectReference
            if (assemblyRef.GetMetadata("ReferenceSourceTarget") == "ProjectReference")
            {
                continue;
            }

            var itemspec = FileUtilities.NormalizePath(assemblyRef.ItemSpec);
            SpookyHash.Hash128(itemspec, out var hash1, out var hash2);
            //var hash = HexHelper.ToString(hash1, hash2);

            AssemblyGroupKind kind;
            var name = assemblyRef.GetMetadata("FrameworkReferenceName");
            string version = null;
            if (!string.IsNullOrEmpty(name))
            {
                kind = AssemblyGroupKind.Framework;
                version = assemblyRef.GetMetadata("FrameworkReferenceVersion");
            }
            else
            {
                name = assemblyRef.GetMetadata("NuGetPackageId");
                if (!string.IsNullOrEmpty(name))
                {
                    kind = AssemblyGroupKind.Package;
                    version = assemblyRef.GetMetadata("NuGetPackageVersion");
                }
                else
                {
                    kind = AssemblyGroupKind.Dll;
                    name = Path.GetFileName(itemspec);
                }
            }

            var key = new AssemblyGroupKey(kind, name, version);

            if (!map.TryGetValue(key, out var assemblyGroup))
            {
                assemblyGroup = new AssemblyGroup();
                map.Add(key, assemblyGroup);
            }

            // Update the hash of the group, use XOR to avoid having to sort items
            assemblyGroup.Hash1 ^= hash1;
            assemblyGroup.Hash2 ^= hash2;
            var item = new FilePathAndTime(itemspec, FileUtilities.GetLastModifiedTimeUtc(itemspec));
            if (item.LastWriteTimeUtc > assemblyGroup.MaxModifiedTime)
            {
                assemblyGroup.MaxModifiedTime = item.LastWriteTimeUtc;
            }
            assemblyGroup.Items.Add(item);
        }
    }
}