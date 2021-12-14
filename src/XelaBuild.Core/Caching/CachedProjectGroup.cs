using System.Collections.Generic;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public class CachedProjectGroup : ITransferable<CachedProjectGroup>
{
    public CachedProjectGroup()
    {
        Projects = new List<CachedProject>();
    }

    public CachedFileReference SolutionFile;

    public List<CachedProject> Projects { get; }
    public CachedProjectGroup Read(TransferBinaryReader reader)
    {
        throw new System.NotImplementedException();
    }

    public void Write(TransferBinaryWriter writer)
    {
        throw new System.NotImplementedException();
    }
}


public class CachedProject : ITransferable<CachedProject>
{
    public CachedProject()
    {
        ProjectReferences = new List<CachedProjectReference>();
        Imports = new List<CachedFileReference>();
        Globs = new List<CachedGlobItem>();
        ProjectReferenceTargets = new List<CachedProjectReferenceTargets>();
        ProjectDependencies = new List<CachedProject>();
    }

    public int Index { get; internal set; }
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
    public List<CachedFileReference> Imports { get; }
    public List<CachedProjectReferenceTargets> ProjectReferenceTargets { get; }
    public CachedProject Read(TransferBinaryReader reader)
    {
        throw new System.NotImplementedException();
    }

    public void Write(TransferBinaryWriter writer)
    {
        throw new System.NotImplementedException();
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
        throw new System.NotImplementedException();
    }

    public void Write(TransferBinaryWriter writer)
    {
        throw new System.NotImplementedException();
    }
}

public class CachedProjectReferenceTargets : ITransferable<CachedProjectReferenceTargets>
{
    public string Include { get; set; }

    public string Targets { get; set; }

    public bool? OuterBuild { get; set; }

    public CachedProjectReferenceTargets Read(TransferBinaryReader reader)
    {
        return new CachedProjectReferenceTargets()
        {
            Include = reader.ReadString(),
            Targets = reader.ReadString(),
            OuterBuild = reader.ReadNullableBoolean(),
        };
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.Write(Include);
        writer.Write(Targets);
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
        return new CachedGlobItem()
        {
            ItemType = reader.ReadString(),
            Include = reader.ReadString(),
            Remove = reader.ReadString(),
            Exclude = reader.ReadString(),
        };
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.Write(ItemType);
        writer.Write(Include);
        writer.Write(Remove);
        writer.Write(Exclude);
    }
}


