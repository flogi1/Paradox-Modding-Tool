namespace ParadoxModTool.GuiDesign.Imaging;

/// <summary>Woher eine Textur kommt: aktuelle Mod, Abhängigkeits-Mod, DLC oder Vanilla (game, jomini, clausewitz).</summary>
public enum GuiTextureOrigin
{
    Mod,
    Dependency,
    Dlc,
    Vanilla,
}

/// <summary>Eine Textur im Katalog: Pfad ab <c>gfx/</c> mit <c>/</c>, volle Datei, Herkunft und deren Name (Ordnername bei Abhängigkeit/DLC, sonst null).</summary>
public sealed record GuiTextureEntry(string RelativePath, string FullPath, GuiTextureOrigin Origin, string? OriginName)
{
    /// <summary>Anzeigetext der Herkunft: <c>Mod</c>, <c>Abhängigkeit: x</c>, <c>DLC: x</c> oder <c>Vanilla</c>.</summary>
    public string OriginLabel => Origin switch
    {
        GuiTextureOrigin.Mod => GuiTexts.TextureOriginMod,
        GuiTextureOrigin.Dependency => GuiTexts.TextureOriginDependency(OriginName ?? string.Empty),
        GuiTextureOrigin.Dlc => GuiTexts.TextureOriginDlc(OriginName ?? string.Empty),
        _ => GuiTexts.TextureOriginVanilla,
    };
}

/// <summary>
/// Der Katalog aller Bilddateien unter <c>&lt;Wurzel&gt;/gfx</c> für die Texturauswahl. Reine Dateisuche ohne Oberfläche; <see cref="Scan"/> läuft
/// auf einem Hintergrund-Thread und ist abbrechbar, <see cref="Filter"/> ist für einige 10 000 Einträge schnell genug für Tippen im Suchfeld.
/// </summary>
public static class GuiTextureCatalog
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".dds", ".png", ".tga", ".jpg", ".jpeg" };

    /// <summary>
    /// Sammelt alle Bilddateien unter <c>gfx</c> jeder Wurzel (in Suchreihenfolge). Je relativem Pfad (ohne Beachtung der Groß-/Kleinschreibung) zählt
    /// nur die erste Wurzel, denn diese Datei lädt das Spiel. Sortiert nach relativem Pfad (ordinal, ohne Groß-/Kleinschreibung).
    /// </summary>
    /// <exception cref="OperationCanceledException">Wenn <paramref name="cancellation"/> abgebrochen wird.</exception>
    public static IReadOnlyList<GuiTextureEntry> Scan(
        IReadOnlyList<string> rootsInSearchOrder,
        string installDirectory,
        string? modRoot,
        IReadOnlyList<string> dependencyRoots,
        CancellationToken cancellation)
    {
        var found = new Dictionary<string, GuiTextureEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in rootsInSearchOrder)
        {
            cancellation.ThrowIfCancellationRequested();
            var gfx = Path.Combine(root, "gfx");
            if (!Directory.Exists(gfx))
            {
                continue;
            }

            var (origin, name) = OriginOf(root, installDirectory, modRoot, dependencyRoots);
            var pending = new Stack<string>();
            pending.Push(gfx);
            while (pending.Count > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                var directory = pending.Pop();
                try
                {
                    foreach (var file in Directory.EnumerateFiles(directory))
                    {
                        if (!Extensions.Contains(Path.GetExtension(file)))
                        {
                            continue;
                        }

                        var relative = "gfx/" + Path.GetRelativePath(gfx, file).Replace('\\', '/');
                        if (!found.ContainsKey(relative))
                        {
                            found[relative] = new GuiTextureEntry(relative, file, origin, name);
                        }
                    }

                    foreach (var sub in Directory.EnumerateDirectories(directory))
                    {
                        pending.Push(sub);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Unlesbarer Ordner: überspringen.
                }
            }
        }

        var result = found.Values.ToList();
        result.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>
    /// Filtert nach Teilstring des relativen Pfads (ohne Groß-/Kleinschreibung). Treffer, bei denen der Text einen Pfadabschnitt beginnt (nach <c>/</c>
    /// oder am Anfang), stehen zuerst; innerhalb jeder Gruppe bleibt die Reihenfolge der Eingabe. Leerer Text liefert alle. Höchstens <paramref name="max"/>.
    /// </summary>
    public static IReadOnlyList<GuiTextureEntry> Filter(IReadOnlyList<GuiTextureEntry> entries, string text, int max = int.MaxValue)
    {
        if (max <= 0)
        {
            return Array.Empty<GuiTextureEntry>();
        }

        if (string.IsNullOrEmpty(text))
        {
            return entries.Count <= max ? entries : entries.Take(max).ToList();
        }

        var segmentStart = new List<GuiTextureEntry>();
        var elsewhere = new List<GuiTextureEntry>();
        foreach (var entry in entries)
        {
            var path = entry.RelativePath;
            var index = path.IndexOf(text, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            if (StartsSegment(path, text, index))
            {
                segmentStart.Add(entry);
            }
            else
            {
                elsewhere.Add(entry);
            }
        }

        return segmentStart.Concat(elsewhere).Take(max).ToList();
    }

    /// <summary>Ob der Text an irgendeiner Fundstelle einen Pfadabschnitt beginnt (die erste Fundstelle <paramref name="first"/> ist schon bekannt).</summary>
    private static bool StartsSegment(string path, string text, int first)
    {
        var index = first;
        while (index >= 0)
        {
            if (index == 0 || path[index - 1] == '/')
            {
                return true;
            }

            index = index + 1 < path.Length ? path.IndexOf(text, index + 1, StringComparison.OrdinalIgnoreCase) : -1;
        }

        return false;
    }

    private static (GuiTextureOrigin Origin, string? Name) OriginOf(string root, string installDirectory, string? modRoot, IReadOnlyList<string> dependencyRoots)
    {
        var normalized = Normalize(root);
        if (!string.IsNullOrEmpty(modRoot) && IsUnder(normalized, Normalize(modRoot)))
        {
            return (GuiTextureOrigin.Mod, null);
        }

        foreach (var dependency in dependencyRoots)
        {
            var dependencyPath = Normalize(dependency);
            if (IsUnder(normalized, dependencyPath))
            {
                return (GuiTextureOrigin.Dependency, Path.GetFileName(dependencyPath));
            }
        }

        var dlc = Normalize(Path.Combine(installDirectory, "game", "dlc"));
        if (normalized.Length > dlc.Length + 1 && IsUnder(normalized, dlc))
        {
            var rest = normalized[(dlc.Length + 1)..];
            var slash = rest.IndexOf('/');
            return (GuiTextureOrigin.Dlc, slash < 0 ? rest : rest[..slash]);
        }

        return (GuiTextureOrigin.Vanilla, null);
    }

    /// <summary>Pfad mit <c>/</c>, ohne abschließenden Trenner.</summary>
    private static string Normalize(string path) => path.Replace('\\', '/').TrimEnd('/');

    /// <summary>Ob <paramref name="path"/> gleich <paramref name="prefix"/> ist oder darunter liegt (an Ordnergrenzen, ohne Groß-/Kleinschreibung).</summary>
    private static bool IsUnder(string path, string prefix) =>
        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (path.Length == prefix.Length || path[prefix.Length] == '/');
}
