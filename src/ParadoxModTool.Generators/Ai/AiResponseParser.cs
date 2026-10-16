using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Patches.Ai;

namespace ParadoxModTool.Generators.Ai;

/// <summary>Dateien aus einer KI-Antwort (relativer Pfad mit "/" → Text mit LF) und Probleme; bei Problemen legt PMT nichts an.</summary>
public sealed record AiParseResult(IReadOnlyDictionary<string, string> Files, IReadOnlyList<string> Problems);

/// <summary>
/// Liest die Dateiblöcke einer KI-Antwort (Spec 2026-10-05-generator-packages-ai-guide, Abschnitt 1, Schritt 8) mit <see cref="AiFileBlockParser"/>
/// und übersetzt dessen Probleme in die Meldungen der Generator-Pakete. Erlaubt sind nur package.json, functions.sbn und templates/*.sbn.
/// </summary>
public static class AiResponseParser
{
    private const string PackageJson = "package.json";

    public static AiParseResult Parse(string text)
    {
        var blocks = AiFileBlockParser.Parse(text, IsAllowed, PackageJson);
        return new AiParseResult(blocks.Files, blocks.Problems.Select(Message).ToList());
    }

    /// <summary>package.json, functions.sbn oder templates/&lt;name&gt;.sbn ohne Unterordner.</summary>
    public static bool IsAllowed(string path) =>
        path is PackageJson or "functions.sbn"
        || path.StartsWith("templates/", StringComparison.Ordinal) && PackageFiles.IsValidTemplateName(path["templates/".Length..]);

    private static string Message(AiBlockProblem problem) => problem.Kind switch
    {
        AiBlockProblemKind.BlockWithoutEnd => GeneratorsTexts.AiBlockWithoutEnd(problem.Path),
        AiBlockProblemKind.NotAllowed => GeneratorsTexts.AiFileNotAllowed(problem.Path),
        AiBlockProblemKind.Duplicate => GeneratorsTexts.AiFileDuplicate(problem.Path),
        AiBlockProblemKind.MissingRequired => GeneratorsTexts.AiNoPackageJson,
        _ => GeneratorsTexts.AiNoFiles,
    };
}
