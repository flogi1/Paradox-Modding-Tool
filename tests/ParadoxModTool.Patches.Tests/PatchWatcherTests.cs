using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class PatchWatcherTests
{
    private static readonly WatchPattern[] SlotAndKey = { new("special_building_slot", false), new("angkor_wat_01", true) };

    private static PatchRecipe Recipe(params string[] keys) => new()
    {
        Id = "r1",
        Rows = keys.Select(k => new PatchRow { Key = k }).ToList(),
    };

    [Fact]
    public void Patterns_drop_empty_and_duplicate_entries()
    {
        var recipe = Recipe("angkor_wat_01", "", "borobudur_01");
        recipe.Watch.Patterns = new() { "every_special_building_province", " ", "", "has_special_building", "every_special_building_province" };

        var patterns = PatchWatcher.Patterns(recipe);

        Assert.Equal(new[] { "every_special_building_province", "has_special_building", "angkor_wat_01", "borobudur_01" }, patterns.Select(p => p.Text).ToArray());
        Assert.Equal(new[] { false, false, true, true }, patterns.Select(p => p.RowKey).ToArray());
    }

    [Fact]
    public void Finds_whole_words_with_line_numbers()
    {
        const string text = "a = 1\nevery_special_building_province = {\n  has_special_building = yes\n  x = every_special_building_province_old\n}";
        var patterns = new[] { new WatchPattern("every_special_building_province", false), new WatchPattern("has_special_building", false) };

        var lines = PatchWatcher.FindLines(text, patterns, null, Array.Empty<string>());

        Assert.Equal(new[] { 2, 3 }, lines.Select(l => l.Line).ToArray());
        Assert.Equal("every_special_building_province", lines[0].Pattern);
        Assert.Equal("has_special_building = yes", lines[1].Text);
    }

    [Fact]
    public void Comment_hits_do_not_count()
    {
        const string text = "#special_building_slot = angkor_wat_01\nb = c # angkor_wat_01\nd = angkor_wat_01";

        var lines = PatchWatcher.FindLines(text, SlotAndKey, null, Array.Empty<string>());

        var line = Assert.Single(lines);
        Assert.Equal(3, line.Line);
        Assert.True(line.RowKey);
    }

    [Fact]
    public void Own_regions_and_own_list_entries_do_not_count()
    {
        const string text =
            "# PMT-Patch r1/rule begin\n# PMT-Unit angkor_wat_01\nangkor_wat_01 = { }\n# PMT-Patch r1/rule end\n"
            + "castle_holding = {\n  buildings = {\n    castle_01\n    angkor_wat_01\n  }\n}\nx = angkor_wat_01";

        var lines = PatchWatcher.FindLines(text, SlotAndKey, "r1", new[] { "angkor_wat_01" });

        var line = Assert.Single(lines);
        Assert.Equal("x = angkor_wat_01", line.Text);
        Assert.Equal(11, line.Line);
    }

    private static readonly IReadOnlySet<string> NoneOutdated = new HashSet<string>();

    private static PatchRecipe WatchRecipe(params string[] keys)
    {
        var recipe = Recipe(keys);
        recipe.Watch.Patterns = new() { "every_special_building_province", "has_special_building" };
        return recipe;
    }

    private static WatchHit Hit(IReadOnlyList<WatchHit> hits, string path) => hits.Single(h => h.File.RelativePath == path);

    [Fact]
    public void Vanilla_hit_without_copy_is_uncovered()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("events/a.txt", "every_special_building_province = { }\n");
        var recipe = WatchRecipe();

        var hit = Hit(PatchWatcher.Classify(PatchWatcher.Scan(PatchRunnerTests.Context(mod), recipe), recipe, NoneOutdated), "events/a.txt");

        Assert.Equal(WatchStatus.Uncovered, hit.Status);
        Assert.Equal(1, Assert.Single(hit.File.VanillaLines).Line);
    }

    [Fact]
    public void Conflict_file_is_not_covered_because_apply_skips_it()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/x.txt", "every_special_building_province = { }\n");
        var recipe = WatchRecipe("a_01");
        recipe.Rules.Add(new TemplateRule { Id = "t", Target = "common/x.txt", Text = "y = 1" });
        PatchPlanWriter.CreateAction(PatchRunnerTests.Context(mod), PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe)).Do();
        mod.WriteMod("common/x.txt", mod.ReadMod("common/x.txt").Replace("y = 1", "y = 2") + "every_special_building_province = { }\n");
        ((TemplateRule)recipe.Rules[0]).Text = "y = 3";
        recipe.Rules.Add(new LineRule { Id = "l", Scope = RuleScope.Global, Files = "common/x.txt", Match = "every_special_building_province", Action = LineAction.Replace, Replacement = "every_province" });

        var scan = PatchWatcher.Scan(PatchRunnerTests.Context(mod), recipe);
        var hit = Hit(PatchWatcher.Classify(scan, recipe, NoneOutdated), "common/x.txt");

        Assert.Equal(PlannedFileStatus.Conflict, PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe).Files.Single(f => f.RelativePath == "common/x.txt").Status);
        Assert.Equal(WatchStatus.Uncovered, hit.Status);
    }

    [Fact]
    public void Renaming_a_rule_does_not_make_the_scan_stale()
    {
        using var mod = new TempMod();
        var recipe = WatchRecipe("a_01");
        recipe.Rules.Add(new LineRule { Id = "l", Name = "Alt", Files = "events/*.txt", Match = "x" });
        var scan = PatchWatcher.Scan(PatchRunnerTests.Context(mod), recipe);

        recipe.Rules[0].Name = "Neu";
        Assert.False(PatchWatcher.IsStale(scan, recipe));

        ((LineRule)recipe.Rules[0]).Match = "y";
        Assert.True(PatchWatcher.IsStale(scan, recipe));
    }

    [Fact]
    public void Plan_paths_with_a_leading_slash_do_not_abort_the_scan()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/x.txt", "every_special_building_province = { }\n");
        var recipe = WatchRecipe("a_01");
        recipe.Rules.Add(new TemplateRule { Id = "a", Target = "/common/x.txt", Text = "y = 1" });
        recipe.Rules.Add(new TemplateRule { Id = "b", Target = "common/x.txt", Text = "z = 1" });

        var scan = PatchWatcher.Scan(PatchRunnerTests.Context(mod), recipe);

        Assert.Contains(scan.Files, f => f.RelativePath == "common/x.txt");
    }

    [Fact]
    public void Existing_but_empty_vanilla_folder_warns()
    {
        using var mod = new TempMod();
        mod.WriteMod("events/a.txt", "every_special_building_province = { }\n");

        var scan = PatchWatcher.Scan(PatchRunnerTests.Context(mod), WatchRecipe());

        Assert.Contains(PatchTexts.WatchVanillaEmpty, scan.Warnings);
    }

    [Fact]
    public void Vanilla_folder_hidden_by_replace_path_is_not_scanned()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_a.txt", "every_special_building_province = { }\n");
        mod.WriteVanilla("events/a.txt", "every_special_building_province = { }\n");
        var context = PatchRunnerTests.Context(mod) with { HiddenVanillaFolders = new[] { "history/provinces" } };

        var scan = PatchWatcher.Scan(context, WatchRecipe());

        Assert.DoesNotContain(scan.Files, f => f.RelativePath == "history/provinces/k_a.txt");
        Assert.Contains(scan.Files, f => f.RelativePath == "events/a.txt");
    }

    [Fact]
    public void Line_rule_with_create_copy_covers_the_file()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("events/a.txt", "every_special_building_province = { }\n");
        var recipe = WatchRecipe();
        recipe.Rules.Add(new LineRule
        {
            Id = "iter", Scope = RuleScope.Global, Files = "events/a.txt", Match = "every_special_building_province",
            Action = LineAction.Replace, Replacement = "every_province", CreateCopy = true,
        });

        var hit = Hit(PatchWatcher.Classify(PatchWatcher.Scan(PatchRunnerTests.Context(mod), recipe), recipe, NoneOutdated), "events/a.txt");

        Assert.Equal(WatchStatus.Covered, hit.Status);
        Assert.True(hit.File.PlannedExists);
        Assert.False(hit.File.ModExists);
    }

    [Fact]
    public void New_vanilla_line_in_a_handled_file_stays_uncovered()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("events/a.txt", "every_special_building_province = { }\nhas_special_building = yes\n");
        var recipe = WatchRecipe();
        recipe.Rules.Add(new LineRule { Id = "iter", Scope = RuleScope.Global, Files = "events/a.txt", Match = "every_special_building_province", Action = LineAction.Comment });
        mod.WriteMod("events/a.txt", "#every_special_building_province = { }\nhas_special_building = yes\n");

        var hit = Hit(PatchWatcher.Classify(PatchWatcher.Scan(PatchRunnerTests.Context(mod), recipe), recipe, NoneOutdated), "events/a.txt");

        Assert.Equal(WatchStatus.Uncovered, hit.Status);
        Assert.Equal("has_special_building", Assert.Single(hit.File.PlannedLines).Pattern);
    }

    [Fact]
    public void Mod_only_file_with_hit_is_own_file()
    {
        using var mod = new TempMod();
        mod.WriteMod("events/flogi.txt", "add_special_building = angkor_wat_01\n");
        var recipe = WatchRecipe("angkor_wat_01");

        var hit = Hit(PatchWatcher.Classify(PatchWatcher.Scan(PatchRunnerTests.Context(mod), recipe), recipe, NoneOutdated), "events/flogi.txt");

        Assert.Equal(WatchStatus.OwnFile, hit.Status);
    }

    [Fact]
    public void Outdated_set_wins_over_covered()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("events/a.txt", "every_special_building_province = { }\n");
        mod.WriteMod("events/a.txt", "every_province = { }\n");
        var recipe = WatchRecipe();
        var scan = PatchWatcher.Scan(PatchRunnerTests.Context(mod), recipe);

        Assert.Equal(WatchStatus.Covered, Hit(PatchWatcher.Classify(scan, recipe, NoneOutdated), "events/a.txt").Status);
        Assert.Equal(WatchStatus.Outdated, Hit(PatchWatcher.Classify(scan, recipe, new HashSet<string> { "events/a.txt" }), "events/a.txt").Status);
    }

    [Fact]
    public void Ignored_path_matches_case_and_slash_insensitive()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("events/a.txt", "every_special_building_province = { }\n");
        var recipe = WatchRecipe();
        recipe.Watch.Ignored.Add(new IgnoredWatchFile { Path = "Events\\A.txt", Note = "egal" });

        var hit = Hit(PatchWatcher.Classify(PatchWatcher.Scan(PatchRunnerTests.Context(mod), recipe), recipe, NoneOutdated), "events/a.txt");

        Assert.Equal(WatchStatus.Ignored, hit.Status);
        Assert.Equal("egal", hit.IgnoreNote);
    }

    [Fact]
    public void A_folder_pattern_ignores_every_file_below_it_with_the_note_and_a_file_entry_goes_first()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/customizable_localization/a.txt", "every_special_building_province = { }\n");
        mod.WriteVanilla("common/customizable_localization/sub/b.txt", "every_special_building_province = { }\n");
        mod.WriteVanilla("common/other/c.txt", "every_special_building_province = { }\n");
        var recipe = WatchRecipe();
        RecipeEditing.IgnoreFolder(recipe, "common/customizable_localization/a.txt", "Texte");
        recipe.Watch.Ignored.Add(new IgnoredWatchFile { Path = "common/customizable_localization/sub/b.txt", Note = "einzeln" });

        var hits = PatchWatcher.Classify(PatchWatcher.Scan(PatchRunnerTests.Context(mod), recipe), recipe, NoneOutdated);

        Assert.Equal(WatchStatus.Ignored, Hit(hits, "common/customizable_localization/a.txt").Status);
        Assert.Equal("Texte", Hit(hits, "common/customizable_localization/a.txt").IgnoreNote);
        Assert.Equal("einzeln", Hit(hits, "common/customizable_localization/sub/b.txt").IgnoreNote);
        Assert.Equal(WatchStatus.Uncovered, Hit(hits, "common/other/c.txt").Status);
    }

    [Fact]
    public void Missing_vanilla_root_is_a_warning()
    {
        using var mod = new TempMod();
        mod.WriteMod("events/flogi.txt", "has_special_building = yes\n");
        var recipe = WatchRecipe();
        var context = new PatchContext(mod.ModRoot, Path.Combine(mod.VanillaRoot, "fehlt"), null, null);

        var scan = PatchWatcher.Scan(context, recipe);

        Assert.NotEmpty(scan.Warnings);
        Assert.Equal(WatchStatus.OwnFile, Hit(PatchWatcher.Classify(scan, recipe, NoneOutdated), "events/flogi.txt").Status);
    }

    [Fact]
    public void Locked_file_is_skipped_with_a_warning()
    {
        using var mod = new TempMod();
        mod.WriteMod("events/a.txt", "has_special_building = yes\n");
        mod.WriteMod("events/b.txt", "has_special_building = yes\n");
        var recipe = WatchRecipe();

        WatchScan scan;
        using (new FileStream(mod.ModPath("events/b.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            scan = PatchWatcher.Scan(PatchRunnerTests.Context(mod), recipe);
        }

        Assert.Contains(scan.Files, f => f.RelativePath == "events/a.txt");
        Assert.Contains(scan.Warnings, w => w.Contains("events/b.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void Fingerprint_ignores_ignore_list_only()
    {
        using var mod = new TempMod();
        var recipe = WatchRecipe("angkor_wat_01");
        recipe.Rules.Add(new LineRule { Id = "l", Files = "events/*.txt", Match = "{key}" });
        var scan = PatchWatcher.Scan(PatchRunnerTests.Context(mod), recipe);

        recipe.Watch.Ignored.Add(new IgnoredWatchFile { Path = "events/a.txt" });
        Assert.False(PatchWatcher.IsStale(scan, recipe));

        ((LineRule)recipe.Rules[0]).Match = "x = {key}";
        Assert.True(PatchWatcher.IsStale(scan, recipe));

        ((LineRule)recipe.Rules[0]).Match = "{key}";
        recipe.Rows.Add(new PatchRow { Key = "borobudur_01" });
        Assert.True(PatchWatcher.IsStale(scan, recipe));
    }

    [Fact]
    public void Fingerprint_ignores_the_description()
    {
        // Die Erklärung ändert kein Ergebnis: Tippen darin darf den Scan nicht veralten lassen (wie Regelnamen).
        var recipe = WatchRecipe("angkor_wat_01");
        var before = PatchWatcher.Fingerprint(recipe);

        recipe.Description = "Neue Erklärung";

        Assert.Equal(before, PatchWatcher.Fingerprint(recipe));
    }

    [Fact]
    public void Scanned_files_skip_excluded_segments()
    {
        Assert.True(PatchWatcher.IsScannedFile("common/buildings/a.txt"));
        Assert.False(PatchWatcher.IsScannedFile("gfx/x.txt"));
        Assert.False(PatchWatcher.IsScannedFile("in_game/gfx/x.txt"));
        Assert.False(PatchWatcher.IsScannedFile("common/a.yml"));
        Assert.False(PatchWatcher.IsScannedFile("Common/GFX/a.TXT"));
    }
}
