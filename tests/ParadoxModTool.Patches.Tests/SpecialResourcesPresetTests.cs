using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class SpecialResourcesPresetTests
{
    [Fact]
    public void Preset_has_one_in_place_block_edit_rule_and_no_target_file()
    {
        var recipe = SpecialResourcesPreset.Create("Spezial", Array.Empty<string>());

        Assert.Equal("ck3", recipe.GameId);
        Assert.Equal(SpecialResourcesPreset.Id, recipe.Preset);
        Assert.Empty(recipe.Columns);
        var rule = Assert.IsType<BlockEditRule>(Assert.Single(recipe.Rules));
        Assert.Equal("common/buildings/*.txt", rule.Files);
        Assert.Equal("{key}", rule.BlockKey);
        Assert.True(rule.FollowNext);
        Assert.True(rule.CreateCopy);
        Assert.Equal(new[] { "on_start", "on_cancelled" }, rule.Edits.Select(e => e.Key).ToArray());
        Assert.All(rule.Edits, e => Assert.Equal(BlockEditKind.InsertInto, e.Kind));
        Assert.Contains("flogis_techmod_subtract_wood_250 = yes", rule.Edits[0].Text, StringComparison.Ordinal);
        Assert.Contains("flogis_techmod_add_luxury_250 = yes", rule.Edits[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Running_the_preset_extends_the_chain_in_place_keeps_the_type_and_a_second_run_changes_nothing()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(
            "common/buildings/00_special_mines.txt",
            "mine_01 = {\n\ttype = special\n\tnext_building = mine_02\n\ton_start = {\n\t\tvanilla_effect = yes\n\t}\n}\n"
            + "mine_02 = {\n\ttype = special\n}\nother = {\n\ttype = special\n}\n");
        var recipe = SpecialResourcesPreset.Create("S", Array.Empty<string>());
        recipe.Rows.Add(new PatchRow { Key = "mine_01" });

        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);
        var file = Assert.Single(plan.Files, f => f.RelativePath == "common/buildings/00_special_mines.txt");

        Assert.Empty(plan.Skipped);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(file.NewText, "type = special").Count);
        Assert.Contains("vanilla_effect = yes\n\t\tflogis_techmod_subtract_wood_250 = yes", file.NewText.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(file.NewText, "flogis_techmod_add_wood_250 = yes").Count);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(file.NewText, "on_start = \\{").Count);
        Assert.Contains("other = {\n\ttype = special\n}", file.NewText.Replace("\r\n", "\n"), StringComparison.Ordinal);

        PatchPlanWriter.CreateAction(PatchRunnerTests.Context(mod), plan).Do();
        var again = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);
        Assert.All(again.Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
    }
}
