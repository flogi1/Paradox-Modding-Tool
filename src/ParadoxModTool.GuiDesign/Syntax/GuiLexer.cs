using System.Text;

namespace ParadoxModTool.GuiDesign.Syntax;

/// <summary>
/// Fehlertoleranter Lexer für .gui-Dateien. Anders als <c>ScriptTokenizer</c> wirft er nie: Eine Zeichenkette ohne schließendes
/// Anführungszeichen endet am Zeilenende (mit Diagnose). Mehrzeilige Zeichenketten sind erlaubt, Vanilla nutzt sie in Datenbindungen.
/// Wörter mit offener eckiger oder runder Klammer laufen über Leerzeichen hinweg bis zur schließenden Klammer (höchstens bis zum
/// Zeilenende), damit <c>@[a - b * 2]</c> und <c>[Func( x )]</c> ein Token bleiben.
/// </summary>
public static class GuiLexer
{
    public static List<GuiToken> Tokenize(string text, List<GuiDiagnostic>? diagnostics = null)
    {
        var tokens = new List<GuiToken>();
        var n = text.Length;
        var i = n > 0 && text[0] == '\uFEFF' ? 1 : 0;
        var line = 1;

        while (i < n)
        {
            var c = text[i];
            if (c == '\n')
            {
                line++;
                i++;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '#')
            {
                var start = i;
                while (i < n && text[i] != '\n')
                {
                    i++;
                }

                var end = i > start && text[i - 1] == '\r' ? i - 1 : i;
                tokens.Add(new GuiToken(GuiTokenKind.Comment, text[start..end], start, end, line));
                continue;
            }

            if (c == '"')
            {
                i = ReadString(text, i, ref line, tokens, diagnostics);
                continue;
            }

            if (c == '{')
            {
                tokens.Add(new GuiToken(GuiTokenKind.Open, "{", i, i + 1, line));
                i++;
                continue;
            }

            if (c == '}')
            {
                tokens.Add(new GuiToken(GuiTokenKind.Close, "}", i, i + 1, line));
                i++;
                continue;
            }

            var operatorLength = OperatorLength(text, i);
            if (operatorLength > 0)
            {
                tokens.Add(new GuiToken(GuiTokenKind.Operator, text.Substring(i, operatorLength), i, i + operatorLength, line));
                i += operatorLength;
                continue;
            }

            var wordStart = i;
            var depth = 0;
            while (i < n)
            {
                var ch = text[i];
                if (ch == '\n')
                {
                    break;
                }

                if (ch is '[' or '(')
                {
                    depth++;
                }
                else if (ch is ']' or ')')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }
                else if (depth == 0 && (IsDelimiter(ch) || OperatorLength(text, i) > 0))
                {
                    break;
                }

                i++;
            }

            if (i == wordStart)
            {
                i++;
            }

            var wordEnd = i;
            while (wordEnd > wordStart + 1 && char.IsWhiteSpace(text[wordEnd - 1]))
            {
                wordEnd--;
            }

            tokens.Add(new GuiToken(GuiTokenKind.Word, text[wordStart..wordEnd], wordStart, wordEnd, line));
        }

        tokens.Add(new GuiToken(GuiTokenKind.Eof, string.Empty, n, n, line));
        return tokens;
    }

    private static int ReadString(string text, int start, ref int line, List<GuiToken> tokens, List<GuiDiagnostic>? diagnostics)
    {
        var n = text.Length;
        var content = new StringBuilder();
        var j = start + 1;
        var newlines = 0;
        while (j < n && text[j] != '"')
        {
            if (text[j] == '\\' && j + 1 < n && text[j + 1] == '"')
            {
                content.Append('"');
                j += 2;
                continue;
            }

            if (text[j] == '\n')
            {
                newlines++;
            }

            content.Append(text[j]);
            j++;
        }

        if (j < n)
        {
            tokens.Add(new GuiToken(GuiTokenKind.String, content.ToString(), start, j + 1, line));
            line += newlines;
            return j + 1;
        }

        var lineEnd = text.IndexOf('\n', start);
        if (lineEnd < 0)
        {
            lineEnd = n;
        }

        var end = lineEnd > start + 1 && text[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd;
        diagnostics?.Add(new GuiDiagnostic(GuiDiagnosticKind.UnterminatedString, start, end, line, "Zeichenkette ohne schließendes Anführungszeichen"));
        tokens.Add(new GuiToken(GuiTokenKind.String, text[(start + 1)..end], start, end, line));
        return lineEnd;
    }

    private static int OperatorLength(string text, int i)
    {
        var c = text[i];
        var next = i + 1 < text.Length ? text[i + 1] : '\0';
        if (c is '<' or '>')
        {
            return next == '=' ? 2 : 1;
        }

        if (c is '!' or '?')
        {
            return next == '=' ? 2 : 0;
        }

        if (c == '=')
        {
            return next == '=' ? 2 : 1;
        }

        return 0;
    }

    private static bool IsDelimiter(char c) => char.IsWhiteSpace(c) || c is '{' or '}' or '#' or '"';
}
