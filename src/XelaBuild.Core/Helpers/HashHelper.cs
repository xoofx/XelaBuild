using System.Collections.Generic;
using System.Linq;

namespace XelaBuild.Core.Helpers;

internal static class HashHelper
{
    public static string Hash128(Dictionary<string, string> dict)
    {
        ulong hash1 = SpookyHash.SpookyConst;
        ulong hash2 = SpookyHash.SpookyConst;
        foreach (var entry in dict.OrderBy(x => x.Key))
        {
            SpookyHash.Hash128(entry.Key, out var lhash1, out var lhash2);
            hash1 ^= lhash1;
            hash2 ^= lhash2;

            SpookyHash.Hash128(entry.Value, out lhash1, out lhash2);
            hash1 ^= lhash1;
            hash2 ^= lhash2;
        }

        return HexHelper.ToString(hash1, hash2);
    }
}