using System.Text;

namespace ParadoxModTool.Scopes;

public enum ScopeTokenKind
{
    Word,
    String,
    Operator,
    Open,
    Close,
}

public readonly record struct ScopeToken(ScopeTokenKind Kind, string Text, int Start, int End);

/// <summary>
/// Zerlegt Paradox-Skript in Token. Anders als der Parser der Vanilla-Diff-Bibliothek ist dieser Lexer fehlertolerant, weil der Editor
/// halbfertigen Code lesen muss: ein nicht geschlossener String endet am Zeilenende, Kommentare werden übersprungen, unbekannte Zeichen werden Wörter.
/// </summary>
public static class ScopeLexer
{
    public static List<ScopeToken> Tokenize(string text)
    {
        var tokens = new List<ScopeToken>();
        var i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
        var n = text.Length;

        while (i < n)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '#')
            {
                while (i < n && text[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (c == '"')
            {
                var start = i++;
                var sb = new StringBuilder();
                while (i < n && text[i] != '"' && text[i] != '\n')
                {
                    if (text[i] == '\\' && i + 1 < n && text[i + 1] == '"')
                    {
                        sb.Append('"');
                        i += 2;
                        continue;
                    }

                    sb.Append(text[i++]);
                }

                if (i < n && text[i] == '"')
                {
                    i++;
                }

                tokens.Add(new ScopeToken(ScopeTokenKind.String, sb.ToString(), start, i));
                continue;
            }

            if (c == '{')
            {
                tokens.Add(new ScopeToken(ScopeTokenKind.Open, "{", i, i + 1));
                i++;
                continue;
            }

            if (c == '}')
            {
                tokens.Add(new ScopeToken(ScopeTokenKind.Close, "}", i, i + 1));
                i++;
                continue;
            }

            if (c is '=' or '<' or '>' || ((c is '!' or '?') && i + 1 < n && text[i + 1] == '='))
            {
                var start = i++;
                if (i < n && text[i] == '=')
                {
                    i++;
                }

                tokens.Add(new ScopeToken(ScopeTokenKind.Operator, text[start..i], start, i));
                continue;
            }

            var wordStart = i;
            while (i < n && !IsDelimiter(text[i]))
            {
                i++;
            }

            if (i == wordStart)
            {
                i++;
                continue;
            }

            tokens.Add(new ScopeToken(ScopeTokenKind.Word, text[wordStart..i], wordStart, i));
        }

        return tokens;
    }

    private static bool IsDelimiter(char c) => char.IsWhiteSpace(c) || c is '{' or '}' or '=' or '<' or '>' or '"' or '#';
}

/// <summary>Ein Eintrag im Skriptbaum: <c>key = { … }</c>, <c>key = wert</c>, ein anonymer Block <c>{ … }</c> oder ein einzelner Listenwert.</summary>
public sealed class ScriptNode
{
    public string? Key { get; init; }

    /// <summary>Skalarer Wert (bei <c>key = wert</c> oder als Listenwert); <see langword="null"/> bei Blöcken.</summary>
    public string? Scalar { get; init; }

    /// <summary>Kinder, wenn der Eintrag einen Block hat; sonst <see langword="null"/>.</summary>
    public List<ScriptNode>? Children { get; init; }

    public int Start { get; init; }

    /// <summary>Position der öffnenden Klammer, sonst -1.</summary>
    public int BlockStart { get; init; } = -1;

    /// <summary>Ende (exklusiv) nach der schließenden Klammer; bei einem nicht geschlossenen Block hinter dem Textende.</summary>
    public int End { get; set; }

    public bool IsBlock => Children is not null;

    /// <summary>Liegt der Cursor innerhalb der Klammern dieses Blocks?</summary>
    public bool ContainsCaret(int caret) => IsBlock && caret > BlockStart && caret < End;
}

/// <summary>Baut aus Token einen fehlertoleranten Baum: fehlende schließende Klammern werden am Textende ergänzt, überzählige ignoriert.</summary>
public static class ScriptTree
{
    public static List<ScriptNode> Parse(string text) => Parse(ScopeLexer.Tokenize(text), text.Length);

    public static List<ScriptNode> Parse(IReadOnlyList<ScopeToken> tokens, int textLength)
    {
        var index = 0;
        return ParseEntries(tokens, ref index, nested: false, textLength, out _);
    }

    private static List<ScriptNode> ParseEntries(IReadOnlyList<ScopeToken> tokens, ref int i, bool nested, int textLength, out int end)
    {
        var nodes = new List<ScriptNode>();
        end = textLength + 1;

        while (i < tokens.Count)
        {
            var token = tokens[i];

            if (token.Kind == ScopeTokenKind.Close)
            {
                i++;
                if (nested)
                {
                    end = token.End;
                    return nodes;
                }

                continue;
            }

            if (token.Kind == ScopeTokenKind.Open)
            {
                i++;
                var children = ParseEntries(tokens, ref i, nested: true, textLength, out var blockEnd);
                nodes.Add(new ScriptNode { Children = children, Start = token.Start, BlockStart = token.Start, End = blockEnd });
                continue;
            }

            if (token.Kind == ScopeTokenKind.Operator)
            {
                i++;
                continue;
            }

            // Wort oder String: Schlüssel, wenn ein Operator folgt, sonst ein Listenwert.
            if (i + 1 < tokens.Count && tokens[i + 1].Kind == ScopeTokenKind.Operator)
            {
                if (i + 2 < tokens.Count && tokens[i + 2].Kind == ScopeTokenKind.Open)
                {
                    var open = tokens[i + 2];
                    i += 3;
                    var children = ParseEntries(tokens, ref i, nested: true, textLength, out var blockEnd);
                    nodes.Add(new ScriptNode { Key = token.Text, Children = children, Start = token.Start, BlockStart = open.Start, End = blockEnd });
                }
                else if (i + 2 < tokens.Count && tokens[i + 2].Kind is ScopeTokenKind.Word or ScopeTokenKind.String)
                {
                    nodes.Add(new ScriptNode { Key = token.Text, Scalar = tokens[i + 2].Text, Start = token.Start, End = tokens[i + 2].End });
                    i += 3;
                }
                else
                {
                    // "key =" ohne Wert (noch beim Tippen).
                    nodes.Add(new ScriptNode { Key = token.Text, Start = token.Start, End = tokens[i + 1].End });
                    i += 2;
                }

                continue;
            }

            nodes.Add(new ScriptNode { Scalar = token.Text, Start = token.Start, End = token.End });
            i++;
        }

        return nodes;
    }
}
