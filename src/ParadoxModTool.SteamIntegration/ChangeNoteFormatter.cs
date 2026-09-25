using System.Text.RegularExpressions;

namespace ParadoxModTool.SteamIntegration;

/// <summary>
/// Wandelt einen Changelog-Text in Steam-BBCode um: Aufzählungspunkte (<c>- Text</c>) werden <c>[list][*]Text[/list]</c>, Unterbanner (<c>####### General</c>) werden <c>[b]General[/b]</c>,
/// Rautenlinien entfallen. Steam begrenzt die Changenote auf <see cref="MaxLength"/> Zeichen.
/// </summary>
public static class ChangeNoteFormatter
{
    public const int MaxLength = 8000;

    private static readonly Regex HashLine = new(@"^#{10,}\s*$", RegexOptions.Compiled);
    private static readonly Regex TitleLine = new(@"^#{3,}[ \t]+([^#\s].*?)\s*$", RegexOptions.Compiled);
    private static readonly Regex Bullet = new(@"^\s*[-*]\s+(.*)$", RegexOptions.Compiled);

    public static bool IsTooLong(string text) => text.Length > MaxLength;

    public static string ToBbCode(string text)
    {
        var output = new List<string>();
        var inList = false;

        void CloseList()
        {
            if (inList)
            {
                output.Add("[/list]");
                inList = false;
            }
        }

        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.TrimEnd();
            if (HashLine.IsMatch(line))
            {
                continue;
            }

            if (TitleLine.Match(line) is { Success: true } title)
            {
                CloseList();
                output.Add($"[b]{title.Groups[1].Value}[/b]");
                continue;
            }

            if (line.Length == 0)
            {
                CloseList();
                if (output.Count > 0 && output[^1].Length > 0)
                {
                    output.Add(string.Empty);
                }

                continue;
            }

            if (Bullet.Match(line) is { Success: true } bullet)
            {
                if (!inList)
                {
                    output.Add("[list]");
                    inList = true;
                }

                output.Add("[*]" + bullet.Groups[1].Value.Trim());
                continue;
            }

            if (inList && char.IsWhiteSpace(raw[0]))
            {
                output[^1] += " " + line.Trim();
                continue;
            }

            CloseList();
            output.Add(line);
        }

        CloseList();
        while (output.Count > 0 && output[^1].Length == 0)
        {
            output.RemoveAt(output.Count - 1);
        }

        return string.Join("\n", output);
    }
}
