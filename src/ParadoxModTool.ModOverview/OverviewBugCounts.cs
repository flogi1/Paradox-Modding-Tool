using ParadoxModTool.Notes;

namespace ParadoxModTool.ModOverview;

/// <summary>Offene und neue Bugreports eines Projekts.</summary>
public readonly record struct BugCount(int Open, int New);

/// <summary>
/// Zählt die Bugreports je Projekt-Hauptmod (Spec 3.2). Ein Report gehört über seine <c>ModId</c> (Register-GUID irgendeiner Mod des Projekts) zur
/// Hauptmod; Reports ohne oder mit unbekannter <c>ModId</c> zählen nicht. Offen = Status Open, neu = nicht gesehen; entfernte zählen nie.
/// </summary>
public static class OverviewBugCounts
{
    public static IReadOnlyDictionary<OverviewMod, BugCount> Compute(OverviewSnapshot snapshot, BugData bugs)
    {
        var byId = new Dictionary<string, OverviewMod>(StringComparer.Ordinal);
        foreach (var mod in snapshot.Mods)
        {
            byId.TryAdd(mod.Mod.Id, mod);
        }

        var counts = new Dictionary<OverviewMod, BugCount>(ReferenceEqualityComparer.Instance);
        foreach (var report in bugs.Reports)
        {
            if (report.Removed || report.ModId is not { Length: > 0 } modId || !byId.TryGetValue(modId, out var mod))
            {
                continue;
            }

            var root = mod.ProjectRoot;
            counts.TryGetValue(root, out var count);
            counts[root] = new BugCount(count.Open + (report.IsOpen ? 1 : 0), count.New + (report.Seen ? 0 : 1));
        }

        return counts;
    }
}
