using System;

namespace XelaBuild.Core.Helpers;

internal class HexHelper
{
    private static ReadOnlySpan<byte> HexChars => new(new byte[16]
    {
        (byte)'0',
        (byte)'1',
        (byte)'2',
        (byte)'3',
        (byte)'4',
        (byte)'5',
        (byte)'6',
        (byte)'7',
        (byte)'8',
        (byte)'9',
        (byte)'a',
        (byte)'b',
        (byte)'c',
        (byte)'d',
        (byte)'e',
        (byte)'f',
    });

    public static unsafe string ToString(ulong uhash1, ulong uhash2)
    {
        var hash = stackalloc char[32];
        int index = 0;
        for (int i = 0; i < 8; i++)
        {
            hash[index++] = (char)HexChars[(int)(uhash1 & 0xF)];
            uhash1 >>= 4;
            hash[index++] = (char)HexChars[(int)(uhash1 & 0xF)];
            uhash1 >>= 4;
        }
        for (int i = 0; i < 8; i++)
        {
            hash[index++] = (char)HexChars[(int)(uhash2 & 0xF)];
            uhash2 >>= 4;
            hash[index++] = (char)HexChars[(int)(uhash2 & 0xF)];
            uhash2 >>= 4;
        }
        return new string(hash, 0, 32);
    }
}