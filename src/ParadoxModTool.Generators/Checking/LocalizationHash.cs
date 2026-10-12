using System.Buffers.Binary;
using System.Text;

namespace ParadoxModTool.Generators;

/// <summary>Der Hash, mit dem EU V Lokalisierungsschlüssel ablegt: MurmurHash3 (x86, 32 Bit, Seed 0) über die UTF-8-Bytes des Schlüssels.</summary>
public static class LocalizationHash
{
    private const uint C1 = 0xcc9e2d51;
    private const uint C2 = 0x1b873593;

    public static uint Murmur3(string key)
    {
        var data = Encoding.UTF8.GetBytes(key);
        var hash = 0u;
        var blocks = data.Length / 4;
        for (var i = 0; i < blocks; i++)
        {
            hash ^= MixBlock(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i * 4, 4)));
            hash = RotateLeft(hash, 13);
            hash = (hash * 5) + 0xe6546b64;
        }

        var tail = 0u;
        var tailStart = blocks * 4;
        switch (data.Length & 3)
        {
            case 3:
                tail ^= (uint)data[tailStart + 2] << 16;
                goto case 2;
            case 2:
                tail ^= (uint)data[tailStart + 1] << 8;
                goto case 1;
            case 1:
                tail ^= data[tailStart];
                hash ^= MixBlock(tail);
                break;
        }

        hash ^= (uint)data.Length;
        hash ^= hash >> 16;
        hash *= 0x85ebca6b;
        hash ^= hash >> 13;
        hash *= 0xc2b2ae35;
        hash ^= hash >> 16;
        return hash;
    }

    private static uint MixBlock(uint k)
    {
        k *= C1;
        k = RotateLeft(k, 15);
        return k * C2;
    }

    private static uint RotateLeft(uint value, int bits) => (value << bits) | (value >> (32 - bits));
}
