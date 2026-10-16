using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class RecipeEditingTests
{
    [Fact]
    public void Add_file_appends_when_rule_has_no_wildcard_or_already_copies()
    {
        var single = new LineRule { Id = "a", Files = "events/x.txt" };
        var copying = new LineRule { Id = "b", Files = "history/provinces/*.txt", CreateCopy = true };
        var empty = new LineRule { Id = "c" };
        var recipe = new PatchRecipe { Rules = new() { single, copying, empty } };

        Assert.Same(single, RecipeEditing.AddFileToLineRule(recipe, single, "events/a.txt"));
        Assert.Equal("events/x.txt;events/a.txt", single.Files);
        Assert.True(single.CreateCopy);

        Assert.Same(copying, RecipeEditing.AddFileToLineRule(recipe, copying, "events/a.txt"));
        Assert.Equal("history/provinces/*.txt;events/a.txt", copying.Files);
        RecipeEditing.AddFileToLineRule(recipe, copying, "history/provinces/k.txt");
        Assert.Equal("history/provinces/*.txt;events/a.txt", copying.Files);

        RecipeEditing.AddFileToLineRule(recipe, empty, "events/b.txt");
        Assert.Equal("events/b.txt", empty.Files);
        Assert.Equal(3, recipe.Rules.Count);
    }

    [Fact]
    public void Add_file_to_wildcard_rule_without_copy_makes_a_narrow_sibling()
    {
        var rule = new LineRule
        {
            Id = "slots", Name = "Slots", Files = "history/provinces/*.txt", Match = "special_building_slot = {key}",
            Action = LineAction.Comment, Scope = RuleScope.PerRow,
        };
        var after = new LineRule { Id = "other" };
        var recipe = new PatchRecipe { Rules = new() { rule, after } };

        var sibling = RecipeEditing.AddFileToLineRule(recipe, rule, "events/a.txt");

        Assert.NotSame(rule, sibling);
        Assert.Equal("history/provinces/*.txt", rule.Files);
        Assert.False(rule.CreateCopy);
        Assert.Equal(new PatchRule[] { rule, sibling, after }, recipe.Rules.ToArray());
        Assert.Equal("events/a.txt", sibling.Files);
        Assert.True(sibling.CreateCopy);
        Assert.Equal(rule.Match, sibling.Match);
        Assert.Equal(rule.Action, sibling.Action);
        Assert.Equal(RuleScope.PerRow, sibling.Scope);
        Assert.True(sibling.Enabled);
        Assert.NotEqual(rule.Id, sibling.Id);
    }

    [Fact]
    public void Adding_one_file_copies_only_that_file()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_a.txt", "has_special_building = yes\n");
        mod.WriteVanilla("history/provinces/k_b.txt", "has_special_building = yes\n");
        mod.WriteVanilla("events/a.txt", "has_special_building = yes\n");
        var rule = new LineRule { Id = "l", Scope = RuleScope.Global, Files = "history/provinces/*.txt", Match = "has_special_building", Action = LineAction.Comment };
        var recipe = new PatchRecipe { Id = "r", Rules = new() { rule } };

        RecipeEditing.AddFileToLineRule(recipe, rule, "events/a.txt");
        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);

        Assert.Equal(new[] { "events/a.txt" }, plan.Files.Where(f => f.CreatedFromVanilla).Select(f => f.RelativePath).ToArray());
    }

    [Fact]
    public void New_rule_from_hit_starts_disabled()
    {
        var recipe = new PatchRecipe { Id = "r" };

        var global = RecipeEditing.NewLineRuleForHit(recipe, "events/a.txt", new WatchLine(3, "every_special_building_province", false, "every_special_building_province = {"));
        var perRow = RecipeEditing.NewLineRuleForHit(recipe, "events/b.txt", new WatchLine(1, "angkor_wat_01", true, "x = angkor_wat_01"));

        Assert.False(global.Enabled);
        Assert.Equal(RuleScope.Global, global.Scope);
        Assert.Equal("every_special_building_province", global.Match);
        Assert.Equal("events/a.txt", global.Files);
        Assert.True(global.CreateCopy);
        Assert.Equal(LineAction.Comment, global.Action);
        Assert.Equal("{key}", perRow.Match);
        Assert.Equal(RuleScope.PerRow, perRow.Scope);
        Assert.False(perRow.Enabled);
        Assert.Equal(new[] { global, perRow }, recipe.Rules.ToArray());
        Assert.NotEqual(global.Id, perRow.Id);
    }

    [Fact]
    public void Ignore_replaces_and_unignore_removes()
    {
        var recipe = new PatchRecipe();

        RecipeEditing.Ignore(recipe, "events/a.txt", "x");
        RecipeEditing.Ignore(recipe, "Events\\A.txt", "y");
        Assert.Equal("y", Assert.Single(recipe.Watch.Ignored).Note);

        RecipeEditing.Unignore(recipe, "EVENTS/a.txt");
        Assert.Empty(recipe.Watch.Ignored);
    }

    [Fact]
    public void IgnoreFolder_ignores_the_folder_of_the_file_and_unignore_removes_the_folder_rule()
    {
        var recipe = new PatchRecipe();

        var pattern = RecipeEditing.IgnoreFolder(recipe, "Common/customizable_localization/a.txt", "n");
        RecipeEditing.IgnoreFolder(recipe, "common/customizable_localization/b.txt", "neu");

        Assert.Equal("Common/customizable_localization/**/*", pattern);
        Assert.Equal("neu", Assert.Single(recipe.Watch.Ignored).Note);
        Assert.True(RecipeEditing.IsIgnoredBy(recipe.Watch.Ignored[0], "common/customizable_localization/x/y.txt"));
        Assert.False(RecipeEditing.IsIgnoredBy(recipe.Watch.Ignored[0], "common/other/y.txt"));

        RecipeEditing.Unignore(recipe, "common/customizable_localization/zzz.txt");
        Assert.Empty(recipe.Watch.Ignored);
        Assert.Null(RecipeEditing.IgnoreFolder(recipe, "root.txt", "x"));
    }

    [Fact]
    public void Watch_patterns_are_trimmed_and_unique()
    {
        var recipe = new PatchRecipe();

        RecipeEditing.SetWatchPatterns(recipe, new[] { " has_special_building ", "", "  ", "add_special_building", "has_special_building" });

        Assert.Equal(new[] { "has_special_building", "add_special_building" }, recipe.Watch.Patterns.ToArray());
    }

    [Fact]
    public void New_id_is_a_safe_unique_slug()
    {
        Assert.Equal("special_buildings_neu", RecipeEditing.NewId("Special/Buildings: neu?", Array.Empty<string>()));
        Assert.Equal("special_buildings_neu_2", RecipeEditing.NewId("Special/Buildings: neu?", new[] { "special_buildings_neu" }));
        Assert.Equal("special_buildings_neu_3", RecipeEditing.NewId("Special/Buildings: neu?", new[] { "special_buildings_neu", "special_buildings_neu_2" }));
        Assert.Equal("rezept", RecipeEditing.NewId("???", Array.Empty<string>()));
        Assert.Equal("geb_ude", RecipeEditing.NewId("Gebäude", Array.Empty<string>()));
    }

    [Fact]
    public void Rename_keeps_id_and_file()
    {
        using var mod = new TempMod();
        var recipe = RecipeEditing.Create("A", "ck3", Array.Empty<string>());
        PatchRecipeStore.Save(PatchRecipeStore.Folder(mod.ModRoot), recipe);
        recipe.Name = "B";
        PatchRecipeStore.Save(PatchRecipeStore.Folder(mod.ModRoot), recipe);

        var loaded = Assert.Single(PatchRecipeStore.LoadAll(PatchRecipeStore.Folder(mod.ModRoot), new List<string>()));
        Assert.Equal(recipe.Id, loaded.Id);
        Assert.Equal("B", loaded.Name);
        Assert.Equal("ck3", loaded.GameId);
        Assert.Single(Directory.GetFiles(PatchRecipeStore.Folder(mod.ModRoot), "*.json"));
    }

    [Fact]
    public void New_rule_ids_are_unique_per_kind()
    {
        var recipe = RecipeEditing.Create("A", "ck3", Array.Empty<string>());
        var first = RecipeEditing.NewRule(recipe, "lines", "x");
        recipe.Rules.Add(first);
        var second = RecipeEditing.NewRule(recipe, "lines", "y");

        Assert.IsType<LineRule>(first);
        Assert.Equal("lines1", first.Id);
        Assert.Equal("lines2", second.Id);
        Assert.IsType<BlockCopyRule>(RecipeEditing.NewRule(recipe, "blockCopy", "b"));
        Assert.IsType<TemplateRule>(RecipeEditing.NewRule(recipe, "template", "t"));
        Assert.IsType<ListEntryRule>(RecipeEditing.NewRule(recipe, "listEntry", "l"));
        Assert.True(second.Enabled);
    }

    [Fact]
    public void Move_is_clamped()
    {
        var recipe = RecipeEditing.Create("A", "ck3", Array.Empty<string>());
        recipe.Rules.Add(new LineRule { Id = "a" });
        recipe.Rules.Add(new LineRule { Id = "b" });
        recipe.Rules.Add(new LineRule { Id = "c" });

        RecipeEditing.Move(recipe, "c", -1);
        Assert.Equal(new[] { "a", "c", "b" }, recipe.Rules.Select(r => r.Id));
        RecipeEditing.Move(recipe, "a", -5);
        Assert.Equal(new[] { "a", "c", "b" }, recipe.Rules.Select(r => r.Id));
        RecipeEditing.Move(recipe, "a", 9);
        Assert.Equal(new[] { "c", "b", "a" }, recipe.Rules.Select(r => r.Id));
    }

    [Fact]
    public void Rule_warnings_list_unknown_placeholders_and_self_match()
    {
        var recipe = RecipeEditing.Create("A", "ck3", Array.Empty<string>());
        recipe.Columns.Add(new PatchColumn { Name = "barony" });

        var warning = Assert.Single(RecipeEditing.RuleWarnings(recipe, new TemplateRule { Name = "T", Text = "{barony} {nope} {key}" }));
        Assert.Contains("nope", warning);
        Assert.Contains(RecipeEditing.RuleWarnings(recipe, new LineRule { Name = "L", Match = "province", Action = LineAction.Replace, Replacement = "big province" }), w => w.Contains("nicht wiederholbar", StringComparison.Ordinal));
        Assert.Empty(RecipeEditing.RuleWarnings(recipe, new BlockCopyRule { Name = "B", Edits = { new BlockEdit { Text = "x = {barony}" } } }));
    }

    [Fact]
    public void Column_name_problems()
    {
        var recipe = RecipeEditing.Create("A", "ck3", Array.Empty<string>());
        recipe.Columns.Add(new PatchColumn { Name = "barony" });

        Assert.Equal(PatchTexts.ColumnNameInvalid, RecipeEditing.ColumnNameProblem(recipe, "1a", null));
        Assert.Equal(PatchTexts.ColumnNameInvalid, RecipeEditing.ColumnNameProblem(recipe, "a b", null));
        Assert.Equal(PatchTexts.ColumnNameInvalid, RecipeEditing.ColumnNameProblem(recipe, "a\n", null));
        Assert.Equal(PatchTexts.ColumnNameReserved, RecipeEditing.ColumnNameProblem(recipe, "key", null));
        Assert.Equal(PatchTexts.ColumnNameTaken, RecipeEditing.ColumnNameProblem(recipe, "barony", null));
        Assert.Null(RecipeEditing.ColumnNameProblem(recipe, "barony", "barony"));
        Assert.Null(RecipeEditing.ColumnNameProblem(recipe, "holdings", null));
    }

    [Fact]
    public void Single_rule_preview_keeps_one_rule_and_one_row()
    {
        var recipe = RecipeEditing.Create("A", "ck3", Array.Empty<string>());
        recipe.Rows.Add(new PatchRow { Key = "a" });
        recipe.Rows.Add(new PatchRow { Key = "b" });
        recipe.Rules.Add(new LineRule { Id = "x" });
        recipe.Rules.Add(new TemplateRule { Id = "y", Enabled = false });

        var preview = RecipeEditing.SingleRulePreview(recipe, "y", "b");

        Assert.Equal(recipe.Id, preview.Id);
        Assert.Equal("b", preview.OnlyRowKey);
        Assert.Equal("b", PatchRecipeStore.Clone(preview).OnlyRowKey);
        Assert.Null(recipe.OnlyRowKey);
        Assert.True(preview.Rules.Single(r => r.Id == "y").Enabled);
        Assert.False(preview.Rules.Single(r => r.Id == "x").Enabled);
        Assert.Equal(2, recipe.Rows.Count);
        Assert.False(recipe.Rules.Single(r => r.Id == "y").Enabled);
    }

    [Fact]
    public void Table_filter_matches_key_values_and_errors()
    {
        var ok = new ResolvedRow { Key = "angkor_wat_01", Values = new Dictionary<string, string> { ["barony"] = "b_Angkor" } };
        var bad = new ResolvedRow { Key = "x", Values = new Dictionary<string, string>() };
        bad.Errors.Add("kaputt");

        Assert.True(TableFilter.Matches(ok, "", false));
        Assert.True(TableFilter.Matches(ok, "ANGKOR_W", false));
        Assert.True(TableFilter.Matches(ok, "b_angkor", false));
        Assert.False(TableFilter.Matches(ok, "zzz", false));
        Assert.False(TableFilter.Matches(ok, "", true));
        Assert.True(TableFilter.Matches(bad, "", true));
    }
}
