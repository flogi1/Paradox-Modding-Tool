using ParadoxModTool.Mods;

namespace ParadoxModTool.ModCompat;

/// <summary>
/// Prüft die <c>replace_path</c>-Einträge der Mods eines Laufs: Einträge, die mit einer Wurzel beginnen (Europa Universalis V: <c>in_game/</c> usw.),
/// wirken nicht; ein Unterordner einer Absturz-Wurzel (Europa Universalis IV: <c>interface/</c>) mit .gui-Dateien lässt das Spiel beim Start
/// abstürzen (beides gemessen 2026-09-30, siehe docs/pmt-adding-a-game.md).
/// </summary>
internal static class ReplacePathChecker
{
    public static IEnumerable<CompatFinding> Check(CompatRequest request, IReadOnlyList<CompatParticipant> mods)
    {
        foreach (var mod in mods)
        {
            if (!request.ReplacePathEntries.TryGetValue(mod.Id, out var entries))
            {
                continue;
            }

            foreach (var entry in entries.Select(ReplacePaths.Normalize).Where(e => e.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var slash = entry.IndexOf('/');
                var first = slash < 0 ? entry : entry[..slash];
                if (slash > 0 && Contains(request.ReplacePathRoots, first))
                {
                    yield return new CompatFinding
                    {
                        Severity = CompatSeverity.Hint,
                        Kind = CompatKind.ReplacePathNoEffect,
                        RelativePath = entry,
                        Winner = mod,
                        Message = ModCompatTexts.FindingReplacePathNoEffect(mod.Name, entry, first, entry[(slash + 1)..]),
                    };
                }
                else if (slash > 0 && Contains(request.ReplacePathGuiCrashRoots, first))
                {
                    var withGui = mods.Where(m => HasGuiFiles(m, entry)).Select(m => "\"" + m.Name + "\"").ToList();
                    if (withGui.Count > 0)
                    {
                        yield return new CompatFinding
                        {
                            Severity = CompatSeverity.Conflict,
                            Kind = CompatKind.ReplacePathGuiCrash,
                            RelativePath = entry,
                            Winner = mod,
                            Message = ModCompatTexts.FindingReplacePathGuiCrash(mod.Name, entry, string.Join(", ", withGui)),
                        };
                    }
                }
            }
        }
    }

    private static bool Contains(IReadOnlyCollection<string> folders, string name) =>
        folders.Any(folder => string.Equals(folder, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Liegen .gui-Dateien direkt im Ordner (nicht in Unterordnern, die replace_path nicht betrifft)?</summary>
    private static bool HasGuiFiles(CompatParticipant mod, string folder)
    {
        var path = Path.Combine(mod.Folder, folder.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            return Directory.Exists(path) && Directory.EnumerateFiles(path, "*.gui", SearchOption.TopDirectoryOnly).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
