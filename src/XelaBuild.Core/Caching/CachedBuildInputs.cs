using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public class CachedBuildInputs : BinaryRootTransferable<CachedBuildInputs>
{
    /// <summary>
    /// CBIF: Cached Build Inputs File
    /// </summary>
    public static readonly MagicVersion CurrentMagicVersion = new("CBIF", 1, 0);

    public CachedBuildInputs()
    {
        ProjectFolder = string.Empty;
        MagicVersion = CurrentMagicVersion;
        InputItems = new List<CachedFileReference>();
        Assemblies = new List<CachedFileReference>();
    }
    
    public string ProjectFolder { get; set; }

    public List<CachedFileReference> InputItems { get; }

    public List<CachedFileReference> Assemblies { get; }


    public static CachedBuildInputs ReadFromFile(string filePath)
    {
        return BinaryTransfer.ReadFromFile<CachedBuildInputs>(filePath);
    }

    public void WriteToFile(string filePath)
    {
        BinaryTransfer.WriteToFile(filePath, this);
    }

    public override CachedBuildInputs Read(BinaryTransferReader reader)
    {
        ProjectFolder = reader.ReadString();
        reader.ReadStructsToList(InputItems);
        reader.ReadStructsToList(Assemblies);
        return this;
    }

    public override void Write(BinaryTransferWriter writer)
    {
        writer.Write(ProjectFolder);
        writer.WriteStructsFromList(InputItems);
        writer.WriteStructsFromList(Assemblies);
    }
}