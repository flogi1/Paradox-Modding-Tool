using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class PatchPlannerTests : CompatchTestBase
{
    private const string Buildings = "in_game/common/building_types/";
    private static readonly EntryAddress Farm = EntryAddress.ForKey("in_game/common/building_types", "farm");

    private PatchPlan Plan(bool euV = true, PatchTarget target = PatchTarget.Compatch, IReadOnlyDictionary<string, MergeDecision>? decisions = null, IReadOnlyDictionary<EntryAddress, PatchForm>? forms = null)
    {
        var input = Input(euV);
        var resolved = EffectiveEntryResolver.Resolve(input);
        var actions = CompatchMerger.Decide(resolved, input);
        var applied = DecisionApplier.Apply(resolved, actions, decisions, input);
        return PatchPlanner.Plan(resolved, applied, input, target, forms);
    }

    [Fact]
    public void Disjoint_injects_need_no_patch()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "INJECT:farm = { a = 1 }\n");
        PutB(Buildings + "b.txt", "INJECT:farm = { b = 1 }\n");

        var plan = Plan();

        Assert.Empty(plan.Entries);
        Assert.Contains(Farm, plan.NotNeeded);
    }

    [Fact]
    public void Pure_addition_over_a_replace_becomes_replace()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 1 a = 1 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 1 b = 1 }\n");

        var op = Assert.Single(Plan().Entries);

        // B's REPLACE: läuft nach jedem INJECT: und würde eine Ergänzung wieder löschen (Prüfbefund C1).
        Assert.Equal(PatchForm.Replace, op.Form);
        Assert.StartsWith("REPLACE:farm = {", op.Text);
        Assert.Contains("a = 1", op.Text);
        Assert.Contains("b = 1", op.Text);
        Assert.Equal("in_game/common/building_types/zz_pmt_compatch.txt", op.TargetFile);
        Assert.False(op.DependsOnLoadOrder);
    }

    [Fact]
    public void Open_conflict_becomes_replace_with_winner_value_and_comment()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 3 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 5 time = 1 }\n");

        var op = Assert.Single(Plan().Entries);

        Assert.Equal(PatchForm.Replace, op.Form);
        Assert.StartsWith("# PMT offen: farm.cost: A = cost=2, B = cost=5\n", op.Text);
        Assert.Contains("REPLACE:farm = {", op.Text);
        Assert.Contains("cost = 5", op.Text);
        Assert.Contains("time = 3", op.Text);
    }

    [Fact]
    public void Base_mod_target_marks_replace_as_load_order_dependent()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 3 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 5 time = 1 }\n");

        Assert.True(Assert.Single(Plan(target: PatchTarget.BaseModA).Entries).DependsOnLoadOrder);
    }

    [Fact]
    public void Both_mods_replace_same_file_keeps_untouched_text_verbatim()
    {
        const string Vanilla = "farm = { cost = 1 }\r\nmine = { cost = 1 }\r\n# keep me\r\nwell   =   { cost = 1 }\r\n";
        PutVanilla("common/buildings/00_x.txt", Vanilla);
        PutA("common/buildings/00_x.txt", Vanilla.Replace("farm = { cost = 1 }", "farm = { cost = 2 }"));
        PutB("common/buildings/00_x.txt", Vanilla.Replace("mine = { cost = 1 }", "mine = { cost = 3 }"));

        var plan = Plan(euV: false);

        var file = Assert.Single(plan.Files);
        Assert.Equal("common/buildings/00_x.txt", file.RelativePath);
        Assert.False(file.Normalized);
        Assert.Contains("farm = { cost = 2 }", file.Text);
        Assert.Contains("mine = { cost = 3 }", file.Text);
        Assert.Contains("# keep me\r\nwell   =   { cost = 1 }\r\n", file.Text);
        Assert.DoesNotContain("\n", file.Text!.Replace("\r\n", string.Empty));
        Assert.True(file.DependsOnLoadOrder);
    }

    [Fact]
    public void Bom_of_source_file_is_remembered()
    {
        var bom = "﻿";
        PutVanilla("common/buildings/00_x.txt", "farm = { cost = 1 }\r\n");
        PutA("common/buildings/00_x.txt", bom + "farm = { cost = 2 }\r\n");
        PutB("common/buildings/00_x.txt", bom + "farm = { cost = 1 }\r\n");

        var file = Assert.Single(Plan(euV: false).Files);

        Assert.True(file.Utf8Bom);
        Assert.DoesNotContain('﻿', file.Text!);
    }

    [Fact]
    public void Removal_is_only_possible_as_whole_file()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\nmine = { cost = 1 }\n");
        PutA(Buildings + "00_x.txt", "mine = { cost = 1 }\n");
        PutB(Buildings + "00_x.txt", "farm = { cost = 1 }\nmine = { cost = 3 }\n");

        var plan = Plan();

        Assert.Equal(new[] { PatchForm.WholeFile }, plan.AvailableForms[Farm]);
        Assert.DoesNotContain("farm", Assert.Single(plan.Files).Text);
    }

    private IReadOnlyList<PatchForm> Vic3Forms(string folder, string desired)
    {
        PutVanilla(folder + "/00_x.txt", "farm = { cost = 10 }\n");
        PutA(folder + "/a.txt", "farm = { cost = 10 }\n");
        PutB(folder + "/b.txt", "farm = { cost = 10 }\n");
        var input = Vic3Input();
        var info = EffectiveEntryResolver.Resolve(input).Addresses[EntryAddress.ForKey(input.Rules.Match(folder + "/a.txt").Scope, "farm")];
        return PatchPlanner.AvailableForms(info, ParadoxModTool.VanillaDiff.ScriptDocument.Parse(desired).Entries, input);
    }

    [Fact]
    public void Appending_game_offers_no_inject_for_a_changed_value()
    {
        var forms = Vic3Forms("common/buildings", "farm = { cost = 20 }\n");

        Assert.DoesNotContain(PatchForm.Inject, forms);
        Assert.Contains(PatchForm.Replace, forms);
    }

    [Fact]
    public void Appending_game_offers_inject_for_a_pure_addition()
    {
        Assert.Contains(PatchForm.Inject, Vic3Forms("common/buildings", "farm = { cost = 10 upkeep = 2 }\n"));
    }

    [Fact]
    public void Replace_only_folder_offers_no_inject()
    {
        var forms = Vic3Forms("common/scripted_effects", "farm = { cost = 10 upkeep = 2 }\n");

        Assert.DoesNotContain(PatchForm.Inject, forms);
        Assert.Contains(PatchForm.Replace, forms);
    }

    [Fact]
    public void Folder_without_operators_offers_neither_inject_nor_replace()
    {
        var forms = Vic3Forms("common/coat_of_arms", "farm = { cost = 10 upkeep = 2 }\n");

        Assert.DoesNotContain(PatchForm.Inject, forms);
        Assert.DoesNotContain(PatchForm.Replace, forms);
    }

    [Fact]
    public void Gui_type_is_wrapped_in_types_block()
    {
        PutVanilla("in_game/gui/shared.gui", "types T {\n type btn = button { size = { 1 1 } color = red }\n}\n");
        PutA("in_game/gui/a.gui", "types A {\n type btn = button { size = { 2 2 } color = red }\n}\n");
        PutB("in_game/gui/b.gui", "types B {\n type btn = button { size = { 1 1 } color = blue }\n}\n");

        var op = Assert.Single(Plan().Entries);

        Assert.Equal(PatchForm.PlainKey, op.Form);
        Assert.StartsWith("types pmt_compatch {\n", op.Text);
        Assert.Equal("in_game/gui/zz_pmt_compatch.gui", op.TargetFile);
        Assert.True(op.DependsOnLoadOrder);
    }

    [Fact]
    public void Form_choice_is_used_when_available()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 1 a = 1 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 1 b = 1 }\n");

        var op = Assert.Single(Plan(forms: new Dictionary<EntryAddress, PatchForm> { [Farm] = PatchForm.Replace }).Entries);

        Assert.Equal(PatchForm.Replace, op.Form);
    }

    [Fact]
    public void Chosen_whole_file_side_is_copied()
    {
        PutA("in_game/common/building_types/readme.md", "a");
        PutB("in_game/common/building_types/readme.md", "b");
        var takeA = new Dictionary<string, MergeDecision>
        {
            [EntryAddress.ForWholeFile("in_game/common/building_types/readme.md").Id] = new(DecisionKind.TakeA),
        };

        Assert.Empty(Plan().Files);
        var file = Assert.Single(Plan(decisions: takeA).Files);

        Assert.Null(file.Text);
        Assert.EndsWith(Path.Combine("a", "in_game", "common", "building_types", "readme.md"), file.CopyFrom);
    }
}
