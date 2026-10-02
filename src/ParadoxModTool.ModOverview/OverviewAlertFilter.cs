namespace ParadoxModTool.ModOverview;

/// <summary>
/// Hinweisarten, die der Nutzer im Hinweisbereich ein- oder ausblenden kann: jede <see cref="OverviewAlertKind"/> (Name als Kennung) sowie die neuen
/// Bugreports und die Workshop-ID-Zeilen, die keine <see cref="OverviewAlert"/> sind. Ausgeblendete Arten zählen auch für "Nur mit Warnungen" nicht.
/// </summary>
public static class OverviewAlertFilter
{
    public const string NewBugs = "NewBugs";

    public const string WorkshopId = "WorkshopId";

    /// <summary>Alle Arten in Anzeige-Reihenfolge: neue Bugs, nicht erreichbare Quellen, die übrigen Hinweise, Workshop-IDs.</summary>
    public static IReadOnlyList<string> Categories { get; } =
        new[] { NewBugs, nameof(OverviewAlertKind.BugSourceFailed) }
            .Concat(Enum.GetNames<OverviewAlertKind>().Where(n => n != nameof(OverviewAlertKind.BugSourceFailed)))
            .Append(WorkshopId)
            .ToList();

    public static IReadOnlyList<OverviewAlert> Visible(IEnumerable<OverviewAlert> alerts, IEnumerable<string> hiddenCategories)
    {
        var hidden = hiddenCategories.ToHashSet(StringComparer.Ordinal);
        return alerts.Where(a => !hidden.Contains(a.Kind.ToString())).ToList();
    }

    /// <summary>Anzahl je Art (alle Arten, auch mit 0); neue Bugs und Workshop-IDs kommen als Zahl von außen.</summary>
    public static IReadOnlyDictionary<string, int> Counts(IEnumerable<OverviewAlert> alerts, int newBugs, int workshopId)
    {
        var counts = Categories.ToDictionary(c => c, _ => 0, StringComparer.Ordinal);
        foreach (var alert in alerts)
        {
            counts[alert.Kind.ToString()]++;
        }

        counts[NewBugs] = newBugs;
        counts[WorkshopId] = workshopId;
        return counts;
    }
}
