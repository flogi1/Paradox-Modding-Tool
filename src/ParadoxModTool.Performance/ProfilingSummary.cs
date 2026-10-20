namespace ParadoxModTool.Performance;

/// <summary>Summiert die Eigenzeit je Herkunft und bildet Ranglisten der Hotspot-Zeilen.</summary>
public sealed class ProfilingSummary
{
    private readonly List<(ProfilingRow Row, RowOrigin Origin)> _rows;

    public ProfilingSummary(ProfilingDump dump, OriginClassifier origins)
    {
        _rows = dump.Rows.Select(r => (r, origins.Classify(r.Path))).ToList();
        var byOrigin = Enum.GetValues<RowOrigin>().ToDictionary(o => o, _ => 0.0);
        foreach (var (row, origin) in _rows)
        {
            byOrigin[origin] += row.Self;
        }

        SelfByOrigin = byOrigin;
        SelfTotal = byOrigin.Values.Sum();
        ModSelf = OriginClassifier.ModSide.Sum(o => byOrigin[o]);
    }

    /// <summary>Alle Zeilen des Dumps in Dump-Reihenfolge, je mit ihrer Herkunft.</summary>
    public IReadOnlyList<(ProfilingRow Row, RowOrigin Origin)> Rows => _rows.AsReadOnly();

    /// <summary>Summe der Eigenzeit je Herkunft; jede <see cref="RowOrigin"/> ist vorhanden (0 ohne Zeilen).</summary>
    public IReadOnlyDictionary<RowOrigin, double> SelfByOrigin { get; }

    /// <summary>Summe der Eigenzeit über alle Zeilen.</summary>
    public double SelfTotal { get; }

    /// <summary>Eigenzeit der Mod-Seite (<see cref="RowOrigin.Mod"/> plus <see cref="RowOrigin.Both"/>).</summary>
    public double ModSelf { get; }

    /// <summary>Die <paramref name="top"/> Zeilen der gewählten Herkünfte, absteigend nach <paramref name="by"/>, Gleichstand nach Schlüssel (ordinal). Nullzeilen werden nur ans Ende sortiert.</summary>
    public IReadOnlyList<RankedRow> Rank(RankBy by, IReadOnlySet<RowOrigin> origins, int top) =>
        Ranked(_rows.Where(r => origins.Contains(r.Origin)), by, top);

    /// <summary>Wie <see cref="Rank"/>, aber Zeilen mit gleichem <c>Pfad:Zeile</c> werden zusammengefasst: Aufrufe = Maximum, Eigen- und Gesamtzeit = Summe, Art = <c>*</c>.</summary>
    public IReadOnlyList<RankedRow> RankByLocation(RankBy by, IReadOnlySet<RowOrigin> origins, int top)
    {
        var merged = _rows
            .Where(r => origins.Contains(r.Origin))
            .GroupBy(r => (r.Row.Path, r.Row.Line))
            .Select(g =>
            {
                var first = g.First();
                var row = new ProfilingRow(
                    $"{first.Row.Path}:{first.Row.Line}", "*", first.Row.Path, first.Row.Line,
                    g.Max(r => r.Row.Calls), g.Sum(r => r.Row.Self), g.Sum(r => r.Row.Total));
                return (row, first.Origin);
            });
        return Ranked(merged, by, top);
    }

    private static IReadOnlyList<RankedRow> Ranked(IEnumerable<(ProfilingRow Row, RowOrigin Origin)> rows, RankBy by, int top) =>
        rows.OrderByDescending(r => Measure(r.Row, by))
            .ThenBy(r => r.Row.Key, StringComparer.Ordinal)
            .Take(Math.Max(top, 0))
            .Select((r, i) => new RankedRow(i + 1, r.Row, r.Origin))
            .ToList();

    private static double Measure(ProfilingRow row, RankBy by) => by switch
    {
        RankBy.Total => row.Total,
        RankBy.Self => row.Self,
        _ => row.Calls,
    };
}
