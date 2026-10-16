namespace ParadoxModTool.Patches.Ai;

/// <summary>Art eines Problems beim Lesen der Dateiblöcke einer KI-Antwort.</summary>
public enum AiBlockProblemKind
{
    /// <summary>Kein einziger Block in der Antwort (<see cref="AiBlockProblem.Path"/> leer).</summary>
    NoFiles,

    /// <summary>Block ohne <c>&lt;&lt;&lt;END&gt;&gt;&gt;</c>.</summary>
    BlockWithoutEnd,

    /// <summary>Pfad ist nicht erlaubt.</summary>
    NotAllowed,

    /// <summary>Derselbe Pfad kommt ein zweites Mal vor.</summary>
    Duplicate,

    /// <summary>Die verlangte Datei fehlt (<see cref="AiBlockProblem.Path"/> = ihr Pfad).</summary>
    MissingRequired,
}

/// <summary>Ein Problem mit Art und Pfad des betroffenen Blocks.</summary>
public sealed record AiBlockProblem(AiBlockProblemKind Kind, string Path);

/// <summary>Dateien aus einer KI-Antwort (relativer Pfad mit "/" → Text mit LF) und Probleme; bei Problemen legt PMT nichts an.</summary>
public sealed record AiFileBlocks(IReadOnlyDictionary<string, string> Files, IReadOnlyList<AiBlockProblem> Problems);

/// <summary>
/// Liest die Dateiblöcke einer KI-Antwort: eine Zeile <c>&lt;&lt;&lt;FILE pfad&gt;&gt;&gt;</c>, der Inhalt, eine Zeile <c>&lt;&lt;&lt;END&gt;&gt;&gt;</c>.
/// Code-Zäune direkt am Anfang und Ende eines Blocks fallen weg, Text außerhalb wird übergangen. Welche Pfade erlaubt sind und welche Datei
/// vorkommen muss, bestimmt der Aufrufer.
/// </summary>
public static class AiFileBlockParser
{
    private const string Begin = "<<<FILE ";
    private const string EndMarker = "<<<END>>>";

    public static AiFileBlocks Parse(string text, Func<string, bool> isAllowed, string requiredFile)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var problems = new List<AiBlockProblem>();
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
                problems.Add(new AiBlockProblem(AiBlockProblemKind.BlockWithoutEnd, current));
            }
            else if (!isAllowed(current))
            {
                problems.Add(new AiBlockProblem(AiBlockProblemKind.NotAllowed, current));
            }
            else if (files.ContainsKey(current))
            {
                problems.Add(new AiBlockProblem(AiBlockProblemKind.Duplicate, current));
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
            problems.Add(new AiBlockProblem(AiBlockProblemKind.NoFiles, string.Empty));
        }
        else if (!files.ContainsKey(requiredFile))
        {
            problems.Add(new AiBlockProblem(AiBlockProblemKind.MissingRequired, requiredFile));
        }

        return new AiFileBlocks(files, problems);
    }

    private static IEnumerable<string> WithoutFences(List<string> lines)
    {
        var start = lines.Count > 0 && lines[0].TrimStart().StartsWith("```", StringComparison.Ordinal) ? 1 : 0;
        var end = lines.Count > start && lines[^1].Trim() == "```" ? lines.Count - 1 : lines.Count;
        return lines.Skip(start).Take(end - start);
    }
}
