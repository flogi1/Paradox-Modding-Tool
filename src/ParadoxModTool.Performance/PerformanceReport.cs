using System.Globalization;
using System.Text;

namespace ParadoxModTool.Performance;

/// <summary>
/// Reine Textberichte zu einem Lauf (<see cref="Summary"/>) und zum Vergleich zweier Läufe (<see cref="Comparison"/>), gedacht für die Zwischenablage.
/// Zahlen sind in der aktuellen Kultur formatiert, Zeiten mit drei Nachkommastellen und der Einheit <see cref="PerformanceTexts.TimeUnit"/> (Rohwerte, keine Umrechnung),
/// Änderungen als vorzeichenbehaftetes Prozent ohne Nachkommastellen.
/// </summary>
public static class PerformanceReport
{
    private const string Missing = "-";

    private static readonly IReadOnlySet<RowOrigin> AllOrigins = Enum.GetValues<RowOrigin>().ToHashSet();

    /// <summary>
    /// Kopfzeile mit Laufname und Monaten, die Eigenzeit je Herkunft (eine Zeile je <see cref="RowOrigin"/>, dazu Summe und Mod-Seite)
    /// und zwei Ranglisten nach Gesamtzeit mit je den <paramref name="top"/> ersten Zeilen: zuerst nur die Mod-Seite (Mod und Both, Standard laut Spec),
    /// dann alle Herkünfte (damit auch ein Vanilla-Basislauf etwas zeigt).
    /// </summary>
    public static string Summary(string runName, ProfilingSummary summary, RunDuration duration, int top)
    {
        var report = new StringBuilder();
        report.AppendLine(PerformanceTexts.ReportHeader(runName, duration.Months));
        report.AppendLine();
        report.AppendLine(PerformanceTexts.ReportTotals);
        foreach (var origin in Enum.GetValues<RowOrigin>())
        {
            report.AppendLine($"{PerformanceTexts.Origin(origin)}: {Time(summary.SelfByOrigin[origin])} {PerformanceTexts.TimeUnit}");
        }

        report.AppendLine($"{PerformanceTexts.ReportSum}: {Time(summary.SelfTotal)} {PerformanceTexts.TimeUnit}");
        report.AppendLine(PerformanceTexts.ReportModSelf(Time(summary.ModSelf)));
        report.AppendLine();
        report.AppendLine(PerformanceTexts.ReportTopModTitle(top));
        AppendRanked(report, summary.Rank(RankBy.Total, OriginClassifier.ModSide, top));
        report.AppendLine();
        report.AppendLine(PerformanceTexts.ReportTopAllTitle(top));
        AppendRanked(report, summary.Rank(RankBy.Total, AllOrigins, top));
        return report.ToString();
    }

    private static void AppendRanked(StringBuilder report, IReadOnlyList<RankedRow> ranked)
    {
        foreach (var item in ranked)
        {
            var row = item.Row;
            report.AppendLine(PerformanceTexts.ReportRowLine(item.Rank, row.Key, PerformanceTexts.Origin(item.Origin), Count(row.Calls), Time(row.Self), Time(row.Total)));
        }
    }

    /// <summary>
    /// Kopfzeile mit beiden Namen, Monatshinweis, Rauschschwellen, Mod-Eigenzeit in A und B mit Änderung, dann zwei Blöcke der Mod-Seite
    /// (Mod und Both) mit je den <paramref name="top"/> ersten Zeilen in der Sortierung des Vergleichs: je voller Schlüssel
    /// (<see cref="RunComparison.Rows"/>) und je Datei (<see cref="RunComparison.RowsByFile"/>, unempfindlich gegen verschobene Zeilen).
    /// </summary>
    public static string Comparison(string nameA, string nameB, RunComparison comparison, int top)
    {
        var report = new StringBuilder();
        report.AppendLine(PerformanceTexts.ComparisonHeader(nameA, nameB));
        report.AppendLine(MonthsLine(comparison));
        report.AppendLine(PerformanceTexts.NoiseNote(comparison.Noise));
        report.AppendLine(PerformanceTexts.ComparisonModSelf(Time(comparison.ModSelfA), Time(comparison.ModSelfB), Percent(comparison.ModSelfDeltaRatio), PerformanceTexts.VerdictName(comparison.ModSelfVerdict)));
        report.AppendLine();
        report.AppendLine(PerformanceTexts.ComparisonRowsTitle(top));
        AppendRows(report, comparison.Rows(OriginClassifier.ModSide), top);
        report.AppendLine();
        report.AppendLine(PerformanceTexts.ComparisonFilesTitle(top));
        AppendRows(report, comparison.RowsByFile(OriginClassifier.ModSide), top);
        return report.ToString();
    }

    private static string MonthsLine(RunComparison comparison)
    {
        var (a, b) = (comparison.DurationA.Months, comparison.DurationB.Months);
        if (comparison.SameMonths)
        {
            return PerformanceTexts.MonthsSame(a!.Value);
        }

        return a is null || b is null ? PerformanceTexts.MonthsUnknown : PerformanceTexts.MonthsDiffer(a, b);
    }

    private static void AppendRows(StringBuilder report, IReadOnlyList<ComparisonRow> rows, int top)
    {
        var rank = 0;
        foreach (var row in rows.Take(Math.Max(top, 0)))
        {
            report.AppendLine(PerformanceTexts.ComparisonRowLine(
                ++rank,
                row.Key,
                PerformanceTexts.Origin(row.Origin),
                $"{Count(row.CallsA)} -> {Count(row.CallsB)}",
                $"{Time(row.TotalA)} -> {Time(row.TotalB)}",
                Percent(row.DeltaRatio),
                PerformanceTexts.VerdictName(row.Verdict)));
        }
    }

    private static string Time(double? value) => value is { } v ? v.ToString("N3", CultureInfo.CurrentCulture) : Missing;

    private static string Count(long? value) => value is { } v ? v.ToString("N0", CultureInfo.CurrentCulture) : Missing;

    private static string Percent(double? ratio) => ratio is { } r ? r.ToString("+0%;-0%;0%", CultureInfo.CurrentCulture) : Missing;
}
