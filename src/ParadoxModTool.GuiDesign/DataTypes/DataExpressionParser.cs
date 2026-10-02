using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.DataTypes;

/// <summary>
/// Zerlegt einen GUI-Datenausdruck in Aufrufe, Ketten, Konstanten und Rohtext, jeweils mit Textposition. Ein Strukturfehler (Klammern, Hochkommas) macht
/// den ganzen Innentext zu Rohtext und meldet die Stelle.
/// </summary>
public static class DataExpressionParser
{
    public static ParsedExpression Parse(string text)
    {
        var open = text.IndexOf('[');
        var close = text.LastIndexOf(']');
        if (open < 0 || close < open || !IsHull(text, 0, open) || !IsHull(text, close + 1, text.Length))
        {
            return new ParsedExpression(text, string.Empty, null, string.Empty, Array.Empty<ExprError>());
        }

        var start = open + 1;
        var end = close;
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        var prefix = text[..start];
        var suffix = text[end..];
        var errors = CheckStructure(text, start, end);
        var root = errors.Count > 0 ? new RawNode(text[start..end], new TextSpan(start, end)) : ParseNode(text, start, end);
        return new ParsedExpression(text, prefix, root, suffix, errors);
    }

    /// <summary>
    /// Eingetippter Ausdruck ohne Hülle (<c>X.Has('a')</c>) als Knoten ohne Textbezug, damit er in einen anderen Ausdruck passt; leer: <c>null</c>,
    /// kaputt: Rohtext.
    /// </summary>
    public static ExprNode? ParseDetached(string text)
    {
        if (text.Trim().Length == 0)
        {
            return null;
        }

        var parsed = Parse("[" + text + "]");
        return parsed.Root is null ? null : Detach(parsed.Root);
    }

    /// <summary>Derselbe Knoten ohne Textpositionen (wird beim Schreiben neu aufgebaut).</summary>
    public static ExprNode Detach(ExprNode node) => node switch
    {
        CallNode call => new CallNode(call.Name, call.Args.Select(Detach).ToArray(), null),
        ChainNode chain => new ChainNode(chain.Links.Select(l => new ChainLink(l.Name, l.Args?.Select(Detach).ToArray())).ToArray(), null),
        LiteralNode literal => literal with { Span = null },
        RawNode raw => raw with { Span = null },
        _ => node,
    };

    private static bool IsHull(string text, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            if (text[i] != '"' && !char.IsWhiteSpace(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static List<ExprError> CheckStructure(string text, int start, int end)
    {
        var errors = new List<ExprError>();
        var depth = 0;
        for (var i = start; i < end; i++)
        {
            var c = text[i];
            if (c == '\'')
            {
                var quote = text.IndexOf('\'', i + 1, end - i - 1);
                if (quote < 0)
                {
                    errors.Add(new ExprError(i, ExprErrorKind.UnclosedQuote));
                    return errors;
                }

                i = quote;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                if (--depth < 0)
                {
                    errors.Add(new ExprError(i, ExprErrorKind.Unexpected));
                    return errors;
                }
            }
        }

        if (depth > 0)
        {
            errors.Add(new ExprError(end, ExprErrorKind.MissingClose));
        }

        return errors;
    }

    /// <summary>Knoten für den Bereich [start, end); Leerraum am Rand wird übersprungen.</summary>
    private static ExprNode ParseNode(string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        var span = new TextSpan(start, end);
        if (start < end && text[start] == '\'' && text.IndexOf('\'', start + 1) == end - 1)
        {
            return Literal(text[(start + 1)..(end - 1)], span);
        }

        return ParseChain(text, start, end) ?? new RawNode(text[start..end], span);
    }

    private static LiteralNode Literal(string content, TextSpan span)
    {
        if (content.StartsWith('(') && content.IndexOf(')') is > 1 and var close)
        {
            return new LiteralNode(content[1..close], content[(close + 1)..], span);
        }

        return new LiteralNode(null, content, span);
    }

    /// <summary>Kette oder Aufruf, die genau den Bereich füllt; sonst <c>null</c>.</summary>
    private static ExprNode? ParseChain(string text, int start, int end)
    {
        var links = new List<ChainLink>();
        var i = start;
        while (true)
        {
            if (i >= end || !(char.IsLetter(text[i]) || text[i] == '_'))
            {
                return null;
            }

            var nameStart = i;
            while (i < end && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
            {
                i++;
            }

            var name = text[nameStart..i];
            IReadOnlyList<ExprNode>? args = null;
            if (i < end && text[i] == '(')
            {
                var closing = Matching(text, i, end);
                if (closing < 0)
                {
                    return null;
                }

                args = Arguments(text, i + 1, closing);
                i = closing + 1;
            }

            links.Add(new ChainLink(name, args));
            if (i == end)
            {
                break;
            }

            if (text[i] != '.')
            {
                return null;
            }

            i++;
        }

        var span = new TextSpan(start, end);
        return links.Count == 1 && links[0].Args is { } callArgs
            ? new CallNode(links[0].Name, callArgs, span)
            : new ChainNode(links, span);
    }

    /// <summary>Position der zu <paramref name="open"/> gehörenden schließenden Klammer (Hochkommas beachtet), sonst -1.</summary>
    private static int Matching(string text, int open, int end)
    {
        var depth = 0;
        for (var i = open; i < end; i++)
        {
            switch (text[i])
            {
                case '\'':
                    var quote = text.IndexOf('\'', i + 1);
                    if (quote < 0 || quote >= end)
                    {
                        return -1;
                    }

                    i = quote;
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    if (--depth == 0)
                    {
                        return i;
                    }

                    break;
            }
        }

        return -1;
    }

    private static IReadOnlyList<ExprNode> Arguments(string text, int start, int end)
    {
        var result = new List<ExprNode>();
        if (text[start..end].Trim().Length == 0)
        {
            return result;
        }

        var depth = 0;
        var argStart = start;
        for (var i = start; i < end; i++)
        {
            switch (text[i])
            {
                case '\'':
                    i = text.IndexOf('\'', i + 1);
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(ParseNode(text, argStart, i));
                    argStart = i + 1;
                    break;
            }
        }

        result.Add(ParseNode(text, argStart, end));
        return result;
    }
}
