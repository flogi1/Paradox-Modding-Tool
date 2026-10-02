using System.Text;

namespace ParadoxModTool.Modifiers;

internal enum TokenKind
{
    Word,
    Equals,
    Open,
    Close,
}

internal sealed record Token(TokenKind Kind, string Text);

/// <summary>Zerlegt Paradox-Skript in Wörter, <c>=</c> und Klammern; Kommentare und BOM fallen weg. Für Dateien, deren Struktur nur grob gelesen wird.</summary>
internal static class ScriptLexer
{
    public static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c) || c == '﻿')
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
            else if (c == '{' || c == '}' || c == '=')
            {
                tokens.Add(new Token(c == '{' ? TokenKind.Open : c == '}' ? TokenKind.Close : TokenKind.Equals, c.ToString()));
                i++;
            }
            else if (c == '"')
            {
                var value = new StringBuilder();
                i++;
                while (i < text.Length && text[i] != '"' && text[i] != '\n')
                {
                    value.Append(text[i]);
                    i++;
                }

                i++;
                tokens.Add(new Token(TokenKind.Word, value.ToString()));
            }
            else if (c == '@' && i + 1 < text.Length && text[i + 1] == '[')
            {
                // Inline-Formel @[ a + b ]: ein Wort samt Leerzeichen, bis zur passenden "]" (oder zum Zeilenende).
                var start = i;
                var depth = 0;
                while (i < text.Length && text[i] != '\n')
                {
                    if (text[i] == '[')
                    {
                        depth++;
                    }
                    else if (text[i] == ']' && --depth == 0)
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                tokens.Add(new Token(TokenKind.Word, text[start..i].TrimEnd('\r')));
            }
            else
            {
                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('{' or '}' or '=' or '#' or '"'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Word, text[start..i]));
            }
        }

        return tokens;
    }
}
