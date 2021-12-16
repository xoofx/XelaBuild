using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public record struct CachedFileReference(string FullPath, DateTime LastWriteTime) : ITransferable<CachedFileReference>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CachedFileReference Read(TransferBinaryReader reader)
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