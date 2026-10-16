using ParadoxModTool.Patches.Ai;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>pmt-patch check und plan: feste Zeilenformen für KIs mit Dateizugriff; schreibt nie in Mod, Spiel oder Rezeptordner.</summary>
public sealed class PatchCliTests : IDisposable
{
    private readonly TempMod _mod = new();
    private readonly string _recipes = Path.Combine(Path.GetTempPath(), "pmt-cli-recipes-" + Guid.NewGuid().ToString("N"));
    private readonly string _keepRoot = Path.Combine(Path.GetTempPath(), "pmt-cli-keep-" + Guid.NewGuid().ToString("N"));

    public PatchCliTests()
    {
        Directory.CreateDirectory(_recipes);
        _mod.WriteVanilla("common/x/00_a.txt", "a_1 = {\n\tv = 1\n}\n");
    }

    public void Dispose()
    {
        _mod.Dispose();
        foreach (var folder in new[] { _recipes, _keepRoot })
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static PatchRecipe Recipe(params string[] keys)
    {
        var recipe = new PatchRecipe
        {
            Id = "r",
            Name = "Test",
            GameId = "ck3",
            Columns = { new PatchColumn { Name = "castle", Default = "c" } },
            Rules =
            {
                new TemplateRule { Id = "one", Target = "common/x/pmt_one.txt", Text = "one_{key} = { c = {castle} }" },
                new TemplateRule { Id = "two", Target = "common/x/pmt_two.txt", Text = "two_{key} = { }" },
            },
        };
        foreach (var key in keys)
        {
            recipe.Rows.Add(new PatchRow { Key = key });
        }

        return recipe;
    }

    private string Write(PatchRecipe recipe, string? name = null)
    {
        var path = Path.Combine(_recipes, name ?? "recipe.json");
        File.WriteAllText(path, PatchRecipeStore.Serialize(recipe));
        return path;
    }

    private PatchCliResult Plan(string path, string? rule = null, string? row = null, bool diff = false, string? keep = null) =>
        PatchCli.Plan(path, _mod.VanillaRoot, _mod.ModRoot, rule, row, diff, keep);

    [Fact]
    public void Run_without_args_is_usage_error()
    {
        var result = PatchCli.Run(Array.Empty<string>());
        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Lines, l => l.Contains("pmt-patch check"));
        Assert.Equal(2, PatchCli.Run(new[] { "plan", "x.json" }).ExitCode);
        Assert.Equal(2, PatchCli.Run(new[] { "check", "x.json", "--bogus" }).ExitCode);
    }

    [Fact]
    public void Run_dispatches_check_with_options()
    {
        var path = Write(Recipe("k1"));
        var result = PatchCli.Run(new[] { "check", path, "--game", _mod.VanillaRoot, "--mod", _mod.ModRoot });
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("OK", result.Lines[^1]);
    }

    [Fact]
    public void Check_reports_unknown_property_as_warning()
    {
        var json = PatchRecipeStore.Serialize(Recipe("k1")).Replace("\"id\": \"r\"", "\"id\": \"r\", \"bogus\": 1");
        var path = Path.Combine(_recipes, "recipe.json");
        File.WriteAllText(path, json);
        var result = PatchCli.Check(path, null, null, null);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(result.Lines, l => l.StartsWith("warning: recipe: ") && l.Contains("bogus"));
    }

    [Fact]
    public void Check_valid_recipe_prints_OK()
    {
        var result = PatchCli.Check(Write(Recipe("k1")), null, null, null);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { "OK" }, result.Lines);
    }

    [Fact]
    public void Check_reports_errors_with_where()
    {
        var recipe = Recipe("k1");
        recipe.Rules.Add(new TemplateRule { Id = "one", Target = "common/x/y.txt", Text = "y" });
        var result = PatchCli.Check(Write(recipe), null, null, null);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains(result.Lines, l => l.StartsWith("rule one: "));
        Assert.Equal("1 issue(s)", result.Lines[^1]);
    }

    [Fact]
    public void Check_with_previous_reports_dropped_rule()
    {
        var previous = Recipe("k1");
        var previousPath = Write(previous, "previous.json");
        File.WriteAllText(PatchRecipeStore.MemoryPath(_recipes, "r"), PatchRecipeStore.SerializeMemory(new PatchMemory { Units = { [PatchMemory.UnitId("two", "k1")] = "two_k1 = { }" } }));
        var next = Recipe("k1");
        next.Rules.RemoveAt(1);
        var result = PatchCli.Check(Write(next, "next.json"), null, null, previousPath);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains(result.Lines, l => l.StartsWith("rule two: "));
    }

    [Fact]
    public void Check_missing_file_is_one_line()
    {
        var result = PatchCli.Check(Path.Combine(_recipes, "nope.json"), null, null, null);
        Assert.Equal(1, result.ExitCode);
        Assert.Single(result.Lines);
        Assert.Contains("nope.json", result.Lines[0]);
    }

    [Fact]
    public void Check_unreadable_json_is_reported()
    {
        var path = Path.Combine(_recipes, "recipe.json");
        File.WriteAllText(path, "{ not json");
        var result = PatchCli.Check(path, null, null, null);
        Assert.Equal(1, result.ExitCode);
        Assert.StartsWith("recipe.json: ", result.Lines[0]);
    }

    [Fact]
    public void Plan_lists_new_file_and_exits_0()
    {
        var result = Plan(Write(Recipe("k1", "k2")));
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("table: 2 row(s)", result.Lines[0]);
        Assert.Contains("  k1: castle=c", result.Lines);
        Assert.Contains("files:", result.Lines);
        Assert.Contains("  new common/x/pmt_one.txt", result.Lines);
        Assert.Contains("  new common/x/pmt_two.txt", result.Lines);
        Assert.Equal("2 file(s) changed, 0 skipped, 0 conflict(s), 0 row error(s)", result.Lines[^1]);
        Assert.DoesNotContain(result.Lines, l => l.StartsWith("---"));
    }

    [Fact]
    public void Plan_with_diff_prints_unified_diff()
    {
        var result = Plan(Write(Recipe("k1")), diff: true);
        Assert.Contains("--- /dev/null", result.Lines);
        Assert.Contains("+++ b/common/x/pmt_one.txt", result.Lines);
        Assert.Contains("@@ -0,0 +1,4 @@", result.Lines);
        Assert.Contains("+# PMT-Patch r/one begin", result.Lines);
        Assert.Contains("+one_k1 = { c = c }", result.Lines);
    }

    [Fact]
    public void Plan_with_skipped_row_exits_1()
    {
        var recipe = Recipe("nope");
        recipe.Rules.Add(new BlockCopyRule { Id = "copy", SourceFiles = "common/x/*.txt", Target = "common/x/pmt_copy.txt" });
        var result = Plan(Write(recipe));
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("skipped:", result.Lines);
        Assert.Contains(result.Lines, l => l.StartsWith("  copy nope: "));
        Assert.Matches(@"^\d+ file\(s\) changed, 1 skipped, 0 conflict\(s\), 0 row error\(s\)$", result.Lines[^1]);
    }

    [Fact]
    public void Plan_row_errors_are_listed_and_exit_1()
    {
        var recipe = Recipe("k1");
        recipe.Columns.Add(new PatchColumn { Name = "found", Lookup = new DerivedLookup { Files = "common/x/*.txt", Match = "missing_{key}" } });
        var result = Plan(Write(recipe));
        Assert.Equal(1, result.ExitCode);
        Assert.Contains(result.Lines, l => l.StartsWith("  k1: ERROR "));
        Assert.EndsWith("1 row error(s)", result.Lines[^1]);
    }

    [Fact]
    public void Plan_rule_filter_runs_one_rule()
    {
        var result = Plan(Write(Recipe("k1")), rule: "two");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("  new common/x/pmt_two.txt", result.Lines);
        Assert.DoesNotContain("  new common/x/pmt_one.txt", result.Lines);
    }

    [Fact]
    public void Plan_row_filter_runs_one_row()
    {
        var result = Plan(Write(Recipe("k1", "k2")), row: "k2", diff: true);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("table: 1 row(s)", result.Lines);
        Assert.Contains("+two_k2 = { }", result.Lines);
        Assert.DoesNotContain("+two_k1 = { }", result.Lines);
    }

    [Fact]
    public void Plan_unknown_rule_or_row_is_one_line()
    {
        var path = Write(Recipe("k1"));
        Assert.Equal(1, Plan(path, rule: "zzz").ExitCode);
        Assert.Single(Plan(path, rule: "zzz").Lines);
        Assert.Equal(1, Plan(path, row: "zzz").ExitCode);
        Assert.Single(Plan(path, row: "zzz").Lines);
    }

    [Fact]
    public void Plan_with_check_errors_prints_them_and_exits_1()
    {
        var recipe = Recipe("k1");
        recipe.Rules.Add(new TemplateRule { Id = "one", Target = "common/x/y.txt", Text = "y" });
        var result = Plan(Write(recipe));
        Assert.Equal(1, result.ExitCode);
        Assert.DoesNotContain("files:", result.Lines);
        Assert.Contains(result.Lines, l => l.StartsWith("rule one: "));
    }

    [Fact]
    public void Plan_keep_writes_files_to_folder()
    {
        var keep = Path.Combine(_keepRoot, "keep");
        var result = Plan(Write(Recipe("k1")), keep: keep);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("one_k1", File.ReadAllText(Path.Combine(keep, "common", "x", "pmt_one.txt")));
        Assert.True(File.Exists(Path.Combine(keep, "common", "x", "pmt_two.txt")));
    }

    [Fact]
    public void Plan_never_writes_into_mod_or_recipe_folder()
    {
        _mod.WriteMod("common/x/mine.txt", "m = 1\n");
        var path = Write(Recipe("k1"));
        string Snapshot(string root) => string.Join("|", Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => p + "@" + File.GetLastWriteTimeUtc(p).Ticks));
        var modBefore = Snapshot(_mod.ModRoot);
        var gameBefore = Snapshot(_mod.VanillaRoot);
        var recipesBefore = Snapshot(_recipes);

        Assert.Equal(0, Plan(path, diff: true).ExitCode);
        Assert.Equal(0, PatchCli.Check(path, _mod.VanillaRoot, _mod.ModRoot, null).ExitCode);

        Assert.Equal(modBefore, Snapshot(_mod.ModRoot));
        Assert.Equal(gameBefore, Snapshot(_mod.VanillaRoot));
        Assert.Equal(recipesBefore, Snapshot(_recipes));
    }

    /// <summary>Wendet das Rezept wie das Fenster an (Dateien und Gedächtnis), damit der nächste Lauf von einem echten Stand ausgeht.</summary>
    private void Apply(string path)
    {
        var recipe = PatchRecipeStore.Deserialize(File.ReadAllText(path));
        var context = new PatchContext(_mod.ModRoot, _mod.VanillaRoot, null, null, RecipesFolder: _recipes);
        PatchPlanWriter.CreateAction(context, PatchRunner.Plan(context, recipe)).Do();
    }

    private static PatchRecipe WithOneText(string text)
    {
        var recipe = Recipe("k1");
        ((TemplateRule)recipe.Rules[0]).Text = text;
        return recipe;
    }

    [Fact]
    public void Plan_after_applying_lists_no_file_and_exits_0()
    {
        var path = Write(Recipe("k1", "k2"));
        Apply(path);
        var result = Plan(path, diff: true);
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("files:", result.Lines);
        Assert.DoesNotContain(result.Lines, l => l.StartsWith("  new ") || l.StartsWith("  changed ") || l.StartsWith("  unchanged "));
        Assert.Equal("0 file(s) changed, 0 skipped, 0 conflict(s), 0 row error(s)", result.Lines[^1]);
    }

    [Fact]
    public void Plan_lists_changed_file_with_diff()
    {
        var path = Write(Recipe("k1"));
        Apply(path);
        Write(WithOneText("one_{key} = { c = {castle} d = 1 }"));
        var result = Plan(path, diff: true);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("  changed common/x/pmt_one.txt", result.Lines);
        Assert.DoesNotContain(result.Lines, l => l.Contains("pmt_two.txt"));
        Assert.Contains("--- a/common/x/pmt_one.txt", result.Lines);
        Assert.Contains("-one_k1 = { c = c }", result.Lines);
        Assert.Contains("+one_k1 = { c = c d = 1 }", result.Lines);
        Assert.Equal("1 file(s) changed, 0 skipped, 0 conflict(s), 0 row error(s)", result.Lines[^1]);
    }

    [Fact]
    public void Plan_lists_conflict_with_messages_and_exits_1()
    {
        var path = Write(Recipe("k1"));
        Apply(path);
        _mod.WriteMod("common/x/pmt_one.txt", _mod.ReadMod("common/x/pmt_one.txt").Replace("c = c }", "c = MINE }"));
        Write(WithOneText("one_{key} = { c = THEIRS }"));
        var result = Plan(path, diff: true);
        Assert.Equal(1, result.ExitCode);
        var line = Assert.Single(result.Lines, l => l.StartsWith("  conflict common/x/pmt_one.txt: "));
        Assert.True(line.Length > "  conflict common/x/pmt_one.txt: ".Length);
        Assert.DoesNotContain("--- a/common/x/pmt_one.txt", result.Lines);
        Assert.EndsWith("1 conflict(s), 0 row error(s)", result.Lines[^1]);
    }

    [Fact]
    public void Plan_prints_warnings_section()
    {
        var result = Plan(Write(WithOneText("one_{key} = {nope}")));
        Assert.Equal(0, result.ExitCode);
        var index = result.Lines.ToList().IndexOf("warnings:");
        Assert.True(index > 0, string.Join("|", result.Lines));
        Assert.StartsWith("  rule one: ", result.Lines[index + 1]);
        Assert.Contains("nope", result.Lines[index + 1]);
    }

    [Fact]
    public void Plan_rule_and_row_together_run_one_rule_for_one_row()
    {
        var result = Plan(Write(Recipe("k1", "k2")), rule: "two", row: "k2", diff: true);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("table: 1 row(s)", result.Lines);
        Assert.Contains("  new common/x/pmt_two.txt", result.Lines);
        Assert.DoesNotContain(result.Lines, l => l.Contains("pmt_one.txt"));
        Assert.Contains("+two_k2 = { }", result.Lines);
        Assert.DoesNotContain("+two_k1 = { }", result.Lines);
    }

    [Fact]
    public void Plan_without_game_is_usage_error()
    {
        var path = Write(Recipe("k1"));
        Assert.Equal(2, PatchCli.Run(new[] { "plan", path, "--mod", _mod.ModRoot }).ExitCode);
        Assert.Equal(2, PatchCli.Run(new[] { "plan", path, "--game", _mod.VanillaRoot }).ExitCode);
    }

    [Fact]
    public void Plan_keep_inside_recipe_folder_is_refused_and_recipe_stays()
    {
        // Ein Ziel namens recipe.json würde das Rezept überschreiben, wenn --keep in den Rezeptordner zeigte.
        var recipe = Recipe("k1");
        ((TemplateRule)recipe.Rules[0]).Target = "recipe.json";
        var path = Write(recipe);
        var before = File.ReadAllText(path);
        foreach (var keep in new[] { _recipes, Path.Combine(_recipes, "keep"), _recipes + Path.DirectorySeparatorChar })
        {
            var result = Plan(path, keep: keep);
            Assert.Equal(1, result.ExitCode);
            Assert.Single(result.Lines);
            Assert.Contains("--keep", result.Lines[0]);
        }

        Assert.Equal(before, File.ReadAllText(path));
        Assert.False(Directory.Exists(Path.Combine(_recipes, "keep")));
    }

    [Fact]
    public void Plan_keep_inside_mod_or_game_folder_is_refused()
    {
        var path = Write(Recipe("k1"));
        foreach (var keep in new[] { _mod.ModRoot, Path.Combine(_mod.ModRoot, "out"), _mod.VanillaRoot, Path.Combine(_mod.VanillaRoot, "out") })
        {
            var result = Plan(path, keep: keep);
            Assert.Equal(1, result.ExitCode);
            Assert.Single(result.Lines);
            Assert.Contains("--keep", result.Lines[0]);
        }

        Assert.False(Directory.Exists(Path.Combine(_mod.ModRoot, "out")));
        Assert.False(File.Exists(Path.Combine(_mod.ModRoot, "common", "x", "pmt_one.txt")));
        Assert.False(Directory.Exists(Path.Combine(_mod.VanillaRoot, "out")));
    }

    [Fact]
    public void Plan_with_missing_mod_or_game_folder_is_one_line_and_exit_1()
    {
        var path = Write(Recipe("k1"));
        var missing = Path.Combine(_keepRoot, "does-not-exist");

        var noMod = PatchCli.Run(new[] { "plan", path, "--game", _mod.VanillaRoot, "--mod", missing });
        Assert.Equal(1, noMod.ExitCode);
        Assert.Equal($"--mod: folder not found: {missing}", Assert.Single(noMod.Lines));

        var noGame = PatchCli.Run(new[] { "plan", path, "--game", missing, "--mod", _mod.ModRoot });
        Assert.Equal(1, noGame.ExitCode);
        Assert.Equal($"--game: folder not found: {missing}", Assert.Single(noGame.Lines));
    }

    [Fact]
    public void Check_with_missing_mod_or_game_folder_is_one_line_and_exit_1()
    {
        var path = Write(Recipe("k1"));
        var missing = Path.Combine(_keepRoot, "does-not-exist");

        var noMod = PatchCli.Run(new[] { "check", path, "--mod", missing });
        Assert.Equal(1, noMod.ExitCode);
        Assert.Equal($"--mod: folder not found: {missing}", Assert.Single(noMod.Lines));

        var noGame = PatchCli.Run(new[] { "check", path, "--game", missing });
        Assert.Equal(1, noGame.ExitCode);
        Assert.Equal($"--game: folder not found: {missing}", Assert.Single(noGame.Lines));

        // Ohne Ordner-Angaben prüft check nur die Struktur.
        Assert.Equal(0, PatchCli.Run(new[] { "check", path }).ExitCode);
    }

    [Fact]
    public void Check_passes_the_special_buildings_preset()
    {
        // Die Vorlage hat eine Iterator-Regel ohne Dateien: das ist eine Warnung, kein Fehler (Ruling 11).
        var path = Write(SpecialBuildingsPreset.Create("Special Buildings", Array.Empty<string>()));

        var result = PatchCli.Check(path, null, null, null);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(result.Lines, l => l.StartsWith("warning: rule iterators: ", StringComparison.Ordinal));
    }

    [Fact]
    public void Unexpected_exception_gives_one_error_line_and_exit_1()
    {
        // Ein Nullzeichen im Pfad wirft ArgumentException tief in der Pfadauflösung; der Befehl darf nicht abstürzen.
        var path = Write(Recipe("k1"));
        var plan = Plan(path, keep: "bad\0folder");
        Assert.Equal(1, plan.ExitCode);
        Assert.Single(plan.Lines);
        Assert.StartsWith("error: ", plan.Lines[0]);

        var run = PatchCli.Run(new[] { "plan", path, "--game", _mod.VanillaRoot, "--mod", _mod.ModRoot, "--keep", "bad\0folder" });
        Assert.Equal(1, run.ExitCode);
        Assert.StartsWith("error: ", run.Lines[0]);

        var check = PatchCli.Check(path, null, null, "bad\0previous.json");
        Assert.Equal(1, check.ExitCode);
        Assert.Single(check.Lines);
    }
}
