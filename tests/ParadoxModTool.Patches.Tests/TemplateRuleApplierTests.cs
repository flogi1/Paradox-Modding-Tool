using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class TemplateRuleApplierTests
{
    [Fact]
    public void Repeat_section_in_a_template_rule_gives_one_unit()
    {
        using var mod = new TempMod();
        var recipe = new PatchRecipe
        {
            Id = "r",
            Columns = { new PatchColumn { Name = "barony", Type = ColumnType.List, Default = "b_a, b_b" } },
            Rows = { new PatchRow { Key = "x_01" } },
            Rules =
            {
                new TemplateRule { Id = "t", Target = "common/scripted_triggers/t.txt", Text = "t_{key} = {\n\tOR = {\n{*barony}\t\tbarony = title:{barony}\n{/barony}\t}\n}" },
            },
        };

        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);

        var unit = Assert.Single(GeneratedRegion.Read(plan.Files.Single().NewText, "r", "t").Units);
        Assert.Equal("x_01", unit.UnitKey);
        Assert.Contains("barony = title:b_a", unit.Text, StringComparison.Ordinal);
        Assert.Contains("barony = title:b_b", unit.Text, StringComparison.Ordinal);
    }

    private static RuleRunContext Context(TempMod mod, PatchMemory? memory, params string[] keys)
    {
        var recipe = new PatchRecipe { Id = "r" };
        foreach (var key in keys)
        {
            recipe.Rows.Add(new PatchRow { Key = key });
        }

        var workspace = new PatchWorkspace(mod.ModRoot, mod.VanillaRoot);
        return new RuleRunContext(recipe, workspace, ColumnResolver.Resolve(recipe, workspace), memory ?? new PatchMemory());
    }

    [Fact]
    public void Renders_one_unit_per_row_inside_a_block()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/scripted_effects/sort.txt", "Sort_add = {\n\thidden_effect = {\n\t}\n}\n", bom: false);
        var ctx = Context(mod, null, "a_01", "b_01");
        var rule = new TemplateRule { Id = "sort", Target = "common/scripted_effects/sort.txt", Inside = "Sort_add/hidden_effect", Text = "if = { limit = { has_variable = {key} } add_building = {key} }" };

        TemplateRuleApplier.Apply(rule, ctx);

        Assert.Equal(
            "Sort_add = {\n\thidden_effect = {\n\t\t# PMT-Patch r/sort begin\n\t\t# PMT-Unit a_01\n\t\tif = { limit = { has_variable = a_01 } add_building = a_01 }\n\t\t# PMT-Unit b_01\n\t\tif = { limit = { has_variable = b_01 } add_building = b_01 }\n\t\t# PMT-Patch r/sort end\n\t}\n}\n",
            ctx.Workspace.Get("common/scripted_effects/sort.txt"));
        Assert.Equal(new[] { "2 Bausteine erzeugt" }, ctx.Workspace.NotesOf("common/scripted_effects/sort.txt"));
    }

    [Fact]
    public void Localization_template_creates_file_with_header_bom_and_merges_per_key()
    {
        using var mod = new TempMod();
        const string target = "localization/english/pmt_special_l_english.yml";
        var rule = new TemplateRule { Id = "loc_en", Target = target, Text = " building_{key}_cost:0 \"Kosten\"" };
        var first = Context(mod, null, "a_01");

        TemplateRuleApplier.Apply(rule, first);

        var created = first.Workspace.Get(target)!;
        Assert.StartsWith("l_english:\r\n# PMT-Patch r/loc_en begin\r\n", created);
        Assert.True(first.Workspace.HasBomOf(target));

        mod.WriteMod(target, created.Replace("\"Kosten\"", "\"Von Hand\""));
        rule.Text = " building_{key}_cost:0 \"Kosten\"\n building_{key}_extra:0 \"Neu\"";
        var second = Context(mod, first.NewMemory, "a_01");
        TemplateRuleApplier.Apply(rule, second);

        var unit = GeneratedRegion.Read(second.Workspace.Get(target)!, "r", "loc_en").Units.Single().Text;
        Assert.Equal(" building_a_01_cost:0 \"Von Hand\"\n building_a_01_extra:0 \"Neu\"", unit);
    }

    [Fact]
    public void Removed_row_removes_its_unchanged_unit()
    {
        using var mod = new TempMod();
        const string target = "common/scripted_triggers/pmt.txt";
        var rule = new TemplateRule { Id = "t", Target = target, Text = "trigger_{key} = { always = yes }" };
        var first = Context(mod, null, "a_01", "b_01");
        TemplateRuleApplier.Apply(rule, first);
        mod.WriteMod(target, first.Workspace.Get(target)!);

        var second = Context(mod, first.NewMemory, "a_01");
        TemplateRuleApplier.Apply(rule, second);

        var units = GeneratedRegion.Read(second.Workspace.Get(target)!, "r", "t").Units;
        Assert.Equal(new[] { "a_01" }, units.Select(u => u.UnitKey));
        Assert.False(second.NewMemory.Units.ContainsKey("t|b_01"));
    }

    [Fact]
    public void Global_template_renders_one_unit()
    {
        using var mod = new TempMod();
        var ctx = Context(mod, null, "a_01", "b_01");
        TemplateRuleApplier.Apply(new TemplateRule { Id = "g", Target = "common/x.txt", Text = "x = 1", Scope = RuleScope.Global }, ctx);

        Assert.Equal(new[] { "global" }, GeneratedRegion.Read(ctx.Workspace.Get("common/x.txt")!, "r", "g").Units.Select(u => u.UnitKey));
    }

    [Fact]
    public void Marker_line_in_template_text_skips_the_file_with_warning()
    {
        using var mod = new TempMod();
        var ctx = Context(mod, null, "a_01");

        TemplateRuleApplier.Apply(new TemplateRule { Id = "t", Target = "common/x.txt", Text = "x = 1\n\t# PMT-Unit fremd\ny = 2" }, ctx);

        Assert.DoesNotContain("common/x.txt", ctx.Workspace.Touched);
        Assert.Contains(ctx.Warnings, w => w.StartsWith("common/x.txt übersprungen:", StringComparison.Ordinal) && w.Contains("a_01", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_inside_block_skips_the_file_with_warning()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/x.txt", "a = { }\n");
        var ctx = Context(mod, null, "a_01");

        TemplateRuleApplier.Apply(new TemplateRule { Id = "t", Target = "common/x.txt", Inside = "b/c", Text = "x = 1" }, ctx);

        Assert.DoesNotContain("common/x.txt", ctx.Workspace.Touched);
        Assert.Contains("common/x.txt übersprungen: Block „b/c“ nicht gefunden", ctx.Warnings);
    }
}
