namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Welche gespeicherte Vanilla-Version als "alte Fassung" einer Datei gilt (Spec 2026-09-30-vanilla-update-merge, Abschnitt 7.2). Nur Versionen
/// unter der aktuellen kommen in Frage; verglichen wird mit <see cref="GameVersionComparer"/>.
/// </summary>
public static class VanillaBaseVersion
{
    /// <param name="recordedVersions">Versionen, von denen ein Snapshot der Datei existiert (Namen aus der Vanilla-Historie).</param>
    /// <param name="currentVersion">Installierte Spielversion.</param>
    /// <param name="lastUpdatedTo">Version, auf die PMT die Datei zuletzt angepasst hat (Regel 1), oder <see langword="null"/>.</param>
    /// <param name="supportedPattern"><c>supported_version</c> der Mod (Regel 2), Platzhalter <c>*</c> erlaubt, oder <see langword="null"/>.</param>
    public static string? Choose(IReadOnlyCollection<string> recordedVersions, string currentVersion, string? lastUpdatedTo, string? supportedPattern)
    {
        var comparer = GameVersionComparer.Instance;
        var below = recordedVersions
            .Where(v => comparer.Compare(v, currentVersion) < 0)
            .OrderByDescending(v => v, comparer)
            .ToList();

        if (lastUpdatedTo is not null && below.FirstOrDefault(v => comparer.Compare(v, lastUpdatedTo) == 0) is { } last)
        {
            return last;
        }

        if (!string.IsNullOrWhiteSpace(supportedPattern) && below.FirstOrDefault(v => GameVersionComparer.Matches(v, supportedPattern)) is { } supported)
        {
            return supported;
        }

        return below.FirstOrDefault();
    }
}
