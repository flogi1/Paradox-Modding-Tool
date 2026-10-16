using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>Strukturprüfung eines Rezepts (Import, Fenster, Kommandozeile): Fehler machen es unbrauchbar, Warnungen nicht.</summary>
public sealed class RecipeCheckerTests
{
    private static PatchRecipe Valid() => new()
    {
        Id = "r",
        Name = "Test",
        GameId = "ck3",
        Columns = { new PatchColumn { Name = "castle" } },
        Rows = { new PatchRow { Key = "k1", Values = { ["castle"] = "x" } } },
        Rules = { new TemplateRule { Id = "template1", Name = "Vorlage", Target = "common/x.txt", Text = "{key} = { }" } },
    };

    private static IReadOnlyList<RecipeIssue> Check(PatchRecipe recipe, RecipeCheckOptions? options = null) => RecipeChecker.Check(recipe, options);

    private static void AssertIssue(IReadOnlyList<RecipeIssue> issues, CheckSeverity severity, string where) =>
        Assert.True(issues.Any(i => i.Severity == severity && i.Where == where && i.Message.Length > 0),
            $"erwartet {severity} bei '{where}', gefunden: " + string.Join(" | ", issues.Select(i => $"{i.Severity} {i.Where}: {i.Message}")));

    [Fact]
    public void Valid_recipe_has_no_issues()
    {
        Assert.Empty(Check(Valid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Mein Rezept")]
    [InlineData("A")]
    [InlineData("a-b")]
    [InlineData("r\n")]
    public void Invalid_recipe_id_is_error(string id)
    {
        var recipe = Valid();
        recipe.Id = id;
        AssertIssue(Check(recipe), CheckSeverity.Error, "recipe");
    }

    [Fact]
    public void Empty_rule_id_is_error()
    {
        var recipe = Valid();
        recipe.Rules[0].Id = string.Empty;
        AssertIssue(Check(recipe), CheckSeverity.Error, "rule ");
    }

    [Fact]
    public void Duplicate_rule_id_is_error()
    {
        var recipe = Valid();
        recipe.Rules.Add(new TemplateRule { Id = "template1", Target = "common/y.txt", Text = "x" });
        var issues = Check(recipe);
        AssertIssue(issues, CheckSeverity.Error, "rule template1");
        Assert.Single(issues, i => i.Severity == CheckSeverity.Error);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a|b")]
    [InlineData("a b")]
    [InlineData("ä")]
    [InlineData("t1\n")]
    public void Rule_id_with_bad_characters_is_error(string id)
    {
        var recipe = Valid();
        recipe.Rules[0].Id = id;
        AssertIssue(Check(recipe), CheckSeverity.Error, "rule " + id);
    }

    [Theory]
    [InlineData("common/x.txt;")]
    [InlineData("common/*.txt")]
    [InlineData("common/x.txt ")]
    public void Target_that_is_no_single_file_is_one_error(string target)
    {
        var recipe = Valid();
        ((TemplateRule)recipe.Rules[0]).Target = target;
        var issues = Check(recipe);
        Assert.Equal(new[] { new RecipeIssue(CheckSeverity.Error, "rule template1", PatchTexts.TargetNotSingleFile(target)) }, issues);
    }

    [Fact]
    public void Unsafe_target_is_not_also_reported_as_no_single_file()
    {
        var recipe = Valid();
        ((TemplateRule)recipe.Rules[0]).Target = "../x.txt;";
        Assert.Equal(new[] { new RecipeIssue(CheckSeverity.Error, "rule template1", PatchTexts.UnsafePath("../x.txt;")) }, Check(recipe));
    }

    [Fact]
    public void Mixed_case_rule_id_is_fine()
    {
        var recipe = Valid();
        recipe.Rules[0].Id = "Trigger_Level_01";
        Assert.Empty(Check(recipe));
    }

    [Fact]
    public void Pmt_marker_line_in_template_text_is_error()
    {
        var recipe = Valid();
        ((TemplateRule)recipe.Rules[0]).Text = "a = 1\n  # PMT-Unit x\nb = 2";
        AssertIssue(Check(recipe), CheckSeverity.Error, "rule template1");
    }

    [Fact]
    public void Pmt_marker_line_in_edit_text_is_error()
    {
        var recipe = Valid();
        recipe.Rules.Add(new BlockCopyRule
        {
            Id = "blockCopy1", SourceFiles = "common/a.txt", Target = "common/b.txt",
            Edits = { new BlockEdit { Kind = BlockEditKind.InsertAtEnd, Text = "# PMT-Patch r/x begin" } },
        });
        AssertIssue(Check(recipe), CheckSeverity.Error, "rule blockCopy1");
    }

    [Fact]
    public void Pmt_in_the_middle_of_a_line_is_fine()
    {
        var recipe = Valid();
        ((TemplateRule)recipe.Rules[0]).Text = "a = 1 # PMT-Unit";
        Assert.Empty(Check(recipe));
    }

    [Theory]
    [InlineData("1bad")]
    [InlineData("a-b")]
    [InlineData("key")]
    [InlineData("")]
    public void Column_name_problem_is_error(string name)
    {
        var recipe = Valid();
        recipe.Columns.Add(new PatchColumn { Name = name });
        AssertIssue(Check(recipe), CheckSeverity.Error, "column " + name);
    }

    [Fact]
    public void Duplicate_column_is_error()
    {
        var recipe = Valid();
        recipe.Columns.Add(new PatchColumn { Name = "castle" });
        AssertIssue(Check(recipe), CheckSeverity.Error, "column castle");
    }

    [Fact]
    public void Lookup_with_empty_files_is_error()
    {
        var recipe = Valid();
        recipe.Columns.Add(new PatchColumn { Name = "prov", Lookup = new DerivedLookup { Files = " ", Match = "slot = {key}" } });
        AssertIssue(Check(recipe), CheckSeverity.Error, "column prov");
    }

    [Fact]
    public void Lookup_with_empty_match_is_error()
    {
        var recipe = Valid();
        recipe.Columns.Add(new PatchColumn { Name = "prov", Lookup = new DerivedLookup { Files = "history/*.txt", Match = string.Empty } });
        AssertIssue(Check(recipe), CheckSeverity.Error, "column prov");
    }

    [Fact]
    public void Unsafe_lookup_files_is_error()
    {
        var recipe = Valid();
        recipe.Columns.Add(new PatchColumn { Name = "prov", Lookup = new DerivedLookup { Files = "../x/*.txt", Match = "slot = {key}" } });
        AssertIssue(Check(recipe), CheckSeverity.Error, "column prov");
    }

    [Fact]
    public void Empty_row_key_is_error()
    {
        var recipe = Valid();
        recipe.Rows.Add(new PatchRow { Key = " " });
        AssertIssue(Check(recipe), CheckSeverity.Error, "row  ");
    }

    [Fact]
    public void Duplicate_row_key_is_error()
    {
        var recipe = Valid();
        recipe.Rows.Add(new PatchRow { Key = "k1" });
        AssertIssue(Check(recipe), CheckSeverity.Error, "row k1");
    }

    [Fact]
    public void Row_value_for_unknown_column_is_error()
    {
        var recipe = Valid();
        recipe.Rows[0].Values["nope"] = "1";
        AssertIssue(Check(recipe), CheckSeverity.Error, "row k1");
    }

    [Fact]
    public void Row_value_for_derived_column_is_error()
    {
        var recipe = Valid();
        recipe.Columns.Add(new PatchColumn { Name = "prov", Lookup = new DerivedLookup { Files = "history/*.txt", Match = "slot = {key}" } });
        recipe.Rows[0].Values["prov"] = "1";
        AssertIssue(Check(recipe), CheckSeverity.Error, "row k1");
    }

    public static IEnumerable<object[]> EmptyRequired()
    {
        yield return new object[] { new LineRule { Id = "lines1", Files = "a.txt", Match = " " } };
        yield return new object[] { new BlockCopyRule { Id = "lines1", SourceFiles = "", BlockKey = "{key}", Target = "t.txt" } };
        yield return new object[] { new BlockCopyRule { Id = "lines1", SourceFiles = "a.txt", BlockKey = "", Target = "t.txt" } };
        yield return new object[] { new BlockCopyRule { Id = "lines1", SourceFiles = "a.txt", BlockKey = "{key}", Target = "" } };
        yield return new object[] { new TemplateRule { Id = "lines1", Target = "", Text = "x" } };
        yield return new object[] { new TemplateRule { Id = "lines1", Target = "t.txt", Text = "" } };
        yield return new object[] { new ListEntryRule { Id = "lines1", Target = "", ListPath = "a", Entry = "b" } };
        yield return new object[] { new ListEntryRule { Id = "lines1", Target = "t.txt", ListPath = "", Entry = "b" } };
        yield return new object[] { new ListEntryRule { Id = "lines1", Target = "t.txt", ListPath = "a", Entry = "" } };
        yield return new object[] { new BlockEditRule { Id = "lines1", Files = "a.txt", BlockKey = " " } };
    }

    [Theory]
    [MemberData(nameof(EmptyRequired))]
    public void Empty_required_field_is_error(PatchRule rule)
    {
        var recipe = Valid();
        recipe.Rules.Clear();
        recipe.Rules.Add(rule);
        AssertIssue(Check(recipe), CheckSeverity.Error, "rule lines1");
    }

    public static IEnumerable<object[]> EmptyFiles()
    {
        yield return new object[] { new LineRule { Id = "lines1", Files = "", Match = "x" } };
        yield return new object[] { new LineRule { Id = "lines1", Files = " ; ", Match = "x" } };
        yield return new object[] { new BlockEditRule { Id = "lines1", Files = "", BlockKey = "{key}" } };
        yield return new object[] { new BlockEditRule { Id = "lines1", Files = ";", BlockKey = "{key}" } };
    }

    [Theory]
    [MemberData(nameof(EmptyFiles))]
    public void Empty_files_on_lines_and_blockEdit_is_warning(PatchRule rule)
    {
        // Ruling 11: eine Regel ohne Dateien tut nichts (z. B. die Iterator-Regel der Vorlage, bis der Ordner eingetragen ist), sie ist kein Fehler.
        var recipe = Valid();
        recipe.Rules.Clear();
        recipe.Rules.Add(rule);
        var issues = Check(recipe);
        Assert.DoesNotContain(issues, i => i.Severity == CheckSeverity.Error);
        Assert.Contains(issues, i => i.Severity == CheckSeverity.Warning && i.Where == "rule lines1" && i.Message == PatchTexts.CheckFilesEmpty);
    }

    [Fact]
    public void Trailing_semicolon_in_files_is_neither_unsafe_nor_empty()
    {
        var recipe = Valid();
        recipe.Rules.Add(new LineRule { Id = "lines1", Files = "common/*.txt;", Match = "x", Action = LineAction.Delete });
        Assert.Empty(Check(recipe));
    }

    [Fact]
    public void Empty_replacement_and_empty_inside_are_fine()
    {
        var recipe = Valid();
        recipe.Rules.Add(new LineRule { Id = "lines1", Files = "a.txt", Match = "x", Action = LineAction.Delete });
        Assert.Empty(Check(recipe));
    }

    [Fact]
    public void Invalid_regex_is_error()
    {
        var recipe = Valid();
        recipe.Rules.Add(new LineRule { Id = "lines1", Files = "a.txt", Match = "(unclosed", MatchKind = MatchKind.Regex });
        var issues = Check(recipe);
        AssertIssue(issues, CheckSeverity.Error, "rule lines1");
    }

    [Fact]
    public void Valid_regex_with_placeholder_is_fine()
    {
        var recipe = Valid();
        recipe.Rules.Add(new LineRule { Id = "lines1", Files = "a.txt", Match = "^\\s*{key}\\s*=", MatchKind = MatchKind.Regex });
        Assert.Empty(Check(recipe));
    }

    [Fact]
    public void Unclosed_section_in_template_is_error()
    {
        var recipe = Valid();
        ((TemplateRule)recipe.Rules[0]).Text = "{?castle}x";
        AssertIssue(Check(recipe), CheckSeverity.Error, "rule template1");
    }

    [Fact]
    public void Stray_section_end_in_other_template_fields_is_error()
    {
        var recipe = Valid();
        recipe.Rules.Add(new ListEntryRule { Id = "listEntry1", Target = "t.txt", ListPath = "a", Entry = "{key}{/castle}" });
        AssertIssue(Check(recipe), CheckSeverity.Error, "rule listEntry1");
    }

    [Fact]
    public void Unsafe_rule_path_is_error()
    {
        var recipe = Valid();
        ((TemplateRule)recipe.Rules[0]).Target = "../outside.txt";
        AssertIssue(Check(recipe), CheckSeverity.Error, "rule template1");
    }

    [Fact]
    public void Unknown_preset_is_error()
    {
        var recipe = Valid();
        recipe.Preset = "something-else";
        AssertIssue(Check(recipe), CheckSeverity.Error, "recipe");
    }

    [Theory]
    [InlineData(SpecialBuildingsPreset.Id)]
    [InlineData(SpecialResourcesPreset.Id)]
    [InlineData(null)]
    public void Known_or_missing_preset_is_fine(string? preset)
    {
        var recipe = Valid();
        recipe.Preset = preset;
        Assert.Empty(Check(recipe));
    }

    [Fact]
    public void Previous_with_other_id_is_error()
    {
        var previous = Valid();
        previous.Id = "old";
        AssertIssue(Check(Valid(), new RecipeCheckOptions(Previous: previous)), CheckSeverity.Error, "recipe");
    }

    [Fact]
    public void Previous_with_same_id_and_rules_is_fine()
    {
        Assert.Empty(Check(Valid(), new RecipeCheckOptions(Previous: Valid(), PreviousMemory: new PatchMemory())));
    }

    [Fact]
    public void Dropped_rule_with_memory_is_reported()
    {
        var previous = Valid();
        previous.Rules.Add(new TemplateRule { Id = "template2", Target = "common/z.txt", Text = "x" });
        var memory = new PatchMemory();
        memory.Units[PatchMemory.UnitId("template2", "k1")] = "x";

        Assert.Equal(new[] { "template2" }, RecipeChecker.DroppedRulesWithOutput(Valid(), previous, memory, null));
        AssertIssue(Check(Valid(), new RecipeCheckOptions(Previous: previous, PreviousMemory: memory)), CheckSeverity.Error, "rule template2");
    }

    [Fact]
    public void Dropped_rule_with_list_entries_is_reported()
    {
        var previous = Valid();
        previous.Rules.Add(new ListEntryRule { Id = "listEntry1", Target = "t.txt", ListPath = "a", Entry = "b" });
        var memory = new PatchMemory();
        memory.ListEntries["listEntry1"] = new List<ListEntryRecord> { new("t.txt", "a", "b") };

        Assert.Equal(new[] { "listEntry1" }, RecipeChecker.DroppedRulesWithOutput(Valid(), previous, memory, null));
    }

    [Fact]
    public void Dropped_rule_with_region_in_mod_is_reported()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/z.txt", "a = 1\n# PMT-Patch r/template2 begin\n# PMT-Unit k1\nx = 1\n# PMT-Patch r/template2 end\n");
        var previous = Valid();
        previous.Rules.Add(new TemplateRule { Id = "template2", Target = "common/z.txt", Text = "x" });

        Assert.Equal(new[] { "template2" }, RecipeChecker.DroppedRulesWithOutput(Valid(), previous, null, mod.ModRoot));
    }

    [Fact]
    public void Dropped_rule_without_output_is_not_reported()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/z.txt", "a = 1\n# PMT-Patch r/template20 begin\n# PMT-Patch r/template20 end\n");
        mod.WriteMod("common/z.bin", "# PMT-Patch r/template2 begin\n");
        var previous = Valid();
        previous.Rules.Add(new TemplateRule { Id = "template2", Target = "common/z.txt", Text = "x" });
        var memory = new PatchMemory();
        memory.Units[PatchMemory.UnitId("template1", "k1")] = "x";
        memory.ListEntries["template2"] = new List<ListEntryRecord>();

        Assert.Empty(RecipeChecker.DroppedRulesWithOutput(Valid(), previous, memory, mod.ModRoot));
        Assert.Empty(Check(Valid(), new RecipeCheckOptions(ModRoot: mod.ModRoot, Previous: previous, PreviousMemory: memory)));
    }

    [Fact]
    public void Kept_rule_is_not_dropped()
    {
        var memory = new PatchMemory();
        memory.Units[PatchMemory.UnitId("template1", "k1")] = "x";
        Assert.Empty(RecipeChecker.DroppedRulesWithOutput(Valid(), Valid(), memory, null));
    }

    [Fact]
    public void Unknown_properties_are_warnings_on_recipe()
    {
        var issues = Check(Valid(), new RecipeCheckOptions(UnknownProperties: new[] { "$.rules[0]: foo" }));
        AssertIssue(issues, CheckSeverity.Warning, "recipe");
        Assert.Contains("foo", Assert.Single(issues).Message);
    }

    [Fact]
    public void Unknown_placeholder_is_warning()
    {
        var recipe = Valid();
        ((TemplateRule)recipe.Rules[0]).Text = "{key} = {castel}";
        var issues = Check(recipe);
        AssertIssue(issues, CheckSeverity.Warning, "rule template1");
        Assert.DoesNotContain(issues, i => i.Severity == CheckSeverity.Error);
    }

    [Fact]
    public void Self_matching_replacement_is_warning()
    {
        var recipe = Valid();
        recipe.Rules.Add(new LineRule { Id = "lines1", Files = "a.txt", Match = "x", Action = LineAction.Replace, Replacement = "x = 1" });
        AssertIssue(Check(recipe), CheckSeverity.Warning, "rule lines1");
    }

    [Fact]
    public void Invalid_regex_does_not_crash_the_warnings()
    {
        var recipe = Valid();
        recipe.Rules.Add(new LineRule { Id = "lines1", Files = "a.txt", Match = "(x", MatchKind = MatchKind.Regex, Action = LineAction.Replace, Replacement = "y" });
        Assert.Single(Check(recipe), i => i.Severity == CheckSeverity.Error);
    }

    [Fact]
    public void Glob_without_match_in_mod_and_game_is_warning()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/b/a.txt", "x");
        mod.WriteVanilla("history/p/1.txt", "x");
        var recipe = Valid();
        recipe.Rules.Add(new LineRule { Id = "lines1", Files = "common/nothing/*.txt", Match = "x" });
        recipe.Rules.Add(new LineRule { Id = "lines2", Files = "common/b/*.txt", Match = "x" });
        recipe.Rules.Add(new LineRule { Id = "lines3", Files = "history/p/*.txt", Match = "x" });
        var issues = Check(recipe, new RecipeCheckOptions(GameRoot: mod.VanillaRoot, ModRoot: mod.ModRoot));

        AssertIssue(issues, CheckSeverity.Warning, "rule lines1");
        Assert.Single(issues);
    }

    [Fact]
    public void Block_rules_source_globs_are_checked_but_targets_are_not()
    {
        using var mod = new TempMod();
        var recipe = Valid();
        recipe.Rules.Add(new BlockCopyRule { Id = "blockCopy1", SourceFiles = "common/none/*.txt", Target = "common/new.txt" });
        var issues = Check(recipe, new RecipeCheckOptions(GameRoot: mod.VanillaRoot, ModRoot: mod.ModRoot));

        AssertIssue(issues, CheckSeverity.Warning, "rule blockCopy1");
        Assert.Single(issues);
    }

    [Fact]
    public void Glob_with_placeholder_is_not_checked()
    {
        using var mod = new TempMod();
        var recipe = Valid();
        recipe.Rules.Add(new LineRule { Id = "lines1", Files = "history/{key}.txt", Match = "x" });
        Assert.Empty(Check(recipe, new RecipeCheckOptions(GameRoot: mod.VanillaRoot, ModRoot: mod.ModRoot)));
    }

    [Fact]
    public void Lookup_files_without_match_is_warning_on_column()
    {
        using var mod = new TempMod();
        var recipe = Valid();
        recipe.Columns.Add(new PatchColumn { Name = "prov", Lookup = new DerivedLookup { Files = "history/none/*.txt", Match = "slot = {key}" } });
        AssertIssue(Check(recipe, new RecipeCheckOptions(GameRoot: mod.VanillaRoot, ModRoot: mod.ModRoot)), CheckSeverity.Warning, "column prov");
    }

    [Fact]
    public void No_roots_means_no_glob_warnings()
    {
        var recipe = Valid();
        recipe.Rules.Add(new LineRule { Id = "lines1", Files = "common/nothing/*.txt", Match = "x" });
        Assert.Empty(Check(recipe));
    }

    [Fact]
    public void Regex_invalid_only_with_empty_key_is_error_and_does_not_throw()
    {
        var recipe = Valid();
        // Mit Dummy-Werten gültig ("[k]"), mit leerem {key} ungültig ("[]"): RuleWarnings würde sonst werfen.
        recipe.Rules.Add(new LineRule { Id = "lines1", Files = "a.txt", Match = "[{key}]", MatchKind = MatchKind.Regex, Action = LineAction.Replace, Replacement = "y" });
        AssertIssue(Check(recipe), CheckSeverity.Error, "rule lines1");
    }

    [Fact]
    public void Warnings_still_come_with_null_columns()
    {
        var recipe = new PatchRecipe { Id = "r", Columns = null!, Rows = null!, Rules = new List<PatchRule>() };
        recipe.Rules.Add(new TemplateRule { Id = "template1", Target = "t.txt", Text = "{key} {castel}" });
        recipe.Rules.Add(new LineRule { Id = "lines1", Files = "a.txt", Match = "x", Action = LineAction.Replace, Replacement = "x = 1" });
        var issues = Check(recipe);

        AssertIssue(issues, CheckSeverity.Warning, "rule template1");
        AssertIssue(issues, CheckSeverity.Warning, "rule lines1");
        Assert.DoesNotContain(issues, i => i.Severity == CheckSeverity.Error);
    }

    [Fact]
    public void Missing_lists_do_not_crash()
    {
        var recipe = new PatchRecipe { Id = "r", Columns = null!, Rows = null!, Rules = null! };
        Assert.Empty(Check(recipe));
    }
}
