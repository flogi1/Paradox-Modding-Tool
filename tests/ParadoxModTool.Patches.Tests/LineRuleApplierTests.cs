using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class LineRuleApplierTests
{
    internal static RuleRunContext Context(TempMod mod, params string[] keys)
    {
        var recipe = new PatchRecipe { Id = "r" };
        foreach (var key in keys)
        {
            recipe.Rows.Add(new PatchRow { Key = key });
        }

        var workspace = new PatchWorkspace(mod.ModRoot, mod.VanillaRoot);
        return new RuleRunContext(recipe, workspace, ColumnResolver.Resolve(recipe, workspace), new PatchMemory());
    }

    private static LineRule Comment(string files, string match = "special_building_slot = {key}") =>
        new() { Id = "c", Files = files, Match = match, Action = LineAction.Comment };

    [Fact]
    public void Vanilla_paths_only_leaves_mod_only_files_alone()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_special_buildings.txt", "x_01 = {\n\ttype = special\n}\ny_01 = {\n\ttype = special\n}\n");
        mod.WriteMod("common/buildings/01_own.txt", "x_01 = {\n\ttype = special\n\tcost = 5\n}\n", bom: false);
        var ctx = Context(mod, "x_01");
        var rule = new LineRule
        {
            Id = "o", Files = "common/buildings/*.txt", Match = "{key}", Action = LineAction.RemoveBlock, CreateCopy = true, VanillaPathsOnly = true,
        };

        LineRuleApplier.Apply(rule, ctx);

        Assert.Equal("x_01 = {\n\ttype = special\n\tcost = 5\n}\n", ctx.Workspace.Get("common/buildings/01_own.txt"));
        var copy = ctx.Workspace.Get("common/buildings/00_special_buildings.txt")!;
        Assert.DoesNotContain("x_01", copy, StringComparison.Ordinal);
        Assert.Contains("y_01", copy, StringComparison.Ordinal);
    }

    private static LineRule ReplaceRule(string files, string match, string replacement) =>
        new() { Id = "t", Files = files, Match = match, MatchKind = MatchKind.Text, Action = LineAction.Replace, Replacement = replacement, Scope = RuleScope.Global };

    [Fact]
    public void Replace_with_a_text_match_also_finds_the_multi_line_form_of_a_block()
    {
        using var mod = new TempMod();
        const string Old = "building_requirement_castle_city_church = { LEVEL = 03 }";
        mod.WriteMod(
            "common/buildings/b.txt",
            "a = {\n\tpotential = {\n\t\tbuilding_requirement_castle_city_church =\n\t\t{\n\t\t\tLEVEL = 03\n\t\t}\n\t\tx = yes\n\t}\n}\nb = { " + Old + " }\n",
            bom: false);
        var ctx = Context(mod);

        LineRuleApplier.Apply(ReplaceRule("common/buildings/b.txt", Old, "building_requirement_main_building = { LEVEL = 6 }"), ctx);

        Assert.Equal(
            "a = {\n\tpotential = {\n\t\tbuilding_requirement_main_building = { LEVEL = 6 }\n\t\tx = yes\n\t}\n}\nb = { building_requirement_main_building = { LEVEL = 6 } }\n",
            ctx.Workspace.Get("common/buildings/b.txt"));
        Assert.Equal(new[] { "2 Stellen ersetzt" }, ctx.Workspace.NotesOf("common/buildings/b.txt"));
    }

    [Fact]
    public void Multi_line_replace_leaves_commented_text_alone_and_a_second_run_changes_nothing()
    {
        using var mod = new TempMod();
        const string Old = "old_trigger = { LEVEL = 1 }";
        const string Text = "a = {\n\t# old_trigger =\n\t# {\n\t#\tLEVEL = 1\n\t# }\n\told_trigger =\n\t{\n\t\tLEVEL = 1\n\t} # alt\n}\n";
        mod.WriteMod("x.txt", Text, bom: false);
        var ctx = Context(mod);
        var rule = ReplaceRule("x.txt", Old, "new_trigger = { LEVEL = 2 }");

        LineRuleApplier.Apply(rule, ctx);
        var first = ctx.Workspace.Get("x.txt")!;
        LineRuleApplier.Apply(rule, ctx);

        Assert.Equal("a = {\n\t# old_trigger =\n\t# {\n\t#\tLEVEL = 1\n\t# }\n\tnew_trigger = { LEVEL = 2 } # alt\n}\n", first);
        Assert.Equal(first, ctx.Workspace.Get("x.txt"));
    }

    [Fact]
    public void Comment_prefixes_hash_and_skips_already_commented_lines()
    {
        using var mod = new TempMod();
        mod.WriteMod("history/provinces/k_x.txt", "1 = {\n\tspecial_building_slot = a_01\n\t#special_building_slot = a_01\n}\n", bom: false);
        var ctx = Context(mod, "a_01");

        LineRuleApplier.Apply(Comment("history/provinces/*.txt"), ctx);

        Assert.Equal("1 = {\n\t#special_building_slot = a_01\n\t#special_building_slot = a_01\n}\n", ctx.Workspace.Get("history/provinces/k_x.txt"));
        Assert.Equal(new[] { "1 Zeilen auskommentiert" }, ctx.Workspace.NotesOf("history/provinces/k_x.txt"));
    }

    [Fact]
    public void Comment_splits_a_line_shared_with_other_code()
    {
        using var mod = new TempMod();
        mod.WriteMod("history/provinces/k_x.txt", "1785 = {\n\t867.1.1 = { special_building_slot = x }\n}\n", bom: false);
        var ctx = Context(mod, "x");

        LineRuleApplier.Apply(Comment("history/provinces/*.txt"), ctx);

        var text = ctx.Workspace.Get("history/provinces/k_x.txt")!;
        Assert.Equal("1785 = {\n\t867.1.1 = {\n\t\t#special_building_slot = x\n\t}\n}\n", text);
        ScriptDocument.Parse(text);
    }

    [Fact]
    public void Text_match_is_whole_word_and_whitespace_tolerant()
    {
        using var mod = new TempMod();
        mod.WriteMod("h/a.txt", "special_building_slot   =  angkor_wat_010\nspecial_building_slot=angkor_wat_01\n", bom: false);
        var ctx = Context(mod, "angkor_wat_01");

        LineRuleApplier.Apply(Comment("h/*.txt"), ctx);

        Assert.Equal("special_building_slot   =  angkor_wat_010\n#special_building_slot=angkor_wat_01\n", ctx.Workspace.Get("h/a.txt"));
    }

    [Fact]
    public void Comment_handles_two_matches_on_one_line_in_one_run()
    {
        using var mod = new TempMod();
        mod.WriteMod("h/a.txt", "1 = {\n\t867.1.1 = { special_building_slot = a_01 special_building = a_01 }\n}\n", bom: false);
        var ctx = Context(mod, "a_01");
        var rule = new LineRule { Id = "c", Files = "h/*.txt", Match = "special_building(_slot)? = {key}", MatchKind = MatchKind.Regex, Action = LineAction.Comment };

        LineRuleApplier.Apply(rule, ctx);
        var first = ctx.Workspace.Get("h/a.txt")!;
        LineRuleApplier.Apply(rule, ctx);

        Assert.Equal("1 = {\n\t867.1.1 = {\n\t\t#special_building_slot = a_01\n\t\t#special_building = a_01\n\t}\n}\n", first);
        Assert.Equal(first, ctx.Workspace.Get("h/a.txt"));
        ScriptDocument.Parse(first);
    }

    [Fact]
    public void Delete_removes_every_match_on_a_line()
    {
        using var mod = new TempMod();
        mod.WriteMod("h/a.txt", "l = { a_01 b_01 c_01 }\n", bom: false);
        var ctx = Context(mod, "a_01", "b_01");

        LineRuleApplier.Apply(new LineRule { Id = "d", Files = "h/*.txt", Match = "{key}", Action = LineAction.Delete }, ctx);

        Assert.Equal("l = { c_01 }\n", ctx.Workspace.Get("h/a.txt"));
    }

    [Fact]
    public void Line_rules_leave_generated_regions_of_the_recipe_alone()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/x.txt", "a = { any_special_building_province = { } }\n# PMT-Patch r/defs begin\n# PMT-Unit k\nk = { any_special_building_province = { } }\n# PMT-Patch r/defs end\n", bom: false);
        var ctx = Context(mod);

        LineRuleApplier.Apply(new LineRule { Id = "it", Scope = RuleScope.Global, Files = "common/*.txt", Match = "any_special_building_province", Action = LineAction.Replace, Replacement = "any_province" }, ctx);

        Assert.Equal("a = { any_province = { } }\n# PMT-Patch r/defs begin\n# PMT-Unit k\nk = { any_special_building_province = { } }\n# PMT-Patch r/defs end\n", ctx.Workspace.Get("common/x.txt"));
    }

    [Fact]
    public void Windows_1252_file_keeps_its_special_characters()
    {
        using var mod = new TempMod();
        var path = mod.ModPath("history/provinces/k_s.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var cp1252 = System.Text.Encoding.GetEncoding(1252);
        File.WriteAllBytes(path, cp1252.GetBytes("# Stockholm, Södermanland\r\n1 = {\r\n\tspecial_building_slot = a_01\r\n}\r\n"));
        var ctx = Context(mod, "a_01");

        LineRuleApplier.Apply(Comment("history/provinces/*.txt"), ctx);

        Assert.StartsWith("# Stockholm, Södermanland", ctx.Workspace.Get("history/provinces/k_s.txt"));
        Assert.Equal("windows-1252", ctx.Workspace.EncodingNameOf("history/provinces/k_s.txt"));
    }

    [Fact]
    public void Text_after_a_hash_is_not_matched()
    {
        using var mod = new TempMod();
        mod.WriteMod("h/a.txt", "x = 1 # special_building_slot = a_01\n", bom: false);
        var ctx = Context(mod, "a_01");

        LineRuleApplier.Apply(Comment("h/*.txt"), ctx);

        Assert.Equal("x = 1 # special_building_slot = a_01\n", ctx.Workspace.Get("h/a.txt"));
        Assert.DoesNotContain("h/a.txt", ctx.Workspace.Touched);
    }

    [Fact]
    public void Replace_global_changes_iterators_and_second_run_changes_nothing()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/t.txt", "a = { every_special_building_province = { } }\n", bom: false);
        var ctx = Context(mod);
        var rule = new LineRule { Id = "r", Files = "common/*.txt", Match = "every_special_building_province", Action = LineAction.Replace, Replacement = "every_province", Scope = RuleScope.Global };

        LineRuleApplier.Apply(rule, ctx);
        var first = ctx.Workspace.Get("common/t.txt");
        LineRuleApplier.Apply(rule, ctx);

        Assert.Equal("a = { every_province = { } }\n", first);
        Assert.Equal(first, ctx.Workspace.Get("common/t.txt"));
        Assert.Equal(new[] { "1 Stellen ersetzt" }, ctx.Workspace.NotesOf("common/t.txt"));
    }

    [Fact]
    public void Self_matching_replacement_is_warned()
    {
        var rule = new LineRule { Id = "r", Name = "Provinz", Match = "province", Action = LineAction.Replace, Replacement = "big province" };
        Assert.Contains(LineRuleValidation.Warnings(rule), w => w.Contains("nicht wiederholbar", StringComparison.Ordinal));
        rule.Replacement = "every_county";
        Assert.Empty(LineRuleValidation.Warnings(rule));
    }

    [Fact]
    public void Delete_removes_line_left_blank()
    {
        using var mod = new TempMod();
        mod.WriteMod("h/a.txt", "1 = {\n\tspecial_building = a_01\n\tx = 1\n}\n", bom: false);
        var ctx = Context(mod, "a_01");

        LineRuleApplier.Apply(new LineRule { Id = "d", Files = "h/*.txt", Match = "special_building = {key}", Action = LineAction.Delete }, ctx);

        Assert.Equal("1 = {\n\tx = 1\n}\n", ctx.Workspace.Get("h/a.txt"));
    }

    [Fact]
    public void RemoveBlock_follows_next_building_chain()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/buildings/00_special_buildings.txt", "# Kopf\na_01 = { next_building = a_02 }\n\n# zu a_02\na_02 = { x = 1 }\nb_01 = { }\n", bom: false);
        var ctx = Context(mod, "a_01");

        LineRuleApplier.Apply(new LineRule { Id = "rb", Files = "common/buildings/00_special_buildings.txt", Match = "{key}", Action = LineAction.RemoveBlock, FollowNext = true }, ctx);

        Assert.Equal("# Kopf\n\n# zu a_02\nb_01 = { }\n", ctx.Workspace.Get("common/buildings/00_special_buildings.txt"));
        Assert.Equal(new[] { "2 Blöcke entfernt" }, ctx.Workspace.NotesOf("common/buildings/00_special_buildings.txt"));
    }

    [Fact]
    public void RemoveBlock_removes_every_definition_of_a_doubled_key()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/buildings/00_special_buildings.txt", "a_01 = { x = 1 }\nb_01 = { }\na_01 = { x = 2 }\n", bom: false);
        var ctx = Context(mod, "a_01");

        LineRuleApplier.Apply(new LineRule { Id = "rb", Files = "common/buildings/00_special_buildings.txt", Match = "{key}", Action = LineAction.RemoveBlock }, ctx);

        Assert.Equal("b_01 = { }\n", ctx.Workspace.Get("common/buildings/00_special_buildings.txt"));
        Assert.Equal(new[] { "2 Blöcke entfernt" }, ctx.Workspace.NotesOf("common/buildings/00_special_buildings.txt"));
    }

    [Fact]
    public void RemoveBlock_on_broken_file_is_skipped_with_warning()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/b.txt", "a_01 = {\n", bom: false);
        var ctx = Context(mod, "a_01");

        LineRuleApplier.Apply(new LineRule { Id = "rb", Files = "common/b.txt", Match = "{key}", Action = LineAction.RemoveBlock }, ctx);

        Assert.Equal("a_01 = {\n", ctx.Workspace.Get("common/b.txt"));
        Assert.Contains(ctx.Warnings, w => w.StartsWith("common/b.txt übersprungen", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateCopy_copies_only_vanilla_files_with_a_match()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_a.txt", "1 = { special_building_slot = a_01 }\n");
        mod.WriteVanilla("history/provinces/k_b.txt", "2 = { culture = x }\n");
        var ctx = Context(mod, "a_01");
        var rule = Comment("history/provinces/*.txt");
        rule.CreateCopy = true;

        LineRuleApplier.Apply(rule, ctx);

        Assert.True(ctx.Workspace.IsCreatedFromVanilla("history/provinces/k_a.txt"));
        Assert.Null(ctx.Workspace.Get("history/provinces/k_b.txt"));
        Assert.Contains("#special_building_slot = a_01", ctx.Workspace.Get("history/provinces/k_a.txt"));
        Assert.Contains("Kopie aus Vanilla angelegt", ctx.Workspace.NotesOf("history/provinces/k_a.txt"));
    }

    [Fact]
    public void Rows_with_errors_are_skipped()
    {
        using var mod = new TempMod();
        mod.WriteMod("h/a.txt", "special_building_slot = a_01\n", bom: false);
        var ctx = Context(mod, "a_01", "a_01");

        LineRuleApplier.Apply(Comment("h/*.txt"), ctx);

        Assert.Equal("#special_building_slot = a_01\n", ctx.Workspace.Get("h/a.txt"));
        Assert.Contains(ctx.Skipped, s => s.RuleId == "c" && s.RowKey == "a_01" && s.Reason == "Doppelter Schlüssel");
    }

    [Fact]
    public void Empty_key_row_changes_no_file()
    {
        using var mod = new TempMod();
        const string text = "1 = {\n\tspecial_building_slot = a_01\n\tspecial_building_slot = b_01\n}\n";
        mod.WriteMod("h/a.txt", text, bom: false);
        var ctx = Context(mod, "");

        LineRuleApplier.Apply(Comment("h/*.txt"), ctx);

        Assert.Equal(text, ctx.Workspace.Get("h/a.txt"));
        Assert.Contains(ctx.Skipped, s => s.RowKey == "" && s.Reason == "Schlüssel fehlt");
    }

    [Fact]
    public void Row_with_empty_placeholder_value_is_skipped_for_that_rule()
    {
        using var mod = new TempMod();
        const string text = "b = { barony = title:b_x }\n";
        mod.WriteMod("h/a.txt", text, bom: false);
        var recipe = new PatchRecipe { Id = "r", Columns = { new PatchColumn { Name = "barony" } } };
        recipe.Rows.Add(new PatchRow { Key = "holy_01", Values = { ["barony"] = "" } });
        var workspace = new PatchWorkspace(mod.ModRoot, mod.VanillaRoot);
        var ctx = new RuleRunContext(recipe, workspace, ColumnResolver.Resolve(recipe, workspace), new PatchMemory());

        LineRuleApplier.Apply(new LineRule { Id = "c", Files = "h/*.txt", Match = "barony = title:{barony}", Action = LineAction.Comment }, ctx);

        Assert.Equal(text, ctx.Workspace.Get("h/a.txt"));
        Assert.Contains(ctx.Skipped, s => s.RuleId == "c" && s.RowKey == "holy_01" && s.Reason.Contains("barony", StringComparison.Ordinal));
    }

    [Fact]
    public void Crlf_and_bom_survive_a_comment_edit()
    {
        using var mod = new TempMod();
        mod.WriteMod("h/a.txt", "1785 = {\r\n\t867.1.1 = { special_building_slot = x }\r\n}\r\n");
        var ctx = Context(mod, "x");

        LineRuleApplier.Apply(Comment("h/*.txt"), ctx);

        var text = ctx.Workspace.Get("h/a.txt")!;
        Assert.Equal("1785 = {\r\n\t867.1.1 = {\r\n\t\t#special_building_slot = x\r\n\t}\r\n}\r\n", text);
        Assert.True(ctx.Workspace.HasBomOf("h/a.txt"));
    }
}
