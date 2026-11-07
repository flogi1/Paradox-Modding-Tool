using ParadoxModTool.ScriptDocs;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class ScriptCheckBuiltInsTests : IDisposable
{
    private readonly ScriptCheckTestRoots _roots = new();

    public void Dispose() => _roots.Dispose();

    /// <summary>Zwei Importe: <c>is_host</c> (Trigger) stand zuletzt in Import 1 und ist damit veraltet.</summary>
    private static ScriptDocDatabase Database() => new()
    {
        Imports = new List<ImportRecord>
        {
            new() { Id = 1, Kinds = new List<ScriptDocKind> { ScriptDocKind.Effect, ScriptDocKind.Trigger, ScriptDocKind.EventTarget } },
            new() { Id = 2, Kinds = new List<ScriptDocKind> { ScriptDocKind.Effect, ScriptDocKind.Trigger, ScriptDocKind.EventTarget } },
        },
        Entries = new List<ScriptDocEntry>
        {
            new() { Kind = ScriptDocKind.Trigger, Name = "is_host", FirstSeenImport = 1, LastSeenImport = 1 },
            new() { Kind = ScriptDocKind.Effect, Name = "add_gold", FirstSeenImport = 1, LastSeenImport = 2 },
            new() { Kind = ScriptDocKind.EventTarget, Name = "root_target", FirstSeenImport = 1, LastSeenImport = 2 },
        },
    };

    [Fact]
    public void CalibrationCountsOutdatedEntries()
    {
        var dir = Path.Combine(_roots.Temp, "docs");
        new ScriptDocStorage(dir).Save(Database());

        var names = CalibrationReport.LoadBuiltIns(dir);

        Assert.Contains("is_host", names);
    }

    [Fact]
    public void FromDatabaseHoldsEffectsTriggersAndEventTargets()
    {
        var database = Database();
        database.Entries.Add(new ScriptDocEntry { Kind = ScriptDocKind.Modifier, Name = "some_modifier", FirstSeenImport = 1, LastSeenImport = 2 });

        var names = ScriptCheckBuiltIns.FromDatabase(database);

        Assert.Equal(new[] { "add_gold", "is_host", "root_target" }, names.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void OutdatedTriggerInModIsNotBroken()
    {
        ScriptCheckTestRoots.Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\ttrigger = {\n\t\tis_host = yes\n\t}\n}");
        var rules = ScriptCheckTestRoots.Rules();
        var index = new ScriptCheckBuilder(_roots.Sources(), rules).Build();

        var result = ScriptCheckAnalyzer.Analyze(index, ScriptCheckBuiltIns.FromDatabase(Database()), rules);

        Assert.DoesNotContain(result.Findings, f => f.Kind == FindingKind.BrokenReference);
    }
}
