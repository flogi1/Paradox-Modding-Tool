using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Generators;

/// <summary>Lage eines Blocks im Text: Beginn der Anweisung, Index von <c>{</c> und von <c>}</c>.</summary>
internal readonly record struct BlockSpan(int StatementStart, int Open, int Close);

/// <summary>Blöcke über die Quellspannen des Parsers finden und Zeilen vor dem schließenden <c>}</c> einfügen; der Rest bleibt zeichengleich.</summary>
internal static class BlockText
{
    /// <summary>
    /// Den Block zu einem Pfad finden (<c>a.b</c>, je erster Treffer); fehlt er, gibt es einen Fehler mit dem Pfad. Ein Pfadteil mit Leerzeichen ist eine
    /// GUI-Kopfzeile: <c>types name</c>, <c>type name</c> (aus <c>type name = basis { }</c>) oder <c>template name</c>, gelesen wie in GuiUnits.
    /// </summary>
    public static BlockSpan Find(string text, string path) => FindWithEntries(text, path).Span;

    /// <summary>Wie <see cref="Find"/>, dazu die Einträge des gefundenen Blocks (für den Abgleich nach Treffer).</summary>
    public static (BlockSpan Span, IReadOnlyList<ScriptEntry> Entries) FindWithEntries(string text, string path)
    {
        ScriptBlock root;
        try
        {
            root = ScriptDocument.Parse(text);
        }
        catch (Exception ex) when (ex is not GeneratorOutputException)
        {
            throw new GeneratorOutputException(GeneratorsTexts.ParseFailed(ex.Message));
        }

        // Gleichnamige Geschwister der Reihe nach probieren, bis der Rest des Pfads passt (z. B. das dritte "widget" mit der scrollbox)
        if (Search(root.Entries, SplitPath(path), 0) is not { } hit)
        {
            throw new GeneratorOutputException(GeneratorsTexts.BlockMissing(path));
        }

        var end = hit.Found.SourceEnd ?? text.Length;
        var blockStart = hit.Found.SourceStart ?? throw new GeneratorOutputException(GeneratorsTexts.BlockMissing(path));
        return (Span(text, hit.Start ?? blockStart, blockStart, end, path), hit.Entries);
    }

    private static (ScriptEntry Found, int? Start, IReadOnlyList<ScriptEntry> Entries)? Search(IReadOnlyList<ScriptEntry> entries, string[] parts, int index)
    {
        foreach (var (found, start) in Candidates(entries, parts[index]))
        {
            var inner = ((ScriptBlock)(found switch { ScriptStatement s => s.Value, ScriptListItem l => l.Value, _ => throw new InvalidOperationException() })).Entries;
            if (index == parts.Length - 1)
            {
                return (found, start, inner);
            }

            if (Search(inner, parts, index + 1) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    /// <summary>Die Blöcke einer Ebene, die zu einem Pfadteil passen, in Quellreihenfolge, mit dem Beginn ihrer Kopfzeile.</summary>
    /// <summary>Pfad an Punkten teilen, außer innerhalb eines Inhaltsfilters in eckigen Klammern.</summary>
    private static string[] SplitPath(string path)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < path.Length; i++)
        {
            depth += path[i] == '[' ? 1 : path[i] == ']' ? -1 : 0;
            if (path[i] == '.' && depth == 0)
            {
                parts.Add(path[start..i]);
                start = i + 1;
            }
        }

        parts.Add(path[start..]);
        return parts.ToArray();
    }

    private static IEnumerable<(ScriptEntry Found, int? Start)> Candidates(IReadOnlyList<ScriptEntry> entries, string part)
    {
        // "if[var:x = 3]": nur Geschwister, deren Text den Inhalt enthält; "[!text]" nur solche ohne; mehrere Filter hintereinander gelten alle
        if (part.EndsWith(']') && part.LastIndexOf('[') is > 0 and var open)
        {
            var needle = part[(open + 1)..^1];
            var negate = needle.StartsWith('!');
            needle = negate ? needle[1..] : needle;
            return Candidates(entries, part[..open]).Where(c => c.Found.SourceText is { } source && c.Found.SourceStart is { } s && c.Found.SourceEnd is { } e
                && source[s..e].Contains(needle, StringComparison.Ordinal) != negate);
        }

        // "if#3": nur das dritte gleichnamige Geschwister (ab 1)
        var hash = part.LastIndexOf('#');
        if (hash > 0 && int.TryParse(part[(hash + 1)..], out var nth) && nth > 0)
        {
            return Candidates(entries, part[..hash]).Skip(nth - 1).Take(1);
        }

        return AllCandidates(entries, part);
    }

    private static IEnumerable<(ScriptEntry Found, int? Start)> AllCandidates(IReadOnlyList<ScriptEntry> entries, string part)
    {
        var space = part.IndexOf(' ');
        if (space <= 0)
        {
            foreach (var statement in entries.OfType<ScriptStatement>().Where(s => s.Key == part && s.Value is ScriptBlock))
            {
                yield return (statement, statement.SourceStart);
            }

            yield break;
        }

        var word = part[..space];
        var name = part[(space + 1)..].Trim().Trim('"');
        var units = EntryUnits.Build(entries).Where(u => u.Kind == EntryUnit.UnitKind.Body && u.Value is ScriptBlock).ToList();
        var direct = units.Where(u => FirstWord(u) == word && HeaderName(u).Trim('"') == name).ToList();
        if (direct.Count == 0 && word == "type")
        {
            // "type X" ohne "types"-Teil: in jedem types-Block dieser Ebene suchen (wie der Python-Generator, der den Namen der Hülle nicht kennt).
            direct = units.Where(u => FirstWord(u) == "types")
                .SelectMany(u => EntryUnits.Build(((ScriptBlock)u.Value).Entries))
                .Where(u => u.Kind == EntryUnit.UnitKind.Body && u.Value is ScriptBlock && FirstWord(u) == word && HeaderName(u).Trim('"') == name)
                .ToList();
        }

        foreach (var unit in direct)
        {
            yield return (unit.Entry, unit.Header.FirstOrDefault()?.SourceStart ?? unit.Entry.SourceStart);
        }
    }

    private static BlockSpan Span(string text, int start, int blockStart, int end, string path)
    {
        var open = text.IndexOf('{', blockStart);
        var close = text.LastIndexOf('}', Math.Max(0, end - 1));
        if (open < 0 || close < open)
        {
            throw new GeneratorOutputException(GeneratorsTexts.BlockMissing(path));
        }

        return new BlockSpan(start, open, close);
    }

    private static string? FirstWord(EntryUnit unit) =>
        unit.Header.Count > 0 && unit.Header[0] is ScriptListItem { Value: ScriptScalar word } ? word.Text : null;

    /// <summary>"template name" → name, "type name = basis" → name (wie GuiUnits).</summary>
    private static string HeaderName(EntryUnit unit) =>
        unit.Header.Skip(1).Select(h => h switch
        {
            ScriptStatement statement => statement.Key,
            ScriptListItem { Value: ScriptScalar value } => value.Text,
            _ => string.Empty,
        }).FirstOrDefault(name => name.Length > 0) ?? string.Empty;

    /// <summary>
    /// Inhalt eines Blocks als ganze Zeilen (mit Einrückung, ohne Zeilenende), ohne die Einträge mit einem Schlüssel aus <paramref name="except"/>;
    /// Leerzeilen fallen weg, Kommentare und verschachtelte Blöcke bleiben (Einleser und <c>keep</c> nutzen dieselbe Sicht).
    /// </summary>
    public static List<string> BodyLines(string text, ScriptStatement statement, IReadOnlyCollection<string> except)
    {
        var start = statement.SourceStart ?? 0;
        var end = statement.SourceEnd ?? text.Length;
        var open = text.IndexOf('{', start);
        var close = text.LastIndexOf('}', Math.Max(0, end - 1));
        if (open < 0 || close <= open || statement.Value is not ScriptBlock block)
        {
            return new List<string>();
        }

        var inner = text[(open + 1)..close];
        foreach (var entry in block.Entries.OfType<ScriptStatement>().Where(e => except.Contains(e.Key)).OrderByDescending(e => e.SourceStart))
        {
            var from = (entry.SourceStart ?? open + 1) - open - 1;
            var to = (entry.SourceEnd ?? close) - open - 1;
            inner = inner[..from] + inner[to..];
        }

        return inner.Split('\n').Select(l => l.TrimEnd('\r', ' ', '\t')).Where(l => l.Trim().Length > 0).ToList();
    }

    /// <summary>Zeilen ohne ihre gemeinsame Einrückung, mit LF verbunden.</summary>
    public static string Dedent(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var indent = lines.Min(l => l.Length - l.TrimStart(' ', '\t').Length);
        return string.Join("\n", lines.Select(l => l[indent..]));
    }

    /// <summary>Einrückung der Einträge eines Blocks: die des ersten Eintrags, sonst die der Blockzeile plus ein Tab.</summary>
    public static string EntryIndent(string text, BlockSpan block)
    {
        var position = LineEnd(text, block.Open);
        while (position < block.Close)
        {
            var lineStart = position;
            var lineEnd = LineEnd(text, lineStart);
            var line = text[lineStart..Math.Min(lineEnd, block.Close)];
            if (line.Trim().Length > 0)
            {
                return LeadingWhitespace(line);
            }

            position = lineEnd;
        }

        return LeadingWhitespace(text[LineStart(text, block.StatementStart)..]) + "\t";
    }

    /// <summary>
    /// Zeilen (je mit Zeilenende) vor dem schließenden <c>}</c> einfügen. Steht vor <c>}</c> in derselben Zeile noch Text (<c>s = { }</c>),
    /// kommt <c>}</c> auf eine eigene Zeile mit der Einrückung der Blockzeile.
    /// </summary>
    public static string InsertBeforeClose(string text, BlockSpan block, string lines, string newLine)
    {
        var closeLineStart = LineStart(text, block.Close);
        if (text[closeLineStart..block.Close].Trim().Length == 0)
        {
            return text[..closeLineStart] + lines + text[closeLineStart..];
        }

        var keep = block.Close;
        while (keep > block.Open + 1 && text[keep - 1] is ' ' or '\t')
        {
            keep--;
        }

        var closingIndent = LeadingWhitespace(text[LineStart(text, block.StatementStart)..]);
        return text[..keep] + newLine + lines + closingIndent + text[block.Close..];
    }

    /// <summary>Anfänge der Zeilen im Bereich [<paramref name="from"/>, <paramref name="to"/>) mit ihrem getrimmten Inhalt.</summary>
    public static IEnumerable<(int Start, int End, string Trimmed)> Lines(string text, int from, int to)
    {
        var position = LineStart(text, from);
        while (position < to)
        {
            var end = LineEnd(text, position);
            yield return (position, end, text[position..Math.Min(end, to)].Trim());
            position = end;
        }
    }

    /// <summary>Zeilen eines Inhalts mit Einrückung und Zeilenende; Leerzeilen bleiben ohne Einrückung, am Ende fallen sie weg.</summary>
    public static string Indent(string content, string indent, string newLine)
    {
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Concat(lines.Select(l => (l.Trim().Length == 0 ? string.Empty : indent + l) + newLine));
    }

    public static int LineStart(string text, int position) => position == 0 ? 0 : text.LastIndexOf('\n', Math.Min(position, text.Length) - 1) + 1;

    /// <summary>Index nach dem Zeilenende der Zeile von <paramref name="position"/> (oder Textende).</summary>
    public static int LineEnd(string text, int position)
    {
        var newLine = text.IndexOf('\n', position);
        return newLine < 0 ? text.Length : newLine + 1;
    }

    public static string LeadingWhitespace(string line) => line[..(line.Length - line.TrimStart(' ', '\t').Length)];
}
