using System.Windows.Media;

namespace ParadoxModTool.GuiDesign.Text;

/// <summary>
/// Eine Schriftdatei (.ttf/.otf) mit Zeichentabelle und Glyphenbreiten als einfache Daten. WPF wird nur beim ersten Öffnen unter einer Sperre benutzt;
/// danach berührt das Messen kein WPF-Objekt mehr und läuft auf jedem Thread.
/// </summary>
internal sealed class JominiFontFile
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, (DateTime WriteTimeUtc, long Length, JominiFontFile? File)> Cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<int, ushort> _glyphs;
    private readonly double[] _advances;

    private JominiFontFile(string fullPath, Dictionary<int, ushort> glyphs, double[] advances, double baseline, double height)
    {
        FullPath = fullPath;
        _glyphs = glyphs;
        _advances = advances;
        Baseline = baseline;
        Height = height;
    }

    public string FullPath { get; }

    /// <summary>Grundlinie in Em (Abstand von oben).</summary>
    public double Baseline { get; }

    /// <summary>Zeilenhöhe in Em.</summary>
    public double Height { get; }

    /// <summary>
    /// Öffnet die Datei einmal je Pfad, Änderungszeit und Länge und merkt sich das Ergebnis, auch ein Scheitern. Null, wenn die Datei fehlt
    /// oder WPF sie nicht lesen kann.
    /// </summary>
    public static JominiFontFile? Open(string fullPath)
    {
        lock (Gate)
        {
            DateTime writeTime;
            long length;
            try
            {
                var info = new FileInfo(fullPath);
                if (!info.Exists)
                {
                    return null;
                }

                writeTime = info.LastWriteTimeUtc;
                length = info.Length;
            }
            catch (Exception)
            {
                return null;
            }

            if (Cache.TryGetValue(fullPath, out var cached) && cached.WriteTimeUtc == writeTime && cached.Length == length)
            {
                return cached.File;
            }

            var file = Read(fullPath);
            Cache[fullPath] = (writeTime, length, file);
            return file;
        }
    }

    /// <summary>Glyphe zum Zeichen, falls die Datei es hat.</summary>
    public bool TryGlyph(int codePoint, out ushort glyph) => _glyphs.TryGetValue(codePoint, out glyph);

    /// <summary>Vorschub einer Glyphe in Em; 0 außerhalb der Tabelle.</summary>
    public double AdvanceOf(ushort glyph) => glyph < _advances.Length ? _advances[glyph] : 0;

    private static JominiFontFile? Read(string fullPath)
    {
        try
        {
            // Kaputte Dateien werfen manchmal erst beim ersten Zugriff auf eine Eigenschaft, deshalb steht alles im try.
            var typeface = new GlyphTypeface(new Uri(fullPath));
            var glyphs = new Dictionary<int, ushort>(typeface.CharacterToGlyphMap);
            var widths = typeface.AdvanceWidths;
            var advances = new double[typeface.GlyphCount];
            for (var i = 0; i < advances.Length; i++)
            {
                advances[i] = widths.TryGetValue((ushort)i, out var width) ? width : 0;
            }

            return new JominiFontFile(fullPath, glyphs, advances, typeface.Baseline, typeface.Height);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
