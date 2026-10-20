using System.Buffers.Binary;
using System.Numerics;

namespace ParadoxModTool.GuiDesign.Imaging;

/// <summary>
/// BC7-Decoder (DXGI 97 bis 99) nach der "BC7 Format"-Beschreibung von Direct3D: acht Modi, bis zu drei Teilmengen, P-Bits, Rotation (Modi 4 und 5),
/// Indexwahl (Modus 4) und Anker-Indizes ohne höchstes Bit. Die Interpolation ist ganzzahlig: <c>((64 - w) * a + w * b + 32) >> 6</c>.
/// Ein Block mit Byte 0 = 0 (reservierter Modus) ergibt transparentes Schwarz. sRGB wird wie UNORM behandelt (Entscheidung der Spezifikation).
/// Die Partitions- und Ankertabellen sind gegen eine Referenzdekodierung (Pillow) geprüft, siehe <c>make_bc7_testdata.py</c>.
/// </summary>
internal static class Bc7Decoder
{
    // Je Modus: Teilmengen, Partitionsbits sowie Farb- und Alphabits je Endpunkt (ohne P-Bit).
    private static readonly int[] Subsets = { 3, 2, 3, 2, 1, 1, 1, 2 };
    private static readonly int[] PartitionBits = { 4, 6, 6, 6, 0, 0, 0, 6 };
    private static readonly int[] ColorBits = { 4, 6, 5, 7, 5, 7, 7, 5 };
    private static readonly int[] AlphaBits = { 0, 0, 0, 0, 6, 8, 7, 5 };

    // P-Bits: 0 keine, 1 je Endpunkt, 2 je Teilmenge geteilt (nur Modus 1).
    private static readonly int[] PBitKind = { 1, 2, 0, 1, 0, 0, 1, 1 };

    private static readonly int[] Weights2 = { 0, 21, 43, 64 };
    private static readonly int[] Weights3 = { 0, 9, 18, 27, 37, 46, 55, 64 };
    private static readonly int[] Weights4 = { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };

    // Zwei Teilmengen: Bit i = Teilmenge von Pixel i.
    private static readonly ushort[] Partition2 =
    {
        0xCCCC, 0x8888, 0xEEEE, 0xECC8, 0xC880, 0xFEEC, 0xFEC8, 0xEC80,
        0xC800, 0xFFEC, 0xFE80, 0xE800, 0xFFE8, 0xFF00, 0xFFF0, 0xF000,
        0xF710, 0x008E, 0x7100, 0x08CE, 0x008C, 0x7310, 0x3100, 0x8CCE,
        0x088C, 0x3110, 0x6666, 0x366C, 0x17E8, 0x0FF0, 0x718E, 0x399C,
        0xAAAA, 0xF0F0, 0x5A5A, 0x33CC, 0x3C3C, 0x55AA, 0x9696, 0xA55A,
        0x73CE, 0x13C8, 0x324C, 0x3BDC, 0x6996, 0xC33C, 0x9966, 0x0660,
        0x0272, 0x04E4, 0x4E40, 0x2720, 0xC936, 0x936C, 0x39C6, 0x639C,
        0x9336, 0x9CC6, 0x817E, 0xE718, 0xCCF0, 0x0FCC, 0x7744, 0xEE22,
    };

    // Drei Teilmengen: zwei Bit je Pixel (Pixel i in den Bits 2i und 2i+1).
    private static readonly uint[] Partition3 =
    {
        0xAA685050, 0x6A5A5040, 0x5A5A4200, 0x5450A0A8,
        0xA5A50000, 0xA0A05050, 0x5555A0A0, 0x5A5A5050,
        0xAA550000, 0xAA555500, 0xAAAA5500, 0x90909090,
        0x94949494, 0xA4A4A4A4, 0xA9A59450, 0x2A0A4250,
        0xA5945040, 0x0A425054, 0xA5A5A500, 0x55A0A0A0,
        0xA8A85454, 0x6A6A4040, 0xA4A45000, 0x1A1A0500,
        0x0050A4A4, 0xAAA59090, 0x14696914, 0x69691400,
        0xA08585A0, 0xAA821414, 0x50A4A450, 0x6A5A0200,
        0xA9A58000, 0x5090A0A8, 0xA8A09050, 0x24242424,
        0x00AA5500, 0x24924924, 0x24499224, 0x50A50A50,
        0x500AA550, 0xAAAA4444, 0x66660000, 0xA5A0A5A0,
        0x50A050A0, 0x69286928, 0x44AAAA44, 0x66666600,
        0xAA444444, 0x54A854A8, 0x95809580, 0x96969600,
        0xA85454A8, 0x80959580, 0xAA141414, 0x96960000,
        0xAAAA1414, 0xA05050A0, 0xA0A5A5A0, 0x96000000,
        0x40804080, 0xA9A8A9A8, 0xAAAAAA44, 0x2A4A5254,
    };

    // Ankerpixel. Teilmenge 0 hat immer Pixel 0. Hier der Anker der Teilmenge 1 bei zwei Teilmengen bzw. der Teilmengen 1 und 2 bei dreien.
    private static readonly byte[] Anchor2 =
    {
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 2, 8, 2, 2, 8, 8, 15, 2, 8, 2, 2, 8, 8, 2, 2,
        15, 15, 6, 8, 2, 8, 15, 15, 2, 8, 2, 2, 2, 15, 15, 6,
        6, 2, 6, 8, 15, 15, 2, 2, 15, 15, 15, 15, 15, 2, 2, 15,
    };

    private static readonly byte[] Anchor3First =
    {
        3, 3, 15, 15, 8, 3, 15, 15, 8, 8, 6, 6, 6, 5, 3, 3,
        3, 3, 8, 15, 3, 3, 6, 10, 5, 8, 8, 6, 8, 5, 15, 15,
        8, 15, 3, 5, 6, 10, 8, 15, 15, 3, 15, 5, 15, 15, 15, 15,
        3, 15, 5, 5, 5, 8, 5, 10, 5, 10, 8, 13, 15, 12, 3, 3,
    };

    private static readonly byte[] Anchor3Second =
    {
        15, 8, 8, 3, 15, 15, 3, 8, 15, 15, 15, 15, 15, 15, 15, 8,
        15, 8, 15, 3, 15, 8, 15, 8, 3, 15, 6, 10, 15, 15, 10, 8,
        15, 3, 15, 10, 10, 8, 9, 10, 6, 15, 8, 15, 3, 6, 6, 8,
        15, 3, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 3, 15, 15, 8,
    };

    /// <summary>
    /// Dekodiert die Blöcke (16 Byte je 4x4 Pixel, zeilenweise) zu einem BGRA-Bild. <see langword="null"/>, wenn weniger Daten da sind, als die Größe braucht.
    /// Randblöcke werden auf die Bildgröße beschnitten.
    /// </summary>
    public static GuiImage? Decode(ReadOnlySpan<byte> blocks, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var blocksX = (width + 3) / 4;
        var blocksY = (height + 3) / 4;
        if (blocks.Length < (long)blocksX * blocksY * 16)
        {
            return null;
        }

        var pixels = new byte[width * height * 4];
        Span<byte> decoded = stackalloc byte[16 * 4]; // RGBA eines Blocks
        for (var by = 0; by < blocksY; by++)
        {
            for (var bx = 0; bx < blocksX; bx++)
            {
                DecodeBlock(blocks.Slice(((by * blocksX) + bx) * 16, 16), decoded);
                for (var p = 0; p < 16; p++)
                {
                    var x = (bx * 4) + (p % 4);
                    var y = (by * 4) + (p / 4);
                    if (x >= width || y >= height)
                    {
                        continue;
                    }

                    var target = ((y * width) + x) * 4;
                    pixels[target] = decoded[(p * 4) + 2];
                    pixels[target + 1] = decoded[(p * 4) + 1];
                    pixels[target + 2] = decoded[p * 4];
                    pixels[target + 3] = decoded[(p * 4) + 3];
                }
            }
        }

        return new GuiImage(width, height, pixels);
    }

    /// <summary>Ein Block zu 16 Pixeln RGBA (Pixel 0 bis 15 in Blockreihenfolge).</summary>
    private static void DecodeBlock(ReadOnlySpan<byte> block, Span<byte> decoded)
    {
        if (block[0] == 0)
        {
            // Reservierter Modus: transparentes Schwarz.
            decoded.Clear();
            return;
        }

        var mode = BitOperations.TrailingZeroCount((uint)block[0]);
        var reader = new BitReader(BinaryPrimitives.ReadUInt64LittleEndian(block), BinaryPrimitives.ReadUInt64LittleEndian(block[8..]), mode + 1);
        var subsets = Subsets[mode];
        var partition = reader.Read(PartitionBits[mode]);
        var rotation = mode is 4 or 5 ? reader.Read(2) : 0;
        var indexSelection = mode == 4 ? reader.Read(1) : 0;

        // Endpunkte: erst alle Rot-, dann alle Grün-, dann alle Blauwerte, dann Alpha. Endpunkt 2s und 2s+1 gehören zur Teilmenge s.
        Span<int> endpoints = stackalloc int[6 * 4]; // je Endpunkt R, G, B, A
        var count = subsets * 2;
        for (var channel = 0; channel < 3; channel++)
        {
            for (var e = 0; e < count; e++)
            {
                endpoints[(e * 4) + channel] = reader.Read(ColorBits[mode]);
            }
        }

        var hasAlpha = AlphaBits[mode] > 0;
        if (hasAlpha)
        {
            for (var e = 0; e < count; e++)
            {
                endpoints[(e * 4) + 3] = reader.Read(AlphaBits[mode]);
            }
        }

        var pBitKind = PBitKind[mode];
        Span<int> pBits = stackalloc int[6];
        pBits.Clear();
        if (pBitKind == 1)
        {
            for (var e = 0; e < count; e++)
            {
                pBits[e] = reader.Read(1);
            }
        }
        else if (pBitKind == 2)
        {
            for (var s = 0; s < subsets; s++)
            {
                pBits[s * 2] = pBits[(s * 2) + 1] = reader.Read(1);
            }
        }

        // P-Bit als niedrigstes Bit anhängen, dann auf 8 Bit strecken; ohne Alphakanal ist Alpha 255.
        var shift = pBitKind != 0 ? 1 : 0;
        for (var e = 0; e < count; e++)
        {
            for (var channel = 0; channel < 3; channel++)
            {
                ref var value = ref endpoints[(e * 4) + channel];
                value = Expand((value << shift) | pBits[e], ColorBits[mode] + shift);
            }

            ref var alpha = ref endpoints[(e * 4) + 3];
            alpha = hasAlpha ? Expand((alpha << shift) | pBits[e], AlphaBits[mode] + shift) : 255;
        }

        // Teilmenge je Pixel und Ankerpixel.
        Span<byte> subsetOf = stackalloc byte[16];
        for (var p = 0; p < 16; p++)
        {
            subsetOf[p] = subsets switch
            {
                2 => (byte)((Partition2[partition] >> p) & 1),
                3 => (byte)((Partition3[partition] >> (2 * p)) & 3),
                _ => (byte)0,
            };
        }

        var anchor1 = subsets == 2 ? Anchor2[partition] : subsets == 3 ? Anchor3First[partition] : 0;
        var anchor2 = subsets == 3 ? Anchor3Second[partition] : 0;

        // Indizes. Der Ankerpixel jeder Teilmenge spart das höchste Bit. Modus 4 hat zwei Sätze (zuerst 2, dann 3 Bit), Modus 5 zwei zu 2 Bit.
        var firstBits = mode switch { 0 or 1 => 3, 6 => 4, _ => 2 };
        Span<byte> first = stackalloc byte[16];
        Span<byte> second = stackalloc byte[16];
        ReadIndices(ref reader, firstBits, subsetOf, anchor1, anchor2, first);
        if (mode is 4 or 5)
        {
            ReadIndices(ref reader, mode == 4 ? 3 : 2, subsetOf, anchor1, anchor2, second);
        }

        ReadOnlySpan<byte> colorIndex = first;
        ReadOnlySpan<byte> alphaIndex = first;
        var colorIndexBits = firstBits;
        var alphaIndexBits = firstBits;
        if (mode == 4)
        {
            // Indexwahl 0: Farbe mit dem 2-Bit-Satz, Alpha mit dem 3-Bit-Satz; Indexwahl 1 umgekehrt.
            colorIndex = indexSelection == 0 ? first : second;
            alphaIndex = indexSelection == 0 ? second : first;
            colorIndexBits = indexSelection == 0 ? 2 : 3;
            alphaIndexBits = indexSelection == 0 ? 3 : 2;
        }
        else if (mode == 5)
        {
            alphaIndex = second;
        }

        var colorWeights = WeightsFor(colorIndexBits);
        var alphaWeights = WeightsFor(alphaIndexBits);
        for (var p = 0; p < 16; p++)
        {
            var e0 = subsetOf[p] * 8; // Endpunkt 2s beginnt bei 2s * 4
            var e1 = e0 + 4;
            var wc = colorWeights[colorIndex[p]];
            var r = Interpolate(endpoints[e0], endpoints[e1], wc);
            var g = Interpolate(endpoints[e0 + 1], endpoints[e1 + 1], wc);
            var b = Interpolate(endpoints[e0 + 2], endpoints[e1 + 2], wc);
            var a = hasAlpha ? Interpolate(endpoints[e0 + 3], endpoints[e1 + 3], alphaWeights[alphaIndex[p]]) : 255;
            switch (rotation)
            {
                case 1:
                    (r, a) = (a, r);
                    break;
                case 2:
                    (g, a) = (a, g);
                    break;
                case 3:
                    (b, a) = (a, b);
                    break;
            }

            decoded[p * 4] = (byte)r;
            decoded[(p * 4) + 1] = (byte)g;
            decoded[(p * 4) + 2] = (byte)b;
            decoded[(p * 4) + 3] = (byte)a;
        }
    }

    private static void ReadIndices(ref BitReader reader, int bits, ReadOnlySpan<byte> subsetOf, int anchor1, int anchor2, Span<byte> target)
    {
        for (var p = 0; p < 16; p++)
        {
            var subset = subsetOf[p];
            var isAnchor = p == 0 || (subset == 1 && p == anchor1) || (subset == 2 && p == anchor2);
            target[p] = (byte)reader.Read(isAnchor ? bits - 1 : bits);
        }
    }

    private static int[] WeightsFor(int bits) => bits switch { 2 => Weights2, 3 => Weights3, _ => Weights4 };

    private static int Interpolate(int a, int b, int weight) => (((64 - weight) * a) + (weight * b) + 32) >> 6;

    /// <summary>Wert mit <paramref name="bits"/> Bit auf 8 Bit strecken (obere Bits wiederholen).</summary>
    private static int Expand(int value, int bits) => bits >= 8 ? value : (value << (8 - bits)) | (value >> ((2 * bits) - 8));

    /// <summary>Liest Bitfelder aus den 128 Bit eines Blocks, niedrigstes Bit zuerst.</summary>
    private struct BitReader
    {
        private readonly ulong _low;
        private readonly ulong _high;
        private int _position;

        public BitReader(ulong low, ulong high, int position)
        {
            _low = low;
            _high = high;
            _position = position;
        }

        public int Read(int count)
        {
            if (count == 0)
            {
                return 0;
            }

            ulong value;
            if (_position >= 64)
            {
                value = _high >> (_position - 64);
            }
            else if (_position + count > 64)
            {
                value = (_low >> _position) | (_high << (64 - _position));
            }
            else
            {
                value = _low >> _position;
            }

            _position += count;
            return (int)(value & ((1UL << count) - 1));
        }
    }
}
