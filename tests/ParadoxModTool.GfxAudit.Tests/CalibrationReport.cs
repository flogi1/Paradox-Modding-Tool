using System.Diagnostics;
using System.Text;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.GfxAudit.Tests;

/// <summary>Ergebnis eines Kalibrierlaufs „Vanilla als Mod“ der GFX-Prüfung.</summary>
/// <param name="Counts">Anzahl der gewerteten Funde je Art und Schwere.</param>
/// <param name="Findings">Die gewerteten Funde: alle außer denen unter den DLC-Ordnern (siehe <see cref="CalibrationReport.IsDlcPath"/>).</param>
/// <param name="FileCount">Gelesene Textdateien aller Wurzeln.</param>
/// <param name="ImageCount">Bilder der gewählten Mod (= der Installation) außerhalb der DLC-Ordner.</param>
/// <param name="ExcludedFindings">Funde unter den DLC-Ordnern, die nicht gewertet werden.</param>
/// <param name="ExcludedImages">Bilder der gewählten Mod unter den DLC-Ordnern.</param>
/// <param name="BuildSeconds">Dauer des Aufbaus ohne Speicher.</param>
/// <param name="RefreshSeconds">Dauer des Refresh mit leerem Speicher (liest alles und speichert).</param>
/// <param name="WarmRefreshSeconds">Dauer eines zweiten Refresh mit gefülltem Speicher (nichts geändert).</param>
/// <param name="ReportPath">Pfad des geschriebenen Berichts <c>&lt;id&gt;-report.txt</c>.</param>
internal sealed record CalibrationResult(
    IReadOnlyDictionary<(GfxFindingKind Kind, FindingSeverity Severity), int> Counts,
    IReadOnlyList<GfxFinding> Findings,
    int FileCount,
    int ImageCount,
    int ExcludedFindings,
    int ExcludedImages,
    double BuildSeconds,
    double RefreshSeconds,
    double WarmRefreshSeconds,
    string ReportPath)
{
    /// <summary>Die Schlüssel aller Error-Funde, eindeutig und ordinal sortiert.</summary>
    public IReadOnlyList<string> ErrorKeys { get; } = Findings
        .Where(f => f.Severity == FindingSeverity.Error)
        .Select(f => f.Key)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(k => k, StringComparer.Ordinal)
        .ToList();

    /// <summary>Anzahl der gewerteten UnusedImage-Funde.</summary>
    public int UnusedImages => Findings.Count(f => f.Kind == GfxFindingKind.UnusedImage);

    /// <summary>Anteil der UnusedImage-Funde an <see cref="ImageCount"/> (0 ohne Bilder).</summary>
    public double UnusedImageShare => ImageCount == 0 ? 0 : (double)UnusedImages / ImageCount;
}

/// <summary>
/// Kalibrierlauf der GFX-Prüfung: die Installation als gewählte Mod, ihre DLC-Unterordner (<c>dlc/*</c>, <c>integrated_dlc/*</c>, <c>builtin_dlc/*</c>) als Vanilla-Wurzeln,
/// damit DLC-Bilder und -Sprites als vorhanden zählen, dazu die Engine-Ordner neben der Installation (<see cref="EngineRoots"/>). Bericht als Textdatei; dazu das Lesen und
/// Vergleichen der Restlisten.
/// </summary>
/// <remarks>
/// Die Installation enthält die DLC-Ordner selbst noch einmal (die Dateisuche geht in alle Unterordner), dort mit Pfaden wie
/// <c>dlc/x/gfx/a.dds</c>. Eine echte Mod hat solche Ordner nicht; ihre Funde werden deshalb nicht gewertet, nur gezählt.
/// </remarks>
internal static class CalibrationReport
{
    private const int SamplesPerKind = 20;
    private const int TopGroups = 30;

    /// <summary>Ordner der Installation, deren Unterordner DLC-Wurzeln sind (EU IV: auch <c>builtin_dlc</c>).</summary>
    private static readonly string[] DlcFolders = { "dlc", "integrated_dlc", "builtin_dlc" };

    /// <summary>Baut, aktualisiert (frischer Speicher in <paramref name="outputDir"/>) und wertet aus; schreibt <c>&lt;outputDir&gt;/&lt;id&gt;-report.txt</c>,
    /// <c>&lt;id&gt;-unused.txt</c> (alle ungenutzten Namen) und <c>&lt;id&gt;-errors.candidate.txt</c> (Restlisten-Vorlage).</summary>
    public static CalibrationResult Run(string gameId, string installRoot, string outputDir)
    {
        // Vollständiger Pfad: der Analyzer vergleicht Wurzeln als Text.
        var root = Path.GetFullPath(installRoot);
        var profile = RepoFiles.Profile(gameId);
        var rules = GfxTestRoots.Rules(gameId);
        var engineRoots = EngineRoots(root);
        var vanillaRoots = engineRoots.Concat(DlcRoots(root)).ToList();
        var sources = FlagSourceSet.ForPlayset(profile, Array.Empty<ModSource>(), root, vanillaRoots);

        Directory.CreateDirectory(outputDir);
        var store = new GfxAuditStore(Path.Combine(outputDir, gameId + "-store"));
        var storeFile = store.FileFor(sources.ScopeKey);
        if (File.Exists(storeFile))
        {
            File.Delete(storeFile);
        }

        var watch = Stopwatch.StartNew();
        new GfxAuditBuilder(sources, rules).Build();
        var buildSeconds = watch.Elapsed.TotalSeconds;

        watch.Restart();
        var index = new GfxAuditBuilder(sources, rules).Refresh(store);
        var refreshSeconds = watch.Elapsed.TotalSeconds;

        watch.Restart();
        index = new GfxAuditBuilder(sources, rules).Refresh(store);
        var warmSeconds = watch.Elapsed.TotalSeconds;

        watch.Restart();
        var analysis = GfxAuditAnalyzer.Analyze(index, rules);
        var analyzeSeconds = watch.Elapsed.TotalSeconds;

        var modImages = index.Images.Where(i => i.Origin == FlagOrigin.ActiveMod).ToList();
        var imageCount = modImages.Count(i => !IsDlcPath(i.File.RelativePath));
        var findings = analysis.Findings.Where(f => !IsDlcPath(f.Location.RelativePath)).ToList();
        var excluded = analysis.Findings.Count - findings.Count;
        var counts = findings.GroupBy(f => (f.Kind, f.Severity)).ToDictionary(g => g.Key, g => g.Count());
        var reportPath = Path.Combine(outputDir, gameId + "-report.txt");
        var result = new CalibrationResult(
            counts, findings, index.Files.Count, imageCount, excluded, modImages.Count - imageCount, buildSeconds, refreshSeconds, warmSeconds, reportPath);

        var text = new StringBuilder();
        text.AppendLine($"Spiel: {gameId}");
        text.AppendLine($"Installation: {root}");
        text.AppendLine($"Engine-Wurzeln (Vanilla): {string.Join(", ", engineRoots)}");
        text.AppendLine($"DLC-Wurzeln (Vanilla): {vanillaRoots.Count - engineRoots.Count}");
        text.AppendLine($"Textdateien: {result.FileCount}, ungelesen: {index.UnreadFiles.Count}");
        text.AppendLine($"Bilder der Installation: {imageCount} (dazu {result.ExcludedImages} unter dlc/, integrated_dlc/, builtin_dlc/), Bilder aller Wurzeln: {index.Images.Count}");
        text.AppendLine($"Erwähnungen: {index.Mentions.Count}");
        text.AppendLine($"Aufbau: {buildSeconds:F1} s, Refresh (leerer Speicher): {refreshSeconds:F1} s, Refresh (gefüllter Speicher): {warmSeconds:F1} s, Auswertung: {analyzeSeconds:F1} s");
        text.AppendLine($"UnusedImage-Anteil: {result.UnusedImages} / {imageCount} = {result.UnusedImageShare:P1}");
        text.AppendLine($"Nicht gewertete Funde unter dlc/, integrated_dlc/, builtin_dlc/: {excluded}");
        foreach (var group in analysis.Findings.Where(f => IsDlcPath(f.Location.RelativePath)).GroupBy(f => f.Kind).OrderBy(g => g.Key))
        {
            text.AppendLine($"  {group.Key}: {group.Count()}");
        }

        text.AppendLine();
        text.AppendLine("== Anzahl je Art und Schwere ==");
        foreach (var count in counts.OrderBy(c => c.Key.Severity).ThenBy(c => c.Key.Kind))
        {
            text.AppendLine($"{count.Key.Kind}\t{count.Key.Severity}\t{count.Value}");
        }

        foreach (var kind in new[] { GfxFindingKind.UnusedImage, GfxFindingKind.UnusedSprite })
        {
            var names = findings.Where(f => f.Kind == kind).ToList();
            if (names.Count == 0)
            {
                continue;
            }

            AppendTop(text, $"{kind}: oberste Ordner", names.Select(f => TopFolder(f.Location.RelativePath)));
            AppendTop(text, $"{kind}: Ordner", names.Select(f => Folder(f.Location.RelativePath)));
            AppendTop(text, $"{kind}: letzte Namensteile _xxx", names.Select(f => LastPart(Stem(f))));
            AppendTop(text, $"{kind}: erste Namensteile xxx_", names.Select(f => FirstPart(Stem(f))));
        }

        var errors = findings.Where(f => f.Severity == FindingSeverity.Error).ToList();
        text.AppendLine();
        text.AppendLine($"== Errors ({errors.Count}, {result.ErrorKeys.Count} Schlüssel) ==");
        foreach (var finding in errors.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            text.AppendLine($"{finding.Key}\t{Where(finding.Location)}\t{string.Join("; ", finding.Related.Take(3))}");
        }

        foreach (var group in findings.Where(f => f.Severity != FindingSeverity.Error).GroupBy(f => (f.Severity, f.Kind)).OrderBy(g => g.Key))
        {
            text.AppendLine();
            text.AppendLine($"== {group.Key.Severity} {group.Key.Kind} ({group.Count()}, die ersten {SamplesPerKind}) ==");
            foreach (var finding in group.Take(SamplesPerKind))
            {
                text.AppendLine($"{finding.Key}\t{Where(finding.Location)}\t{string.Join("; ", finding.Related.Take(3))}");
            }
        }

        File.WriteAllText(reportPath, text.ToString(), new UTF8Encoding(false));

        var unused = findings.Where(f => f.Kind is GfxFindingKind.UnusedImage or GfxFindingKind.UnusedSprite)
            .Select(f => $"{f.Kind}\t{f.Name}\t{Where(f.Location)}\t{string.Join("; ", f.Related)}");
        File.WriteAllLines(Path.Combine(outputDir, gameId + "-unused.txt"), unused, new UTF8Encoding(false));

        var candidate = errors
            .GroupBy(f => f.Key, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}\t{Reason(g.First())} ({Where(g.First().Location)}{(g.Count() > 1 ? $", {g.Count()} Stellen" : string.Empty)})");
        File.WriteAllLines(Path.Combine(outputDir, gameId + "-errors.candidate.txt"), candidate, new UTF8Encoding(false));
        return result;
    }

    /// <summary>
    /// Die Engine-Ordner neben der Installation (<c>&lt;install&gt;/../jomini</c>, <c>&lt;install&gt;/../clausewitz</c>; CK III, Vic3, EU V), soweit vorhanden. Die App nimmt sie
    /// ebenso als Vanilla-Wurzeln (<c>SourceResolver.VanillaRootsFor</c>: <c>game</c>, <c>jomini</c>, <c>clausewitz</c>).
    /// </summary>
    public static IReadOnlyList<string> EngineRoots(string installRoot)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return parent is null
            ? Array.Empty<string>()
            : new[] { "jomini", "clausewitz" }.Select(d => Path.Combine(parent, d)).Where(Directory.Exists).ToList();
    }

    /// <summary>Die vorhandenen Ordner unter <c>dlc/</c>, <c>integrated_dlc/</c> und <c>builtin_dlc/</c> (Dateien wie <c>.zip</c> zählen nicht), je Ordner ordinal sortiert.</summary>
    public static IReadOnlyList<string> DlcRoots(string installRoot) =>
        DlcFolders
            .Select(d => Path.Combine(installRoot, d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetDirectories(d).OrderBy(x => x, StringComparer.Ordinal))
            .ToList();

    /// <summary>Liegt der relative Pfad (der Installation) unter <c>dlc/</c>, <c>integrated_dlc/</c> oder <c>builtin_dlc/</c>?</summary>
    public static bool IsDlcPath(string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        return DlcFolders.Any(d => path.StartsWith(d + "/", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Restliste lesen: Zeilen <c>schlüssel&lt;TAB&gt;grund</c> (Grund darf fehlen); Leerzeilen und <c>#</c>-Kommentare zählen nicht.</summary>
    public static IReadOnlyDictionary<string, string> ReadResidual(string path)
    {
        var residual = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            var line = raw.TrimEnd('\r');
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var tab = line.IndexOf('\t');
            var key = (tab < 0 ? line : line[..tab]).Trim();
            residual[key] = tab < 0 ? string.Empty : line[(tab + 1)..].Trim();
        }

        return residual;
    }

    /// <summary>
    /// Vergleich mit der Restliste: <c>Unlisted</c> = Error-Schlüssel, die nicht in der Liste stehen (lassen den Test scheitern);
    /// <c>Vanished</c> = Listeneinträge, die nicht mehr auftreten (nur Ausgabe). Beide ordinal sortiert.
    /// </summary>
    public static (IReadOnlyList<string> Unlisted, IReadOnlyList<string> Vanished) Compare(IEnumerable<string> errorKeys, IReadOnlyDictionary<string, string> residual)
    {
        var keys = errorKeys.ToHashSet(StringComparer.Ordinal);
        var unlisted = keys.Where(k => !residual.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var vanished = residual.Keys.Where(k => !keys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        return (unlisted, vanished);
    }

    private static string Reason(GfxFinding finding) => finding.Kind switch
    {
        GfxFindingKind.MissingTexture when finding.Related.Count > 0 => "Vanilla: Textur des Sprites " + finding.Related[0] + " fehlt in der Installation",
        GfxFindingKind.MissingTexture => "Vanilla: Textur fehlt in der Installation",
        GfxFindingKind.MissingSprite => "Vanilla: Sprite nirgends definiert",
        _ => "Vanilla",
    };

    private static void AppendTop(StringBuilder text, string title, IEnumerable<string> values)
    {
        text.AppendLine();
        text.AppendLine($"== {title} (die {TopGroups} häufigsten; Anzahl, Wert) ==");
        foreach (var group in values.GroupBy(v => v, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(TopGroups))
        {
            text.AppendLine($"{group.Count()}\t{group.Key}");
        }
    }

    /// <summary>Name ohne Ordner und Bildendung (Bild) bzw. der Sprite-Name.</summary>
    private static string Stem(GfxFinding finding) =>
        finding.Kind == GfxFindingKind.UnusedImage ? GfxNames.Stem(finding.Name) : finding.Name;

    private static string TopFolder(string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        var parts = path.Split('/');
        return parts.Length <= 1 ? "(Stamm)" : parts.Length == 2 ? parts[0] : parts[0] + "/" + parts[1];
    }

    private static string Folder(string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        var slash = path.LastIndexOf('/');
        return slash < 0 ? "(Stamm)" : path[..slash];
    }

    private static string LastPart(string stem)
    {
        var underscore = stem.LastIndexOf('_');
        return underscore <= 0 ? "(kein _)" : stem[underscore..].ToLowerInvariant();
    }

    private static string FirstPart(string stem)
    {
        var underscore = stem.IndexOf('_');
        return underscore <= 0 ? "(kein _)" : stem[..(underscore + 1)].ToLowerInvariant();
    }

    private static string Where(SiteLocation location) => $"{location.RelativePath}:{location.Line}";
}
