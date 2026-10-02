using ParadoxModTool.Mods;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class CompatAnalyzerKeyTests : CompatTestBase
{
    private const string Types = "in_game/common/building_types/";

    private CompatResult Run(IReadOnlyList<CompatParticipant> mods, string? focus = null, Func<CompatParticipant, CompatParticipant, ModRelationKind>? relation = null) =>
        CompatAnalyzer.Analyze(new CompatRequest { Participants = mods, Rules = Rules(), FocusId = focus, Relation = relation });

    private static CompatFinding One(CompatResult result, string key) => Assert.Single(result.Findings, f => f.Key == key);

    [Fact]
    public void Plain_definitions_in_different_files_conflict_and_the_later_mod_wins()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "farm = { cost = 2 }\n");
        var finding = One(Run(new[] { a, b }), "farm");
        Assert.Equal(CompatKind.KeyConflict, finding.Kind);
        Assert.Equal(CompatSeverity.Conflict, finding.Severity);
        Assert.Equal("B", finding.Winner!.Name);
        Assert.Equal(CompatSuggestion.NeedsCompatch, finding.Suggestion);
        Assert.False(finding.Uncertain);
    }

    [Fact]
    public void Identical_definitions_are_info_and_formatting_does_not_matter()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "farm = {\n\t# anders formatiert\n\tcost = 1\n}\n");
        Assert.Equal(CompatKind.KeyIdentical, One(Run(new[] { a, b }), "farm").Kind);
    }

    [Fact]
    public void Injects_of_different_child_keys_are_combinable()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "INJECT:farm = { a = 1 }\n");
        Put(b, Types + "b.txt", "INJECT:farm = { b = 1 }\n");
        var finding = One(Run(new[] { a, b }), "farm");
        Assert.Equal(CompatKind.KeyCombinable, finding.Kind);
        Assert.Equal(CompatSeverity.Hint, finding.Severity);
        Assert.Null(finding.Winner);
    }

    [Fact]
    public void Injects_of_the_same_child_key_with_different_values_conflict_and_the_later_one_wins()
    {
        // INJECT überschreibt vorhandene Teilschlüssel (gemessen 2026-10-02 in Europa Universalis V).
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "INJECT:farm = { a = 1 }\n");
        Put(b, Types + "b.txt", "INJECT:farm = { a = 2 }\n");
        var finding = One(Run(new[] { a, b }), "farm");
        Assert.Equal(CompatKind.KeyConflict, finding.Kind);
        Assert.False(finding.Uncertain);
        Assert.Equal("B", finding.Winner?.Name);
        Assert.EndsWith("\"B\".", finding.Message);
    }

    [Fact]
    public void Injects_of_the_same_child_block_with_different_values_stay_uncertain()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "INJECT:farm = { modifier = { x = 1 } }\n");
        Put(b, Types + "b.txt", "INJECT:farm = { modifier = { y = 1 } }\n");
        var finding = One(Run(new[] { a, b }), "farm");
        Assert.Equal(CompatKind.KeyConflict, finding.Kind);
        Assert.True(finding.Uncertain);
    }

    [Fact]
    public void Plain_definition_plus_inject_of_other_child_key_is_combinable()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "INJECT:farm = { extra = 1 }\n");
        Assert.Equal(CompatKind.KeyCombinable, One(Run(new[] { a, b }), "farm").Kind);
    }

    [Fact]
    public void Replace_removes_the_inject_of_another_mod_regardless_of_rank()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "REPLACE:farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "INJECT:farm = { extra = 1 }\n");
        var finding = One(Run(new[] { a, b }), "farm");
        Assert.Equal(CompatKind.KeyLoss, finding.Kind);
        Assert.Equal(CompatSeverity.Conflict, finding.Severity);
        Assert.Equal("A", finding.Winner!.Name);
        Assert.Equal(CompatSuggestion.ReviewBothSides, finding.Suggestion);
    }

    [Fact]
    public void Two_replaces_conflict_and_the_later_wins()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "REPLACE:farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "REPLACE:farm = { cost = 2 }\n");
        var finding = One(Run(new[] { a, b }), "farm");
        Assert.Equal(CompatKind.KeyConflict, finding.Kind);
        Assert.Equal("B", finding.Winner!.Name);
    }

    [Fact]
    public void Replace_over_a_plain_definition_of_another_mod_is_only_a_hint()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "REPLACE:farm = { cost = 2 }\n");
        var finding = One(Run(new[] { a, b }), "farm");
        Assert.Equal(CompatKind.KeyLoss, finding.Kind);
        Assert.Equal(CompatSeverity.Hint, finding.Severity);
        Assert.Equal("B", finding.Winner!.Name);
    }

    [Fact]
    public void Events_first_loaded_wins()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "in_game/events/e1.txt", "my.1 = { a = 1 }\n");
        Put(b, "in_game/events/e2.txt", "my.1 = { a = 2 }\n");
        var finding = One(Run(new[] { a, b }), "my.1");
        Assert.Equal(CompatKind.KeyConflict, finding.Kind);
        Assert.Equal("A", finding.Winner!.Name);
    }

    [Fact]
    public void Gui_last_loaded_wins()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "in_game/gui/x.gui", "types X { type window_x = window { size = { 1 1 } } }\n");
        Put(b, "in_game/gui/y.gui", "types Y { type window_x = window { size = { 2 2 } } }\n");
        Assert.Equal("B", One(Run(new[] { a, b }), "type window_x").Winner!.Name);
    }

    [Fact]
    public void Defines_conflict_only_on_the_same_value_key()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "in_game/common/defines/a.txt", "NGame = { START_YEAR = 1444 OTHER = 5 }\n");
        Put(b, "in_game/common/defines/b.txt", "NGame = { START_YEAR = 1500 OTHER = 5 }\n");
        var result = Run(new[] { a, b });
        Assert.Equal(CompatKind.KeyConflict, One(result, "NGame.START_YEAR").Kind);
        Assert.Equal(CompatKind.KeyIdentical, One(result, "NGame.OTHER").Kind);
    }

    [Fact]
    public void On_action_is_combinable_and_always_uncertain()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "in_game/common/on_action/a.txt", "on_x = { events = { e1 } }\n");
        Put(b, "in_game/common/on_action/b.txt", "on_x = { events = { e2 } }\n");
        var finding = One(Run(new[] { a, b }), "on_x");
        Assert.Equal(CompatKind.KeyCombinable, finding.Kind);
        Assert.True(finding.Uncertain);
    }

    [Fact]
    public void Localization_plain_duplicates_are_uncertain_and_replace_folder_wins()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "in_game/localization/english/a_l_english.yml", "l_english:\n foo:0 \"A\"\n");
        Put(b, "in_game/localization/english/b_l_english.yml", "l_english:\n foo:0 \"B\"\n");
        var plain = One(Run(new[] { a, b }), "english/foo");
        Assert.Equal(CompatKind.KeyConflict, plain.Kind);
        Assert.True(plain.Uncertain);
        Assert.Equal("B", plain.Winner!.Name);

        var c = Mod("C", 1);
        var d = Mod("D", 2);
        Put(c, "in_game/localization/english/replace/c_l_english.yml", "l_english:\n foo:0 \"C\"\n");
        Put(d, "in_game/localization/english/d_l_english.yml", "l_english:\n foo:0 \"D\"\n");
        var replaced = One(Run(new[] { c, d }), "english/foo");
        Assert.Equal("C", replaced.Winner!.Name);
        Assert.False(replaced.Uncertain);
    }

    [Fact]
    public void File_replaced_at_the_same_path_reports_only_the_file_conflict()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, Types + "x.txt", "farm = { cost = 1 }\nonly_a = { cost = 1 }\n");
        Put(b, Types + "x.txt", "farm = { cost = 2 }\n");
        var findings = Run(new[] { a, b }).Findings;
        var finding = Assert.Single(findings);
        Assert.Equal(CompatKind.FileOverride, finding.Kind);
    }

    [Fact]
    public void Unknown_folder_findings_are_uncertain()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "in_game/setup/a.txt", "thing = { x = 1 }\n");
        Put(b, "in_game/setup/b.txt", "thing = { x = 2 }\n");
        Assert.True(One(Run(new[] { a, b }), "thing").Uncertain);
    }

    [Fact]
    public void Key_in_only_one_mod_and_vanilla_participant_produce_nothing()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        var vanilla = Vanilla();
        Put(a, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(b, Types + "b.txt", "mine = { cost = 1 }\n");
        Assert.Empty(Run(new[] { vanilla, a, b }).Findings);
    }

    [Fact]
    public void Focus_requires_the_focus_mod_in_the_finding()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        var c = Mod("C", 3);
        Put(b, Types + "b.txt", "farm = { cost = 1 }\n");
        Put(c, Types + "c.txt", "farm = { cost = 2 }\n");
        Assert.Empty(Run(new[] { a, b, c }, focus: "A").Findings);
        Assert.Single(Run(new[] { a, b, c }, focus: "C").Findings);
    }

    [Fact]
    public void Project_relations_apply_to_key_findings()
    {
        var main = Mod("Main", 1, "m");
        var dev = Mod("Dev", 2, "d");
        Put(main, Types + "a.txt", "farm = { cost = 1 }\n");
        Put(dev, Types + "b.txt", "farm = { cost = 2 }\n");
        Assert.Empty(Run(new[] { main, dev }, relation: (_, _) => ModRelationKind.Alternative).Findings);
        Assert.Equal(CompatSeverity.Info, Assert.Single(Run(new[] { main, dev }, relation: (_, _) => ModRelationKind.Addon).Findings).Severity);
    }
}
