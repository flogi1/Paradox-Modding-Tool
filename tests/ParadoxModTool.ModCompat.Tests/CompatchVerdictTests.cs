using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public sealed class CompatchVerdictTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-compatch-verdict-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Mod(string name, string content)
    {
        var folder = Path.Combine(_dir, name);
        var file = Path.Combine(folder, "in_game", "common", "building_types", name + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return folder;
    }

    private static CompatRequest Request(IReadOnlyList<CompatParticipant> participants, Func<CompatParticipant, CompatParticipant, bool>? isCompatchFor) => new()
    {
        Participants = participants,
        Rules = new OverrideRules(new[] { new OverrideRule("*/common", OverrideBehavior.PerKey) }),
        IsCompatchFor = isCompatchFor,
    };

    [Fact]
    public void Finding_with_a_compatch_for_all_others_becomes_info()
    {
        var a = new CompatParticipant("a", "A", Mod("a", "farm = { a = 1 }\n"), 1, false);
        var b = new CompatParticipant("b", "B", Mod("b", "farm = { b = 1 }\n"), 2, false);
        var p = new CompatParticipant("p", "Patch", Mod("p", "farm = { a = 1 b = 1 }\n"), 3, false);

        var withPatch = CompatAnalyzer.Analyze(Request(new[] { a, b, p }, (x, y) => x.Id == "p"));
        var without = CompatAnalyzer.Analyze(Request(new[] { a, b, p }, null));

        var finding = Assert.Single(withPatch.Findings, f => f.Key == "farm");
        Assert.Equal(CompatSeverity.Info, finding.Severity);
        Assert.Equal("Kompatch vorhanden: Patch", finding.Note);
        Assert.NotEqual(CompatSeverity.Info, Assert.Single(without.Findings, f => f.Key == "farm").Severity);
    }

    [Fact]
    public void Compatch_covering_only_one_side_changes_nothing()
    {
        var a = new CompatParticipant("a", "A", Mod("a", "farm = { a = 1 }\n"), 1, false);
        var b = new CompatParticipant("b", "B", Mod("b", "farm = { b = 1 }\n"), 2, false);
        var p = new CompatParticipant("p", "Patch", Mod("p", "farm = { a = 1 b = 1 }\n"), 3, false);

        var result = CompatAnalyzer.Analyze(Request(new[] { a, b, p }, (x, y) => x.Id == "p" && y.Id == "a"));

        Assert.NotEqual(CompatSeverity.Info, Assert.Single(result.Findings, f => f.Key == "farm").Severity);
    }
}
