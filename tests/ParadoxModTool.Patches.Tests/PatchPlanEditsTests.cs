using Xunit;
using static ParadoxModTool.Patches.Tests.PatchRunnerTests;

namespace ParadoxModTool.Patches.Tests;

public sealed class PatchPlanEditsTests
{
    private static PlannedFile FileOf(PatchPlan plan, string rel) => plan.Files.Single(f => f.RelativePath == rel);

    /// <summary>Lauf 1 schreiben, dann Handänderung und Vanilla-Änderung am selben Wert: Lauf 2 hat einen Konflikt.</summary>
    private static PatchPlan ConflictPlan(TempMod mod, PatchRecipe recipe)
    {
        WriteVanilla(mod);
        PatchPlanWriter.CreateAction(Context(mod), PatchRunner.Plan(Context(mod), recipe)).Do();
        mod.WriteMod(Generated, mod.ReadMod(Generated).Replace("construction_time = 10", "construction_time = 99"));
        mod.WriteVanilla(Specials, "angkor_wat_01 = {\n\ttype = special\n\tconstruction_time = 50\n}\n");
        return PatchRunner.Plan(Context(mod), recipe);
    }

    [Fact]
    public void Conflict_units_carry_base_fresh_mine_and_merged()
    {
        using var mod = new TempMod();
        var plan = ConflictPlan(mod, Recipe("angkor_wat_01"));

        var conflict = Assert.Single(plan.ConflictUnits);
        Assert.Equal(Generated, conflict.File);
        Assert.Equal("defs", conflict.RuleId);
        Assert.Equal("angkor_wat_01", conflict.UnitKey);
        Assert.Contains("construction_time = 10", conflict.Base);
        Assert.Contains("construction_time = 50", conflict.Fresh);
        Assert.Contains("construction_time = 99", conflict.Mine);
        Assert.False(string.IsNullOrEmpty(conflict.Merged));
        Assert.NotEmpty(conflict.Messages);
    }

    [Fact]
    public void Edited_file_is_written_and_next_plan_is_unchanged()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        var recipe = Recipe("angkor_wat_01");
        var plan = PatchRunner.Plan(Context(mod), recipe);
        var rejected = FileOf(plan, Holdings).NewText.Replace("\t\tcity_01\n\t\tangkor_wat_01\n", "\t\tcity_01\n");

        plan = PatchPlanEdits.WithEditedFile(plan, Holdings, rejected);
        Assert.Equal(rejected, FileOf(plan, Holdings).NewText);
        Assert.Equal(PlannedFileStatus.New, FileOf(plan, Holdings).Status);
        PatchPlanWriter.CreateAction(Context(mod), plan).Do();

        var second = PatchRunner.Plan(Context(mod), recipe);
        var holdings = FileOf(second, Holdings);
        Assert.Equal(PlannedFileStatus.Unchanged, holdings.Status);
        Assert.Contains(holdings.Notes, n => n.Contains("city_holding", StringComparison.Ordinal) && n.Contains("von Hand gelöscht", StringComparison.Ordinal));
    }

    [Fact]
    public void Edited_file_back_to_original_is_unchanged()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        mod.WriteMod(Holdings, "castle_holding = {\n\tbuildings = {\n\t}\n}\n");
        var plan = PatchRunner.Plan(Context(mod), Recipe("angkor_wat_01"));

        plan = PatchPlanEdits.WithEditedFile(plan, Holdings, FileOf(plan, Holdings).OriginalText!);

        Assert.Equal(PlannedFileStatus.Unchanged, FileOf(plan, Holdings).Status);
    }

    [Fact]
    public void Resolved_conflict_is_written_with_fresh_as_memory()
    {
        using var mod = new TempMod();
        var recipe = Recipe("angkor_wat_01");
        var plan = ConflictPlan(mod, recipe);
        var conflict = plan.ConflictUnits.Single();

        plan = PatchPlanEdits.ResolveConflict(plan, Generated, "defs", "angkor_wat_01", conflict.Mine.Replace("= 99", "= 77"));

        Assert.Equal(PlannedFileStatus.Changed, FileOf(plan, Generated).Status);
        Assert.Empty(plan.ConflictUnits);
        Assert.Empty(plan.ConflictUnitIds);
        Assert.Empty(FileOf(plan, Generated).Conflicts);
        PatchPlanWriter.CreateAction(Context(mod), plan).Do();

        Assert.Contains("construction_time = 77", mod.ReadMod(Generated));
        Assert.Equal(conflict.Fresh, PatchRecipeStore.LoadMemory(PatchRecipeStore.Folder(mod.ModRoot), recipe.Id).Units["defs|angkor_wat_01"]);
        Assert.All(PatchRunner.Plan(Context(mod), recipe).Files.Where(f => f.RelativePath == Generated), f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
    }

    [Fact]
    public void Single_rule_preview_does_not_remove_other_rows()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        var recipe = Recipe("angkor_wat_01", "borobudur_01");
        PatchPlanWriter.CreateAction(Context(mod), PatchRunner.Plan(Context(mod), recipe)).Do();

        var defs = PatchRunner.Plan(Context(mod), PatchRecipeStore.Clone(RecipeEditing.SingleRulePreview(recipe, "defs", "angkor_wat_01")));
        var holdings = PatchRunner.Plan(Context(mod), PatchRecipeStore.Clone(RecipeEditing.SingleRulePreview(recipe, "holdings", "angkor_wat_01")));

        Assert.DoesNotContain(defs.Files, f => f.Status is PlannedFileStatus.Changed or PlannedFileStatus.New);
        Assert.DoesNotContain(holdings.Files, f => f.Status is PlannedFileStatus.Changed or PlannedFileStatus.New);
        Assert.Empty(defs.Skipped);
        Assert.Empty(holdings.Skipped);
    }

    [Fact]
    public void Stale_files_are_found()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        mod.WriteMod(Holdings, "castle_holding = {\n\tbuildings = {\n\t}\n}\n");
        var plan = PatchRunner.Plan(Context(mod), Recipe("angkor_wat_01"));
        Assert.Empty(PatchPlanWriter.StaleFiles(Context(mod), plan));

        mod.WriteMod(Holdings, "x = 1\n");
        mod.WriteMod(Generated, "y = 1\n");

        Assert.Equal(new[] { Generated, Holdings }, PatchPlanWriter.StaleFiles(Context(mod), plan).OrderBy(p => p, StringComparer.Ordinal));
    }
}
