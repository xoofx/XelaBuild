using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace XelaBuild.Core.Serialization;

public readonly record struct CachedMagicVersion : ITransferable<CachedMagicVersion>
{
    public CachedMagicVersion(string magic, ushort majorVersion, ushort minorVersion)
    {
        var magicBytes = Encoding.UTF8.GetBytes(magic);
        if (magicBytes.Length != 4) throw new ArgumentException($"Invalid magic `{magic}` must be 4 bytes UTF8 length but is {magicBytes.Length} bytes.", nameof(magic));
        Magic = BinaryPrimitives.ReadUInt32LittleEndian(magicBytes);
        Version = (uint)(majorVersion << 16) | minorVersion;
    }

    private CachedMagicVersion(uint magic, uint version)
    {
        Magic = magic;
        Version = version;
    }

    public uint Magic { get; }

    public uint Version { get; }

    public void CheckValidAgainst(CachedMagicVersion againstVersion)
    {
        if (againstVersion.Magic != Magic) throw new InvalidDataException($"Invalid Magic 0x{Magic:X8}, while expecting 0x{againstVersion.Magic:X8}");
        if (againstVersion.Version != Version) throw new InvalidDataException($"Invalid Version 0x{Version:X8}, while expecting 0x{againstVersion.Version:X8}");
    }

    public CachedMagicVersion Read(TransferBinaryReader reader)
    {
        return new CachedMagicVersion(reader.ReadUInt32(), reader.ReadUInt32());
    }

    public void Write(TransferBinaryWriter writer)
    {
        writer.Write(Magic);
        writer.Write(Version);
    }
}