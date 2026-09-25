using System.Globalization;
using System.Text;
using ParadoxModTool.Scopes;

namespace ParadoxModTool.Templates;

/// <summary>Welche Zahl zählt, wenn ein Bezeichner mehrere frei stehende Zahlen enthält (<c>ARA_1_1</c>: erste = Zähler, letzte = Stufe).</summary>
public enum MultiNumberRule
{
    /// <summary>Nur die erste Zahl zählt (Standard).</summary>
    First,

    /// <summary>Nur die letzte Zahl zählt.</summary>
    Last,

    /// <summary>Alle frei stehenden Zahlen zählen.</summary>
    All,
}

/// <summary>Frühere Art der eigenen Regel je Form eines Bezeichners. Wird nur noch beim Laden alter Vorlagen gelesen und in angehakte Zahlen umgerechnet (siehe <see cref="SavedTemplate.MigrateLegacyRules"/>).</summary>
public enum TokenRule
{
    First,
    Second,
    Last,

    /// <summary>Jede Zahl im Bezeichner zählt.</summary>
    All,

    /// <summary>Keine Zahl im Bezeichner zählt.</summary>
    None,
}

public sealed record AnalyzeOptions(
    bool CollapseExamples = true,
    MultiNumberRule MultiNumbers = MultiNumberRule.First,
    IReadOnlyDictionary<string, IReadOnlyCollection<int>>? ShapeNumbers = null,
    IReadOnlyList<EffectiveRule>? Rules = null);

/// <summary>
/// Findet in einem Beispiel-Snippet die Stellen, die beim Erzeugen hochgezählt werden.
/// Ablauf: (1) Gleichförmige Unterblöcke werden zu einer <see cref="RepeatGroup"/> zusammengefasst, (2) mehrfach eingefügte Beispiele auf oberster Ebene werden zu einer Vorlage
/// zusammengefasst; aus den Unterschieden der Ziffern lernt der Analyzer Start und Schritt. (3) Alle Ziffernblöcke des übrigen Textes werden als Zählpunkte angeboten.
/// </summary>
public static class TemplateAnalyzer
{

    public static TemplateModel Analyze(string snippet, AnalyzeOptions? options = null, IEnumerable<PointOverride>? overrides = null)
    {
        options ??= new AnalyzeOptions();
        var source = snippet ?? string.Empty;
        var bom = source.Length > 0 && source[0] == '\uFEFF' ? 1 : 0;
        var firstContent = bom;
        while (firstContent < source.Length && char.IsWhiteSpace(source[firstContent]))
        {
            firstContent++;
        }

        // Führende Leerzeilen fallen weg, die Einrückung der ersten Zeile mit Inhalt bleibt erhalten (sonst fehlen ihr die Tabs, den übrigen Zeilen aber nicht).
        var lead = firstContent >= source.Length ? firstContent : Math.Max(bom, source.LastIndexOf('\n', firstContent) + 1);
        var text = source[lead..].TrimEnd();
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var groups = new List<GroupInfo>();
        var inner = new Dictionary<int, Learn>();
        var outer = new Dictionary<int, Learn>();
        var exampleCount = 0;
        var originalLength = text.Length;
        Func<int, int> forward = o => o;

        if (options.CollapseExamples && text.Length > 0)
        {
            var passA = new CollapsePass(text);
            passA.RunNested();
            var (t1, mapA) = ApplyRemovals(text, passA.Removals);
            inner = MapKeys(passA.Learned, mapA);
            groups = MapGroups(passA.Groups, mapA);
            text = t1;

            var passB = new CollapsePass(text);
            passB.RunTop();
            var (t2, mapB) = ApplyRemovals(text, passB.Removals);
            outer = MapKeys(passB.Learned, mapB);
            inner = MapKeys(inner, mapB);
            groups = MapGroups(groups, mapB);
            exampleCount = passB.MaxRepeats;
            text = t2;
            forward = o =>
            {
                var m1 = mapA(o);
                return m1 < 0 ? -1 : mapB(m1);
            };
        }

        var sourceOffsets = new int[text.Length];
        Array.Fill(sourceOffsets, -1);
        for (var o = 0; o < originalLength; o++)
        {
            var mapped = forward(o);
            if (mapped >= 0 && mapped < sourceOffsets.Length)
            {
                sourceOffsets[mapped] = o + lead;
            }
        }

        groups = groups.OrderBy(g => g.Start).ThenByDescending(g => g.End).ToList();
        var model = new TemplateModel
        {
            Text = text,
            Newline = newline,
            ExampleCount = exampleCount,
            SourceOffsets = sourceOffsets,
            Groups = groups.Select(g => new RepeatGroup { Start = g.Start, End = g.End, Count = g.Count, Separator = g.Separator }).ToList(),
        };

        BuildPoints(model, inner, outer, overrides, options.MultiNumbers, options.ShapeNumbers, options.Rules ?? ShippedRules.Effective());
        return model;
    }

    private static void BuildPoints(TemplateModel model, Dictionary<int, Learn> inner, Dictionary<int, Learn> outer, IEnumerable<PointOverride>? overrides, MultiNumberRule rule, IReadOnlyDictionary<string, IReadOnlyCollection<int>>? shapeNumbers, IReadOnlyList<EffectiveRule> rules)
    {
        var text = model.Text;
        var tokens = ScopeLexer.Tokenize(text);
        var byKey = new Dictionary<string, List<Candidate>>();
        var order = new List<string>();
        var reasonByKey = new Dictionary<string, string>();

        var evaluator = new RuleEvaluator(rules, text, tokens);
        var scanned = DigitScanner.Scan(text, tokens, evaluator);
        model.RuleHits = new Dictionary<string, int>(evaluator.Hits);
        model.Shapes.AddRange(BuildShapes(scanned, rule, shapeNumbers));

        foreach (var candidate in scanned)
        {
            var group = InnermostGroup(model.Groups, candidate.Offset);
            var reason = candidate.Verdict == RuleVerdict.NotCounts ? candidate.RuleReason : null;
            var key = string.Create(CultureInfo.InvariantCulture, $"{candidate.Kind}|{candidate.Context}|{candidate.Prefix}|{candidate.Digits}|g{group}") + (reason is null ? string.Empty : "|ign");
            if (reason is not null)
            {
                reasonByKey[key] = reason;
            }
            if (!byKey.TryGetValue(key, out var list))
            {
                byKey[key] = list = new List<Candidate>();
                order.Add(key);
            }

            list.Add(candidate);
        }

        var overrideMap = overrides?.GroupBy(o => o.Key).ToDictionary(g => g.Key, g => g.Last()) ?? new Dictionary<string, PointOverride>();

        foreach (var key in order)
        {
            var list = byKey[key];
            var first = list[0];
            var group = InnermostGroup(model.Groups, first.Offset);
            Learn? innerLearn = null;
            Learn? outerLearn = null;
            foreach (var c in list)
            {
                innerLearn ??= inner.TryGetValue(c.Offset, out var il) ? il : null;
                outerLearn ??= outer.TryGetValue(c.Offset, out var ol) ? ol : null;
            }

            var value = long.Parse(first.Digits, CultureInfo.InvariantCulture);
            var irregular = innerLearn is { Irregular: true } || outerLearn is { Irregular: true };
            var anyRegular = IsRegular(innerLearn) || IsRegular(outerLearn);
            var learned = innerLearn is not null || outerLearn is not null;
            var explicitRules = list.Where(c => shapeNumbers is not null && shapeNumbers.ContainsKey(c.Shape)).ToList();
            var counts = first.Kind == PointKind.Number
                || (explicitRules.Count > 0
                    ? explicitRules.All(c => shapeNumbers![c.Shape].Contains(c.RunIndex))
                    : list.All(c => AutomaticRuleCounts(c, rule)));
            var ruleDisabled = !counts;
            var ruleCounts = list.All(c => c.Verdict == RuleVerdict.Counts);

            var point = new VariationPoint
            {
                Key = key,
                Kind = first.Kind,
                Label = LabelFor(first, tokens, text),
                Occurrences = list.Select(c => new PointOccurrence(c.Offset, c.Digits.Length)).ToList(),
                RepeatGroup = group,
                Start = outerLearn?.Start ?? innerLearn?.Start ?? value,
                Step = outerLearn?.Step ?? (group >= 0 ? 0 : 1),
                InnerStep = innerLearn?.Step ?? 0,
                Width = outerLearn?.Width ?? innerLearn?.Width ?? ZeroWidth(first.Digits),
                Irregular = irregular,
                ConstantInExamples = learned && !anyRegular && !irregular,
                Enabled = learned ? anyRegular && !irregular : !ruleDisabled && (first.Kind != PointKind.Number || ruleCounts) && (!reasonByKey.ContainsKey(key) || explicitRules.Count > 0),
                LearnedFromExamples = learned && anyRegular && !irregular,
                IgnoredReason = reasonByKey.TryGetValue(key, out var ignoredReason) ? ignoredReason : string.Empty,
                CountedByRule = ruleCounts ? first.RuleReason : string.Empty,
                RuleDisabled = !learned && ruleDisabled,
                Shape = first.Shape,
                RunCount = first.RunCount,
                RunIndex = first.RunIndex,
                HasCustomRule = explicitRules.Count > 0,
            };

            if (overrideMap.TryGetValue(key, out var o))
            {
                point.Enabled = o.Enabled ?? point.Enabled;
                point.Start = o.Start ?? point.Start;
                point.Step = o.Step ?? point.Step;
                point.InnerStep = o.InnerStep ?? point.InnerStep;
            }

            model.Points.Add(point);
        }
    }

    private static bool IsRegular(Learn? learn) => learn is { Irregular: false, Constant: false };

    /// <summary>Fasst für die Prüfliste zusammen, welche Zahlen der Bezeichner einer Form zählen (Beispiel: das jeweils erste Vorkommen der Form).</summary>
    private static List<ShapeInfo> BuildShapes(List<Candidate> all, MultiNumberRule rule, IReadOnlyDictionary<string, IReadOnlyCollection<int>>? shapeNumbers)
    {
        var result = new List<ShapeInfo>();
        foreach (var group in all.Where(c => c.Kind != PointKind.Number && c.RunCount >= 2).GroupBy(c => c.Shape))
        {
            var firstToken = group.First().TokenIndex;
            var runs = group.Where(c => c.TokenIndex == firstToken).OrderBy(c => c.RunIndex).ToList();
            IReadOnlyCollection<int>? custom = null;
            shapeNumbers?.TryGetValue(group.Key, out custom);
            result.Add(new ShapeInfo(
                group.Key,
                runs.Select(c => c.Digits).ToList(),
                runs.Select(c => custom is not null ? custom.Contains(c.RunIndex) : AutomaticRuleCounts(c, rule)).ToList(),
                custom is not null));
        }

        return result;
    }

    /// <summary>Entscheidet eine Regel, gilt sie; sonst: Namenszahlen (iv2_…) zählen nie, und bei mehreren Zahlen entscheidet die allgemeine Regel.</summary>
    private static bool AutomaticRuleCounts(Candidate c, MultiNumberRule rule) => c.Verdict switch
    {
        RuleVerdict.Counts => true,
        RuleVerdict.NotCounts => false,
        _ => !c.NameDigit && (c.RunCount < 2 || IsCounterRun(c, rule)),
    };

    /// <summary>Bei Bezeichnern mit mehreren Zahlen: ist dieser Ziffernblock der, der nach der Regel zählt?</summary>
    private static bool IsCounterRun(Candidate candidate, MultiNumberRule rule) => candidate.StandaloneIndex >= 0 && rule switch
    {
        MultiNumberRule.First => candidate.StandaloneIndex == 0,
        MultiNumberRule.Last => candidate.StandaloneIndex == candidate.StandaloneCount - 1,
        _ => true,
    };

    private static string LabelFor(Candidate candidate, IReadOnlyList<ScopeToken> tokens, string text)
    {
        if (candidate.Kind == PointKind.Number)
        {
            return candidate.Context.Length > 0 ? $"{candidate.Context} = {candidate.Digits}" : candidate.Digits;
        }

        var token = tokens[candidate.TokenIndex];
        var label = token.Text;
        if (candidate.RunCount >= 2)
        {
            // Der gezählte Block steht in Klammern, damit man ihn von den anderen Zahlen im selben Bezeichner unterscheidet.
            var at = candidate.Offset - token.Start - (token.Kind == ScopeTokenKind.String ? 1 : 0);
            if (at >= 0 && at + candidate.Digits.Length <= label.Length)
            {
                label = label[..at] + "[" + candidate.Digits + "]" + label[(at + candidate.Digits.Length)..];
                if (label.Length > 60)
                {
                    var from = Math.Max(0, at - 25);
                    label = (from > 0 ? "…" : string.Empty) + label[from..Math.Min(label.Length, from + 55)] + "…";
                }
            }

            return label;
        }

        return label.Length > 50 ? label[..50] + "…" : label;
    }

    private static int InnermostGroup(IReadOnlyList<RepeatGroup> groups, int offset)
    {
        var best = -1;
        for (var i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (offset >= g.Start && offset < g.End && (best < 0 || g.End - g.Start < groups[best].End - groups[best].Start))
            {
                best = i;
            }
        }

        return best;
    }

    internal static int ZeroWidth(string digits) => digits.Length > 1 && digits[0] == '0' ? digits.Length : 0;

    // ---- Zusammenfassen -------------------------------------------------------------------------------------------------------------------------------

    internal sealed record Learn(long Start, long Step, bool Irregular, bool Constant, int Width);

    internal sealed record GroupInfo(int Start, int End, int Count, string Separator);

    internal sealed record Run(int Index, int Period, int Repeats);

    private static Dictionary<int, Learn> MapKeys(Dictionary<int, Learn> source, Func<int, int> map)
    {
        var result = new Dictionary<int, Learn>();
        foreach (var (offset, learn) in source)
        {
            var mapped = map(offset);
            if (mapped >= 0)
            {
                result[mapped] = learn;
            }
        }

        return result;
    }

    private static List<GroupInfo> MapGroups(List<GroupInfo> source, Func<int, int> map)
    {
        var result = new List<GroupInfo>();
        foreach (var g in source)
        {
            var start = map(g.Start);
            var last = map(g.End - 1);
            if (start >= 0 && last >= 0)
            {
                result.Add(g with { Start = start, End = last + 1 });
            }
        }

        return result;
    }

    /// <summary>Entfernt die Bereiche und liefert eine Abbildung alter auf neue Positionen; -1 für Positionen in einem entfernten Bereich.</summary>
    private static (string Text, Func<int, int> Map) ApplyRemovals(string text, List<(int Start, int End)> removals)
    {
        var merged = new List<(int Start, int End)>();
        foreach (var r in removals.Where(r => r.End > r.Start).OrderBy(r => r.Start))
        {
            if (merged.Count > 0 && r.Start <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, r.End));
            }
            else
            {
                merged.Add(r);
            }
        }

        var sb = new StringBuilder();
        var pos = 0;
        foreach (var (start, end) in merged)
        {
            sb.Append(text, pos, start - pos);
            pos = end;
        }

        sb.Append(text, pos, text.Length - pos);

        int Map(int offset)
        {
            var shift = 0;
            foreach (var (start, end) in merged)
            {
                if (offset >= start && offset < end)
                {
                    return -1;
                }

                if (end <= offset)
                {
                    shift += end - start;
                }
            }

            return offset - shift;
        }

        return (sb.ToString(), Map);
    }

    private sealed class CollapsePass
    {
        private readonly string _text;
        private readonly List<ScopeToken> _tokens;
        private readonly List<ScriptNode> _roots;
        private readonly List<Candidate> _digits;

        public CollapsePass(string text)
        {
            _text = text;
            _tokens = ScopeLexer.Tokenize(text);
            _roots = ScriptTree.Parse(_tokens, text.Length);
            _digits = DigitScanner.Scan(text, _tokens);
        }

        public List<(int Start, int End)> Removals { get; } = new();

        public Dictionary<int, Learn> Learned { get; } = new();

        public List<GroupInfo> Groups { get; } = new();

        public int MaxRepeats { get; private set; }

        public void RunNested() => Visit(_roots, detectHere: false);

        public void RunTop() => ProcessList(_roots, nested: false);

        private void Visit(List<ScriptNode> nodes, bool detectHere)
        {
            foreach (var node in nodes)
            {
                if (node.Children is not null)
                {
                    Visit(node.Children, detectHere: true);
                }
            }

            if (detectHere)
            {
                ProcessList(nodes, nested: true);
            }
        }

        private void ProcessList(List<ScriptNode> nodes, bool nested)
        {
            if (nodes.Count < 2)
            {
                return;
            }

            var signatures = nodes.Select(Signature).ToArray();
            foreach (var run in FindRuns(nodes, signatures, nested ? 1 : nodes.Count / 2))
            {
                Record(nodes, run, nested);
            }
        }

        private static List<Run> FindRuns(List<ScriptNode> nodes, string[] signatures, int maxPeriod)
        {
            var runs = new List<Run>();
            var i = 0;
            while (i < nodes.Count)
            {
                Run? found = null;
                for (var p = 1; p <= maxPeriod && i + 2 * p <= nodes.Count; p++)
                {
                    var repeats = 1;
                    while (i + (repeats + 1) * p <= nodes.Count && PeriodEqual(signatures, i, i + repeats * p, p))
                    {
                        repeats++;
                    }

                    if (repeats >= 2 && nodes.Skip(i).Take(p).Any(n => n.IsBlock))
                    {
                        found = new Run(i, p, repeats);
                        break;
                    }
                }

                if (found is null)
                {
                    i++;
                    continue;
                }

                runs.Add(found);
                i += found.Period * found.Repeats;
            }

            return runs;
        }

        private static bool PeriodEqual(string[] signatures, int a, int b, int length)
        {
            for (var k = 0; k < length; k++)
            {
                if (signatures[a + k] != signatures[b + k])
                {
                    return false;
                }
            }

            return true;
        }

        private void Record(List<ScriptNode> nodes, Run run, bool nested)
        {
            var lastOfFirst = nodes[run.Index + run.Period - 1];
            var lastNode = nodes[run.Index + run.Period * run.Repeats - 1];
            var removeStart = Math.Min(lastOfFirst.End, _text.Length);
            var removeEnd = Math.Min(lastNode.End, _text.Length);
            Removals.Add((removeStart, removeEnd));

            if (!nested)
            {
                MaxRepeats = Math.Max(MaxRepeats, run.Repeats);
            }

            if (nested)
            {
                var body = nodes[run.Index];
                var next = nodes[run.Index + 1];
                var separator = _text.Substring(Math.Min(body.End, _text.Length), Math.Max(0, Math.Min(next.Start, _text.Length) - Math.Min(body.End, _text.Length)));
                Groups.Add(new GroupInfo(body.Start, Math.Min(body.End, _text.Length), run.Repeats, separator));
            }

            var perRepeat = new List<List<Candidate>>();
            for (var k = 0; k < run.Repeats; k++)
            {
                var list = new List<Candidate>();
                for (var n = 0; n < run.Period; n++)
                {
                    var node = nodes[run.Index + k * run.Period + n];
                    list.AddRange(DigitsIn(node.Start, node.End));
                }

                perRepeat.Add(list);
            }

            var count = perRepeat.Min(l => l.Count);
            if (perRepeat.Any(l => l.Count != count))
            {
                return;
            }

            for (var m = 0; m < count; m++)
            {
                var samples = perRepeat.Select(l => l[m]).ToList();
                var learn = LearnSequence(samples);
                if (learn is not null)
                {
                    Learned[samples[0].Offset] = learn;
                }
            }
        }

        private static Learn? LearnSequence(List<Candidate> samples)
        {
            var values = samples.Select(s => long.Parse(s.Digits, CultureInfo.InvariantCulture)).ToList();
            var width = ZeroWidth(samples[0].Digits);
            if (samples.All(s => s.Digits == samples[0].Digits))
            {
                return new Learn(values[0], 0, false, true, width);
            }

            var step = values[1] - values[0];
            for (var k = 2; k < values.Count; k++)
            {
                if (values[k] - values[k - 1] != step)
                {
                    return new Learn(values[0], 0, true, false, width);
                }
            }

            return step == 0 ? new Learn(values[0], 0, false, true, width) : new Learn(values[0], step, false, false, width);
        }

        private IEnumerable<Candidate> DigitsIn(int start, int end)
        {
            var lo = 0;
            var hi = _digits.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (_digits[mid].Offset < start)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            for (var i = lo; i < _digits.Count && _digits[i].Offset < end; i++)
            {
                yield return _digits[i];
            }
        }

        private string Signature(ScriptNode node)
        {
            var sb = new StringBuilder();
            var lo = 0;
            var hi = _tokens.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (_tokens[mid].Start < node.Start)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            for (var i = lo; i < _tokens.Count && _tokens[i].Start < node.End; i++)
            {
                var token = _tokens[i];
                sb.Append((char)('a' + (int)token.Kind));
                var digitsInRun = false;
                foreach (var ch in token.Text)
                {
                    if (char.IsAsciiDigit(ch))
                    {
                        if (!digitsInRun)
                        {
                            sb.Append('#');
                        }

                        digitsInRun = true;
                    }
                    else
                    {
                        digitsInRun = false;
                        sb.Append(ch);
                    }
                }

                sb.Append('');
            }

            return sb.ToString();
        }
    }
}

/// <summary>
/// Ein Ziffernblock in einem Wort oder Text, der als Zählpunkt in Frage kommt. <paramref name="RunCount"/> ist die Zahl der Ziffernblöcke im ganzen Wort,
/// <paramref name="StandaloneIndex"/> die Nummer unter den frei stehenden Blöcken (nicht direkt hinter einem Buchstaben wie in <c>iv2</c>), -1 für die anderen.
/// </summary>
internal sealed record Candidate(
    int Offset,
    string Digits,
    string Prefix,
    PointKind Kind,
    string Context,
    int TokenIndex,
    int RunCount = 1,
    int StandaloneIndex = 0,
    int StandaloneCount = 1,
    int RunIndex = 0,
    bool NameDigit = false,
    string Shape = "",
    RuleVerdict Verdict = RuleVerdict.Open,
    string RuleReason = "");

internal static class DigitScanner
{
    private const int MaxDigits = 15;

    public static List<Candidate> Scan(string text, IReadOnlyList<ScopeToken> tokens, RuleEvaluator? evaluator = null)
    {
        var result = new List<Candidate>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind is not (ScopeTokenKind.Word or ScopeTokenKind.String))
            {
                continue;
            }

            var end = Math.Min(token.End, text.Length);
            var raw = text.Substring(token.Start, end - token.Start);
            var kind = token.Kind == ScopeTokenKind.String ? PointKind.Text : PointKind.Identifier;
            var context = string.Empty;

            if (token.Kind == ScopeTokenKind.Word && raw.All(c => char.IsAsciiDigit(c) || c is '.' or '-'))
            {
                // Reine Zahl. Dezimalzahlen, negative Zahlen und Daten (1444.11.11) zählen nicht.
                if (!raw.All(char.IsAsciiDigit) || raw.Length > MaxDigits)
                {
                    continue;
                }

                kind = PointKind.Number;
                if (i >= 2 && tokens[i - 1].Kind == ScopeTokenKind.Operator && tokens[i - 2].Kind is ScopeTokenKind.Word or ScopeTokenKind.String)
                {
                    context = tokens[i - 2].Text;
                }
            }

            var runs = new List<(int Start, int End)>();
            var pos = 0;
            while (pos < raw.Length)
            {
                if (!char.IsAsciiDigit(raw[pos]))
                {
                    pos++;
                    continue;
                }

                var runStart = pos;
                while (pos < raw.Length && char.IsAsciiDigit(raw[pos]))
                {
                    pos++;
                }

                if (pos - runStart <= MaxDigits)
                {
                    runs.Add((runStart, pos));
                }
            }

            // Direkt hinter einem Buchstaben und mit weiterem Text dahinter (iv2_…, ck3_…) ist die Zahl meist Teil eines Namens, kein Zähler; eine Regel kann das ändern.
            var contentEnd = kind == PointKind.Text && raw.Length > 1 && raw[^1] == '"' ? raw.Length - 1 : raw.Length;
            var contentStart = kind == PointKind.Text && raw.Length > 0 && raw[0] == '"' ? 1 : 0;
            var verdicts = evaluator?.Evaluate(token.Start, raw[contentStart..contentEnd], runs.Select(r => r.Start - contentStart).ToList());
            var nameDigit = runs.Select((r, i2) => (verdicts?[i2].Verdict ?? RuleVerdict.Open) switch
            {
                RuleVerdict.NotCounts => true,
                RuleVerdict.Counts => false,
                _ => r.Start > 0 && char.IsLetter(raw[r.Start - 1]) && r.End < contentEnd,
            }).ToList();

            var standaloneCount = nameDigit.Count(g => !g);
            var standaloneIndex = 0;
            var shape = new StringBuilder();
            var shapeAt = 0;
            foreach (var run in runs)
            {
                shape.Append(raw, shapeAt, run.Start - shapeAt).Append('#');
                shapeAt = run.End;
            }

            shape.Append(raw, shapeAt, raw.Length - shapeAt);
            var shapeText = shape.ToString().Trim('"');

            for (var r = 0; r < runs.Count; r++)
            {
                var index = nameDigit[r] ? -1 : standaloneIndex++;
                result.Add(new Candidate(token.Start + runs[r].Start, raw[runs[r].Start..runs[r].End], raw[..runs[r].Start], kind, context, i, runs.Count, index, standaloneCount, r, nameDigit[r], shapeText, verdicts?[r].Verdict ?? RuleVerdict.Open, verdicts?[r].Rule?.Reason ?? string.Empty));
            }
        }

        return result;
    }
}
