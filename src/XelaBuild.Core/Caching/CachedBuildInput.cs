using System.Collections.Generic;
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
        CompileAndContentItems = new List<CachedFileReference>();
        Assemblies = new List<CachedFileReference>();
    }
    
    public CachedMagicVersion MagicVersion { get; set; }

    public List<CachedFileReference> CompileAndContentItems { get; }

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
        reader.ReadStructsToList(CompileAndContentItems);
        reader.ReadStructsToList(Assemblies);
        return this;
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.WriteStructsFromList(CompileAndContentItems);
        writer.WriteStructsFromList(Assemblies);
    }
}