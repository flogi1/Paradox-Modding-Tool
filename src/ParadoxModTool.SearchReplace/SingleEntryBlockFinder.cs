using System.Text;

namespace ParadoxModTool.SearchReplace;

/// <summary>Ein <c>OR</c>-, <c>NOR</c>- oder <c>AND</c>-Block: Schlüsselwort, Lage (Zeile 1-basiert, Zeichenposition) und Zahl der Einträge auf oberster Ebene.</summary>
public sealed record LogicBlock(string Keyword, int Line, int Offset, int Entries, string Key);

/// <summary>
/// Findet nach einem Ersetzen die Blöcke <c>OR = { }</c>, <c>NOR = { }</c> und <c>AND = { }</c>, in denen nur noch ein Eintrag oder keiner steht (dann lässt sich der
/// Block vereinfachen oder entfernen). Gemeldet werden nur Blöcke, die es vorher nicht gab (<see cref="NewIn"/>); ein Block mit einem Eintrag, der schon
/// vorher so dastand, ist kein Fund des Ersetzens. Kommentare (<c>#</c>) und Zeichenketten zählen nicht; ein Eintrag ist eine Anweisung auf oberster Ebene
/// (<c>a = 1</c>, <c>a = { … }</c>, ein einzelner Wert oder ein Block ohne Namen).
/// </summary>
public static class SingleEntryBlockFinder
{
    private static readonly string[] Keywords = { "OR", "NOR", "AND" };

    private enum Kind
    {
        Word,
        Operator,
        Open,
        Close,
    }

    private readonly record struct Token(Kind Kind, string Text, int Line, int Offset);

    /// <summary>Alle Blöcke der Schlüsselwörter im Text, auch verschachtelte, mit ihrer Eintragszahl.</summary>
    public static IReadOnlyList<LogicBlock> Blocks(string text)
    {
        var tokens = Tokenize(text);
        var blocks = new List<LogicBlock>();
        for (var i = 0; i + 2 < tokens.Count; i++)
        {
            if (tokens[i].Kind != Kind.Word || Array.IndexOf(Keywords, tokens[i].Text) < 0
                || tokens[i + 1] is not { Kind: Kind.Operator, Text: "=" } || tokens[i + 2].Kind != Kind.Open)
            {
                continue;
            }

            var close = MatchingClose(tokens, i + 2);
            if (close < 0)
            {
                continue;
            }

            var entries = CountEntries(tokens, i + 3, close);
            var key = Normalize(tokens, i, close);
            blocks.Add(new LogicBlock(tokens[i].Text, tokens[i].Line, tokens[i].Offset, entries, key));
        }

        return blocks;
    }

    /// <summary>
    /// Blöcke mit höchstens einem Eintrag im neuen Text, die im alten Text nicht schon genauso (bis auf Leerraum) standen. Gleiche Blöcke werden eins zu eins
    /// verrechnet: stand einer vorher einmal da und steht jetzt zweimal da, ist einer neu.
    /// </summary>
    public static IReadOnlyList<LogicBlock> NewIn(string oldText, string newText)
    {
        if (string.Equals(oldText, newText, StringComparison.Ordinal))
        {
            return Array.Empty<LogicBlock>();
        }

        var before = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var block in Blocks(oldText).Where(b => b.Entries <= 1))
        {
            before[block.Key] = before.GetValueOrDefault(block.Key) + 1;
        }

        var added = new List<LogicBlock>();
        foreach (var block in Blocks(newText).Where(b => b.Entries <= 1))
        {
            if (before.GetValueOrDefault(block.Key) > 0)
            {
                before[block.Key]--;
            }
            else
            {
                added.Add(block);
            }
        }

        return added;
    }

    /// <summary>
    /// Aktualisiert eine Liste gemeldeter Blöcke gegen den aktuellen Text: Ein Block, den es dort nicht mehr als Block mit höchstens einem Eintrag gibt
    /// (ergänzt, geändert, entfernt), fällt weg; die übrigen bekommen ihre aktuelle Zeile und Position. Gleiche Blöcke werden eins zu eins zugeordnet.
    /// </summary>
    public static IReadOnlyList<LogicBlock> StillPresent(IReadOnlyList<LogicBlock> stored, string currentText)
    {
        var current = new Dictionary<string, Queue<LogicBlock>>(StringComparer.Ordinal);
        foreach (var block in Blocks(currentText).Where(b => b.Entries <= 1))
        {
            if (!current.TryGetValue(block.Key, out var queue))
            {
                current[block.Key] = queue = new Queue<LogicBlock>();
            }

            queue.Enqueue(block);
        }

        var result = new List<LogicBlock>();
        foreach (var block in stored)
        {
            if (current.TryGetValue(block.Key, out var queue) && queue.Count > 0)
            {
                result.Add(queue.Dequeue());
            }
        }

        return result;
    }

    private static int CountEntries(List<Token> tokens, int start, int end)
    {
        var entries = 0;
        var i = start;
        while (i < end)
        {
            var token = tokens[i];
            if (token.Kind == Kind.Open)
            {
                i = Math.Max(MatchingClose(tokens, i), i) + 1;
                entries++;
            }
            else if (token.Kind == Kind.Word)
            {
                entries++;
                i++;
                if (i < end && tokens[i].Kind == Kind.Operator)
                {
                    i++;
                    if (i < end && tokens[i].Kind == Kind.Open)
                    {
                        i = Math.Max(MatchingClose(tokens, i), i) + 1;
                    }
                    else if (i < end)
                    {
                        i++;
                    }
                }
            }
            else
            {
                i++;
            }
        }

        return entries;
    }

    private static int MatchingClose(List<Token> tokens, int open)
    {
        var depth = 0;
        for (var i = open; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == Kind.Open)
            {
                depth++;
            }
            else if (tokens[i].Kind == Kind.Close && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Der Block als Text mit einfachem Leerraum zwischen den Token, damit Einrückung und Umbrüche keinen Unterschied machen.</summary>
    private static string Normalize(List<Token> tokens, int from, int to) => string.Join(' ', tokens.Skip(from).Take(to - from + 1).Select(t => t.Text));

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var line = 1;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\n')
            {
                line++;
                i++;
            }
            else if (char.IsWhiteSpace(c) || c == '﻿')
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
                var builder = new StringBuilder("\"");
                while (i < text.Length && text[i] != '"' && text[i] != '\n')
                {
                    builder.Append(text[i++]);
                }

                if (i < text.Length && text[i] == '"')
                {
                    i++;
                }

                tokens.Add(new Token(Kind.Word, builder.Append('"').ToString(), line, start));
            }
            else if (c == '{' || c == '}')
            {
                tokens.Add(new Token(c == '{' ? Kind.Open : Kind.Close, c.ToString(), line, i));
                i++;
            }
            else if (IsOperatorChar(c))
            {
                var start = i;
                while (i < text.Length && IsOperatorChar(text[i]))
                {
                    i++;
                }

                tokens.Add(new Token(Kind.Operator, text[start..i], line, start));
            }
            else
            {
                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('{' or '}' or '#' or '"') && !IsOperatorChar(text[i]))
                {
                    i++;
                }

                tokens.Add(new Token(Kind.Word, text[start..i], line, start));
            }
        }

        return tokens;
    }

    private static bool IsOperatorChar(char c) => c is '=' or '<' or '>' or '!' or '?';
}
