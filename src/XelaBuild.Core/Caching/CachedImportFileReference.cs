using System;
using System.Runtime.CompilerServices;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public record CachedImportFileReference(string FullPath, DateTime LastWriteTime) : ITransferable<CachedImportFileReference>
{
    public CachedImportFileReference() : this(null, DateTime.MinValue)
    {
    }

    public string FullPath { get; set; } = FullPath;

    public DateTime LastWriteTime { get; set; } = LastWriteTime;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CachedImportFileReference Read(TransferBinaryReader readerArg)
    {
        var reader = (CachedProjectGroup.Reader)readerArg;
        var kind = reader.ReadByte();
        if (kind == 1)
        {
            var id = reader.ReadInt32();
            return reader.OrderedImports[id];
        }
        else if (kind == 2)
        {
            var id = reader.Imports.Count;
            reader.OrderedImports.Add(this);
            reader.Imports.Add(this, id);

            FullPath = reader.ReadString();
            LastWriteTime = reader.ReadDateTime();

            return this;
        }

        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(TransferBinaryWriter writerArg)
    {
        var writer = (CachedProjectGroup.Writer)writerArg;
        if (writer.Imports.TryGetValue(this, out var importIndex))
        {
            writer.Write((byte)1);
            writer.Write(importIndex);
        }
        else
        {
            writer.Write((byte)2);
            var id = writer.Imports.Count;
            writer.Imports.Add(this, id);

            writer.Write(FullPath);
            writer.Write((long)LastWriteTime.Ticks);
        }
    }
}