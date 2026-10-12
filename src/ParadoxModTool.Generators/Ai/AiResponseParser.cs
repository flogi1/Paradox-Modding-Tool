using ParadoxModTool.Generators.Editing.Package;

namespace ParadoxModTool.Generators.Ai;

/// <summary>Dateien aus einer KI-Antwort (relativer Pfad mit "/" → Text mit LF) und Probleme; bei Problemen legt PMT nichts an.</summary>
public sealed record AiParseResult(IReadOnlyDictionary<string, string> Files, IReadOnlyList<string> Problems);

/// <summary>
/// Liest die Dateiblöcke einer KI-Antwort (Spec 2026-10-05-generator-packages-ai-guide, Abschnitt 1, Schritt 8): eine Zeile <c>&lt;&lt;&lt;FILE pfad&gt;&gt;&gt;</c>,
/// der Inhalt, eine Zeile <c>&lt;&lt;&lt;END&gt;&gt;&gt;</c>. Code-Zäune direkt am Anfang und Ende eines Blocks fallen weg, Text außerhalb wird übergangen.
/// Erlaubt sind nur package.json, functions.sbn und templates/*.sbn.
/// </summary>
public static class AiResponseParser
{
    private const string Begin = "<<<FILE ";
    private const string EndMarker = "<<<END>>>";

    public static AiParseResult Parse(string text)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var problems = new List<string>();
        string? current = null;
        var content = new List<string>();
        var blocks = 0;

        void Close(bool ended)
        {
            if (current is null)
            {
                return;
            }

            if (!ended)
            {
                problems.Add(GeneratorsTexts.AiBlockWithoutEnd(current));
            }
            else if (!IsAllowed(current))
            {
                problems.Add(GeneratorsTexts.AiFileNotAllowed(current));
            }
            else if (files.ContainsKey(current))
            {
                problems.Add(GeneratorsTexts.AiFileDuplicate(current));
            }
            else
            {
                files[current] = string.Join("\n", WithoutFences(content));
            }

            current = null;
            content.Clear();
        }

        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(Begin, StringComparison.Ordinal) && trimmed.EndsWith(">>>", StringComparison.Ordinal))
            {
                Close(ended: false);
                current = trimmed[Begin.Length..^3].Trim().Replace('\\', '/');
                blocks++;
            }
            else if (trimmed == EndMarker)
            {
                Close(ended: true);
            }
            else if (current is not null)
            {
                content.Add(line);
            }
        }

        Close(ended: false);
        if (blocks == 0)
        {
            problems.Add(GeneratorsTexts.AiNoFiles);
        }
        else if (!files.ContainsKey("package.json"))
        {
            problems.Add(GeneratorsTexts.AiNoPackageJson);
        }

        return new AiParseResult(files, problems);
    }

    /// <summary>package.json, functions.sbn oder templates/&lt;name&gt;.sbn ohne Unterordner.</summary>
    public static bool IsAllowed(string path) =>
        path is "package.json" or "functions.sbn"
        || path.StartsWith("templates/", StringComparison.Ordinal) && PackageFiles.IsValidTemplateName(path["templates/".Length..]);

    private static IEnumerable<string> WithoutFences(List<string> lines)
    {
        var start = lines.Count > 0 && lines[0].TrimStart().StartsWith("```", StringComparison.Ordinal) ? 1 : 0;
        var end = lines.Count > start && lines[^1].Trim() == "```" ? lines.Count - 1 : lines.Count;
        return lines.Skip(start).Take(end - start);
    }
}
