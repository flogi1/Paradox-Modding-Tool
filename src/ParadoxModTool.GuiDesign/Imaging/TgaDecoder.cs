using System.Buffers.Binary;

namespace ParadoxModTool.GuiDesign.Imaging;

/// <summary>
/// TGA-Decoder: Bildtyp 2 (unkomprimiert) und 10 (RLE), 16 (A1R5G5B5, kommt in EU-IV-Gebäudesymbolen vor), 24 und 32 Bit. Bit 5 des Deskriptors sagt, ob die erste Zeile oben (gesetzt) oder unten
/// liegt. Bild-ID und Farbtabelle werden übersprungen. Alles andere liefert <see langword="null"/>.
/// </summary>
public static class TgaDecoder
{
    private const int HeaderSize = 18;

    public static GuiImage? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            return null;
        }

        var idLength = data[0];
        var colorMapType = data[1];
        var imageType = data[2];
        var colorMapLength = BinaryPrimitives.ReadUInt16LittleEndian(data[5..]);
        var colorMapEntryBits = data[7];
        var width = BinaryPrimitives.ReadUInt16LittleEndian(data[12..]);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(data[14..]);
        var bits = data[16];
        var topDown = (data[17] & 0x20) != 0;

        // 16 Bit (A1R5G5B5): hat der Kopf ein Attribut-Bit (Deskriptor, untere 4 Bit = 1), gilt das oberste Bit als Alpha, sonst ist das Bild undurchsichtig.
        var alphaBit = (data[17] & 0x0F) == 1;
        if ((imageType != 2 && imageType != 10) || bits is not (16 or 24 or 32) || colorMapType > 1 || !GuiImageDecoder.IsValidSize(width, height))
        {
            return null;
        }

        var offset = HeaderSize + idLength;
        if (colorMapType == 1)
        {
            offset += colorMapLength * ((colorMapEntryBits + 7) / 8);
        }

        if (offset > data.Length)
        {
            return null;
        }

        var bytesPerPixel = bits / 8;
        var count = width * height;
        var source = data[offset..];

        // Erst die Länge der Nutzdaten prüfen, dann den Pixelpuffer anlegen (ein kaputter Kopf mit 8192 × 8192 hätte sonst 256 MB belegt). RLE: ein
        // Paket aus Kopfbyte und einem Pixel deckt höchstens 128 Pixel ab, weniger Daten können das Bild nie füllen.
        var minimum = imageType == 2 ? (long)count * bytesPerPixel : (count + 127L) / 128 * (1 + bytesPerPixel);
        if (source.Length < minimum)
        {
            return null;
        }

        var pixels = new byte[count * 4];
        if (imageType == 2)
        {
            for (var i = 0; i < count; i++)
            {
                WritePixel(pixels, i, source.Slice(i * bytesPerPixel, bytesPerPixel), alphaBit);
            }
        }
        else if (!DecodeRle(source, bytesPerPixel, count, alphaBit, pixels))
        {
            return null;
        }

        if (!topDown)
        {
            FlipRows(pixels, width, height);
        }

        return new GuiImage(width, height, pixels);
    }

    /// <summary>RLE-Pakete: Bit 7 gesetzt = ein Pixel, (n + 1)-mal wiederholt; sonst n + 1 Rohpixel. Pakete dürfen über Zeilengrenzen laufen.</summary>
    private static bool DecodeRle(ReadOnlySpan<byte> source, int bytesPerPixel, int count, bool alphaBit, byte[] pixels)
    {
        var position = 0;
        var written = 0;
        while (written < count)
        {
            if (position >= source.Length)
            {
                return false;
            }

            var header = source[position++];
            var length = (header & 0x7F) + 1;
            var isRun = (header & 0x80) != 0;
            var needed = isRun ? bytesPerPixel : length * bytesPerPixel;
            if (position + needed > source.Length)
            {
                return false;
            }

            // Ein Paket, das über das Bildende hinausläuft, wird abgeschnitten.
            length = Math.Min(length, count - written);
            for (var i = 0; i < length; i++)
            {
                WritePixel(pixels, written++, source.Slice(isRun ? position : position + (i * bytesPerPixel), bytesPerPixel), alphaBit);
            }

            position += needed;
        }

        return true;
    }

    /// <summary>Schreibt ein 16-, 24- oder 32-Bit-Pixel (16: A1R5G5B5, sonst B, G, R[, A]) als BGRA.</summary>
    private static void WritePixel(byte[] pixels, int index, ReadOnlySpan<byte> pixel, bool alphaBit)
    {
        var target = index * 4;
        if (pixel.Length == 2)
        {
            var value = pixel[0] | (pixel[1] << 8);
            var b = value & 31;
            var g = (value >> 5) & 31;
            var r = (value >> 10) & 31;
            pixels[target] = (byte)((b << 3) | (b >> 2));
            pixels[target + 1] = (byte)((g << 3) | (g >> 2));
            pixels[target + 2] = (byte)((r << 3) | (r >> 2));
            pixels[target + 3] = alphaBit && (value & 0x8000) == 0 ? (byte)0 : (byte)255;
            return;
        }

        pixels[target] = pixel[0];
        pixels[target + 1] = pixel[1];
        pixels[target + 2] = pixel[2];
        pixels[target + 3] = pixel.Length == 4 ? pixel[3] : (byte)255;
    }

    private static void FlipRows(byte[] pixels, int width, int height)
    {
        var stride = width * 4;
        var row = new byte[stride];
        for (var y = 0; y < height / 2; y++)
        {
            var top = pixels.AsSpan(y * stride, stride);
            var bottom = pixels.AsSpan((height - 1 - y) * stride, stride);
            top.CopyTo(row);
            bottom.CopyTo(top);
            row.CopyTo(bottom);
        }
    }
}
