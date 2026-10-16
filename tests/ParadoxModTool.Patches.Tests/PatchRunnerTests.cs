using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class PatchRunnerTests
{
    internal const string Specials = "common/buildings/00_special_buildings.txt";
    internal const string Generated = "common/buildings/pmt_special_buildings.txt";
    internal const string History = "history/provinces/k_x.txt";
    internal const string Holdings = "common/holdings/00_holdings.txt";
    internal const string Triggers = "common/scripted_triggers/00_travel_triggers.txt";

    internal static void WriteVanilla(TempMod mod)
    {
        mod.WriteVanilla(Specials, "angkor_wat_01 = {\n\ttype = special\n\tconstruction_time = 10\n}\n\nborobudur_01 = {\n\ttype = special\n\tconstruction_time = 20\n}\n\nother_01 = {\n\ttype = special\n}\n");
        mod.WriteVanilla(History, "100 = {\n\tspecial_building_slot = angkor_wat_01\n}\n200 = {\n\t867.1.1 = { special_building_slot = borobudur_01 }\n}\n");
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", "e_x = { k_x = { d_x = { c_x = { b_angkor = { province = 100 } b_boro = { province = 200 } } } } }\n");
        mod.WriteVanilla(Holdings, "castle_holding = {\n\tbuildings = {\n\t\tcastle_01\n\t}\n}\ncity_holding = {\n\tbuildings = {\n\t\tcity_01\n\t}\n}\n");
        mod.WriteVanilla(Triggers, "t = {\n\tany_special_building_province = { }\n\tevery_special_building_province = { }\n}\n");
    }

    internal static PatchRecipe Recipe(params string[] keys)
    {
        var recipe = new PatchRecipe
        {
            Id = "special_buildings",
            Name = "Special Buildings",
            GameId = "ck3",
            Columns =
            {
                new PatchColumn { Name = "holdings", Type = ColumnType.List, Default = "castle_holding, city_holding" },
                new PatchColumn { Name = "provinz", Lookup = new DerivedLookup { Files = "history/provinces/*.txt", Match = "special_building_slot = {key}", Take = LookupTake.TopLevelKey, Optional = true } },
                new PatchColumn { Name = "barony", TitleKey = true, Lookup = new DerivedLookup { Files = "common/landed_titles/**/*.txt", Match = "province = {provinz}", Take = LookupTake.EnclosingKey, Optional = true } },
            },
            Rules =
            {
                new LineRule { Id = "slots", Files = "history/provinces/*.txt", Match = "special_building_slot = {key}", Action = LineAction.Comment, CreateCopy = true },
                new LineRule { Id = "built", Files = "history/provinces/*.txt", Match = "special_building = {key}", Action = LineAction.Comment, CreateCopy = true },
                new LineRule { Id = "iterators", Scope = RuleScope.Global, Files = "common/**/*.txt", Match = "(every|any|random|ordered)_special_building_province", MatchKind = MatchKind.Regex, Action = LineAction.Replace, Replacement = "${1}_province", CreateCopy = true },
                new LineRule { Id = "originals", Files = Specials, Match = "{key}", Action = LineAction.RemoveBlock, FollowNext = true, CreateCopy = true },
                new BlockCopyRule
                {
                    Id = "defs",
                    SourceFiles = Specials,
                    FollowNext = true,
                    Target = Generated,
                    Edits =
                    {
                        new BlockEdit { Kind = BlockEditKind.Remove, Key = "type", Value = "special" },
                        new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "can_construct_potential", Text = "{?barony}barony = title:{barony}{/barony}" },
                    },
                },
                new ListEntryRule { Id = "holdings", Target = Holdings, ListPath = "{holdings}/buildings", Entry = "{key}", CreateCopy = true },
            },
        };
        foreach (var key in keys)
        {
            recipe.Rows.Add(new PatchRow { Key = key });
        }

        return recipe;
    }

    internal static PatchContext Context(TempMod mod, VanillaHistoryStore? history = null, string? version = null) =>
        new(mod.ModRoot, mod.VanillaRoot, version, history);

    private static PlannedFile File(PatchPlan plan, string rel) => plan.Files.Single(f => f.RelativePath == rel);

    [Fact]
    public void Special_buildings_recipe_end_to_end()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);

        var plan = PatchRunner.Plan(Context(mod), Recipe("angkor_wat_01", "borobudur_01"));

        Assert.Empty(plan.Skipped);
        Assert.All(plan.Files, f => Assert.Empty(f.Conflicts));
        Assert.Equal(new[] { Generated, Specials, Holdings, Triggers, History }.OrderBy(p => p, StringComparer.Ordinal), plan.Files.Select(f => f.RelativePath));
        Assert.All(plan.Files, f => Assert.Equal(PlannedFileStatus.New, f.Status));

        var history = File(plan, History).NewText;
        Assert.Contains("\t#special_building_slot = angkor_wat_01", history);
        Assert.Contains("\t\t#special_building_slot = borobudur_01", history);
        ScriptDocument.Parse(history);

        var holdings = File(plan, Holdings).NewText;
        Assert.Equal(2, holdings.Split("\t\tangkor_wat_01\n").Length - 1);
        Assert.Equal(2, holdings.Split("\t\tborobudur_01\n").Length - 1);

        var originals = File(plan, Specials).NewText;
        Assert.DoesNotContain("angkor_wat_01", originals);
        Assert.DoesNotContain("borobudur_01", originals);
        Assert.Contains("other_01", originals);

        var generated = File(plan, Generated).NewText;
        Assert.DoesNotContain("type = special", generated);
        Assert.Contains("barony = title:b_angkor", generated);
        Assert.Contains("barony = title:b_boro", generated);
        Assert.True(File(plan, Generated).HasBom);

        var triggers = File(plan, Triggers).NewText;
        Assert.Contains("any_province = { }", triggers);
        Assert.Contains("every_province = { }", triggers);
    }

    [Fact]
    public void Applying_the_plan_twice_gives_only_unchanged_files()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        var recipe = Recipe("angkor_wat_01", "borobudur_01");

        PatchPlanWriter.CreateAction(Context(mod), PatchRunner.Plan(Context(mod), recipe)).Do();
        var second = PatchRunner.Plan(Context(mod), recipe);

        Assert.All(second.Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
        Assert.Empty(second.Skipped);
    }

    [Fact]
    public void Notes_of_unchanged_files_reach_the_plan()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        var recipe = Recipe("angkor_wat_01", "borobudur_01");
        PatchPlanWriter.CreateAction(Context(mod), PatchRunner.Plan(Context(mod), recipe)).Do();
        var generated = mod.ReadMod(Generated);
        var unitStart = generated.IndexOf("# PMT-Unit borobudur_01", StringComparison.Ordinal);
        var unitEnd = generated.IndexOf("# PMT-Patch", unitStart, StringComparison.Ordinal);
        mod.WriteMod(Generated, generated.Remove(unitStart, unitEnd - unitStart));

        var plan = PatchRunner.Plan(Context(mod), recipe);

        var file = File(plan, Generated);
        Assert.Equal(PlannedFileStatus.Unchanged, file.Status);
        Assert.Contains(file.Notes, n => n.Contains("borobudur_01", StringComparison.Ordinal) && n.Contains("von Hand gelöscht", StringComparison.Ordinal));
    }

    [Fact]
    public void Iterators_inside_copied_blocks_are_stable_on_the_second_apply()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        mod.WriteVanilla(Specials, "angkor_wat_01 = {\n\ttype = special\n\tis_enabled = { any_special_building_province = { } }\n}\n");
        var recipe = Recipe("angkor_wat_01");

        var first = PatchRunner.Plan(Context(mod), recipe);
        Assert.Contains("any_province = { }", File(first, Generated).NewText);
        PatchPlanWriter.CreateAction(Context(mod), first).Do();
        var second = PatchRunner.Plan(Context(mod), recipe);

        Assert.All(second.Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
    }

    [Fact]
    public void Windows_1252_file_is_written_back_in_1252_and_undo_restores_the_bytes()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var cp1252 = System.Text.Encoding.GetEncoding(1252);
        var original = cp1252.GetBytes("# Södermanland\r\n100 = {\r\n\tspecial_building_slot = angkor_wat_01\r\n}\r\n");
        Directory.CreateDirectory(Path.GetDirectoryName(mod.ModPath(History))!);
        System.IO.File.WriteAllBytes(mod.ModPath(History), original);
        var recipe = Recipe("angkor_wat_01");
        foreach (var rule in recipe.Rules)
        {
            rule.Enabled = rule.Id == "slots";
        }

        var action = PatchPlanWriter.CreateAction(Context(mod), PatchRunner.Plan(Context(mod), recipe));
        action.Do();
        var written = System.IO.File.ReadAllBytes(mod.ModPath(History));
        action.Undo();

        Assert.Equal(cp1252.GetBytes("# Södermanland\r\n100 = {\r\n\t#special_building_slot = angkor_wat_01\r\n}\r\n"), written);
        Assert.Equal(original, System.IO.File.ReadAllBytes(mod.ModPath(History)));
    }

    [Fact]
    public void Undo_restores_files_and_memory()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        mod.WriteMod(Holdings, "castle_holding = {\n\tbuildings = {\n\t}\n}\n");

        var action = PatchPlanWriter.CreateAction(Context(mod), PatchRunner.Plan(Context(mod), Recipe("angkor_wat_01")));
        action.Do();
        Assert.True(mod.ModExists(".pmt-patches/special_buildings.memory.json"));
        Assert.Contains("angkor_wat_01", mod.ReadMod(Holdings));

        action.Undo();

        Assert.Equal("castle_holding = {\n\tbuildings = {\n\t}\n}\n", mod.ReadMod(Holdings));
        Assert.False(mod.ModExists(Generated));
        Assert.False(mod.ModExists(".pmt-patches/special_buildings.memory.json"));
        Assert.Equal("Patch-Rezept „Special Buildings“ anwenden", action.Description);
    }

    [Fact]
    public void Conflict_file_is_not_written_and_memory_keeps_old_units()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        var recipe = Recipe("angkor_wat_01");
        PatchPlanWriter.CreateAction(Context(mod), PatchRunner.Plan(Context(mod), recipe)).Do();
        var rememberedBefore = PatchRecipeStore.LoadMemory(PatchRecipeStore.Folder(mod.ModRoot), recipe.Id).Units["defs|angkor_wat_01"];
        var edited = mod.ReadMod(Generated).Replace("construction_time = 10", "construction_time = 99");
        mod.WriteMod(Generated, edited);
        mod.WriteVanilla(Specials, "angkor_wat_01 = {\n\ttype = special\n\tconstruction_time = 50\n}\n");

        var plan = PatchRunner.Plan(Context(mod), recipe);
        Assert.Equal(PlannedFileStatus.Conflict, File(plan, Generated).Status);
        Assert.NotEmpty(File(plan, Generated).Conflicts);
        PatchPlanWriter.CreateAction(Context(mod), plan).Do();

        Assert.Equal(edited, mod.ReadMod(Generated));
        Assert.Equal(rememberedBefore, PatchRecipeStore.LoadMemory(PatchRecipeStore.Folder(mod.ModRoot), recipe.Id).Units["defs|angkor_wat_01"]);
    }

    [Fact]
    public void Faulty_row_is_skipped_and_others_apply()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);

        var plan = PatchRunner.Plan(Context(mod), Recipe("angkor_wat_01", "angkor_wat_01"));

        Assert.Contains(plan.Skipped, s => s.RuleId == "defs" && s.RowKey == "angkor_wat_01" && s.Reason == "Doppelter Schlüssel");
        Assert.Contains("barony = title:b_angkor", File(plan, Generated).NewText);
    }

    [Fact]
    public void Disabled_rule_does_nothing()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        var recipe = Recipe("angkor_wat_01");
        foreach (var rule in recipe.Rules)
        {
            rule.Enabled = rule.Id == "holdings";
        }

        var plan = PatchRunner.Plan(Context(mod), recipe);

        Assert.Equal(new[] { Holdings }, plan.Files.Select(f => f.RelativePath));
    }

    [Fact]
    public void Created_copies_get_a_vanilla_snapshot()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        var history = new VanillaHistoryStore(mod.ModRoot);
        var ctx = Context(mod, history, "1.18");
        var plan = PatchRunner.Plan(ctx, Recipe("angkor_wat_01"));

        PatchPlanWriter.CreateAction(ctx, plan).Do();
        PatchPlanWriter.RecordSnapshots(ctx, plan);

        Assert.True(history.HasSnapshot(Holdings, "1.18"));
        Assert.True(history.HasSnapshot(History, "1.18"));
        Assert.False(history.HasSnapshot(Generated, "1.18"));
    }

    [Fact]
    public void Cancelled_plan_throws()
    {
        using var mod = new TempMod();
        WriteVanilla(mod);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        Assert.Throws<OperationCanceledException>(() => PatchRunner.Plan(Context(mod), Recipe("angkor_wat_01"), cancel.Token));
    }

    private static PatchRecipe TemplateRecipe(string target, params string[] keys)
    {
        var recipe = new PatchRecipe { Id = "safe", Name = "Safe", GameId = "ck3", Rules = { new TemplateRule { Id = "t1", Target = target, Text = "{key} = { }" } } };
        foreach (var key in keys)
        {
            recipe.Rows.Add(new PatchRow { Key = key });
        }

        return recipe;
    }

    private static void AssertAllInsideMod(TempMod mod, PatchPlan plan) =>
        Assert.All(plan.Files, f => Assert.True(PathSafety.IsInside(mod.ModRoot, f.RelativePath), f.RelativePath));

    [Fact]
    public void Template_rule_with_parent_target_is_skipped()
    {
        using var mod = new TempMod();

        var plan = PatchRunner.Plan(Context(mod), TemplateRecipe("../outside.txt", "a"));

        Assert.Empty(plan.Files);
        Assert.Equal(new[] { new SkippedRow("t1", "global", PatchTexts.UnsafePath("../outside.txt")) }, plan.Skipped);
    }

    [Theory]
    [InlineData("/abs.txt")]
    [InlineData("C:/abs.txt")]
    [InlineData("a/../../x.txt")]
    public void Template_rule_with_absolute_or_parent_target_writes_nothing(string target)
    {
        using var mod = new TempMod();

        var plan = PatchRunner.Plan(Context(mod), TemplateRecipe(target, "a"));

        Assert.Empty(plan.Files);
        Assert.Equal(new[] { new SkippedRow("t1", "global", PatchTexts.UnsafePath(target)) }, plan.Skipped);
    }

    [Theory]
    [InlineData("common/x.txt;")]
    [InlineData("common/a.txt;common/b.txt")]
    [InlineData("common/*.txt")]
    [InlineData("common/x?.txt")]
    [InlineData("common/x.txt ")]
    [InlineData(" common/x.txt")]
    [InlineData("common/x.")]
    [InlineData("common//x.txt")]
    [InlineData("common/")]
    [InlineData("common/x|y.txt")]
    public void Template_rule_with_target_that_is_no_single_file_writes_nothing(string target)
    {
        using var mod = new TempMod();

        var plan = PatchRunner.Plan(Context(mod), TemplateRecipe(target, "a"));

        Assert.Empty(plan.Files);
        Assert.Equal(new[] { new SkippedRow("t1", "global", PatchTexts.TargetNotSingleFile(target)) }, plan.Skipped);
    }

    [Fact]
    public void Unsafe_rule_does_not_stop_the_other_rules()
    {
        using var mod = new TempMod();
        var recipe = TemplateRecipe("../outside.txt", "a");
        recipe.Rules.Add(new TemplateRule { Id = "t2", Target = "common/ok.txt", Text = "{key} = { }" });

        var plan = PatchRunner.Plan(Context(mod), recipe);

        Assert.Equal(new[] { "common/ok.txt" }, plan.Files.Select(f => f.RelativePath));
        Assert.Single(plan.Skipped);
    }

    [Fact]
    public void Line_rule_with_parent_files_reads_nothing_outside_the_mod()
    {
        using var mod = new TempMod();
        System.IO.File.WriteAllText(Path.Combine(Directory.GetParent(mod.ModRoot)!.FullName, "bait.txt"), "a = 1\n");
        var recipe = new PatchRecipe { Id = "safe", Name = "Safe", GameId = "ck3", Rules = { new LineRule { Id = "l1", Scope = RuleScope.Global, Files = "../*.txt", Match = "a = 1", Action = LineAction.Delete } } };

        var plan = PatchRunner.Plan(Context(mod), recipe);

        Assert.Empty(plan.Files);
        Assert.Equal(new[] { new SkippedRow("l1", "global", PatchTexts.UnsafePath("../*.txt")) }, plan.Skipped);
    }

    [Fact]
    public void Lookup_with_parent_files_gives_each_row_an_error()
    {
        using var mod = new TempMod();
        System.IO.File.WriteAllText(Path.Combine(Directory.GetParent(mod.ModRoot)!.FullName, "bait.txt"), "100 = { special_building_slot = a }\n");
        var recipe = TemplateRecipe("common/ok.txt", "a");
        recipe.Columns.Add(new PatchColumn { Name = "provinz", Lookup = new DerivedLookup { Files = "../*.txt", Source = LookupSource.Mod, Match = "special_building_slot = {key}", Optional = true } });

        var plan = PatchRunner.Plan(Context(mod), recipe);

        Assert.Empty(plan.Files);
        Assert.Equal(new[] { new SkippedRow("t1", "a", PatchTexts.UnsafePath("../*.txt")) }, plan.Skipped);
    }

    /// <summary>Pfade in Zielen werden nicht je Zeile gefüllt: <c>{key}</c> bleibt Teil des Dateinamens, ein Zeilenschlüssel mit <c>..</c> verlässt die Mod nie.</summary>
    [Fact]
    public void Placeholder_in_target_stays_literal_so_row_key_cannot_leave_the_mod()
    {
        using var mod = new TempMod();
        var outer = Directory.GetParent(mod.ModRoot)!.FullName;

        var plan = PatchRunner.Plan(Context(mod), TemplateRecipe("{key}/x.txt", "a", "../../outside"));
        PatchPlanWriter.CreateAction(Context(mod), plan).Do();

        AssertAllInsideMod(mod, plan);
        Assert.Equal(new[] { "{key}/x.txt" }, plan.Files.Select(f => f.RelativePath));
        Assert.False(Directory.Exists(Path.Combine(outer, "..", "outside")));
        Assert.False(System.IO.File.Exists(Path.Combine(outer, "outside", "x.txt")));
        Assert.Empty(Directory.GetFiles(outer, "*.txt"));
    }

    [Fact]
    public void Own_column_value_with_parent_path_cannot_leave_the_mod()
    {
        using var mod = new TempMod();
        var recipe = TemplateRecipe("common/{dir}/x.txt", "a", "b");
        recipe.Columns.Add(new PatchColumn { Name = "dir" });
        recipe.Rows[1].Values["dir"] = "../..";

        var plan = PatchRunner.Plan(Context(mod), recipe);
        PatchPlanWriter.CreateAction(Context(mod), plan).Do();

        AssertAllInsideMod(mod, plan);
        Assert.Empty(Directory.GetFiles(Directory.GetParent(mod.ModRoot)!.FullName, "*.txt"));
    }

    [Fact]
    public void Plan_writer_refuses_file_outside_mod()
    {
        using var mod = new TempMod();
        var plan = new PatchPlan
        {
            RecipeId = "safe",
            RecipeName = "Safe",
            Files = new[] { new PlannedFile("../x.txt", PlannedFileStatus.New, null, "x", true, false, Array.Empty<string>(), Array.Empty<string>()) },
            Skipped = Array.Empty<SkippedRow>(),
            Warnings = Array.Empty<string>(),
            OldMemory = new PatchMemory(),
            NewMemory = new PatchMemory(),
            ConflictUnitIds = Array.Empty<string>(),
        };

        var ex = Assert.Throws<UnsafePathException>(() => PatchPlanWriter.CreateAction(Context(mod), plan));

        Assert.Equal("../x.txt", ex.Path);
        Assert.False(System.IO.File.Exists(Path.Combine(Directory.GetParent(mod.ModRoot)!.FullName, "x.txt")));
    }
}
