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
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stores = new Dictionary<string, VanillaHistoryStore>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in modFolders)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var files = VanillaFileTracker.FindOverriddenFiles(vanillaRoot, mod, profile);
                var store = storeFor(mod);
                work.AddRange(files.Select(f => (store, f)));

                // Nur Mods, deren Historie noch keine Liste für diese Version hat, müssen durchlaufen werden.
                if (!store.HasStructureListing(gameVersion))
                {
                    var modScopes = VanillaStructureListing.ScopesOf(mod);
                    if (modScopes.Count > 0)
                    {
                        scopes.UnionWith(modScopes);
                        stores[store.StructureListingPath(gameVersion)] = store;
                    }
                }
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

        failed += WriteListings(vanillaRoot, gameVersion, scopes, stores, ct);

        return new SnapshotRecordResult(recorded, present, failed);
    }

    /// <summary>
    /// Liest Vanilla einmal über die Bereiche aller Mods und legt die Verzeichnisliste in jeder Historie ab, die für die
    /// Version noch keine hat (mehrere Mods teilen oft eine Historie, sie wird nur einmal geschrieben). Gibt die Zahl der Fehler zurück.
    /// </summary>
    private static int WriteListings(
        string vanillaRoot, string gameVersion, IReadOnlyCollection<string> scopes,
        Dictionary<string, VanillaHistoryStore> stores, CancellationToken ct)
    {
        var missing = stores.Values.Where(s => !s.HasStructureListing(gameVersion)).ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        ct.ThrowIfCancellationRequested();
        var failed = 0;
        VanillaStructureListing listing;
        try
        {
            listing = VanillaStructureListing.Scan(vanillaRoot, scopes, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return missing.Count;
        }

        foreach (var store in missing)
        {
            try
            {
                listing.Save(store.StructureListingPath(gameVersion));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }

        return failed;
    }

    /// <summary>Endung der Archivordner der GFX-Prüfung; muss mit <c>GfxArchive.ArchiveSuffix</c> übereinstimmen (hier doppelt, weil VanillaDiff GfxAudit nicht kennt; ein Test prüft die Gleichheit).</summary>
    public const string GfxArchiveSuffix = "_gfx_archiv";

    /// <summary>Die Mod-Ordner im Sammelordner des Spiels, ohne Ordner mit Punkt am Anfang und ohne Archivordner der GFX-Prüfung, sortiert; leer, wenn es ihn nicht gibt.</summary>
    public static IReadOnlyList<string> LocalModFolders(string? modRoot)
    {
        if (string.IsNullOrWhiteSpace(modRoot) || !Directory.Exists(modRoot))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateDirectories(modRoot)
            .Where(d => !Path.GetFileName(d).StartsWith('.') && !Path.GetFileName(d).EndsWith(GfxArchiveSuffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
