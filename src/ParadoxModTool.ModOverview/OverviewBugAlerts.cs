using ParadoxModTool.Notes;

namespace ParadoxModTool.ModOverview;

/// <summary>Neue Bugreports eines Projekts (Hauptmod), neueste zuerst; ab mehr als drei in einer zusammengeklappten Zeile.</summary>
public sealed record OverviewBugGroup(OverviewMod Root, IReadOnlyList<BugReport> Reports)
{
    public bool Collapsed => Reports.Count > 3;
}

/// <summary>
/// Neue Bugreports (nicht gesehen, nicht entfernt) je Projekt-Hauptmod, für den Hinweisbereich. Zuordnung wie <see cref="OverviewBugCounts"/>;
/// Reports ohne oder mit unbekannter Mod fallen weg. Reihenfolge: Spiel wie im Stand, dann Name der Hauptmod.
/// </summary>
public static class OverviewBugAlerts
{
    public static IReadOnlyList<OverviewBugGroup> New(OverviewSnapshot snapshot, BugData bugs)
    {
        var byId = new Dictionary<string, OverviewMod>(StringComparer.Ordinal);
        foreach (var mod in snapshot.Mods)
        {
            byId.TryAdd(mod.Mod.Id, mod);
        }

        var gameOrder = snapshot.Games.Select((g, i) => (g.Id, i)).ToDictionary(x => x.Id, x => x.i);
        return bugs.Reports
            .Where(r => !r.Seen && !r.Removed && r.ModId is { Length: > 0 } && byId.ContainsKey(r.ModId))
            .GroupBy(r => byId[r.ModId!].ProjectRoot)
            .OrderBy(g => gameOrder.TryGetValue(g.Key.GameId, out var index) ? index : int.MaxValue)
            .ThenBy(g => g.Key.Mod.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new OverviewBugGroup(g.Key, g.OrderByDescending(r => r.CreatedUtc).ToList()))
            .ToList();
    }
}
