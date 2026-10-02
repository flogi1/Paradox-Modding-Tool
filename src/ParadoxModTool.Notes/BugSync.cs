namespace ParadoxModTool.Notes;

/// <summary>Eine Meldung, wie der Dienst sie liefert (nur Felder, deren Eigentümer der Dienst ist).</summary>
public sealed record RemoteBug(string ExternalId, string Url, string Title, string Body, string Author, BugStatus Status, DateTime CreatedUtc, DateTime UpdatedUtc, DateTime? StatusChangedUtc, IReadOnlyList<BugAttachment>? Attachments = null);

/// <summary>Eine Antwort in der Diskussion einer Meldung (Kommentar bzw. Thread-Nachricht); wird nicht gespeichert.</summary>
public sealed record BugComment(string Author, DateTime CreatedUtc, string Body, IReadOnlyList<BugAttachment>? Attachments = null);

public sealed record BugSyncResult(int Added, int Changed, int Removed);

/// <summary>
/// Gleicht die abgerufenen Meldungen EINER Quelle mit dem Speicher ab. Bekannte (gleiche externe Kennung) bekommen die Dienst-Felder neu,
/// Bezüge, "gesehen" und Besitzer bleiben. Neue kommen ungesehen mit <paramref name="ownerModId"/> als Besitzer dazu. Nicht mehr gelieferte
/// werden als entfernt markiert, nie gelöscht.
/// </summary>
public static class BugSync
{
    public static BugSyncResult Apply(BugData data, BugSource source, string sourceKey, string ownerModId, IReadOnlyList<RemoteBug> fetched)
    {
        var existing = data.Reports
            .Where(report => report.Source == source && string.Equals(report.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase))
            .GroupBy(report => report.ExternalId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        int added = 0, changed = 0, removed = 0;

        foreach (var remote in fetched)
        {
            if (existing.Remove(remote.ExternalId, out var report))
            {
                if (report.Removed || Differs(report, remote))
                {
                    Copy(report, remote);
                    report.Removed = false;
                    changed++;
                }
                else
                {
                    // Gleiche Dateien, aber Discord signiert die Adressen bei jedem Abruf neu: die frischen Adressen trotzdem speichern.
                    report.Attachments = (remote.Attachments ?? Array.Empty<BugAttachment>()).ToList();
                }

                continue;
            }

            report = new BugReport { Source = source, SourceKey = sourceKey, ExternalId = remote.ExternalId, ModId = ownerModId, Seen = false };
            Copy(report, remote);
            data.Reports.Add(report);
            added++;
        }

        foreach (var gone in existing.Values.Where(report => !report.Removed))
        {
            gone.Removed = true;
            removed++;
        }

        return new BugSyncResult(added, changed, removed);
    }

    /// <summary>Die Quelle ist nicht mehr verknüpft: alle ihre Meldungen gelten als entfernt. Liefert die Zahl neu markierter.</summary>
    public static int MarkSourceRemoved(BugData data, BugSource source, string sourceKey)
    {
        var count = 0;
        foreach (var report in data.Reports.Where(r => r.Source == source && !r.Removed && string.Equals(r.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase)))
        {
            report.Removed = true;
            count++;
        }

        return count;
    }

    private static bool Differs(BugReport report, RemoteBug remote) =>
        report.Url != remote.Url || report.Title != remote.Title || report.Body != remote.Body || report.Author != remote.Author
        || report.Status != remote.Status || report.CreatedUtc != remote.CreatedUtc || report.UpdatedUtc != remote.UpdatedUtc
        || !SameAttachments(report.Attachments, remote.Attachments ?? Array.Empty<BugAttachment>());

    private static bool SameAttachments(IReadOnlyList<BugAttachment> stored, IReadOnlyList<BugAttachment> fetched) =>
        stored.Count == fetched.Count && stored.Zip(fetched).All(pair => pair.First.SameFile(pair.Second));

    private static void Copy(BugReport report, RemoteBug remote)
    {
        if (report.Status != remote.Status || report.StatusChangedUtc is null)
        {
            report.StatusChangedUtc = remote.StatusChangedUtc ?? (report.Status != remote.Status ? DateTime.UtcNow : report.StatusChangedUtc);
        }

        report.Url = remote.Url;
        report.Title = remote.Title;
        report.Body = remote.Body;
        report.Author = remote.Author;
        report.Status = remote.Status;
        report.CreatedUtc = remote.CreatedUtc;
        report.UpdatedUtc = remote.UpdatedUtc;
        report.Attachments = (remote.Attachments ?? Array.Empty<BugAttachment>()).ToList();
    }
}
