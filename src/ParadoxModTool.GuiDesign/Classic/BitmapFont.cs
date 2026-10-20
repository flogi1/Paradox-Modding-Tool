using System.Globalization;
using System.Text.RegularExpressions;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>Ein Zeichen einer Bitmap-Schrift: Rechteck auf der Seitentextur, Versatz beim Zeichnen und Vorschub zum nächsten Zeichen.</summary>
public sealed record BitmapGlyph(int X, int Y, int Width, int Height, int XOffset, int YOffset, int XAdvance, int Page);

/// <summary>
/// Eine Bitmap-Schrift im BMFont-Textformat (<c>.fnt</c> der klassischen Spiele). Gelesen werden <c>common</c> (Zeilenhöhe, Grundlinie), <c>page</c>,
/// <c>char</c> und <c>kerning</c>; <c>info</c> und <c>chars</c> sind für die Vorschau unwichtig. Kaputte Zeilen werden übersprungen. Unveränderlich.
/// </summary>
public sealed class BitmapFont
{
    private const int MaxPages = 256;

    private static readonly Regex Pair = new("(\\w+)=(\"[^\"]*\"|\\S*)", RegexOptions.Compiled);

    private readonly Dictionary<char, BitmapGlyph> _glyphs;
    private readonly Dictionary<(char, char), int> _kerning;

    private BitmapFont(int lineHeight, int baseLine, IReadOnlyList<string> pages, Dictionary<char, BitmapGlyph> glyphs, Dictionary<(char, char), int> kerning)
    {
        LineHeight = lineHeight;
        Base = baseLine;
        Pages = pages;
        _glyphs = glyphs;
        _kerning = kerning;
    }

    public int LineHeight { get; }

    /// <summary>Abstand der Grundlinie von der Oberkante der Zeile.</summary>
    public int Base { get; }

    /// <summary>Dateinamen der Seitentexturen, nach Seitennummer. Eine nicht benannte Nummer bleibt leer.</summary>
    public IReadOnlyList<string> Pages { get; }

    public static BitmapFont Parse(string fntText)
    {
        int lineHeight = 0, baseLine = 0;
        var pages = new SortedDictionary<int, string>();
        var glyphs = new Dictionary<char, BitmapGlyph>();
        var kerning = new Dictionary<(char, char), int>();
        foreach (var raw in fntText.Split('\n'))
        {
            var line = raw.Trim();
            var space = line.IndexOf(' ');
            if (space <= 0)
            {
                continue;
            }

            var values = Values(line[space..]);
            switch (line[..space])
            {
                case "common":
                    lineHeight = Int(values, "lineHeight") ?? lineHeight;
                    baseLine = Int(values, "base") ?? baseLine;
                    break;
                case "page":
                    // Seitennummern über MaxPages gelten als kaputt (sonst würde die Seitenliste riesig oder ihre Größe läuft über).
                    if (Int(values, "id") is { } pageId and >= 0 and < MaxPages && values.TryGetValue("file", out var file))
                    {
                        pages[pageId] = file;
                    }

                    break;
                case "char":
                    // Zeichen über 0xFFFF (Ersatzpaare) kennt die Vorschau nicht.
                    if (Int(values, "id") is { } id and >= 0 and <= char.MaxValue && Int(values, "xadvance") is { } advance)
                    {
                        glyphs[(char)id] = new BitmapGlyph(
                            Int(values, "x") ?? 0, Int(values, "y") ?? 0, Int(values, "width") ?? 0, Int(values, "height") ?? 0,
                            Int(values, "xoffset") ?? 0, Int(values, "yoffset") ?? 0, advance, Int(values, "page") ?? 0);
                    }

                    break;
                case "kerning":
                    if (Int(values, "first") is { } first and >= 0 and <= char.MaxValue
                        && Int(values, "second") is { } second and >= 0 and <= char.MaxValue
                        && Int(values, "amount") is { } amount)
                    {
                        kerning[((char)first, (char)second)] = amount;
                    }

                    break;
            }
        }

        var pageNames = new string[pages.Count == 0 ? 0 : pages.Keys.Max() + 1];
        Array.Fill(pageNames, string.Empty);
        foreach (var (number, name) in pages)
        {
            pageNames[number] = name;
        }

        return new BitmapFont(lineHeight, baseLine, pageNames, glyphs, kerning);
    }

    public BitmapGlyph? Glyph(char c) => _glyphs.GetValueOrDefault(c);

    /// <summary>Kerning-Anpassung (meist negativ) zwischen zwei Zeichen, sonst 0.</summary>
    public int Kerning(char a, char b) => _kerning.GetValueOrDefault((a, b));

    /// <summary>Breite in Pixeln: Summe der Vorschübe plus Kerning. Ein fehlendes Zeichen zählt mit dem Vorschub von <c>?</c> (ohne <c>?</c>: 0).</summary>
    public int MeasureWidth(string text)
    {
        var width = 0;
        for (var i = 0; i < text.Length; i++)
        {
            width += (Glyph(text[i]) ?? Glyph('?'))?.XAdvance ?? 0;
            if (i + 1 < text.Length)
            {
                width += Kerning(text[i], text[i + 1]);
            }
        }

        return width;
    }

    /// <summary>Hat der Text Zeichen, die die Schrift nicht kennt (und die deshalb als <c>?</c> gelten)?</summary>
    public bool HasMissingGlyphs(string text) => text.Any(c => !_glyphs.ContainsKey(c));

    private static Dictionary<string, string> Values(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Pair.Matches(text))
        {
            values[match.Groups[1].Value] = match.Groups[2].Value.Trim('"');
        }

        return values;
    }

    private static int? Int(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;
}
