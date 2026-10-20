namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Löst Schriftnamen aus <c>.gui</c> (<c>hoi_24header</c>) zu gelesenen Bitmap-Schriften auf: Name über das <c>.gfx</c>-Verzeichnis zum Pfad,
/// <c>.fnt</c> in den Wurzeln des Spiels suchen, Seitentexturen neben der <c>.fnt</c>. Gemerkt je Name und Vorschaulauf des
/// <see cref="ClassicTextureLocator"/>: Der Zeichner fragt bei jedem Bild, das kostet im selben Lauf nichts; im nächsten Lauf wird eine fehlende Schrift
/// neu gesucht und eine gelesene nur dann neu gelesen, wenn sich Schreibzeitpunkt oder Länge der <c>.fnt</c> geändert haben. Parallel aufrufbar.
/// </summary>
public sealed class ClassicFontLibrary
{
    private readonly ClassicSpriteIndex _sprites;
    private readonly ClassicTextureLocator _textures;
    private readonly Dictionary<string, Entry> _cache = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public ClassicFontLibrary(ClassicSpriteIndex sprites, ClassicTextureLocator textures)
    {
        _sprites = sprites;
        _textures = textures;
    }

    /// <summary>
    /// Schrift, voller Pfad der <c>.fnt</c> und voller Pfad je Seitentextur (<see langword="null"/>: Textur fehlt; <c>.tga</c> fällt auf <c>.dds</c> zurück).
    /// <see langword="null"/>, wenn es den Namen nicht gibt oder die <c>.fnt</c> fehlt oder nicht lesbar ist.
    /// </summary>
    public (BitmapFont Font, string FntPath, IReadOnlyList<string?> PageTexturePaths)? Get(string fontName)
    {
        var generation = _textures.Generation;
        Entry? known;
        lock (_gate)
        {
            _cache.TryGetValue(fontName, out known);
        }

        if (known is not null && known.Generation == generation)
        {
            return known.Font;
        }

        // Gelesene Schrift aus einem früheren Lauf: dieselbe .fnt unverändert und alle Seitentexturen gefunden, dann gilt sie weiter. Fehlte eine Seite,
        // wird neu gelesen, damit eine nachträglich angelegte Seitentextur erscheint.
        if (known?.Font is { } font && font.PageTexturePaths.All(page => page is not null)
            && _sprites.Font(fontName) is { } entry && _textures.Locate(entry.FontPath + ".fnt") is { } fntPath
            && string.Equals(fntPath, font.FntPath, StringComparison.OrdinalIgnoreCase) && StampOf(fntPath) == known.Stamp)
        {
            Store(fontName, new Entry(font, known.Stamp, generation));
            return font;
        }

        // Außerhalb der Sperre lesen; lesen zwei Aufrufer gleichzeitig, gilt das zuletzt gespeicherte Ergebnis.
        var loaded = Load(fontName);
        Store(fontName, new Entry(loaded, loaded is { } read ? StampOf(read.FntPath) : default, generation));
        return loaded;
    }

    private void Store(string fontName, Entry entry)
    {
        lock (_gate)
        {
            _cache[fontName] = entry;
        }
    }

    private (BitmapFont Font, string FntPath, IReadOnlyList<string?> PageTexturePaths)? Load(string fontName)
    {
        if (_sprites.Font(fontName) is not { } entry || _textures.Locate(entry.FontPath + ".fnt") is not { } fntPath)
        {
            return null;
        }

        BitmapFont font;
        try
        {
            font = BitmapFont.Parse(File.ReadAllText(fntPath));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        // Seitentexturen liegen relativ zum Ordner der .fnt, gesucht in denselben Wurzeln.
        var path = entry.FontPath.Replace('\\', '/');
        var folder = path.Contains('/') ? path[..path.LastIndexOf('/')] + "/" : string.Empty;
        var pages = font.Pages.Select(page => page.Length == 0 ? null : _textures.Locate(folder + page)).ToList();

        // Seite 0 ohne Datei (oder .fnt ohne page-Zeile): die Textur mit dem Namen der Schrift, <pfad>.tga bzw. .dds. In HOI IV fehlt die genannte
        // Seitendatei bei 50 von 59 Schriften, in EU IV bei allen 24, und daneben liegt jeweils diese Textur; im Spiel erscheinen die Texte (Probe-Bild
        // 2026-10-17). Ausgewertet, nicht einzeln gemessen.
        if (pages.Count == 0)
        {
            pages.Add(null);
        }

        pages[0] ??= _textures.Locate(entry.FontPath + ".tga");
        return (font, fntPath, pages);
    }

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

    private sealed record Entry(
        (BitmapFont Font, string FntPath, IReadOnlyList<string?> PageTexturePaths)? Font, (DateTime WriteTime, long Length) Stamp, int Generation);
}
