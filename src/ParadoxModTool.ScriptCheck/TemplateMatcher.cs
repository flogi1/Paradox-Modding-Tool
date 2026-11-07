using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.ScriptCheck;

/// <summary>
/// Namen, die über einen Parameter zusammengesetzt werden (<c>rvt_tmpl_$X$_effect = yes</c>): Die Vorlagen-Wörter werden zu verankerten Mustern
/// (<c>$…$</c> = ein oder mehr Wortzeichen), und eine passende Definition gilt als benutzt.
/// </summary>
internal sealed class TemplateMatcher
{
    /// <summary>Mindestzahl fester Zeichen außerhalb der <c>$…$</c>-Teile; <c>$A$</c> oder <c>$A$_b</c> sagen nichts über den Namen.</summary>
    private const int MinLiteralChars = 3;

    private readonly List<Entry> _entries;

    public TemplateMatcher(IEnumerable<CheckTemplate> templates)
    {
        _entries = templates
            .GroupBy(t => t.Token, StringComparer.Ordinal)
            .Select(g => new Entry(g.Key, g.Select(t => (t.Owner, t.OwnerKind)).ToHashSet()))
            .ToList();
    }

    /// <summary>
    /// Die Vorlagen-Wörter eines Tokens: Teile aus Buchstaben, Ziffern, <c>_</c> und vollständigen <c>$…$</c>-Abschnitten mit mindestens
    /// <see cref="MinLiteralChars"/> festen Zeichen. Getrennt wird an allem anderen (<c>value:x_$A$</c>, <c>scope:a.$B$_c</c>); ein <c>$</c> ohne Gegenstück beendet die Suche.
    /// </summary>
    public static IEnumerable<string> WordsOf(string token)
    {
        if (!token.Contains('$'))
        {
            yield break;
        }

        var start = -1;
        var literals = 0;
        var hasParameter = false;
        for (var i = 0; i <= token.Length; i++)
        {
            var c = i < token.Length ? token[i] : '\0';
            if (c == '$')
            {
                var close = token.IndexOf('$', i + 1);
                if (close < 0)
                {
                    yield break;
                }

                start = start < 0 ? i : start;
                hasParameter = true;
                i = close;
                continue;
            }

            if (i < token.Length && (char.IsLetterOrDigit(c) || c == '_'))
            {
                start = start < 0 ? i : start;
                literals++;
                continue;
            }

            if (start >= 0 && hasParameter && literals >= MinLiteralChars)
            {
                yield return token[start..i];
            }

            start = -1;
            literals = 0;
            hasParameter = false;
        }
    }

    /// <summary>Ob eine Vorlage außerhalb der Definition <paramref name="name"/> (Art <paramref name="kind"/>) auf den Namen passt.</summary>
    public bool Uses(string name, CheckKind kind)
    {
        foreach (var entry in _entries)
        {
            if (entry.Matches(name) && entry.Sites.Any(s => !(s.Owner == name && s.OwnerKind == kind)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Für die Kalibrierung: je Vorlage die Anzahl passender Namen, die häufigsten zuerst (nur Vorlagen mit Treffer).</summary>
    public IReadOnlyList<(string Token, int Count)> Coverage(IReadOnlyCollection<string> names) =>
        _entries
            .Select(e => (e.Token, Count: names.Count(e.Matches)))
            .Where(e => e.Count > 0)
            .OrderByDescending(e => e.Count)
            .ThenBy(e => e.Token, StringComparer.Ordinal)
            .ToList();

    /// <summary>Eine Vorlage: fester Anfang und festes Ende filtern billig vor, das Muster entsteht erst beim ersten Bedarf.</summary>
    private sealed class Entry
    {
        private readonly string _prefix;
        private readonly string _suffix;
        private readonly Lazy<Regex> _pattern;

        public Entry(string token, HashSet<(string? Owner, CheckKind OwnerKind)> sites)
        {
            Token = token;
            Sites = sites;
            var parts = token.Split('$');
            _prefix = parts[0];
            _suffix = parts[^1];
            _pattern = new Lazy<Regex>(() =>
            {
                // Ungerade Teile stehen zwischen zwei $: das ist der Parameter.
                var regex = new StringBuilder("^");
                for (var i = 0; i < parts.Length; i++)
                {
                    regex.Append(i % 2 == 0 ? Regex.Escape(parts[i]) : "\\w+");
                }

                return new Regex(regex.Append('$').ToString(), RegexOptions.CultureInvariant);
            });
        }

        public string Token { get; }

        public HashSet<(string? Owner, CheckKind OwnerKind)> Sites { get; }

        public bool Matches(string name) =>
            name.Length > _prefix.Length + _suffix.Length
            && name.StartsWith(_prefix, StringComparison.Ordinal)
            && name.EndsWith(_suffix, StringComparison.Ordinal)
            && _pattern.Value.IsMatch(name);
    }
}
