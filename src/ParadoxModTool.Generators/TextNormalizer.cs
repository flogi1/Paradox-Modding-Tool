namespace ParadoxModTool.Generators;

/// <summary>Gleicht gerenderten Text an: keine Leerzeichen oder Tabs am Zeilenende, einheitliche Zeilenenden, genau ein Zeilenende am Schluss.</summary>
public static class TextNormalizer
{
    public static string Normalize(string text, string newLine)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(l => l.TrimEnd(' ', '\t')).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines.Count == 0 ? string.Empty : string.Join(newLine, lines) + newLine;
    }
}
