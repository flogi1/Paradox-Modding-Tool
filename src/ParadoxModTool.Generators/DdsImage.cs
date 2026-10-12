using System.Buffers.Binary;

namespace ParadoxModTool.Generators;

/// <summary>Dekodierte Pixel einer DDS-Datei: BGRA, 4 Byte je Pixel, Zeile für Zeile.</summary>
public sealed record DdsPixels(int Width, int Height, byte[] Bgra);

/// <summary>
/// Kleiner DDS-Decoder für die Vorschau von Bildfeldern (Wappen der IV2-Bibliothek): DXT1 (mit 1-Bit-Transparenz) und DXT5. Andere Formate und
/// kaputte Dateien liefern <see langword="null"/>; die Vorschau zeigt dann "keine Vorschau". Rechnung wie im Python-IV2-Generator.
/// </summary>
public static class DdsImage
{
    private const int HeaderSize = 128;

    public static DdsPixels? Decode(byte[] data)
    {
        if (data.Length < HeaderSize || data[0] != 'D' || data[1] != 'D' || data[2] != 'S' || data[3] != ' ')
        {
            return null;
        }

        var height = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(12));
        var width = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(16));
        var fourCc = System.Text.Encoding.ASCII.GetString(data, 84, 4);
        var blockSize = fourCc switch { "DXT1" => 8, "DXT5" => 16, _ => 0 };
        if (blockSize == 0 || width <= 0 || height <= 0 || width > 8192 || height > 8192)
        {
            return null;
        }

        var blocksX = (width + 3) / 4;
        var blocksY = (height + 3) / 4;
        if (data.Length < HeaderSize + (long)blocksX * blocksY * blockSize)
        {
            return null;
        }

        var pixels = new byte[width * height * 4];
        var colours = new byte[4][];
        var alphas = new byte[8];
        for (var by = 0; by < blocksY; by++)
        {
            for (var bx = 0; bx < blocksX; bx++)
            {
                var offset = HeaderSize + ((by * blocksX) + bx) * blockSize;
                ulong alphaBits = 0;
                if (blockSize == 16)
                {
                    Alphas(data[offset], data[offset + 1], alphas);
                    for (var i = 5; i >= 0; i--)
                    {
                        alphaBits = (alphaBits << 8) | data[offset + 2 + i];
                    }

                    offset += 8;
                }

                var c0 = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
                var c1 = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 2));
                Colours(c0, c1, alwaysFour: blockSize == 16, colours);
                var indices = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 4));
                for (var p = 0; p < 16; p++)
                {
                    var x = (bx * 4) + (p % 4);
                    var y = (by * 4) + (p / 4);
                    if (x >= width || y >= height)
                    {
                        continue;
                    }

                    var colour = colours[(indices >> (2 * p)) & 3];
                    var target = ((y * width) + x) * 4;
                    pixels[target] = colour[0];
                    pixels[target + 1] = colour[1];
                    pixels[target + 2] = colour[2];
                    pixels[target + 3] = blockSize == 16 ? alphas[(alphaBits >> (3 * p)) & 7] : colour[3];
                }
            }
        }

        return new DdsPixels(width, height, pixels);
    }

    /// <summary>Die vier Farben eines Blocks als BGRA; bei DXT1 mit c0 &lt;= c1 sind es drei Farben und transparentes Schwarz.</summary>
    private static void Colours(ushort c0, ushort c1, bool alwaysFour, byte[][] colours)
    {
        var a = Expand(c0);
        var b = Expand(c1);
        colours[0] = a;
        colours[1] = b;
        if (alwaysFour || c0 > c1)
        {
            colours[2] = Mix(a, b, 2, 1, 3);
            colours[3] = Mix(a, b, 1, 2, 3);
        }
        else
        {
            colours[2] = Mix(a, b, 1, 1, 2);
            colours[3] = new byte[] { 0, 0, 0, 0 };
        }
    }

    /// <summary>565-Farbe als BGRA (Bits auf 8 Bit gestreckt).</summary>
    private static byte[] Expand(ushort c)
    {
        var r = (c >> 11) & 31;
        var g = (c >> 5) & 63;
        var b = c & 31;
        return new[] { (byte)((b << 3) | (b >> 2)), (byte)((g << 2) | (g >> 4)), (byte)((r << 3) | (r >> 2)), (byte)255 };
    }

    private static byte[] Mix(byte[] a, byte[] b, int wa, int wb, int sum) =>
        new[] { (byte)(((wa * a[0]) + (wb * b[0])) / sum), (byte)(((wa * a[1]) + (wb * b[1])) / sum), (byte)(((wa * a[2]) + (wb * b[2])) / sum), (byte)255 };

    /// <summary>Die acht Alphawerte eines DXT5-Blocks.</summary>
    private static void Alphas(byte a0, byte a1, byte[] alphas)
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
