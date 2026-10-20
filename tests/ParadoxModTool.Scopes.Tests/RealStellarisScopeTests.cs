using System.Text;
using ParadoxModTool.ScriptDocs;
using Xunit;

namespace ParadoxModTool.Scopes.Tests;

/// <summary>
/// Scope-Lernen auf den echten Dateien von Stellaris (flache Ordner <c>common</c>/<c>events</c>, Doku-Logs in <c>logs/script_documentation</c>):
/// Für jeden Trigger/Effect der Vanilla-Skripte wird der Scope an seiner Stelle bestimmt und gegen die Doku-Logs geprüft. Die Root-Scopes der
/// Event-Blöcke (country_event …) kommen aus dem ausgelieferten Profil <c>profiles/stellaris.json</c>. Ohne Spiel und Logs läuft nichts.
/// </summary>
public class RealStellarisScopeTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public RealStellarisScopeTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly string Logs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Paradox Interactive",
        "Stellaris",
        "logs",
        "script_documentation");

    private const string Game = @"D:\SteamLibrary\steamapps\common\Stellaris";

    [Fact]
    public void Learned_rules_on_stellaris()
    {
        if (!File.Exists(Path.Combine(Logs, "effects.log")) || !Directory.Exists(Game))
        {
            return;
        }

        var database = new ScriptDocDatabase();
        Assert.NotNull(ScriptDocImporter.Import(database, Logs, DateTime.UtcNow, new Dictionary<string, string> { ["EventTarget"] = "scopes.log" }));
        var db = ScopeDb.FromDatabase(database);
        var eventBlocks = ProfileEventBlockScopes();
        foreach (var (block, scope) in eventBlocks.Where(b => b.Value != "none"))
        {
            Assert.True(db.IsScopeType(scope), $"{block}: {scope} ist kein Scope der Doku-Logs");
        }

        var rules = ScopeRuleLearner.Learn(new[] { Game }, db, 1, DateTime.UtcNow, eventBlockScopes: eventBlocks).Rules;
        var engine = new ScopeEngine(db, rules);

        // Gelernt gegen fest: der Scope, den die Vanilla-Events selbst nahelegen, neben dem Wert aus dem Profil.
        foreach (var (path, rule) in rules.Fields.Where(f => f.Key.StartsWith("events/", StringComparison.Ordinal) && (f.Key.Count(c => c == '/') == 2 || f.Key.Contains("inline_script", StringComparison.Ordinal))).OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            _output.WriteLine($"{path}: gelernt {rule.Scope ?? "-"} ({rule.Support:P1} von {rule.Total}), Profil {eventBlocks[path.Split('/')[1]]}");
        }

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
        _output.WriteLine($"Stellaris: {rules.ConfidentCount} sichere Regeln, {rules.UnresolvedCount} offen, {rules.FilesScanned} Dateien; geprüft {checkedCount}, erlaubt {allowed}, Widersprüche {notAllowed}, ohne sicheren Scope {unknown}, Konsistenz {ratio:P1}");
        var withoutType = reasons.Where(r => r.Key.StartsWith("Ereignis ohne type", StringComparison.Ordinal)).Sum(r => r.Value);
        var withoutRoot = reasons.Where(r => r.Key.StartsWith("Ereignis ohne Root-Scope", StringComparison.Ordinal)).Sum(r => r.Value);
        _output.WriteLine($"Ereignisse: ohne type {withoutType}, ohne Root-Scope {withoutRoot}");
        _output.WriteLine("Gründe: " + string.Join(" | ", reasons.OrderByDescending(r => r.Value).Take(14).Select(r => $"{r.Value}x {r.Key}")));
        _output.WriteLine("Ordner ohne sicheren Scope: " + string.Join(" | ", unknownFolders.OrderByDescending(r => r.Value).Take(14).Select(r => $"{r.Value}x {r.Key}")));
        var worst = string.Join("\n", violations.OrderByDescending(v => v.Value).Take(12).Select(v => $"  {v.Value}x {v.Key}"));
        _output.WriteLine("Häufigste Widersprüche:\n" + worst);

        Assert.True(withoutType < 10_000, $"{withoutType} Stellen in Ereignissen ohne type: Blocknamen aus eventBlockScopes greifen nicht");
        Assert.True(rules.ConfidentCount > 200, $"nur {rules.ConfidentCount} sichere Regeln");
        Assert.True(ratio > 0.95, $"Konsistenz {ratio:P1} ({notAllowed} Widersprüche von {checkedCount}, unbekannt: {unknown})\n{worst}");
    }

    /// <summary>Die Event-Blöcke aus dem ausgelieferten Profil (gesucht von der Testausgabe aufwärts).</summary>
    private static Dictionary<string, string> ProfileEventBlockScopes()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "profiles", "stellaris.json");
            if (File.Exists(file))
            {
                using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
                return json.RootElement.GetProperty("eventBlockScopes").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
            }
        }

        throw new FileNotFoundException("profiles/stellaris.json");
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
