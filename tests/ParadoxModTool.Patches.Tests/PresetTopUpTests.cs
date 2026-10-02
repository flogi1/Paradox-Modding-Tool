using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class PresetTopUpTests
{
    /// <summary>Ein Rezept, wie es vor den Vorlagen-Erweiterungen entstand: fünf Regeln, keine Trigger-Regeln, keine Ressourcen-Bearbeitungen.</summary>
    private static PatchRecipe OldRecipe()
    {
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());
        recipe.Rules.RemoveAll(r => r.Id.StartsWith("trigger_level_", StringComparison.Ordinal));
        var buildings = (BlockCopyRule)recipe.Rules.Single(r => r.Id == "buildings");
        buildings.Edits.RemoveAll(e => e.Key is "on_start" or "on_cancelled");
        return recipe;
    }

    [Fact]
    public void Adds_missing_rules_edits_after_the_rule_that_precedes_them_in_the_preset()
    {
        var recipe = OldRecipe();

        var result = PresetTopUp.Apply(recipe);

        Assert.Equal(
            new[] { "slots", "iterators", "originals", "buildings", "holdings", "trigger_level_01", "trigger_level_02", "trigger_level_03", "trigger_level_04" },
            recipe.Rules.Select(r => r.Id).ToArray());
        Assert.Equal(4, result.Rules.Count);
        Assert.Equal(new[] { "Gebäude: on_start", "Gebäude: on_cancelled" }, result.Edits.ToArray());
        var buildings = (BlockCopyRule)recipe.Rules.Single(r => r.Id == "buildings");
        Assert.Contains(buildings.Edits, e => e.Key == "on_start" && e.Text.Contains("flogis_techmod_subtract_wood_250", StringComparison.Ordinal));
        Assert.Contains(buildings.Edits, e => e.Key == "on_cancelled");
        Assert.Empty(result.Columns);
        Assert.Empty(result.Patterns);
    }

    [Fact]
    public void A_missing_rule_in_the_middle_is_inserted_at_its_preset_position()
    {
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());
        recipe.Rules.RemoveAll(r => r.Id == "iterators");

        PresetTopUp.Apply(recipe);

        Assert.Equal("iterators", recipe.Rules[1].Id);
    }

    [Fact]
    public void Missing_columns_and_watch_patterns_are_added()
    {
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());
        recipe.Columns.RemoveAll(c => c.Name == "barony");
        recipe.Watch.Patterns.Remove("has_special_building");

        var result = PresetTopUp.Apply(recipe);

        Assert.Equal(new[] { "barony" }, result.Columns.ToArray());
        Assert.Contains(recipe.Columns, c => c.Name == "barony");
        Assert.Equal(new[] { "has_special_building" }, result.Patterns.ToArray());
        Assert.Contains("has_special_building", recipe.Watch.Patterns);
    }

    [Fact]
    public void Existing_rules_edits_and_hand_changes_stay_untouched_and_a_second_run_adds_nothing()
    {
        var recipe = OldRecipe();
        recipe.Rules.Single(r => r.Id == "originals").Enabled = false;
        var buildings = (BlockCopyRule)recipe.Rules.Single(r => r.Id == "buildings");
        buildings.Edits.Add(new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "on_start", Text = "mine = yes", Enabled = false });
        buildings.Target = "common/buildings/my_file.txt";

        var result = PresetTopUp.Apply(recipe);

        Assert.False(recipe.Rules.Single(r => r.Id == "originals").Enabled);
        Assert.Equal("common/buildings/my_file.txt", buildings.Target);
        var starts = buildings.Edits.Where(e => e.Key == "on_start").ToList();
        Assert.Equal("mine = yes", Assert.Single(starts).Text);
        Assert.False(starts[0].Enabled);
        Assert.Equal(new[] { "Gebäude: on_cancelled" }, result.Edits.ToArray());
        Assert.True(PresetTopUp.Apply(recipe).Count == 0);
    }

    [Fact]
    public void Missing_does_not_change_the_recipe()
    {
        var recipe = OldRecipe();
        var before = PatchRecipeStore.Serialize(recipe);

        var result = PresetTopUp.Missing(recipe);

        Assert.Equal(6, result.Count);
        Assert.Equal(before, PatchRecipeStore.Serialize(recipe));
    }

    [Fact]
    public void Resources_preset_gets_its_missing_edit_and_a_recipe_without_preset_gets_nothing()
    {
        var resources = SpecialResourcesPreset.Create("R", Array.Empty<string>());
        ((BlockEditRule)resources.Rules.Single()).Edits.RemoveAll(e => e.Key == "on_cancelled");

        var result = PresetTopUp.Apply(resources);

        Assert.Single(result.Edits);
        Assert.Equal(2, ((BlockEditRule)resources.Rules.Single()).Edits.Count);

        var plain = RecipeEditing.Create("P", "ck3", Array.Empty<string>());
        Assert.Equal(0, PresetTopUp.Apply(plain).Count);
        Assert.Null(PresetTopUp.Template(plain));
    }
}
