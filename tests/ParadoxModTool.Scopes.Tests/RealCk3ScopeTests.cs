using System.Text;
using ParadoxModTool.ScriptDocs;
using Xunit;

namespace ParadoxModTool.Scopes.Tests;

/// <summary>
/// Scope-Lernen auf den echten Dateien von Crusader Kings III (flache Ordner <c>common</c>/<c>events</c>, Doku-Logs im Ordner <c>logs</c>):
/// Für jeden Trigger/Effect der Vanilla-Skripte wird der Scope an seiner Stelle bestimmt und gegen die Doku-Logs geprüft. Ohne Spiel und
/// Logs läuft nichts.
/// </summary>
public class RealCk3ScopeTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public RealCk3ScopeTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly string Logs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Paradox Interactive",
        "Crusader Kings III",
        "logs");

    private const string Game = @"D:\SteamLibrary\steamapps\common\Crusader Kings III\game";

    [Fact]
    public void Learned_rules_on_ck3()
    {
        if (!File.Exists(Path.Combine(Logs, "effects.log")) || !Directory.Exists(Game))
        {
            return;
        }

        var database = new ScriptDocDatabase();
        Assert.NotNull(ScriptDocImporter.Import(database, Logs, DateTime.UtcNow));
        var db = ScopeDb.FromDatabase(database);
        var rules = ScopeRuleLearner.Learn(new[] { Game }, db, 1, DateTime.UtcNow, defaultEventType: "character_event").Rules;
        var engine = new ScopeEngine(db, rules);

        var allowed = 0;
        var notAllowed = 0;
        var unknown = 0;
        var violations = new Dictionary<string, int>();
        var unknownFolders = new Dictionary<string, int>();
        var reasons = new Dictionary<string, int>();
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
        _output.WriteLine($"CK III: {rules.ConfidentCount} sichere Regeln, {rules.UnresolvedCount} offen, {rules.FilesScanned} Dateien; geprüft {checkedCount}, erlaubt {allowed}, Widersprüche {notAllowed}, ohne sicheren Scope {unknown}, Konsistenz {ratio:P1}");
        _output.WriteLine("Gründe: " + string.Join(" | ", reasons.OrderByDescending(r => r.Value).Take(14).Select(r => $"{r.Value}x {r.Key}")));
        _output.WriteLine("Ordner ohne sicheren Scope: " + string.Join(" | ", unknownFolders.OrderByDescending(r => r.Value).Take(14).Select(r => $"{r.Value}x {r.Key}")));
        var worst = string.Join("\n", violations.OrderByDescending(v => v.Value).Take(12).Select(v => $"  {v.Value}x {v.Key}"));
        _output.WriteLine("Häufigste Widersprüche:\n" + worst);

        Assert.True(rules.ConfidentCount > 200, $"nur {rules.ConfidentCount} sichere Regeln");
        Assert.True(ratio > 0.95, $"Konsistenz {ratio:P1} ({notAllowed} Widersprüche von {checkedCount}, unbekannt: {unknown})\n{worst}");
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
