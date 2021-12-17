using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XelaBuild.Core.Helpers;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public class CachedAssemblyGroup : BinaryRootTransferable<CachedAssemblyGroup>
{
    private const string FrameworkPrefix = "fwk-";
    private const string PackagePrefix = "pkg-";
    private const string DllPrefix = "dll-";

    /// <summary>
    /// CAGF: Cached Assembly Group File
    /// </summary>
    public static readonly MagicVersion CurrentMagicVersion = new("CAGF", 1, 0);

    public CachedAssemblyGroup()
    {
        MagicVersion = CurrentMagicVersion;
        Items = new List<CachedFileReference>();
    }

    public ulong Hash1;

    public ulong Hash2;

    public DateTime MaxModifiedTime;

    public List<CachedFileReference> Items { get; }

    public static CachedAssemblyGroup ReadFromFile(string filePath)
    {
        return BinaryTransfer.ReadFromFile<CachedAssemblyGroup>(filePath);
    }

    public static CachedAssemblyGroup ReadFromStream(Stream stream)
    {
        return BinaryTransfer.ReadFromStream<CachedAssemblyGroup>(stream);
    }

    public static bool ShouldReload(string filePath)
    {
        return filePath.StartsWith("dll-");
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

        // The hash takes into account the timestamp as it should be stable for all
        // packages, assembly references
        var customHash2 = (((ulong)MaxModifiedTime.Ticks) * 397) ^ Hash2;

        switch (key.GroupKind)
        {
            case CachedAssemblyGroupKind.Framework:
                builder.Append(FrameworkPrefix);
                break;
            case CachedAssemblyGroupKind.Package:
                builder.Append(PackagePrefix);
                break;
            case CachedAssemblyGroupKind.Dll:
                builder.Append(DllPrefix);
                // Except for dll where we re-read them from disk
                customHash2 = Hash2;
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

        builder.Append(HexHelper.ToString(Hash1, customHash2));

        builder.Append(".cache");

        return builder.ToString();
    }

    public bool TryWriteToFile(string filePath, out DateTime lastWriteTime)
    {
        // Try not to overwrite a file already created (files are supposed to be unique)
        var fileInfo = FileUtilities.GetFileInfoNoThrow(filePath);
        if (fileInfo != null && fileInfo.Exists)
        {
            lastWriteTime = fileInfo.LastWriteTimeUtc;
            return true;
        }

        BinaryTransfer.WriteToFile(filePath, this);
        fileInfo = FileUtilities.GetFileInfoNoThrow(filePath);
        if (fileInfo != null)
        {
            fileInfo.LastWriteTimeUtc = MaxModifiedTime;
        }
        lastWriteTime = MaxModifiedTime;
        return true;
    }

    public void WriteToStream(Stream stream)
    {
        BinaryTransfer.WriteToStream(stream, this);
    }

    public override CachedAssemblyGroup Read(BinaryTransferReader reader)
    {
        Hash1 = reader.ReadUInt64();
        Hash2 = reader.ReadUInt64();
        MaxModifiedTime = reader.ReadDateTime();
        reader.ReadStructsToList(Items);
        return this;
    }

    public override void Write(BinaryTransferWriter writer)
    {
        writer.Write(Hash1);
        writer.Write(Hash2);
        writer.Write(MaxModifiedTime);
        writer.WriteStructsFromList(Items);
    }
}

public record struct CachedAssemblyGroupKey(CachedAssemblyGroupKind GroupKind, string Name, string Version) : IComparable<CachedAssemblyGroupKey>, IComparable
{
    public int CompareTo(CachedAssemblyGroupKey other)
    {
        var groupKindComparison = GroupKind.CompareTo(other.GroupKind);
        if (groupKindComparison != 0) return groupKindComparison;
        var nameComparison = string.Compare(Name, other.Name, StringComparison.Ordinal);
        if (nameComparison != 0) return nameComparison;
        return string.Compare(Version, other.Version, StringComparison.Ordinal);
    }

    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj)) return 1;
        return obj is CachedAssemblyGroupKey other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(CachedAssemblyGroupKey)}");
    }
}

public enum CachedAssemblyGroupKind
{
    Framework,
    Package,
    Dll
}