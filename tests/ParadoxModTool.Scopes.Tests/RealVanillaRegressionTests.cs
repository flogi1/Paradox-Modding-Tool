using System.Text;
using ParadoxModTool.ScriptDocs;
using Xunit;

namespace ParadoxModTool.Scopes.Tests;

/// <summary>
/// Prüft Lernen und Scope-Verfolgung gegen die echten Dateien dieses Rechners: Für jeden Trigger/Effect in den Vanilla-Skripten wird der Scope an seiner Stelle
/// bestimmt und geprüft, ob der Trigger/Effect dort laut Doku-Logs erlaubt ist. Ohne Spiel und Logs (z. B. auf einem anderen Rechner) läuft nichts.
/// </summary>
public class RealVanillaRegressionTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public RealVanillaRegressionTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly string Docs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Paradox Interactive",
        "Europa Universalis V",
        "docs");

    private const string Game = @"C:\Program Files (x86)\Steam\steamapps\common\Europa Universalis V\game";

    [Fact]
    public void LearnedRules_AreConsistentWithTheDocLogsOnRealVanillaFiles()
    {
        if (!Directory.Exists(Docs) || !Directory.Exists(Game))
        {
            return;
        }

        var database = new ScriptDocDatabase();
        Assert.NotNull(ScriptDocImporter.Import(database, Docs, DateTime.UtcNow));
        var db = ScopeDb.FromDatabase(database);
        var rules = ScopeRuleLearner.Learn(new[] { Game }, db, 1, DateTime.UtcNow).Rules;
        var engine = new ScopeEngine(db, rules);

        Assert.True(rules.ConfidentCount > 200, $"nur {rules.ConfidentCount} sichere Regeln");

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
        _output.WriteLine($"Regeln: {rules.ConfidentCount} sicher, {rules.UnresolvedCount} offen, {rules.Discriminators.Count} Unterscheider, {rules.FilesScanned} Dateien; geprüft {checkedCount}, erlaubt {allowed}, Widersprüche {notAllowed}, ohne sicheren Scope {unknown}");
        _output.WriteLine("Gründe: " + string.Join(" | ", reasons.OrderByDescending(r => r.Value).Take(14).Select(r => $"{r.Value}x {r.Key}")));
        _output.WriteLine("Ordner: " + string.Join(" | ", unknownFolders.OrderByDescending(r => r.Value).Take(14).Select(r => $"{r.Value}x {r.Key}")));
        var ratio = (double)allowed / checkedCount;
        var worst = string.Join("\n", violations.OrderByDescending(v => v.Value).Take(12).Select(v => $"  {v.Value}x {v.Key}"));
        Assert.True(checkedCount > 20000, $"nur {checkedCount} Statements geprüft (unbekannt: {unknown})");
        Assert.True(ratio >= 0.97, $"Konsistenz {ratio:P1} ({notAllowed} Widersprüche von {checkedCount}, unbekannt: {unknown})\n{worst}");
    }

    [Fact]
    public void FindIssues_OnRealVanillaFiles_ReportsAlmostNothing()
    {
        if (!Directory.Exists(Docs) || !Directory.Exists(Game))
        {
            return;
        }

        var database = new ScriptDocDatabase();
        Assert.NotNull(ScriptDocImporter.Import(database, Docs, DateTime.UtcNow));
        var db = ScopeDb.FromDatabase(database);
        var rules = ScopeRuleLearner.Learn(new[] { Game }, db, 1, DateTime.UtcNow).Rules;
        var engine = new ScopeEngine(db, rules);

        var issues = 0;
        var commands = 0;
        var labels = new Dictionary<string, int>();
        var examples = new Dictionary<string, string>();
        var started = DateTime.UtcNow;
        foreach (var file in ScopeRuleLearner.EnumerateFiles(new[] { Game }))
        {
            var tree = ScriptTree.Parse(File.ReadAllText(file.Path, Encoding.UTF8));
            commands += Walk(tree).Count(n => n.Key is not null && db.GetCommand(n.Key) is not null);
            foreach (var issue in engine.FindIssues(tree, file.Path))
            {
                issues++;
                var label = $"{issue.Command} in {issue.Scope}";
                labels[label] = labels.GetValueOrDefault(label) + 1;
                if (!examples.ContainsKey(label))
                {
                    var ctx = engine.Analyze(tree, issue.Start, file.Path);
                    examples[label] = $"{file.Path.Replace(Game, "")} @ {ctx.FieldPath} :: {ctx.Describe()}";
                }
            }
        }

        var savedFields = rules.SavedFields.Values.Sum(v => v.Count);
        _output.WriteLine($"Gespeicherte Scopes gelernt: {savedFields} je Feld, {rules.SavedNames.Count} je Name. Prüfdauer {(DateTime.UtcNow - started).TotalSeconds:0.0} s");
        _output.WriteLine($"Meldungen: {issues} bei {commands} Trigger/Effect-Anweisungen ({(double)issues / commands:P2})");
        foreach (var top in labels.OrderByDescending(l => l.Value).Take(14))
        {
            _output.WriteLine($"BEISPIEL {top.Value}x {top.Key}: {examples[top.Key]}");
        }

        _output.WriteLine("Häufigste: " + string.Join(" | ", labels.OrderByDescending(l => l.Value).Take(20).Select(l => $"{l.Value}x {l.Key}")));

        Assert.True(rules.SavedNames.Count > 100, $"nur {rules.SavedNames.Count} Namen gelernt");
        Assert.True((double)issues / commands < 0.001, $"{issues} Meldungen bei {commands} Anweisungen; häufigste: " + string.Join(", ", labels.OrderByDescending(l => l.Value).Take(8).Select(l => $"{l.Value}x {l.Key}")));
    }

    [Fact]
    public void PathFinder_OnRealDocs_FindsTheObviousWays()
    {
        if (!Directory.Exists(Docs))
        {
            return;
        }

        var database = new ScriptDocDatabase();
        Assert.NotNull(ScriptDocImporter.Import(database, Docs, DateTime.UtcNow));
        var finder = new ScopePathFinder(ScopeDb.FromDatabase(database));

        string[] Direct(string from, string to) => finder.Find(from, to, maxSteps: 1).Select(p => p.Describe("every")).ToArray();

        _output.WriteLine($"{finder.Scopes.Count} Scopes im Graph");
        _output.WriteLine("country -> location: " + string.Join(", ", Direct("country", "location").Take(10)));
        _output.WriteLine("location -> country: " + string.Join(", ", Direct("location", "country").Take(10)));
        _output.WriteLine("country -> character: " + string.Join(", ", Direct("country", "character").Take(10)));

        Assert.Contains("capital", Direct("country", "location"));
        Assert.Contains("owner", Direct("location", "country"));
        Assert.Contains("ruler", Direct("country", "character"));
        Assert.DoesNotContain(finder.Find("country", "location", maxSteps: 1).SelectMany(p => p.Steps), s => s.Kind == PathStepKind.GlobalLink);
        Assert.Contains(finder.Find("country", "location", maxSteps: 1, includeGlobalLinks: true), p => p.Steps[0].Kind == PathStepKind.GlobalLink);
        Assert.NotEmpty(finder.Find("location", "country", maxSteps: 3));
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
