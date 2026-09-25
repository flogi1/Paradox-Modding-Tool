using System.Text;

namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Wird geworfen, wenn eine Paradox-Skriptdatei nicht tokenisiert/geparst werden kann
/// (z. B. eine nicht geschlossene Klammer oder ein nicht terminierter String). Enthält
/// die betroffene Zeilennummer, damit die Fehlermeldung in der UI direkt zur Stelle
/// im Editor springen kann.
/// </summary>
public sealed class ScriptParseException : Exception
{
    public int Line { get; }

    public ScriptParseException(string message, int line) : base($"{message} (Zeile {line})")
    {
        Line = line;
    }
}

/// <summary>
/// Zerlegt eine Paradox-Skriptdatei (.txt/.gui/.gfx im "Jomini"-Skriptdialekt, siehe
/// Architektur-Spezifikation Abschnitt 7) in eine flache Token-Liste. Kennt bewusst
/// keine Grammatik — das übernimmt <see cref="ScriptParser"/>. Whitespace wird
/// verworfen, Kommentare werden als eigene Tokens weitergereicht (nicht ignoriert),
/// damit sie beim Parsen den benachbarten Anweisungen zugeordnet werden können.
/// </summary>
public static class ScriptTokenizer
{
    private const char Eof = '\0';

    public static List<ScriptToken> Tokenize(string source)
    {
        var tokens = new List<ScriptToken>();
        var pos = 0;
        var line = 1;
        var length = source.Length;

        // UTF-8-BOM, das beim Einlesen als Zeichen ﻿ übrig bleiben kann, überspringen.
        if (length > 0 && source[0] == '﻿')
        {
            pos = 1;
        }

        char Current() => pos < length ? source[pos] : Eof;
        char Peek(int offset) => pos + offset < length ? source[pos + offset] : Eof;

        while (pos < length)
        {
            var c = Current();

            if (c == '\n')
            {
                line++;
                pos++;
                continue;
            }

            if (c is ' ' or '\t' or '\r')
            {
                pos++;
                continue;
            }

            if (c == '#')
            {
                var startLine = line;
                var start = pos + 1;
                while (pos < length && source[pos] != '\n')
                {
                    pos++;
                }

                var text = source.Substring(start, pos - start).Trim();
                tokens.Add(new ScriptToken { Kind = ScriptTokenKind.Comment, Text = text, Line = startLine, Start = start - 1, End = pos });
                continue;
            }

            if (c == '"')
            {
                var startLine = line;
                var stringStart = pos;
                var sb = new StringBuilder();
                pos++; // öffnendes Anführungszeichen überspringen

                while (true)
                {
                    if (pos >= length)
                    {
                        throw new ScriptParseException("Nicht geschlossener String-Literal", startLine);
                    }

                    var ch = source[pos];

                    if (ch == '\\' && pos + 1 < length && source[pos + 1] == '"')
                    {
                        sb.Append('"');
                        pos += 2;
                        continue;
                    }

                    if (ch == '"')
                    {
                        pos++;
                        break;
                    }

                    if (ch == '\n')
                    {
                        line++;
                    }

                    sb.Append(ch);
                    pos++;
                }

                tokens.Add(new ScriptToken { Kind = ScriptTokenKind.String, Text = sb.ToString(), Line = startLine, Start = stringStart, End = pos });
                continue;
            }

            if (c == '{')
            {
                tokens.Add(new ScriptToken { Kind = ScriptTokenKind.OpenBrace, Text = "{", Line = line, Start = pos, End = pos + 1 });
                pos++;
                continue;
            }

            if (c == '}')
            {
                tokens.Add(new ScriptToken { Kind = ScriptTokenKind.CloseBrace, Text = "}", Line = line, Start = pos, End = pos + 1 });
                pos++;
                continue;
            }

            if (c == '=')
            {
                tokens.Add(new ScriptToken { Kind = ScriptTokenKind.Operator, Text = "=", Line = line, Start = pos, End = pos + 1 });
                pos++;
                continue;
            }

            if (c == '<')
            {
                var opStart = pos;
                if (Peek(1) == '=')
                {
                    tokens.Add(new ScriptToken { Kind = ScriptTokenKind.Operator, Text = "<=", Line = line, Start = opStart, End = opStart + 2 });
                    pos += 2;
                }
                else
                {
                    tokens.Add(new ScriptToken { Kind = ScriptTokenKind.Operator, Text = "<", Line = line, Start = opStart, End = opStart + 1 });
                    pos++;
                }

                continue;
            }

            if (c == '>')
            {
                var opStart = pos;
                if (Peek(1) == '=')
                {
                    tokens.Add(new ScriptToken { Kind = ScriptTokenKind.Operator, Text = ">=", Line = line, Start = opStart, End = opStart + 2 });
                    pos += 2;
                }
                else
                {
                    tokens.Add(new ScriptToken { Kind = ScriptTokenKind.Operator, Text = ">", Line = line, Start = opStart, End = opStart + 1 });
                    pos++;
                }

                continue;
            }

            // Alles andere: ein unquotiertes Wort (Bezeichner, Zahl, Datum, yes/no, ...)
            // bis zum nächsten Trennzeichen einlesen.
            var wordStart = pos;
            var wordLine = line;
            while (pos < length && !IsDelimiter(source[pos]))
            {
                pos++;
            }

            if (pos == wordStart)
            {
                // Sollte durch die obigen Zweige nie erreicht werden, ist aber eine sichere
                // Absicherung gegen eine Endlosschleife bei einem unerwarteten Zeichen.
                throw new ScriptParseException($"Unerwartetes Zeichen '{c}'", wordLine);
            }

            var word = source.Substring(wordStart, pos - wordStart);
            tokens.Add(new ScriptToken { Kind = ScriptTokenKind.Identifier, Text = word, Line = wordLine, Start = wordStart, End = pos });
        }

        tokens.Add(new ScriptToken { Kind = ScriptTokenKind.Eof, Text = string.Empty, Line = line, Start = length, End = length });
        return tokens;
    }

    private static bool IsDelimiter(char c) =>
        c is ' ' or '\t' or '\r' or '\n' or '{' or '}' or '=' or '<' or '>' or '#' or '"';
}
