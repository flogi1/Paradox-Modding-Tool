using System.Text;

namespace ParadoxModTool.GuiDesign.Text;

/// <summary>Ein Baustein eines Jomini-Lokalisierungstexts nach dem Zerlegen.</summary>
public abstract record JominiTextToken;

/// <summary>Gewöhnlicher Text ohne Zeilenumbrüche.</summary>
public sealed record TextToken(string Text) : JominiTextToken;

/// <summary>Beginn eines Textformats (<c>#name</c>); <c>Spec</c> kann <c>;</c>- und <c>:</c>-Teile enthalten.</summary>
public sealed record FormatStart(string Spec) : JominiTextToken;

/// <summary>Ende des innersten offenen Textformats (<c>#!</c>, oder implizit am Textende).</summary>
public sealed record FormatEnd : JominiTextToken;

/// <summary>Texticon (<c>@name!</c>); <c>Name</c> null bei dynamischen Icons (<c>@[…]!</c>).</summary>
public sealed record IconToken(string? Name) : JominiTextToken;

/// <summary>Platzhalter für Wert, Binding oder Konzept; <c>Display</c> wird als Text angezeigt.</summary>
public sealed record PlaceholderToken(string Display, bool Concept) : JominiTextToken;

/// <summary>Harter Zeilenumbruch.</summary>
public sealed record LineBreakToken : JominiTextToken;

/// <summary>
/// Zerlegt Jomini-Lokalisierungstext (<c>#format … #!</c>, <c>$key$</c>, <c>[binding]</c>, <c>@icon!</c>, Zeilenumbrüche) in Bausteine.
/// Kaputte Auszeichnung wirft nie, sondern bleibt Text.
/// </summary>
public static class JominiTextMarkup
{
    /// <param name="text">Der Lokalisierungstext; Zeilenumbrüche sind echte <c>\n</c>.</param>
    /// <param name="localise">Schlüssel nach Text; null = nicht gefunden.</param>
    public static IReadOnlyList<JominiTextToken> Parse(string text, Func<string, string?> localise)
    {
        var state = new State(localise);
        Run(text ?? "", true, state);
        state.FlushText();
        // Ein nicht geschlossenes Format endet am Textende.
        while (state.OpenFormats > 0)
        {
            state.Tokens.Add(new FormatEnd());
            state.OpenFormats--;
        }
        return state.Tokens;
    }

    private sealed class State(Func<string, string?> localise)
    {
        public readonly List<JominiTextToken> Tokens = [];
        public readonly StringBuilder Text = new();
        public readonly Func<string, string?> Localise = localise;
        public int OpenFormats;

        public void FlushText()
        {
            if (Text.Length == 0) return;
            Tokens.Add(new TextToken(Text.ToString()));
            Text.Clear();
        }

        public void Add(JominiTextToken token)
        {
            FlushText();
            Tokens.Add(token);
        }
    }

    // Bei expandDollar=false (Text aus einem $-Schlüssel) bleibt $ wörtlich: nur eine Ersetzungsebene.
    private static void Run(string s, bool expandDollar, State state)
    {
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            switch (c)
            {
                case '\r':
                    i++;
                    break;
                case '\n':
                    state.Add(new LineBreakToken());
                    i++;
                    break;
                case '#':
                    i = Hash(s, i, state);
                    break;
                case '$' when expandDollar:
                    i = Dollar(s, i, state);
                    break;
                case '[':
                    i = Bracket(s, i, state);
                    break;
                case '@':
                    i = At(s, i, state);
                    break;
                default:
                    state.Text.Append(c);
                    i++;
                    break;
            }
        }
    }

    private static int Hash(string s, int i, State state)
    {
        if (i + 1 < s.Length && s[i + 1] == '!')
        {
            // Ein überzähliges #! fällt weg.
            if (state.OpenFormats > 0)
            {
                state.Add(new FormatEnd());
                state.OpenFormats--;
            }
            return i + 2;
        }
        if (i + 1 < s.Length && char.IsLetter(s[i + 1]))
        {
            // Der Formatname reicht bis zum ersten Leerzeichen, das mit verbraucht wird; ein Zeilenumbruch beendet ihn auch.
            var end = i + 1;
            while (end < s.Length && s[end] != ' ' && s[end] != '\n' && s[end] != '\r') end++;
            state.Add(new FormatStart(s.Substring(i + 1, end - i - 1)));
            state.OpenFormats++;
            return end < s.Length && s[end] == ' ' ? end + 1 : end;
        }
        state.Text.Append('#');
        return i + 1;
    }

    private static int Dollar(string s, int i, State state)
    {
        var close = s.IndexOf('$', i + 1);
        // Schlüssel enthalten weder Leerraum noch sind sie leer; sonst ist das $ nur ein Zeichen ("kostet $5 und $6").
        if (close > i + 1 && !HasWhitespace(s, i + 1, close))
        {
            var inner = s.Substring(i + 1, close - i - 1);
            var bar = inner.IndexOf('|');
            var key = bar >= 0 ? inner[..bar] : inner;
            if (key.Length > 0)
            {
                var value = state.Localise(key);
                if (value != null)
                    Run(value, false, state);
                else
                    state.Add(new PlaceholderToken(key, false));
                return close + 1;
            }
        }
        state.Text.Append('$');
        return i + 1;
    }

    private static int Bracket(string s, int i, State state)
    {
        var close = FindClosingBracket(s, i);
        if (close > i + 1)
        {
            var expr = s.Substring(i + 1, close - i - 1).Trim();
            if (expr.Length > 0)
            {
                state.Add(BindingToken(expr, state.Localise));
                return close + 1;
            }
        }
        state.Text.Append('[');
        return i + 1;
    }

    private static int At(string s, int i, State state)
    {
        var p = i + 1;
        if (p < s.Length && s[p] == '[')
        {
            var close = FindClosingBracket(s, p);
            if (close > p + 1 && close + 1 < s.Length && s[close + 1] == '!')
            {
                state.Add(new IconToken(null));
                return close + 2;
            }
        }
        else
        {
            var end = p;
            while (end < s.Length && (char.IsLetterOrDigit(s[end]) || s[end] == '_' || s[end] == '-')) end++;
            if (end > p && end < s.Length && s[end] == '!')
            {
                state.Add(new IconToken(s.Substring(p, end - p)));
                return end + 1;
            }
        }
        state.Text.Append('@');
        return i + 1;
    }

    /// <summary>Passende <c>]</c> zu der <c>[</c> an <paramref name="open"/>; -1 ohne Treffer oder wenn vorher ein Zeilenumbruch kommt.</summary>
    private static int FindClosingBracket(string s, int open)
    {
        var depth = 0;
        for (var i = open; i < s.Length; i++)
        {
            switch (s[i])
            {
                case '\n':
                    return -1;
                case '[':
                    depth++;
                    break;
                case ']':
                    depth--;
                    if (depth == 0) return i;
                    break;
            }
        }
        return -1;
    }

    private static bool HasWhitespace(string s, int from, int to)
    {
        for (var i = from; i < to; i++)
            if (char.IsWhiteSpace(s[i])) return true;
        return false;
    }

    private static PlaceholderToken BindingToken(string expr, Func<string, string?> localise)
    {
        // Konzept CK III / EU V: [name|E] oder [name|e] mit schlichtem Namen.
        var bar = expr.IndexOf('|');
        if (bar > 0)
        {
            var flag = expr[(bar + 1)..].Trim();
            var name = expr[..bar].Trim();
            if ((flag == "E" || flag == "e") && IsIdentifier(name))
                return new PlaceholderToken(localise("game_concept_" + name) ?? name, true);
        }
        // Konzept Victoria 3: [concept_name], der Schlüssel ist der Bezeichner selbst.
        const string conceptPrefix = "concept_";
        if (expr.Length > conceptPrefix.Length && expr.StartsWith(conceptPrefix, StringComparison.Ordinal) && IsIdentifier(expr))
            return new PlaceholderToken(localise(expr) ?? expr[conceptPrefix.Length..], true);

        return new PlaceholderToken(LastNamePart(expr), false);
    }

    private static bool IsIdentifier(string s)
    {
        if (s.Length == 0) return false;
        foreach (var c in s)
            if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
        return true;
    }

    /// <summary>Letzter Namensteil eines Bindings: <c>|…</c> bei Tiefe 0 abschneiden, an <c>.</c> bei Tiefe 0 teilen, <c>(…)</c> des letzten Teils entfernen.</summary>
    private static string LastNamePart(string expr)
    {
        var depth = 0;
        var cut = expr.Length;
        for (var i = 0; i < expr.Length; i++)
        {
            var c = expr[i];
            if (c is '(' or '[') depth++;
            else if (c is ')' or ']') { if (depth > 0) depth--; }
            else if (c == '|' && depth == 0) { cut = i; break; }
        }
        var head = expr[..cut];

        depth = 0;
        var start = 0;
        for (var i = 0; i < head.Length; i++)
        {
            var c = head[i];
            if (c is '(' or '[') depth++;
            else if (c is ')' or ']') { if (depth > 0) depth--; }
            else if (c == '.' && depth == 0) start = i + 1;
        }
        var part = head[start..].Trim();
        var paren = part.IndexOf('(');
        if (paren >= 0) part = part[..paren].TrimEnd();
        return part.Length > 0 ? part : expr;
    }
}
