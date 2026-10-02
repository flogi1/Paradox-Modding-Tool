using System.Text;
using ParadoxModTool.Scopes;

namespace ParadoxModTool.Templates;

/// <summary>Baut Muster aus der Form eines Namens (für "Regel aus dieser Zahl erstellen").</summary>
public static class RulePatternBuilder
{
    /// <summary>
    /// <paramref name="shape"/> ist der Name mit <c>#</c> für jeden Ziffernblock (<see cref="VariationPoint.Shape"/>); der Block mit der Nummer <paramref name="runIndex"/>
    /// (ab 0) wird zum markierten <c>[#]</c>.
    /// </summary>
    public static string FromShape(string shape, int runIndex)
    {
        var builder = new StringBuilder();
        var count = -1;
        foreach (var c in shape)
        {
            if (c == '#')
            {
                count++;
                builder.Append(count == runIndex ? "[#]" : "#");
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}

/// <summary>Prüft eine Regel vor dem Speichern.</summary>
public static class RuleValidator
{
    /// <summary>Die Fehlermeldungen (deutsch, für den Nutzer); leer, wenn die Regel gültig ist.</summary>
    public static IReadOnlyList<string> Validate(CountingRule rule)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            errors.Add(TemplatesTexts.ValidateNoName);
        }

        var patterns = rule.Patterns.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (patterns.Count == 0)
        {
            errors.Add(TemplatesTexts.ValidateNoPattern);
        }

        foreach (var pattern in patterns.Where(p => Markers(p) > 1))
        {
            errors.Add(TemplatesTexts.ValidateMultipleMarkers(pattern));
        }

        if (rule.Action is RuleAction.OnlyMarkedCounts or RuleAction.MarkedDoesNotCount)
        {
            foreach (var pattern in patterns.Where(p => Markers(p) == 0))
            {
                errors.Add(TemplatesTexts.ValidateMarkerMissing(RuleSummary.ActionText(rule.Action), pattern));
            }
        }

        if (rule.Where == RuleWhere.InsideKey && string.IsNullOrWhiteSpace(rule.WhereKey))
        {
            errors.Add(TemplatesTexts.ValidateNoKey);
        }

        return errors;
    }

    private static int Markers(string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = pattern.IndexOf("[#]", index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += 3;
        }

        return count;
    }
}

/// <summary>Kurze deutsche Texte für Listen und Dialoge.</summary>
public static class RuleSummary
{
    public static string ActionText(RuleAction action) => action switch
    {
        RuleAction.Ignore => TemplatesTexts.ActionIgnore,
        RuleAction.OnlyMarkedCounts => TemplatesTexts.ActionOnlyMarkedCounts,
        RuleAction.AllCount => TemplatesTexts.ActionAllCount,
        RuleAction.MarkedDoesNotCount => TemplatesTexts.ActionMarkedDoesNotCount,
        _ => action.ToString(),
    };

    public static string ActionHelp(RuleAction action) => action switch
    {
        RuleAction.Ignore => TemplatesTexts.ActionIgnoreHelp,
        RuleAction.OnlyMarkedCounts => TemplatesTexts.ActionOnlyMarkedCountsHelp,
        RuleAction.AllCount => TemplatesTexts.ActionAllCountHelp,
        RuleAction.MarkedDoesNotCount => TemplatesTexts.ActionMarkedDoesNotCountHelp,
        _ => string.Empty,
    };

    public static string WhereText(CountingRule rule) => rule.Where == RuleWhere.InsideKey ? TemplatesTexts.WhereInsideKey(rule.WhereKey) : TemplatesTexts.WhereAnywhere;

    public static string PatternsText(CountingRule rule, int max = 60)
    {
        var text = string.Join(" · ", rule.Patterns.Where(p => !string.IsNullOrWhiteSpace(p)));
        return text.Length <= max ? text : text[..Math.Max(0, max - 1)] + "…";
    }

    /// <summary>Die Spiele einer Regel; nur auf der Ebene "Alle Spiele" von Bedeutung, sonst leer.</summary>
    public static string GamesText(CountingRule rule, RuleLayer layer)
    {
        if (layer != RuleLayer.AllGames)
        {
            return string.Empty;
        }

        return rule.Games.Count == 0 || rule.Games.Contains("*") ? TemplatesTexts.GamesAll : string.Join(", ", rule.Games);
    }
}

/// <summary>Ein Ziffernblock in einer Zeile der Vorschau: Anfang und Länge im Text der Zeile und das Ergebnis der Regel.</summary>
public sealed record RulePreviewRun(int Start, int Length, RuleVerdict Verdict);

/// <summary>Ein Name aus dem Snippet, über dessen Zahlen die geprüfte Regel entscheidet.</summary>
public sealed record RulePreviewLine(string Text, IReadOnlyList<RulePreviewRun> Runs);

/// <summary>Zeigt für den Bearbeiten-Dialog, welche Namen im Snippet eine (noch nicht gespeicherte) Regel treffen.</summary>
public static class RulePreview
{
    /// <summary>Nur Zeilen, in denen die Regel mindestens einen Ziffernblock entscheidet; jeder Name einmal, höchstens <paramref name="maxLines"/>.</summary>
    public static IReadOnlyList<RulePreviewLine> Evaluate(string snippet, CountingRule draft, int maxLines = 200)
    {
        var text = snippet ?? string.Empty;
        var tokens = ScopeLexer.Tokenize(text);
        var evaluator = new RuleEvaluator(new[] { new EffectiveRule(draft, RuleLayer.Template) }, text, tokens);
        var candidates = DigitScanner.Scan(text, tokens, evaluator);

        var lines = new List<RulePreviewLine>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in candidates.GroupBy(c => c.TokenIndex))
        {
            if (group.All(c => c.Verdict == RuleVerdict.Open))
            {
                continue;
            }

            var token = tokens[group.Key];
            var raw = text.Substring(token.Start, Math.Min(token.End, text.Length) - token.Start);
            if (!seen.Add(raw))
            {
                continue;
            }

            lines.Add(new RulePreviewLine(raw, group.Select(c => new RulePreviewRun(c.Offset - token.Start, c.Digits.Length, c.Verdict)).ToList()));
            if (lines.Count >= maxLines)
            {
                break;
            }
        }

        return lines;
    }
}
