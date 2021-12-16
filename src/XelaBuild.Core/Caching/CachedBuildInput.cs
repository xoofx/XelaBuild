using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public class CachedBuildInputs : IVersionedTransferable<CachedBuildInputs>
{
    /// <summary>
    /// CBIF: Cached Build Inputs File
    /// </summary>
    public static readonly CachedMagicVersion CurrentMagicVersion = new("CBIF", 1, 0);

    public CachedBuildInputs()
    {
        MagicVersion = CurrentMagicVersion;
        InputItems = new List<CachedFileReference>();
        Assemblies = new List<CachedFileReference>();
    }
    
    public CachedMagicVersion MagicVersion { get; set; }
    public DateTime LastWriteTimeWhenRead { get; set; }

    public string ProjectFolder { get; set; }

    public List<CachedFileReference> InputItems { get; }

    public List<CachedFileReference> Assemblies { get; }


    public static CachedBuildInputs ReadFromFile(string filePath)
    {
        return CachedBinaryHelper.ReadFromFile<CachedBuildInputs>(filePath);
    }

    public void WriteToFile(string filePath)
    {
        CachedBinaryHelper.WriteToFile(filePath, this);
    }

    public CachedBuildInputs Read(TransferBinaryReader reader)
    {
        ProjectFolder = reader.ReadString();
        reader.ReadStructsToList(InputItems);
        reader.ReadStructsToList(Assemblies);
        return this;
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.Write(ProjectFolder);
        writer.WriteStructsFromList(InputItems);
        writer.WriteStructsFromList(Assemblies);
    }
}