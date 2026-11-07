namespace ParadoxModTool.VanillaDiff;

/// <summary>Art einer Vanilla-Strukturänderung, die eine Mod-Datei trifft.</summary>
public enum StructureChangeKind
{
    /// <summary>Vanilla hat statt der Datei "X.ext" jetzt den Ordner "X/" an derselben Stelle.</summary>
    FileReplacedByFolder,

    /// <summary>Die Datei gibt es in Vanilla nicht mehr hier, aber mit demselben Namen an anderer Stelle.</summary>
    FileMoved,

    /// <summary>Die Datei gibt es in Vanilla nicht mehr.</summary>
    FileRemoved,

    /// <summary>Ein Vanilla-Ordner mit Mod-Dateien ist verschwunden (laut alter Verzeichnisliste).</summary>
    FolderGone,
}

/// <summary>
/// Eine Mod-Datei (bei <see cref="StructureChangeKind.FolderGone"/> ein Mod-Ordner, Pfad mit "/" am Ende), die eine Vanilla-Strukturänderung trifft.
/// <see cref="NewVanillaPaths"/>: relative Vanilla-Pfade des neuen Orts (Ordner mit "/" am Ende), höchstens 3, leer bei
/// <see cref="StructureChangeKind.FileRemoved"/>. <see cref="OldVersion"/>: Version der Historie, die die Änderung belegt; <see langword="null"/> ohne Historie.
/// </summary>
public sealed record StructureChange(string ModRelativePath, string ModFullPath, StructureChangeKind Kind, IReadOnlyList<string> NewVanillaPaths, string? OldVersion);

/// <summary>
/// Findet Mod-Dateien, die eine Strukturänderung von Vanilla trifft (z. B. EU V: "main_menu/gui/messagetypes.txt" wurde zum Ordner
/// "main_menu/gui/messagetypes/", eine Mod mit der alten Datei verliert still ihre Meldungstypen). Liest nur, ändert nichts.
/// Eigene Ordner der Mod ("gfx/interface/&lt;mod&gt;/…") meldet sie nie: berücksichtigt werden nur Dateien in Ordnern, die Vanilla
/// nachweislich hat oder hatte.
/// </summary>
public static class VanillaStructureCheck
{
    private const int MaxCandidates = 3;

    public static IReadOnlyList<StructureChange> Find(string modRoot, string vanillaRoot, VanillaHistoryStore store, string currentVersion, CancellationToken ct)
    {
        // Ohne Vanilla-Ordner (nicht gefunden) wäre jeder alte Vanilla-Ordner "verschwunden": lieber nichts melden.
        if (!Directory.Exists(vanillaRoot))
        {
            return Array.Empty<StructureChange>();
        }

        var comparer = GameVersionComparer.Instance;
        var oldListings = store.ListStructureListingVersions()
            .Where(v => comparer.Compare(v, currentVersion) < 0)
            .OrderByDescending(v => v, comparer)
            .Select(v => (Version: v, Listing: VanillaStructureListing.Load(store.StructureListingPath(v))))
            .Where(l => l.Listing is not null)
            .Select(l => (l.Version, Listing: l.Listing!))
            .ToList();
        (string Version, VanillaStructureListing Listing)? latest = oldListings.Count > 0 ? oldListings[0] : null;
        var index = new Lazy<VanillaStructureListing>(() => BuildNameIndex(modRoot, vanillaRoot, ct));

        var changes = new List<StructureChange>();
        var goneFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relative in ModFiles(modRoot))
        {
            ct.ThrowIfCancellationRequested();
            var parent = relative[..relative.LastIndexOf('/')];
            var snapshotVersion = store.ListRecordedVersions(relative)
                .Where(v => comparer.Compare(v, currentVersion) < 0)
                .OrderByDescending(v => v, comparer)
                .FirstOrDefault();

            // Regel 1: nur Ordner, die Vanilla hat oder hatte (eigener Snapshot der Datei belegt den Ordner).
            if (!VanillaFolderExists(vanillaRoot, parent) && snapshotVersion is null && !oldListings.Any(l => l.Listing.HasFolder(parent)))
            {
                continue;
            }

            // Regel 3: verschwundener Vanilla-Ordner laut neuester alter Liste; der oberste gilt, seine Dateien nicht einzeln.
            if (latest is { } old && TopmostGoneFolder(vanillaRoot, old.Listing, parent) is { } gone)
            {
                if (goneFolders.Add(gone))
                {
                    var name = gone[(gone.LastIndexOf('/') + 1)..];
                    var candidates = index.Value.FoldersNamed(name)
                        .Where(f => !string.Equals(f, gone, StringComparison.OrdinalIgnoreCase))
                        .Take(MaxCandidates)
                        .Select(f => f + "/")
                        .ToList();
                    changes.Add(new StructureChange(gone + "/", FullPath(modRoot, gone) + Path.DirectorySeparatorChar, StructureChangeKind.FolderGone, candidates, old.Version));
                }

                continue;
            }

            if (File.Exists(FullPath(vanillaRoot, relative)))
            {
                continue;
            }

            // Regel 2: Datei fehlt in Vanilla.
            var listingVersion = oldListings.FirstOrDefault(l => l.Listing.HasFile(relative)).Version;
            var historyVersion = new[] { snapshotVersion, listingVersion }.Where(v => v is not null).OrderByDescending(v => v, comparer).FirstOrDefault();
            var fileName = relative[(relative.LastIndexOf('/') + 1)..];
            var folder = parent + "/" + Path.GetFileNameWithoutExtension(fileName);
            if (VanillaFolderExists(vanillaRoot, folder))
            {
                changes.Add(new StructureChange(relative, FullPath(modRoot, relative), StructureChangeKind.FileReplacedByFolder, new[] { folder + "/" }, historyVersion));
                continue;
            }

            if (historyVersion is null)
            {
                continue;
            }

            var moved = index.Value.FilesNamed(fileName)
                .Where(f => !string.Equals(f, relative, StringComparison.OrdinalIgnoreCase))
                .Take(MaxCandidates)
                .ToList();
            changes.Add(new StructureChange(relative, FullPath(modRoot, relative), moved.Count > 0 ? StructureChangeKind.FileMoved : StructureChangeKind.FileRemoved, moved, historyVersion));
        }

        return changes.OrderBy(c => c.ModRelativePath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Relative Pfade der Mod-Dateien ohne Dateien im Wurzelordner und ohne Punkt-Ordner (wie <see cref="VanillaStructureListing.ScopesOf"/>).</summary>
    private static IEnumerable<string> ModFiles(string modRoot)
    {
        if (!Directory.Exists(modRoot))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(modRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(modRoot, file).Replace('\\', '/');
            if (!relative.Contains('/') || relative.StartsWith('.') || relative.Contains("/.", StringComparison.Ordinal))
            {
                continue;
            }

            yield return relative;
        }
    }

    /// <summary>Der oberste Ordner ab <paramref name="folder"/> aufwärts, der in der alten Liste steht und jetzt fehlt; jede Stufe muss beides erfüllen.</summary>
    private static string? TopmostGoneFolder(string vanillaRoot, VanillaStructureListing old, string folder)
    {
        string? gone = null;
        for (var current = folder; current.Length > 0; current = current.Contains('/') ? current[..current.LastIndexOf('/')] : "")
        {
            if (!old.HasFolder(current) || VanillaFolderExists(vanillaRoot, current))
            {
                break;
            }

            gone = current;
        }

        return gone;
    }

    /// <summary>
    /// Namensverzeichnis des aktuellen Vanilla für "verschoben": die Bereiche der Mod (<see cref="VanillaStructureListing.ScopesOf"/>). Fehlt ein
    /// Bereich "A/B" in Vanilla, kommen die Ordner "A/&lt;x&gt;/B" dazu (ein Ordner eine Ebene tiefer gerutscht) — kein Lauf über die ganze Installation.
    /// </summary>
    private static VanillaStructureListing BuildNameIndex(string modRoot, string vanillaRoot, CancellationToken ct)
    {
        var scopes = new HashSet<string>(VanillaStructureListing.ScopesOf(modRoot), StringComparer.OrdinalIgnoreCase);
        foreach (var scope in scopes.ToList())
        {
            var slash = scope.IndexOf('/');
            if (slash < 0 || VanillaFolderExists(vanillaRoot, scope))
            {
                continue;
            }

            var top = FullPath(vanillaRoot, scope[..slash]);
            if (!Directory.Exists(top))
            {
                continue;
            }

            var name = scope[(slash + 1)..];
            foreach (var sub in Directory.EnumerateDirectories(top))
            {
                ct.ThrowIfCancellationRequested();
                if (Directory.Exists(Path.Combine(sub, name)))
                {
                    scopes.Add(Path.GetRelativePath(vanillaRoot, Path.Combine(sub, name)).Replace('\\', '/'));
                }
            }
        }

        return VanillaStructureListing.Scan(vanillaRoot, scopes, ct);
    }

    private static bool VanillaFolderExists(string vanillaRoot, string relativeFolder) => Directory.Exists(FullPath(vanillaRoot, relativeFolder));

    private static string FullPath(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
}
