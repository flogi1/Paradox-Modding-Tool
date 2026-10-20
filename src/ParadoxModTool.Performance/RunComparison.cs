namespace ParadoxModTool.Performance;

/// <summary>Vergleicht zwei Profiler-Läufe (A vorher, B nachher) mit Rauschschwelle und Verhaltens-Erkennung, entweder Zeile für Zeile über den vollen Schlüssel (<see cref="Rows"/>) oder je Datei (<see cref="RowsByFile"/>).</summary>
public sealed class RunComparison
{
    private readonly IReadOnlyList<ComparisonRow> _rows;
    private readonly IReadOnlyList<ComparisonRow> _fileRows;

    public RunComparison(ProfilingSummary a, ProfilingSummary b, RunDuration durationA, RunDuration durationB, NoiseSettings noise)
    {
        DurationA = durationA;
        DurationB = durationB;
        Noise = noise;
        ModSelfA = a.ModSelf;
        ModSelfB = b.ModSelf;
        ModSelfDeltaRatio = ModSelfA == 0 ? null : (ModSelfB - ModSelfA) / ModSelfA;
        ModSelfVerdict = TimeVerdict(ModSelfA, ModSelfB - ModSelfA, noise);
        SameMonths = durationA.Months is { } ma && durationB.Months is { } mb && ma == mb;

        // Bei doppelten Schlüsseln zählt wie bei ProfilingDump.Find die erste Zeile.
        _rows = Build(ByKey(a), ByKey(b), noise);
        _fileRows = Build(ByFile(a), ByFile(b), noise);
    }

    /// <summary>Laufdauer von Lauf A, wie übergeben (für Berichte).</summary>
    public RunDuration DurationA { get; }

    /// <summary>Laufdauer von Lauf B, wie übergeben (für Berichte).</summary>
    public RunDuration DurationB { get; }

    /// <summary>Die Rauschschwellen, mit denen verglichen wurde (für Berichte).</summary>
    public NoiseSettings Noise { get; }

    /// <summary>Eigenzeit der Mod-Seite (Mod plus Both) in Lauf A, über alle Zeilen, auch ohne Gegenstück in B.</summary>
    public double ModSelfA { get; }

    /// <summary>Eigenzeit der Mod-Seite (Mod plus Both) in Lauf B, über alle Zeilen, auch ohne Gegenstück in A.</summary>
    public double ModSelfB { get; }

    /// <summary>(B - A) / A der Mod-Eigenzeit; <c>null</c>, wenn A = 0.</summary>
    public double? ModSelfDeltaRatio { get; }

    /// <summary>Urteil über die Mod-Eigenzeit A -> B mit denselben Rauschregeln wie bei den Zeilen (ohne Aufrufe-Prüfung): nur <see cref="Verdict.Better"/>, <see cref="Verdict.Worse"/> oder <see cref="Verdict.Unchanged"/>.</summary>
    public Verdict ModSelfVerdict { get; }

    /// <summary>Beide Läufe haben bekannte, gleiche Monatszahl; nur dann sind die Zeiten direkt vergleichbar.</summary>
    public bool SameMonths { get; }

    /// <summary>
    /// Die Zeilen der gewählten Herkünfte, gepaart über den vollen Schlüssel, absteigend nach der größeren Gesamtzeit beider Seiten
    /// (fehlt eine Seite, zählt die andere; so landet eine starke Verschlechterung einer in A kleinen Zeile oben), Gleichstand nach Schlüssel (ordinal).
    /// Verschiebt sich eine Zeile in der Datei, erscheint sie als <see cref="Verdict.MissingInB"/> plus <see cref="Verdict.NewInB"/>; dafür gibt es <see cref="RowsByFile"/>.
    /// </summary>
    public IReadOnlyList<ComparisonRow> Rows(IReadOnlySet<RowOrigin> origins) =>
        _rows.Where(r => origins.Contains(r.Origin)).ToList();

    /// <summary>
    /// Je Dateipfad eine Zeile (<c>Key</c> = Pfad): Aufrufe und Gesamtzeit sind je Lauf über alle Zeilen des Pfads summiert,
    /// Herkunft aus A, sonst B. Dieselben Urteilsregeln und dieselbe Sortierung wie bei <see cref="Rows"/>; ein Pfad nur in einem Lauf ergibt
    /// <see cref="Verdict.MissingInB"/> bzw. <see cref="Verdict.NewInB"/>. Unempfindlich gegen verschobene Zeilennummern.
    /// </summary>
    public IReadOnlyList<ComparisonRow> RowsByFile(IReadOnlySet<RowOrigin> origins) =>
        _fileRows.Where(r => origins.Contains(r.Origin)).ToList();

    private readonly record struct Entry(RowOrigin Origin, long Calls, double Total);

    private static Dictionary<string, Entry> ByKey(ProfilingSummary summary)
    {
        var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var (row, origin) in summary.Rows)
        {
            map.TryAdd(row.Key, new Entry(origin, row.Calls, row.Total));
        }

        return map;
    }

    private static Dictionary<string, Entry> ByFile(ProfilingSummary summary)
    {
        var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var (row, origin) in summary.Rows)
        {
            map[row.Path] = map.TryGetValue(row.Path, out var sum)
                ? sum with { Calls = sum.Calls + row.Calls, Total = sum.Total + row.Total }
                : new Entry(origin, row.Calls, row.Total);
        }

        return map;
    }

    private static List<ComparisonRow> Build(Dictionary<string, Entry> a, Dictionary<string, Entry> b, NoiseSettings noise)
    {
        var rows = new List<ComparisonRow>();
        foreach (var (key, entryA) in a)
        {
            rows.Add(b.TryGetValue(key, out var entryB)
                ? Compare(key, entryA, entryB, noise)
                : new ComparisonRow(key, entryA.Origin, entryA.Calls, null, entryA.Total, null, null, Verdict.MissingInB));
        }

        foreach (var (key, entryB) in b)
        {
            if (!a.ContainsKey(key))
            {
                rows.Add(new ComparisonRow(key, entryB.Origin, null, entryB.Calls, null, entryB.Total, null, Verdict.NewInB));
            }
        }

        return rows
            .OrderByDescending(r => Math.Max(r.TotalA ?? 0, r.TotalB ?? 0))
            .ThenBy(r => r.Key, StringComparer.Ordinal)
            .ToList();
    }

    private static ComparisonRow Compare(string key, Entry a, Entry b, NoiseSettings noise)
    {
        var delta = b.Total - a.Total;
        double? ratio = a.Total == 0 ? null : delta / a.Total;
        return new ComparisonRow(key, a.Origin, a.Calls, b.Calls, a.Total, b.Total, ratio, VerdictOf(a, b, delta, noise));
    }

    private static Verdict VerdictOf(Entry a, Entry b, double delta, NoiseSettings noise)
    {
        if (a.Calls > 0 && Math.Abs((double)(b.Calls - a.Calls)) / a.Calls > noise.CallsChangeThreshold)
        {
            return Verdict.BehaviorChanged;
        }

        return TimeVerdict(a.Total, delta, noise);
    }

    private static Verdict TimeVerdict(double totalA, double delta, NoiseSettings noise)
    {
        if (Math.Abs(delta) <= noise.AbsoluteFloor || (totalA > 0 && Math.Abs(delta) / totalA <= noise.RelativeThreshold))
        {
            return Verdict.Unchanged;
        }

        return delta < 0 ? Verdict.Better : Verdict.Worse;
    }
}
