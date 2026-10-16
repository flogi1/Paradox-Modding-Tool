using System.Text.Json;

namespace ParadoxModTool.Patches.Ai;

/// <summary>
/// Gelesene Antwort einer KI: das Rezept (<see langword="null"/> bei Problemen), die Probleme als fertige Meldungen und die Eigenschaften, die
/// das Modell nicht kennt (<c>&lt;JSON-Pfad&gt;: &lt;Name&gt;</c>, werden ignoriert und gehen als Warnung in die Prüfung).
/// </summary>
public sealed record RecipeAnswer(PatchRecipe? Recipe, IReadOnlyList<string> Problems, IReadOnlyList<string> UnknownProperties);

/// <summary>
/// Liest die Antwort einer KI auf einen Rezept-Auftrag (Spec 2026-10-12-patch-recipes-ai-guide, Abschnitt 2 "Ergebnis"): ein Dateiblock
/// <c>recipe.json</c> aus einer Chat-Antwort oder die Datei <c>recipe.json</c> in einem Ordner. Das JSON liest <see cref="PatchRecipeStore.Deserialize"/>
/// tolerant (Kommentare, Endkommas, Regelart und Enum-Werte in beliebiger Schreibweise, Null-Listen als leer). Liest nur.
/// </summary>
public static class RecipeAnswerReader
{
    private const string RecipeJson = "recipe.json";

    /// <summary>Chat-Antwort mit Dateiblöcken; erlaubt ist nur <c>recipe.json</c>, bei einem Problem entsteht kein Rezept.</summary>
    public static RecipeAnswer FromText(string answer)
    {
        var blocks = AiFileBlockParser.Parse(answer ?? string.Empty, path => path == RecipeJson, RecipeJson);
        if (blocks.Problems.Count > 0)
        {
            return Failed(blocks.Problems.Select(Message).ToList());
        }

        return FromJson(blocks.Files[RecipeJson]);
    }

    /// <summary>Ordner mit <c>recipe.json</c>; eine fehlende oder nicht lesbare Datei ist ein Problem.</summary>
    public static RecipeAnswer FromFolder(string folder)
    {
        var path = Path.Combine(folder, RecipeJson);
        if (!File.Exists(path))
        {
            return Failed(new[] { PatchTexts.AiFolderNoRecipeJson(folder) });
        }

        try
        {
            return FromJson(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failed(new[] { PatchTexts.AiJsonProblem(ex.Message) });
        }
    }

    /// <summary>Der Text von <c>recipe.json</c>; ein nicht lesbares JSON wird zum Problem <c>recipe.json: &lt;Meldung&gt;</c>.</summary>
    public static RecipeAnswer FromJson(string json)
    {
        var unknown = new List<string>();
        try
        {
            var recipe = PatchRecipeStore.Deserialize(json ?? string.Empty, unknown);
            return recipe is null
                ? Failed(new[] { PatchTexts.AiJsonProblem("null") })
                : new RecipeAnswer(recipe, Array.Empty<string>(), unknown);
        }
        catch (JsonException ex)
        {
            return Failed(new[] { PatchTexts.AiJsonProblem(ex.Message) });
        }
    }

    private static RecipeAnswer Failed(IReadOnlyList<string> problems) => new(null, problems, Array.Empty<string>());

    private static string Message(AiBlockProblem problem) => problem.Kind switch
    {
        AiBlockProblemKind.BlockWithoutEnd => PatchTexts.AiBlockWithoutEnd(problem.Path),
        AiBlockProblemKind.NotAllowed => PatchTexts.AiFileNotAllowed(problem.Path),
        AiBlockProblemKind.Duplicate => PatchTexts.AiFileDuplicate(problem.Path),
        AiBlockProblemKind.MissingRequired => PatchTexts.AiNoRecipeJson,
        _ => PatchTexts.AiNoFiles,
    };
}
