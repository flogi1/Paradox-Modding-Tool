namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Welche Zeilen eines Editors im Vergleich eingefärbt werden und wie. Hinzugefügte und entfernte Einträge färben ihre ganze Zeilenspanne,
/// geänderte Einzelwerte ihre Zeile. Ein geänderter Block mit Kind-Diff färbt seine Kopfzeile (<c>effect = {</c>) nur, wenn sich der Kopf selbst
/// unterscheidet (Operator); sonst färben allein die geänderten Kinder, damit die Färbung strikt die Unterschiede zeigt.
/// </summary>
public static class DiffLineKinds
{
    /// <summary>Zeilennummer (1-basiert) je gefärbter Zeile. <paramref name="lineOf"/> liefert zu einem Quelltext-Offset die Zeilennummer dieser Seite.</summary>
    public static Dictionary<int, DiffKind> Compute(IReadOnlyList<EntryDiff> diffs, bool useLeftSide, Func<int, int> lineOf)
    {
        var lineKinds = new Dictionary<int, DiffKind>();
        Collect(diffs, useLeftSide, lineOf, lineKinds);
        return lineKinds;
    }

    private static void Collect(IReadOnlyList<EntryDiff> diffs, bool useLeftSide, Func<int, int> lineOf, Dictionary<int, DiffKind> lineKinds)
    {
        foreach (var diff in diffs)
        {
            var entry = useLeftSide ? diff.Left : diff.Right;
            var hasChildren = diff.Kind == DiffKind.Modified && diff.Children is not null;

            if (diff.Kind != DiffKind.Unchanged && entry?.SourceStart is int start && entry.SourceEnd is int end && (!hasChildren || HeadDiffers(diff)))
            {
                var firstLine = lineOf(start);
                var lastLine = hasChildren ? firstLine : lineOf(Math.Max(start, end - 1));
                for (var line = firstLine; line <= lastLine; line++)
                {
                    lineKinds[line] = diff.Kind;
                }
            }

            if (diff.Children is not null)
            {
                Collect(diff.Children, useLeftSide, lineOf, lineKinds);
            }
        }
    }

    /// <summary>Unterscheidet sich der Kopf eines gepaarten Blocks (Schlüssel und Operator)? Der Schlüssel ist beim Paaren gleich; geändert sein kann der Operator.</summary>
    private static bool HeadDiffers(EntryDiff diff) =>
        diff.Left is ScriptStatement left && diff.Right is ScriptStatement right
        && (!string.Equals(left.Key, right.Key, StringComparison.Ordinal) || left.Operator != right.Operator);
}
