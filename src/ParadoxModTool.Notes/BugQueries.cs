namespace ParadoxModTool.Notes;

/// <summary>Ein Bezug einer Meldung, der zu einer bestimmten Datei passt.</summary>
public sealed record BugMatch(BugReport Report, BugLink Link);

/// <summary>Lesende Abfragen über alle Meldungen. Sortierung überall: offene zuerst, dann erledigte, dann verworfene; darin nach Anlegezeit.</summary>
public static class BugQueries
{
    /// <summary>
    /// Passt ein Bezug zur Datei mit dem Notiz-Schlüssel <paramref name="fileKey"/>? Mod-Schlüssel: gleicher relativer Pfad (ohne Groß-/Kleinschreibung)
    /// und dieselbe Mod oder derselbe Hauptmod. Volle Pfade: gleicher Pfad.
    /// </summary>
    public static bool Matches(BugLink link, string fileKey, BugProjects projects)
    {
        if (NoteKeys.TryParseModKey(fileKey, out var fileMod, out var fileRelative))
        {
            return NoteKeys.TryParseModKey(link.Key, out var linkMod, out var linkRelative)
                && string.Equals(fileRelative, linkRelative, StringComparison.OrdinalIgnoreCase)
                && (fileMod == linkMod || (projects.RootOf(fileMod) is { } root && root == projects.RootOf(linkMod)));
        }

        return string.Equals(link.Key, fileKey, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<BugMatch> ForFile(BugData data, string filePath, BugProjects projects) =>
        ForFileKey(data, NoteKeys.ForFile(filePath), projects);

    /// <summary>Alle Meldungen mit einem Bezug auf die Datei, je Meldung der erste passende Bezug.</summary>
    public static IReadOnlyList<BugMatch> ForFileKey(BugData data, string fileKey, BugProjects projects) =>
        Order(data.Reports)
            .Select(report => report.Links.FirstOrDefault(link => Matches(link, fileKey, projects)) is { } link ? new BugMatch(report, link) : null)
            .OfType<BugMatch>()
            .ToList();

    /// <summary>Meldungen eines Projekts (Id des Hauptmods); <see langword="null"/> = Meldungen ohne Projekt (<c>ModId == null</c>).</summary>
    public static IReadOnlyList<BugReport> ForProject(BugData data, string? projectId, BugProjects projects) =>
        Order(data.Reports.Where(report => projectId is null
            ? report.ModId is null
            : report.ModId is not null && projects.RootOf(report.ModId) == projectId)).ToList();

    public static int UnseenCount(BugData data, string? projectId, BugProjects projects) =>
        ForProject(data, projectId, projects).Count(report => !report.Seen && !report.Removed);

    /// <summary>Projekt (Id des Hauptmods) einer Datei; <see langword="null"/> außerhalb jeder bekannten Mod.</summary>
    public static string? ProjectOfFile(string filePath, BugProjects projects) =>
        NoteKeys.TryParseModKey(NoteKeys.ForFile(filePath), out var modId, out _) ? projects.RootOf(modId) : null;

    /// <summary>Der Bezug auf die Datei <paramref name="fileKey"/>, sonst der erste Bezug; <see langword="null"/> ohne Bezüge.</summary>
    public static BugLink? PreferredLink(BugReport report, string? fileKey, BugProjects projects) =>
        (fileKey is null ? null : report.Links.FirstOrDefault(link => Matches(link, fileKey, projects))) ?? report.Links.FirstOrDefault();

    private static IEnumerable<BugReport> Order(IEnumerable<BugReport> reports) =>
        reports.OrderBy(report => report.Status).ThenBy(report => report.CreatedUtc);
}
