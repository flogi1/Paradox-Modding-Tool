using ParadoxModTool.Mods;

namespace ParadoxModTool.ModOverview;

public enum OverviewSortKey
{
    Name,
    Subscribers,
    OpenBugs,
    LastRelease,
}

/// <summary>Was das Fenster gerade zeigen soll (Werkzeugleiste und Spielliste).</summary>
public sealed record OverviewViewOptions(
    bool GroupByGame,
    OverviewSortKey Sort,
    bool Descending,
    bool OnlyPublished,
    bool OnlyOpenBugs,
    bool OnlyWarnings,
    string Search,
    IReadOnlyList<string> GameOrder,
    IReadOnlyCollection<string> HiddenGames);

/// <summary>Eine Kachel bzw. Listenzeile. <see cref="Bugs"/> steht nur beim Eintrag der Projekt-Hauptmod, bei Mitgliedern ist es leer.</summary>
public sealed record OverviewEntry(OverviewMod Mod, IReadOnlyList<OverviewMod> DevBranches, BugCount Bugs, IReadOnlyList<OverviewAlert> Alerts);

/// <summary>Ein Projektrahmen (<see cref="ProjectRoot"/> gesetzt) oder eine einzelne Mod ohne Projekt (<see cref="ProjectRoot"/> leer, ein Eintrag).</summary>
public sealed record OverviewGroup(OverviewMod? ProjectRoot, IReadOnlyList<OverviewEntry> Entries);

/// <summary>Ein Abschnitt je Spiel; bei "Alle Spiele" genau einer ohne Spiel und ohne Titel.</summary>
public sealed record OverviewSection(string? GameId, string Title, IReadOnlyList<OverviewGroup> Groups);

/// <summary>
/// Baut aus einem Stand der Übersicht die Abschnitte, Projektrahmen und Einträge (Spec 2.2-2.4): filtert je Mod, sortiert Rahmen nach ihrer
/// Hauptmod und Mitglieder nach demselben Schlüssel (Hauptmod zuerst), Gleichstand nach Name. Reine Funktion.
/// </summary>
public static class OverviewView
{
    public static IReadOnlyList<OverviewSection> Build(
        OverviewSnapshot snapshot,
        IReadOnlyList<OverviewAlert> alerts,
        IReadOnlyDictionary<OverviewMod, BugCount> bugs,
        OverviewViewOptions options)
    {
        var hidden = new HashSet<string>(options.HiddenGames, StringComparer.Ordinal);
        var mods = snapshot.Mods.Where(m => !hidden.Contains(m.GameId)).ToList();
        var alertsByMod = alerts.ToLookup(a => (a.GameId, a.ModId));
        var groups = Groups(mods, alertsByMod, bugs, options);
        var comparer = new ModComparer(options, bugs);

        if (!options.GroupByGame)
        {
            var all = groups.OrderBy(g => g.Key, comparer).Select(g => g.Group).ToList();
            return all.Count == 0 ? Array.Empty<OverviewSection>() : new[] { new OverviewSection(null, string.Empty, all) };
        }

        var order = options.GameOrder
            .Concat(snapshot.Games.Select(g => g.Id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var sections = new List<OverviewSection>();
        foreach (var gameId in order)
        {
            if (hidden.Contains(gameId) || snapshot.Games.FirstOrDefault(g => g.Id == gameId) is not { } game)
            {
                continue;
            }

            var inGame = groups.Where(g => g.Key.GameId == gameId).OrderBy(g => g.Key, comparer).Select(g => g.Group).ToList();
            if (inGame.Count > 0)
            {
                sections.Add(new OverviewSection(gameId, game.DisplayName, inGame));
            }
        }

        return sections;
    }

    /// <summary>Die Rahmen samt ihrer Hauptmod als Sortierschlüssel; nur Rahmen mit mindestens einem Eintrag, der die Filter besteht.</summary>
    private static List<(OverviewMod Key, OverviewGroup Group)> Groups(
        IReadOnlyList<OverviewMod> mods,
        ILookup<(string, string), OverviewAlert> alertsByMod,
        IReadOnlyDictionary<OverviewMod, BugCount> bugs,
        OverviewViewOptions options)
    {
        var comparer = new ModComparer(options, bugs);
        var attached = new Dictionary<OverviewMod, OverviewMod>();
        foreach (var mod in mods)
        {
            if (AttachedTo(mod, mods) is { } target)
            {
                attached[mod] = target;
            }
        }

        var result = new List<(OverviewMod, OverviewGroup)>();

        foreach (var root in mods.Where(m => ReferenceEquals(m.ProjectRoot, m) && !attached.ContainsKey(m)))
        {
            var members = mods
                .Where(m => !ReferenceEquals(m, root) && ReferenceEquals(m.ProjectRoot, root) && m.Mod.Role != ModRole.DevBranch)
                .Concat(attached.Where(a => ReferenceEquals(a.Value, root)).Select(a => a.Key))
                .ToList();
            var devBranches = mods
                .Where(m => !ReferenceEquals(m, root) && ReferenceEquals(m.ProjectRoot, root) && m.Mod.Role == ModRole.DevBranch)
                .ToList();
            var rootBugs = bugs.TryGetValue(root, out var count) ? count : default;

            OverviewEntry Entry(OverviewMod mod, bool isRoot) => new(
                mod,
                isRoot ? devBranches : Array.Empty<OverviewMod>(),
                isRoot ? rootBugs : default,
                alertsByMod[(mod.GameId, mod.Mod.Id)].ToList());

            bool Passes(OverviewEntry entry) =>
                (!options.OnlyPublished || entry.Mod.IsPublished)
                && (!options.OnlyOpenBugs || rootBugs.Open > 0)
                && (!options.OnlyWarnings || entry.Alerts.Count > 0)
                && Matches(entry.Mod, options.Search);

            var entries = new[] { Entry(root, true) }
                .Concat(members.OrderBy(m => m, comparer).Select(m => Entry(m, false)))
                .Where(Passes)
                .ToList();
            if (entries.Count > 0)
            {
                var isProject = members.Count > 0 || devBranches.Count > 0;
                result.Add((root, new OverviewGroup(isProject ? root : null, entries)));
            }
        }

        return result;
    }

    /// <summary>Ein Kompatibilitätspatch ohne eigenes Projekt gehört in den Rahmen der Hauptmod, auf die er zielt (erste passende im selben Spiel).</summary>
    private static OverviewMod? AttachedTo(OverviewMod mod, IReadOnlyList<OverviewMod> mods)
    {
        if (!mod.Mod.IsCompatch || mod.Mod.ParentModId is not null || !ReferenceEquals(mod.ProjectRoot, mod))
        {
            return null;
        }

        return mod.Mod.CompatchTargets
            .Select(t => t.ModId)
            .Where(id => id is { Length: > 0 })
            .Select(id => mods.FirstOrDefault(m => m.GameId == mod.GameId && m.Mod.Id == id && ReferenceEquals(m.ProjectRoot, m) && !ReferenceEquals(m, mod)))
            .FirstOrDefault(m => m is not null && !m.Mod.IsCompatch);
    }

    private static bool Matches(OverviewMod mod, string search) =>
        string.IsNullOrWhiteSpace(search)
        || mod.Mod.Name.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase)
        || (mod.Metadata?.Name.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase) ?? false);

    /// <summary>Hauptschlüssel laut Optionen (absteigend dreht nur ihn um), bei Gleichstand der Name aufsteigend.</summary>
    private sealed class ModComparer(OverviewViewOptions options, IReadOnlyDictionary<OverviewMod, BugCount> bugs) : IComparer<OverviewMod>
    {
        public int Compare(OverviewMod? x, OverviewMod? y)
        {
            if (x is null || y is null)
            {
                return x is null ? (y is null ? 0 : -1) : 1;
            }

            var primary = options.Sort switch
            {
                OverviewSortKey.Subscribers => (x.Steam?.Subscriptions ?? -1).CompareTo(y.Steam?.Subscriptions ?? -1),
                OverviewSortKey.OpenBugs => Open(x).CompareTo(Open(y)),
                OverviewSortKey.LastRelease => string.CompareOrdinal(x.LastRelease?.Date ?? string.Empty, y.LastRelease?.Date ?? string.Empty),
                _ => Name(x, y),
            };
            if (options.Descending)
            {
                primary = -primary;
            }

            return primary != 0 ? primary : Name(x, y);
        }

        private int Open(OverviewMod mod) => bugs.TryGetValue(mod.ProjectRoot, out var count) ? count.Open : 0;

        private static int Name(OverviewMod x, OverviewMod y) => StringComparer.CurrentCultureIgnoreCase.Compare(x.Mod.Name, y.Mod.Name);
    }
}
