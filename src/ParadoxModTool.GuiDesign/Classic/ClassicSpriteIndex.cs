using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Alle Sprites und Schriften der <c>.gfx</c>-Dateien eines klassischen Spiels (HOI IV, EU IV). Es gilt die Ladereihenfolge (<see cref="GameLoadOrder"/>,
/// bei gleichem Pfad die höhere Ebene): bei <c>spriteFirstWins</c> gewinnt der zuerst geladene Eintrag, sonst der zuletzt geladene. Unveränderlich.
/// </summary>
public sealed class ClassicSpriteIndex
{
    private static readonly Dictionary<string, ClassicSpriteKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["spriteType"] = ClassicSpriteKind.Sprite,
        ["corneredTileSpriteType"] = ClassicSpriteKind.CorneredTile,
        ["frameAnimatedSpriteType"] = ClassicSpriteKind.FrameAnimated,
        ["progressbartype"] = ClassicSpriteKind.ProgressBar,
        ["textSpriteType"] = ClassicSpriteKind.TextSprite,
        ["maskedShieldType"] = ClassicSpriteKind.MaskedShield,
        ["LineChartType"] = ClassicSpriteKind.Chart,
        ["PieChartType"] = ClassicSpriteKind.Chart,
        ["circularProgressBarType"] = ClassicSpriteKind.Chart,
    };

    private static readonly string[] TextureKeys = { "texturefile", "texturefile1", "texturefile2" };

    private readonly IReadOnlyList<FileEntry> _entries;
    private readonly bool _spriteFirstWins;
    private readonly bool _caseSensitive;
    private readonly Dictionary<string, List<ClassicSprite>> _byName;
    private readonly Dictionary<string, List<ClassicSprite>> _byFoldedName;
    private readonly Dictionary<string, ClassicFont> _fonts;

    private ClassicSpriteIndex(IReadOnlyList<FileEntry> entries, bool spriteFirstWins, bool caseSensitive)
    {
        _entries = entries;
        _spriteFirstWins = spriteFirstWins;
        _caseSensitive = caseSensitive;

        // Ladereihenfolge: Pfad, bei gleichem Pfad die höhere Ebene später; innerhalb einer Datei die Textreihenfolge (stabile Sortierung).
        var ordered = entries
            .OrderBy(e => e.File.RelativePath, GameLoadOrder.For(caseSensitive))
            .ThenBy(e => e.File.Layer)
            .ToList();
        _byName = new Dictionary<string, List<ClassicSprite>>(StringComparer.Ordinal);
        _byFoldedName = new Dictionary<string, List<ClassicSprite>>(StringComparer.OrdinalIgnoreCase);
        _fonts = new Dictionary<string, ClassicFont>(StringComparer.Ordinal);
        var sprites = new List<ClassicSprite>();
        foreach (var entry in ordered)
        {
            foreach (var sprite in entry.Sprites)
            {
                sprites.Add(sprite);
                Add(_byName, sprite.Name, sprite);
                Add(_byFoldedName, sprite.Name, sprite);
            }

            // Schriften: der zuletzt geladene Eintrag gilt (nicht gemessen, wie bei Sprites).
            foreach (var font in entry.Fonts)
            {
                _fonts[font.Name] = font;
            }
        }

        Sprites = sprites;
        Fonts = _fonts.Values.ToList();
    }

    /// <summary>Alle Sprites in Ladereihenfolge (auch die überdeckten).</summary>
    public IReadOnlyList<ClassicSprite> Sprites { get; }

    /// <summary>Alle Schriften, je Name die wirksame.</summary>
    public IReadOnlyList<ClassicFont> Fonts { get; }

    /// <summary>
    /// Baut den Index. <paramref name="parse"/> liefert den Syntaxbaum einer Datei und muss parallel aufrufbar sein. Dateien, die <paramref name="previous"/>
    /// schon kennt (gleicher Pfad, gleiche Größe und Zeit), werden nicht neu gelesen.
    /// </summary>
    public static ClassicSpriteIndex Build(
        IReadOnlyList<GuiSourceFile> gfxFiles,
        Func<GuiSourceFile, GuiDocument> parse,
        bool spriteFirstWins,
        bool caseSensitive,
        ClassicSpriteIndex? previous = null)
    {
        var known = new Dictionary<GuiSourceFile, FileEntry>();
        if (previous is not null)
        {
            foreach (var entry in previous._entries)
            {
                known[entry.File] = entry;
            }
        }

        var entries = new FileEntry[gfxFiles.Count];
        Parallel.For(0, gfxFiles.Count, i =>
        {
            entries[i] = known.TryGetValue(gfxFiles[i], out var entry) ? entry : Read(gfxFiles[i], parse(gfxFiles[i]));
        });
        return new ClassicSpriteIndex(entries, spriteFirstWins, caseSensitive);
    }

    /// <summary>Neuer Index, in dem die Datei (gleicher voller Pfad) durch den Syntaxbaum ersetzt bzw. ergänzt ist.</summary>
    public ClassicSpriteIndex WithFile(GuiSourceFile file, GuiDocument document)
    {
        // An der alten Stelle ersetzen: Die Reihenfolge der Einträge entscheidet bei gleichem Pfad und gleicher Ebene (zwei Abhängigkeiten).
        var entries = GuiSourceSet.ReplaceOrInsert(_entries, Read(file, document), e => e.File);
        return new ClassicSpriteIndex(entries, _spriteFirstWins, _caseSensitive);
    }

    /// <summary>
    /// Das wirksame Sprite: erst genau der Name, sonst ohne Rücksicht auf Groß-/Kleinschreibung (dann <c>CaseMismatch</c>; ob das Spiel das ebenso findet,
    /// ist nicht gemessen).
    /// </summary>
    public (ClassicSprite? Sprite, bool CaseMismatch) Find(string name)
    {
        if (_byName.TryGetValue(name, out var exact))
        {
            return (Winner(exact), false);
        }

        return _byFoldedName.TryGetValue(name, out var folded) ? (Winner(folded), true) : (null, false);
    }

    /// <summary>Alle Sprites genau dieses Namens in Ladereihenfolge.</summary>
    public IReadOnlyList<ClassicSprite> FindAll(string name) =>
        _byName.TryGetValue(name, out var list) ? list : Array.Empty<ClassicSprite>();

    public ClassicFont? Font(string name) => _fonts.GetValueOrDefault(name);

    private ClassicSprite Winner(List<ClassicSprite> inLoadOrder) => _spriteFirstWins ? inLoadOrder[0] : inLoadOrder[^1];

    private static void Add(Dictionary<string, List<ClassicSprite>> map, string key, ClassicSprite sprite)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = new List<ClassicSprite>();
        }

        list.Add(sprite);
    }

    private static FileEntry Read(GuiSourceFile file, GuiDocument document)
    {
        var sprites = new List<ClassicSprite>();
        var fonts = new List<ClassicFont>();
        foreach (var node in document.Root.Descendants())
        {
            if (node.Kind != GuiNodeKind.KeyedBlock)
            {
                continue;
            }

            if (Kinds.TryGetValue(node.Name, out var kind))
            {
                if (ReadSprite(node, kind, file) is { } sprite)
                {
                    sprites.Add(sprite);
                }
            }
            else if (string.Equals(node.Name, "bitmapfont", StringComparison.OrdinalIgnoreCase) && ReadFont(node, file) is { } font)
            {
                // bitmapfont_override (Sprachen mit anderen Schriften) hat einen anderen Schlüssel und wird nicht gelesen.
                fonts.Add(font);
            }
        }

        return new FileEntry(file, sprites, fonts);
    }

    private static ClassicSprite? ReadSprite(GuiNode node, ClassicSpriteKind kind, GuiSourceFile file)
    {
        var name = Value(node, "name");
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        string? texture = null;
        foreach (var key in TextureKeys)
        {
            if (Value(node, key) is { Length: > 0 } found)
            {
                texture = found;
                break;
            }
        }

        var frames = int.TryParse(Value(node, "noOfFrames"), out var count) && count > 0 ? count : 1;
        var (borderX, borderY) = (0, 0);
        if (Block(node, "borderSize") is { } border)
        {
            borderX = int.TryParse(Value(border, "x"), out var x) ? x : 0;
            borderY = int.TryParse(Value(border, "y"), out var y) ? y : 0;
        }

        var tiling = string.Equals(Value(node, "tilingCenter"), "yes", StringComparison.OrdinalIgnoreCase);
        return new ClassicSprite(name, kind, texture, frames, borderX, borderY, tiling, file, node);
    }

    private static ClassicFont? ReadFont(GuiNode node, GuiSourceFile file)
    {
        var name = Value(node, "name");
        var path = Value(node, "path");
        if (string.IsNullOrEmpty(path) && Block(node, "fontfiles") is { } files)
        {
            path = files.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.ListItem)?.Value;
        }

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
        {
            return null;
        }

        // Ohne Endung: "gfx/fonts/standard.fnt" wird zu "gfx/fonts/standard".
        var extension = Path.GetExtension(path);
        return new ClassicFont(name, extension.Length > 0 ? path[..^extension.Length] : path, file);
    }

    private static string? Value(GuiNode node, string key) =>
        node.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.Property && string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase))?.Value;

    private static GuiNode? Block(GuiNode node, string key) =>
        node.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.KeyedBlock && string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase));

    private sealed record FileEntry(GuiSourceFile File, IReadOnlyList<ClassicSprite> Sprites, IReadOnlyList<ClassicFont> Fonts);
}
