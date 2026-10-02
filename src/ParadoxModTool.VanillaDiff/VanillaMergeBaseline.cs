namespace ParadoxModTool.VanillaDiff;

/// <summary>Ergebnis von <see cref="VanillaMergeBaseline.Resolve"/>.</summary>
public sealed class VanillaMergeBaselineResult
{
    /// <summary>True, wenn ein ÄLTERER (von der aktuellen Version verschiedener) Snapshot existiert - erst dann ist "Merge nach Update" möglich.</summary>
    public required bool HasOlderBaseline { get; init; }

    public string? OlderVersion { get; init; }
    public string? OlderVanillaText { get; init; }
}

/// <summary>
/// Klärt beim Öffnen des Diff-Fensters (Abschnitt 7), ob für eine Vanilla/Mod-Dateipaarung
/// bereits eine Basis für den 3-Wege-Merge existiert. Zeichnet bei Bedarf die aktuelle
/// Vanilla-Fassung als Snapshot der aktuellen Version auf (falls noch nicht geschehen) und
/// meldet, ob zusätzlich ein ÄLTERER Snapshot vorliegt, der als "alte Vanilla-Fassung" für
/// <see cref="VanillaMergeWorkflow.ComputeMerge"/> dienen kann. Bei mehreren älteren Snapshots
/// gilt die neueste Version unter der aktuellen (<see cref="VanillaBaseVersion.Choose"/>).
/// </summary>
public static class VanillaMergeBaseline
{
    public static VanillaMergeBaselineResult Resolve(
        VanillaHistoryStore store, string relativePath, string currentVersion, string currentVanillaFileFullPath)
    {
        var recordedVersions = store.ListRecordedVersions(relativePath);
        var olderVersion = VanillaBaseVersion.Choose(recordedVersions, currentVersion, null, null);

        if (!store.HasSnapshot(relativePath, currentVersion))
        {
            store.RecordSnapshot(relativePath, currentVersion, currentVanillaFileFullPath);
        }

        if (olderVersion is null)
        {
            return new VanillaMergeBaselineResult { HasOlderBaseline = false };
        }

        return new VanillaMergeBaselineResult
        {
            HasOlderBaseline = true,
            OlderVersion = olderVersion,
            OlderVanillaText = store.ReadSnapshot(relativePath, olderVersion)
        };
    }
}
