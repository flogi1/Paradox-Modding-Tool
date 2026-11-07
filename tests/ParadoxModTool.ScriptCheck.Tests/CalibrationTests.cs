using ParadoxModTool.ScriptDocs;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.ScriptCheck.Tests;

/// <summary>
/// Kalibrierung der Skriptprüfung: „Vanilla als Mod“ je Spiel mit den importierten Script-Docs.
/// Die Spieltests laufen nur mit <c>PMT_FLAGS_&lt;SPIEL&gt;_GAME</c>; jeder Error-Schlüssel muss in
/// <c>Data/calibration/&lt;id&gt;-errors.txt</c> stehen. Die Hilfstests laufen immer auf einem kleinen Temp-Vanilla.
/// </summary>
public sealed class CalibrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ScriptCheckTestRoots _roots = new();

    public CalibrationTests(ITestOutputHelper output) => _output = output;

    public void Dispose() => _roots.Dispose();

    [Fact]
    public void Calibration_Ck3() => Calibrate("ck3", "CK3");

    [Fact]
    public void Calibration_Vic3() => Calibrate("vic3", "VIC3");

    [Fact]
    public void Calibration_Eu4() => Calibrate("eu4", "EU4");

    [Fact]
    public void Calibration_Eu5() => Calibrate("eu5", "EU5");

    [Fact]
    public void Calibration_Hoi4() => Calibrate("hoi4", "HOI4");

    [Fact]
    public void Calibration_Stellaris() => Calibrate("stellaris", "STELLARIS");

    [Fact]
    public void PerformanceData_Ck3() => CheckPerformanceData("ck3", "CK3");

    [Fact]
    public void PerformanceData_Vic3() => CheckPerformanceData("vic3", "VIC3");

    [Fact]
    public void PerformanceData_Eu4() => CheckPerformanceData("eu4", "EU4");

    [Fact]
    public void PerformanceData_Eu5() => CheckPerformanceData("eu5", "EU5");

    [Fact]
    public void PerformanceData_Hoi4() => CheckPerformanceData("hoi4", "HOI4");

    [Fact]
    public void PerformanceData_Stellaris() => CheckPerformanceData("stellaris", "STELLARIS");

    [Fact]
    public void PerformanceDataPresent()
    {
        foreach (var id in new[] { "ck3", "vic3", "eu4", "eu5", "hoi4", "stellaris" })
        {
            var performance = ScriptCheckTestRoots.Rules(id).Performance;
            Assert.True(performance is not null, id + ": kein Objekt performance");
            Assert.True(performance.GlobalIterators.Count > 0, id + ": keine globalIterators");
            Assert.True(performance.PulseOnActions.Count > 0, id + ": keine pulseOnActions");
        }
    }

    [Fact]
    public void ReportListsErrorKeys()
    {
        WriteFixture();
        var docs = WriteScriptDocs("add_gold");

        var result = CalibrationReport.Run("ck3", _roots.Vanilla, docs, Path.Combine(_roots.Temp, "out"));

        Assert.True(result.BlockReferencesChecked);
        Assert.Equal(new[] { "BrokenReference|m.9", "BrokenReference|my_unknown_effect" }, result.ErrorKeys);
        Assert.Equal(2, result.Counts[(FindingKind.BrokenReference, FindingSeverity.Error)]);
        Assert.Equal(Path.Combine(_roots.Temp, "out", "ck3-report.txt"), result.ReportPath);
        var report = File.ReadAllText(result.ReportPath);
        Assert.Contains("BrokenReference|m.9\tevents/m.txt:4", report);
        Assert.Contains("BrokenReference|my_unknown_effect\tevents/m.txt:5", report);
        Assert.Contains("my_unknown_effect", report[report.IndexOf("Blockaufrufe", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void ReportListsPerformance()
    {
        ScriptCheckTestRoots.Write(_roots.Vanilla, "common/on_action/p.txt", "yearly_global_pulse = {\n\tevents = { m.1 }\n}");
        ScriptCheckTestRoots.Write(_roots.Vanilla, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = {\n\t\tevery_county = {\n\t\t\tany_living_character = { is_ai = no }\n\t\t}\n\t}\n}");

        var result = CalibrationReport.Run("ck3", _roots.Vanilla, null, Path.Combine(_roots.Temp, "out"));

        var finding = Assert.Single(result.Findings, f => f.Kind == FindingKind.ExpensiveIterator);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        var report = File.ReadAllText(result.ReportPath);
        Assert.Contains("== Leistung (1, verschachtelt 1) ==", report);
        Assert.Contains("Warning\tYearly\t1", report);
        Assert.Contains("Warning\tYearly\tevery_county\tevents/m.txt:4\tany_living_character\tyearly_global_pulse → m.1", report);
        Assert.Contains("1\tevery_county", report[report.IndexOf("häufigsten Iteratoren", StringComparison.Ordinal)..]);
        Assert.Single(File.ReadAllLines(Path.Combine(_roots.Temp, "out", "ck3-performance.txt")));
        Assert.DoesNotContain(File.ReadAllLines(Path.Combine(_roots.Temp, "out", "ck3-other-findings.txt")), l => l.StartsWith("ExpensiveIterator", StringComparison.Ordinal));
    }

    [Fact]
    public void UnlistedErrorFails()
    {
        var residual = new Dictionary<string, string>(StringComparer.Ordinal) { ["BrokenReference|a"] = "Vanilla-Fehler" };

        var (unlisted, vanished) = CalibrationReport.Compare(new[] { "BrokenReference|a", "EffectCycle|b → c" }, residual);

        Assert.Equal(new[] { "EffectCycle|b → c" }, unlisted);
        Assert.Empty(vanished);
    }

    [Fact]
    public void VanishedResidualKeyOnlyReported()
    {
        var path = Path.Combine(_roots.Temp, "x-errors.txt");
        File.WriteAllText(path, "# Kopf\n\nBrokenReference|a\tVanilla-Fehler\nEffectCycle|b → c\tSchleife über zwei Effekte\nBrokenReference|ohne_grund\n");

        var residual = CalibrationReport.ReadResidual(path);
        var (unlisted, vanished) = CalibrationReport.Compare(new[] { "BrokenReference|a" }, residual);

        Assert.Equal(3, residual.Count);
        Assert.Equal("Schleife über zwei Effekte", residual["EffectCycle|b → c"]);
        Assert.Equal(string.Empty, residual["BrokenReference|ohne_grund"]);
        Assert.Empty(unlisted);
        Assert.Equal(new[] { "BrokenReference|ohne_grund", "EffectCycle|b → c" }, vanished);
    }

    [Fact]
    public void MissingScriptDocsTurnsOffBlockReferences()
    {
        WriteFixture();

        var result = CalibrationReport.Run("ck3", _roots.Vanilla, Path.Combine(_roots.Temp, "fehlt"), Path.Combine(_roots.Temp, "out"));

        Assert.False(result.BlockReferencesChecked);
        Assert.Equal(new[] { "BrokenReference|m.9" }, result.ErrorKeys);
        Assert.Contains("BlockReferencesChecked: False", File.ReadAllText(result.ReportPath));
    }

    [Fact]
    public void LocMentionsCounted()
    {
        ScriptCheckTestRoots.Write(_roots.Vanilla, "common/script_values/sv.txt", "my_sv = 1\nother_sv = 2");
        ScriptCheckTestRoots.Write(_roots.Vanilla, "localization/english/a_l_english.yml", "l_english:\n k:0 \"[GetPlayer.MakeScope.ScriptValue('my_sv')] other_sv\"\n");
        ScriptCheckTestRoots.Write(_roots.Vanilla, "localization/german/a_l_german.yml", "l_german:\n k:0 \"my_sv\"\n");

        var result = CalibrationReport.Run("ck3", _roots.Vanilla, null, Path.Combine(_roots.Temp, "out"));

        Assert.Equal(1, result.LocMentions[(FindingKind.UnusedBlock, CheckKind.Value)]);
        var report = File.ReadAllText(result.ReportPath);
        Assert.Contains("Unused but in localisation", report);
        Assert.Contains("other_sv\tlocalization/english/a_l_english.yml:2", report);
        Assert.DoesNotContain("my_sv\tlocalization", report);
    }

    [Fact]
    public void ResidualFilesExistForAllGames()
    {
        foreach (var id in new[] { "ck3", "vic3", "eu4", "eu5", "hoi4", "stellaris" })
        {
            Assert.True(File.Exists(RepoFiles.Data("calibration/" + id + "-errors.txt")), id);
        }
    }

    /// <summary>Kleines Vanilla: Event <c>m.1</c> ruft das fehlende Event <c>m.9</c> und den unbekannten Effekt <c>my_unknown_effect</c>.</summary>
    private void WriteFixture() =>
        ScriptCheckTestRoots.Write(
            _roots.Vanilla,
            "events/m.txt",
            "namespace = m\nm.1 = {\n\timmediate = {\n\t\ttrigger_event = m.9\n\t\tmy_unknown_effect = yes\n\t\tadd_gold = 5\n\t}\n}");

    /// <summary>Script-Docs mit den genannten Effekten (ohne Importe, also nichts veraltet).</summary>
    private string WriteScriptDocs(params string[] effects)
    {
        var dir = Path.Combine(_roots.Temp, "docs");
        new ScriptDocStorage(dir).Save(new ScriptDocDatabase
        {
            Entries = effects.Select(e => new ScriptDocEntry { Kind = ScriptDocKind.Effect, Name = e }).ToList(),
        });
        return dir;
    }

    /// <summary>Vanilla-Ordner aus <c>PMT_FLAGS_&lt;SPIEL&gt;_GAME</c>; null, wenn nicht gesetzt oder nicht vorhanden.</summary>
    private static string? VanillaFor(string game)
    {
        var vanilla = Environment.GetEnvironmentVariable("PMT_FLAGS_" + game + "_GAME");
        return string.IsNullOrEmpty(vanilla) || !Directory.Exists(vanilla) ? null : vanilla;
    }

    /// <summary>Script-Docs aus <c>PMT_SCRIPTDOCS_&lt;SPIEL&gt;</c>, sonst der Ordner der App.</summary>
    private static string DocsFor(string gameId, string game)
    {
        var docs = Environment.GetEnvironmentVariable("PMT_SCRIPTDOCS_" + game);
        return string.IsNullOrEmpty(docs)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ParadoxModTool", "games", gameId, "script-docs")
            : docs;
    }

    /// <summary>
    /// Leistungsdaten eines Spiels gegen Vanilla prüfen (ohne Umgebungsvariable sofort fertig): jeder Puls ist eine On-Action-Definition in Vanilla;
    /// jeder globale und engere Iterator ist ein Effekt oder Trigger der Script-Docs (ohne Docs: Schlüssel in Vanilla); jeder Ordner ständig geprüfter
    /// Blöcke hat eine Vanilla-Datei, in der jeder Block und jeder Hüllschlüssel als <c>name = {</c> vorkommt; jede On-Action-Aufrufliste kommt in einer
    /// On-Action-Datei vor.
    /// </summary>
    private void CheckPerformanceData(string gameId, string game)
    {
        if (VanillaFor(game) is not { } vanilla)
        {
            return;
        }

        var (rules, index) = CalibrationReport.BuildVanilla(gameId, vanilla);
        var performance = rules.Performance;
        Assert.True(performance is not null, gameId + ": kein Objekt performance");
        var problems = new List<string>();

        var onActions = index.Files
            .SelectMany(f => f.Scan.Definitions)
            .Where(d => d.Kind == CheckKind.OnAction)
            .Select(d => d.Name)
            .ToHashSet(StringComparer.Ordinal);
        problems.AddRange(performance.PulseOnActions.Keys.Where(p => !onActions.Contains(p)).Select(p => "Puls ohne On-Action-Definition: " + p));

        var root = Path.GetFullPath(vanilla);
        var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string TextOf(CheckFile file)
        {
            if (!texts.TryGetValue(file.File.RelativePath, out var text))
            {
                // Ohne Kommentare: ein auskommentierter Block zählt nicht.
                texts[file.File.RelativePath] = text = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(Path.Combine(root, file.File.RelativePath)), @"#[^\r\n]*", string.Empty);
            }

            return text;
        }

        static bool HasBlockKey(string text, string key) =>
            System.Text.RegularExpressions.Regex.IsMatch(text, @"(^|[\s{])" + System.Text.RegularExpressions.Regex.Escape(key) + @"\s*=\s*\{", System.Text.RegularExpressions.RegexOptions.Multiline);

        var docsDir = DocsFor(gameId, game);
        var docNames = Directory.Exists(docsDir)
            ? new ScriptDocStorage(docsDir).Load().Entries
                .Where(e => e.Kind is ScriptDocKind.Effect or ScriptDocKind.Trigger)
                .Select(e => e.Name)
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var iteratorNames = performance.GlobalIterators
            .SelectMany(g => new[] { g.Name }.Concat(g.Narrower ?? Array.Empty<string>()))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var scriptFiles = index.Files.Where(f => f.File.RelativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var name in iteratorNames)
        {
            var known = docNames.Count > 0 ? docNames.Contains(name) : scriptFiles.Any(f => HasBlockKey(TextOf(f), name));
            if (!known)
            {
                problems.Add("Iterator weder in den Script-Docs noch in Vanilla: " + name);
            }
        }

        foreach (var rule in performance.AlwaysCheckedBlocks)
        {
            foreach (var excluded in rule.ExcludeFolders ?? Array.Empty<string>())
            {
                if (!scriptFiles.Any(f => rules.IsUnder(f.File.RelativePath, new[] { excluded }, true)))
                {
                    problems.Add("Ausgenommener Ordner ohne Vanilla-Datei: " + excluded);
                }
            }

            var files = scriptFiles.Where(f => rules.Matches(rule, f.File.RelativePath)).ToList();
            if (files.Count == 0)
            {
                problems.Add("Ordner ohne Vanilla-Datei: " + rule.Folder);
                continue;
            }

            foreach (var key in rule.Blocks.Concat((rule.Wrappers ?? Array.Empty<string>()).Where(w => w != CheckedBlockRule.AnyWrapper)))
            {
                if (!files.Any(f => HasBlockKey(TextOf(f), key)))
                {
                    problems.Add($"Block {key} kommt in {rule.Folder} nicht vor");
                }
            }

            if (rule.NameField is { } field && !files.Any(f => System.Text.RegularExpressions.Regex.IsMatch(TextOf(f), @"(^|[\s{])" + System.Text.RegularExpressions.Regex.Escape(field) + @"\s*=\s*[^\s{]", System.Text.RegularExpressions.RegexOptions.Multiline)))
            {
                problems.Add($"Namensfeld {field} kommt in {rule.Folder} nicht vor");
            }
        }

        var onActionFiles = scriptFiles.Where(f => rules.IsUnder(f.File.RelativePath, rules.OnActionFolders, true)).ToList();
        foreach (var list in performance.OnActionCallLists)
        {
            if (!onActionFiles.Any(f => HasBlockKey(TextOf(f), list)))
            {
                problems.Add("On-Action-Aufrufliste kommt in Vanilla nicht vor: " + list);
            }
        }

        _output.WriteLine($"{gameId}: {performance.PulseOnActions.Count} Pulse, {iteratorNames.Count} Iteratoren, {performance.AlwaysCheckedBlocks.Count} Ordner, Script-Docs {docNames.Count > 0}");
        Assert.True(problems.Count == 0, gameId + ":\n" + string.Join("\n", problems));
    }

    /// <summary>Ein Spiel kalibrieren: ohne Umgebungsvariable sofort fertig; sonst jeder Error-Schlüssel muss in der Restliste stehen.</summary>
    private void Calibrate(string gameId, string game)
    {
        if (VanillaFor(game) is not { } vanilla)
        {
            return;
        }

        var docs = DocsFor(gameId, game);

        var result = CalibrationReport.Run(gameId, vanilla, docs, Path.Combine(Path.GetTempPath(), "pmt-scriptcheck-calibration"));
        Assert.True(result.HasModRoot, "Vanilla wurde nicht als Mod erkannt");
        Assert.True(result.FileCount > 0, "keine Dateien gelesen");

        _output.WriteLine($"{gameId}: Dateien {result.FileCount}, Aufbau {result.BuildSeconds:F1} s, Refresh {result.RefreshSeconds:F1} s, BlockReferencesChecked {result.BlockReferencesChecked}");
        foreach (var count in result.Counts.OrderBy(c => c.Key.Severity).ThenBy(c => c.Key.Kind))
        {
            _output.WriteLine($"  {count.Key.Kind} {count.Key.Severity}: {count.Value}");
        }

        _output.WriteLine("Bericht: " + result.ReportPath);

        var residual = CalibrationReport.ReadResidual(RepoFiles.Data("calibration/" + gameId + "-errors.txt"));
        var (unlisted, vanished) = CalibrationReport.Compare(result.ErrorKeys, residual);
        foreach (var key in vanished)
        {
            _output.WriteLine("Nicht mehr aufgetreten (Restliste): " + key);
        }

        Assert.True(
            unlisted.Count == 0,
            $"{unlisted.Count} Error-Schlüssel fehlen in der Restliste {gameId}-errors.txt (alle im Bericht {result.ReportPath}):\n"
                + string.Join("\n", unlisted.Take(30)));
    }
}
