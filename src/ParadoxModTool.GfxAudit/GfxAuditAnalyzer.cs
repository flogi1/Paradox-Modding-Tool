using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.GfxAudit;

/// <summary>
/// Wertet einen <see cref="GfxAuditIndex"/> aus: unbenutzte Bilder und Sprites, fehlende Texturen und Sprites, doppelte Sprite-Namen.
/// Funde gibt es nur für Bilder, Definitionen und Verweise der gewählten Mod; Vanilla und alle Playset-Mods zählen als Nutzer und Definitionen.
/// „Benutzt“ ist großzügig: Lieber ein altes Bild übersehen als ein benutztes melden.
/// </summary>
public static class GfxAuditAnalyzer
{
    private static readonly char[] Placeholders = { '$', '[', '@' };

    /// <summary>Ein Sprite mit seiner Datei und dem Ort seiner Definition.</summary>
    private sealed record Located(GfxFile File, GfxSprite Sprite, SiteLocation Location)
    {
        public bool IsMod => File.Origin == FlagOrigin.ActiveMod;
    }

    public static GfxAuditResult Analyze(GfxAuditIndex index, GfxAuditRules rules)
    {
        if (index.ModRoot is null)
        {
            return new GfxAuditResult(Array.Empty<GfxFinding>());
        }

        var sprites = index.Files.SelectMany(f => f.Scan.Sprites.Select(s => new Located(f, s, f.File with { Line = s.Line }))).ToList();
        var mentioned = index.Mentions.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Ein Sprite der Mod, dessen Name es auch außerhalb der Mod gibt, ersetzt einen vorhandenen Sprite (wie ein ersetztes Bild): Die Engine
        // oder Dritte können ihn ohne Erwähnung im Text nutzen.
        var foreignSprites = sprites.Where(s => !s.IsMod).Select(s => s.Sprite.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool IsUsed(Located s) => mentioned.Contains(s.Sprite.Name) || foreignSprites.Contains(s.Sprite.Name);

        var findings = new List<GfxFinding>();
        findings.AddRange(UnusedSprites(sprites, IsUsed));
        findings.AddRange(UnusedImages(index, rules, sprites, mentioned, IsUsed));
        findings.AddRange(MissingTextures(index, rules));
        if (rules.SpriteRefPrefix is not null)
        {
            findings.AddRange(MissingSprites(index, sprites));
        }

        findings.AddRange(DuplicateSprites(index.Sources, sprites));

        var sorted = findings
            .OrderBy(f => f.Severity)
            .ThenBy(f => f.Location.RelativePath, StringComparer.Ordinal)
            .ThenBy(f => f.Location.Line)
            .ThenBy(f => f.Kind)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
        return new GfxAuditResult(sorted);
    }

    /// <summary>
    /// Schlüssel für den Vergleich von Bildverweisen mit Bildern: logischer Pfad ohne Bildendung. So lösen sich getauschte Endungen
    /// (<c>.tga</c> statt <c>.dds</c>), fehlende Endungen und die EU-V-Phasenordner auf.
    /// </summary>
    private static string TextureKey(string path, GfxAuditRules rules) => GfxNames.WithoutExtension(rules.LogicalPath(path));

    /// <summary>Sprites der Mod, deren Name nirgends erwähnt wird; ein Fund je Name an der ersten Definition.</summary>
    private static IEnumerable<GfxFinding> UnusedSprites(List<Located> sprites, Func<Located, bool> isUsed) =>
        sprites
            .Where(s => s.IsMod && !isUsed(s))
            .GroupBy(s => s.Sprite.Name, StringComparer.Ordinal)
            .Select(g => GfxFinding.Create(GfxFindingKind.UnusedSprite, FindingSeverity.Warning, g.Key, g.First().Location, Array.Empty<string>()));

    /// <summary>
    /// Bilder der Mod, die nichts nutzt: kein Kandidat ihres Namens erwähnt, kein benutzter Sprite der Mod und kein Sprite außerhalb der Mod
    /// zeigt darauf, nicht in einem immer geladenen oder von einem Bildpfad ohne Endung genannten Ordner und kein Ersatz eines vorhandenen Bildes.
    /// Benutzt ist ein Sprite der Mod, wenn sein Name erwähnt oder auch außerhalb der Mod definiert ist. Related = die unbenutzten Sprites der Mod,
    /// die darauf zeigen (sie verschwinden mit dem Bild).
    /// </summary>
    private static IEnumerable<GfxFinding> UnusedImages(
        GfxAuditIndex index, GfxAuditRules rules, List<Located> sprites, HashSet<string> mentioned, Func<Located, bool> isUsed)
    {
        var usedTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unusedSpritesByTexture = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (s, key) in sprites.SelectMany(s => s.Sprite.Textures.Select(t => (s, TextureKey(t.Value, rules)))))
        {
            if (!s.IsMod || isUsed(s))
            {
                usedTextures.Add(key);
            }
            else
            {
                if (!unusedSpritesByTexture.TryGetValue(key, out var list))
                {
                    unusedSpritesByTexture[key] = list = new List<string>();
                }

                if (!list.Contains(s.Sprite.Name, StringComparer.Ordinal))
                {
                    list.Add(s.Sprite.Name);
                }
            }
        }

        // Bildpfade ohne Endung aus allen Wurzeln können einen Ordner nennen, aus dem die Engine lädt (CK III paths.settings): Alles darunter gilt als benutzt.
        var referencedFolders = index.Files
            .SelectMany(f => f.Scan.TexturePaths)
            .Select(r => rules.LogicalPath(r.Value))
            .Where(p => GfxNames.WithoutExtension(p) == p && !rules.IsIgnoredFolderReference(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var image in index.Images.Where(i => i.Origin == FlagOrigin.ActiveMod))
        {
            var key = GfxNames.WithoutExtension(image.LogicalPath);
            if (mentioned.Contains(image.LogicalPath) || image.ReplacesOriginal || rules.IsAlwaysUsed(image.File.RelativePath) || usedTextures.Contains(key)
                || Ancestors(image.LogicalPath).Any(referencedFolders.Contains))
            {
                continue;
            }

            IReadOnlyList<string> related = unusedSpritesByTexture.TryGetValue(key, out var names) ? names : Array.Empty<string>();
            yield return GfxFinding.Create(GfxFindingKind.UnusedImage, FindingSeverity.Warning, image.LogicalPath, image.File, related);
        }
    }

    /// <summary>
    /// Bildverweise der Mod (Texturen ihrer Sprites und Bildpfade außerhalb von Sprites, die auf eine Bildendung enden), zu denen es in keiner Wurzel
    /// ein Bild gibt; ein Fund je Stelle. Eine Sprite-Textur ohne Endung gilt auch als vorhanden, wenn sie einen Ordner mit Bildern nennt. Bildpfade
    /// außerhalb von Sprites ohne Endung werden nie gemeldet (sie zählen nur für die Ordnerregel in <see cref="UnusedImages"/>).
    /// </summary>
    private static IEnumerable<GfxFinding> MissingTextures(GfxAuditIndex index, GfxAuditRules rules)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in index.Images)
        {
            existing.Add(GfxNames.WithoutExtension(image.LogicalPath));
            folders.UnionWith(Ancestors(image.LogicalPath));
        }

        bool Exists(string value)
        {
            var key = TextureKey(value, rules);
            return existing.Contains(key) || (key == rules.LogicalPath(value) && folders.Contains(key));
        }

        foreach (var file in index.Files.Where(f => f.Origin == FlagOrigin.ActiveMod))
        {
            foreach (var sprite in file.Scan.Sprites)
            {
                foreach (var texture in sprite.Textures.Where(t => !IsPlaceholder(t.Value) && !Exists(t.Value)))
                {
                    yield return GfxFinding.Create(
                        GfxFindingKind.MissingTexture, FindingSeverity.Error, texture.Value, file.File with { Line = texture.Line }, new[] { sprite.Name });
                }
            }

            // Außerhalb von Sprites nur Werte mit Bildendung: Ohne Endung kann es ein Ordner, ein Namensteil oder eine .fnt sein (fontname = "gfx/fonts/x").
            foreach (var reference in file.Scan.TexturePaths.Where(r => HasImageExtension(r.Value)))
            {
                if (!IsPlaceholder(reference.Value) && !Exists(reference.Value))
                {
                    yield return GfxFinding.Create(
                        GfxFindingKind.MissingTexture, FindingSeverity.Error, reference.Value, file.File with { Line = reference.Line }, Array.Empty<string>());
                }
            }
        }
    }

    /// <summary>Sprite-Verweise der Mod ohne Sprite dieses Namens in irgendeiner Datei (Groß-/Kleinschreibung egal); ein Fund je Stelle.</summary>
    private static IEnumerable<GfxFinding> MissingSprites(GfxAuditIndex index, List<Located> sprites)
    {
        var known = sprites.Select(s => s.Sprite.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return index.Files
            .Where(f => f.Origin == FlagOrigin.ActiveMod)
            .SelectMany(f => f.Scan.SpriteRefs.Select(r => (File: f, Ref: r)))
            .Where(x => !IsPlaceholder(x.Ref.Value) && !known.Contains(x.Ref.Value))
            .Select(x => GfxFinding.Create(
                GfxFindingKind.MissingSprite, FindingSeverity.Error, x.Ref.Value, x.File.File with { Line = x.Ref.Line }, Array.Empty<string>()));
    }

    /// <summary>
    /// Sprite-Namen (ordinal) mit mehreren Definitionen, von denen eine aus der Mod stammt: ein Fund je Name, wenn die Mod den Namen mehrmals
    /// definiert oder ihre Definition gegen eine andere verliert; nie, wenn die einzige Mod-Definition gewinnt. Es gewinnt die zuletzt geladene
    /// Definition (gemessen für EU IV, HOI IV und Stellaris): zuerst die Ladereihenfolge des relativen Pfads über Vanilla und Mods
    /// (<see cref="FlagSourceSet.LoadOrder"/>, die Dateien eines Ordners vor seinen Unterordnern), dann der Rank der Wurzel; bei Gleichstand
    /// (dieselbe Datei) bleibt die frühere Zeile. Unabhängig von den Überschreibungsregeln des Profils, die für <c>interface</c> nicht in jedem Spiel
    /// nach Ladereihenfolge vergleichen. Ort = die erste Mod-Definition, die nicht gewinnt; Related = alle Definitionen als <c>pfad:zeile (Herkunft)</c>,
    /// die gewinnende zuerst.
    /// </summary>
    private static IEnumerable<GfxFinding> DuplicateSprites(FlagSourceSet sources, List<Located> sprites)
    {
        var groups = sprites
            .GroupBy(s => s.Sprite.Name, StringComparer.Ordinal)
            .Where(g => g.Count() >= 2 && g.Any(s => s.IsMod));
        foreach (var group in groups)
        {
            var members = group.ToList();
            var winner = members[0];
            foreach (var candidate in members.Skip(1))
            {
                var cmp = sources.LoadOrder.Compare(candidate.Location.RelativePath, winner.Location.RelativePath);
                if (cmp == 0)
                {
                    cmp = sources.RankOf(candidate.Location.RootDirectory).CompareTo(sources.RankOf(winner.Location.RootDirectory));
                }

                if (cmp > 0)
                {
                    winner = candidate;
                }
            }

            if (members.Count(s => s.IsMod) < 2 && winner.IsMod)
            {
                continue;
            }

            var location = members.First(s => s.IsMod && !ReferenceEquals(s, winner)).Location;
            var related = members.Where(s => !ReferenceEquals(s, winner)).Prepend(winner)
                .Select(s => $"{s.Location.RelativePath}:{s.Location.Line} ({s.File.RootName})")
                .ToList();
            yield return GfxFinding.Create(GfxFindingKind.DuplicateSprite, FindingSeverity.Warning, group.Key, location, related);
        }
    }

    /// <summary>Die Ordner über einem logischen Pfad, der nächste zuerst (<c>gfx/a/b.dds</c>: <c>gfx/a</c>, <c>gfx</c>).</summary>
    private static IEnumerable<string> Ancestors(string logicalPath)
    {
        for (var slash = logicalPath.LastIndexOf('/'); slash > 0; slash = logicalPath.LastIndexOf('/', slash - 1))
        {
            yield return logicalPath[..slash];
        }
    }

    private static bool IsPlaceholder(string value) => value.IndexOfAny(Placeholders) >= 0;

    /// <summary>Ob der Pfad auf <c>.dds</c>, <c>.png</c> oder <c>.tga</c> endet (Groß-/Kleinschreibung egal).</summary>
    private static bool HasImageExtension(string path) => GfxNames.WithoutExtension(path).Length != path.Length;
}
