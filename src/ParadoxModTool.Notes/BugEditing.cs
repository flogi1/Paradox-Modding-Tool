namespace ParadoxModTool.Notes;

/// <summary>Änderungen an Meldungen. Alle Methoden arbeiten auf einem geladenen <see cref="BugData"/> (typisch in <see cref="BugReportStore.Update"/>).</summary>
public static class BugEditing
{
    public static BugReport? Find(BugData data, string id) => data.Reports.FirstOrDefault(report => report.Id == id);

    public static BugReport AddLocal(BugData data, string title, string body, string? modId, BugLink? link)
    {
        var now = DateTime.UtcNow;
        var report = new BugReport { Title = title, Body = body, ModId = modId, CreatedUtc = now, UpdatedUtc = now, Seen = true };
        if (link is not null)
        {
            report.Links.Add(link);
        }

        data.Reports.Add(report);
        return report;
    }

    /// <summary>Hängt einen Bezug an, außer es gibt ihn schon. Hat die Meldung noch kein Projekt, bekommt sie die Mod des Bezugs.</summary>
    public static bool AddLink(BugData data, string reportId, BugLink link)
    {
        if (Find(data, reportId) is not { } report || report.Links.Any(existing => existing.SameAs(link)))
        {
            return false;
        }

        report.Links.Add(link);
        report.ModId ??= BugLink.ModIdOf(link.Key);
        return true;
    }

    public static bool RemoveLink(BugData data, string reportId, BugLink link) =>
        Find(data, reportId) is { } report && report.Links.RemoveAll(existing => existing.SameAs(link)) > 0;

    /// <summary>Titel und Text ändern; nur bei lokalen Bugs, externe gehören dem Dienst.</summary>
    public static bool UpdateLocal(BugData data, string reportId, string title, string body)
    {
        if (Find(data, reportId) is not { Source: BugSource.Local } report)
        {
            return false;
        }

        report.Title = title;
        report.Body = body;
        report.UpdatedUtc = DateTime.UtcNow;
        return true;
    }

    public static bool SetStatus(BugData data, string reportId, BugStatus status)
    {
        if (Find(data, reportId) is not { } report)
        {
            return false;
        }

        if (report.Status != status)
        {
            report.Status = status;
            report.StatusChangedUtc = DateTime.UtcNow;
            report.UpdatedUtc = report.StatusChangedUtc.Value;
        }

        return true;
    }

    public static bool Delete(BugData data, string reportId) => data.Reports.RemoveAll(report => report.Id == reportId) > 0;

    /// <summary><see langword="true"/>, wenn die Meldung vorher ungesehen war.</summary>
    public static bool MarkSeen(BugData data, string reportId)
    {
        if (Find(data, reportId) is not { Seen: false } report)
        {
            return false;
        }

        report.Seen = true;
        return true;
    }
}
