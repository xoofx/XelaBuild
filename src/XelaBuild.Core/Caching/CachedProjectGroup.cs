using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public class CachedProjectGroup : IVersionedTransferable<CachedProjectGroup>
{
    /// <summary>
    /// Cached Project Group File
    /// </summary>
    public static readonly CachedMagicVersion CurrentVersion = new("CPGF", 1, 0);

    public CachedProjectGroup()
    {
        MagicVersion = CurrentVersion;
        Projects = new List<CachedProject>();
    }

    public CachedMagicVersion MagicVersion { get; set; }
    public DateTime LastWriteTimeWhenRead { get; set; }

    public CachedFileReference SolutionFile;

    /// <summary>
    /// List of cached projects in reverse topological order.
    /// </summary>
    public List<CachedProject> Projects { get; }

    public static CachedProjectGroup ReadFromFile(string filePath)
    {
        return CachedBinaryHelper.ReadFromFile<CachedProjectGroup>(filePath);
    }

    public void WriteToFile(string filePath)
    {
        CachedBinaryHelper.WriteToFile(filePath, this);
    }

    public CachedProjectGroup Read(TransferBinaryReader reader)
    {
        SolutionFile.Read(reader);
        reader.ReadObjectsToList(Projects);
        return this;
    }

    public void Write(TransferBinaryWriter writer)
    {
        SolutionFile.Write(writer);
        writer.WriteObjectsFromList(Projects);
    }
}


[DebuggerDisplay("{ToDebuggerDisplay(),nq}")]
public class CachedProject : ITransferable<CachedProject>
{
    public CachedProject()
    {
        ProjectReferences = new List<CachedProjectReference>();
        Imports = new List<CachedImportFileReference>();
        Globs = new List<CachedGlobItem>();
        //Properties = new List<CachedProperty>();
        ProjectReferenceTargets = new List<CachedProjectReferenceTargets>();
        ProjectDependencies = new List<CachedProject>();
    }

    public bool IsRoot { get; set; }
    public string ProjectFolder { get; set; }
    public CachedFileReference File;
    public List<CachedGlobItem> Globs { get; }
    //public List<CachedProperty> Properties { get; }
    public bool IsRestoreSuccessful { get; set; }
    public CachedFileReference ProjectAssetsCachedFile;
    public CachedFileReference BuildInputsCacheFile;
    public CachedFileReference BuildResultCacheFile;
    public List<CachedProjectReference> ProjectReferences { get; }
    public List<CachedProject> ProjectDependencies { get; }
    public List<CachedImportFileReference> Imports { get; }
    public List<CachedProjectReferenceTargets> ProjectReferenceTargets { get; }
    
    public CachedProject Read(TransferBinaryReader reader)
    {
        IsRoot = reader.ReadBoolean();
        ProjectFolder = reader.ReadString();
        File.Read(reader);
        reader.ReadObjectsToList(this.Globs);
        //reader.ReadStructsToList(this.Properties);
        IsRestoreSuccessful = reader.ReadBoolean();

        CachedFileReference cachedFileReference = default;
        ProjectAssetsCachedFile = reader.ReadStruct(cachedFileReference);
        BuildInputsCacheFile = reader.ReadStruct(cachedFileReference);
        BuildResultCacheFile = reader.ReadStruct(cachedFileReference);

        reader.ReadObjectsToList(ProjectReferences);
        reader.ReadObjectsToList(ProjectDependencies);
        reader.ReadObjectsToList(Imports);
        reader.ReadObjectsToList(ProjectReferenceTargets);
        return this;
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.Write(this.IsRoot);
        writer.Write(ProjectFolder);
        writer.WriteStruct(File);
        writer.WriteObjectsFromList(Globs);
        //writer.WriteStructsFromList(Properties);
        writer.Write(IsRestoreSuccessful);

        writer.WriteStruct(ProjectAssetsCachedFile);
        writer.WriteStruct(BuildInputsCacheFile);
        writer.WriteStruct(BuildResultCacheFile);

        writer.WriteObjectsFromList(ProjectReferences);
        writer.WriteObjectsFromList(ProjectDependencies);
        writer.WriteObjectsFromList(Imports);
        writer.WriteObjectsFromList(ProjectReferenceTargets);
    }

    private string ToDebuggerDisplay()
    {
        return $"{Path.GetFileName(File.FullPath)}, References={ProjectReferences.Count}";
    }
}

public record struct CachedProperty(string Name, string Value) : ITransferable<CachedProperty>
{
    public CachedProperty Read(TransferBinaryReader reader)
    {
        Name = reader.ReadStringShared();
        Value = reader.ReadStringShared();
        return this;
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.WriteStringShared(Name);
        writer.WriteStringShared(Value);
    }
}



public class CachedProjectReference : ITransferable<CachedProjectReference>
{
    public CachedProject Project { get; set; }

    public string GlobalPropertiesToRemove { get; set; }
    public string SetConfiguration { get; set; }
    public string SetPlatform { get; set; }
    public string SetTargetFramework { get; set; }
    public string Properties { get; set; }
    public string AdditionalProperties { get; set; }
    public string UndefinedProperties { get; set; }

    public CachedProjectReference Read(TransferBinaryReader reader)
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

    public void Write(TransferBinaryWriter writer)
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

public class CachedProjectReferenceTargets : ITransferable<CachedProjectReferenceTargets>
{
    public string Include { get; set; }

    public string Targets { get; set; }

    public bool? OuterBuild { get; set; }

    public CachedProjectReferenceTargets Read(TransferBinaryReader reader)
    {
        Include = reader.ReadStringShared();
        Targets = reader.ReadStringShared();
        OuterBuild = reader.ReadNullableBoolean();
        return this;
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.WriteStringShared(Include);
        writer.WriteStringShared(Targets);
        writer.WriteNullable(OuterBuild);
    }
}

public class CachedGlobItem : ITransferable<CachedGlobItem>
{
    public string ItemType { get; set; }

    public string Include { get; set; }

    public string Remove { get; set; }

    public string Exclude { get; set; }

    public CachedGlobItem Read(TransferBinaryReader reader)
    {
        ItemType = reader.ReadStringShared();
        Include = reader.ReadStringShared();
        Remove = reader.ReadStringShared();
        Exclude = reader.ReadStringShared();
        return this;
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.WriteStringShared(ItemType);
        writer.WriteStringShared(Include);
        writer.WriteStringShared(Remove);
        writer.WriteStringShared(Exclude);
    }
}


