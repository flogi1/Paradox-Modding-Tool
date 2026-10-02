namespace ParadoxModTool.GuiDesign.DataTypes;

/// <summary>Eine Punkt-Kette in einem GUI-Ausdruck (<c>Country.GetCapital.GetName</c>); <see cref="IsCall"/>: direkt danach folgt <c>(</c>.</summary>
public sealed record DataExpressionChain(IReadOnlyList<string> Parts, bool IsCall);

/// <summary>
/// Liest die Bezeichner-Ketten aus GUI-Datenausdrücken wie <c>[Not(Country.IsPlayer)]</c> oder <c>[LateralView.Vars.Exists( 'x' )]</c>: Punkt-Ketten
/// auch innerhalb von Funktionsargumenten, Texte in Anführungszeichen und Zahlen übersprungen.
/// </summary>
public static class DataExpression
{
    /// <summary>Innerer Text ohne äußere eckige Klammern und Leerraum.</summary>
    public static string Strip(string expression)
    {
        var text = expression.Trim();
        if (text.StartsWith('[') && text.EndsWith(']'))
        {
            text = text[1..^1];
        }

        return text.Trim();
    }

    public static IReadOnlyList<DataExpressionChain> Chains(string expression)
    {
        var result = new List<DataExpressionChain>();
        var text = expression;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c is '\'' or '"')
            {
                var end = text.IndexOf(c, i + 1);
                i = end < 0 ? text.Length : end + 1;
                continue;
            }

            if (char.IsDigit(c))
            {
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '.' or '_'))
                {
                    i++;
                }

                continue;
            }

            if (!IsIdentifierStart(c))
            {
                i++;
                continue;
            }

            var parts = new List<string>();
            while (true)
            {
                var start = i;
                while (i < text.Length && IsIdentifierPart(text[i]))
                {
                    i++;
                }

                parts.Add(text[start..i]);
                var next = SkipSpaces(text, i);
                if (next < text.Length && text[next] == '.' && next + 1 < text.Length && IsIdentifierStart(text[SkipSpaces(text, next + 1)]))
                {
                    i = SkipSpaces(text, next + 1);
                    continue;
                }

                var isCall = next < text.Length && text[next] == '(';
                result.Add(new DataExpressionChain(parts, isCall));
                i = next;
                break;
            }
        }

        return result;
    }

    private static int SkipSpaces(string text, int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
