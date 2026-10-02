using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.VanillaDiff;

/// <summary>Ergebnis von <see cref="VanillaSnapshotRecorder.Record"/>: neu abgelegt, schon vorhanden, fehlgeschlagen (Datei gesperrt, fehlt, Platte voll).</summary>
public sealed record SnapshotRecordResult(int Recorded, int AlreadyPresent, int Failed);

/// <summary>
/// Legt die Vanilla-Dateien, die lokale Mods überschreiben, für die installierte Spielversion in der Vanilla-Historie ab (Spec
/// 2026-09-30-vanilla-update-merge, Abschnitt 7.1). Vorhandene Snapshots bleiben unverändert; Fehler zählen nur, sie werfen nie.
/// </summary>
public static class VanillaSnapshotRecorder
{
    /// <param name="storeFor">Historie je Mod-Ordner (bei "im Mod-Ordner speichern" liegt sie in der Mod).</param>
    public static SnapshotRecordResult Record(
        IEnumerable<string> modFolders,
        string vanillaRoot,
        GameProfile profile,
        string? gameVersion,
        Func<string, VanillaHistoryStore> storeFor,
        IProgress<(int Done, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            return new SnapshotRecordResult(0, 0, 0);
        }

        var work = new List<(VanillaHistoryStore Store, OverriddenFile File)>();
        var failed = 0;
        foreach (var mod in modFolders)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var files = VanillaFileTracker.FindOverriddenFiles(vanillaRoot, mod, profile);
                var store = storeFor(mod);
                work.AddRange(files.Select(f => (store, f)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                failed++;
            }
        }

        var recorded = 0;
        var present = 0;
        for (var i = 0; i < work.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (store, file) = work[i];
            try
            {
                if (store.HasSnapshot(file.RelativePath, gameVersion))
                {
                    present++;
                }
                else
                {
                    store.RecordSnapshot(file.RelativePath, gameVersion, file.VanillaFullPath);
                    recorded++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;
            }

            progress?.Report((i + 1, work.Count));
        }

        return new SnapshotRecordResult(recorded, present, failed);
    }

    /// <summary>Die Mod-Ordner im Sammelordner des Spiels, ohne Ordner mit Punkt am Anfang, sortiert; leer, wenn es ihn nicht gibt.</summary>
    public static IReadOnlyList<string> LocalModFolders(string? modRoot)
    {
        if (string.IsNullOrWhiteSpace(modRoot) || !Directory.Exists(modRoot))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateDirectories(modRoot)
            .Where(d => !Path.GetFileName(d).StartsWith('.'))
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
