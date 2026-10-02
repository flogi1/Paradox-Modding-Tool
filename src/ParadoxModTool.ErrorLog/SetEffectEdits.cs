using System.Text;

namespace ParadoxModTool.ErrorLog;

/// <summary>Eine gefundene Stelle, die sich auskommentieren lässt (Zeilen 1-basiert, einschließlich).</summary>
public sealed record SetStatement(string Path, int StartLine, int EndLine);

/// <summary>Eine gefundene Stelle, die mit anderem Code in einer Zeile steht und daher von Hand bearbeitet werden muss.</summary>
public sealed record SetSkipped(string Path, int Line);

/// <summary>Ergebnis für eine Mod: auskommentierbare und ausgelassene Stellen, nicht lesbare Dateien und die Änderungen als Plan.</summary>
public sealed record SetEffectScan(string ModName, string Folder, IReadOnlyList<SetStatement> Statements, IReadOnlyList<SetSkipped> Skipped, IReadOnlyList<string> Unreadable, ModRenamePlan Plan);

/// <summary>
/// "`set_variable` auskommentieren" (Spielstand-Explorer Plan 2b): findet in den Skriptdateien einer Mod die Effekte aus dem Katalog
/// (<c>setEffects</c>), die einen Namen setzen, als Block <c>set_variable = { name = X ... }</c> oder Kurzform <c>set_variable = X</c>, und
/// kommentiert jede ihrer Zeilen aus (<c># </c> hinter der Einrückung). Kommentare und Zeichenketten zählen bei der Suche nicht. Eine Stelle,
/// die mit anderem Code in einer Zeile steht, wird nie teilweise auskommentiert, sondern ausgelassen.
/// </summary>
public static class SetEffectEdits
{
    public static SetEffectScan Scan(ModSource mod, string name, IReadOnlyList<string> setEffects, IReadOnlyCollection<string> excludedDirectories)
    {
        var scan = NameEdits.Scan(mod.Directory, name, excludedDirectories);
        var statements = new List<SetStatement>();
        var skipped = new List<SetSkipped>();
        var files = new List<RenameFile>();
        foreach (var file in scan.Files.Where(f => IsScript(f.Path)))
        {
            var found = Find(file.Text, name, setEffects);
            var fileStatements = found.Where(f => f.Alone).Select(f => new SetStatement(file.Path, f.StartLine, f.EndLine)).ToList();
            statements.AddRange(fileStatements);
            skipped.AddRange(found.Where(f => !f.Alone).Select(f => new SetSkipped(file.Path, f.StartLine)));
            if (fileStatements.Count > 0)
            {
                var newText = CommentOut(file.Text, fileStatements);
                files.Add(new RenameFile(file.Path, newText, fileStatements.Count, NameEdits.ChangedLines(file.Text, newText), file.HasBom));
            }
        }

        return new SetEffectScan(mod.Name, mod.Directory, statements, skipped, scan.Unreadable.Where(IsScript).ToList(), new ModRenamePlan(mod.Name, mod.Directory, files));
    }

    /// <summary>Vorkommen des Namens, die nicht in einer gefundenen Stelle liegen (ohne Definitionen): dort wird er womöglich noch gelesen.</summary>
    public static IReadOnlyList<SymbolOccurrence> ReadsOutside(SymbolIndex index, string name, IEnumerable<SetEffectScan> scans)
    {
        var covered = scans.SelectMany(s => s.Statements).ToList();
        return index.Find(name)
            .Where(o => o.Role != SymbolRole.Definition)
            .Where(o => !covered.Any(s => string.Equals(ErrorLogSession.PathKey(s.Path), ErrorLogSession.PathKey(o.FilePath), StringComparison.OrdinalIgnoreCase)
                && o.Line >= s.StartLine && o.Line <= s.EndLine))
            .ToList();
    }

    private static bool IsScript(string path) => System.IO.Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase);

    private enum TokenKind
    {
        Word,
        Text,
        Equals,
        Open,
        Close,
    }

    private readonly record struct Token(TokenKind Kind, string Value, int Start, int End);

    private readonly record struct Found(int StartLine, int EndLine, bool Alone);

    private static List<Found> Find(string text, string name, IReadOnlyList<string> setEffects)
    {
        var tokens = Tokenize(text);
        var lineStarts = LineStarts(text);
        var result = new List<Found>();
        for (var i = 0; i + 2 < tokens.Count; i++)
        {
            if (tokens[i].Kind != TokenKind.Word || !setEffects.Contains(tokens[i].Value) || tokens[i + 1].Kind != TokenKind.Equals)
            {
                continue;
            }

            var value = tokens[i + 2];
            int? end = null;
            if (value.Kind == TokenKind.Open)
            {
                end = BlockSettingName(tokens, i + 2, name);
            }
            else if (value.Kind is TokenKind.Word or TokenKind.Text && value.Value == name)
            {
                end = value.End;
            }

            if (end is not int endOffset)
            {
                continue;
            }

            var start = tokens[i].Start;
            var startLine = LineOf(lineStarts, start);
            var endLine = LineOf(lineStarts, endOffset - 1);
            var before = text[lineStarts[startLine - 1]..start];
            var lineEnd = text.IndexOf('\n', endOffset);
            var after = (lineEnd < 0 ? text[endOffset..] : text[endOffset..lineEnd]).Trim();
            var alone = string.IsNullOrWhiteSpace(before) && (after.Length == 0 || after.StartsWith('#'));
            result.Add(new Found(startLine, endLine, alone));
        }

        return result;
    }

    /// <summary>Ende (Offset hinter der schließenden Klammer) eines Blocks, der auf erster Ebene <c>name = X</c> enthält; sonst null.</summary>
    private static int? BlockSettingName(List<Token> tokens, int open, string name)
    {
        var depth = 0;
        var setsName = false;
        for (var j = open; j < tokens.Count; j++)
        {
            switch (tokens[j].Kind)
            {
                case TokenKind.Open:
                    depth++;
                    break;
                case TokenKind.Close:
                    depth--;
                    if (depth == 0)
                    {
                        return setsName ? tokens[j].End : null;
                    }

                    break;
                case TokenKind.Word when depth == 1 && tokens[j].Value == "name" && j + 2 < tokens.Count
                    && tokens[j + 1].Kind == TokenKind.Equals && tokens[j + 2].Kind is TokenKind.Word or TokenKind.Text && tokens[j + 2].Value == name:
                    setsName = true;
                    break;
            }
        }

        return null;
    }

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '#')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
            }
            else if (c == '"')
            {
                var start = i++;
                var value = new StringBuilder();
                while (i < text.Length && text[i] != '"' && text[i] != '\n')
                {
                    if (text[i] == '\\' && i + 1 < text.Length)
                    {
                        i++;
                    }

                    value.Append(text[i++]);
                }

                i = Math.Min(i + 1, text.Length);
                tokens.Add(new Token(TokenKind.Text, value.ToString(), start, i));
            }
            else if (c is '=' or '{' or '}')
            {
                tokens.Add(new Token(c == '=' ? TokenKind.Equals : c == '{' ? TokenKind.Open : TokenKind.Close, c.ToString(), i, i + 1));
                i++;
            }
            else
            {
                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('=' or '{' or '}' or '"' or '#'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Word, text[start..i], start, i));
            }
        }

        return tokens;
    }

    private static List<int> LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts;
    }

    private static int LineOf(List<int> lineStarts, int offset)
    {
        var index = lineStarts.BinarySearch(offset);
        return index >= 0 ? index + 1 : ~index;
    }

    /// <summary>Jede Zeile der Stellen bekommt <c># </c> hinter ihre Einrückung; leere Zeilen, Zeilenenden und alles andere bleiben.</summary>
    private static string CommentOut(string text, IReadOnlyList<SetStatement> statements)
    {
        var lines = text.Split('\n');
        foreach (var statement in statements)
        {
            for (var line = statement.StartLine; line <= statement.EndLine && line <= lines.Length; line++)
            {
                var current = lines[line - 1];
                if (string.IsNullOrWhiteSpace(current))
                {
                    continue;
                }

                var indent = current.Length - current.TrimStart(' ', '\t').Length;
                lines[line - 1] = current[..indent] + "# " + current[indent..];
            }
        }

        return string.Join('\n', lines);
    }
}
