using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Templates;

/// <summary>Treffer eines Musters: die Ziffernblöcke von <c>#</c> und <c>[#]</c> in Musterreihenfolge und der Anfang des markierten Blocks (-1 ohne Markierung).</summary>
public readonly record struct RuleMatch(IReadOnlyList<(int Start, int End)> Runs, int MarkedStart);

/// <summary>
/// Ein übersetztes Muster der Regel-Sprache: <c>*</c> beliebiger Text, <c>#</c> ein ganzer Ziffernblock, <c>[#]</c> der markierte Ziffernblock, alles andere Text.
/// Das Muster passt auf den ganzen Text. Intern entsteht ein .NET-<see cref="Regex"/> mit Zeitlimit; der Nutzer sieht keine regulären Ausdrücke.
/// </summary>
public sealed class RulePattern
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);

    private readonly Regex _regex;
    private readonly int _groups;
    private readonly int _markedGroup;

    private RulePattern(Regex regex, int groups, int markedGroup)
    {
        _regex = regex;
        _groups = groups;
        _markedGroup = markedGroup;
    }

    /// <summary>Enthält das Muster ein <c>[#]</c>?</summary>
    public bool HasMarker => _markedGroup >= 0;

    public static RulePattern Compile(string pattern, bool caseSensitive)
    {
        var builder = new StringBuilder("^");
        var groups = 0;
        var marked = -1;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '[' && i + 2 < pattern.Length && pattern[i + 1] == '#' && pattern[i + 2] == ']')
            {
                i += 2;
                if (marked < 0)
                {
                    marked = groups;
                }

                builder.Append("(?<n").Append(groups++).Append(">(?<![0-9])[0-9]+(?![0-9]))");
            }
            else if (c == '#')
            {
                builder.Append("(?<n").Append(groups++).Append(">(?<![0-9])[0-9]+(?![0-9]))");
            }
            else if (c == '*')
            {
                builder.Append(".*");
            }
            else
            {
                builder.Append(Regex.Escape(c.ToString()));
            }
        }

        builder.Append('$');
        return new RulePattern(Build(builder.ToString(), caseSensitive), groups, marked);
    }

    /// <summary>Muster für Schlüssel: nur <c>*</c> hat eine Bedeutung, ohne Beachtung der Groß-/Kleinschreibung.</summary>
    public static RulePattern CompileGlob(string key)
    {
        var builder = new StringBuilder("^");
        foreach (var c in key)
        {
            builder.Append(c == '*' ? ".*" : Regex.Escape(c.ToString()));
        }

        builder.Append('$');
        return new RulePattern(Build(builder.ToString(), caseSensitive: false), 0, -1);
    }

    /// <summary>Der Treffer im ganzen Text; <see langword="null"/>, wenn das Muster nicht passt (oder das Zeitlimit überschritten wurde).</summary>
    public RuleMatch? Match(string text)
    {
        Match match;
        try
        {
            match = _regex.Match(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        if (!match.Success)
        {
            return null;
        }

        var runs = new List<(int Start, int End)>();
        var markedStart = -1;
        for (var g = 0; g < _groups; g++)
        {
            var group = match.Groups["n" + g];
            runs.Add((group.Index, group.Index + group.Length));
            if (g == _markedGroup)
            {
                markedStart = group.Index;
            }
        }

        return new RuleMatch(runs, markedStart);
    }

    public bool IsMatch(string text) => Match(text) is not null;

    private static Regex Build(string expression, bool caseSensitive) =>
        new(expression, RegexOptions.Singleline | RegexOptions.CultureInvariant | (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase), Timeout);
}
