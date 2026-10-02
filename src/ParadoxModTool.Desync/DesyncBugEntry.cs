using ParadoxModTool.Notes;

namespace ParadoxModTool.Desync;

/// <summary>
/// Der Eintrag für den Bug-Tracker: Titel aus der Zusammenfassung, der Bericht für Mod-Autoren als Text und je Fundstelle
/// in einem eigenen Mod ein Bezug auf die Datei. Alle Texte sind Englisch wie der Bericht.
/// </summary>
/// <param name="ModId">Besitzer-Mod aus dem ersten Bezug; <c>null</c>, wenn kein Bezug zu einer geführten Mod gehört.</param>
public sealed record DesyncBugEntry(string Title, string Body, string? ModId, IReadOnlyList<BugLink> Links)
{
    private const string SummaryPrefix = "Out of Sync ";

    /// <summary>
    /// Baut den Eintrag. Bezüge: je verschiedener (Pfad, Zeile) der Hinweise aus eigenen Mods, deren Datei gefunden wurde,
    /// in der Reihenfolge der Hinweise (neueste zuerst).
    /// </summary>
    public static DesyncBugEntry Build(ReportFacts facts, IReadOnlyList<AdviceRule> advice)
    {
        var summary = ParadoxFormReport.SummaryLine(facts);
        var title = "Desync: " + (summary.StartsWith(SummaryPrefix, StringComparison.Ordinal) ? summary[SummaryPrefix.Length..] : summary);

        var links = facts.Leads
            .Where(lead => lead is { IsOwnMod: true, FullPath: not null })
            .DistinctBy(lead => (lead.FullPath, lead.Line))
            .Select(lead => BugLink.ForFile(lead.FullPath!, lead.Line, lead.Line))
            .ToList();

        var modId = links.Count > 0 ? BugLink.ModIdOf(links[0].Key) : null;
        return new DesyncBugEntry(title, ModAuthorReport.Build(facts, advice), modId, links);
    }

    /// <summary>Legt eine lokale Meldung mit allen Bezügen an; <paramref name="modId"/> ersetzt <see cref="ModId"/> (Auswahl des Nutzers).</summary>
    public BugReport AddTo(BugData data, string? modId)
    {
        var report = BugEditing.AddLocal(data, Title, Body, modId, Links.Count > 0 ? Links[0] : null);
        foreach (var link in Links.Skip(1))
        {
            BugEditing.AddLink(data, report.Id, link);
        }

        return report;
    }
}
