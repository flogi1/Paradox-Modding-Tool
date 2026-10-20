using System.Collections.Concurrent;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Text;

/// <summary>Schriftschnitt aus <c>fontstyle = { style = … }</c>; <c>bold|italic</c> setzt beide Bits.</summary>
[Flags]
public enum JominiFontStyle
{
    Regular = 0,
    Bold = 1,
    Italic = 2,
}

/// <summary>
/// Schriften der Jomini-Spiele aus <c>&lt;Wurzel&gt;/fonts/*.font</c>: <c>fontfiles = { name group = { languages files } }</c> und
/// <c>font = { name fontstyle = { style fontfiles } }</c>. Die Wurzeln kommen in Suchreihenfolge der Texturen (<see cref="Layout.GuiTextureLocator.Roots"/>:
/// Mod, Abhängigkeiten, game/jomini/clausewitz je Kontext); bei gleichem Namen gewinnt die frühere Wurzel. Schriftdateien werden ebenso über die Wurzeln
/// gesucht. Nach <see cref="Load"/> von jedem Thread benutzbar.
/// </summary>
public sealed class JominiFontLibrary
{
    private const string FontsFolder = "fonts";

    private static readonly Lazy<JominiFontFace> SubstituteFace = new(CreateSubstitute, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly IReadOnlyList<string> _roots;
    private readonly Dictionary<string, Dictionary<JominiFontStyle, string>> _fonts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<FileGroup>> _fontFiles = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Font, JominiFontStyle Style, string Language), JominiFontFace?> _faces = new();

    private JominiFontLibrary(IReadOnlyList<string> roots) => _roots = roots;

    /// <summary>Ersatzschrift, wenn ein Spiel die verlangte Schrift nicht kennt: Segoe UI aus dem Windows-Schriftordner.</summary>
    public static JominiFontFace Substitute => SubstituteFace.Value;

    /// <summary>Liest alle <c>fonts/*.font</c> der Wurzeln (je Wurzel nach Dateiname sortiert). Unlesbare Dateien werden übersprungen.</summary>
    public static JominiFontLibrary Load(IReadOnlyList<string> searchRoots)
    {
        var library = new JominiFontLibrary(searchRoots.ToList());
        foreach (var root in library._roots)
        {
            foreach (var file in FontDefinitionFiles(root))
            {
                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                library.Read(GuiParser.Parse(text).Root);
            }
        }

        return library;
    }

    /// <summary>Gibt es einen <c>font</c> dieses Namens?</summary>
    public bool Defines(string fontName) => _fonts.ContainsKey(fontName);

    /// <summary>Keine einzige Schrift gefunden (Suchordner ohne <c>fonts/*.font</c>): dann ist kein Name "unbekannt", sondern alle fehlen.</summary>
    internal bool IsEmpty => _fonts.Count == 0;

    /// <summary>Namen aller definierten Schriften.</summary>
    internal IEnumerable<string> FontNames => _fonts.Keys;

    /// <summary>
    /// Schriftschnitt für Name, Stil und Sprache: der <c>fontstyle</c> mit genau diesem Stil, sonst <c>regular</c> mit simuliertem Fett/Kursiv; darin die
    /// <c>fontfiles</c>-Gruppe der Sprache, sonst die englische, sonst die erste. Null, wenn nichts davon existiert oder keine Datei lesbar ist.
    /// </summary>
    public JominiFontFace? Face(string fontName, JominiFontStyle style, string language) =>
        _faces.GetOrAdd((fontName, style, JominiLocalisation.Normalize(language)), key => CreateFace(key.Font, key.Style, key.Language));

    private JominiFontFace? CreateFace(string fontName, JominiFontStyle style, string language)
    {
        if (!_fonts.TryGetValue(fontName, out var styles))
        {
            return null;
        }

        bool simulateBold = false, simulateItalic = false;
        if (!styles.TryGetValue(style, out var fontFilesName))
        {
            if (!styles.TryGetValue(JominiFontStyle.Regular, out fontFilesName))
            {
                return null;
            }

            simulateBold = style.HasFlag(JominiFontStyle.Bold);
            simulateItalic = style.HasFlag(JominiFontStyle.Italic);
        }

        if (!_fontFiles.TryGetValue(fontFilesName, out var groups) || groups.Count == 0)
        {
            return null;
        }

        var group = groups.FirstOrDefault(g => g.Languages.Contains(language))
            ?? groups.FirstOrDefault(g => g.Languages.Contains(JominiLocalisation.English))
            ?? groups[0];

        // Die erste lesbare Datei wird gleich geöffnet (Metriken, Test auf "lesbar"); die übrigen erst, wenn ihr eine Glyphe fehlt. Victoria 3 hängt an
        // jede Gruppe CJK-Schriften von mehreren MB an, deren Öffnen je Datei bis zu Sekunden dauert und für lateinischen Text nie nötig ist.
        JominiFontFile? primary = null;
        var fallbacks = new List<string>();
        foreach (var entry in group.Files)
        {
            if (Resolve(entry) is not { } path)
            {
                continue;
            }

            if (primary is not null)
            {
                fallbacks.Add(path);
            }
            else
            {
                primary = JominiFontFile.Open(path);
            }
        }

        return primary is null ? null : new JominiFontFace(primary, fallbacks, simulateBold, simulateItalic, isSubstitute: false);
    }

    /// <summary>Erster existierender Pfad unter den Wurzeln. Führende Schrägstriche zählen nicht; ".." und absolute Pfade werden abgelehnt.</summary>
    private string? Resolve(string entry)
    {
        var relative = entry.Trim().Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        if (relative.Length == 0 || Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar).Contains(".."))
        {
            return null;
        }

        foreach (var root in _roots)
        {
            var full = Path.Combine(root, relative);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return null;
    }

    private static IEnumerable<string> FontDefinitionFiles(string root)
    {
        var folder = Path.Combine(root, FontsFolder);
        try
        {
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.font", SearchOption.TopDirectoryOnly).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()
                : Array.Empty<string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Übernimmt die Einträge einer Datei; schon bekannte Namen (aus früheren Wurzeln oder Dateien) bleiben.</summary>
    private void Read(GuiNode document)
    {
        foreach (var node in document.Children.Where(n => n.Kind == GuiNodeKind.KeyedBlock))
        {
            if (node.Name == "fontfiles" && Property(node, "name") is { Length: > 0 } fileName && !_fontFiles.ContainsKey(fileName))
            {
                _fontFiles[fileName] = Blocks(node, "group")
                    .Select(g => new FileGroup(
                        new HashSet<string>(ListItems(g, "languages").Select(JominiLocalisation.Normalize), StringComparer.Ordinal),
                        ListItems(g, "files").ToList()))
                    .ToList();
            }
            else if (node.Name == "font" && Property(node, "name") is { Length: > 0 } fontName && !_fonts.ContainsKey(fontName))
            {
                var styles = new Dictionary<JominiFontStyle, string>();
                foreach (var fontStyle in Blocks(node, "fontstyle"))
                {
                    if (ParseStyle(Property(fontStyle, "style")) is { } parsed && Property(fontStyle, "fontfiles") is { Length: > 0 } files)
                    {
                        styles.TryAdd(parsed, files);
                    }
                }

                _fonts[fontName] = styles;
            }
        }
    }

    /// <summary><c>regular</c>, <c>bold</c>, <c>italic</c> und Verbindungen mit "|". Andere Wörter (<c>semibold</c>, <c>extrabold</c>) machen den Eintrag ungültig.</summary>
    private static JominiFontStyle? ParseStyle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var style = JominiFontStyle.Regular;
        foreach (var part in value.Split('|', StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "regular":
                    break;
                case "bold":
                    style |= JominiFontStyle.Bold;
                    break;
                case "italic":
                    style |= JominiFontStyle.Italic;
                    break;
                default:
                    return null;
            }
        }

        return style;
    }

    private static string? Property(GuiNode block, string name) =>
        block.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.Property && c.Name == name)?.Value;

    private static IEnumerable<GuiNode> Blocks(GuiNode block, string name) =>
        block.Children.Where(c => c.Kind == GuiNodeKind.KeyedBlock && c.Name == name);

    private static IEnumerable<string> ListItems(GuiNode block, string name) =>
        Blocks(block, name).SelectMany(b => b.Children).Where(c => c.Kind == GuiNodeKind.ListItem && !string.IsNullOrEmpty(c.Value)).Select(c => c.Value!);

    private static JominiFontFace CreateSubstitute()
    {
        var path = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows", "Fonts", "segoeui.ttf");
        return new JominiFontFace(JominiFontFile.Open(path), Array.Empty<string>(), simulateBold: false, simulateItalic: false, isSubstitute: true);
    }

    private sealed record FileGroup(HashSet<string> Languages, List<string> Files);
}

/// <summary>
/// Ein aufgelöster Schriftschnitt: die Dateien in Rückfallreihenfolge und ob Fett oder Kursiv simuliert werden. Messen berührt kein WPF-Objekt und ist
/// parallel aufrufbar.
/// </summary>
public sealed class JominiFontFace
{
    private readonly JominiFontFile? _primary;
    private readonly Lazy<JominiFontFile?>[] _fallbacks;
    private readonly Lazy<IReadOnlyList<string>> _files;

    /// <param name="primary">Erste lesbare Datei (Metriken); null nur, wenn selbst die Ersatzschrift fehlt.</param>
    /// <param name="fallbackPaths">Weitere Dateien der Gruppe; geöffnet erst, wenn den vorigen eine Glyphe fehlt.</param>
    internal JominiFontFace(JominiFontFile? primary, IReadOnlyList<string> fallbackPaths, bool simulateBold, bool simulateItalic, bool isSubstitute)
    {
        _primary = primary;
        _fallbacks = fallbackPaths
            .Select(path => new Lazy<JominiFontFile?>(() => JominiFontFile.Open(path), LazyThreadSafetyMode.ExecutionAndPublication))
            .ToArray();
        _files = new Lazy<IReadOnlyList<string>>(
            () => (primary is null ? Enumerable.Empty<JominiFontFile>() : new[] { primary })
                .Concat(_fallbacks.Select(f => f.Value).OfType<JominiFontFile>())
                .Select(f => f.FullPath)
                .ToList(),
            LazyThreadSafetyMode.ExecutionAndPublication);
        SimulateBold = simulateBold;
        SimulateItalic = simulateItalic;
        IsSubstitute = isSubstitute;
    }

    /// <summary>Vollständige Pfade der lesbaren Schriftdateien in Rückfallreihenfolge (öffnet dafür alle Rückfalldateien).</summary>
    public IReadOnlyList<string> Files => _files.Value;

    public bool SimulateBold { get; }

    public bool SimulateItalic { get; }

    /// <summary>Ersatzschrift (Segoe UI), weil das Spiel die Schrift nicht kennt.</summary>
    public bool IsSubstitute { get; }

    /// <summary>
    /// Vorschub eines Zeichens: aus der ersten Datei, die die Glyphe hat, sonst Glyphe 0 (".notdef") der ersten Datei. Gerundet je Glyphe
    /// (<c>Math.Round(Vorschub × Größe, AwayFromZero)</c>, siehe Breitenregel); ohne Unterschneidung. <paramref name="file"/> nennt die benutzte Datei.
    /// </summary>
    public double Advance(int codePoint, double fontSize, out string file)
    {
        if (_primary is null)
        {
            // Nur möglich, wenn selbst Segoe UI fehlt: halbe Schriftgröße als grobe Annahme, damit das Layout weiterläuft.
            file = string.Empty;
            return Math.Round(fontSize / 2, MidpointRounding.AwayFromZero);
        }

        if (_primary.TryGlyph(codePoint, out var glyph))
        {
            file = _primary.FullPath;
            return Round(_primary.AdvanceOf(glyph), fontSize);
        }

        foreach (var fallback in _fallbacks)
        {
            if (fallback.Value is { } candidate && candidate.TryGlyph(codePoint, out glyph))
            {
                file = candidate.FullPath;
                return Round(candidate.AdvanceOf(glyph), fontSize);
            }
        }

        file = _primary.FullPath;
        return Round(_primary.AdvanceOf(0), fontSize);
    }

    /// <summary>Höhe über der Grundlinie aus der ersten Datei (<c>Baseline</c> × Größe).</summary>
    public double Ascent(double fontSize) => _primary is null ? fontSize * 0.8 : _primary.Baseline * fontSize;

    /// <summary>Tiefe unter der Grundlinie aus der ersten Datei (<c>(Height − Baseline)</c> × Größe).</summary>
    public double Descent(double fontSize) => _primary is null ? fontSize * 0.2 : (_primary.Height - _primary.Baseline) * fontSize;

    private static double Round(double advance, double fontSize) => Math.Round(advance * fontSize, MidpointRounding.AwayFromZero);
}
