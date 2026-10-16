using System.Text;

namespace ParadoxModTool.Patches.Ai;

/// <summary>
/// Auftrag an eine KI für ein Patch-Rezept: Beschreibung, Spiel, Mod- und Spiel-Ordner mit den angehakten Vorbild-Dateien (relativ zum jeweiligen
/// Ordner), optional das offene Rezept samt Gedächtnis-Datei und der Pfad von pmt-patch.exe (nur wenn die KI selbst prüfen kann).
/// <see cref="Snippets"/>: von Hand eingefügte Codeblöcke statt ganzer Dateien. <see cref="GameRoot"/> ist genau der Vanilla-Ordner, den das
/// Rezepte-Fenster benutzt (<c>PatchWindowHost.VanillaRoot()</c>), sonst finden Nachschlagen und Blockkopie der KI-Probeläufe nichts.
/// </summary>
public sealed record RecipeTaskRequest(string Description, string GameId, string? ModRoot, string? GameRoot,
    IReadOnlyList<string> ModFiles, IReadOnlyList<string> GameFiles, PatchRecipe? OpenRecipe, string? OpenRecipeMemoryPath, string? CliPath)
{
    public IReadOnlyList<AiTaskSnippet> Snippets { get; init; } = Array.Empty<AiTaskSnippet>();
}

/// <summary>
/// "Rezept mit KI erstellen", Reiter "Auftrag" (Spec 2026-10-12-patch-recipes-ai-guide, Abschnitt 2): baut aus Anleitung, Beispielrezepten, Beschreibung
/// und Vorbild-Dateien einen Text für Chat-KIs oder einen Ordner für KIs mit Dateizugriff. Liest nur, schreibt nur in den Zielordner.
/// Die allgemeinen Helfer (Dateien lesen, Blöcke und Codeblöcke ausgeben) liegen in <see cref="AiTaskFiles"/>.
/// </summary>
public static class RecipeTaskBuilder
{
    private const string ExamplesFolder = "examples";

    /// <summary>Wohin die KI im Ordner-Modus ihr Rezept schreibt: neben task.md, dort liest es der Import ("Ordner übernehmen").</summary>
    private const string WriteRecipeHere = "Write your recipe to `recipe.json` in the task folder (next to `task.md`)";

    /// <summary>Wie die KI einen Ausschnitt schreibt: setzt den Satz nach "must stay" fort (<see cref="AiTaskFiles.AppendSnippets"/>).</summary>
    private const string WriteHint = ", so a rule must never replace or drop it. An excerpt shows how the game or the mod does it, " +
        "and the paths in the recipe are relative to the game or mod root.";

    /// <summary>Was ein Spiel-Codeblock nützt (<see cref="AiTaskFiles.AppendSnippets"/>).</summary>
    private const string GameFileHint = "It shows how the game does it; a recipe rule reads vanilla files by their path relative to the game folder " +
        "and does not edit game files. If it is unclear whether the mod should override, extend or only imitate it, ask the modder.";

    /// <summary>Ein Text: SKILL.md, reference.md, die Beispielrezepte in Dateiblöcken, dann Auftrag, Vorbild-Dateien, Codeblöcke und das offene Rezept.</summary>
    public static string BuildText(RecipeTaskRequest request, string guideFolder)
    {
        var text = new StringBuilder();
        text.Append(Read(guideFolder, "SKILL.md")).Append("\n\n");
        text.Append(Read(guideFolder, "reference.md")).Append("\n\n");
        var examples = ExampleFiles(guideFolder);
        if (examples.Count > 0)
        {
            text.Append("# Example recipes (for reference, do not copy their ids)\n\n");
            foreach (var file in AiTaskFiles.ReadFiles(guideFolder, examples).Where(f => f.Text is not null))
            {
                text.Append($"<<<FILE {file.RelativePath}>>>\n").Append(file.Text!.Replace("\r\n", "\n").TrimEnd('\n')).Append("\n<<<END>>>\n");
            }

            text.Append('\n');
        }

        text.Append(TaskMarkdown(request, folderMode: false)).Append('\n');
        if (request.ModRoot is { } mod)
        {
            AiTaskFiles.AppendFiles(text, "mod file", AiTaskFiles.ReadFiles(mod, request.ModFiles));
        }

        if (request.GameRoot is { } game)
        {
            AiTaskFiles.AppendFiles(text, "game file", AiTaskFiles.ReadFiles(game, request.GameFiles));
        }

        if (request.Snippets.Count > 0)
        {
            AiTaskFiles.AppendSnippets(text, request.Snippets, WriteHint, GameFileHint);
        }

        if (request.OpenRecipe is { } open)
        {
            text.Append("----- open recipe: recipe.json -----\n").Append(PatchRecipeStore.Serialize(open).Replace("\r\n", "\n").TrimEnd('\n')).Append("\n----- end -----\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// Ordner für KIs mit Dateizugriff: Anleitung, task.md, example/mod/ und example/game/ mit den Vorbild-Dateien (ganz, auch binär),
    /// recipe/recipe.json mit dem offenen Rezept und recipe/&lt;id&gt;.memory.json mit dessen Gedächtnis, wenn es die Datei gibt.
    /// </summary>
    public static void BuildFolder(RecipeTaskRequest request, string guideFolder, string targetFolder)
    {
        FolderCopy.CopyTree(guideFolder, targetFolder);
        File.WriteAllText(Path.Combine(targetFolder, "task.md"), TaskMarkdown(request, folderMode: true), new UTF8Encoding(false));
        if (request.ModRoot is { } mod)
        {
            AiTaskFiles.CopyFiles(mod, request.ModFiles, Path.Combine(targetFolder, "example", "mod"));
        }

        if (request.GameRoot is { } game)
        {
            AiTaskFiles.CopyFiles(game, request.GameFiles, Path.Combine(targetFolder, "example", "game"));
        }

        if (request.OpenRecipe is { } open)
        {
            var folder = Path.Combine(targetFolder, "recipe");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "recipe.json"), PatchRecipeStore.Serialize(open), new UTF8Encoding(false));
            if (request.OpenRecipeMemoryPath is { } memory && File.Exists(memory))
            {
                File.Copy(memory, PatchRecipeStore.MemoryPath(folder, open.Id), overwrite: true);
            }
        }
    }

    /// <summary>Text für "Fehler für die KI kopieren": PMTs Meldungen als Liste, auf Englisch eingeleitet.</summary>
    public static string Feedback(IEnumerable<string> messages) =>
        AiTaskFiles.Feedback("PMT reported these problems with your recipe. Please fix them and answer again with the complete recipe.json:", messages);

    private static string TaskMarkdown(RecipeTaskRequest request, bool folderMode)
    {
        var text = new StringBuilder("# Task\n\n");
        text.Append($"Game: {request.GameId}\n\n");
        text.Append("## What the modder wants\n\n").Append(request.Description.Trim().Replace("\r\n", "\n")).Append("\n\n");
        var hasFiles = request.ModFiles.Count > 0 || request.GameFiles.Count > 0;
        var source = (hasFiles, request.Snippets.Count > 0) switch
        {
            (true, true) => "the example files and code blocks below",
            (true, false) => "the example files below",
            (false, true) => "the code blocks below",
            _ => "the description above",
        };
        if (request.OpenRecipe is null)
        {
            text.Append($"Build a new recipe from {source}.\n\n");
        }
        else
        {
            text.Append("Change the recipe below; keep its `id` and every existing rule `id`. Use ").Append(source).Append(".\n\n");
        }

        text.Append("## How to check\n\n");
        if (request.CliPath is { } cli)
        {
            AppendCliCommands(text, request, cli, folderMode);
        }
        else
        {
            if (folderMode)
            {
                text.Append(WriteRecipeHere).Append(". ");
            }

            text.Append("You cannot run PMT yourself. Give the modder your `recipe.json`; they import it in PMT (\"Rezept mit KI erstellen\", tab \"Ergebnis\") ")
                .Append("and send the messages back to you.\n\n");
        }

        if (hasFiles)
        {
            text.Append("## Files from the mod and the game\n\n");
            if (folderMode)
            {
                text.Append("Mod files are in the folder `example/mod/` (paths relative to the mod root), game files in `example/game/` (paths relative to the game root).\n");
            }
            else
            {
                text.Append("Each file follows below between `----- mod file: <path> -----` or `----- game file: <path> -----` and `----- end -----`. ")
                    .Append("Paths are relative to the mod root or the game root.\n");
            }
        }

        if (request.OpenRecipe is not null)
        {
            text.Append(folderMode
                ? "The current recipe is in `recipe/recipe.json`; read it, but never change anything in `recipe/`. Write your version to `recipe.json` in the task folder, next to `task.md`.\n"
                : "The current recipe follows at the end between `----- open recipe: recipe.json -----` and `----- end -----`.\n");
        }

        // Im Text-Modus folgen die Codeblöcke erst nach den Vorbild-Dateien (BuildText), damit jede Überschrift über ihrem Inhalt steht.
        if (folderMode && request.Snippets.Count > 0)
        {
            AiTaskFiles.AppendSnippets(text, request.Snippets, WriteHint, GameFileHint);
        }

        return text.ToString();
    }

    /// <summary>
    /// Die Befehle von pmt-patch.exe mit den echten Pfaden von Spiel und Mod; <c>--previous</c> nur, wenn das offene Rezept als Datei im Ordner liegt.
    /// Im Ordner schreibt die KI ihr Rezept neben task.md (dort liest es auch der Import), <c>recipe/</c> bleibt unverändert: sonst vergliche
    /// <c>--previous recipe/recipe.json</c> das Rezept mit sich selbst.
    /// </summary>
    private static void AppendCliCommands(StringBuilder text, RecipeTaskRequest request, string cli, bool folderMode)
    {
        text.Append(folderMode
            ? "You can run PMT's check program. " + WriteRecipeHere + " and run these commands from the task folder:\n\n"
            : "You can run PMT's check program. Save your recipe as `recipe.json`, then run:\n\n");
        var recipeArgument = folderMode ? "recipe.json" : "<recipe.json>";
        var paths = new StringBuilder();
        if (request.GameRoot is { } game)
        {
            paths.Append($" --game \"{game}\"");
        }

        if (request.ModRoot is { } mod)
        {
            paths.Append($" --mod \"{mod}\"");
        }

        var previous = folderMode && request.OpenRecipe is not null ? " --previous recipe/recipe.json" : string.Empty;
        text.Append($"    \"{cli}\" check {recipeArgument}{paths}{previous}\n");
        text.Append($"    \"{cli}\" plan {recipeArgument}{paths} --diff\n\n");
        text.Append("`check` lists errors and warnings; `plan` shows what the recipe would change in the mod without writing anything. Fix every error and read every skipped row.\n");
        if (request.OpenRecipe is { } open)
        {
            text.Append(folderMode
                ? $"Copy the memory file `recipe/{open.Id}.memory.json` (if present) next to your `recipe.json`: the dry run reads it to know the hand edits in generated regions. "
                    + "Otherwise never change anything in `recipe/`: `--previous` compares your recipe with the unchanged one there.\n"
                : "Keep the memory file of the open recipe next to your `recipe.json` if the modder gives it to you.\n");
        }

        text.Append('\n');
    }

    /// <summary>Alle Textdateien unter <c>examples/&lt;Unterordner&gt;/…</c> der Anleitung (relativ zur Anleitung, mit "/", sortiert).</summary>
    private static IReadOnlyList<string> ExampleFiles(string guideFolder)
    {
        var root = Path.Combine(guideFolder, ExamplesFolder);
        if (!Directory.Exists(root))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateDirectories(root)
            .SelectMany(sub => Directory.EnumerateFiles(sub, "*", SearchOption.AllDirectories))
            .Select(file => Path.GetRelativePath(guideFolder, file).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static string Read(string folder, string name) =>
        File.ReadAllText(Path.Combine(folder, name)).Replace("\r\n", "\n").TrimEnd('\n');
}
