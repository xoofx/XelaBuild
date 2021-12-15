using System.Collections.Generic;
using System.IO;
using System.Text;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public class CachedProjectGroup : ITransferable<CachedProjectGroup>
{
    public CachedProjectGroup()
    {
        Projects = new List<CachedProject>();
    }

    public CachedFileReference SolutionFile;

    /// <summary>
    /// List of cached projects in reverse topological order.
    /// </summary>
    public List<CachedProject> Projects { get; }
    

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

    internal class Reader : TransferBinaryReader
    {
        public Reader(Stream input) : this(input, Encoding.Default)
        {
        }

        public Reader(Stream input, Encoding encoding) : this(input, encoding, false)
        {
        }

        public Reader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {
            Projects = new Dictionary<CachedProject, int>();
            OrderedProjects = new List<CachedProject>();
            Imports = new Dictionary<CachedImportFileReference, int>();
            OrderedImports = new List<CachedImportFileReference>();
        }

        public Dictionary<CachedProject, int> Projects { get; }

        public List<CachedProject> OrderedProjects { get; }

        public Dictionary<CachedImportFileReference, int> Imports { get; }

        public List<CachedImportFileReference> OrderedImports { get; }
    }

    internal class Writer : TransferBinaryWriter
    {
        public Writer(Stream input) : this(input, Encoding.Default)
        {
        }

        public Writer(Stream input, Encoding encoding) : this(input, encoding, false)
        {
        }

        public Writer(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {
            Projects = new Dictionary<CachedProject, int>();
            Imports = new Dictionary<CachedImportFileReference, int>();
        }

        public Dictionary<CachedProject, int> Projects { get; }

        public Dictionary<CachedImportFileReference, int> Imports { get; }
    }
}


public class CachedProject : ITransferable<CachedProject>
{
    public CachedProject()
    {
        ProjectReferences = new List<CachedProjectReference>();
        Imports = new List<CachedImportFileReference>();
        Globs = new List<CachedGlobItem>();
        ProjectReferenceTargets = new List<CachedProjectReferenceTargets>();
        ProjectDependencies = new List<CachedProject>();
    }

    public bool IsRoot { get; set; }
    public string ProjectFolder { get; set; }
    public CachedFileReference File;
    public List<CachedGlobItem> Globs { get; }
    public bool IsRestoreSuccessful { get; set; }
    public CachedFileReference? ProjectAssetsCachedFile;
    public CachedFileReference? BuildInputsCacheFile;
    public CachedFileReference? BuildResultCacheFile;
    public List<CachedProjectReference> ProjectReferences { get; }
    public List<CachedProject> ProjectDependencies { get; }
    public List<CachedImportFileReference> Imports { get; }
    public List<CachedProjectReferenceTargets> ProjectReferenceTargets { get; }
    
    public CachedProject Read(TransferBinaryReader readerArg)
    {
        var reader = (CachedProjectGroup.Reader)readerArg;
        var kind = reader.ReadByte();
        if (kind == 1)
        {
            var id = reader.ReadInt32();
            return reader.OrderedProjects[id];
        }
        else if (kind == 2)
        {
            var id = reader.Projects.Count;
            reader.OrderedProjects.Add(this);
            reader.Projects.Add(this, id);

            this.IsRoot = reader.ReadBoolean();
            this.ProjectFolder = reader.ReadString();
            this.File.Read(reader);
            reader.ReadObjectsToList(this.Globs);
            this.IsRestoreSuccessful = reader.ReadBoolean();

            CachedFileReference cachedFileReference = default;
            ProjectAssetsCachedFile = reader.ReadNullableStruct(cachedFileReference);
            BuildInputsCacheFile = reader.ReadNullableStruct(cachedFileReference);
            BuildResultCacheFile = reader.ReadNullableStruct(cachedFileReference);

            reader.ReadObjectsToList(ProjectReferences);
            reader.ReadObjectsToList(ProjectDependencies);
            reader.ReadObjectsToList(Imports);
            reader.ReadObjectsToList(ProjectReferenceTargets);

            return this;
        }

        throw new InvalidDataException($"Invalid reference kind {kind} to a CachedProject. Expecting only 1 or 2.");
    }

    public void Write(TransferBinaryWriter writerArg)
    {
        var writer = (CachedProjectGroup.Writer)writerArg;
        if (writer.Projects.TryGetValue(this, out var projectIndex))
        {
            writer.Write((byte)1);
            writer.Write(projectIndex);
        }
        else
        {
            writer.Write((byte)2);
            var id = writer.Projects.Count;
            writer.Projects.Add(this, id);

            writer.Write(this.IsRoot);
            writer.Write(ProjectFolder);
            writer.WriteStruct(File);
            writer.WriteObjectsFromList(Globs);
            writer.Write(IsRestoreSuccessful);

            writer.WriteNullableStruct(ProjectAssetsCachedFile);
            writer.WriteNullableStruct(BuildInputsCacheFile);
            writer.WriteNullableStruct(BuildResultCacheFile);

            writer.WriteObjectsFromList(ProjectReferences);
            writer.WriteObjectsFromList(ProjectDependencies);
            writer.WriteObjectsFromList(Imports);
            writer.WriteObjectsFromList(ProjectReferenceTargets);
        }
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
        GlobalPropertiesToRemove = reader.ReadNullableString();
        SetConfiguration = reader.ReadNullableString();
        SetPlatform = reader.ReadNullableString();
        SetTargetFramework = reader.ReadNullableString();
        Properties = reader.ReadNullableString();
        AdditionalProperties = reader.ReadNullableString();
        UndefinedProperties = reader.ReadNullableString();
        return this;
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.WriteObject(Project);
        writer.WriteNullable(GlobalPropertiesToRemove);
        writer.WriteNullable(SetConfiguration);
        writer.WriteNullable(SetPlatform);
        writer.WriteNullable(SetTargetFramework);
        writer.WriteNullable(Properties);
        writer.WriteNullable(AdditionalProperties);
        writer.WriteNullable(UndefinedProperties);
    }
}

public class CachedProjectReferenceTargets : ITransferable<CachedProjectReferenceTargets>
{
    public string Include { get; set; }

    public string Targets { get; set; }

    public bool? OuterBuild { get; set; }

    public CachedProjectReferenceTargets Read(TransferBinaryReader reader)
    {
        Include = reader.ReadString();
        Targets = reader.ReadString();
        OuterBuild = reader.ReadNullableBoolean();
        return this;
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
        ItemType = reader.ReadString();
        Include = reader.ReadString();
        Remove = reader.ReadString();
        Exclude = reader.ReadString();
        return this;
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.Write(ItemType);
        writer.Write(Include);
        writer.Write(Remove);
        writer.Write(Exclude);
    }
}


