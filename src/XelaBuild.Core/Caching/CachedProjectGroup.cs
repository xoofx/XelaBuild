using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public class CachedProjectGroup : BinaryRootTransferable<CachedProjectGroup>
{
    /// <summary>
    /// Cached Project Group File
    /// </summary>
    public static readonly MagicVersion CurrentVersion = new("CPGF", 1, 0);

    public CachedProjectGroup()
    {
        MagicVersion = CurrentVersion;
        Projects = new List<CachedProject>();
    }

    public CachedFileReference SolutionFile;

    /// <summary>
    /// List of cached projects in reverse topological order.
    /// </summary>
    public List<CachedProject> Projects { get; }

    public static CachedProjectGroup ReadFromFile(string filePath)
    {
        return BinaryTransfer.ReadFromFile<CachedProjectGroup>(filePath);
    }

    public void WriteToFile(string filePath)
    {
        BinaryTransfer.WriteToFile(filePath, this);
    }

    public override CachedProjectGroup Read(BinaryTransferReader reader)
    {
        SolutionFile.Read(reader);
        reader.ReadObjectsToList(Projects);
        return this;
    }

    public override void Write(BinaryTransferWriter writer)
    {
        SolutionFile.Write(writer);
        writer.WriteObjectsFromList(Projects);
    }
}


[DebuggerDisplay("{ToDebuggerDisplay(),nq}")]
public class CachedProject : IBinaryTransferable<CachedProject>
{
    public CachedProject()
    {
        ProjectFolder = string.Empty;
        File = CachedFileReference.Empty;
        Globs = new List<CachedGlobItem>();
        ProjectAssetsCachedFile = CachedFileReference.Empty;
        BuildInputsCacheFile = CachedFileReference.Empty;
        BuildResultCacheFile = CachedFileReference.Empty;
        ProjectReferences = new List<CachedProjectReference>();
        Imports = new List<CachedImportFileReference>();
        ProjectReferenceTargets = new List<CachedProjectReferenceTargets>();
        ProjectDependencies = new List<CachedProject>();
    }

    public bool IsRoot { get; set; }
    public string ProjectFolder { get; set; }
    public CachedFileReference File;
    public List<CachedGlobItem> Globs { get; }
    public bool IsRestoreSuccessful { get; set; }
    public CachedFileReference ProjectAssetsCachedFile;
    public CachedFileReference BuildInputsCacheFile;
    public CachedFileReference BuildResultCacheFile;
    public List<CachedProjectReference> ProjectReferences { get; }
    public List<CachedProject> ProjectDependencies { get; }
    public List<CachedImportFileReference> Imports { get; }
    public Hash128 HashImports;
    public List<CachedProjectReferenceTargets> ProjectReferenceTargets { get; }
    
    public CachedProject Read(BinaryTransferReader reader)
    {
        IsRoot = reader.ReadBoolean();
        ProjectFolder = reader.ReadString();
        File.Read(reader);
        reader.ReadObjectsToList(this.Globs);
        IsRestoreSuccessful = reader.ReadBoolean();

        CachedFileReference cachedFileReference = default;
        ProjectAssetsCachedFile = reader.ReadStruct(cachedFileReference);
        BuildInputsCacheFile = reader.ReadStruct(cachedFileReference);
        BuildResultCacheFile = reader.ReadStruct(cachedFileReference);

        reader.ReadObjectsToList(ProjectReferences);
        reader.ReadObjectsToList(ProjectDependencies);
        reader.ReadObjectsToList(Imports);
        HashImports = reader.ReadStruct(HashImports);
        reader.ReadObjectsToList(ProjectReferenceTargets);
        return this;
    }

    public void Write(BinaryTransferWriter writer)
    {
        writer.Write(this.IsRoot);
        writer.Write(ProjectFolder);
        writer.WriteStruct(File);
        writer.WriteObjectsFromList(Globs);
        writer.Write(IsRestoreSuccessful);

        writer.WriteStruct(ProjectAssetsCachedFile);
        writer.WriteStruct(BuildInputsCacheFile);
        writer.WriteStruct(BuildResultCacheFile);

        writer.WriteObjectsFromList(ProjectReferences);
        writer.WriteObjectsFromList(ProjectDependencies);
        writer.WriteObjectsFromList(Imports);
        writer.WriteStruct(HashImports);
        writer.WriteObjectsFromList(ProjectReferenceTargets);
    }

    private string ToDebuggerDisplay()
    {
        return $"{Path.GetFileName(File.FullPath)}, References={ProjectReferences.Count}";
    }
}

public record struct CachedProperty(string Name, string Value) : IBinaryTransferable<CachedProperty>
{
    public CachedProperty Read(BinaryTransferReader reader)
    {
        Name = reader.ReadStringShared() ?? string.Empty;
        Value = reader.ReadStringShared() ?? string.Empty;
        return this;
    }

    public void Write(BinaryTransferWriter writer)
    {
        writer.WriteStringShared(Name);
        writer.WriteStringShared(Value);
    }
}

public class CachedProjectReference : IBinaryTransferable<CachedProjectReference>
{
    public CachedProject? Project { get; set; }
    public string? GlobalPropertiesToRemove { get; set; }
    public string? SetConfiguration { get; set; }
    public string? SetPlatform { get; set; }
    public string? SetTargetFramework { get; set; }
    public string? Properties { get; set; }
    public string? AdditionalProperties { get; set; }
    public string? UndefinedProperties { get; set; }

    public CachedProjectReference Read(BinaryTransferReader reader)
    {
        Project = reader.ReadObject(new CachedProject());
        GlobalPropertiesToRemove = reader.ReadStringShared();
        SetConfiguration = reader.ReadStringShared();
        SetPlatform = reader.ReadStringShared();
        SetTargetFramework = reader.ReadStringShared();
        Properties = reader.ReadStringShared();
        AdditionalProperties = reader.ReadStringShared();
        UndefinedProperties = reader.ReadStringShared();
        return this;
    }

    public void Write(BinaryTransferWriter writer)
    {
        writer.WriteObject(Project);
        writer.WriteStringShared(GlobalPropertiesToRemove);
        writer.WriteStringShared(SetConfiguration);
        writer.WriteStringShared(SetPlatform);
        writer.WriteStringShared(SetTargetFramework);
        writer.WriteStringShared(Properties);
        writer.WriteStringShared(AdditionalProperties);
        writer.WriteStringShared(UndefinedProperties);
    }
}

public class CachedProjectReferenceTargets : IBinaryTransferable<CachedProjectReferenceTargets>
{
    public CachedProjectReferenceTargets()
    {
        Include = string.Empty;
        Targets = string.Empty;
    }

    public string Include { get; set; }

    public string Targets { get; set; }

    public bool? OuterBuild { get; set; }

    public CachedProjectReferenceTargets Read(BinaryTransferReader reader)
    {
        Include = reader.ReadStringShared() ?? string.Empty;
        Targets = reader.ReadStringShared() ?? string.Empty;
        OuterBuild = reader.ReadNullableBoolean();
        return this;
    }

    public void Write(BinaryTransferWriter writer)
    {
        writer.WriteStringShared(Include);
        writer.WriteStringShared(Targets);
        writer.WriteNullable(OuterBuild);
    }
}

public class CachedGlobItem : IBinaryTransferable<CachedGlobItem>
{
    public CachedGlobItem()
    {
        ItemType = string.Empty;
        Include = string.Empty;
    }

    public string ItemType { get; set; }

    public string Include { get; set; }

    public string? Remove { get; set; }

    public string? Exclude { get; set; }

    public CachedGlobItem Read(BinaryTransferReader reader)
    {
        ItemType = reader.ReadStringShared() ?? string.Empty;
        Include = reader.ReadStringShared() ?? string.Empty;
        Remove = reader.ReadStringShared();
        Exclude = reader.ReadStringShared();
        return this;
    }

    public void Write(BinaryTransferWriter writer)
    {
        writer.WriteStringShared(ItemType);
        writer.WriteStringShared(Include);
        writer.WriteStringShared(Remove);
        writer.WriteStringShared(Exclude);
    }
}