using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>Regelart "Block bearbeiten": Bearbeitungen (Einfügen u. a.) direkt in den bestehenden Blöcken der Dateien, ohne Block-Kopie und ohne eigene Datei.</summary>
public sealed class BlockEditApplierTests
{
    private const string Folder = "common/buildings/*.txt";

    private static BlockEdit Insert(string key, string text, bool enabled = true) => new() { Kind = BlockEditKind.InsertInto, Key = key, Text = text, Enabled = enabled };

    private static BlockEditRule Rule(bool followNext = true, bool createCopy = true, params BlockEdit[] edits) => new()
    {
        Id = "e", Name = "Ressourcen", Files = Folder, BlockKey = "{key}", FollowNext = followNext, CreateCopy = createCopy,
        Edits = edits.Length == 0 ? new List<BlockEdit> { Insert("on_start", "fx_start = yes"), Insert("on_cancelled", "fx_back = yes") } : edits.ToList(),
    };

    private static RuleRunContext Run(TempMod mod, BlockEditRule rule, params string[] keys)
    {
        var ctx = LineRuleApplierTests.Context(mod, keys);
        BlockEditApplier.Apply(rule, ctx);
        return ctx;
    }

    [Fact]
    public void Inserts_into_a_block_of_a_mod_file_in_place_and_creates_missing_blocks()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/buildings/01_own.txt", "other = {\n\tcost = 1\n}\n\nx_01 = {\n\ttype = special\n\tcost = 7\n\ton_start = {\n\t\tfx_old = yes\n\t}\n}\n", bom: false);

        var ctx = Run(mod, Rule(), "x_01");

        Assert.Equal(
            "other = {\n\tcost = 1\n}\n\nx_01 = {\n\ttype = special\n\tcost = 7\n\ton_start = {\n\t\tfx_old = yes\n\t\tfx_start = yes\n\t}\n\ton_cancelled = {\n\t\tfx_back = yes\n\t}\n}\n",
            ctx.Workspace.Get("common/buildings/01_own.txt"));
        Assert.Equal(new[] { "1 Blöcke bearbeitet" }, ctx.Workspace.NotesOf("common/buildings/01_own.txt"));
        Assert.Empty(ctx.Skipped);
    }

    [Fact]
    public void A_block_only_in_vanilla_is_edited_in_a_new_mod_copy_of_its_file()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_special_buildings.txt", "x_01 = {\n\ttype = special\n}\ny_01 = {\n\ttype = special\n}\n");

        var ctx = Run(mod, Rule(), "x_01");

        var text = ctx.Workspace.Get("common/buildings/00_special_buildings.txt")!;
        Assert.Contains("x_01 = {\n\ttype = special\n\ton_start = {\n\t\tfx_start = yes\n\t}", text, StringComparison.Ordinal);
        Assert.Contains("y_01 = {\n\ttype = special\n}", text, StringComparison.Ordinal);
        Assert.True(ctx.Workspace.IsCreatedFromVanilla("common/buildings/00_special_buildings.txt"));
    }

    [Fact]
    public void Without_create_copy_a_vanilla_only_block_is_skipped_and_nothing_is_created()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_special_buildings.txt", "x_01 = {\n\ttype = special\n}\n");

        var ctx = Run(mod, Rule(createCopy: false), "x_01");

        Assert.Null(ctx.Workspace.Get("common/buildings/00_special_buildings.txt"));
        Assert.Contains(ctx.Skipped, s => s.RuleId == "e" && s.RowKey == "x_01");
    }

    [Fact]
    public void An_unknown_key_is_skipped_with_a_reason()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_special_buildings.txt", "x_01 = {\n\ttype = special\n}\n");

        var ctx = Run(mod, Rule(), "gibt_es_nicht");

        Assert.Single(ctx.Skipped);
        Assert.Contains("gibt_es_nicht", ctx.Skipped[0].Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Follow_next_edits_every_level_of_the_chain_in_all_files()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_a.txt", "x_01 = {\n\tnext_building = x_02\n}\nz = {\n\tcost = 1\n}\n");
        mod.WriteVanilla("common/buildings/00_b.txt", "x_02 = {\n\tnext_building = x_03\n}\nx_03 = {\n\tcost = 3\n}\n");

        var ctx = Run(mod, Rule(), "x_01");

        Assert.Contains("x_01 = {", ctx.Workspace.Get("common/buildings/00_a.txt"), StringComparison.Ordinal);
        var second = ctx.Workspace.Get("common/buildings/00_b.txt")!;
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(second, "on_start = \\{").Count);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(second, "on_cancelled = \\{").Count);
        Assert.DoesNotContain("on_start", ctx.Workspace.Get("common/buildings/00_a.txt")!.Split("z = {")[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Without_follow_next_only_the_named_block_is_edited()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_a.txt", "x_01 = {\n\tnext_building = x_02\n}\nx_02 = {\n\tcost = 2\n}\n");

        var ctx = Run(mod, Rule(followNext: false), "x_01");

        var text = ctx.Workspace.Get("common/buildings/00_a.txt")!;
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(text, "on_start = \\{").Count);
    }

    [Fact]
    public void A_second_run_changes_nothing_and_a_partly_filled_block_only_gets_the_missing_lines()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/buildings/01_own.txt", "x_01 = {\n\ton_start = {\n\t\tfx_start = yes\n\t}\n}\n", bom: false);
        var rule = Rule(edits: Insert("on_start", "fx_start = yes\nfx_more = yes"));
        var ctx = Run(mod, rule, "x_01");
        var first = ctx.Workspace.Get("common/buildings/01_own.txt")!;

        BlockEditApplier.Apply(rule, ctx);

        Assert.Equal("x_01 = {\n\ton_start = {\n\t\tfx_start = yes\n\t\tfx_more = yes\n\t}\n}\n", first);
        Assert.Equal(first, ctx.Workspace.Get("common/buildings/01_own.txt"));
    }

    [Fact]
    public void Switched_off_edits_do_not_run()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/buildings/01_own.txt", "x_01 = {\n\tcost = 1\n}\n", bom: false);

        var ctx = Run(mod, Rule(edits: new[] { Insert("on_start", "fx_start = yes", enabled: false), Insert("on_cancelled", "fx_back = yes") }), "x_01");

        var text = ctx.Workspace.Get("common/buildings/01_own.txt")!;
        Assert.DoesNotContain("on_start", text, StringComparison.Ordinal);
        Assert.Contains("on_cancelled", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Line_endings_and_the_bom_of_the_file_stay()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/buildings/01_own.txt", "x_01 = {\r\n\tcost = 1\r\n}\r\n", bom: true);

        var ctx = Run(mod, Rule(), "x_01");

        Assert.Equal("x_01 = {\r\n\tcost = 1\r\n\ton_start = {\r\n\t\tfx_start = yes\r\n\t}\r\n\ton_cancelled = {\r\n\t\tfx_back = yes\r\n\t}\r\n}\r\n", ctx.Workspace.Get("common/buildings/01_own.txt"));
        Assert.True(ctx.Workspace.HasBomOf("common/buildings/01_own.txt"));
    }

    [Fact]
    public void Commented_blocks_and_generated_regions_are_not_touched()
    {
        using var mod = new TempMod();
        mod.WriteMod(
            "common/buildings/01_own.txt",
            "# x_01 = {\n#\tcost = 1\n# }\n# PMT-Patch r/buildings begin\n# PMT-Unit x_01\nx_01 = {\n\tcost = 9\n}\n# PMT-Patch r/buildings end\ny = {\n}\n",
            bom: false);

        var ctx = Run(mod, Rule(), "x_01");

        Assert.Equal(
            "# x_01 = {\n#\tcost = 1\n# }\n# PMT-Patch r/buildings begin\n# PMT-Unit x_01\nx_01 = {\n\tcost = 9\n}\n# PMT-Patch r/buildings end\ny = {\n}\n",
            ctx.Workspace.Get("common/buildings/01_own.txt"));
    }
}
