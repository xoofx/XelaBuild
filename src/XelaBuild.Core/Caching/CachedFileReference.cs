using System;
using System.Runtime.CompilerServices;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public record struct CachedFileReference(string FullPath, DateTime LastWriteTime) : IBinaryTransferable<CachedFileReference>
{
    public static readonly CachedFileReference Empty = new CachedFileReference(string.Empty, DateTime.MinValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CachedFileReference Read(BinaryTransferReader reader)
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