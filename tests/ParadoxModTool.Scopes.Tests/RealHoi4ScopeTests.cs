using System.Text;
using ParadoxModTool.ScriptDocs;
using Xunit;

namespace ParadoxModTool.Scopes.Tests;

/// <summary>
/// Scope-Lernen auf den echten Dateien von Hearts of Iron IV (flache Installation, Doku als Markdown in <c>documentation</c>, klassische Events
/// <c>country_event = { id = … }</c>). Ohne Spiel läuft nichts.
/// </summary>
public class RealHoi4ScopeTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public RealHoi4ScopeTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    private const string Game = @"C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV";

    private static readonly string Docs = Path.Combine(Game, "documentation");

    private static readonly Dictionary<string, string> Names = new()
    {
        ["Effect"] = "effects_documentation.md",
        ["Trigger"] = "triggers_documentation.md",
        ["Modifier"] = "modifiers_documentation.md",
        ["DynamicVariable"] = "dynamic_variables_documentation.md",
    };

    /// <summary>Blockschlüssel der Events in Vanilla (gezählt 2026-10-16: 6247/677/6/33/7), wie im Profil <c>profiles/hoi4.json</c>.</summary>
    private static readonly Dictionary<string, string> EventBlockScopes = new()
    {
        ["country_event"] = ScopeRules.LearnedRootScope,
        ["news_event"] = ScopeRules.LearnedRootScope,
        ["state_event"] = ScopeRules.LearnedRootScope,
        ["unit_leader_event"] = ScopeRules.LearnedRootScope,
        ["operative_leader_event"] = ScopeRules.LearnedRootScope,
    };

    private static (ScopeDb Db, ScopeRules Rules)? Learn()
    {
        if (!File.Exists(Path.Combine(Docs, "effects_documentation.md")))
        {
            return null;
        }

        var database = new ScriptDocDatabase();
        Assert.NotNull(ScriptDocImporter.Import(database, Docs, DateTime.UtcNow, Names));
        var db = ScopeDb.FromDatabase(database);
        var rules = ScopeRuleLearner.Learn(new[] { Game }, db, 1, DateTime.UtcNow, eventBlockScopes: EventBlockScopes, numericKeysChangeScope: true).Rules;
        return (db, rules);
    }

    [Fact]
    public void Learned_rules_on_hoi4()
    {
        if (Learn() is not var (db, rules))
        {
            return;
        }

        var engine = new ScopeEngine(db, rules);
        var allowed = 0;
        var notAllowed = 0;
        var unknown = 0;
        var violations = new Dictionary<string, int>();
        var reasons = new Dictionary<string, int>();
        var unknownFolders = new Dictionary<string, int>();
        foreach (var file in ScopeRuleLearner.EnumerateFiles(new[] { Game }))
        {
            var tree = ScriptTree.Parse(File.ReadAllText(file.Path, Encoding.UTF8));
            foreach (var node in Walk(tree))
            {
                if (node.Key is null || db.GetCommand(node.Key) is null)
                {
                    continue;
                }

                var context = engine.Analyze(tree, node.Start, file.Path);
                if (!context.Confident)
                {
                    unknown++;
                    var reason = context.UnknownReason ?? (context.CurrentScope is null ? "?" : "start-scope schwach");
                    var key = System.Text.RegularExpressions.Regex.Replace(reason, "(scope:|Typ von |Parameterblock von |hat mehrere|unbekannter Block |unbekanntes Ziel |Start-Scope von |keine Regel für )[^ ]*", "$1*");
                    reasons[key] = reasons.GetValueOrDefault(key) + 1;
                    var folder = ScopeEngine.FolderOf(file.Path) ?? "?";
                    unknownFolders[folder] = unknownFolders.GetValueOrDefault(folder) + 1;
                    continue;
                }

                if (engine.Check(node.Key, context).Result == ScopeCheck.NotAllowed)
                {
                    notAllowed++;
                    var label = $"{node.Key} in {context.CurrentScope} ({context.FieldPath})";
                    violations[label] = violations.GetValueOrDefault(label) + 1;
                }
                else
                {
                    allowed++;
                }
            }
        }

        var checkedCount = allowed + notAllowed;
        var ratio = checkedCount == 0 ? 0 : (double)allowed / checkedCount;
        _output.WriteLine($"HOI IV: {rules.ConfidentCount} sichere Regeln, {rules.UnresolvedCount} offen, {rules.FilesScanned} Dateien; geprüft {checkedCount}, erlaubt {allowed}, Widersprüche {notAllowed}, ohne sicheren Scope {unknown}, Konsistenz {ratio:P1}");
        _output.WriteLine("Gründe: " + string.Join(" | ", reasons.OrderByDescending(r => r.Value).Take(14).Select(r => $"{r.Value}x {r.Key}")));
        _output.WriteLine("Ordner ohne sicheren Scope: " + string.Join(" | ", unknownFolders.OrderByDescending(r => r.Value).Take(14).Select(r => $"{r.Value}x {r.Key}")));
        var worst = string.Join("\n", violations.OrderByDescending(v => v.Value).Take(12).Select(v => $"  {v.Value}x {v.Key}"));
        _output.WriteLine("Häufigste Widersprüche:\n" + worst);

        Assert.Contains(rules.Fields.Keys, k => k.StartsWith("events/country_event/", StringComparison.Ordinal));
        Assert.True(ratio >= 0.95, $"Konsistenz {ratio:P1} ({notAllowed} Widersprüche von {checkedCount}, unbekannt: {unknown})\n{worst}");
    }

    [Fact]
    public void Vanilla_events_and_focus_trees_give_no_scope_warnings()
    {
        if (Learn() is not var (db, rules))
        {
            return;
        }

        var engine = new ScopeEngine(db, rules);
        var issues = new List<string>();
        var files = 0;
        foreach (var folder in new[] { "events", Path.Combine("common", "national_focus") })
        {
            foreach (var path in Directory.GetFiles(Path.Combine(Game, folder), "*.txt"))
            {
                files++;
                var text = File.ReadAllText(path, Encoding.UTF8);
                foreach (var issue in engine.FindIssues(text, path))
                {
                    var line = text[..issue.Start].Count(c => c == '\n') + 1;
                    issues.Add($"{Path.GetFileName(path)}:{line}: {issue.Message}");
                }
            }
        }

        var grouped = issues.GroupBy(i => i[(i.IndexOf(": ", StringComparison.Ordinal) + 2)..]).OrderByDescending(g => g.Count()).ToList();
        _output.WriteLine(string.Join("\n", issues.Take(10)));
        _output.WriteLine($"{files} Dateien, {issues.Count} Warnungen, {grouped.Count} verschiedene");
        _output.WriteLine(string.Join("\n", grouped.Take(20).Select(g => $"  {g.Count()}x {g.Key} (z. B. {g.First()[..g.First().IndexOf(':')]})")));

        // 1.19.3 (2026-10-16): 8 Warnungen. 7 sind echte Fehler in Vanilla (SEA_Japan, WUW_Germany: add_building_construction steht nach dem
        // Block random_core_state, also im Land); 1 betrifft ROOT unter einem Charakter-Schlüssel (PHI_jose_laurel = { … ROOT = { … } }), den die
        // Scope-Verfolgung als Feld liest. Vor numericKeysChangeScope waren es 1022 (Staats-IDs wie 763 = { … }).
        Assert.True(issues.Count <= 10, $"{issues.Count} Warnungen auf Vanilla");
    }

    private static IEnumerable<ScriptNode> Walk(IEnumerable<ScriptNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            if (node.Children is not null)
            {
                foreach (var child in Walk(node.Children))
                {
                    yield return child;
                }
            }
        }
    }
}
