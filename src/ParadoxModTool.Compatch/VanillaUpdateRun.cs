using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Eine Mod-Datei am Vanilla-Pfad im Update-Lauf. <see cref="OldVersion"/> = <see langword="null"/>: keine alte Fassung gespeichert.</summary>
public sealed record UpdateFile(string RelativePath, string VanillaFullPath, string ModFullPath, string? OldVersion, bool VanillaChanged);

/// <summary>
/// Ergebnis von <see cref="VanillaUpdateRun.Scan"/>: Dateien, deren Vanilla-Fassung sich seit der alten Version geändert hat; Dateien ohne alte
/// Fassung; Mod-Dateien, deren Vanilla-Datei es nicht mehr gibt; alle gespeicherten Versionen unter der aktuellen (Auswahl im Startfenster).
/// </summary>
public sealed record UpdateScan(IReadOnlyList<UpdateFile> Changed, IReadOnlyList<UpdateFile> WithoutOldVersion, IReadOnlyList<string> RemovedInVanilla, IReadOnlyList<string> RecordedVersionsBelowCurrent);

/// <summary>Findet die Dateien eines Update-Laufs und legt seine Ordner an (Spec 2026-09-30-vanilla-update-merge, Abschnitte 3, 4 und 7.2).</summary>
public static class VanillaUpdateRun
{
    /// <param name="forcedOldVersion">Im Startfenster gewählte Version für alle Dateien; <see langword="null"/> = je Datei automatisch.</param>
    public static UpdateScan Scan(
        IReadOnlyList<OverriddenFile> overridden,
        IEnumerable<string> modRelativePaths,
        string vanillaRoot,
        VanillaHistoryStore store,
        string currentVersion,
        VanillaVersionLedger ledger,
        string? supportedPattern,
        string? forcedOldVersion)
    {
        var comparer = GameVersionComparer.Instance;
        var changed = new List<UpdateFile>();
        var without = new List<UpdateFile>();
        var below = new List<string>();
        foreach (var file in overridden.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var recorded = store.ListRecordedVersions(file.RelativePath);
            below.AddRange(recorded.Where(v => comparer.Compare(v, currentVersion) < 0));
            if (forcedOldVersion is null && ledger.Get(file.RelativePath) is { } updatedTo && comparer.Compare(updatedTo, currentVersion) >= 0)
            {
                // Schon auf die installierte Version angepasst: nichts zu tun.
                continue;
            }

            var old = forcedOldVersion is null
                ? VanillaBaseVersion.Choose(recorded, currentVersion, ledger.Get(file.RelativePath), supportedPattern)
                : recorded.FirstOrDefault(v => comparer.Compare(v, forcedOldVersion) == 0 && comparer.Compare(v, currentVersion) < 0);
            if (old is null)
            {
                without.Add(new UpdateFile(file.RelativePath, file.VanillaFullPath, file.ModFullPath, null, true));
                continue;
            }

            if (!SameBytes(store.SnapshotPath(file.RelativePath, old), file.VanillaFullPath))
            {
                changed.Add(new UpdateFile(file.RelativePath, file.VanillaFullPath, file.ModFullPath, old, true));
            }
        }

        var present = overridden.Select(f => Norm(f.RelativePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removed = modRelativePaths
            .Where(p => !present.Contains(Norm(p)) && !File.Exists(Path.Combine(vanillaRoot, p.Replace('/', Path.DirectorySeparatorChar))))
            .Where(p => store.ListRecordedVersions(p).Count > 0)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var versions = below
            .GroupBy(v => GameVersionComparer.Normalize(v) ?? v, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderByDescending(v => v, comparer)
            .ToList();
        return new UpdateScan(changed, without, removed, versions);
    }

    /// <summary>
    /// Legt <c>old/</c> (Snapshots der alten Fassung) und <c>new/</c> (Kopien der installierten Vanilla-Dateien) unter
    /// <paramref name="runDirectory"/> neu an und baut Eingabe und Arbeitsstand für das Ziel <see cref="PatchTarget.UpdateMod"/>.
    /// </summary>
    public static (CompatchInput Input, CompatchState State) Prepare(
        string runDirectory,
        IReadOnlyList<UpdateFile> files,
        VanillaHistoryStore store,
        string modFolder,
        string modName,
        string? modId,
        string currentVersion,
        OverrideRules rules,
        IReadOnlyCollection<string> keyOperators)
    {
        var oldRoot = Path.Combine(runDirectory, "old");
        var newRoot = Path.Combine(runDirectory, "new");
        foreach (var root in new[] { oldRoot, newRoot })
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            Directory.CreateDirectory(root);
        }

        foreach (var file in files)
        {
            if (file.OldVersion is { } old)
            {
                Copy(store.SnapshotPath(file.RelativePath, old), Path.Combine(oldRoot, Local(file.RelativePath)));
            }

            Copy(file.VanillaFullPath, Path.Combine(newRoot, Local(file.RelativePath)));
        }

        var paths = files.Select(f => f.RelativePath).ToList();
        var input = new CompatchInput
        {
            VanillaFolder = oldRoot,
            ModAFolder = newRoot,
            ModBFolder = modFolder,
            BLoadsAfterA = true,
            Rules = UpdateModRules.From(rules),
            KeyOperators = keyOperators,
            // Alle Dateiarten: auch eine Grafik, die die Mod am Vanilla-Pfad ersetzt, gehört zum Update.
            Filter = new CompatchFileFilter(new Dictionary<string, string>(), Array.Empty<string>(), skipLocked: false, onlyPaths: paths),
            NoBasePaths = files.Where(f => f.OldVersion is null).Select(f => f.RelativePath).ToList(),
            ExcludeOwnPatchFilesOf = MergeSide.B,
        };
        var state = new CompatchState
        {
            ModAFolder = newRoot,
            ModBFolder = modFolder,
            ModAName = "Vanilla " + currentVersion,
            ModBName = modName,
            ModBId = modId,
            BLoadsAfterA = true,
            Target = PatchTarget.UpdateMod,
            TargetFolder = modFolder,
            TargetModId = modId,
            UpdateNewVersion = currentVersion,
            UpdateOldVersions = files.Where(f => f.OldVersion is not null).ToDictionary(f => f.RelativePath, f => f.OldVersion!, StringComparer.OrdinalIgnoreCase),
            UpdatePaths = paths,
        };
        return (input, state);
    }

    /// <summary>
    /// Dateien des Laufs, die nach dem Schreiben auf der neuen Vanilla-Version stehen: ohne offene Stelle und ohne Schreibproblem (gesperrt,
    /// unlesbar …). Nur sie kommen ins <see cref="VanillaVersionLedger"/>; die anderen vergleicht der nächste Lauf wieder mit ihrer alten Fassung.
    /// </summary>
    public static IReadOnlyList<string> UpdatedFiles(IReadOnlyCollection<string> runPaths, IEnumerable<string> openFiles, PreparedWrite prepared)
    {
        var skipped = openFiles.Concat(prepared.Issues.Select(i => i.RelativePath)).Select(Norm).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return runPaths.Where(p => !skipped.Contains(Norm(p))).ToList();
    }

    private static bool SameBytes(string a, string b) =>
        File.Exists(a) && File.Exists(b) && new FileInfo(a).Length == new FileInfo(b).Length && File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));

    private static void Copy(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: true);
    }

    private static string Local(string relativePath) => relativePath.Replace('/', Path.DirectorySeparatorChar);

    private static string Norm(string relativePath) => relativePath.Replace('\\', '/').Trim('/');
}
