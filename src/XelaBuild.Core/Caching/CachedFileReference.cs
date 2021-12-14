using System;
using System.Buffers;
using System.Diagnostics;
using System.Text;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public record struct CachedFileReference(string FullPath, DateTime LastWriteTime) : ITransferable<CachedFileReference>
{
    public CachedFileReference Read(TransferBinaryReader reader)
    {
        return new CachedFileReference(reader.ReadString(), reader.ReadDateTime());
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.Write(FullPath);
        writer.Write((long)LastWriteTime.Ticks);
    }
}