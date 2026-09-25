using ParadoxModTool.Scopes;

namespace ParadoxModTool.Templates;

/// <summary>Ergebnis der Regeln für einen Ziffernblock.</summary>
public enum RuleVerdict
{
    /// <summary>Keine Regel hat entschieden; es gilt die übrige Logik.</summary>
    Open,

    Counts,

    NotCounts,
}

/// <summary>Das Ergebnis für einen Ziffernblock und die Regel, die entschieden hat (bei <see cref="RuleVerdict.Open"/> <see langword="null"/>).</summary>
public readonly record struct RunVerdict(RuleVerdict Verdict, EffectiveRule? Rule);

/// <summary>
/// Wendet die Regeln in ihrer Rangfolge auf die Ziffernblöcke eines Bezeichners an. Je Block entscheidet die erste Regel, die für ihn ein Ergebnis liefert.
/// Unvollständige Regeln (kein Muster, ein <c>[#]</c> fehlt bei einer Aktion, die es braucht, kein Schlüssel) entscheiden nichts.
/// </summary>
public sealed class RuleEvaluator
{
    private readonly List<Compiled> _rules = new();
    private readonly Dictionary<string, int> _hits = new();
    private readonly List<(int Start, int End, string Key)> _keyRanges = new();

    public RuleEvaluator(IReadOnlyList<EffectiveRule> rules, string text, IReadOnlyList<ScopeToken> tokens)
    {
        foreach (var effective in rules)
        {
            var compiled = Compile(effective);
            if (compiled is not null)
            {
                _rules.Add(compiled);
            }
        }

        if (_rules.Any(r => r.KeyGlob is not null))
        {
            CollectKeyRanges(ScriptTree.Parse(tokens, text.Length), text.Length);
        }
    }

    /// <summary>Je Regelkennung die Zahl der Ziffernblöcke, über die sie entschieden hat.</summary>
    public IReadOnlyDictionary<string, int> Hits => _hits;

    /// <param name="tokenStart">Position des Tokens im Text (für "Innerhalb von Schlüssel").</param>
    /// <param name="content">Text des Tokens ohne Anführungszeichen.</param>
    /// <param name="runStarts">Anfänge der Ziffernblöcke des Tokens in Koordinaten von <paramref name="content"/>.</param>
    public RunVerdict[] Evaluate(int tokenStart, string content, IReadOnlyList<int> runStarts)
    {
        var result = new RunVerdict[runStarts.Count];
        var open = runStarts.Count;
        foreach (var rule in _rules)
        {
            if (open == 0)
            {
                break;
            }

            if (rule.KeyGlob is not null && !InsideMatchingKey(rule.KeyGlob, tokenStart))
            {
                continue;
            }

            RulePattern? matchedPattern = null;
            RuleMatch? match = null;
            foreach (var pattern in rule.Patterns)
            {
                match = pattern.Match(content);
                if (match is not null)
                {
                    matchedPattern = pattern;
                    break;
                }
            }

            if (match is null || matchedPattern is null)
            {
                continue;
            }

            for (var i = 0; i < runStarts.Count; i++)
            {
                if (result[i].Verdict != RuleVerdict.Open)
                {
                    continue;
                }

                var marked = matchedPattern.HasMarker && match.Value.MarkedStart == runStarts[i];
                var verdict = rule.Effective.Rule.Action switch
                {
                    RuleAction.Ignore => RuleVerdict.NotCounts,
                    RuleAction.AllCount => RuleVerdict.Counts,
                    RuleAction.OnlyMarkedCounts => marked ? RuleVerdict.Counts : RuleVerdict.NotCounts,
                    RuleAction.MarkedDoesNotCount => marked ? RuleVerdict.NotCounts : RuleVerdict.Open,
                    _ => RuleVerdict.Open,
                };

                if (verdict != RuleVerdict.Open)
                {
                    result[i] = new RunVerdict(verdict, rule.Effective);
                    _hits[rule.Effective.Rule.Id] = _hits.GetValueOrDefault(rule.Effective.Rule.Id) + 1;
                    open--;
                }
            }
        }

        return result;
    }

    private static Compiled? Compile(EffectiveRule effective)
    {
        var rule = effective.Rule;
        var patterns = rule.Patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => RulePattern.Compile(p, rule.CaseSensitive))
            .ToList();
        if (patterns.Count == 0)
        {
            return null;
        }

        var needsMarker = rule.Action is RuleAction.OnlyMarkedCounts or RuleAction.MarkedDoesNotCount;
        if (needsMarker)
        {
            patterns = patterns.Where(p => p.HasMarker).ToList();
            if (patterns.Count == 0)
            {
                return null;
            }
        }

        RulePattern? keyGlob = null;
        if (rule.Where == RuleWhere.InsideKey)
        {
            if (string.IsNullOrWhiteSpace(rule.WhereKey))
            {
                return null;
            }

            keyGlob = RulePattern.CompileGlob(rule.WhereKey.Trim());
        }

        return new Compiled(effective, patterns, keyGlob);
    }

    private void CollectKeyRanges(List<ScriptNode> nodes, int textLength)
    {
        foreach (var node in nodes)
        {
            if (node.Key is not null)
            {
                _keyRanges.Add((node.Start, Math.Min(node.End, textLength), node.Key));
            }

            if (node.Children is not null)
            {
                CollectKeyRanges(node.Children, textLength);
            }
        }
    }

    private bool InsideMatchingKey(RulePattern glob, int offset) =>
        _keyRanges.Any(r => offset >= r.Start && offset < r.End && glob.IsMatch(r.Key));

    private sealed record Compiled(EffectiveRule Effective, List<RulePattern> Patterns, RulePattern? KeyGlob);
}
