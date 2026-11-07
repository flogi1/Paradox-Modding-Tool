using ParadoxModTool.Scopes;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.GfxAudit;

/// <summary>
/// Liest eine Textdatei für die GFX-Prüfung: Sprites, Bildverweise, Sprite-Verweise und Erwähnungen von Namen. Welche Blöcke Sprites sind und
/// wie sie referenziert werden, steht in den <see cref="GfxAuditRules"/>. Zeilen 1-basiert; Namen von Blöcken und Schlüsseln werden ohne Rücksicht
/// auf Groß-/Kleinschreibung verglichen. Eine fehlerhafte Datei (unbalancierte Klammern) wirft nicht, es zählt, was der Parser liefert.
/// </summary>
public sealed class GfxFileReader
{
    private const string NameKey = "name";
    private static readonly string[] ImageExtensions = { ".dds", ".png", ".tga" };
    private static readonly char[] LocSeparators = { ' ', '\t', '\r', '"' };

    private readonly GfxAuditRules _rules;
    private readonly HashSet<string> _spriteBlocks;
    private readonly HashSet<string> _textExtensions;

    public GfxFileReader(GfxAuditRules rules)
    {
        _rules = rules;
        _spriteBlocks = new HashSet<string>(rules.SpriteBlocks, StringComparer.OrdinalIgnoreCase);
        _textExtensions = new HashSet<string>(rules.TextExtensions, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Ob die Endung der Datei in <see cref="GfxAuditRules.TextExtensions"/> steht (Groß-/Kleinschreibung egal).</summary>
    public bool IsTextFile(string relativePath) => _textExtensions.Contains(Path.GetExtension(relativePath));

    private static bool IsLocalisation(string relativePath) => ScriptCheckReader.IsLocalisation(relativePath);

    /// <summary>Sprites stehen nur in <c>.gfx</c>-Dateien; anderswo sind gleichnamige Blöcke normaler Text (z. B. ein Fenster-Element in einer <c>.gui</c>).</summary>
    private static bool IsSpriteFile(string relativePath) => string.Equals(Path.GetExtension(relativePath), ".gfx", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sprites (nur in <c>.gfx</c>-Dateien: Blöcke aus <see cref="GfxAuditRules.SpriteBlocks"/> in beliebiger Tiefe mit <c>name</c>; Texturen über
    /// <see cref="GfxNames.NormalizeSpriteTexture"/>, also auch außerhalb von <c>gfx/</c>), Bildpfade (Werte außerhalb von Sprite-Blöcken,
    /// die <see cref="GfxNames.NormalizePath"/> annimmt und die auf ein Bild enden oder keine Endung haben, keine Ordner) und Sprite-Verweise
    /// (Werte mit <see cref="GfxAuditRules.SpriteRefPrefix"/>, nur aus Wortzeichen, nicht auf <c>_</c> endend: <c>unit_sprite_start = "GFX_ap1_"</c> in EU IV ist ein
    /// Namensanfang, an den die Engine den Rest hängt). Lokalisierung hat keine davon.
    /// </summary>
    public GfxScan Read(string relativePath, string text)
    {
        var sprites = new List<GfxSprite>();
        var paths = new List<GfxRef>();
        var refs = new List<GfxRef>();
        if (!IsLocalisation(relativePath))
        {
            var lines = new LineIndex(text);
            var prefix = _rules.SpriteRefPrefix;
            Walk(ScriptTree.Parse(text), IsSpriteFile(relativePath), node =>
            {
                if (node.Scalar is not { } value)
                {
                    return;
                }

                var line = lines.LineOf(node.Start);
                if (NormalizePathValue(value) is { } path)
                {
                    paths.Add(new GfxRef(path, line));
                }

                if (!string.IsNullOrEmpty(prefix) && IsSpriteRef(value.Trim(), prefix))
                {
                    refs.Add(new GfxRef(value.Trim(), line));
                }
            }, (node, name) => sprites.Add(ReadSprite(node, name, lines)));
        }

        return new GfxScan(sprites, paths, refs);
    }

    /// <summary>
    /// Jedes Wort aus Schlüsseln und Werten außerhalb von Sprite-Blöcken (<see cref="MentionWords.Of"/>) sowie der ganze Wert als Pfad und als Pfad
    /// ohne Bildendung, das ein Kandidat in <paramref name="namesByCandidate"/> ist. In <c>.yml</c>-Dateien zählt jedes durch Leerraum oder Anführungszeichen
    /// getrennte Token hinter dem ersten <c>:</c>. Eine Erwähnung je Name und Zeile; Kommentare zählen nie.
    /// </summary>
    /// <param name="namesByCandidate">Kandidat → Mod-Namen; das Wörterbuch muss mit <see cref="StringComparer.OrdinalIgnoreCase"/> angelegt sein.</param>
    public IReadOnlyList<GfxMention> ReadMentions(string relativePath, string text, IReadOnlyDictionary<string, IReadOnlyList<string>> namesByCandidate)
    {
        var result = new List<GfxMention>();
        if (namesByCandidate.Count == 0)
        {
            return result;
        }

        var seen = new HashSet<(string Name, int Line)>();

        void Lookup(string candidate, int line)
        {
            if (!namesByCandidate.TryGetValue(candidate, out var names))
            {
                return;
            }

            foreach (var name in names)
            {
                if (seen.Add((name, line)))
                {
                    result.Add(new GfxMention(name, line));
                }
            }
        }

        void Check(string token, int line)
        {
            foreach (var word in MentionWords.Of(token))
            {
                Lookup(word, line);
            }

            if (GfxNames.NormalizePath(token) is { } path)
            {
                Lookup(path, line);
                Lookup(GfxNames.WithoutExtension(path), line);
            }
        }

        if (IsLocalisation(relativePath))
        {
            var lineNo = 0;
            foreach (var raw in text.Split('\n'))
            {
                lineNo++;
                if (raw.AsSpan().TrimStart().StartsWith("#"))
                {
                    continue;
                }

                var colon = raw.IndexOf(':');
                if (colon < 0)
                {
                    continue;
                }

                foreach (var token in raw[(colon + 1)..].Split(LocSeparators, StringSplitOptions.RemoveEmptyEntries))
                {
                    Check(token, lineNo);
                }
            }

            return result;
        }

        var lines = new LineIndex(text);
        Walk(ScriptTree.Parse(text), IsSpriteFile(relativePath), node =>
        {
            var line = lines.LineOf(node.Start);
            if (node.Key is { } key)
            {
                Check(key, line);
            }

            if (node.Scalar is { } value)
            {
                Check(value, line);
            }
        }, null);
        return result;
    }

    private GfxSprite ReadSprite(ScriptNode node, string name, LineIndex lines)
    {
        // Jeder Texturschlüssel zählt (progressbartype hat textureFile1 und textureFile2, frameAnimatedSpriteType Textur und Maske).
        // Ohne Präfixprüfung (GfxNames.NormalizeSpriteTexture): Auch "tutorial\\x.dds" oder "/gfx/x.dds" nennt ein Bild.
        var textures = new List<GfxRef>();
        foreach (var child in node.Children!)
        {
            if (child.Scalar is { } value && child.Key is { } key && _rules.SpriteTextureKeys.Contains(key, StringComparer.OrdinalIgnoreCase)
                && GfxNames.NormalizeSpriteTexture(value) is { } texture)
            {
                textures.Add(new GfxRef(texture, lines.LineOf(child.Start)));
            }
        }

        return new GfxSprite(name, textures, lines.LineOf(node.Start));
    }

    /// <summary>
    /// Läuft über alle Knoten außerhalb von Sprite-Blöcken. Ein Sprite-Block ist ein Block aus <see cref="GfxAuditRules.SpriteBlocks"/> mit
    /// <c>name</c>-Wert; ohne Namen wäre es kein Sprite und sein Inhalt zählt als normaler Text (großzügig: lieber ein Verweis zu viel).
    /// Ohne <paramref name="spriteFile"/> (keine <c>.gfx</c>-Datei) gibt es keine Sprite-Blöcke.
    /// </summary>
    private void Walk(List<ScriptNode> nodes, bool spriteFile, Action<ScriptNode> visit, Action<ScriptNode, string>? onSprite)
    {
        foreach (var node in nodes)
        {
            if (spriteFile && SpriteName(node) is { } name)
            {
                onSprite?.Invoke(node, name);
                continue;
            }

            visit(node);
            if (node.Children is not null)
            {
                Walk(node.Children, spriteFile, visit, onSprite);
            }
        }
    }

    private string? SpriteName(ScriptNode node)
    {
        if (node.Key is not { } key || node.Children is null || !_spriteBlocks.Contains(key))
        {
            return null;
        }

        var name = node.Children.FirstOrDefault(c => c.Scalar is not null && string.Equals(c.Key, NameKey, StringComparison.OrdinalIgnoreCase))?.Scalar;
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    private static string? NormalizePathValue(string value)
    {
        var path = GfxNames.NormalizePath(value);
        if (path is null || path.EndsWith('/'))
        {
            return null;
        }

        var lastSegment = path[(path.LastIndexOf('/') + 1)..];
        var hasExtension = lastSegment.Contains('.');
        return !hasExtension || ImageExtensions.Any(e => path.EndsWith(e, StringComparison.Ordinal)) ? path : null;
    }

    private static bool IsSpriteRef(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.Ordinal) && !value.EndsWith('_') && value.All(c => char.IsLetterOrDigit(c) || c == '_');

    /// <summary>Zeilenanfänge eines Textes, um Offsets in Zeilen (1-basiert) umzurechnen.</summary>
    private sealed class LineIndex
    {
        private readonly int[] _starts;

        public LineIndex(string text)
        {
            var starts = new List<int> { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            _starts = starts.ToArray();
        }

        public int LineOf(int offset)
        {
            var idx = Array.BinarySearch(_starts, offset);
            return (idx >= 0 ? idx : ~idx - 1) + 1;
        }
    }
}
