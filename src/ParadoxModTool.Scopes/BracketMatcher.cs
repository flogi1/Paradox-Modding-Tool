namespace ParadoxModTool.Scopes;

/// <summary>Eine Klammer und ihr Gegenstück, jeweils als Zeichenposition im Text.</summary>
/// <param name="Anchor">Die Klammer am Cursor.</param>
/// <param name="Partner">Die dazugehörige öffnende bzw. schließende Klammer.</param>
public readonly record struct BracketMatch(int Anchor, int Partner);

/// <summary>
/// Findet zur Klammer am Cursor die passende Gegenklammer. Unterstützt <c>{ }</c>, <c>( )</c> und <c>[ ]</c>; jeder Typ wird für sich
/// verschachtelt gezählt. Klammern in Kommentaren (<c>#</c> bis Zeilenende) und in Zeichenketten (<c>"…"</c>, endet spätestens am Zeilenende)
/// zählen nicht mit. Lokalisierungsdateien (<c>.yml</c>) kennen keine Zeichenketten in diesem Sinn - dort stehen Klammern wie <c>[Concept]</c>
/// gerade in den Anführungszeichen -, und ein <c>#</c> beginnt nur am Zeilenanfang einen Kommentar.
/// </summary>
public static class BracketMatcher
{
    private const string Openers = "{([";
    private const string Closers = "})]";

    /// <summary>
    /// Sucht die Klammer direkt vor dem Cursor, danach die direkt dahinter, und liefert die erste, die ein Gegenstück hat.
    /// Ohne Klammer am Cursor oder ohne Gegenstück (unbalanciert, in Kommentar/Zeichenkette) ist das Ergebnis <see langword="null"/>.
    /// </summary>
    public static BracketMatch? Find(string text, int caret, bool localization)
    {
        foreach (var position in new[] { caret - 1, caret })
        {
            if (position < 0 || position >= text.Length || TypeOf(text[position]) < 0)
            {
                continue;
            }

            if (FindPartner(text, position, localization) is { } partner)
            {
                return new BracketMatch(position, partner);
            }
        }

        return null;
    }

    /// <summary>Die schnelle Vorprüfung: steht überhaupt eine Klammer direkt vor oder hinter dem Cursor?</summary>
    public static bool IsBracketNextTo(string text, int caret)
        => (caret - 1 >= 0 && caret - 1 < text.Length && TypeOf(text[caret - 1]) >= 0)
           || (caret >= 0 && caret < text.Length && TypeOf(text[caret]) >= 0);

    private static int TypeOf(char c)
    {
        var index = Openers.IndexOf(c);
        return index >= 0 ? index : Closers.IndexOf(c);
    }

    private static int? FindPartner(string text, int target, bool localization)
    {
        var stacks = new Stack<int>[] { new(), new(), new() };
        var lineIsBlankSoFar = true;
        var inString = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\n')
            {
                inString = false;
                lineIsBlankSoFar = true;
                continue;
            }

            if (inString)
            {
                inString = c != '"';
                continue;
            }

            if (c == '#' && (!localization || lineIsBlankSoFar))
            {
                var lineEnd = text.IndexOf('\n', i);
                if (lineEnd < 0)
                {
                    return null;
                }

                i = lineEnd - 1;
                continue;
            }

            if (!char.IsWhiteSpace(c))
            {
                lineIsBlankSoFar = false;
            }

            if (c == '"' && !localization)
            {
                inString = true;
                continue;
            }

            var type = TypeOf(c);
            if (type < 0)
            {
                continue;
            }

            if (Openers.Contains(c))
            {
                stacks[type].Push(i);
                continue;
            }

            if (stacks[type].Count == 0)
            {
                if (i == target)
                {
                    return null;
                }

                continue;
            }

            var opener = stacks[type].Pop();
            if (opener == target)
            {
                return i;
            }

            if (i == target)
            {
                return opener;
            }
        }

        return null;
    }
}
