using System;
using System.Runtime.CompilerServices;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public record CachedImportFileReference(string FullPath, DateTime LastWriteTime) : IBinaryTransferable<CachedImportFileReference>
{
    public CachedImportFileReference() : this(string.Empty, DateTime.MinValue)
    {
    }

    public string FullPath { get; set; } = FullPath;

    public DateTime LastWriteTime { get; set; } = LastWriteTime;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CachedImportFileReference Read(BinaryTransferReader reader)
    {
        FullPath = reader.ReadString();
        LastWriteTime = reader.ReadDateTime();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(BinaryTransferWriter writer)
    {
        writer.Write(FullPath);
        writer.Write(LastWriteTime);
    }
}