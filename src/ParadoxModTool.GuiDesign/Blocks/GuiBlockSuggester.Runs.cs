using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Blocks;

/// <summary>Wiederholungsvorschlag: ein oder mehrere verknüpfte Läufe gleicher Anzahl (Plan-2b-Ruling 4) mit ihren Parametern.</summary>
internal sealed class GuiBlockRunPlan
{
    public List<GuiBlockRunGroup> Groups { get; } = new();

    public List<GuiBlockRunParameter> Parameters { get; } = new();
}

/// <summary>
/// Ein Lauf: Bereich im Rumpf (von der ersten Elementzeile bis hinter das letzte Element bzw. dessen Leerzeilen), Text des ersten Eintrags ab
/// Zeilenanfang und die Ersetzungen darin (relativ zum Bereichsanfang).
/// </summary>
internal sealed class GuiBlockRunGroup
{
    public required TextSpan Region { get; init; }

    public required TextSpan FirstEntry { get; init; }

    public required string Gap { get; init; }

    public List<(TextSpan Span, string Text)> Edits { get; } = new();
}

internal sealed class GuiBlockRunParameter
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string Default { get; init; }

    public required IReadOnlyList<string> Values { get; init; }
}

public static partial class GuiBlockSuggester
{
    [GeneratedRegex(@"[^a-z0-9_]", RegexOptions.CultureInvariant)]
    private static partial Regex NotKeyCharacter();

    /// <summary>Wertstelle eines Eintrags: Spanne, Wert und Name (Eigenschaft bzw. <c>var</c>/<c>id</c>).</summary>
    private sealed record Position(TextSpan Span, string Value, string Name);

    private static IEnumerable<GuiBlockSuggestion> SuggestRuns(string body, GuiDocument document, KeyPool keys)
    {
        // Mit vorhandenen Markierungen keine Laufvorschläge (Plan-2b-Ruling 2).
        if (GuiBlockDraft.KeysIn(body).Any(k => k.IsRepeat))
        {
            return Array.Empty<GuiBlockSuggestion>();
        }

        var runs = new List<List<GuiNode>>();
        FindRuns(document.Root, body, runs);
        var plans = new List<(GuiBlockSuggestion Suggestion, GuiBlockRunPlan Plan, int Count)>();
        foreach (var run in runs)
        {
            var positions = run.Select(entry => PositionsOf(entry, body)).ToList();
            var differing = Differing(positions);
            if (differing is null)
            {
                continue;
            }

            // Verknüpfen: gleiche Anzahl und mindestens eine gleiche unterschiedliche Werteliste (Tab-Knöpfe und Tab-Inhalte).
            var linked = plans.FirstOrDefault(p => p.Count == run.Count
                && differing.Any(d => p.Plan.Parameters.Any(q => q.Values.SequenceEqual(d.Values))));
            GuiBlockRunPlan plan;
            string runKey;
            if (linked.Plan is not null)
            {
                plan = linked.Plan;
                runKey = linked.Suggestion.Key;
            }
            else
            {
                plan = new GuiBlockRunPlan();
                runKey = keys.Take(SanitizeKey(run[0].Name));
                var suggestion = new GuiBlockSuggestion("repeat:" + runKey, GuiBlockSuggestionKind.Repeat, runKey, GuiTexts.SuggestRepeat(run[0].Name), string.Empty, $"{run.Count}× {run[0].Name}", run.Count)
                {
                    Run = plan,
                };
                plans.Add((suggestion, plan, run.Count));
            }

            var group = NewGroup(run, body);
            foreach (var (spans, values, name) in differing)
            {
                var parameter = plan.Parameters.FirstOrDefault(p => p.Values.SequenceEqual(values));
                if (parameter is null)
                {
                    var ids = plan.Parameters.Count(p => p.Key.EndsWith(".id", StringComparison.Ordinal) || p.Key.Contains(".id_", StringComparison.Ordinal));
                    var label = name switch
                    {
                        "id" => GuiTexts.SuggestId(ids + 1),
                        "var" => GuiTexts.SuggestVariable(1),
                        _ => name,
                    };
                    parameter = new GuiBlockRunParameter
                    {
                        Key = keys.Take(runKey + "." + SanitizeKey(name)),
                        Label = label,
                        Default = CountingDefault(values),
                        Values = values,
                    };
                    plan.Parameters.Add(parameter);
                }

                foreach (var span in spans)
                {
                    group.Edits.Add((new TextSpan(span.Start - group.FirstEntry.Start, span.End - group.FirstEntry.Start), "$" + parameter.Key + "$"));
                }
            }

            AddCommentEdits(run, body, group, plan);
            plan.Groups.Add(group);
        }

        return plans.Select(p => p.Suggestion);
    }

    private static (IReadOnlyList<(TextSpan Span, string Text)> Edits, IReadOnlyList<GuiBlockDraftParameter> Parameters) ApplyRun(
        string body,
        GuiBlockSuggestion run,
        IReadOnlyList<GuiBlockSuggestion> values)
    {
        var plan = run.Run!;
        var edits = new List<(TextSpan Span, string Text)>();
        foreach (var group in plan.Groups)
        {
            var entryEdits = new List<(TextSpan Span, string Text)>(group.Edits);

            // Wertvorschläge im ersten Eintrag, sofern kein Laufparameter dieselbe Stelle belegt (Plan-2b-Ruling 7).
            foreach (var value in values)
            {
                foreach (var span in value.Spans.Where(s => group.FirstEntry.Contains(s)))
                {
                    var relative = new TextSpan(span.Start - group.FirstEntry.Start, span.End - group.FirstEntry.Start);
                    if (!entryEdits.Any(e => Overlaps(e.Span, relative)))
                    {
                        entryEdits.Add((relative, "$" + value.Key + "$"));
                    }
                }
            }

            var entry = new StringBuilder(body.Substring(group.FirstEntry.Start, group.FirstEntry.Length));
            foreach (var (span, text) in entryEdits.OrderByDescending(e => e.Span.Start))
            {
                entry.Remove(span.Start, span.Length).Insert(span.Start, text);
            }

            var section = $"$repeat {run.Key}$\n{entry}{group.Gap}$end {run.Key}$\n";
            edits.Add((group.Region, section));
        }

        var parameters = plan.Parameters
            .Select(p => new GuiBlockDraftParameter(p.Key, p.Label, p.Default, GuiBlockParameterKind.Text))
            .ToList();
        return (edits, parameters);
    }

    /// <summary>Läufe oben zuerst; in die Elemente eines Laufs wird nicht weiter hineingesucht (keine verschachtelten Vorschläge).</summary>
    private static void FindRuns(GuiNode parent, string body, List<List<GuiNode>> runs)
    {
        var children = parent.Children;
        var inRun = new HashSet<GuiNode>();
        for (var i = 0; i < children.Count;)
        {
            var run = new List<GuiNode> { children[i] };
            if (IsRunElement(children[i], body))
            {
                var signature = Signature(children[i]);
                var j = i + 1;
                while (j < children.Count
                    && IsRunElement(children[j], body)
                    && children[j].Name == children[i].Name
                    && Signature(children[j]) == signature
                    && OnlyBlankLinesBetween(body, children[j - 1], children[j]))
                {
                    run.Add(children[j]);
                    j++;
                }
            }

            if (run.Count >= 2)
            {
                runs.Add(run);
                inRun.UnionWith(run);
            }

            i += run.Count;
        }

        foreach (var child in children.Where(c => !inRun.Contains(c)))
        {
            FindRuns(child, body, runs);
        }
    }

    /// <summary>Element mit Rumpf, das auf eigener Zeile beginnt und mit seiner Klammer die Zeile beendet.</summary>
    private static bool IsRunElement(GuiNode node, string body) =>
        node is { Kind: GuiNodeKind.KeyedBlock, CloseBrace: >= 0 }
        && body[LineStart(body, node.Span.Start)..node.Span.Start].Trim().Length == 0
        && body[(node.CloseBrace + 1)..LineEnd(body, node.CloseBrace)].Trim().Length == 0;

    private static bool OnlyBlankLinesBetween(string body, GuiNode previous, GuiNode next) =>
        body[LineEnd(body, previous.CloseBrace)..LineStart(body, next.Span.Start)].Trim().Length == 0;

    /// <summary>Aufbau: Knotenart und Name je Kind, rekursiv; <c>using</c> mit Ziel; ohne Werte und Kommentare (Plan-2b-Ruling 2).</summary>
    private static string Signature(GuiNode node)
    {
        var text = new StringBuilder();
        text.Append(node.Kind).Append(':').Append(node.Name);
        if (node.Kind == GuiNodeKind.Using)
        {
            text.Append('=').Append(node.Value);
        }

        if (node.HasBody)
        {
            text.Append('{');
            foreach (var child in node.Children.Where(c => c.Kind != GuiNodeKind.Comment))
            {
                text.Append(Signature(child)).Append(';');
            }

            text.Append('}');
        }

        return text.ToString();
    }

    /// <summary>Wertstellen eines Eintrags in Dokumentreihenfolge; bei <c>[...]</c> mit GetVariableSystem die Argumente, sonst der ganze Wert.</summary>
    private static List<(GuiNode Node, List<Position> Parts, string Masked, Position Whole)> PositionsOf(GuiNode entry, string body)
    {
        var result = new List<(GuiNode, List<Position>, string, Position)>();
        foreach (var node in entry.Descendants())
        {
            if (node.Kind is not (GuiNodeKind.Property or GuiNodeKind.ListItem) || node.ValueSpan is not { } span || node.Value is null)
            {
                continue;
            }

            var inner = Inner(node, span);
            var name = node.Kind == GuiNodeKind.ListItem ? node.Parent?.Name ?? "item" : node.Name;
            var whole = new Position(inner, body.Substring(inner.Start, inner.Length), name);
            var parts = new List<Position>();
            var masked = whole.Value;
            if (node.IsQuoted && node.Value.StartsWith('['))
            {
                var matches = VariableCall().Matches(whole.Value);
                var mask = new StringBuilder(whole.Value);
                foreach (Match match in matches.Cast<Match>().Reverse())
                {
                    foreach (var groupName in new[] { "id", "var" })
                    {
                        var group = match.Groups[groupName];
                        if (group.Success)
                        {
                            mask.Remove(group.Index, group.Length).Insert(group.Index, "\u0001");
                        }
                    }
                }

                foreach (Match match in matches)
                {
                    foreach (var groupName in new[] { "var", "id" })
                    {
                        var group = match.Groups[groupName];
                        if (group.Success)
                        {
                            parts.Add(new Position(Span(inner.Start + group.Index, group.Length), group.Value, groupName));
                        }
                    }
                }

                masked = mask.ToString();
            }

            result.Add((node, parts, masked, whole));
        }

        return result;
    }

    /// <summary>
    /// Unterschiedliche Wertstellen über alle Einträge: je Stelle die Spannen im ersten Eintrag, die Werte je Eintrag und der Name. Null, wenn eine
    /// unterschiedliche Stelle keinen Platzhalter tragen darf (<c>$</c>, <c>|</c>, Zeilenumbruch) oder die Einträge nicht zusammenpassen.
    /// </summary>
    private static List<(List<TextSpan> Spans, IReadOnlyList<string> Values, string Name)>? Differing(
        List<List<(GuiNode Node, List<Position> Parts, string Masked, Position Whole)>> entries)
    {
        var count = entries[0].Count;
        if (entries.Any(e => e.Count != count))
        {
            return null;
        }

        var found = new List<(List<TextSpan> Spans, IReadOnlyList<string> Values, string Name)>();
        for (var i = 0; i < count; i++)
        {
            var useParts = entries.All(e => e[i].Parts.Count == entries[0][i].Parts.Count && e[i].Masked == entries[0][i].Masked)
                && entries[0][i].Parts.Count > 0;
            var positions = useParts
                ? Enumerable.Range(0, entries[0][i].Parts.Count).Select(p => entries.Select(e => e[i].Parts[p]).ToList())
                : new[] { entries.Select(e => e[i].Whole).ToList() };
            foreach (var position in positions)
            {
                var values = position.Select(p => p.Value).ToList();
                if (values.All(v => v == values[0]))
                {
                    continue;
                }

                if (values.Any(v => v.IndexOfAny(new[] { '$', '|', '\n', '\r' }) >= 0))
                {
                    return null;
                }

                var existing = found.FirstOrDefault(f => f.Values.SequenceEqual(values));
                if (existing.Spans is not null)
                {
                    existing.Spans.Add(position[0].Span);
                }
                else
                {
                    found.Add((new List<TextSpan> { position[0].Span }, values, position[0].Name));
                }
            }
        }

        return found;
    }

    /// <summary>Bereich eines Laufs; Leerzeilen gehören dazu, wenn auf jedes Element (auch das letzte) gleich viele folgen (Plan-2b-Ruling 6).</summary>
    private static GuiBlockRunGroup NewGroup(List<GuiNode> run, string body)
    {
        var start = LineStart(body, run[0].Span.Start);
        var firstEnd = LineEnd(body, run[0].CloseBrace);
        var gaps = run.Select(entry => BlankLinesAfter(body, LineEnd(body, entry.CloseBrace))).ToList();
        var uniform = gaps.All(g => g == gaps[0]);
        var lastEnd = LineEnd(body, run[^1].CloseBrace);
        var gap = uniform ? body.Substring(firstEnd, gaps[0]) : string.Empty;
        var end = uniform ? lastEnd + gaps[^1] : lastEnd;
        return new GuiBlockRunGroup { Region = new TextSpan(start, end), FirstEntry = new TextSpan(start, firstEnd), Gap = gap };
    }

    /// <summary>Länge der Leerzeilen (samt Umbrüchen) ab <paramref name="position"/>.</summary>
    private static int BlankLinesAfter(string body, int position)
    {
        var end = position;
        while (end < body.Length)
        {
            var next = LineEnd(body, end);
            if (body[end..next].Trim().Length > 0)
            {
                break;
            }

            end = next;
        }

        return end - position;
    }

    /// <summary>
    /// Kommentare im ersten Eintrag bleiben; enthält jeder Eintrag denselben Kommentar bis auf den eigenen Wert eines Laufparameters, kommt dort der
    /// Platzhalter hinein (Plan-2b-Ruling 3).
    /// </summary>
    private static void AddCommentEdits(List<GuiNode> run, string body, GuiBlockRunGroup group, GuiBlockRunPlan plan)
    {
        var comments = run.Select(e => e.Descendants().Where(n => n.Kind == GuiNodeKind.Comment).ToList()).ToList();
        if (comments.Any(c => c.Count != comments[0].Count))
        {
            return;
        }

        for (var c = 0; c < comments[0].Count; c++)
        {
            var texts = comments.Select(list => body.Substring(list[c].Span.Start, list[c].Span.Length)).ToList();
            foreach (var parameter in plan.Parameters)
            {
                var first = parameter.Values[0];
                if (!texts[0].Contains(first, StringComparison.Ordinal))
                {
                    continue;
                }

                var placeholder = "$" + parameter.Key + "$";
                var replaced = texts[0].Replace(first, placeholder, StringComparison.Ordinal);
                if (texts.Select((t, i) => t.Replace(parameter.Values[i], placeholder, StringComparison.Ordinal)).All(t => t == replaced))
                {
                    var span = comments[0][c].Span;
                    group.Edits.Add((new TextSpan(span.Start - group.FirstEntry.Start, span.End - group.FirstEntry.Start), replaced));
                    break;
                }
            }
        }
    }

    /// <summary>Zählmuster: ersetzt man in jedem Wert die eigene Nummer durch <c>$index$</c> und kommt überall derselbe Text heraus, ist das die Vorgabe.</summary>
    private static string CountingDefault(IReadOnlyList<string> values)
    {
        var templates = values
            .Select((value, i) => Regex.Replace(value, $@"(?<!\d){(i + 1).ToString(CultureInfo.InvariantCulture)}(?!\d)", "$$index$$"))
            .ToList();
        return templates.All(t => t == templates[0]) && templates[0].Contains("$index$", StringComparison.Ordinal) ? templates[0] : values[0];
    }

    private static string SanitizeKey(string name)
    {
        var key = NotKeyCharacter().Replace(name.ToLowerInvariant(), "_").Trim('_');
        return key.Length == 0 ? "items" : key;
    }

    private static int LineStart(string body, int position) => position <= 0 ? 0 : body.LastIndexOf('\n', position - 1) + 1;

    private static int LineEnd(string body, int position)
    {
        var newline = body.IndexOf('\n', Math.Min(position, body.Length));
        return newline < 0 ? body.Length : newline + 1;
    }
}
