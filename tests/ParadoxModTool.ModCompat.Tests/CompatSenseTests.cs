using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class CompatSenseTests : CompatTestBase
{
    private const string Types = "in_game/common/building_types/";

    private CompatResult Run(IReadOnlyList<CompatParticipant> all, string? focus = "Mine") =>
        CompatAnalyzer.Analyze(new CompatRequest { Participants = all, Rules = Rules(), FocusId = focus });

    private static IEnumerable<CompatFinding> Sense(CompatResult result) =>
        result.Findings.Where(f => f.Kind is CompatKind.RedundantOverride or CompatKind.TooCoarse or CompatKind.AdditiveOnly or CompatKind.ValueChangeOnly or CompatKind.ReplaceNeeded);

    private (CompatParticipant Vanilla, CompatParticipant Mine) Setup(string vanillaText, string mineText, string file = "a.txt")
    {
        var vanilla = Vanilla();
        var mine = Mod("Mine", 2);
        Put(vanilla, Types + file, vanillaText);
        Put(mine, Types + file, mineText);
        return (vanilla, mine);
    }

    [Fact]
    public void Byte_identical_copy_is_redundant()
    {
        var (v, m) = Setup("farm = { cost = 1 }\n", "farm = { cost = 1 }\n");
        var finding = Assert.Single(Sense(Run(new[] { v, m })));
        Assert.Equal(CompatKind.RedundantOverride, finding.Kind);
        Assert.Equal(CompatSeverity.Hint, finding.Severity);
        Assert.Equal(CompatSuggestion.RemoveRedundant, finding.Suggestion);
        Assert.Equal(new[] { "Mine", "Vanilla" }, finding.Contributions.Select(c => c.Participant.Name));
    }

    [Fact]
    public void Hidden_vanilla_file_is_no_base_for_redundant_override()
    {
        var (v, m) = Setup("farm = { cost = 1 }\n", "farm = { cost = 1 }\n");

        var result = CompatAnalyzer.Analyze(new CompatRequest
        {
            Participants = new[] { v, m },
            Rules = Rules(),
            FocusId = "Mine",
            HiddenVanillaFolders = new[] { "common/building_types" },
            ReplacePathRoots = new[] { "in_game", "main_menu", "loading_screen" },
        });

        Assert.Empty(Sense(result));
    }

    [Fact]
    public void Formatting_only_difference_is_redundant()
    {
        var (v, m) = Setup("farm = { cost = 1 }\n", "farm = {\n  cost = 1 # egal\n}\n");
        Assert.Equal(CompatKind.RedundantOverride, Assert.Single(Sense(Run(new[] { v, m }))).Kind);
    }

    [Fact]
    public void Copied_file_with_one_changed_entry_is_too_coarse_and_names_the_inject_need()
    {
        var (v, m) = Setup(
            "farm = { cost = 1 }\nmine = { cost = 2 }\nwell = { cost = 3 }\n",
            "farm = { cost = 1 }\nmine = { cost = 2 }\nwell = { cost = 9 }\n");
        var findings = Sense(Run(new[] { v, m })).ToList();
        var coarse = Assert.Single(findings, f => f.Kind == CompatKind.TooCoarse);
        Assert.Contains("2 von 3", coarse.Message);
        Assert.Equal(CompatSuggestion.ProvideOnlyChanged, coarse.Suggestion);
        var change = Assert.Single(findings, f => f.Kind == CompatKind.ValueChangeOnly);
        Assert.Equal("well", change.Key);
        Assert.Equal(CompatSeverity.Hint, change.Severity);
        Assert.Equal(CompatSuggestion.UseInject, change.Suggestion);
    }

    [Fact]
    public void Entry_with_only_added_child_keys_suggests_inject()
    {
        var (v, m) = Setup("farm = { cost = 1 }\n", "farm = { cost = 1 extra = 5 }\n");
        var finding = Assert.Single(Sense(Run(new[] { v, m })));
        Assert.Equal(CompatKind.AdditiveOnly, finding.Kind);
        Assert.Equal("farm", finding.Key);
        Assert.Equal(CompatSuggestion.UseInject, finding.Suggestion);
        Assert.Equal(CompatSeverity.Hint, finding.Severity);
    }

    [Fact]
    public void Changed_simple_child_value_suggests_inject()
    {
        var (v, m) = Setup("farm = { cost = 1 }\n", "farm = { cost = 2 }\n");
        var finding = Assert.Single(Sense(Run(new[] { v, m })));
        Assert.Equal(CompatKind.ValueChangeOnly, finding.Kind);
        Assert.Equal("farm", finding.Key);
        Assert.Equal(CompatSuggestion.UseInject, finding.Suggestion);
        Assert.Equal(CompatSeverity.Hint, finding.Severity);
    }

    [Fact]
    public void Changed_and_added_simple_child_keys_suggest_inject()
    {
        var (v, m) = Setup("farm = { cost = 1 time = 1 }\n", "farm = { cost = 2 time = 1 extra = 5 }\n");
        Assert.Equal(CompatKind.ValueChangeOnly, Assert.Single(Sense(Run(new[] { v, m }))).Kind);
    }

    [Theory]
    [InlineData("farm = { cost = 1 mod = { x = 1 } }", "farm = { cost = 1 mod = { x = 2 } }")]
    [InlineData("farm = { cost = 1 time = 1 }", "farm = { cost = 1 }")]
    [InlineData("farm = { cost = 1 time = 1 }", "farm = { time = 1 cost = 2 }")]
    [InlineData("farm = { cost = 1 }", "farm = { cost = 1 cost = 2 }")]
    [InlineData("farm = { cost = 1 }", "farm = { cost ?= 1 }")]
    [InlineData("farm = { a = 1 else = { b = 1 } }", "farm = { a = 1 else_if = { c = 1 } else = { b = 1 } }")]
    public void Changes_INJECT_cannot_express_need_replace(string vanilla, string mine)
    {
        var (v, m) = Setup(vanilla + "\n", mine + "\n");
        Assert.Equal(CompatKind.ReplaceNeeded, Assert.Single(Sense(Run(new[] { v, m }))).Kind);
    }

    [Fact]
    public void Removed_child_key_needs_replace_not_inject()
    {
        var (v, m) = Setup("farm = { cost = 1 extra = 5 }\n", "farm = { cost = 1 }\n");
        Assert.Equal(CompatKind.ReplaceNeeded, Assert.Single(Sense(Run(new[] { v, m }))).Kind);
    }

    [Fact]
    public void New_entry_next_to_copied_ones_is_too_coarse_but_needs_no_replace()
    {
        var (v, m) = Setup("farm = { cost = 1 }\n", "farm = { cost = 1 }\nnew_one = { cost = 4 }\n");
        var findings = Sense(Run(new[] { v, m })).ToList();
        Assert.Equal(CompatKind.TooCoarse, Assert.Single(findings).Kind);
    }

    [Fact]
    public void File_that_already_uses_operators_is_left_alone()
    {
        var (v, m) = Setup("farm = { cost = 1 }\n", "INJECT:farm = { extra = 1 }\n");
        Assert.Empty(Sense(Run(new[] { v, m })));
    }

    [Fact]
    public void New_file_without_a_lower_version_gives_nothing()
    {
        var vanilla = Vanilla();
        var mine = Mod("Mine", 2);
        Put(mine, Types + "brand_new.txt", "farm = { cost = 1 }\n");
        Assert.Empty(Sense(Run(new[] { vanilla, mine })));
    }

    [Fact]
    public void Lower_mod_is_the_base_when_vanilla_lacks_the_file_and_higher_mods_are_ignored()
    {
        var vanilla = Vanilla();
        var low = Mod("Low", 1);
        var mine = Mod("Mine", 2);
        var high = Mod("High", 3);
        Put(low, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(mine, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(high, Types + "a.txt", "farm = { cost = 7 }\n");
        var finding = Assert.Single(Sense(Run(new[] { vanilla, low, mine, high })));
        Assert.Equal(CompatKind.RedundantOverride, finding.Kind);
        Assert.Equal("Low", finding.Contributions[1].Participant.Name);
    }

    [Fact]
    public void Identical_binary_file_is_redundant()
    {
        var vanilla = Vanilla();
        var mine = Mod("Mine", 2);
        Put(vanilla, "in_game/gfx/icon.dds", "bytes");
        Put(mine, "in_game/gfx/icon.dds", "bytes");
        Assert.Equal(CompatKind.RedundantOverride, Assert.Single(Sense(Run(new[] { vanilla, mine }))).Kind);
    }

    [Fact]
    public void Without_focus_no_sense_findings_are_produced()
    {
        var (v, m) = Setup("farm = { cost = 1 }\n", "farm = { cost = 1 }\n");
        Assert.Empty(Sense(Run(new[] { v, m }, focus: null)));
    }

    [Fact]
    public void Unparsable_files_are_skipped_without_error()
    {
        var (v, m) = Setup("farm = { cost = 1\n", "farm = { cost = 2\n");
        Assert.Empty(Sense(Run(new[] { v, m })));
    }
}
