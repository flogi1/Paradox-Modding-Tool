using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

/// <summary>Prüft den Parser gegen die echte Doku von Hearts of Iron IV im Installationsordner; ohne Spiel läuft nichts.</summary>
public class RealHoi4DocsTests
{
    private const string Docs = @"C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV\documentation";

    /// <summary>Dateinamen wie im Profil <c>profiles/hoi4.json</c> (<c>scriptDocsFiles</c>).</summary>
    public static readonly Dictionary<string, string> Names = new()
    {
        ["Effect"] = "effects_documentation.md",
        ["Trigger"] = "triggers_documentation.md",
        ["Modifier"] = "modifiers_documentation.md",
        ["DynamicVariable"] = "dynamic_variables_documentation.md",
    };

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public RealHoi4DocsTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Reads_the_installed_documentation()
    {
        if (!File.Exists(Path.Combine(Docs, "effects_documentation.md")))
        {
            return;
        }

        var counts = new Dictionary<ScriptDocKind, int>();
        foreach (var kind in new[] { ScriptDocKind.Effect, ScriptDocKind.Trigger, ScriptDocKind.Modifier, ScriptDocKind.DynamicVariable })
        {
            var entries = ScriptDocLogParser.Parse(kind, File.ReadAllText(Path.Combine(Docs, ScriptDocLogFiles.FileNameOf(kind, Names))));
            counts[kind] = entries.Count;
            Assert.All(entries, e =>
            {
                Assert.DoesNotContain("for scope", e.Name);
                Assert.DoesNotContain("Table of", e.Name);
                Assert.DoesNotContain("<", e.Name);
                Assert.DoesNotContain(" ", e.Name);
                Assert.NotEqual(string.Empty, e.Name);
            });
            _output.WriteLine($"{kind}: {entries.Count}");
        }

        // 1.19.3 (2026-10-16): 564 Überschriften bei Effects, 610 bei Triggern, 764 Modifier-Namen (42 davon Vorlagen), 388 Dynamic Variables.
        Assert.True(counts[ScriptDocKind.Effect] >= 500);
        Assert.True(counts[ScriptDocKind.Trigger] >= 550);
        Assert.True(counts[ScriptDocKind.Modifier] >= 700);
        Assert.True(counts[ScriptDocKind.DynamicVariable] >= 300);

        var database = new ScriptDocDatabase();
        Assert.NotNull(ScriptDocImporter.Import(database, Docs, DateTime.UtcNow, Names));
        Assert.Contains(database.Entries, e => e.Kind == ScriptDocKind.Effect && e.Name == "add_political_power");
    }
}
