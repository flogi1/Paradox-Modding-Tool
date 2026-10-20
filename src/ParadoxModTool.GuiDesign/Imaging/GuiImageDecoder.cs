using System.Buffers.Binary;

namespace ParadoxModTool.GuiDesign.Imaging;

/// <summary>
/// Dekodiert Texturen klassischer Spiele für die Vorschau. Gewählt wird nach der Endung (<c>.dds</c>, <c>.tga</c>); alles andere liefert
/// <see langword="null"/> (<c>.png</c> dekodiert WPF). Kaputte oder nicht unterstützte Dateien liefern ebenfalls <see langword="null"/>, nie eine Ausnahme.
/// </summary>
public static class GuiImageDecoder
{
    /// <summary>Größte erlaubte Kantenlänge; schützt vor riesigen Puffern durch kaputte Köpfe. HOI IV hat Streifen mit 12672 Pixeln Breite.</summary>
    internal const int MaxSize = 16384;

    /// <summary>Größte erlaubte Pixelzahl (64 Millionen, 256 MB als BGRA).</summary>
    internal const long MaxPixels = 64L * 1024 * 1024;

    private static long _decodeCalls;

    /// <summary>Aufrufe von <see cref="TryDecode"/> seit Programmstart; Tests prüfen damit, dass der Aufbau des Arbeitsstands kein Bild dekodiert.</summary>
    internal static long DecodeCalls => Interlocked.Read(ref _decodeCalls);

    public static GuiImage? TryDecode(string path)
    {
        Interlocked.Increment(ref _decodeCalls);
        var extension = Path.GetExtension(path);
        var isDds = extension.Equals(".dds", StringComparison.OrdinalIgnoreCase);
        var isTga = extension.Equals(".tga", StringComparison.OrdinalIgnoreCase);
        if (!isDds && !isTga)
        {
            return null;
        }

        try
        {
            var data = File.ReadAllBytes(path);
            return isDds ? DdsDecoder.Decode(data) : TgaDecoder.Decode(data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException
            or IndexOutOfRangeException or OverflowException or OutOfMemoryException)
        {
            // Die Vorschau zeigt dann "keine Vorschau"; die Datei selbst bleibt unberührt.
            return null;
        }
    }

    /// <summary>
    /// Kurzer Name des DDS-Formats im Kopf <paramref name="head"/> (mindestens die ersten 128 Byte, bei DX10 148), wenn <see cref="DdsDecoder"/> es nicht
    /// dekodiert, sonst <see langword="null"/>. Der Name steht im Platzhalter, z.B. <c>DDS DXGI 83 (BC5)</c> oder <c>DDS FourCC ATI2</c>. Die Liste der
    /// unterstützten Formate muss mit <see cref="DdsDecoder"/> übereinstimmen.
    /// </summary>
    internal static string? DescribeUnsupported(ReadOnlySpan<byte> head)
    {
        const int headerSize = 128;
        const int dx10HeaderSize = 148;
        if (head.Length < headerSize || !head[..4].SequenceEqual("DDS "u8))
        {
            return GuiTexts.TextureFormatDdsTruncated;
        }

        var height = BinaryPrimitives.ReadInt32LittleEndian(head[12..]);
        var width = BinaryPrimitives.ReadInt32LittleEndian(head[16..]);
        if (!IsValidSize(width, height))
        {
            return GuiTexts.TextureFormatDdsBadSize;
        }

        var pixelFlags = BinaryPrimitives.ReadUInt32LittleEndian(head[80..]);
        if ((pixelFlags & 0x4) != 0)
        {
            var fourCc = head.Slice(84, 4);
            if (fourCc.SequenceEqual("DXT1"u8) || fourCc.SequenceEqual("DXT3"u8) || fourCc.SequenceEqual("DXT5"u8) || fourCc.SequenceEqual("RXGB"u8))
            {
                return null;
            }

            if (!fourCc.SequenceEqual("DX10"u8))
            {
                var printable = fourCc.ToArray().All(b => b is >= 0x20 and < 0x7F);
                return "DDS FourCC " + (printable
                    ? System.Text.Encoding.ASCII.GetString(fourCc)
                    : "0x" + BinaryPrimitives.ReadUInt32LittleEndian(fourCc).ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
            }

            if (head.Length < dx10HeaderSize)
            {
                return "DDS DX10-Kopf unvollständig";
            }

            var dxgi = BinaryPrimitives.ReadUInt32LittleEndian(head[headerSize..]);
            if (dxgi is 28 or 29 or 87 or 91 or (>= 70 and <= 78) or (>= 97 and <= 99))
            {
                return null;
            }

            // Nicht dekodierte Formate: BC4/BC5 (79-84), BC6H (94-96) und Fließkomma (2 = RGBA32, 10 = RGBA16).
            var name = dxgi switch
            {
                >= 79 and <= 81 => "BC4",
                >= 82 and <= 84 => "BC5",
                >= 94 and <= 96 => "BC6H",
                2 or 10 => "Float",
                _ => null,
            };
            return "DDS DXGI " + dxgi.ToString(System.Globalization.CultureInfo.InvariantCulture) + (name is null ? string.Empty : " (" + name + ")");
        }

        if ((pixelFlags & 0x40) == 0)
        {
            return "DDS ohne Pixelformat";
        }

        var bits = BinaryPrimitives.ReadInt32LittleEndian(head[88..]);
        if (bits is not (16 or 24 or 32))
        {
            return "DDS RGB " + bits.ToString(System.Globalization.CultureInfo.InvariantCulture) + " Bit";
        }

        var masks = BinaryPrimitives.ReadUInt32LittleEndian(head[92..]) | BinaryPrimitives.ReadUInt32LittleEndian(head[96..]) | BinaryPrimitives.ReadUInt32LittleEndian(head[100..]);
        return masks == 0 ? "DDS ohne Farbmasken" : null;
    }

    /// <summary>Breite, Höhe und Pixelzahl liegen im sinnvollen Bereich.</summary>
    internal static bool IsValidSize(int width, int height) =>
        width is > 0 and <= MaxSize && height is > 0 and <= MaxSize && (long)width * height <= MaxPixels;
}
