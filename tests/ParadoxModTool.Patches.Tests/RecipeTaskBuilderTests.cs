using ParadoxModTool.Patches.Ai;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>Auftrag an eine KI für Patch-Rezepte (Spec 2026-10-12-patch-recipes-ai-guide, Abschnitt 2 "Auftrag"): Text für Chat-KIs, Ordner für KIs mit Dateizugriff.</summary>
public sealed class RecipeTaskBuilderTests : IDisposable
{
    private readonly TempMod _mod = new();
    private readonly string _work = Path.Combine(Path.GetTempPath(), "pmt-recipe-task-" + Guid.NewGuid().ToString("N"));

    public RecipeTaskBuilderTests()
    {
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        _mod.Dispose();
        try
        {
            Directory.Delete(_work, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Kleine Anleitung mit zwei Beispielen (eins mit Spiel- und Mod-Ordner) und einer Binärdatei, die nicht in den Text darf.</summary>
    private string Guide()
    {
        var guide = Path.Combine(_work, "guide");
        Write(guide, "SKILL.md", "SKILL TEXT\r\n");
        Write(guide, "reference.md", "REFERENCE TEXT\r\n");
        Write(guide, "examples/sample/recipe.json", "{ \"id\": \"sample\" }");
        Write(guide, "examples/sample/mod/common/a.txt", "SAMPLE MOD FILE");
        Write(guide, "examples/sample/game/common/b.txt", "SAMPLE GAME FILE");
        Write(guide, "examples/another/note.md", "ANOTHER NOTE");
        File.WriteAllBytes(Path.Combine(guide, "examples", "sample", "image.bin"), new byte[] { 1, 0, 2 });
        return guide;
    }

    private static void Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static PatchRecipe Open() => new()
    {
        Id = "open_recipe",
        Name = "Offen",
        GameId = "ck3",
        Rules = { new TemplateRule { Id = "template1", Target = "common/x.txt", Text = "{key} = { }" } },
    };

    private RecipeTaskRequest Request(PatchRecipe? open = null, string? cli = null, string? memory = null) =>
        new("Turn the buildings\r\ninto normal ones.", "ck3", _mod.ModRoot, _mod.VanillaRoot,
            new[] { "common/a.txt" }, new[] { "common/g.txt" }, open, memory, cli);

    private void WriteFiles()
    {
        _mod.WriteMod("common/a.txt", "MOD LINE\r\n", bom: false);
        _mod.WriteVanilla("common/g.txt", "GAME LINE\r\n");
    }

    [Fact]
    public void Text_contains_guide_task_mod_and_game_files()
    {
        WriteFiles();

        var text = RecipeTaskBuilder.BuildText(Request(), Guide());

        Assert.StartsWith("SKILL TEXT\n\nREFERENCE TEXT\n\n", text);
        Assert.Contains("<<<FILE examples/sample/recipe.json>>>\n{ \"id\": \"sample\" }\n<<<END>>>\n", text);
        Assert.Contains("<<<FILE examples/sample/mod/common/a.txt>>>\nSAMPLE MOD FILE\n<<<END>>>\n", text);
        Assert.Contains("<<<FILE examples/sample/game/common/b.txt>>>\nSAMPLE GAME FILE\n<<<END>>>\n", text);
        Assert.Contains("<<<FILE examples/another/note.md>>>\nANOTHER NOTE\n<<<END>>>\n", text);
        Assert.DoesNotContain("image.bin", text);
        Assert.True(text.IndexOf("examples/another/note.md", StringComparison.Ordinal) < text.IndexOf("examples/sample/recipe.json", StringComparison.Ordinal), "Beispiele sortiert");
        Assert.True(text.IndexOf("examples/sample/recipe.json", StringComparison.Ordinal) < text.IndexOf("# Task", StringComparison.Ordinal));
        Assert.Contains("# Task\n\nGame: ck3\n", text);
        Assert.Contains("## What the modder wants\n\nTurn the buildings\ninto normal ones.\n", text);
        Assert.Contains("Build a new recipe", text);
        Assert.Contains("----- mod file: common/a.txt -----\nMOD LINE\n----- end -----\n", text);
        Assert.Contains("----- game file: common/g.txt -----\nGAME LINE\n----- end -----\n", text);
        Assert.DoesNotContain("----- open recipe", text);
    }

    [Fact]
    public void Text_marks_truncated_game_file()
    {
        _mod.WriteVanilla("common/g.txt", new string('x', 210_000));
        _mod.WriteMod("common/a.txt", "m", bom: false);

        var text = RecipeTaskBuilder.BuildText(Request(), Guide());

        Assert.Contains("----- game file: common/g.txt -----\n[truncated: first 200 KB of ", text);
    }

    [Fact]
    public void Text_with_open_recipe_says_keep_ids()
    {
        WriteFiles();

        var text = RecipeTaskBuilder.BuildText(Request(Open()), Guide());

        Assert.Contains("Change the recipe below; keep its `id` and every existing rule `id`", text);
        Assert.DoesNotContain("Build a new recipe", text);
        Assert.Contains("----- open recipe: recipe.json -----\n", text);
        Assert.Contains("\"id\": \"open_recipe\"", text);
        Assert.EndsWith("\n----- end -----\n", text);
    }

    [Fact]
    public void Text_without_files_has_no_file_sections()
    {
        var request = Request() with { ModFiles = Array.Empty<string>(), GameFiles = Array.Empty<string>() };

        var text = RecipeTaskBuilder.BuildText(request, Guide());

        Assert.DoesNotContain("----- mod file", text);
        Assert.DoesNotContain("----- game file", text);
        Assert.DoesNotContain("## Files from the mod and the game", text);
    }

    [Fact]
    public void Text_with_code_blocks_explains_recipe_specific_hints()
    {
        WriteFiles();
        var request = Request() with
        {
            Snippets = new[]
            {
                new AiTaskSnippet("common/a.txt", false, "x = 1") { Note = "Repeats." },
                new AiTaskSnippet("common/g.txt", true, "y = 2") { FromGame = true },
            },
        };

        var text = RecipeTaskBuilder.BuildText(request, Guide());

        Assert.Contains("## Code blocks from the modder", text);
        Assert.Contains("----- code block: common/a.txt (excerpt) -----\nNote: Repeats.\nx = 1\n----- end -----\n", text);
        Assert.Contains("----- code block: game file common/g.txt (whole file) -----\ny = 2\n----- end -----\n", text);
        Assert.Contains("a recipe rule reads vanilla files by their path relative to the game folder", text);
        Assert.DoesNotContain("the package never writes game files", text);
        Assert.Contains("relative to the game or mod root", text);
        Assert.Contains("If a path is unknown, ask the modder for it.", text);
    }

    [Fact]
    public void Folder_has_example_mod_game_and_recipe_with_memory()
    {
        WriteFiles();
        var memory = Path.Combine(_work, "somewhere.memory.json");
        File.WriteAllText(memory, "{ \"units\": {} }");
        var target = Path.Combine(_work, "task");

        RecipeTaskBuilder.BuildFolder(Request(Open(), memory: memory), Guide(), target);

        Assert.Equal("SKILL TEXT\r\n", File.ReadAllText(Path.Combine(target, "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(target, "reference.md")));
        Assert.True(File.Exists(Path.Combine(target, "examples", "sample", "mod", "common", "a.txt")));
        Assert.True(File.Exists(Path.Combine(target, "task.md")));
        Assert.Equal("MOD LINE\r\n", File.ReadAllText(Path.Combine(target, "example", "mod", "common", "a.txt")));
        Assert.Equal("GAME LINE\r\n", File.ReadAllText(Path.Combine(target, "example", "game", "common", "g.txt")));
        var recipe = PatchRecipeStore.Deserialize(File.ReadAllText(Path.Combine(target, "recipe", "recipe.json")));
        Assert.Equal("open_recipe", recipe.Id);
        Assert.Equal("{ \"units\": {} }", File.ReadAllText(Path.Combine(target, "recipe", "open_recipe.memory.json")));
        var task = File.ReadAllText(Path.Combine(target, "task.md"));
        Assert.Contains("Change the recipe below; keep its `id` and every existing rule `id`", task);
        Assert.Contains("`recipe/recipe.json`", task);
        // Das offene Rezept bleibt unverändert, sonst vergliche --previous das Rezept mit sich selbst.
        Assert.Contains("never change anything in `recipe/`", task);
        Assert.Contains("Write your version to `recipe.json` in the task folder, next to `task.md`", task);
        Assert.Contains("`example/mod/`", task);
        Assert.Contains("`example/game/`", task);
    }

    [Fact]
    public void Folder_without_open_recipe_or_memory_has_no_recipe_folder()
    {
        WriteFiles();
        var target = Path.Combine(_work, "task");

        RecipeTaskBuilder.BuildFolder(Request(memory: Path.Combine(_work, "missing.memory.json")), Guide(), target);

        Assert.False(Directory.Exists(Path.Combine(target, "recipe")));
        Assert.Contains("Build a new recipe", File.ReadAllText(Path.Combine(target, "task.md")));
    }

    [Fact]
    public void Folder_with_open_recipe_but_no_memory_file_copies_only_the_recipe()
    {
        WriteFiles();
        var target = Path.Combine(_work, "task");

        RecipeTaskBuilder.BuildFolder(Request(Open(), memory: Path.Combine(_work, "missing.memory.json")), Guide(), target);

        Assert.True(File.Exists(Path.Combine(target, "recipe", "recipe.json")));
        Assert.False(File.Exists(Path.Combine(target, "recipe", "open_recipe.memory.json")));
    }

    [Fact]
    public void Task_md_lists_check_and_plan_with_real_paths()
    {
        WriteFiles();
        var cli = Path.Combine(_work, "pmt-patch.exe");
        var target = Path.Combine(_work, "task");

        RecipeTaskBuilder.BuildFolder(Request(Open(), cli), Guide(), target);

        var task = File.ReadAllText(Path.Combine(target, "task.md"));
        Assert.Contains("Write your recipe to `recipe.json` in the task folder (next to `task.md`) and run these commands from the task folder:", task);
        Assert.Contains($"    \"{cli}\" check recipe.json --game \"{_mod.VanillaRoot}\" --mod \"{_mod.ModRoot}\" --previous recipe/recipe.json\n", task);
        Assert.Contains($"    \"{cli}\" plan recipe.json --game \"{_mod.VanillaRoot}\" --mod \"{_mod.ModRoot}\" --diff\n", task);
        Assert.Contains("Copy the memory file `recipe/open_recipe.memory.json` (if present) next to your `recipe.json`", task);
        Assert.Contains("never change anything in `recipe/`", task);
    }

    [Fact]
    public void Task_md_without_open_recipe_has_no_previous_option()
    {
        WriteFiles();
        var cli = Path.Combine(_work, "pmt-patch.exe");
        var target = Path.Combine(_work, "task");

        RecipeTaskBuilder.BuildFolder(Request(cli: cli), Guide(), target);

        var task = File.ReadAllText(Path.Combine(target, "task.md"));
        Assert.Contains($"    \"{cli}\" check recipe.json --game \"{_mod.VanillaRoot}\" --mod \"{_mod.ModRoot}\"\n", task);
        Assert.DoesNotContain("--previous", task);
    }

    [Fact]
    public void Task_md_without_cli_asks_modder_to_import()
    {
        WriteFiles();
        var target = Path.Combine(_work, "task");

        RecipeTaskBuilder.BuildFolder(Request(), Guide(), target);

        var task = File.ReadAllText(Path.Combine(target, "task.md"));
        Assert.Contains("You cannot run PMT yourself", task);
        Assert.Contains("\"Rezept mit KI erstellen\"", task);
        Assert.Contains("\"Ergebnis\"", task);
        Assert.Contains("Write your recipe to `recipe.json` in the task folder (next to `task.md`).", task);
        Assert.DoesNotContain(" check <recipe.json>", task);
        Assert.DoesNotContain(" check recipe.json", task);
    }

    [Fact]
    public void Text_with_cli_lists_check_without_previous_path()
    {
        WriteFiles();

        var text = RecipeTaskBuilder.BuildText(Request(Open(), "C:\\tools\\pmt-patch.exe"), Guide());

        Assert.Contains("\"C:\\tools\\pmt-patch.exe\" check <recipe.json>", text);
        Assert.DoesNotContain("--previous", text);
    }

    [Fact]
    public void Feedback_lists_messages()
    {
        var text = RecipeTaskBuilder.Feedback(new[] { "rule x: path missing", "rule y: empty" });

        Assert.StartsWith("PMT reported these problems with your recipe.", text);
        Assert.Contains("\n\n- rule x: path missing\n- rule y: empty\n", text);
    }
}
