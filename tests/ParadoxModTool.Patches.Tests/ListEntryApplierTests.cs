using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class ListEntryApplierTests
{
    private const string Holdings = "common/holdings/00_holdings.txt";
    private const string HoldingsText = "castle_holding = {\n\tbuildings = {\n\t\tcastle_01\n\t}\n}\ncity_holding = {\n\tbuildings = {\n\t\tcity_01\n\t}\n}\n";

    private static RuleRunContext Context(TempMod mod, PatchMemory? memory, params string[] keys)
    {
        var recipe = new PatchRecipe { Id = "r", Columns = { new PatchColumn { Name = "holdings", Type = ColumnType.List, Default = "castle_holding, city_holding" } } };
        foreach (var key in keys)
        {
            recipe.Rows.Add(new PatchRow { Key = key });
        }

        var workspace = new PatchWorkspace(mod.ModRoot, mod.VanillaRoot);
        return new RuleRunContext(recipe, workspace, ColumnResolver.Resolve(recipe, workspace), memory ?? new PatchMemory());
    }

    private static ListEntryRule Rule(bool createCopy = false) => new() { Id = "h", Target = Holdings, ListPath = "{holdings}/buildings", Entry = "{key}", CreateCopy = createCopy };

    [Fact]
    public void Adds_entries_to_each_holding_list_from_a_list_column()
    {
        using var mod = new TempMod();
        mod.WriteMod(Holdings, HoldingsText, bom: false);
        var ctx = Context(mod, null, "angkor_wat_01");

        ListEntryApplier.Apply(Rule(), ctx);

        Assert.Equal(HoldingsText.Replace("\t\tcastle_01\n", "\t\tcastle_01\n\t\tangkor_wat_01\n").Replace("\t\tcity_01\n", "\t\tcity_01\n\t\tangkor_wat_01\n"), ctx.Workspace.Get(Holdings));
        Assert.Equal(new[] { "+2 Listeneinträge" }, ctx.Workspace.NotesOf(Holdings));
        Assert.Equal(2, ctx.NewMemory.ListEntries["h"].Count);
    }

    [Fact]
    public void Creates_the_copy_from_vanilla_when_asked()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Holdings, HoldingsText);
        var ctx = Context(mod, null, "a_01");

        ListEntryApplier.Apply(Rule(createCopy: true), ctx);

        Assert.True(ctx.Workspace.IsCreatedFromVanilla(Holdings));
        Assert.Contains("\t\ta_01\n", ctx.Workspace.Get(Holdings));
    }

    [Fact]
    public void Inline_list_is_broken_open()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/x.txt", "castle_holding = { buildings = { castle_01 } }\n", bom: false);
        var ctx = Context(mod, null, "a_01");

        ListEntryApplier.Apply(new ListEntryRule { Id = "h", Target = "common/x.txt", ListPath = "castle_holding/buildings", Entry = "{key}" }, ctx);

        var text = ctx.Workspace.Get("common/x.txt")!;
        Assert.Contains("castle_01", text);
        Assert.Contains("a_01", text);
        ParadoxModTool.VanillaDiff.ScriptDocument.Parse(text);
    }

    [Fact]
    public void Foreign_entries_are_never_removed()
    {
        using var mod = new TempMod();
        mod.WriteMod(Holdings, HoldingsText, bom: false);
        var memory = new PatchMemory();
        memory.ListEntries["h"] = new List<ListEntryRecord> { new(Holdings, "castle_holding/buildings", "old_01") };
        var ctx = Context(mod, memory);

        ListEntryApplier.Apply(Rule(), ctx);

        Assert.Equal(HoldingsText, ctx.Workspace.Get(Holdings));
        Assert.Empty(ctx.NewMemory.ListEntries["h"]);
    }

    [Fact]
    public void Own_entry_goes_when_the_row_goes()
    {
        using var mod = new TempMod();
        mod.WriteMod(Holdings, HoldingsText, bom: false);
        var first = Context(mod, null, "a_01", "b_01");
        ListEntryApplier.Apply(Rule(), first);
        mod.WriteMod(Holdings, first.Workspace.Get(Holdings)!, bom: false);

        var second = Context(mod, first.NewMemory, "a_01");
        ListEntryApplier.Apply(Rule(), second);

        var text = second.Workspace.Get(Holdings)!;
        Assert.DoesNotContain("b_01", text);
        Assert.Contains("a_01", text);
        Assert.Contains("castle_01", text);
        Assert.Contains("−2 Listeneinträge", second.Workspace.NotesOf(Holdings));
        Assert.DoesNotContain(second.NewMemory.ListEntries["h"], r => r.Entry == "b_01");
    }

    [Fact]
    public void Entry_deleted_by_hand_is_not_added_again()
    {
        using var mod = new TempMod();
        mod.WriteMod(Holdings, HoldingsText, bom: false);
        var first = Context(mod, null, "a_01");
        ListEntryApplier.Apply(Rule(), first);
        mod.WriteMod(Holdings, first.Workspace.Get(Holdings)!.Replace("\t\tcity_01\n\t\ta_01\n", "\t\tcity_01\n"), bom: false);

        var second = Context(mod, first.NewMemory, "a_01");
        ListEntryApplier.Apply(Rule(), second);

        Assert.DoesNotContain(Holdings, second.Workspace.Touched);
        Assert.Contains(second.NewMemory.ListEntries["h"], r => r.ListPath == "city_holding/buildings" && r.Entry == "a_01");
        Assert.Contains(second.Workspace.NotesOf(Holdings), n => n.Contains("von Hand gelöscht", StringComparison.Ordinal));
    }

    [Fact]
    public void Statement_entry_is_detected_as_present()
    {
        using var mod = new TempMod();
        const string text = "is_religious_studies_location = {\n\tOR = {\n\t\thas_building_or_higher  =  a_01\n\t}\n}\n";
        mod.WriteMod("common/t.txt", text, bom: false);
        var ctx = Context(mod, null, "a_01");

        ListEntryApplier.Apply(new ListEntryRule { Id = "rs", Target = "common/t.txt", ListPath = "is_religious_studies_location/OR", Entry = "has_building_or_higher = {key}" }, ctx);

        Assert.Equal(text, ctx.Workspace.Get("common/t.txt"));
        Assert.Empty(ctx.NewMemory.ListEntries["rs"]);
    }

    [Fact]
    public void Entry_that_existed_before_the_recipe_stays_when_the_row_goes()
    {
        using var mod = new TempMod();
        const string text = "is_religious_studies_location = {\n\tOR = {\n\t\thas_building_or_higher = a_01\n\t}\n}\n";
        mod.WriteMod("common/t.txt", text, bom: false);
        var rule = new ListEntryRule { Id = "rs", Target = "common/t.txt", ListPath = "is_religious_studies_location/OR", Entry = "has_building_or_higher = {key}" };
        var first = Context(mod, null, "a_01");
        ListEntryApplier.Apply(rule, first);

        var second = Context(mod, first.NewMemory);
        ListEntryApplier.Apply(rule, second);

        Assert.Equal(text, second.Workspace.Get("common/t.txt"));
    }

    [Fact]
    public void Row_with_error_keeps_its_entries()
    {
        using var mod = new TempMod();
        mod.WriteMod(Holdings, HoldingsText, bom: false);
        var first = Context(mod, null, "a_01");
        ListEntryApplier.Apply(Rule(), first);
        mod.WriteMod(Holdings, first.Workspace.Get(Holdings)!, bom: false);

        var second = Context(mod, first.NewMemory, "a_01", "a_01");
        second.Table.Rows[0].Errors.Add("kaputt");
        ListEntryApplier.Apply(Rule(), second);

        Assert.DoesNotContain(Holdings, second.Workspace.Touched);
        Assert.Equal(2, second.NewMemory.ListEntries["h"].Count(r => r.Entry == "a_01"));
    }

    [Fact]
    public void Missing_list_skips_the_row()
    {
        using var mod = new TempMod();
        mod.WriteMod(Holdings, "castle_holding = { }\n", bom: false);
        var ctx = Context(mod, null, "a_01");

        ListEntryApplier.Apply(new ListEntryRule { Id = "h", Target = Holdings, ListPath = "castle_holding/buildings", Entry = "{key}" }, ctx);

        Assert.Contains(new SkippedRow("h", "a_01", "Liste „castle_holding/buildings“ nicht gefunden in common/holdings/00_holdings.txt"), ctx.Skipped);
    }

    [Fact]
    public void Broken_target_is_skipped_with_warning()
    {
        using var mod = new TempMod();
        mod.WriteMod(Holdings, "castle_holding = {\n", bom: false);
        var ctx = Context(mod, null, "a_01");

        ListEntryApplier.Apply(Rule(), ctx);

        Assert.DoesNotContain(Holdings, ctx.Workspace.Touched);
        Assert.Contains(ctx.Warnings, w => w.StartsWith(Holdings + " übersprungen", StringComparison.Ordinal));
    }

    [Fact]
    public void Second_run_changes_nothing()
    {
        using var mod = new TempMod();
        mod.WriteMod(Holdings, HoldingsText, bom: false);
        var first = Context(mod, null, "a_01");
        ListEntryApplier.Apply(Rule(), first);
        mod.WriteMod(Holdings, first.Workspace.Get(Holdings)!, bom: false);

        var second = Context(mod, first.NewMemory, "a_01");
        ListEntryApplier.Apply(Rule(), second);

        Assert.DoesNotContain(Holdings, second.Workspace.Touched);
        Assert.Empty(second.Workspace.NotesOf(Holdings));
    }
}
