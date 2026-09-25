using System.Text.RegularExpressions;

namespace ParadoxModTool.GitIntegration;

/// <summary>Wandelt den Changelog-Abschnitt einer Version in Markdown für die Release-Notes: Hash-Linien entfallen, Unterbanner werden <c>### Titel</c>, Aufzählungen bleiben.</summary>
public static class ReleaseNotesFormatter
{
    /// <summary>Die Grenze von GitHub für den Text eines Releases.</summary>
    public const int MaxLength = 125000;

    private static readonly Regex HashLine = new(@"^#{10,}\s*$", RegexOptions.Compiled);
    private static readonly Regex TitleLine = new(@"^#{3,}[ \t]+([^#\s].*?)\s*$", RegexOptions.Compiled);

    public static string ToMarkdown(string? sectionText)
    {
        if (string.IsNullOrWhiteSpace(sectionText))
        {
            return string.Empty;
        }

        var result = new List<string>();
        foreach (var raw in sectionText.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (HashLine.IsMatch(line))
            {
                continue;
            }

            if (TitleLine.Match(line) is { Success: true } title)
            {
                line = "### " + title.Groups[1].Value;
            }

            if (line.Length == 0 && (result.Count == 0 || result[^1].Length == 0))
            {
                continue;
            }

            result.Add(line);
        }

        while (result.Count > 0 && result[^1].Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }

        return string.Join("\n", result);
    }

    public static bool IsTooLong(string notes) => notes.Length > MaxLength;
}
