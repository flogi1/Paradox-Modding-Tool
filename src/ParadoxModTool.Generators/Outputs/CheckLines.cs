namespace ParadoxModTool.Generators;

/// <summary>Zeilen einer Prüfung (Ausgabeart "check"): je nicht leere Zeile <c>Muster =&gt; Argument</c>, geteilt am letzten <c> =&gt; </c>.</summary>
public static class CheckLines
{
    private const string Arrow = " => ";

    public static IReadOnlyList<(string Pattern, string Argument)> Parse(string rendered)
    {
        var lines = new List<(string, string)>();
        foreach (var raw in rendered.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var split = line.LastIndexOf(Arrow, StringComparison.Ordinal);
            lines.Add(split < 0 ? (line, string.Empty) : (line[..split].Trim(), line[(split + Arrow.Length)..].Trim()));
        }

        return lines;
    }
}
