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
    public CachedImportFileReference Read(TransferBinaryReader reader)
    {
        FullPath = reader.ReadString();
        LastWriteTime = reader.ReadDateTime();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(TransferBinaryWriter writer)
    {
        writer.Write(FullPath);
        writer.Write(LastWriteTime);
    }
}