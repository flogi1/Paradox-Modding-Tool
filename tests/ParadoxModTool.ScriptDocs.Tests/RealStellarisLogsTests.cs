using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

/// <summary>Prüft den Parser gegen die echten Doku-Logs von Stellaris auf diesem Rechner; ohne die Dateien läuft nichts.</summary>
public class RealStellarisLogsTests
{
    private static readonly string Docs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Paradox Interactive",
        "Stellaris",
        "logs",
        "script_documentation");

    private static readonly Dictionary<string, string> Names = new() { ["EventTarget"] = "scopes.log" };

    [Fact]
    public void All_four_logs_give_every_entry()
    {
        if (!File.Exists(Path.Combine(Docs, "modifiers.log")))
        {
            return;
        }

        int Count(ScriptDocKind kind) =>
            ScriptDocLogParser.Parse(kind, File.ReadAllText(Path.Combine(Docs, ScriptDocLogFiles.FileNameOf(kind, Names)))).Count;

        // Logs von Stellaris 4.5.2 (2026-10-16): 1080 Effects, 1098 Trigger, 99 Scope-Wechsel, 45 583 Modifier; spätere Versionen haben eher mehr.
        Assert.True(Count(ScriptDocKind.Effect) >= 1080);
        Assert.True(Count(ScriptDocKind.Trigger) >= 1098);
        Assert.True(Count(ScriptDocKind.EventTarget) >= 99);
        Assert.True(Count(ScriptDocKind.Modifier) >= 45583);
        Assert.NotNull(ScriptDocImporter.Import(new ScriptDocDatabase(), Docs, DateTime.UtcNow, Names));
    }

    [Fact]
    public void Every_entry_ends_with_its_scopes_line()
    {
        if (!File.Exists(Path.Combine(Docs, "effects.log")))
        {
            return;
        }

        foreach (var kind in new[] { ScriptDocKind.Effect, ScriptDocKind.Trigger })
        {
            var text = File.ReadAllText(Path.Combine(Docs, ScriptDocLogFiles.FileNameOf(kind)));
            var scopeLines = text.Split('\n').Count(l => l.StartsWith("Supported Scopes:", StringComparison.Ordinal));
            var entries = ScriptDocLogParser.Parse(kind, text);
            Assert.Equal(scopeLines, entries.Count);
            Assert.All(entries, e => Assert.DoesNotContain(" ", e.Name));
        }
    }
}
