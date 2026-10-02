namespace ParadoxModTool.ErrorLog;

/// <summary>Eine Zeile des Codeausschnitts; <c>IsTarget</c> ist die Fehlerzeile.</summary>
public sealed record ExcerptLine(int Number, string Text, bool IsTarget);

/// <summary>Codeausschnitt an der Fehlerstelle (Spec: ±5 Zeilen, Zeile markiert).</summary>
public static class CodeExcerpt
{
    public static IReadOnlyList<ExcerptLine> Read(string path, int line, int radius = 5)
    {
        try
        {
            if (!File.Exists(path))
            {
                return Array.Empty<ExcerptLine>();
            }

            var lines = File.ReadAllLines(path);
            if (line < 1 || line > lines.Length)
            {
                return Array.Empty<ExcerptLine>();
            }

            var from = Math.Max(1, line - radius);
            var to = Math.Min(lines.Length, line + radius);
            return Enumerable.Range(from, to - from + 1).Select(n => new ExcerptLine(n, lines[n - 1], n == line)).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<ExcerptLine>();
        }
    }
}
