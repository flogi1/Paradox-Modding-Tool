using System.Globalization;
using ParadoxModTool.Localization;

namespace ParadoxModTool.Performance;

/// <summary>
/// Texte des Performance-Checkers. Die Schlüssel stehen in <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche Text hier ist der
/// Rückfall und muss mit de.json übereinstimmen (Test <c>PerformanceTextsTests</c>). Jeder Aufruf von <c>T</c>/<c>F</c> steht in einer Zeile.
/// Die Einheit der Zeiten steht nur hier (<see cref="TimeUnit"/>); die Zeiten selbst sind Rohwerte des Dumps.
/// </summary>
public static class PerformanceTexts
{
    /// <summary>Einheit hinter jeder Zeit; der Dump liefert Rohwerte, es wird nicht umgerechnet.</summary>
    public static string TimeUnit => T("performance.timeUnit", "s");

    public static string Origin(RowOrigin origin) => origin switch
    {
        RowOrigin.Mod => T("performance.origin.mod", "Mod"),
        RowOrigin.Both => T("performance.origin.both", "Mod und Vanilla"),
        RowOrigin.OtherMod => T("performance.origin.otherMod", "Andere Mod"),
        RowOrigin.Vanilla => T("performance.origin.vanilla", "Vanilla"),
        _ => T("performance.origin.unknown", "Unbekannt"),
    };

    public static string VerdictName(Verdict verdict) => verdict switch
    {
        Verdict.Better => T("performance.verdict.better", "besser"),
        Verdict.Worse => T("performance.verdict.worse", "schlechter"),
        Verdict.Unchanged => T("performance.verdict.unchanged", "unverändert"),
        Verdict.NewInB => T("performance.verdict.newInB", "neu in B"),
        Verdict.MissingInB => T("performance.verdict.missingInB", "fehlt in B"),
        _ => T("performance.verdict.behaviorChanged", "Verhalten geändert"),
    };

    // Zusammenfassung eines Laufs.
    public static string ReportHeader(string runName, int? months) => months is { } m
        ? F("performance.report.header", "Profiler-Lauf {0}: {1} Monate", runName, m.ToString(CultureInfo.CurrentCulture))
        : F("performance.report.headerUnknownMonths", "Profiler-Lauf {0}: Monate unbekannt", runName);

    public static string ReportTotals => T("performance.report.totals", "Eigenzeit nach Herkunft");

    public static string ReportSum => T("performance.report.sum", "Summe");

    /// <param name="selfFormatted">Die Eigenzeit der Mod-Seite, bereits formatiert.</param>
    public static string ReportModSelf(string selfFormatted) => F("performance.report.modSelf", "Mod-Seite (Mod sowie Mod und Vanilla): {0} {1}", selfFormatted, TimeUnit);

    public static string ReportTopModTitle(int top) => F("performance.report.topModTitle", "Top {0} Mod-Zeilen (Mod und beide)", top.ToString(CultureInfo.CurrentCulture));

    public static string ReportTopAllTitle(int top) => F("performance.report.topAllTitle", "Top {0} Zeilen aller Herkünfte", top.ToString(CultureInfo.CurrentCulture));

    /// <param name="calls">Bereits formatiert, ebenso <paramref name="self"/> und <paramref name="total"/>.</param>
    public static string ReportRowLine(int rank, string key, string origin, string calls, string self, string total) =>
        F("performance.report.rowLine", "{0}. {1} [{2}] Aufrufe: {3}, Eigenzeit: {4} {6}, Gesamtzeit: {5} {6}", rank.ToString(CultureInfo.CurrentCulture), key, origin, calls, self, total, TimeUnit);

    // Vergleich zweier Läufe.
    public static string ComparisonHeader(string a, string b) => F("performance.compare.header", "Vergleich der Profiler-Läufe: A = {0}, B = {1}", a, b);

    public static string MonthsSame(int months) => F("performance.compare.monthsSame", "Beide Läufe: {0} Monate", months.ToString(CultureInfo.CurrentCulture));

    public static string MonthsDiffer(int? a, int? b) => F("performance.compare.monthsDiffer", "Achtung: Die Läufe decken unterschiedlich viele Monate ab (A: {0}, B: {1}). Die Zeiten sind nicht direkt vergleichbar.", Months(a), Months(b));

    public static string MonthsUnknown => T("performance.compare.monthsUnknown", "Achtung: Die Monatszahl mindestens eines Laufs ist unbekannt. Die Zeiten sind nur bedingt vergleichbar.");

    public static string NoiseNote(NoiseSettings noise) =>
        F("performance.compare.noise", "Rauschschwellen: Zeit relativ {0}, absolut {1} {2}, Aufrufe {3}", Percent(noise.RelativeThreshold), noise.AbsoluteFloor.ToString("N3", CultureInfo.CurrentCulture), TimeUnit, Percent(noise.CallsChangeThreshold));

    /// <param name="a">Mod-Eigenzeit in Lauf A, bereits formatiert, ebenso <paramref name="b"/> und <paramref name="delta"/>; <paramref name="verdict"/> ist das Urteilswort.</param>
    public static string ComparisonModSelf(string a, string b, string delta, string verdict) => F("performance.compare.modSelf", "Mod-Eigenzeit: A {0} {3}, B {1} {3}, Änderung {2}, Urteil: {4}", a, b, delta, TimeUnit, verdict);

    public static string ComparisonRowsTitle(int top) => F("performance.compare.rowsTitle", "Die {0} zeitintensivsten Zeilen der Mod-Seite (voller Schlüssel, nach der größeren Gesamtzeit von A und B)", top.ToString(CultureInfo.CurrentCulture));

    public static string ComparisonFilesTitle(int top) => F("performance.compare.filesTitle", "Die {0} zeitintensivsten Dateien der Mod-Seite (Summe je Datei, unempfindlich gegen verschobene Zeilen)", top.ToString(CultureInfo.CurrentCulture));

    /// <param name="calls">Aufrufe als <c>A -> B</c>, bereits formatiert, ebenso <paramref name="total"/> und <paramref name="delta"/>.</param>
    public static string ComparisonRowLine(int rank, string key, string origin, string calls, string total, string delta, string verdict) =>
        F("performance.compare.rowLine", "{0}. {1} [{2}] Aufrufe: {3}, Gesamtzeit: {4} {7}, Änderung: {5}, Urteil: {6}", rank.ToString(CultureInfo.CurrentCulture), key, origin, calls, total, delta, verdict, TimeUnit);

    private static string Months(int? months) => months?.ToString(CultureInfo.CurrentCulture) ?? "?";

    private static string Percent(double ratio) => ratio.ToString("P0", CultureInfo.CurrentCulture);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
