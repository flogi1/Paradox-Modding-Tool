using System.Buffers.Binary;

namespace ParadoxModTool.GuiDesign.Imaging;

/// <summary>
/// Kopf einer Bilddatei: Breite, Höhe und, falls das Bild nicht dekodiert werden kann, eine kurze Bezeichnung des Formats für den Platzhalter
/// (<c>null</c>: dekodierbar). Eine leere Datei hat Breite und Höhe 0 und die Bezeichnung <see cref="GuiTexts.TextureFormatEmptyFile"/>.
/// </summary>
public readonly record struct TextureHeader(int Width, int Height, string? UnsupportedFormat);

/// <summary>
/// Findet Texturpfade (<c>gfx//interface//x.tga</c>) in den Wurzeln des Spiels und merkt sich Pfad und Kopf (Größe, Format). Gelesen wird nur der Kopf;
/// Bilder werden hier nie dekodiert. Parallel aufrufbar. Gemeinsame Basis des klassischen (<c>ClassicTextureLocator</c>) und des Jomini-Suchers
/// (<c>GuiTextureLocator</c>).
/// <para>
/// Gemerkt wird je Vorschaulauf (<see cref="NewRun"/>, jede Layout-Runde beginnt einen): Ein Fehlschlag gilt nur im selben Lauf, im nächsten wird erneut
/// gesucht, damit eine nachträglich angelegte Textur in der nächsten Vorschau erscheint. Ein Treffer bleibt gemerkt und wird je Lauf nur mit einem
/// <see cref="File.Exists(string)"/> bestätigt (statt alle Wurzeln, bei EU IV rund 146 Prüfungen je <c>.tga</c>, erneut zu durchsuchen). Ein Kopf gilt,
/// solange Schreibzeitpunkt und Länge der Datei gleich sind, ebenfalls einmal je Lauf geprüft. Eine Textur, die später in einer vorrangigen Wurzel
/// (Mod vor Vanilla) dazukommt, sieht die Vorschau erst nach dem Neuaufbau des Arbeitsbereichs.
/// </para>
/// </summary>
public class TextureLocator
{
    /// <summary>So viele Byte vom Dateianfang genügen für jeden Kopf (JPG-Marker liegen vor der ersten Bildzeile, meist unter 20 KB).</summary>
    private const int HeadSize = 64 * 1024;

    private static readonly byte[] PngSignature = { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };

    private readonly IReadOnlyList<string> _roots;
    private readonly bool _tgaFallsBackToDds;

    // Schlüssel: der vereinheitlichte relative Pfad ("gfx\interface\x.tga"), ohne Rücksicht auf Groß-/Kleinschreibung (Windows).
    private readonly Dictionary<string, Located> _located = new(StringComparer.OrdinalIgnoreCase);

    // Schlüssel: der gefundene volle Pfad.
    private readonly Dictionary<string, HeaderEntry> _headers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private int _generation;
    private int _searches;

    /// <param name="rootsInSearchOrder">Wurzeln in Suchreihenfolge: Mod, Abhängigkeits-Mods, dann Vanilla (klassisch rückwärts, DLC-Ordner vor der Installation).</param>
    /// <param name="tgaFallsBackToDds">Fehlt eine <c>.tga</c>, gilt die gleichnamige <c>.dds</c> (klassische Spiele).</param>
    public TextureLocator(IReadOnlyList<string> rootsInSearchOrder, bool tgaFallsBackToDds)
    {
        _roots = rootsInSearchOrder;
        _tgaFallsBackToDds = tgaFallsBackToDds;
    }

    public IReadOnlyList<string> Roots => _roots;

    /// <summary>Zahl der Suchen durch alle Wurzeln (für Tests der Merkliste).</summary>
    internal int Searches => Volatile.Read(ref _searches);

    /// <summary>Nummer des aktuellen Vorschaulaufs; die Schriften richten ihre Merkliste danach.</summary>
    internal int Generation => Volatile.Read(ref _generation);

    /// <summary>Beginnt einen neuen Vorschaulauf: Fehlschläge werden neu gesucht, Treffer und Köpfe einmal neu bestätigt.</summary>
    public void NewRun() => Interlocked.Increment(ref _generation);

    /// <summary>Voller Pfad der Textur oder <see langword="null"/>, wenn es sie nicht gibt.</summary>
    public string? Locate(string texturePath)
    {
        if (Relative(texturePath) is not { } relative)
        {
            return null;
        }

        var generation = Generation;
        Located? known;
        lock (_gate)
        {
            _located.TryGetValue(relative, out known);
        }

        if (known is not null)
        {
            if (known.Generation == generation)
            {
                return known.FullPath;
            }

            // Treffer aus einem früheren Lauf: nur bestätigen. Ist die Datei weg, wird unten neu gesucht.
            if (known.FullPath is { } previous && File.Exists(previous))
            {
                Store(relative, previous, generation);
                return previous;
            }
        }

        var found = Search(relative);
        Store(relative, found, generation);
        return found;
    }

    /// <summary>
    /// Größe aus dem Kopf der Datei (DDS, TGA, PNG, JPG). Parallel aufrufbar. <see langword="null"/>: Textur fehlt oder Kopf unlesbar (auch leere Datei).
    /// </summary>
    public (int Width, int Height)? SizeOf(string texturePath) =>
        HeaderOf(texturePath) is { Width: > 0 } header ? (header.Width, header.Height) : null;

    /// <summary>
    /// Kopf der Textur (Größe und, wenn nicht dekodierbar, Formatname), je Lauf einmal geprüft. <see langword="null"/>: Textur fehlt oder die Datei hat
    /// kein erkennbares Bildformat. Parallel aufrufbar.
    /// </summary>
    public TextureHeader? HeaderOf(string texturePath)
    {
        if (Locate(texturePath) is not { } full)
        {
            return null;
        }

        var generation = Generation;
        HeaderEntry? known;
        lock (_gate)
        {
            _headers.TryGetValue(full, out known);
        }

        if (known is not null && known.Generation == generation)
        {
            return known.Header;
        }

        var stamp = StampOf(full);
        var header = known is not null && known.Stamp == stamp ? known.Header : ReadHeader(full);
        lock (_gate)
        {
            _headers[full] = new HeaderEntry(stamp, header, generation);
        }

        return header;
    }

    /// <summary>"//", "\\" und "\" werden zu einem Trenner, ein führender fällt weg; ".." führt nie aus der Wurzel heraus.</summary>
    private static string? Relative(string texturePath)
    {
        var parts = texturePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Contains(".."))
        {
            return null;
        }

        var relative = string.Join(Path.DirectorySeparatorChar, parts);
        return Path.IsPathRooted(relative) ? null : relative;
    }

    private string? Search(string relative)
    {
        Interlocked.Increment(ref _searches);
        var fallback = _tgaFallsBackToDds && relative.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) ? Path.ChangeExtension(relative, ".dds") : null;
        foreach (var root in _roots)
        {
            var full = Path.Combine(root, relative);
            if (File.Exists(full))
            {
                return full;
            }

            if (fallback is not null && Path.Combine(root, fallback) is var dds && File.Exists(dds))
            {
                return dds;
            }
        }

        return null;
    }

    private void Store(string relative, string? fullPath, int generation)
    {
        lock (_gate)
        {
            _located[relative] = new Located(fullPath, generation);
        }
    }

    /// <summary>Schreibzeitpunkt und Länge; eine fehlende Datei hat (default, -1).</summary>
    private static (DateTime WriteTime, long Length) StampOf(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? (info.LastWriteTimeUtc, info.Length) : (default, -1);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (default, -1);
        }
    }

    /// <summary>Größe aus dem Kopf einer Bilddatei (DDS, TGA, PNG, JPG) ohne Suche und Merkliste. <see langword="null"/>: Datei fehlt oder Kopf unlesbar.</summary>
    public static (int Width, int Height)? ReadSizeOfFile(string fullPath) =>
        ReadHeader(fullPath) is { Width: > 0 } header ? (header.Width, header.Height) : null;

    /// <summary>
    /// Liest höchstens 64 KB, einmal. Das Bildformat bestimmen die ersten Byte (<c>DDS </c>, PNG-Signatur, <c>FF D8</c>); nur TGA hat keine und gilt
    /// nach der Endung <c>.tga</c>. Leere Datei: <c>(0, 0, </c><see cref="GuiTexts.TextureFormatEmptyFile"/><c>)</c>. <see langword="null"/>: Datei fehlt oder kein erkennbares Bildformat.
    /// </summary>
    public static TextureHeader? ReadHeader(string fullPath)
    {
        try
        {
            using var stream = File.OpenRead(fullPath);
            var head = new byte[HeadSize];
            var read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            if (read == 0)
            {
                return new TextureHeader(0, 0, GuiTexts.TextureFormatEmptyFile);
            }

            var data = head.AsSpan(0, read);
            if (data.StartsWith("DDS "u8))
            {
                // Höhe an Offset 12, Breite an Offset 16 (Little Endian).
                if (read < 20)
                {
                    return null;
                }

                var height = BinaryPrimitives.ReadInt32LittleEndian(data[12..]);
                var width = BinaryPrimitives.ReadInt32LittleEndian(data[16..]);
                return width > 0 && height > 0 ? new TextureHeader(width, height, GuiImageDecoder.DescribeUnsupported(data)) : null;
            }

            if (data.StartsWith(PngSignature))
            {
                // Signatur (8), Länge (4), "IHDR" (4), dann Breite und Höhe als Big Endian.
                if (read < 24 || !data.Slice(12, 4).SequenceEqual("IHDR"u8))
                {
                    return null;
                }

                var width = BinaryPrimitives.ReadInt32BigEndian(data[16..]);
                var height = BinaryPrimitives.ReadInt32BigEndian(data[20..]);
                return width > 0 && height > 0 ? new TextureHeader(width, height, null) : null;
            }

            if (read >= 2 && data[0] == 0xFF && data[1] == 0xD8)
            {
                return ReadJpegHeader(data);
            }

            if (fullPath.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) && read >= 18)
            {
                // TGA-Kopf: 18 Byte, Breite und Höhe als 16 Bit Little Endian an Offset 12 und 14.
                var width = BinaryPrimitives.ReadUInt16LittleEndian(data[12..]);
                var height = BinaryPrimitives.ReadUInt16LittleEndian(data[14..]);
                return width > 0 && height > 0 ? new TextureHeader(width, height, null) : null;
            }

            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Läuft die Marker ab <c>FF D8</c> ab und liest am ersten SOF-Marker (<c>C0</c>–<c>CF</c> außer <c>C4</c>, <c>C8</c>, <c>CC</c>) Höhe (+5) und
    /// Breite (+7) als Big Endian. <see langword="null"/>: kein SOF im gelesenen Anfang.
    /// </summary>
    private static TextureHeader? ReadJpegHeader(ReadOnlySpan<byte> data)
    {
        var position = 2;
        while (position + 4 <= data.Length)
        {
            if (data[position] != 0xFF)
            {
                return null;
            }

            var marker = data[position + 1];
            if (marker == 0xFF)
            {
                // Füllbyte vor einem Marker.
                position++;
                continue;
            }

            if (marker is 0x01 or (>= 0xD0 and <= 0xD8))
            {
                // Marker ohne Länge.
                position += 2;
                continue;
            }

            if (marker is 0xD9 or 0xDA)
            {
                // Ende des Bildes oder Beginn der Bilddaten ohne vorheriges SOF.
                return null;
            }

            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
            {
                if (position + 9 > data.Length)
                {
                    return null;
                }

                var height = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 5)..]);
                var width = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 7)..]);
                return width > 0 && height > 0 ? new TextureHeader(width, height, null) : null;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 2)..]);
            if (length < 2)
            {
                return null;
            }

            position += 2 + length;
        }

        return null;
    }

    private sealed record Located(string? FullPath, int Generation);

    private sealed record HeaderEntry((DateTime WriteTime, long Length) Stamp, TextureHeader? Header, int Generation);
}
