using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.ScriptCheck;

/// <summary>Art eines fremden Leistungsziels: Vanilla, eine Mod des gewählten Playsets oder eine installierte Mod außerhalb davon.</summary>
public enum PerformanceTargetKind
{
    Vanilla,
    PlaysetMod,
    InstalledMod,
}

/// <summary>
/// Ein fremdes Ziel der Leistungsprüfung (nur Leistungsregeln, es wird nie hineingeschrieben). <paramref name="Directory"/> ist bei Vanilla der
/// Spielinhalt-Ordner, sonst der Mod-Ordner.
/// </summary>
public sealed record PerformanceTarget(PerformanceTargetKind Kind, string Name, string Directory)
{
    /// <summary>
    /// Quellen für das Ziel; es ist immer die aktive Wurzel. Vanilla allein (ohne Mods, die übrigen Vanilla-Ordner bleiben); eine Playset-Mod mit
    /// Playset nur mit den Mods des Playsets (ohne eigene Mods außerhalb); eine installierte Mod oder jede Mod ohne Playset nur über Vanilla. Vergleich der Ordner über <see cref="ErrorLogSources.Key"/>.
    /// </summary>
    public static FlagSourceSet Sources(PerformanceTarget target, GameProfile profile, IReadOnlyList<ModSource> modSources, bool hasPlayset, IReadOnlyList<string> vanillaRoots)
    {
        switch (target.Kind)
        {
            case PerformanceTargetKind.Vanilla:
                var targetKey = ErrorLogSources.Key(target.Directory);
                var rest = vanillaRoots.Where(v => !string.Equals(ErrorLogSources.Key(v), targetKey, StringComparison.OrdinalIgnoreCase)).ToList();
                return FlagSourceSet.ForPlayset(profile, Array.Empty<ModSource>(), target.Directory, rest);
            case PerformanceTargetKind.PlaysetMod when hasPlayset:
                // Nur das Playset: Eigene Mods außerhalb davon wären zusätzliche Aufrufer und änderten die Takte.
                return FlagSourceSet.ForPlayset(profile, modSources.Where(m => m.InPlayset).ToList(), target.Directory, vanillaRoots);
            default:
                return FlagSourceSet.ForPlayset(profile, Array.Empty<ModSource>(), target.Directory, vanillaRoots);
        }
    }

    /// <summary>
    /// Wählbare Ziele: Vanilla (nur mit <paramref name="contentRoot"/>), dann die Playset-Mods in Ladereihenfolge, dann die installierten Mods außerhalb
    /// des Playsets nach Namen. Doppelte Ordner (<see cref="ErrorLogSources.Key"/>) und leere Ordnerangaben fallen weg; der erste gewinnt.
    /// </summary>
    public static IReadOnlyList<PerformanceTarget> Choices(string? contentRoot, IReadOnlyList<(string Name, string Directory)> playsetMods, IReadOnlyList<(string Name, string Directory)> installedMods)
    {
        var result = new List<PerformanceTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(PerformanceTargetKind kind, string name, string directory)
        {
            if (!string.IsNullOrWhiteSpace(directory) && seen.Add(ErrorLogSources.Key(directory)))
            {
                result.Add(new PerformanceTarget(kind, name, directory));
            }
        }

        if (contentRoot is not null)
        {
            Add(PerformanceTargetKind.Vanilla, "Vanilla", contentRoot);
        }

        foreach (var (name, directory) in playsetMods)
        {
            Add(PerformanceTargetKind.PlaysetMod, name, directory);
        }

        foreach (var (name, directory) in installedMods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
        {
            Add(PerformanceTargetKind.InstalledMod, name, directory);
        }

        return result;
    }

    /// <summary>
    /// Anfrage der Leistungsprüfung für das Ziel (macht I/O, im Hintergrund aufrufen): Quellen nach <see cref="Sources"/> (Playset = <paramref name="playsetName"/>
    /// nicht null), alle Namen (<see cref="ScriptCheckNames.All"/>), nur Leistungsregeln, keine eingebauten Befehle. Hinweise: Playset, wenn es genutzt wird;
    /// <see cref="FlagTrackerTexts.NoGameFolder"/> ohne Vanilla-Ordner; <see cref="ScriptCheckTexts.TargetMissing"/>, wenn der Zielordner fehlt oder keine Dateien hat.
    /// </summary>
    public static ScriptCheckRequest Request(
        PerformanceTarget target,
        GameProfile profile,
        IReadOnlyList<ModSource> modSources,
        string? playsetName,
        IReadOnlyList<string> vanillaRoots,
        ScriptCheckRules rules,
        ScriptCheckStore store)
    {
        var hasPlayset = playsetName is not null;
        var notes = new List<string>(3);
        if (hasPlayset && target.Kind == PerformanceTargetKind.PlaysetMod)
        {
            notes.Add(FlagTrackerTexts.PlaysetNote(playsetName!));
        }

        // Vor dem Abzug des Ziels: Ist Vanilla selbst das Ziel, fehlt der Spielordner nicht.
        if (vanillaRoots.Count == 0)
        {
            notes.Add(FlagTrackerTexts.NoGameFolder);
        }

        if (!HasFiles(target.Directory))
        {
            notes.Add(ScriptCheckTexts.TargetMissing(target.Directory));
        }

        var sources = Sources(target, profile, modSources, hasPlayset, vanillaRoots);
        return new ScriptCheckRequest(sources, rules, store, new HashSet<string>(StringComparer.Ordinal), notes, ScriptCheckNames.All, PerformanceOnly: true);
    }

    private static bool HasFiles(string directory)
    {
        try
        {
            return System.IO.Directory.Exists(directory) && System.IO.Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any();
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
