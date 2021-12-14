using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XelaBuild.Core.Helpers;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public class CachedAssemblyGroup : ITransferable<CachedAssemblyGroup>
{
    private const uint Magic = 0x43494243; // "CBIC"
    private const uint Version = 0x0001_0000;

    public CachedAssemblyGroup()
    {
        Items = new List<CachedFileReference>();
        MaxModifiedTime = DateTime.MinValue;
    }

    public ulong Hash1;

    public ulong Hash2;

    public DateTime MaxModifiedTime;

    public List<CachedFileReference> Items { get; }

    public static CachedAssemblyGroup ReadFromFile(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadFromStream(stream);
    }

    // Format of the file
    // uint: magic "CBIC" 0x43494243 (CacheBuilderIndexCache)
    // uint: version 0x0001_0000
    // ulong: Hash1
    // ulong: Hash2
    // long: MaxModifiedTime
    // int: number of entries
    // entry+: long time (tick_utc), int length (number of of UTF 8 bytes), length bytes

    public static CachedAssemblyGroup ReadFromStream(Stream stream)
    {
        var group = new CachedAssemblyGroup();
        using var reader = new TransferBinaryReader(stream, Encoding.Default, true);

        if (reader.ReadUInt32() != Magic) throw new InvalidDataException("Invalid Magic Number");
        var version = reader.ReadUInt32();
        if (version != Version) throw new InvalidDataException($"Invalid Version {version} instead of {Version} only supported");

        group.Read(reader);
        return group;
    }

    public string GetFilePath(CachedAssemblyGroupKey key, string folder)
    {
        // Hash on disk
        // fwk-$(FrameworkReferenceName)-$(FrameworkReferenceVersion)-(hash).cache
        // pkg-$(NuGetPackageId)-$(NuGetPackageVersion)-(hash).cache
        // dll-$(FileNameWithoutExtension)-(hash).cache
        var builder = new StringBuilder(folder, folder.Length + 3 + 3 + 256 + 32);

        // Make sure that we are going to write to a file
        if (!folder.EndsWith(Path.DirectorySeparatorChar))
        {
            builder.Append(Path.DirectorySeparatorChar);
        }

        switch (key.GroupKind)
        {
            case CachedAssemblyGroupKind.Framework:
                builder.Append("fwk-");
                break;
            case CachedAssemblyGroupKind.Package:
                builder.Append("pkg-");
                break;
            case CachedAssemblyGroupKind.Dll:
                builder.Append("dll-");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        builder.Append(key.Name);
        if (!string.IsNullOrEmpty(key.Version))
        {
            builder.Append('-');
            builder.Append(key.Version);
        }

        builder.Append('-');

        builder.Append(HexHelper.ToString(Hash1, Hash2));

        builder.Append(".cache");

        return builder.ToString();
    }

    public void TryWriteToFile(string filePath)
    {
        // Try not to overwrite a file already created (files are supposed to be unique)
        if (File.Exists(filePath)) return;

        FileStream stream = null;

        try
        {
            stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException)
        {
            // ignore
        }

        try
        {
            WriteToStream(stream);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    public void WriteToStream(Stream stream)
    {
        using var writer = new TransferBinaryWriter(stream, Encoding.Default, true);
        writer.Write((uint)Magic);
        writer.Write((uint)Version);
        this.Write(writer);
        writer.Flush();
    }

    public CachedAssemblyGroup Read(TransferBinaryReader reader)
    {
        Hash1 = reader.ReadUInt64();
        Hash2 = reader.ReadUInt64();
        MaxModifiedTime = reader.ReadDateTime();
        reader.ReadStructsToList(Items);
        return this;
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.Write(Hash1);
        writer.Write(Hash2);
        writer.Write(MaxModifiedTime);
        writer.WriteStructsFromList(Items);
    }
}

public record struct CachedAssemblyGroupKey(CachedAssemblyGroupKind GroupKind, string Name, string Version);

public enum CachedAssemblyGroupKind
{
    Framework,
    Package,
    Dll
}