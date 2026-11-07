using ParadoxModTool.Patches.Ai;

namespace ParadoxModTool.Performance;

/// <summary>Gelesene KI-Antwort: der Beratungstext, das optionale Rezept-JSON und die Probleme beim Lesen der Dateiblöcke als fertige Meldungen.</summary>
public sealed record PerformanceAnswer(string Advice, string? RecipeJson, IReadOnlyList<string> Problems);

/// <summary>
/// Liest die Antwort einer KI auf den Performance-Auftrag: Beratungstext, optional ein Dateiblock <c>recipe.json</c>
/// (<c>&lt;&lt;&lt;FILE recipe.json&gt;&gt;&gt;</c> ... <c>&lt;&lt;&lt;END&gt;&gt;&gt;</c>). Ein Rezept ist freiwillig; fehlt der Block, bleibt nur der Text.
/// Das JSON selbst wird hier nicht geprüft.
/// </summary>
public static class PerformanceAnswerReader
{
    public const string RecipeFile = "recipe.json";

    private const string Begin = "<<<FILE ";
    private const string EndMarker = "<<<END>>>";

    public static PerformanceAnswer Read(string text)
    {
        text ??= string.Empty;
        var blocks = AiFileBlockParser.Parse(text, name => name == RecipeFile, RecipeFile);
        var problems = blocks.Problems
            .Where(p => p.Kind is not (AiBlockProblemKind.NoFiles or AiBlockProblemKind.MissingRequired))
            .Select(p => PerformanceTexts.AiBlockProblem(p.Kind, p.Path))
            .ToList();
        return new PerformanceAnswer(AdviceOf(text), blocks.Files.GetValueOrDefault(RecipeFile), problems);
    }

    /// <summary>Der Text ohne Dateiblöcke und ohne Code-Zäune, die einen Block umschließen.</summary>
    private static string AdviceOf(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var kept = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (!(trimmed.StartsWith(Begin, StringComparison.Ordinal) && trimmed.EndsWith(">>>", StringComparison.Ordinal)))
            {
                kept.Add(lines[i]);
                continue;
            }

            // nur ein öffnender Zaun (ungerade Zahl von Zaunzeilen bis hier) gehört zum Block; ein schließender Zaun der Antwort bleibt
            if (kept.Count > 0 && IsFence(kept[^1]) && kept.Count(IsFence) % 2 == 1)
            {
                kept.RemoveAt(kept.Count - 1);
            }

            while (i < lines.Length && lines[i].Trim() != EndMarker)
            {
                i++;
            }

            if (i + 1 < lines.Length && lines[i + 1].Trim() == "```")
            {
                i++;
            }
        }

        return string.Join("\n", kept).Trim();
    }

    private static bool IsFence(string line) => line.TrimStart().StartsWith("```", StringComparison.Ordinal);
}
