namespace ParadoxModTool.ErrorLog;

/// <summary>Ein Ordner einer eigenen Mod aus dem PMT-Register (auch Dev-Branches und Submods).</summary>
public sealed record OwnModFolder(string Name, string Directory);

/// <summary>Eine installierte Mod (lokaler Mod-Ordner oder Workshop) mit ihrem Anzeigenamen.</summary>
public sealed record InstalledModFolder(string Name, string Directory);

/// <summary>Ein Eintrag des gewählten Playsets in Ladereihenfolge.</summary>
public sealed record PlaysetEntry(string Directory, bool Enabled);

/// <summary>
/// Baut aus Register, Playset und installierten Mods die Mod-Liste für Zuordnung und Symbolindex. Derselbe Ordner kommt oft aus mehreren
/// Quellen mit anderer Schreibweise (Schrägstriche, Groß-/Kleinschreibung, Trenner am Ende); <see cref="Key"/> macht ihn vergleichbar.
/// </summary>
public static class ErrorLogSources
{
    public static string Key(string directory) => Path.GetFullPath(directory.Trim()).TrimEnd('\\', '/');

    public static IReadOnlyList<ModSource> BuildMods(IEnumerable<OwnModFolder> own, IReadOnlyList<PlaysetEntry>? playset, IEnumerable<InstalledModFolder> installed)
    {
        var ownList = own.Where(o => !string.IsNullOrWhiteSpace(o.Directory)).ToList();
        var ownKeys = new HashSet<string>(ownList.Select(o => Key(o.Directory)), StringComparer.OrdinalIgnoreCase);
        var installedList = installed.Where(i => !string.IsNullOrWhiteSpace(i.Directory)).ToList();

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in installedList)
        {
            names.TryAdd(Key(mod.Directory), mod.Name);
        }

        foreach (var mod in ownList)
        {
            names[Key(mod.Directory)] = mod.Name;
        }

        // Nur aktivierte Einträge laden im Spiel; deaktivierte verschieben die Reihenfolge der übrigen nicht.
        var loadOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var loaded = playset is null ? ownList.Select(o => o.Directory) : playset.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Directory)).Select(p => p.Directory);
        foreach (var directory in loaded)
        {
            loadOrder.TryAdd(Key(directory), loadOrder.Count);
        }

        // Eigene Mods zuerst: Ihre Schreibweise des Ordners gilt (sie ist die, unter der PMT die Dateien öffnet).
        var result = new Dictionary<string, ModSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in ownList.Select(o => o.Directory).Concat(loaded).Concat(installedList.Select(i => i.Directory)))
        {
            var key = Key(directory);
            if (result.ContainsKey(key))
            {
                continue;
            }

            result[key] = new ModSource(
                names.TryGetValue(key, out var name) ? name : Path.GetFileName(key),
                key,
                ownKeys.Contains(key),
                loadOrder.TryGetValue(key, out var order) ? order : -1);
        }

        return result.Values
            .OrderBy(m => m.InPlayset ? 0 : 1)
            .ThenBy(m => m.LoadOrder)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<SymbolRoot> SymbolRoots(IReadOnlyList<ModSource> mods, IReadOnlyList<string> vanillaRoots) =>
        vanillaRoots.Select(root => new SymbolRoot(root, null, false))
            .Concat(mods.Select(mod => new SymbolRoot(mod.Directory, mod, mod.InPlayset || mod.IsOwn)))
            .ToList();

    /// <summary>Vanilla-Ordner (Plan-Ruling 11): vorhandene <c>game</c>, <c>jomini</c>, <c>clausewitz</c>; sonst der Spielinhalt-Ordner.</summary>
    public static IReadOnlyList<string> VanillaRoots(string? installDirectory, string? contentRoot)
    {
        var roots = installDirectory is null
            ? new List<string>()
            : SourceResolver.VanillaRootsFor(installDirectory).Where(Directory.Exists).ToList();
        if (roots.Count == 0 && contentRoot is not null && Directory.Exists(contentRoot))
        {
            roots.Add(contentRoot);
        }

        return roots;
    }
}
