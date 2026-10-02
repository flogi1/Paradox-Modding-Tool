using System.Collections.Concurrent;

namespace ParadoxModTool.Patches;

/// <summary>Einstufung einer Trefferdatei; die Reihenfolge ist auch die Sortierung der Liste.</summary>
public enum WatchStatus
{
    /// <summary>Nach dem Anwenden bleibt ein Treffer übrig, oder Vanilla trifft und die Mod bekommt keine Kopie.</summary>
    Uncovered,

    /// <summary>Das Compare und Update Tool würde die Mod-Kopie bei "Merge nach Spiel-Update" anbieten.</summary>
    Outdated,

    /// <summary>Datei nur in der Mod, mit Treffern auch nach dem Anwenden.</summary>
    OwnFile,

    /// <summary>Nach dem Anwenden bleibt kein Treffer übrig.</summary>
    Covered,

    Ignored,
}

/// <summary>
/// Eine Datei mit Treffern. <see cref="ModLines"/> gilt für den Text auf der Platte, <see cref="PlannedLines"/> für den Text nach "Anwenden"
/// (<see cref="PlannedExists"/>: die Mod-Datei gibt es danach, auf der Platte oder als neue Kopie aus Vanilla).
/// </summary>
public sealed record WatchFile(
    string RelativePath,
    bool VanillaExists,
    bool ModExists,
    bool PlannedExists,
    IReadOnlyList<WatchLine> VanillaLines,
    IReadOnlyList<WatchLine> ModLines,
    IReadOnlyList<WatchLine> PlannedLines);

/// <summary>Ergebnis eines Scans; <see cref="Fingerprint"/> ist der Stand des Rezepts, für den er gilt.</summary>
public sealed record WatchScan(string Fingerprint, IReadOnlyList<WatchFile> Files, IReadOnlyList<string> Warnings);

public sealed record WatchHit(WatchFile File, WatchStatus Status, string? IgnoreNote);

public static partial class PatchWatcher
{
    /// <summary>
    /// Stand des Rezepts ohne Ignorierliste und ohne Regelnamen: Ignorieren stuft nur neu ein, ein neuer Regelname ändert kein Ergebnis;
    /// alles andere verlangt einen neuen Scan.
    /// </summary>
    public static string Fingerprint(PatchRecipe recipe)
    {
        var copy = PatchRecipeStore.Clone(recipe);
        copy.Watch.Ignored = new List<IgnoredWatchFile>();
        copy.OnlyRowKey = null;
        foreach (var rule in copy.Rules)
        {
            rule.Name = string.Empty;
        }

        return PatchRecipeStore.Serialize(copy);
    }

    public static bool IsStale(WatchScan scan, PatchRecipe recipe) => scan.Fingerprint != Fingerprint(recipe);

    /// <summary>Offene Treffer (für die Zahl am Reiter).</summary>
    public static bool IsOpen(WatchStatus status) => status is WatchStatus.Uncovered or WatchStatus.Outdated or WatchStatus.OwnFile;

    /// <summary>
    /// Durchsucht Vanilla und Mod nach den Mustern des Rezepts und berechnet dazu einmal die Vorschau (schreibt nichts), um zu sehen, welche Treffer
    /// nach dem Anwenden übrig bleiben.
    /// </summary>
    public static WatchScan Scan(PatchContext context, PatchRecipe recipe, CancellationToken ct = default, IProgress<(int Done, int Total)>? progress = null)
    {
        var fingerprint = Fingerprint(recipe);
        var warnings = new List<string>();
        var matcher = Matcher.For(Patterns(recipe));
        var vanillaFound = Directory.Exists(context.VanillaRoot);
        if (!vanillaFound)
        {
            warnings.Add(PatchTexts.WatchNoVanilla);
        }

        PatchPlan? plan = null;
        try
        {
            plan = PatchRunner.Plan(context, recipe, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            warnings.Add(PatchTexts.WatchPlanFailed(ex.Message));
        }

        var oldMemory = plan?.OldMemory ?? PatchRecipeStore.LoadMemory(context.EffectiveRecipesFolder, recipe.Id);
        var newMemory = plan?.NewMemory ?? oldMemory;

        var hidden = context.HiddenVanillaFolders ?? Array.Empty<string>();
        var allVanilla = vanillaFound ? GlobMatcher.Enumerate(context.VanillaRoot, "**/*.txt").ToList() : new List<string>();
        if (vanillaFound && allVanilla.Count == 0)
        {
            warnings.Add(PatchTexts.WatchVanillaEmpty);
        }

        // Per replace_path ausgeblendete Vanilla-Ordner lädt das Spiel nicht: ihre Treffer sind keine Arbeit für die Mod.
        var vanillaFiles = allVanilla.Where(IsScannedFile).Where(rel => !ParadoxModTool.Mods.ReplacePaths.Hides(hidden, rel, context.ReplacePathRoots)).ToList();
        var modFiles = GlobMatcher.Enumerate(context.ModRoot, "**/*.txt").Where(IsScannedFile).ToList();
        var jobs = vanillaFiles.Select(rel => (Rel: rel, Vanilla: true)).Concat(modFiles.Select(rel => (Rel: rel, Vanilla: false))).ToList();
        var done = 0;
        var skipped = new ConcurrentBag<string>();
        var found = jobs.AsParallel().WithCancellation(ct).Select(job =>
        {
            var root = job.Vanilla ? context.VanillaRoot : context.ModRoot;
            var text = TryRead(FullPath(root, job.Rel), job.Rel, skipped) ?? string.Empty;
            var lines = job.Vanilla
                ? FindLines(text, matcher, null, Array.Empty<string>())
                : FindLines(text, matcher, recipe.Id, OwnEntries(oldMemory, job.Rel));
            var count = Interlocked.Increment(ref done);
            if (count % 50 == 0 || count == jobs.Count)
            {
                progress?.Report((count, jobs.Count));
            }

            return (job.Rel, job.Vanilla, Lines: lines);
        }).Where(r => r.Lines.Count > 0).ToList();

        var vanillaHits = found.Where(f => f.Vanilla).ToDictionary(f => f.Rel, f => f.Lines, StringComparer.OrdinalIgnoreCase);
        var modHits = found.Where(f => !f.Vanilla).ToDictionary(f => f.Rel, f => f.Lines, StringComparer.OrdinalIgnoreCase);
        var planned = (plan?.Files ?? Array.Empty<PlannedFile>()).ToDictionary(f => NormalizePath(f.RelativePath), StringComparer.OrdinalIgnoreCase);
        var files = new List<WatchFile>();
        foreach (var rel in vanillaHits.Keys.Concat(modHits.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            var modPath = FullPath(context.ModRoot, rel);
            var modExists = File.Exists(modPath);
            // Konflikt-Dateien schreibt "Anwenden" nicht: für sie gilt der Stand auf der Platte, sonst stünden sie fälschlich als "abgedeckt" da.
            string? plannedText = planned.TryGetValue(rel, out var plannedFile) && plannedFile.Status != PlannedFileStatus.Conflict
                ? plannedFile.NewText
                : modExists ? TryRead(modPath, rel, skipped) : null;
            files.Add(new WatchFile(
                rel,
                vanillaFound && File.Exists(FullPath(context.VanillaRoot, rel)),
                modExists,
                plannedText is not null,
                vanillaHits.GetValueOrDefault(rel) ?? Array.Empty<WatchLine>(),
                modHits.GetValueOrDefault(rel) ?? Array.Empty<WatchLine>(),
                plannedText is null ? Array.Empty<WatchLine>() : FindLines(plannedText, matcher, recipe.Id, OwnEntries(newMemory, rel))));
        }

        warnings.AddRange(skipped.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        return new WatchScan(fingerprint, files, warnings);
    }

    /// <summary>Eine gesperrte oder inzwischen verschwundene Datei überspringt nur sich selbst, mit Warnung, statt den ganzen Scan abzubrechen.</summary>
    private static string? TryRead(string path, string rel, ConcurrentBag<string> skipped)
    {
        try
        {
            return TextFile.Read(path).Text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            skipped.Add(PatchTexts.WatchFileSkipped(rel, ex.Message));
            return null;
        }
    }

    /// <summary>Stuft die Dateien eines Scans ein; ohne Dateizugriff, damit Ignorieren sofort wirkt.</summary>
    public static IReadOnlyList<WatchHit> Classify(WatchScan scan, PatchRecipe recipe, IReadOnlySet<string> outdated)
    {
        // Einträge ohne * nennen eine Datei und gehen vor; Ordner-Muster (mit *) gelten danach, der erste Treffer in Listenreihenfolge zählt.
        var ignored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var folders = new List<IgnoredWatchFile>();
        foreach (var entry in recipe.Watch.Ignored)
        {
            if (entry.Path.Contains('*'))
            {
                folders.Add(entry);
            }
            else
            {
                ignored[NormalizePath(entry.Path)] = entry.Note;
            }
        }

        var outdatedPaths = outdated.Select(NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return scan.Files
            .Select(file =>
            {
                var path = NormalizePath(file.RelativePath);
                if (ignored.TryGetValue(path, out var note))
                {
                    return new WatchHit(file, WatchStatus.Ignored, note);
                }

                var folder = folders.FirstOrDefault(entry => RecipeEditing.IsIgnoredBy(entry, file.RelativePath));
                if (folder is not null)
                {
                    return new WatchHit(file, WatchStatus.Ignored, folder.Note);
                }

                var status = !file.VanillaExists ? (file.PlannedLines.Count > 0 ? WatchStatus.OwnFile : WatchStatus.Covered)
                    : file.ModExists && outdatedPaths.Contains(path) ? WatchStatus.Outdated
                    : file.PlannedExists && file.PlannedLines.Count == 0 ? WatchStatus.Covered
                    : WatchStatus.Uncovered;
                return new WatchHit(file, status, null);
            })
            .OrderBy(hit => hit.Status)
            .ThenBy(hit => hit.File.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyCollection<string> OwnEntries(PatchMemory memory, string rel) =>
        memory.ListEntries.Values.SelectMany(list => list)
            .Where(record => string.Equals(NormalizePath(record.File), NormalizePath(rel), StringComparison.OrdinalIgnoreCase))
            .Select(record => record.Entry)
            .ToList();

    private static string NormalizePath(string path) => path.Replace('\\', '/').Trim('/');

    private static string FullPath(string root, string rel) => Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
}
