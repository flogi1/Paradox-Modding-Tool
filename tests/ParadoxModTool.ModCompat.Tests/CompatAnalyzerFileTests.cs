using ParadoxModTool.Mods;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class CompatAnalyzerFileTests : CompatTestBase
{
    private CompatResult Run(CompatParticipant[] mods, string? focus = null, string[]? counterparts = null, Func<CompatParticipant, CompatParticipant, ModRelationKind>? relation = null) =>
        CompatAnalyzer.Analyze(new CompatRequest
        {
            Participants = mods,
            Rules = Rules(),
            FocusId = focus,
            CounterpartIds = counterparts,
            Relation = relation,
        });

    [Fact]
    public void Same_path_with_different_content_is_a_file_conflict_won_by_the_higher_rank()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "in_game/gfx/icon.dds", "aaa");
        Put(b, "in_game/gfx/icon.dds", "bbb");
        var finding = Assert.Single(Run(new[] { a, b }).Findings);
        Assert.Equal(CompatKind.FileOverride, finding.Kind);
        Assert.Equal(CompatSeverity.Conflict, finding.Severity);
        Assert.Equal("B", finding.Winner!.Name);
        Assert.Equal("in_game/gfx/icon.dds", finding.RelativePath);
        Assert.Equal(new[] { "A", "B" }, finding.Contributions.Select(c => c.Participant.Name));
        Assert.Equal(CompatSuggestion.NeedsCompatch, finding.Suggestion);
    }

    [Fact]
    public void Metadata_and_root_files_of_the_mods_are_no_conflict()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, ".metadata/metadata.json", "{ \"name\": \"A\" }");
        Put(b, ".metadata/metadata.json", "{ \"name\": \"B\" }");
        Put(a, "descriptor.mod", "name=\"A\"");
        Put(b, "descriptor.mod", "name=\"B\"");
        Put(a, "thumbnail.png", "a");
        Put(b, "thumbnail.png", "b");

        Assert.Empty(Run(new[] { a, b }).Findings);
    }

    [Fact]
    public void Same_path_with_identical_content_is_info()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "in_game/gfx/icon.dds", "same");
        Put(b, "in_game/gfx/icon.dds", "same");
        var finding = Assert.Single(Run(new[] { a, b }).Findings);
        Assert.Equal(CompatKind.FileIdentical, finding.Kind);
        Assert.Equal(CompatSeverity.Info, finding.Severity);
    }

    [Fact]
    public void File_in_only_one_mod_gives_no_finding()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "in_game/gfx/only_a.dds", "x");
        Put(b, "in_game/gfx/only_b.dds", "y");
        Assert.Empty(Run(new[] { a, b }).Findings);
    }

    [Fact]
    public void Focus_limits_findings_to_groups_that_contain_the_focus_mod()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        var c = Mod("C", 3);
        Put(b, "in_game/gfx/x.dds", "1");
        Put(c, "in_game/gfx/x.dds", "2");
        Assert.Empty(Run(new[] { a, b, c }, focus: "A").Findings);
        Assert.Single(Run(new[] { a, b, c }, focus: "B").Findings);
    }

    [Fact]
    public void Counterparts_limit_the_other_side()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        var c = Mod("C", 3);
        Put(a, "in_game/gfx/x.dds", "1");
        Put(b, "in_game/gfx/x.dds", "2");
        Put(c, "in_game/gfx/x.dds", "3");
        var result = Run(new[] { a, b, c }, focus: "A", counterparts: new[] { "B" });
        var finding = Assert.Single(result.Findings);
        Assert.Equal(new[] { "A", "B" }, finding.Contributions.Select(x => x.Participant.Name));
        Assert.Equal("B", finding.Winner!.Name);
    }

    [Fact]
    public void Focus_that_is_not_in_the_set_throws()
    {
        var a = Mod("A", 1);
        Assert.Throws<ArgumentException>(() => Run(new[] { a }, focus: "X"));
    }

    [Fact]
    public void Alternative_relation_suppresses_and_addon_relation_caps_to_info()
    {
        var main = Mod("Main", 1, "m");
        var dev = Mod("Dev", 2, "d");
        Put(main, "in_game/gfx/x.dds", "1");
        Put(dev, "in_game/gfx/x.dds", "2");
        Assert.Empty(Run(new[] { main, dev }, relation: (_, _) => ModRelationKind.Alternative).Findings);
        var addon = Assert.Single(Run(new[] { main, dev }, relation: (_, _) => ModRelationKind.Addon).Findings);
        Assert.Equal(CompatSeverity.Info, addon.Severity);
        Assert.NotNull(addon.Note);
        var normal = Assert.Single(Run(new[] { main, dev }, relation: (_, _) => ModRelationKind.Unrelated).Findings);
        Assert.Equal(CompatSeverity.Conflict, normal.Severity);
    }

    [Fact]
    public void Missing_folder_is_reported_not_thrown()
    {
        var a = Mod("A", 1);
        var ghost = new CompatParticipant("G", "Geist", Path.Combine(Dir, "weg"), 2, false);
        Put(a, "in_game/gfx/x.dds", "1");
        var finding = Assert.Single(Run(new[] { a, ghost }).Findings);
        Assert.Equal(CompatKind.MissingFolder, finding.Kind);
        Assert.Equal(CompatSeverity.Info, finding.Severity);
        Assert.Contains("Geist", finding.Message);
    }

    [Fact]
    public void Unreadable_script_is_reported_as_info()
    {
        var a = Mod("A", 1);
        Put(a, "in_game/common/building_types/broken.txt", "farm = { cost = 1\n");
        var finding = Assert.Single(Run(new[] { a }).Findings);
        Assert.Equal(CompatKind.Unreadable, finding.Kind);
        Assert.Equal("in_game/common/building_types/broken.txt", finding.RelativePath);
    }

    [Fact]
    public void Empty_set_gives_no_findings()
    {
        Assert.Empty(Run(Array.Empty<CompatParticipant>()).Findings);
    }

    [Fact]
    public void Findings_are_sorted_by_severity_then_path()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "in_game/gfx/b_same.dds", "s");
        Put(b, "in_game/gfx/b_same.dds", "s");
        Put(a, "in_game/gfx/a_diff.dds", "1");
        Put(b, "in_game/gfx/a_diff.dds", "2");
        var kinds = Run(new[] { a, b }).Findings.Select(f => f.Kind).ToList();
        Assert.Equal(new[] { CompatKind.FileOverride, CompatKind.FileIdentical }, kinds);
    }
}
