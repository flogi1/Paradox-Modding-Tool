using System.Diagnostics;
using System.Text;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptDocs;

namespace ParadoxModTool.ScriptCheck.Tests;

/// <summary>Ergebnis eines Kalibrierlaufs „Vanilla als Mod“.</summary>
/// <param name="Counts">Anzahl der Funde je Art und Schwere.</param>
/// <param name="Findings">Alle Funde, wie der Analyzer sie liefert.</param>
/// <param name="BlockReferencesChecked">false ohne Script-Docs: kaputte Effekt-/Trigger-Aufrufe wurden nicht geprüft.</param>
/// <param name="FileCount">Gelesene Dateien.</param>
/// <param name="HasModRoot">true, wenn Vanilla als gewählte Mod erkannt wurde.</param>
/// <param name="BuildSeconds">Dauer des Aufbaus ohne Speicher.</param>
/// <param name="RefreshSeconds">Dauer des Refresh mit leerem Speicher.</param>
/// <param name="ReportPath">Pfad des geschriebenen Berichts <c>&lt;id&gt;-report.txt</c>.</param>
internal sealed record CalibrationResult(
    IReadOnlyDictionary<(FindingKind Kind, FindingSeverity Severity), int> Counts,
    IReadOnlyList<Finding> Findings,
    bool BlockReferencesChecked,
    int FileCount,
    bool HasModRoot,
    double BuildSeconds,
    double RefreshSeconds,
    string ReportPath,
    IReadOnlyDictionary<(FindingKind Kind, CheckKind NameKind), int> LocMentions)
{
    /// <summary>Die Schlüssel aller Error-Funde, eindeutig und ordinal sortiert.</summary>
    public IReadOnlyList<string> ErrorKeys { get; } = Findings
        .Where(f => f.Severity == FindingSeverity.Error)
        .Select(f => f.Key)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(k => k, StringComparer.Ordinal)
        .ToList();
}

/// <summary>
/// Kalibrierlauf der Skriptprüfung: ein Vanilla-Ordner als gewählte Mod (ohne Vanilla-Wurzeln, also zählt jede Datei als Mod-Datei),
/// eingebaute Namen aus den Script-Docs, Bericht als Textdatei; dazu das Lesen und Vergleichen der Restlisten.
/// </summary>
internal static class CalibrationReport
{
    private const int SamplesPerKind = 20;
    private const int TopBlockTargets = 100;
    private const int TopTemplates = 15;

    /// <summary>Baut, aktualisiert (frischer Speicher in <paramref name="outputDir"/>) und wertet aus; schreibt <c>&lt;outputDir&gt;/&lt;id&gt;-report.txt</c>.</summary>
    /// <param name="scriptDocsDir">Ordner der Script-Docs; fehlt er (oder ist null), gibt es keine eingebauten Namen.</param>
    public static CalibrationResult Run(string gameId, string vanillaRoot, string? scriptDocsDir, string outputDir)
    {
        // Vollständiger Pfad: der Analyzer vergleicht Wurzeln als Text.
        var root = Path.GetFullPath(vanillaRoot);
        var profile = RepoFiles.Profile(gameId);
        var rules = ScriptCheckRules.ForProfile(profile, ScriptCheckTestRoots.ProfilesDir)
            ?? throw new InvalidOperationException("Keine Regeln für " + gameId);
        var sources = FlagSourceSet.ForPlayset(profile, Array.Empty<ModSource>(), root, Array.Empty<string>());
        var builtIns = LoadBuiltIns(scriptDocsDir);

        Directory.CreateDirectory(outputDir);
        var store = new ScriptCheckStore(Path.Combine(outputDir, gameId + "-store"));
        var storeFile = store.FileFor(sources.ScopeKey);
        if (File.Exists(storeFile))
        {
            File.Delete(storeFile);
        }

        var watch = Stopwatch.StartNew();
        new ScriptCheckBuilder(sources, rules).Build();
        var buildSeconds = watch.Elapsed.TotalSeconds;

        watch.Restart();
        var index = new ScriptCheckBuilder(sources, rules).Refresh(store);
        var refreshSeconds = watch.Elapsed.TotalSeconds;

        watch.Restart();
        var analysis = ScriptCheckAnalyzer.Analyze(index, builtIns, rules);
        var analyzeSeconds = watch.Elapsed.TotalSeconds;

        var counts = analysis.Findings
            .GroupBy(f => (f.Kind, f.Severity))
            .ToDictionary(g => g.Key, g => g.Count());
        var loc = FindLocMentions(root, analysis.Findings);
        var locCounts = loc.GroupBy(m => (m.Finding.Kind, m.Finding.NameKind)).ToDictionary(g => g.Key, g => g.Count());
        var reportPath = Path.Combine(outputDir, gameId + "-report.txt");
        var result = new CalibrationResult(counts, analysis.Findings, analysis.BlockReferencesChecked, index.FileCount, index.ModRoot is not null, buildSeconds, refreshSeconds, reportPath, locCounts);

        var text = new StringBuilder();
        text.AppendLine($"Spiel: {gameId}");
        text.AppendLine($"Vanilla: {root}");
        text.AppendLine(builtIns.Count > 0 ? $"Script-Docs: {scriptDocsDir} ({builtIns.Count} eingebaute Namen)" : $"Script-Docs: keine ({scriptDocsDir ?? "-"})");
        text.AppendLine($"Dateien: {index.FileCount}, ungelesen: {index.UnreadFiles.Count}");
        text.AppendLine($"Aufbau: {buildSeconds:F1} s, Refresh (leerer Speicher): {refreshSeconds:F1} s, Auswertung: {analyzeSeconds:F1} s");
        text.AppendLine($"BlockReferencesChecked: {analysis.BlockReferencesChecked}");
        text.AppendLine($"Blockaufrufe: {index.ModBlockCalls.Count}");
        text.AppendLine();
        text.AppendLine("== Anzahl je Art und Schwere ==");
        foreach (var count in counts.OrderBy(c => c.Key.Severity).ThenBy(c => c.Key.Kind))
        {
            text.AppendLine($"{count.Key.Kind}\t{count.Key.Severity}\t{count.Value}");
        }

        var errors = analysis.Findings.Where(f => f.Severity == FindingSeverity.Error).ToList();
        text.AppendLine();
        text.AppendLine($"== Errors ({errors.Count}, {result.ErrorKeys.Count} Schlüssel) ==");
        foreach (var finding in errors.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            text.AppendLine($"{finding.Key}\t{Where(finding.Location)}\t{string.Join("; ", finding.Related.Take(3))}");
        }

        foreach (var group in analysis.Findings.Where(f => f.Severity != FindingSeverity.Error).GroupBy(f => (f.Severity, f.Kind)).OrderBy(g => g.Key))
        {
            text.AppendLine();
            text.AppendLine($"== {group.Key.Severity} {group.Key.Kind} ({group.Count()}, die ersten {SamplesPerKind}) ==");
            foreach (var finding in group.Take(SamplesPerKind))
            {
                text.AppendLine($"{finding.Key}\t{Where(finding.Location)}\t{string.Join("; ", finding.Related.Take(3))}");
            }
        }

        var defined = index.Files
            .SelectMany(f => f.Scan.Definitions)
            .Where(d => d.Kind is CheckKind.Effect or CheckKind.Trigger or CheckKind.Named)
            .Select(d => d.Name)
            .ToHashSet(StringComparer.Ordinal);
        var extra = rules.ExtraBuiltins.ToHashSet(StringComparer.Ordinal);
        text.AppendLine();
        text.AppendLine($"== Blockaufrufe ohne Definition (die {TopBlockTargets} häufigsten; Anzahl, Name, eingebaut) ==");
        foreach (var target in index.ModBlockCalls
            .Where(c => !defined.Contains(c.Ref.Target))
            .GroupBy(c => c.Ref.Target, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(TopBlockTargets))
        {
            var known = builtIns.Contains(target.Key) ? "Script-Docs" : extra.Contains(target.Key) ? "extraBuiltins" : "nein";
            text.AppendLine($"{target.Count()}\t{target.Key}\t{known}");
        }

        var blockNames = index.Files.SelectMany(f => f.Scan.Definitions)
            .Where(d => d.Kind is CheckKind.Effect or CheckKind.Trigger or CheckKind.Value)
            .Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
        var coverage = new TemplateMatcher(index.Files.SelectMany(f => f.Scan.Templates)).Coverage(blockNames);
        text.AppendLine();
        text.AppendLine($"== Vorlagen mit Treffer ({coverage.Count}, die {TopTemplates} breitesten; Anzahl passender Definitionen, Vorlage) ==");
        foreach (var (token, matched) in coverage.Take(TopTemplates))
        {
            text.AppendLine($"{matched}\t{token}");
        }

        foreach (var group in loc.GroupBy(m => (m.Finding.Kind, m.Finding.NameKind)).OrderBy(g => g.Key.Kind).ThenBy(g => g.Key.NameKind))
        {
            text.AppendLine();
            text.AppendLine($"== Unused but in localisation: {group.Key.Kind} {group.Key.NameKind} ({group.Count()}, die ersten {SamplesPerKind}) ==");
            foreach (var mention in group.Take(SamplesPerKind))
            {
                text.AppendLine($"{mention.Finding.Name}	{mention.File}:{mention.Line}");
            }
        }

        AppendPerformance(text, analysis.Findings);
        File.WriteAllText(reportPath, text.ToString(), new UTF8Encoding(false));
        WriteFindingLists(outputDir, gameId, analysis.Findings);
        return result;
    }

    /// <summary>
    /// Abschnitt „Leistung“: Funde <see cref="FindingKind.ExpensiveIterator"/> je Schwere und Takt, Anzahl verschachtelter, die ersten
    /// <see cref="SamplesPerKind"/> mit vollem Weg und die <see cref="TopTemplates"/> häufigsten Iteratoren.
    /// </summary>
    internal static void AppendPerformance(StringBuilder text, IReadOnlyList<Finding> findings)
    {
        var performance = findings.Where(f => f.Kind == FindingKind.ExpensiveIterator && f.Performance is not null).ToList();
        text.AppendLine();
        text.AppendLine($"== Leistung ({performance.Count}, verschachtelt {performance.Count(f => f.Performance!.Inner is not null)}) ==");
        foreach (var group in performance.GroupBy(f => (f.Severity, f.Performance!.Rate)).OrderBy(g => g.Key.Severity).ThenBy(g => g.Key.Rate))
        {
            text.AppendLine($"{group.Key.Severity}\t{group.Key.Rate}\t{group.Count()}");
        }

        text.AppendLine();
        text.AppendLine($"== Leistung: die ersten {SamplesPerKind} (Schwere, Takt, Iterator, Ort, innerer Iterator, Weg) ==");
        foreach (var finding in performance.Take(SamplesPerKind))
        {
            text.AppendLine(PerformanceLine(finding));
        }

        text.AppendLine();
        text.AppendLine($"== Leistung: die {TopTemplates} häufigsten Iteratoren (Anzahl, Iterator) ==");
        foreach (var group in performance.GroupBy(f => f.Name, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(TopTemplates))
        {
            text.AppendLine($"{group.Count()}\t{group.Key}");
        }
    }

    /// <summary>
    /// Zwei Listen neben dem Bericht: <c>&lt;id&gt;-performance.txt</c> (alle Leistungsfunde mit Weg, zum Beurteilen) und
    /// <c>&lt;id&gt;-other-findings.txt</c> (alle übrigen Funde als Art, Schwere, Schlüssel, Ort; sortiert, zum Vergleich vorher/nachher).
    /// </summary>
    private static void WriteFindingLists(string outputDir, string gameId, IReadOnlyList<Finding> findings)
    {
        var performance = findings.Where(f => f.Kind == FindingKind.ExpensiveIterator && f.Performance is not null).Select(PerformanceLine);
        File.WriteAllLines(Path.Combine(outputDir, gameId + "-performance.txt"), performance, new UTF8Encoding(false));
        var other = findings
            .Where(f => f.Kind != FindingKind.ExpensiveIterator)
            .Select(f => $"{f.Kind}\t{f.Severity}\t{f.Key}\t{Where(f.Location)}")
            .OrderBy(l => l, StringComparer.Ordinal);
        File.WriteAllLines(Path.Combine(outputDir, gameId + "-other-findings.txt"), other, new UTF8Encoding(false));
    }

    private static string PerformanceLine(Finding f) =>
        $"{f.Severity}\t{f.Performance!.Rate}\t{f.Name}\t{Where(f.Location)}\t{f.Performance.Inner ?? "-"}\t{string.Join(" → ", f.Performance.Path)}";

    /// <summary>Regeln des Spiels und der Index „Vanilla als Mod“ (ohne Speicher), z. B. für die Datenprüfung der Leistungsregeln.</summary>
    public static (ScriptCheckRules Rules, ScriptCheckIndex Index) BuildVanilla(string gameId, string vanillaRoot)
    {
        var root = Path.GetFullPath(vanillaRoot);
        var profile = RepoFiles.Profile(gameId);
        var rules = ScriptCheckRules.ForProfile(profile, ScriptCheckTestRoots.ProfilesDir)
            ?? throw new InvalidOperationException("Keine Regeln für " + gameId);
        var sources = FlagSourceSet.ForPlayset(profile, Array.Empty<ModSource>(), root, Array.Empty<string>());
        return (rules, new ScriptCheckBuilder(sources, rules).Build());
    }

    /// <summary>Eingebaute Namen: <see cref="ScriptCheckBuiltIns.FromDatabase"/>; ohne Ordner leer.</summary>
    public static HashSet<string> LoadBuiltIns(string? scriptDocsDir)
    {
        return string.IsNullOrEmpty(scriptDocsDir) || !Directory.Exists(scriptDocsDir)
            ? new HashSet<string>(StringComparer.Ordinal)
            : ScriptCheckBuiltIns.FromDatabase(new ScriptDocStorage(scriptDocsDir).Load());
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

    /// <summary>Ein Fund (ungenutzter Block oder totes Event), dessen Name in englischer Lokalisierung vorkommt: erste Fundstelle.</summary>
    internal sealed record LocMention(Finding Finding, string File, int Line);

    /// <summary>
    /// Misst nur: welche UnusedBlock- und DeadEvent-Funde erscheinen als Wort (<see cref="MentionWords.Of"/>) in einer englischen <c>.yml</c>
    /// unter <paramref name="root"/> (Ordner <c>localization</c>/<c>localisation</c>, Pfad mit <c>english</c> oder Name <c>*_l_english.yml</c>)?
    /// </summary>
    internal static IReadOnlyList<LocMention> FindLocMentions(string root, IEnumerable<Finding> findings)
    {
        var wanted = findings
            .Where(f => f.Kind is FindingKind.UnusedBlock or FindingKind.DeadEvent)
            .ToList();
        var result = new List<LocMention>();
        if (wanted.Count == 0 || !Directory.Exists(root))
        {
            return result;
        }

        var byName = wanted.ToLookup(f => f.Name, StringComparer.Ordinal);
        var found = new HashSet<Finding>();
        foreach (var path in Directory.EnumerateFiles(root, "*.yml", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
            var lower = rel.ToLowerInvariant();
            var inLoc = lower.Contains("/localization/") || lower.Contains("/localisation/") || lower.StartsWith("localization/") || lower.StartsWith("localisation/");
            if (!inLoc || !(lower.Contains("english") || lower.EndsWith("_l_english.yml")))
            {
                continue;
            }

            var number = 0;
            foreach (var line in File.ReadLines(path))
            {
                number++;
                if (line.TrimStart().StartsWith('#'))
                {
                    continue;
                }

                foreach (var word in MentionWords.Of(line).Distinct(StringComparer.Ordinal))
                {
                    foreach (var finding in byName[word])
                    {
                        if (found.Add(finding))
                        {
                            result.Add(new LocMention(finding, rel, number));
                        }
                    }
                }
            }
        }

        return result;
    }

    private static string Where(SiteLocation location) => $"{location.RelativePath}:{location.Line}";
}
