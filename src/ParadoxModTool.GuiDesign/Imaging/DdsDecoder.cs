using System.Buffers.Binary;
using System.Numerics;

namespace ParadoxModTool.GuiDesign.Imaging;

/// <summary>
/// DDS-Decoder für die Vorschau, liest nur die oberste Mip-Stufe. Unterstützt unkomprimierte Formate mit Bitmasken (32, 24 und 16 Bit, auch 4-4-4-4
/// und 5-6-5), die FourCCs DXT1, DXT3, DXT5 und RXGB (DXT5 mit Rot im Alphakanal) sowie DX10 mit DXGI 28/29 (RGBA8), 87/91 (BGRA8), 70-72 (BC1),
/// 73-75 (BC2), 76-78 (BC3) und 97-99 (BC7, siehe <see cref="Bc7Decoder"/>).
/// Alles andere und kaputte Dateien liefern <see langword="null"/>. Die Blockrechnung folgt dem DXT-Decoder der IV2-Bibliothek, ist aber eigenständig.
/// </summary>
public static class DdsDecoder
{
    private const int HeaderSize = 128;
    private const int Dx10HeaderSize = 20;
    private const uint FlagFourCc = 0x4;
    private const uint FlagRgb = 0x40;

    private static readonly uint FourCcDxt1 = FourCc("DXT1");
    private static readonly uint FourCcDxt3 = FourCc("DXT3");
    private static readonly uint FourCcDxt5 = FourCc("DXT5");
    private static readonly uint FourCcRxgb = FourCc("RXGB");
    private static readonly uint FourCcDx10 = FourCc("DX10");

    private enum BlockFormat
    {
        Dxt1,
        Dxt3,
        Dxt5,
        Rxgb,
    }

    public static GuiImage? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize || !data[..4].SequenceEqual("DDS "u8))
        {
            return null;
        }

        var height = BinaryPrimitives.ReadInt32LittleEndian(data[12..]);
        var width = BinaryPrimitives.ReadInt32LittleEndian(data[16..]);
        if (!GuiImageDecoder.IsValidSize(width, height))
        {
            return null;
        }

        var pixelFlags = BinaryPrimitives.ReadUInt32LittleEndian(data[80..]);
        if ((pixelFlags & FlagFourCc) != 0)
        {
            var fourCc = BinaryPrimitives.ReadUInt32LittleEndian(data[84..]);
            if (fourCc == FourCcDxt1)
            {
                return DecodeBlocks(data[HeaderSize..], width, height, BlockFormat.Dxt1);
            }

            if (fourCc == FourCcDxt3)
            {
                return DecodeBlocks(data[HeaderSize..], width, height, BlockFormat.Dxt3);
            }

            if (fourCc == FourCcDxt5)
            {
                return DecodeBlocks(data[HeaderSize..], width, height, BlockFormat.Dxt5);
            }

            if (fourCc == FourCcRxgb)
            {
                return DecodeBlocks(data[HeaderSize..], width, height, BlockFormat.Rxgb);
            }

            return fourCc == FourCcDx10 ? DecodeDx10(data, width, height) : null;
        }

        return (pixelFlags & FlagRgb) != 0 ? DecodeMasked(data, width, height) : null;
    }

    private static uint FourCc(string text) => BinaryPrimitives.ReadUInt32LittleEndian(System.Text.Encoding.ASCII.GetBytes(text));

    // --- Unkomprimiert mit Bitmasken ---

    private static GuiImage? DecodeMasked(ReadOnlySpan<byte> data, int width, int height)
    {
        var bits = BinaryPrimitives.ReadInt32LittleEndian(data[88..]);
        if (bits is not (16 or 24 or 32))
        {
            return null;
        }

        var red = new Channel(BinaryPrimitives.ReadUInt32LittleEndian(data[92..]));
        var green = new Channel(BinaryPrimitives.ReadUInt32LittleEndian(data[96..]));
        var blue = new Channel(BinaryPrimitives.ReadUInt32LittleEndian(data[100..]));
        var alpha = new Channel(BinaryPrimitives.ReadUInt32LittleEndian(data[104..]));
        if (red.Mask == 0 && green.Mask == 0 && blue.Mask == 0)
        {
            return null;
        }

        var bytesPerPixel = bits / 8;
        var count = width * height;
        var source = data[HeaderSize..];
        if (source.Length < (long)count * bytesPerPixel)
        {
            return null;
        }

        var pixels = new byte[count * 4];
        for (var i = 0; i < count; i++)
        {
            uint value = 0;
            for (var b = 0; b < bytesPerPixel; b++)
            {
                value |= (uint)source[(i * bytesPerPixel) + b] << (8 * b);
            }

            var target = i * 4;
            pixels[target] = blue.Extract(value, 0);
            pixels[target + 1] = green.Extract(value, 0);
            pixels[target + 2] = red.Extract(value, 0);
            pixels[target + 3] = alpha.Extract(value, 255);
        }

        return new GuiImage(width, height, pixels);
    }

    /// <summary>Ein Farbkanal einer Bitmaske: Lage der Bits und Höchstwert, um auf 0..255 zu strecken.</summary>
    private readonly struct Channel
    {
        private readonly int _shift;
        private readonly uint _max;

        public Channel(uint mask)
        {
            Mask = mask;
            _shift = mask == 0 ? 0 : BitOperations.TrailingZeroCount(mask);
            _max = mask >> _shift;
        }

        public uint Mask { get; }

        /// <summary>Wert des Kanals auf 8 Bit gestreckt (gerundet); ohne Maske <paramref name="fallback"/>.</summary>
        public byte Extract(uint pixel, byte fallback)
        {
            if (Mask == 0)
            {
                return fallback;
            }

            var value = (pixel & Mask) >> _shift;
            return (byte)(((value * 255UL) + (_max / 2)) / _max);
        }
    }

    // --- DX10 ---

    private static GuiImage? DecodeDx10(ReadOnlySpan<byte> data, int width, int height)
    {
        if (data.Length < HeaderSize + Dx10HeaderSize)
        {
            return null;
        }

        var dxgi = BinaryPrimitives.ReadUInt32LittleEndian(data[HeaderSize..]);
        var source = data[(HeaderSize + Dx10HeaderSize)..];

        // Blockformate: DXGI 70-72 = BC1 (wie DXT1), 73-75 = BC2 (DXT3), 76-78 = BC3 (DXT5), 97-99 = BC7. sRGB wird wie UNORM behandelt.
        switch (dxgi)
        {
            case >= 70 and <= 72:
                return DecodeBlocks(source, width, height, BlockFormat.Dxt1);
            case >= 73 and <= 75:
                return DecodeBlocks(source, width, height, BlockFormat.Dxt3);
            case >= 76 and <= 78:
                return DecodeBlocks(source, width, height, BlockFormat.Dxt5);
            case >= 97 and <= 99:
                return Bc7Decoder.Decode(source, width, height);
        }

        var swapRedBlue = dxgi is 28 or 29;
        if (!swapRedBlue && dxgi is not (87 or 91))
        {
            return null;
        }

        var count = width * height;
        if (source.Length < (long)count * 4)
        {
            return null;
        }

        var pixels = source[..(count * 4)].ToArray();
        if (swapRedBlue)
        {
            for (var i = 0; i < pixels.Length; i += 4)
            {
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            }
        }

        return new GuiImage(width, height, pixels);
    }

    // --- Blockkomprimiert ---

    private static GuiImage? DecodeBlocks(ReadOnlySpan<byte> source, int width, int height, BlockFormat format)
    {
        var blockSize = format == BlockFormat.Dxt1 ? 8 : 16;
        var blocksX = (width + 3) / 4;
        var blocksY = (height + 3) / 4;
        if (source.Length < (long)blocksX * blocksY * blockSize)
        {
            return null;
        }

        var pixels = new byte[width * height * 4];
        Span<byte> colors = stackalloc byte[16]; // vier Farben als BGRA
        Span<byte> alphas = stackalloc byte[8];
        for (var by = 0; by < blocksY; by++)
        {
            for (var bx = 0; bx < blocksX; bx++)
            {
                var block = source.Slice(((by * blocksX) + bx) * blockSize, blockSize);
                var colorBlock = format == BlockFormat.Dxt1 ? block : block[8..];
                var c0 = BinaryPrimitives.ReadUInt16LittleEndian(colorBlock);
                var c1 = BinaryPrimitives.ReadUInt16LittleEndian(colorBlock[2..]);
                var indices = BinaryPrimitives.ReadUInt32LittleEndian(colorBlock[4..]);
                BuildColors(c0, c1, alwaysFour: format != BlockFormat.Dxt1, colors);

                ulong alphaBits = 0;
                if (format is BlockFormat.Dxt5 or BlockFormat.Rxgb)
                {
                    BuildAlphas(block[0], block[1], alphas);
                    for (var i = 5; i >= 0; i--)
                    {
                        alphaBits = (alphaBits << 8) | block[2 + i];
                    }
                }

                for (var p = 0; p < 16; p++)
                {
                    var x = (bx * 4) + (p % 4);
                    var y = (by * 4) + (p / 4);
                    if (x >= width || y >= height)
                    {
                        continue;
                    }

                    var color = colors.Slice((int)((indices >> (2 * p)) & 3) * 4, 4);
                    var target = ((y * width) + x) * 4;
                    pixels[target] = color[0];
                    pixels[target + 1] = color[1];
                    pixels[target + 2] = color[2];
                    switch (format)
                    {
                        case BlockFormat.Dxt1:
                            pixels[target + 3] = color[3];
                            break;
                        case BlockFormat.Dxt3:
                            // 4 Bit je Pixel, Pixel 0 im niedrigen Halbbyte; auf 8 Bit gestreckt (x17).
                            pixels[target + 3] = (byte)(((block[p / 2] >> ((p % 2) * 4)) & 0xF) * 17);
                            break;
                        case BlockFormat.Dxt5:
                            pixels[target + 3] = alphas[(int)((alphaBits >> (3 * p)) & 7)];
                            break;
                        default:
                            // RXGB: Rot steckt im Alphakanal; Alpha selbst ist undurchsichtig.
                            pixels[target + 2] = alphas[(int)((alphaBits >> (3 * p)) & 7)];
                            pixels[target + 3] = 255;
                            break;
                    }
                }
            }
        }

        return new GuiImage(width, height, pixels);
    }

    /// <summary>Die vier Farben eines Blocks als BGRA; bei DXT1 mit c0 &lt;= c1 sind es drei Farben und transparentes Schwarz.</summary>
    private static void BuildColors(ushort c0, ushort c1, bool alwaysFour, Span<byte> colors)
    {
        Expand(c0, colors[..4]);
        Expand(c1, colors.Slice(4, 4));
        if (alwaysFour || c0 > c1)
        {
            Mix(colors, 2, 1, 3, colors.Slice(8, 4));
            Mix(colors, 1, 2, 3, colors.Slice(12, 4));
        }
        else
        {
            Mix(colors, 1, 1, 2, colors.Slice(8, 4));
            colors.Slice(12, 4).Clear();
        }
    }

    /// <summary>565-Farbe als BGRA (Bits auf 8 Bit gestreckt).</summary>
    private static void Expand(ushort c, Span<byte> target)
    {
        var r = (c >> 11) & 31;
        var g = (c >> 5) & 63;
        var b = c & 31;
        target[0] = (byte)((b << 3) | (b >> 2));
        target[1] = (byte)((g << 2) | (g >> 4));
        target[2] = (byte)((r << 3) | (r >> 2));
        target[3] = 255;
    }

    /// <summary>Gewichtete Mischung der Farben 0 und 1 aus <paramref name="colors"/>, undurchsichtig.</summary>
    private static void Mix(Span<byte> colors, int weight0, int weight1, int sum, Span<byte> target)
    {
        for (var i = 0; i < 3; i++)
        {
            target[i] = (byte)(((weight0 * colors[i]) + (weight1 * colors[4 + i])) / sum);
        }

        target[3] = 255;
    }

    /// <summary>Die acht Alphawerte eines DXT5-Blocks.</summary>
    private static void BuildAlphas(byte a0, byte a1, Span<byte> alphas)
    {
        alphas[0] = a0;
        alphas[1] = a1;
        if (a0 > a1)
        {
            for (var i = 1; i < 7; i++)
            {
                alphas[i + 1] = (byte)((((7 - i) * a0) + (i * a1)) / 7);
            }
        }
        else
        {
            for (var i = 1; i < 5; i++)
            {
                alphas[i + 1] = (byte)((((5 - i) * a0) + (i * a1)) / 5);
            }

            alphas[6] = 0;
            alphas[7] = 255;
        }
    }
}
