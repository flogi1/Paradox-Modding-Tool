namespace ParadoxModTool.FlagTracker;

/// <summary>
/// Zeile der Übersicht. Hint ist null ohne Hinweis; HintIsWarning ist false bei „evtl. über Muster“ und bei ausgeblendeten Namen.
/// Hidden: Eine Ausblendregel der aktiven Mod passt auf den Namen (Hint trägt dann den Vermerk „ausgeblendet“).
/// </summary>
public sealed record OverviewRow(string Name, string KindText, int SetCount, int ReadCount, int RemoveCount, string? Hint, bool HintIsWarning, bool Hidden = false);

/// <summary>Art einer Zeile in „Alle Stellen“.</summary>
public enum SiteRowKind
{
    NameHeader,
    PatternHeader,
    Site,
}

/// <summary>
/// Zeile in „Alle Stellen“. Bei Site ist <paramref name="Site"/> gesetzt; FileText = "relativer/pfad.txt:12", bei anderen Mods mit
/// "&lt;Modname&gt; / " davor. ViaText ist null ohne Via.
/// </summary>
public sealed record SiteRow(SiteRowKind Kind, string Text, FlagSite? Site, string OriginText, string RoleText, string KindText, string FileText, string? ViaText)
{
    /// <summary>Gleichheit nur über die Referenz: Zwei Zeilen mit gleichem Inhalt (z. B. dieselbe Stelle über zwei Wege) bleiben in der ListView getrennt wählbar.</summary>
    public bool Equals(SiteRow? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
}

/// <summary>Anzeigemodell eines Suchergebnisses: Übersichtszeilen und die flache Liste aller Stellen.</summary>
public sealed class FlagTrackerView
{
    private readonly Dictionary<string, int> _nameIndex;

    private FlagTrackerView(IReadOnlyList<OverviewRow> overview, IReadOnlyList<SiteRow> sites, bool truncated, Dictionary<string, int> nameIndex)
    {
        Overview = overview;
        Sites = sites;
        Truncated = truncated;
        _nameIndex = nameIndex;
    }

    public static FlagTrackerView Empty { get; } = new(Array.Empty<OverviewRow>(), Array.Empty<SiteRow>(), false, new Dictionary<string, int>());

    /// <summary>Eine Zeile je Name, in der Reihenfolge des Ergebnisses.</summary>
    public IReadOnlyList<OverviewRow> Overview { get; }

    /// <summary>Flach: Kopfzeile je Name, dann seine Stellen; die Musterstellen stehen zuletzt unter einer eigenen Kopfzeile.</summary>
    public IReadOnlyList<SiteRow> Sites { get; }

    public bool Truncated { get; }


    /// <summary>Index der Kopfzeile des Namens in <see cref="Sites"/>, -1 wenn es ihn nicht gibt.</summary>
    public int IndexOfName(string name) => _nameIndex.TryGetValue(name, out var index) ? index : -1;

    /// <param name="hide">Ausblendregeln der aktiven Mod; null ohne Regeln.</param>
    public static FlagTrackerView From(FlagSearchResult result, FlagHideList? hide = null)
    {
        var overview = new List<OverviewRow>(result.Names.Count);
        var rows = new List<SiteRow>();
        var index = new Dictionary<string, int>(result.Names.Count);

        foreach (var summary in result.Names)
        {
            var hidden = hide is not null && hide.IsHidden(summary.Name);
            overview.Add(OverviewOf(summary, hidden));
            result.SitesByName.TryGetValue(summary.Name, out var sites);
            var count = sites?.Count ?? 0;
            index[summary.Name] = rows.Count;
            rows.Add(new SiteRow(SiteRowKind.NameHeader, FlagTrackerTexts.NameGroup(summary.Name, count), null, "", "", "", "", null));
            if (sites is not null)
            {
                AddSorted(rows, sites, pattern: false);
            }
        }

        if (result.PatternSites.Count > 0)
        {
            rows.Add(new SiteRow(SiteRowKind.PatternHeader, FlagTrackerTexts.PatternGroup(result.PatternSites.Count), null, "", "", "", "", null));
            AddSorted(rows, result.PatternSites, pattern: true);
        }

        return new FlagTrackerView(overview, rows, result.Truncated, index);
    }

    private static OverviewRow OverviewOf(FlagNameSummary summary, bool hidden)
    {
        var kinds = string.Join(", ", summary.Kinds.Select(k => FlagTrackerTexts.Kind(k.Kind, k.Family)));
        var hints = new List<string>(2);
        if (summary.NeverSet)
        {
            hints.Add(summary.PatternMayCover ? FlagTrackerTexts.MaybeSetByPattern : FlagTrackerTexts.NeverSet);
        }

        if (summary.NeverRead)
        {
            hints.Add(summary.PatternMayCover ? FlagTrackerTexts.MaybeReadByPattern : FlagTrackerTexts.NeverRead);
        }

        var hint = hints.Count == 0 ? null : string.Join("; ", hints);
        if (hidden)
        {
            return new OverviewRow(summary.Name, kinds, summary.SetCount, summary.ReadCount, summary.RemoveCount, FlagTrackerTexts.HiddenSuffix(hint), false, true);
        }

        return new OverviewRow(summary.Name, kinds, summary.SetCount, summary.ReadCount, summary.RemoveCount, hint, hint is not null && !summary.PatternMayCover);
    }

    private static void AddSorted(List<SiteRow> rows, IReadOnlyList<FlagSite> sites, bool pattern)
    {
        var sorted = sites.OrderBy(s => s.Role).ThenBy(s => s.Origin)
            .ThenBy(s => s.Location.RelativePath, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Location.Line);
        foreach (var site in sorted)
        {
            var file = $"{site.Location.RelativePath}:{site.Location.Line}";
            if (site.Origin == FlagOrigin.OtherMod)
            {
                file = $"{site.OriginName} / {file}";
            }

            var via = site.Via is null
                ? null
                : FlagTrackerTexts.Via(site.ViaName ?? "", FileNameOf(site.Via.RelativePath), site.Via.Line);
            rows.Add(new SiteRow(SiteRowKind.Site, pattern ? site.Name : "", site, FlagTrackerTexts.Origin(site.Origin), FlagTrackerTexts.Role(site.Role),
                FlagTrackerTexts.Kind(site.Kind, site.Family), file, via));
        }
    }

    private static string FileNameOf(string relativePath)
    {
        var slash = relativePath.LastIndexOf('/');
        return slash < 0 ? relativePath : relativePath[(slash + 1)..];
    }
}
