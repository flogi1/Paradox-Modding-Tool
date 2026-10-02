using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.SearchReplace.ReplaceLists;

/// <summary>
/// Treffer außerhalb von Kommentaren, je nach <see cref="RuleMode"/>:
/// <list type="bullet">
/// <item><see cref="RuleMode.Word"/>: ganze Wörter; Wortzeichen sind Buchstaben, Ziffern und "_", vor und nach einem Treffer steht keins.</item>
/// <item><see cref="RuleMode.Text"/>: der Suchtext überall (ohne Wortgrenzen); Leerraum im Suchtext passt auf jeden Leerraum, auch auf Zeilenumbrüche.</item>
/// <item><see cref="RuleMode.Pattern"/>: wie Text, dazu ist <c>*</c> genau ein Wort oder Wert (<see cref="ValueClass"/>), wiederzugeben mit <c>$1</c>, <c>$2</c> …</item>
/// </list>
/// Kommentar ist der Text ab dem ersten "#" außerhalb von Anführungszeichen bis zum Zeilenende; der Anführungszeichen-Zustand endet an
/// jedem Zeilenumbruch. Ein Treffer, der in einem Kommentar beginnt, zählt nicht. So trifft "castle_01" nicht in "#castle_01", und ein zweiter Lauf ändert nichts.
/// </summary>
public static class RuleMatcher
{
    /// <summary>Zeichen eines Werts, für den <c>*</c> steht: Wortzeichen, dazu <c>. : - @</c> (Zahlen wie 0.1 und -5, Namen wie scope:x oder @konstante).</summary>
    private const string ValueClass = "[A-Za-z0-9_.:@\\-]+";

    private static readonly Regex SearchWord = new(@"^[A-Za-z0-9_](?:[A-Za-z0-9_.:\-]*[A-Za-z0-9_])?$", RegexOptions.CultureInvariant);

    private static readonly Regex Capture = new(@"\$(\$|\d+)", RegexOptions.CultureInvariant);

    private static readonly ConcurrentDictionary<(RuleMode, string), Regex> RegexCache = new();

    public static bool IsValidSearchWord(string search) => SearchWord.IsMatch(search);

    /// <summary>Anzahl der <c>*</c> im Suchtext einer Platzhalter-Regel (so viele Werte kann "Ersetzen durch" mit <c>$n</c> wiedergeben).</summary>
    public static int WildcardCount(string search) => search.Count(c => c == '*');

    /// <summary>Ein Eintrag je Regel. Abgeschaltete Regeln sind immer <see cref="RuleProblem.None"/> und zählen nicht als Doppel.</summary>
    public static IReadOnlyList<RuleProblem> Validate(IReadOnlyList<ReplaceRule> rules)
    {
        var enabledCounts = rules
            .Where(r => r.Enabled && Basic(r) == RuleProblem.None)
            .GroupBy(r => (r.Mode, Key(r)))
            .ToDictionary(g => g.Key, g => g.Count());

        return rules.Select(rule =>
        {
            if (!rule.Enabled)
            {
                return RuleProblem.None;
            }

            var basic = Basic(rule);
            if (basic != RuleProblem.None)
            {
                return basic;
            }

            return enabledCounts[(rule.Mode, Key(rule))] > 1 ? RuleProblem.Duplicate : RuleProblem.None;
        }).ToList();
    }

    /// <summary>Der Suchtext, wie er gesucht wird: bei Text und Platzhalter ohne Leerraum an den Enden.</summary>
    private static string Key(ReplaceRule rule) => rule.Mode == RuleMode.Word ? rule.Search : rule.Search.Trim();

    private static RuleProblem Basic(ReplaceRule rule)
    {
        var search = Key(rule);
        if (search.Length == 0)
        {
            return RuleProblem.EmptySearch;
        }

        switch (rule.Mode)
        {
            case RuleMode.Word:
                return IsValidSearchWord(search) ? RuleProblem.None : RuleProblem.NotAWord;
            case RuleMode.Text:
                return search.Contains('\n') || search.Contains('\r') ? RuleProblem.Multiline : RuleProblem.None;
            default:
                if (search.Contains('\n') || search.Contains('\r'))
                {
                    return RuleProblem.Multiline;
                }

                var wildcards = WildcardCount(search);
                var badReference = Capture.Matches(rule.Replace).Any(m =>
                    m.Groups[1].Value != "$" && (!int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) || n < 1 || n > wildcards));
                return badReference ? RuleProblem.BadCapture : RuleProblem.None;
        }
    }

    /// <summary>Ganze-Wort-Ersetzung ohne Optionen (die Art Wort).</summary>
    public static string Replace(string text, string word, string replacement, out int count) =>
        Replace(text, new ReplaceRule { Search = word, Replace = replacement }, out count);

    /// <summary>Wendet die Regel auf den Text an (ohne zu prüfen, ob sie gültig ist); <paramref name="count"/> sind die Ersetzungen.</summary>
    public static string Replace(string text, ReplaceRule rule, out int count)
    {
        count = 0;
        var search = Key(rule);
        if (search.Length == 0)
        {
            return text;
        }

        var matches = rule.Mode == RuleMode.Word ? FindWords(text, search, rule.Replace) : FindPattern(text, rule, search);
        if (matches.Count == 0)
        {
            return text;
        }

        count = matches.Count;
        return Apply(text, matches, rule.SafeComment);
    }

    private readonly record struct Hit(int Index, int Length, string Replacement);

    private static List<Hit> FindWords(string text, string word, string replacement)
    {
        var comment = CommentMask(text);
        var matches = new List<Hit>();
        var i = 0;
        while (i < text.Length)
        {
            if (!comment[i] && IsMatchAt(text, i, word))
            {
                matches.Add(new Hit(i, word.Length, replacement));
                i += word.Length;
                continue;
            }

            i++;
        }

        return matches;
    }

    /// <summary>Text und Platzhalter: Regex aus dem Suchtext (Leerraum → beliebiger Leerraum, * → ein Wert, alles andere wörtlich), Treffer in Kommentaren übersprungen.</summary>
    private static List<Hit> FindPattern(string text, ReplaceRule rule, string search)
    {
        var regex = RegexCache.GetOrAdd((rule.Mode, search), key => BuildRegex(key.Item1, key.Item2));
        var comment = CommentMask(text);
        var matches = new List<Hit>();
        var position = 0;
        while (position <= text.Length)
        {
            var match = regex.Match(text, position);
            if (!match.Success)
            {
                break;
            }

            if (comment[match.Index] || match.Length == 0)
            {
                position = match.Index + 1;
                continue;
            }

            matches.Add(new Hit(match.Index, match.Length, rule.Mode == RuleMode.Pattern ? Expand(rule.Replace, match) : rule.Replace));
            position = match.Index + match.Length;
        }

        return matches;
    }

    private static Regex BuildRegex(RuleMode mode, string search)
    {
        var pattern = new StringBuilder();
        for (var i = 0; i < search.Length; i++)
        {
            var c = search[i];
            if (char.IsWhiteSpace(c))
            {
                while (i + 1 < search.Length && char.IsWhiteSpace(search[i + 1]))
                {
                    i++;
                }

                pattern.Append("\\s+");
            }
            else if (c == '*' && mode == RuleMode.Pattern)
            {
                pattern.Append('(').Append(ValueClass).Append(')');
            }
            else
            {
                pattern.Append(Regex.Escape(c.ToString()));
            }
        }

        return new Regex(pattern.ToString(), RegexOptions.CultureInvariant);
    }

    /// <summary>Setzt <c>$1</c>, <c>$2</c> … aus den Platzhaltern ein; <c>$$</c> ist ein <c>$</c>.</summary>
    private static string Expand(string replacement, System.Text.RegularExpressions.Match match) =>
        Capture.Replace(replacement, m =>
            m.Groups[1].Value == "$"
                ? "$"
                : int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 1 && n < match.Groups.Count
                    ? match.Groups[n].Value
                    : string.Empty);

    /// <summary>Für jedes Zeichen: liegt es in einem Kommentar (ab "#" außerhalb von Anführungszeichen bis vor den Zeilenumbruch)?</summary>
    private static bool[] CommentMask(string text)
    {
        var mask = new bool[text.Length];
        var inQuote = false;
        var inComment = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\n')
            {
                inQuote = false;
                inComment = false;
            }
            else if (!inComment && c == '"')
            {
                inQuote = !inQuote;
            }
            else if (!inComment && !inQuote && c == '#')
            {
                inComment = true;
            }

            mask[i] = inComment;
        }

        return mask;
    }

    private static string Apply(string text, List<Hit> matches, bool safeComment)
    {
        var eol = text.Contains("\r\n") ? "\r\n" : "\n";
        var result = new StringBuilder(text.Length + matches.Count * 8);
        var position = 0;
        foreach (var match in matches)
        {
            result.Append(text, position, match.Index - position);
            position = match.Index + match.Length;
            if (safeComment && match.Replacement.StartsWith('#') && CodeFollows(text, match, out var indent))
            {
                SplitLine(result, match.Replacement, indent, eol);
                while (position < text.Length && text[position] is ' ' or '\t')
                {
                    position++;
                }

                continue;
            }

            result.Append(match.Replacement);
        }

        result.Append(text, position, text.Length - position);
        return result.ToString();
    }

    /// <summary>Liegt der Treffer auf einer Zeile und folgt danach noch Code (kein Kommentar) auf derselben Zeile? <paramref name="indent"/> ist die Einrückung der Zeile.</summary>
    private static bool CodeFollows(string text, Hit match, out string indent)
    {
        indent = string.Empty;
        var end = match.Index + match.Length;
        if (text.AsSpan(match.Index, match.Length).Contains('\n'))
        {
            return false;
        }

        var lineStart = match.Index == 0 ? 0 : text.LastIndexOf('\n', match.Index - 1) + 1;
        var lineEnd = text.IndexOf('\n', end);
        lineEnd = lineEnd < 0 ? text.Length : lineEnd;
        if (lineEnd > end && text[lineEnd - 1] == '\r')
        {
            lineEnd--;
        }

        var rest = text.AsSpan(end, Math.Max(0, lineEnd - end)).Trim();
        if (rest.Length == 0 || rest[0] == '#')
        {
            return false;
        }

        var indentLength = 0;
        while (lineStart + indentLength < text.Length && text[lineStart + indentLength] is ' ' or '\t')
        {
            indentLength++;
        }

        indent = text.Substring(lineStart, indentLength);
        return true;
    }

    /// <summary>Hängt den kommentierten Text auf eine eigene Zeile an und beginnt dahinter eine neue Zeile mit derselben Einrückung für den Rest.</summary>
    private static void SplitLine(StringBuilder result, string replacement, string indent, string eol)
    {
        while (result.Length > 0 && result[^1] is ' ' or '\t')
        {
            result.Length--;
        }

        if (result.Length > 0 && result[^1] != '\n')
        {
            result.Append(eol);
        }

        result.Append(indent).Append(replacement).Append(eol).Append(indent);
    }

    private static bool IsMatchAt(string text, int index, string word) =>
        string.CompareOrdinal(text, index, word, 0, word.Length) == 0
        && (index == 0 || !IsWordChar(text[index - 1]))
        && (index + word.Length >= text.Length || !IsWordChar(text[index + word.Length]));

    private static bool IsWordChar(char c) => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_';
}
