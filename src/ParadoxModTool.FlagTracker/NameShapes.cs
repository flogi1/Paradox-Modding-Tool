using System.Globalization;
using System.Text.RegularExpressions;

namespace ParadoxModTool.FlagTracker;

/// <summary>Reine Hilfen für Namensformen der Clausewitz-Spiele (HOI IV, EU IV, Stellaris): <c>@</c>-Anhang, HOI-IV-Präfixe, Werte ohne Namen.</summary>
public static class NameShapes
{
    /// <summary>Bereichswort am Anfang eines <c>@</c>-Anhangs, allein oder vor einem <c>.</c> (Ketten wie <c>root.owner</c>, <c>prev.prev</c>).</summary>
    private static readonly Regex ScopeWordTail = new(
        @"^(root|(from)+|(prev)+|this|owner)(\.|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Tag = new(@"^[A-Z0-9]{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] ScopeWords = { "root", "from", "prev", "this", "owner", "yes", "no" };

    private static readonly string[] LiteralPrefixes = { "value:", "trigger:", "modifier:", "token:", "constant:" };

    /// <summary>
    /// <c>X_@ROOT</c>, <c>x@root.owner</c>, <c>x@event_target:y</c>, <c>x@var:y</c>, <c>x@$P$</c>, <c>x@prev.prev</c>: Anhang mit Bereichswort
    /// (ROOT, FROM, PREV, THIS, OWNER und Ketten), Event-Target, Variable oder Parameter wird zu <c>*</c> und die Stelle ein Muster ("X_*", true).
    /// Ein Anhang ohne Bereichswort (<c>num_equipment@infantry_equipment</c>) und ein Name, der mit <c>@</c> beginnt, bleiben unverändert (name, false).
    /// </summary>
    public static (string Name, bool IsPattern) AtSuffix(string name)
    {
        var at = name.IndexOf('@');
        if (at <= 0)
        {
            return (name, false);
        }

        var tail = name[(at + 1)..];
        var runtime = ScopeWordTail.IsMatch(tail)
            || tail.StartsWith("event_target:", StringComparison.OrdinalIgnoreCase)
            || tail.StartsWith("var:", StringComparison.OrdinalIgnoreCase)
            || tail.StartsWith('$');
        return runtime ? (name[..at] + "*", true) : (name, false);
    }

    /// <summary>
    /// Name einer HOI-IV-Variable oder eines Arrays: zuerst hinten ab <c>^</c>, <c>?</c> oder <c>|</c> abschneiden, dann vorne
    /// <c>var:</c>, <c>event_target:</c>, <c>global.</c>, <c>GLOBAL.</c>, <c>ROOT.</c>, <c>FROM.</c>, <c>PREV.</c>, <c>THIS.</c>, Ländertags,
    /// Zahlen und Ketten davon: es zählt das letzte Glied nach <c>.</c> oder <c>:</c>. Ein <c>@</c>-Anhang bleibt dabei unberührt
    /// (<c>ROOT.party_popularity@ruling_party</c> → <c>party_popularity@ruling_party</c>). Nie auf Flag-Namen anwenden: dort gehören Punkte zum Namen.
    /// </summary>
    public static string Hoi4VariableName(string raw)
    {
        var name = raw;
        var cut = name.IndexOfAny(new[] { '^', '?', '|' });
        if (cut >= 0)
        {
            name = name[..cut];
        }

        var at = name.IndexOf('@');
        var head = at > 0 ? name[..at] : name;
        var tail = at > 0 ? name[at..] : string.Empty;
        var last = head.LastIndexOfAny(new[] { '.', ':' });
        if (last >= 0 && last < head.Length - 1)
        {
            head = head[(last + 1)..];
        }

        return head + tail;
    }

    /// <summary>
    /// True für Werte, die kein Name sind: leer, Zahlen, <c>@</c>-Konstanten, <c>value:</c>/<c>trigger:</c>/<c>modifier:</c>/<c>token:</c>/<c>constant:</c>,
    /// Bereichswörter (ROOT, FROM, PREV, THIS, OWNER in jeder Schreibweise), <c>yes</c>/<c>no</c> und Ländertags (3 Zeichen, Großbuchstaben oder Ziffern).
    /// </summary>
    public static bool IsNonNameValue(string token)
    {
        if (token.Length == 0 || token[0] == '@')
        {
            return true;
        }

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return true;
        }

        foreach (var prefix in LiteralPrefixes)
        {
            if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var word in ScopeWords)
        {
            if (string.Equals(token, word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return Tag.IsMatch(token);
    }
}
