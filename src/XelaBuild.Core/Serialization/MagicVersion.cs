using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace XelaBuild.Core.Serialization;

public readonly record struct MagicVersion
{
    public MagicVersion(string magic, byte majorVersion, ushort minorVersion)
    {
        var magicBytes = Encoding.UTF8.GetBytes(magic);
        if (magicBytes.Length != 4) throw new ArgumentException($@"Invalid magic `{magic}` must be 4 bytes UTF8 length but is {magicBytes.Length} bytes.", nameof(magic));
        Magic = BinaryPrimitives.ReadUInt32LittleEndian(magicBytes);
        Version = (uint)(majorVersion << 16) | minorVersion;
    }

    private MagicVersion(uint magic, uint versionAndCompressionKind)
    {
        Magic = magic;
        Version = versionAndCompressionKind;
    }

    public uint Magic { get; }

    public uint Version { get; }

    public void CheckValidAgainst(MagicVersion againstVersion)
    {
        if (againstVersion.Magic != Magic) throw new InvalidDataException($"Invalid Magic 0x{Magic:X8}, while expecting 0x{againstVersion.Magic:X8}");
        if (againstVersion.Version != Version) throw new InvalidDataException($"Invalid Version 0x{Version:X8}, while expecting 0x{againstVersion.Version:X8}");
    }
    internal (MagicVersion, BinaryTransferCompressionKind) ReadFrom(ReadOnlySpan<byte> span)
    {
        if (span.Length != 8) throw new ArgumentException($"Invalid length ({span.Length} of input span. Must be 8 bytes.");
        var spanUInt = MemoryMarshal.Cast<byte, uint>(span);

        var versionAndCompressionKind = spanUInt[1];
        var version = versionAndCompressionKind >> 8;
        var compressionKind = (BinaryTransferCompressionKind)(versionAndCompressionKind & 0xFF);
        return (new MagicVersion(spanUInt[0], version), compressionKind);
    }

    internal void WriteTo(Span<byte> span, BinaryTransferCompressionKind compressionKind)
    {
        if (span.Length != 8) throw new ArgumentException($"Invalid length ({span.Length} of input span. Must be 8 bytes.");
        var spanUInt = MemoryMarshal.Cast<byte, uint>(span);
        spanUInt[0] = Magic;
        spanUInt[1] = (Version << 8) | (uint)compressionKind;
    }
}