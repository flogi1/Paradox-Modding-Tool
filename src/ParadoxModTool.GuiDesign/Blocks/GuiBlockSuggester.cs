using System.Text;
using System.Text.RegularExpressions;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Blocks;

public enum GuiBlockSuggestionKind
{
    Value,
    Repeat,
}

/// <summary>
/// Vorschlag für "Platzhalter vorschlagen": ein Wert (alle Stellen mit diesem Wert) oder eine Wiederholung. <see cref="Found"/> ist der gefundene Wert
/// bzw. "3× button", <see cref="Count"/> die Zahl der Stellen bzw. Einträge.
/// </summary>
public sealed record GuiBlockSuggestion(string Id, GuiBlockSuggestionKind Kind, string Key, string Label, string Default, string Found, int Count)
{
    /// <summary>Wert: die Spannen (ohne Anführungszeichen) im Rumpf.</summary>
    internal IReadOnlyList<TextSpan> Spans { get; init; } = Array.Empty<TextSpan>();

    /// <summary>Wiederholung: Läufe und ihre Parameter.</summary>
    internal GuiBlockRunPlan? Run { get; init; }
}

public sealed record GuiBlockSuggestionResult(string Body, IReadOnlyList<GuiBlockDraftParameter> Parameters, IReadOnlyList<GuiBlockDraftRepeat> Repeats);

/// <summary>
/// Schlägt für Code im Dialog "Als Baustein speichern" Platzhalter und Wiederholungen vor (Spec-Abschnitt "Platzhalter vorschlagen", Plan 2b) und
/// wendet die gewählten als Textersetzungen an. Vorgabe ist immer der ursprüngliche Wert, sodass der Baustein mit Vorgaben den Code wieder erzeugt.
/// </summary>
public static partial class GuiBlockSuggester
{
    private static readonly string[] TextProperties = { "text", "raw_text", "tooltip" };

    [GeneratedRegex(@"GetVariableSystem\.(?:Set|SetIf|HasValue|Exists|Clear|Toggle)\(\s*'(?<var>[^'$|\n]*)'(?:\s*,\s*'(?<id>[^'$|\n]*)')?", RegexOptions.CultureInvariant)]
    private static partial Regex VariableCall();

    public static IReadOnlyList<GuiBlockSuggestion> Suggest(string body)
    {
        var document = GuiParser.Parse(body);
        var keys = new KeyPool(GuiBlockDraft.KeysIn(body).Select(k => k.Key));
        var values = new List<GuiBlockSuggestion>();
        var byValue = new Dictionary<(string Category, string Value), int>();
        var counters = new Dictionary<string, int>(StringComparer.Ordinal);

        void Add(string category, string value, TextSpan span)
        {
            if (value.Length == 0 || value.IndexOfAny(new[] { '$', '|', '\n', '\r' }) >= 0)
            {
                return;
            }

            if (byValue.TryGetValue((category, value), out var index))
            {
                var known = values[index];
                values[index] = known with { Count = known.Count + 1, Spans = known.Spans.Append(span).ToList() };
                return;
            }

            var (key, label) = NewKey(category, keys, counters);
            byValue[(category, value)] = values.Count;
            values.Add(new GuiBlockSuggestion(key, GuiBlockSuggestionKind.Value, key, label, value, value, 1) { Spans = new[] { span } });
        }

        foreach (var node in document.Root.Descendants())
        {
            var topLevel = node.Parent is { Kind: GuiNodeKind.KeyedBlock } element && element.Parent?.Kind == GuiNodeKind.Document;
            if (node is { Kind: GuiNodeKind.Property, Value: { } value, ValueSpan: { } span })
            {
                var inner = Inner(node, span);
                if (node.Name == "name" && topLevel)
                {
                    Add("name", value, inner);
                }
                else if (node.IsQuoted && value.StartsWith('['))
                {
                    foreach (Match match in VariableCall().Matches(body.Substring(inner.Start, inner.Length)))
                    {
                        Add("var", match.Groups["var"].Value, Span(inner.Start + match.Groups["var"].Index, match.Groups["var"].Length));
                        if (match.Groups["id"].Success)
                        {
                            Add("id", match.Groups["id"].Value, Span(inner.Start + match.Groups["id"].Index, match.Groups["id"].Length));
                        }
                    }
                }
                else if (node.IsQuoted && TextProperties.Contains(node.Name))
                {
                    Add("text", value, inner);
                }
                else if (node.IsQuoted && node.Name == "texture" && value.StartsWith("gfx/", StringComparison.OrdinalIgnoreCase))
                {
                    Add("texture", value, inner);
                }
            }
            else if (node is { Kind: GuiNodeKind.KeyedBlock, Name: "size" } && topLevel
                && node.Children.Count == 2
                && node.Children.All(c => c is { Kind: GuiNodeKind.ListItem, IsQuoted: false, ValueSpan: not null } && double.TryParse(c.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)))
            {
                Add("width", node.Children[0].Value!, node.Children[0].ValueSpan!.Value);
                Add("height", node.Children[1].Value!, node.Children[1].ValueSpan!.Value);
            }
        }

        return values.Concat(SuggestRuns(body, document, keys)).ToList();
    }

    public static GuiBlockSuggestionResult Apply(string body, IReadOnlyList<GuiBlockSuggestion> chosen)
    {
        var replacements = new List<(TextSpan Span, string Text)>();
        var parameters = new List<GuiBlockDraftParameter>();
        var repeats = new List<GuiBlockDraftRepeat>();

        // Läufe zuerst: sie ersetzen ganze Bereiche; Wertstellen darin gelten nur im ersten Eintrag (Plan-2b-Ruling 7).
        var values = chosen.Where(s => s.Kind == GuiBlockSuggestionKind.Value).ToList();
        foreach (var run in chosen.Where(s => s.Kind == GuiBlockSuggestionKind.Repeat && s.Run is not null))
        {
            var (edits, runParameters) = ApplyRun(body, run, values);
            replacements.AddRange(edits);
            parameters.AddRange(runParameters);
            repeats.Add(new GuiBlockDraftRepeat(run.Key, run.Label, run.Count));
        }

        foreach (var value in values)
        {
            var used = false;
            foreach (var span in value.Spans)
            {
                if (replacements.Any(r => Overlaps(r.Span, span)))
                {
                    continue;
                }

                replacements.Add((span, "$" + value.Key + "$"));
                used = true;
            }

            if (used || InsideReplacedRuns(value, replacements))
            {
                parameters.Add(new GuiBlockDraftParameter(value.Key, value.Label, value.Default, GuiBlockParameterKind.Text));
            }
        }

        var text = new StringBuilder(body);
        foreach (var (span, replacement) in replacements.OrderByDescending(r => r.Span.Start))
        {
            text.Remove(span.Start, span.Length).Insert(span.Start, replacement);
        }

        return new GuiBlockSuggestionResult(text.ToString(), parameters, repeats);
    }

    private static bool Overlaps(TextSpan a, TextSpan b) => a.Start < b.End && b.Start < a.End;

    /// <summary>Spanne aus Anfang und Länge (<see cref="TextSpan"/> nimmt Anfang und Ende).</summary>
    private static TextSpan Span(int start, int length) => new(start, start + length);

    /// <summary>Ein Wert, dessen Stellen alle in gewählten Läufen liegen, zählt, wenn der Lauf ihn im ersten Eintrag eingesetzt hat.</summary>
    private static bool InsideReplacedRuns(GuiBlockSuggestion value, List<(TextSpan Span, string Text)> replacements) =>
        replacements.Any(r => r.Text.Contains("$" + value.Key + "$", StringComparison.Ordinal));

    private static TextSpan Inner(GuiNode node, TextSpan span) =>
        node.IsQuoted && span.Length >= 2 ? new TextSpan(span.Start + 1, span.End - 1) : span;

    private static (string Key, string Label) NewKey(string category, KeyPool keys, Dictionary<string, int> counters)
    {
        switch (category)
        {
            case "name":
                return (keys.Take("name"), GuiTexts.SuggestName);
            case "width":
                return (keys.Take("width"), GuiTexts.SuggestWidth);
            case "height":
                return (keys.Take("height"), GuiTexts.SuggestHeight);
        }

        string key;
        int number;
        do
        {
            number = counters.GetValueOrDefault(category) + 1;
            counters[category] = number;
            key = $"{category}_{number}";
        }
        while (!keys.TryTake(key));

        var label = category switch
        {
            "text" => GuiTexts.SuggestText(number),
            "texture" => GuiTexts.SuggestTexture(number),
            "var" => GuiTexts.SuggestVariable(number),
            _ => GuiTexts.SuggestId(number),
        };
        return (key, label);
    }

    /// <summary>Vergebene Schlüssel: die schon im Rumpf benutzten, <c>index</c> und alle neuen; Kollisionen bekommen <c>_2</c>, <c>_3</c> usw.</summary>
    internal sealed class KeyPool
    {
        private readonly HashSet<string> _taken;

        public KeyPool(IEnumerable<string> existing) =>
            _taken = new HashSet<string>(existing.Append(GuiBlockParser.IndexKey), StringComparer.Ordinal);

        public bool TryTake(string key) => _taken.Add(key);

        public string Take(string key)
        {
            if (TryTake(key))
            {
                return key;
            }

            for (var i = 2; ; i++)
            {
                if (TryTake($"{key}_{i}"))
                {
                    return $"{key}_{i}";
                }
            }
        }
    }
}
