using System;
using XelaBuild.Core.Helpers;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core;

public record struct Hash128(ulong Hash1, ulong Hash2) : IBinaryTransferable<Hash128>
{
    public Hash128 CombineUnordered(in Hash128 value)
    {
        return new Hash128(Hash1 ^ value.Hash1, Hash2 ^ value.Hash2);
    }

    public Hash128 CombineOrdered(in Hash128 value)
    {
        // Use FNV-1a hash for combine (only combining 64 bits separately)
        const ulong FNV_offset_basis = 0xcbf29ce484222325;
        const ulong FNV_prime = 0x00000100000001B3;
        var hash1 = FNV_offset_basis ^ Hash1;
        var hash2 = FNV_offset_basis ^ Hash2;

        hash1 *= FNV_prime;
        hash1 ^= value.Hash1;
        hash2 *= FNV_prime;
        hash2 ^= value.Hash2;

        return new Hash128(hash1, hash2);
    }

    public static Hash128 FromString(string message)
    {
        SpookyHash.Hash128(message, out var hash1, out var hash2);
        return new Hash128(hash1, hash2);
    }

    public Hash128 Read(BinaryTransferReader reader)
    {
        return new Hash128(reader.ReadUInt64(), reader.ReadUInt64());
    }

    public void Write(BinaryTransferWriter writer)
    {
        writer.Write(Hash1);
        writer.Write(Hash2);
    }
}