using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.ReferenceTree;

/// <summary>Macht aus der Cursorstelle oder Auswahl im Editor den Namen für den Verweisbaum.</summary>
public static class ReferenceQuery
{
    private static readonly string[] Prefixes = { "var:", "global_var:", "local_var:", "flag:", "scope:" };

    /// <summary>
    /// Name aus Zeile und Spalte (0-basiert). Anders als beim Flag-Tracker bleiben Punkte im Namen (Event-IDs wie <c>iv2.5</c>) erhalten;
    /// nur ein Präfix wie <c>var:</c> wird abgeschnitten. Null bei leerem Ergebnis oder Platzhaltern.
    /// </summary>
    public static string? FromEditor(string line, int column, string? selection)
    {
        var text = FlagQuery.RawFromEditor(line, column, selection)?.Trim('.');
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        // Nach dem letzten Präfix zählt nur der Rest (z. B. scope:actor.var:x -> x).
        var cut = -1;
        var cutLength = 0;
        foreach (var prefix in Prefixes)
        {
            var index = text.LastIndexOf(prefix, StringComparison.Ordinal);
            if (index >= 0 && index + prefix.Length > cut + cutLength)
            {
                cut = index;
                cutLength = prefix.Length;
            }
        }

        if (cut >= 0)
        {
            text = text[(cut + cutLength)..];
        }

        text = text.Trim('.');
        return text.Length == 0 || text.Contains('$') || text.Contains('*') ? null : text;
    }
}
