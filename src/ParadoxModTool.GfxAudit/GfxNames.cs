namespace ParadoxModTool.GfxAudit;

/// <summary>Namensvarianten eines Sprite- oder Bildnamens und Normalisierung von Bildpfaden aus Skripttext.</summary>
public static class GfxNames
{
    private const int MinLength = 3;
    private static readonly string[] ImageExtensions = { ".dds", ".png", ".tga" };

    /// <summary>
    /// Der Name selbst und seine Varianten ohne ein passendes Präfix, ohne ein passendes Suffix und ohne beides (Groß-/Kleinschreibung egal bei
    /// der Suche, die Treffer behalten die Schreibung des Namens). Längere Präfixe/Suffixe zuerst. Varianten unter 3 Zeichen entfallen
    /// (der Name selbst bleibt immer), keine Doppelten.
    /// </summary>
    public static IReadOnlyList<string> Candidates(string name, GfxAuditRules rules)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string value)
        {
            if (seen.Add(value))
            {
                result.Add(value);
            }
        }

        void AddShort(string value)
        {
            if (value.Length >= MinLength)
            {
                Add(value);
            }
        }

        Add(name);

        var prefixes = rules.NamePrefixes.Where(p => p.Length > 0 && name.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Length).ToList();
        var suffixes = rules.NameSuffixes.Where(s => s.Length > 0 && name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.Length).ToList();

        foreach (var p in prefixes)
        {
            AddShort(name[p.Length..]);
        }

        foreach (var s in suffixes)
        {
            AddShort(name[..^s.Length]);
        }

        foreach (var p in prefixes)
        {
            foreach (var s in suffixes)
            {
                if (name.Length - p.Length - s.Length >= 0)
                {
                    AddShort(name[p.Length..^s.Length]);
                }
            }
        }

        return result;
    }

    /// <summary>Dateiname ohne Endung (<c>gfx/a/x.dds</c> wird zu <c>x</c>).</summary>
    public static string Stem(string relativePath) => Path.GetFileNameWithoutExtension(relativePath.Replace('\\', '/'));

    /// <summary>
    /// Ein Pfad aus Skripttext: Anführungszeichen entfernt, Schrägstriche (mehrere hintereinander als einer), klein. Null, wenn er nicht mit <c>gfx/</c> oder <c>flags/</c> beginnt
    /// oder Platzhalter enthält (<c>$</c>, <c>[</c>, <c>@</c>).
    /// </summary>
    public static string? NormalizePath(string value)
    {
        var path = CollapseSlashes(value.Trim().Trim('"').Trim().Replace('\\', '/')).ToLowerInvariant();
        if (!path.StartsWith("gfx/", StringComparison.Ordinal) && !path.StartsWith("flags/", StringComparison.Ordinal))
        {
            return null;
        }

        return path.IndexOfAny(new[] { '$', '[', '@' }) >= 0 ? null : path;
    }

    /// <summary>
    /// Eine Textur aus einem Sprite-Block (<see cref="GfxAuditRules.SpriteTextureKeys"/>): Anführungszeichen entfernt, Schrägstriche (mehrere hintereinander
    /// als einer), ohne führenden <c>/</c>, klein. Anders als <see cref="NormalizePath"/> ohne Präfixprüfung: Sprite-Eigenschaften sind eine eindeutige Form,
    /// und die Engine nimmt auch Pfade außerhalb von <c>gfx/</c> (EU IV <c>texturefile = "tutorial\\task_icon.dds"</c>, HOI IV <c>pdx_launcher/…</c>)
    /// und mit führendem <c>/</c> (Stellaris <c>"/gfx/…"</c>). Null, wenn leer oder mit Platzhaltern (<c>$</c>, <c>[</c>, <c>@</c>).
    /// </summary>
    public static string? NormalizeSpriteTexture(string value)
    {
        var path = CollapseSlashes(value.Trim().Trim('"').Trim().Replace('\\', '/')).TrimStart('/').ToLowerInvariant();
        return path.Length == 0 || path.IndexOfAny(new[] { '$', '[', '@' }) >= 0 ? null : path;
    }

    /// <summary>Mehrere <c>/</c> hintereinander werden zu einem (<c>gfx//a.dds</c> wird zu <c>gfx/a.dds</c>).</summary>
    public static string CollapseSlashes(string path)
    {
        if (!path.Contains("//", StringComparison.Ordinal))
        {
            return path;
        }

        var builder = new System.Text.StringBuilder(path.Length);
        foreach (var c in path)
        {
            if (c != '/' || builder.Length == 0 || builder[^1] != '/')
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>Der logische Pfad ohne Bildendung (<c>.dds</c>, <c>.png</c>, <c>.tga</c>), falls vorhanden.</summary>
    public static string WithoutExtension(string logicalPath)
    {
        foreach (var ext in ImageExtensions)
        {
            if (logicalPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                return logicalPath[..^ext.Length];
            }
        }

        return logicalPath;
    }
}
