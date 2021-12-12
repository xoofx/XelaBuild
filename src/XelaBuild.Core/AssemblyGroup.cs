using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace XelaBuild.Core;

public class AssemblyGroup
{
    private const uint Magic = 0x43494243;
    private const uint Version = 0x0001_0000;

    public AssemblyGroup()
    {
        Items = new List<FilePathAndTime>();
        MaxModifiedTime = DateTime.MinValue;
    }

    public ulong Hash1;

    public ulong Hash2;

    public DateTime MaxModifiedTime;

    public List<FilePathAndTime> Items { get; }

    private static DateTime ReadDateTime(BinaryReader reader)
    {
        var ticks = reader.ReadInt64();
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private static void WriteDateTime(BinaryWriter writer, DateTime time)
    {
        writer.Write(time.Ticks);
    }

    public static AssemblyGroup ReadFromFile(string filePath)
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

    public static AssemblyGroup ReadFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.Default, true);
        if (reader.ReadUInt32() != Magic) throw new InvalidDataException("Invalid Magic Number");
        var version = reader.ReadUInt32();
        if (version != Version) throw new InvalidDataException($"Invalid Version {version} instead of {Version} only supported");
        var group = new AssemblyGroup();
        group.Hash1 = reader.ReadUInt64();
        group.Hash2 = reader.ReadUInt64();
        group.MaxModifiedTime = ReadDateTime(reader);
        var count = reader.ReadInt32();
        group.Items.Capacity = count;
        for(int i = 0; i < count; i++)
        {
            var time = ReadDateTime(reader);
            var length = reader.ReadInt32();
            var buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                var read = reader.Read(buffer, 0, length);
                Debug.Assert(length == read);
                var filePath = Encoding.UTF8.GetString(buffer, 0, read);
                group.Items.Add(new FilePathAndTime(filePath, time));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        return group;
    }

    public string GetFilePath(AssemblyGroupKey key, string folder)
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
            case AssemblyGroupKind.Framework:
                builder.Append("fwk-");
                break;
            case AssemblyGroupKind.Package:
                builder.Append("pkg-");
                break;
            case AssemblyGroupKind.Dll:
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
        using var writer = new BinaryWriter(stream, Encoding.Default, true);
        writer.Write((uint)Magic);
        writer.Write((uint)Version);
        writer.Write((ulong)Hash1);
        writer.Write((ulong)Hash2);
        writer.Write((long)MaxModifiedTime.Ticks);
        writer.Write((int)Items.Count);
        foreach (FilePathAndTime item in Items)
        {
            writer.Write((long)item.LastWriteTimeUtc.Ticks);
            var length = Encoding.UTF8.GetByteCount(item.FullPath);
            var buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                var written = Encoding.UTF8.GetBytes(item.FullPath, 0, item.FullPath.Length, buffer, 0);
                Debug.Assert(length == written);
                writer.Write((int) written);
                writer.Write(buffer, 0, written);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        writer.Flush();
    }
}

public record struct AssemblyGroupKey(AssemblyGroupKind GroupKind, string Name, string Version);

public enum AssemblyGroupKind
{
    Framework,
    Package,
    Dll
}

public record struct FilePathAndTime(string FullPath, DateTime LastWriteTimeUtc);

internal class HexHelper
{
    private static ReadOnlySpan<byte> HexChars => new(new byte[16]
    {
        (byte)'0',
        (byte)'1',
        (byte)'2',
        (byte)'3',
        (byte)'4',
        (byte)'5',
        (byte)'6',
        (byte)'7',
        (byte)'8',
        (byte)'9',
        (byte)'a',
        (byte)'b',
        (byte)'c',
        (byte)'d',
        (byte)'e',
        (byte)'f',
    });

    public static unsafe string ToString(ulong uhash1, ulong uhash2)
    {
        var hash = stackalloc char[32];
        int index = 0;
        for (int i = 0; i < 8; i++)
        {
            hash[index++] = (char)HexChars[(int)(uhash1 & 0xF)];
            uhash1 >>= 4;
            hash[index++] = (char)HexChars[(int)(uhash1 & 0xF)];
            uhash1 >>= 4;
        }
        for (int i = 0; i < 8; i++)
        {
            hash[index++] = (char)HexChars[(int)(uhash2 & 0xF)];
            uhash2 >>= 4;
            hash[index++] = (char)HexChars[(int)(uhash2 & 0xF)];
            uhash2 >>= 4;
        }
        return new string(hash, 0, 32);
    }
}