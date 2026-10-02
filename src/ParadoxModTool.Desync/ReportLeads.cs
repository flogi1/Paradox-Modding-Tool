using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.Desync;

/// <summary>Zuordnung eines erkannten Fehlers zu einem Mod; ohne Zuordnung alles <c>null</c> bzw. <c>false</c>.</summary>
/// <param name="ModName">Name des Mods, zu dem die Datei gehört; <c>null</c> bei Spieldateien oder ohne Zuordnung.</param>
/// <param name="IsOwnMod">Ob die Datei zu einem Mod des Nutzers gehört.</param>
/// <param name="FullPath">Der volle Pfad, nur wenn die Datei auf der Platte gefunden wurde.</param>
public sealed record LeadAttribution(string? ModName, bool IsOwnMod, string? FullPath);

/// <summary>Macht aus Einträgen der Zeitleiste die Hinweise eines Berichts.</summary>
public static class ReportLeads
{
    private const string ScriptErrorHeader = "Script system error!";
    private const string ScriptLocationPrefix = "Script location:";
    private const string LineSeparator = " / ";

    /// <summary>
    /// Je Eintrag ein Hinweis mit genau seiner Maschine, in der Reihenfolge der Zeitleiste (zusammengefasst wird erst in
    /// <see cref="ReportFacts.Build"/>). <paramref name="attribute"/> wird nur für Einträge mit erkanntem Fehler gerufen.
    /// Meldung: die Zeilen des Blocks ohne die Kopfzeile "Script system error!" und ohne "Script location:"-Zeilen (Datei und Zeile stehen
    /// eigens), mit " / " verbunden; bleibt nichts übrig, die erste Zeile.
    /// </summary>
    public static IReadOnlyList<ReportLead> From(IEnumerable<TimelineEntry> timeline, Func<ErrorEntry, LeadAttribution> attribute) =>
        timeline.Select(entry =>
        {
            var attribution = entry.Error is { } error ? attribute(error) : null;
            return new ReportLead([entry.MachineId], entry.GameDate, MessageOf(entry.Line.Message), entry.Error?.FilePath,
                attribution?.FullPath, entry.Error?.FileLine, attribution?.ModName, attribution?.IsOwnMod == true, entry.Lead);
        }).ToList();

    /// <summary>Der Text einer Log-Meldung auf einer Zeile; Folgezeilen sind mit "\n" angehängt (siehe <see cref="LogLine"/>).</summary>
    private static string MessageOf(string message)
    {
        var lines = message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var meaningful = lines
            .Where(line => !string.Equals(line, ScriptErrorHeader, StringComparison.Ordinal)
                           && !line.StartsWith(ScriptLocationPrefix, StringComparison.Ordinal))
            .ToList();
        return meaningful.Count > 0 ? string.Join(LineSeparator, meaningful) : lines.FirstOrDefault() ?? string.Empty;
    }
}
