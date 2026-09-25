namespace ParadoxModTool.Notes;

/// <summary>Filter des Bugtracker-Fensters. <see cref="Source"/> <see langword="null"/> = alle Quellen; die Suche prüft Titel, Text, Autor und externe Kennung.</summary>
public sealed record BugFilter(bool Open, bool Done, bool Rejected, BugSource? Source, bool OnlyUnseen, bool OnlyWithoutLinks, string Search)
{
    public bool Matches(BugReport report)
    {
        var statusShown = report.Status switch
        {
            BugStatus.Done => Done,
            BugStatus.Rejected => Rejected,
            _ => Open,
        };
        if (!statusShown || (Source is { } source && report.Source != source) || (OnlyUnseen && report.Seen) || (OnlyWithoutLinks && report.Links.Count > 0))
        {
            return false;
        }

        var search = Search.Trim();
        return search.Length == 0
            || report.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
            || report.Body.Contains(search, StringComparison.OrdinalIgnoreCase)
            || report.Author.Contains(search, StringComparison.OrdinalIgnoreCase)
            || report.ExternalId.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}
