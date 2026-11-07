using System.Text;
using ParadoxModTool.Patches;
using ParadoxModTool.Patches.Ai;

namespace ParadoxModTool.Performance;

/// <summary>
/// Auftrag an eine KI für die Performance-Analyse: der Entwurf (<see cref="AiTaskDraft"/>), die Beschreibung des Modders, angehakte Mod- und Spiel-Dateien
/// (relativ zum jeweiligen Ordner), eigene Codeblöcke und, wenn gewünscht, das Format der Patch-Rezepte. <see cref="WithRecipeFormat"/> hängt die
/// Rezept-Anleitung nur an, wenn der Ordner dafür angegeben ist und existiert.
/// </summary>
public sealed record PerformanceTaskRequest(
    AiTaskDraft Draft,
    string Description,
    string GameId,
    string? ModRoot,
    string? GameRoot,
    IReadOnlyList<string> ModFiles,
    IReadOnlyList<string> GameFiles,
    IReadOnlyList<AiTaskSnippet> UserSnippets,
    bool WithRecipeFormat);

/// <summary>
/// Baut aus Anleitung, Beispielen, Entwurf und Dateien den Text für Chat-KIs (<see cref="BuildText"/>) oder den Ordner für KIs mit Dateizugriff
/// (<see cref="BuildFolder"/>). Liest nur, schreibt nur in den Zielordner. Wie beim Rezept-Auftrag (<see cref="RecipeTaskBuilder"/>) liegen die
/// allgemeinen Helfer in <see cref="AiTaskFiles"/>.
/// </summary>
public static class PerformanceTaskBuilder
{
    private const string ExamplesFolder = "examples";

    private const string ReferenceFile = "reference.md";

    /// <summary>Wie die KI einen Ausschnitt behandelt: setzt den Satz nach "must stay" fort (<see cref="AiTaskFiles.AppendSnippets"/>).</summary>
    private const string WriteHint = ", so never propose to replace the whole file with the excerpt. Paths are relative to the mod root or the game root.";

    /// <summary>Was ein Spiel-Codeblock nützt (<see cref="AiTaskFiles.AppendSnippets"/>).</summary>
    private const string GameFileHint = "It shows how the game does it; game files cannot be changed, only overridden by the mod. " +
        "If it is unclear whether the mod should override, extend or only imitate it, ask the modder.";

    /// <summary>Ein Text: SKILL.md, reference.md, Beispiele, Auftrag, Dateien, Codeblöcke des Modders und der Hotspots, optional das Rezeptformat.</summary>
    public static string BuildText(PerformanceTaskRequest request, string guideFolder, string? recipeGuideFolder)
    {
        var text = new StringBuilder();
        text.Append(Read(guideFolder, "SKILL.md")).Append("\n\n");
        text.Append(Read(guideFolder, ReferenceFile)).Append("\n\n");
        var examples = ExampleFiles(guideFolder);
        if (examples.Count > 0)
        {
            text.Append("# Example\n\n");
            AiTaskFiles.AppendFiles(text, "example", AiTaskFiles.ReadFiles(guideFolder, examples));
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

        if (request.UserSnippets.Count > 0)
        {
            AiTaskFiles.AppendSnippets(text, request.UserSnippets, WriteHint, GameFileHint);
        }

        AppendHotspotBlocks(text, request.Draft.Snippets);
        if (RecipeReference(request, recipeGuideFolder) is { } recipe)
        {
            text.Append("\n# Patch recipe format (optional)\n\n").Append(PerformanceTexts.AiRecipeIntro).Append("\n\n").Append(recipe).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Ordner für KIs mit Dateizugriff: Anleitung, task.md, mod/ und game/ mit den angehakten Dateien (ganz, auch binär), snippets.md mit allen
    /// Codeblöcken und, wenn gewünscht, recipe-format.md.
    /// </summary>
    public static void BuildFolder(PerformanceTaskRequest request, string guideFolder, string? recipeGuideFolder, string targetFolder)
    {
        FolderCopy.CopyTree(guideFolder, targetFolder);
        File.WriteAllText(Path.Combine(targetFolder, "task.md"), TaskMarkdown(request, folderMode: true), new UTF8Encoding(false));
        if (request.ModRoot is { } mod)
        {
            AiTaskFiles.CopyFiles(mod, request.ModFiles, Path.Combine(targetFolder, "mod"));
        }

        if (request.GameRoot is { } game)
        {
            AiTaskFiles.CopyFiles(game, request.GameFiles, Path.Combine(targetFolder, "game"));
        }

        if (request.UserSnippets.Count > 0 || request.Draft.Snippets.Count > 0)
        {
            var snippets = new StringBuilder();
            if (request.UserSnippets.Count > 0)
            {
                AiTaskFiles.AppendSnippets(snippets, request.UserSnippets, WriteHint, GameFileHint);
            }

            AppendHotspotBlocks(snippets, request.Draft.Snippets);
            File.WriteAllText(Path.Combine(targetFolder, "snippets.md"), snippets.ToString(), new UTF8Encoding(false));
        }

        if (RecipeReference(request, recipeGuideFolder) is { } recipe)
        {
            File.WriteAllText(Path.Combine(targetFolder, "recipe-format.md"), PerformanceTexts.AiRecipeIntro + "\n\n" + recipe + "\n", new UTF8Encoding(false));
        }
    }

    /// <summary>Text für "Fehler für die KI kopieren": PMTs Meldungen als Liste, mit <see cref="PerformanceTexts.AiFeedbackIntro"/> eingeleitet.</summary>
    public static string Feedback(IEnumerable<string> messages) => AiTaskFiles.Feedback(PerformanceTexts.AiFeedbackIntro, messages);

    private static string TaskMarkdown(PerformanceTaskRequest request, bool folderMode)
    {
        var text = new StringBuilder("# Task\n\n");
        text.Append($"Game: {request.GameId}\n\n");
        text.Append(request.Draft.Markdown.Replace("\r\n", "\n").TrimEnd('\n')).Append("\n\n");
        text.Append("## What the modder wants\n\n").Append(request.Description.Trim().Replace("\r\n", "\n")).Append('\n');
        var hasFiles = request.ModFiles.Count > 0 || request.GameFiles.Count > 0;
        if (hasFiles)
        {
            text.Append("\n## Files from the mod and the game\n\n");
            text.Append(folderMode
                ? "Mod files are in the folder `mod/` (paths relative to the mod root), game files in `game/` (paths relative to the game root).\n"
                : "Each file follows below between `----- mod file: <path> -----` or `----- game file: <path> -----` and `----- end -----`. Paths are relative to the mod root or the game root.\n");
        }

        if (request.Draft.Snippets.Count > 0 || request.UserSnippets.Count > 0)
        {
            text.Append(folderMode
                ? "\nThe code blocks around the hotspots and the modder's own code blocks are in `snippets.md`.\n"
                : "\nThe code blocks around the hotspots and the modder's own code blocks follow below.\n");
        }

        if (folderMode)
        {
            text.Append("\n## Where to put the answer\n\nWrite your whole answer, including a recipe block if you give one, to `answer.md` in this folder.\n");
        }

        return text.ToString();
    }

    /// <summary>Die Blöcke um die Fundstellen als Abschnitte <c>----- hotspot block: pfad:start-ende -----</c> bis <c>----- end -----</c>.</summary>
    private static void AppendHotspotBlocks(StringBuilder text, IReadOnlyList<AiTaskDraftSnippet> snippets)
    {
        if (snippets.Count == 0)
        {
            return;
        }

        text.Append('\n');
        foreach (var snippet in snippets)
        {
            text.Append($"----- hotspot block: {snippet.Path}:{snippet.StartLine}-{snippet.EndLine} -----\n");
            if (snippet.Note.Trim().Length > 0)
            {
                text.Append("Note: ").Append(snippet.Note.Trim()).Append('\n');
            }

            text.Append(snippet.Code.Replace("\r\n", "\n").TrimEnd('\n')).Append("\n----- end -----\n");
        }
    }

    /// <summary>reference.md der Rezept-Anleitung, wenn das Format gewünscht ist und es die Datei gibt.</summary>
    private static string? RecipeReference(PerformanceTaskRequest request, string? recipeGuideFolder)
    {
        if (!request.WithRecipeFormat || recipeGuideFolder is null || !File.Exists(Path.Combine(recipeGuideFolder, ReferenceFile)))
        {
            return null;
        }

        return Read(recipeGuideFolder, ReferenceFile);
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
