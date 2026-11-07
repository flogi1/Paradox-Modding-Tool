using ParadoxModTool.Scopes;

namespace ParadoxModTool.Performance;

/// <summary>Der Block auf oberster Ebene, in dem eine Zeile liegt: Schlüssel, Zeilenbereich (1-basiert, einschließlich), Quelltext und Schlüsselpfad.</summary>
/// <param name="KeyPath">Vom obersten Schlüssel bis zum innersten Block, der die Zeile enthält; Blöcke ohne Schlüssel (Listenwerte) fehlen.</param>
public sealed record ScriptBlockInfo(string TopLevelKey, int StartLine, int EndLine, string BlockText, IReadOnlyList<string> KeyPath);

/// <summary>Findet zu einer Zeile eines Skripts den umgebenden Block, damit Hotspots auf Quelltext und Regeln abgebildet werden können.</summary>
public static class ScriptBlockLocator
{
    /// <summary>
    /// Liefert den Block auf oberster Ebene, der die Zeile berührt; <c>null</c>, wenn die Zeile in keinem Block liegt (Leerzeile, Kommentar,
    /// Zuweisung ohne Block) oder der Text leer ist. Der Parser ist fehlertolerant: ein nicht geschlossener Block reicht bis zum Textende.
    /// </summary>
    public static ScriptBlockInfo? Locate(string text, int line)
    {
        if (string.IsNullOrEmpty(text) || line < 1)
        {
            return null;
        }

        var index = new ScriptLineIndex(text);
        if (line > index.LineCount)
        {
            return null;
        }

        var lineStart = index.StartOf(line);
        var lineEnd = index.EndOf(line);
        var top = Touching(ScriptTree.Parse(text), lineStart, lineEnd);
        if (top is null)
        {
            return null;
        }

        var path = new List<string> { top.Key ?? string.Empty };
        for (var inner = Touching(top.Children, lineStart, lineEnd); inner is not null; inner = Touching(inner.Children, lineStart, lineEnd))
        {
            if (inner.Key is not null)
            {
                path.Add(inner.Key);
            }
        }

        var end = Math.Min(top.End, text.Length);
        var lastChar = Math.Max(top.Start, end - 1);
        return new ScriptBlockInfo(path[0], index.LineOf(top.Start), index.LineOf(lastChar), text[top.Start..end], path);
    }

    /// <summary>Der erste Block unter <paramref name="nodes"/>, der den Bereich [<paramref name="lineStart"/>, <paramref name="lineEnd"/>] der Zeile berührt.</summary>
    private static ScriptNode? Touching(List<ScriptNode>? nodes, int lineStart, int lineEnd) =>
        nodes?.FirstOrDefault(n => n.IsBlock && n.Start < lineEnd && n.End > lineStart);
}
