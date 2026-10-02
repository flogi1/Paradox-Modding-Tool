using System.Text;
using System.Text.RegularExpressions;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>
/// Wendet eine Zeilen-Regel an: Treffer auskommentieren, löschen, ersetzen oder ganze Blöcke entfernen.
/// Kommentarzeilen und Text hinter einem <c>#</c> werden nie getroffen; ein zweiter Lauf ändert nichts.
/// </summary>
public static class LineRuleApplier
{
    public static void Apply(LineRule rule, RuleRunContext ctx)
    {
        var patterns = Patterns(rule, ctx);
        if (patterns.Count == 0)
        {
            return;
        }

        var regexes = rule.Action == LineAction.RemoveBlock ? new List<Regex>()
            : rule.MatchKind == MatchKind.Text ? new List<Regex> { LinePattern.BuildAny(patterns) }
            : patterns.Select(p => LinePattern.Build(p, rule.MatchKind)).ToList();
        var workspace = ctx.Workspace;

        if (rule.CreateCopy)
        {
            foreach (var rel in workspace.VanillaFiles(rule.Files).Where(rel => workspace.Get(rel) is null))
            {
                var vanilla = workspace.ReadVanilla(rel);
                if (vanilla is not null && (rule.Action == LineAction.RemoveBlock ? HasTopLevel(vanilla, patterns) : HasMatch(vanilla, regexes)))
                {
                    workspace.CreateFromVanilla(rel);
                    workspace.AddNote(rel, PatchTexts.CopyCreated);
                }
            }
        }

        foreach (var rel in workspace.ModFiles(rule.Files))
        {
            if (rule.VanillaPathsOnly && workspace.ReadVanilla(rel) is null)
            {
                // Nur Mod-Kopien von Vanilla-Dateien; eigene Dateien der Mod (z. B. eigene Gebäude-Definitionen) bleiben unberührt.
                continue;
            }

            var text = workspace.Get(rel)!;
            string result;
            int count;
            if (rule.Action == LineAction.RemoveBlock)
            {
                ScriptBlock root;
                try
                {
                    root = ScriptDocument.Parse(text);
                }
                catch (ScriptParseException ex)
                {
                    ctx.Warnings.Add(PatchTexts.FileSkipped(rel, ex.Message));
                    continue;
                }

                (result, count) = RemoveBlocks(text, root, patterns, rule.FollowNext, ctx.Recipe.Id);
            }
            else
            {
                (result, count) = EditLines(text, regexes, rule, workspace.NewLineOf(rel), ctx.Recipe.Id);
            }

            if (count == 0 || result == text)
            {
                continue;
            }

            workspace.Set(rel, result);
            workspace.AddNote(rel, rule.Action switch
            {
                LineAction.Comment => PatchTexts.LinesCommented(count),
                LineAction.Delete => PatchTexts.LinesDeleted(count),
                LineAction.Replace => PatchTexts.Replaced(count),
                _ => PatchTexts.BlocksRemoved(count),
            });
        }
    }

    private static List<string> Patterns(LineRule rule, RuleRunContext ctx, bool record = true) =>
        ctx.RowsFor(rule, record)
            .Where(row => rule.Scope == RuleScope.Global || !(record ? ctx.SkipForEmptyPlaceholders(rule, row, rule.Match) : TemplateRenderer.EmptyPlaceholders(rule.Match, row.Values).Count > 0))
            .SelectMany(row => TemplateRenderer.RenderEach(rule.Match, row.Values, ctx.Table.ListColumns))
            .Select(i => i.Text)
            .Where(p => p.Trim().Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static bool HasMatch(string text, List<Regex> regexes) =>
        SplitLines(text).Any(line => !IsComment(line.Body) && regexes.Any(r => r.IsMatch(line.Body[..ScriptSpans.CodeLength(line.Body)])));

    private static bool HasTopLevel(string text, List<string> keys)
    {
        try
        {
            var root = ScriptDocument.Parse(text);
            return keys.Any(k => ScriptSpans.FindTopLevel(root, k.Trim()) is not null);
        }
        catch (ScriptParseException)
        {
            return false;
        }
    }

    /// <param name="recipeId">Erzeugte Bereiche dieses Rezepts werden übersprungen (sie gehören den Block-Kopie- und Vorlagen-Regeln).</param>
    private static (string Text, int Count) EditLines(string text, List<Regex> regexes, LineRule rule, string newLine, string? recipeId)
    {
        var output = new StringBuilder(text.Length + 64);
        var count = 0;
        var regionPrefix = recipeId is null ? null : "# PMT-Patch " + recipeId + "/";
        var inRegion = false;

        // Ein Treffer, der über mehrere Zeilen geht (Block in Mehrzeilform), lässt sich zeilenweise nicht finden: zuerst diese, danach die Treffer innerhalb einer Zeile.
        if (rule.Action == LineAction.Replace)
        {
            (text, count) = ReplaceAcrossLines(text, regexes, rule.Replacement, regionPrefix);
        }

        foreach (var line in SplitLines(text))
        {
            var body = line.Body;
            var trimmed = body.Trim();
            if (regionPrefix is not null && trimmed.StartsWith(regionPrefix, StringComparison.Ordinal))
            {
                inRegion = trimmed.EndsWith(" begin", StringComparison.Ordinal);
                output.Append(body).Append(line.Ending);
                continue;
            }

            if (inRegion || IsComment(body))
            {
                output.Append(body).Append(line.Ending);
                continue;
            }

            switch (rule.Action)
            {
                case LineAction.Replace:
                    foreach (var regex in regexes)
                    {
                        var code = ScriptSpans.CodeLength(body);
                        var hits = regex.Matches(body[..code]).Count;
                        if (hits > 0)
                        {
                            body = regex.Replace(body[..code], rule.Replacement) + body[code..];
                            count += hits;
                        }
                    }

                    break;

                case LineAction.Delete:
                    while (FirstMatch(body[..ScriptSpans.CodeLength(body)], regexes) is { } match)
                    {
                        body = body[..match.Index] + body[(match.Index + match.Length)..].TrimStart(' ', '\t');
                        count++;
                    }

                    if (body.Trim().Length == 0 && trimmed.Length > 0)
                    {
                        continue;
                    }

                    break;

                default:
                    body = CommentLine(body, regexes, newLine, ref count);
                    break;
            }

            output.Append(body).Append(line.Ending);
        }

        return (output.ToString(), count);
    }

    /// <summary>
    /// Ersetzt die Treffer, die einen Zeilenumbruch enthalten, im ganzen Text. Treffer, die Kommentartext oder einen erzeugten Bereich berühren, bleiben stehen
    /// (wie bei der zeilenweisen Ersetzung); überlappende Treffer zählt der frühere. Ersatztext mit <c>$1</c> wird wie bei <see cref="Regex.Replace(string,string)"/> aufgelöst.
    /// </summary>
    private static (string Text, int Count) ReplaceAcrossLines(string text, List<Regex> regexes, string replacement, string? regionPrefix)
    {
        if (!text.Contains('\n'))
        {
            return (text, 0);
        }

        var inactive = InactiveMask(text, regionPrefix);
        var found = new List<(int Start, int Length, string Text)>();
        foreach (var regex in regexes)
        {
            foreach (Match match in regex.Matches(text))
            {
                if (match.Length > 0
                    && text.AsSpan(match.Index, match.Length).Contains('\n')
                    && !Enumerable.Range(match.Index, match.Length).Any(i => inactive[i]))
                {
                    found.Add((match.Index, match.Length, match.Result(replacement)));
                }
            }
        }

        var accepted = new List<(int Start, int Length, string Text)>();
        foreach (var hit in found.OrderBy(h => h.Start))
        {
            if (accepted.Count == 0 || hit.Start >= accepted[^1].Start + accepted[^1].Length)
            {
                accepted.Add(hit);
            }
        }

        var result = text;
        foreach (var hit in accepted.OrderByDescending(h => h.Start))
        {
            result = result.Remove(hit.Start, hit.Length).Insert(hit.Start, hit.Text);
        }

        return (result, accepted.Count);
    }

    /// <summary>Für jedes Zeichen: liegt es in einer Kommentarzeile, hinter einem <c>#</c> oder in einem erzeugten Bereich des Rezepts?</summary>
    private static bool[] InactiveMask(string text, string? regionPrefix)
    {
        var mask = new bool[text.Length];
        var offset = 0;
        var inRegion = false;
        foreach (var line in SplitLines(text))
        {
            var trimmed = line.Body.Trim();
            var length = line.Body.Length + line.Ending.Length;
            if (regionPrefix is not null && trimmed.StartsWith(regionPrefix, StringComparison.Ordinal))
            {
                inRegion = trimmed.EndsWith(" begin", StringComparison.Ordinal);
                Array.Fill(mask, true, offset, length);
            }
            else if (inRegion || IsComment(line.Body))
            {
                Array.Fill(mask, true, offset, length);
            }
            else
            {
                var code = ScriptSpans.CodeLength(line.Body);
                Array.Fill(mask, true, offset + code, line.Body.Length - code);
            }

            offset += length;
        }

        return mask;
    }

    /// <summary>Frühester nicht leerer Treffer aller Ausdrücke.</summary>
    private static Match? FirstMatch(string code, List<Regex> regexes) =>
        regexes.Select(r => r.Match(code)).Where(m => m.Success && m.Length > 0).OrderBy(m => m.Index).FirstOrDefault();

    /// <summary>
    /// Kommentiert alle Treffer einer Zeile aus. Steht nur der Treffer auf der Zeile, bekommt er ein <c>#</c>; teilt er sie mit anderem Code,
    /// kommt jeder Treffer auf eine eigene Zeile und der übrige Code (z. B. die schließende Klammer) bleibt Code.
    /// </summary>
    private static string CommentLine(string body, List<Regex> regexes, string newLine, ref int count)
    {
        if (FirstMatch(body[..ScriptSpans.CodeLength(body)], regexes) is not { } first)
        {
            return body;
        }

        var before = body[..first.Index];
        var after = body[(first.Index + first.Length)..];
        if (before.Trim().Length == 0 && after[..ScriptSpans.CodeLength(after)].Trim().Length == 0)
        {
            count++;
            return before + "#" + first.Value + after;
        }

        var indent = ScriptSpans.Indent(body);
        var inner = indent;
        var lines = new List<string>();
        if (before.Trim().Length > 0)
        {
            lines.Add(before.TrimEnd());
            if (before.TrimEnd().EndsWith('{'))
            {
                inner = indent + "\t";
            }
        }

        lines.Add(inner + "#" + first.Value);
        count++;
        var rest = after.TrimStart(' ', '\t');
        while (FirstMatch(rest[..ScriptSpans.CodeLength(rest)], regexes) is { } next)
        {
            var between = rest[..next.Index].Trim();
            if (between.Length > 0)
            {
                lines.Add(inner + between);
            }

            lines.Add(inner + "#" + next.Value);
            count++;
            rest = rest[(next.Index + next.Length)..].TrimStart(' ', '\t');
        }

        if (rest.Trim().Length > 0)
        {
            lines.Add((rest.StartsWith('}') ? indent : inner) + rest);
        }

        return string.Join(newLine, lines);
    }

    /// <summary>
    /// Wendet die Ersetzen-Regeln des Rezepts, deren Dateimuster zum Ziel passt, auf einen frisch erzeugten Baustein an.
    /// So bleibt das Ergebnis gleich, egal ob die Ersetzen-Regel vor oder nach der Block-Kopie läuft.
    /// </summary>
    public static string ApplyReplaces(RuleRunContext ctx, string target, string unitText)
    {
        foreach (var rule in ctx.Recipe.Rules.OfType<LineRule>().Where(r => r.Enabled && r.Action == LineAction.Replace && GlobMatcher.IsMatch(r.Files, target)))
        {
            var patterns = Patterns(rule, ctx, record: false);
            if (patterns.Count > 0)
            {
                var regexes = rule.MatchKind == MatchKind.Text ? new List<Regex> { LinePattern.BuildAny(patterns) } : patterns.Select(p => LinePattern.Build(p, rule.MatchKind)).ToList();
                unitText = EditLines(unitText, regexes, rule, "\n", null).Text;
            }
        }

        return unitText;
    }

    private static (string Text, int Count) RemoveBlocks(string text, ScriptBlock root, List<string> keys, bool followNext, string recipeId)
    {
        var spans = new List<(int Start, int End)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(keys.Select(k => k.Trim()));
        var regions = OwnRegions(text, recipeId);
        while (queue.Count > 0)
        {
            var key = queue.Dequeue();
            if (!visited.Add(key))
            {
                continue;
            }

            // Alle Definitionen des Schlüssels außerhalb der eigenen Bereiche, auch doppelte (sonst bliebe die zweite bis zum nächsten Lauf stehen).
            var statements = root.Entries.OfType<ScriptStatement>()
                .Where(s => s.Key == key && s.SourceStart is { } at && s.SourceEnd is not null && !regions.Any(r => at >= r.Start && at < r.End));
            foreach (var statement in statements)
            {
                spans.Add(ScriptSpans.LineExtent(text, statement.SourceStart!.Value, statement.SourceEnd!.Value));
                if (followNext && statement.Value is ScriptBlock block)
                {
                    foreach (var next in block.Entries.OfType<ScriptStatement>().Where(s => s.Key == "next_building" && s.Value is ScriptScalar))
                    {
                        queue.Enqueue(((ScriptScalar)next.Value).Text);
                    }
                }
            }
        }

        var result = text;
        foreach (var (start, end) in spans.OrderByDescending(s => s.Start))
        {
            result = result.Remove(start, end - start);
        }

        return (result, spans.Count);
    }

    /// <summary>Zeichenbereiche der erzeugten Bereiche dieses Rezepts.</summary>
    internal static List<(int Start, int End)> OwnRegions(string text, string recipeId)
    {
        var regions = new List<(int, int)>();
        var prefix = "# PMT-Patch " + recipeId + "/";
        var begin = -1;
        var position = 0;
        foreach (var line in SplitLines(text))
        {
            var trimmed = line.Body.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                if (trimmed.EndsWith(" begin", StringComparison.Ordinal))
                {
                    begin = position;
                }
                else if (begin >= 0)
                {
                    regions.Add((begin, position + line.Body.Length));
                    begin = -1;
                }
            }

            position += line.Body.Length + line.Ending.Length;
        }

        return regions;
    }

    private static bool IsComment(string body) => body.TrimStart(' ', '\t').StartsWith('#');

    private static IEnumerable<(string Body, string Ending)> SplitLines(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var newLine = text.IndexOf('\n', start);
            if (newLine < 0)
            {
                yield return (text[start..], string.Empty);
                yield break;
            }

            var bodyEnd = newLine > start && text[newLine - 1] == '\r' ? newLine - 1 : newLine;
            yield return (text[start..bodyEnd], text[bodyEnd..(newLine + 1)]);
            start = newLine + 1;
        }
    }
}

/// <summary>Baut den regulären Ausdruck einer Zeilen-Regel.</summary>
public static class LinePattern
{
    private const string Operators = "=<>{}!";

    /// <summary>Text: Leerraum an Operatoren optional, zwischen Wörtern mindestens ein Zeichen; nur ganze Wörter. Regex: unverändert.</summary>
    public static Regex Build(string pattern, MatchKind kind)
    {
        if (kind == MatchKind.Regex)
        {
            return new Regex(pattern, RegexOptions.CultureInvariant);
        }

        var tokens = Regex.Split(pattern.Trim(), @"\s+");
        var builder = new StringBuilder();
        if (tokens[0].Length > 0 && IsWordChar(tokens[0][0]))
        {
            builder.Append("(?<![A-Za-z0-9_.])");
        }

        for (var i = 0; i < tokens.Length; i++)
        {
            if (i > 0)
            {
                var operatorNear = Operators.Contains(tokens[i - 1][^1]) || Operators.Contains(tokens[i][0]);
                builder.Append(operatorNear ? @"\s*" : @"\s+");
            }

            builder.Append(SplitAroundOperators(tokens[i]));
        }

        var last = tokens[^1];
        if (last.Length > 0 && IsWordChar(last[^1]))
        {
            builder.Append("(?![A-Za-z0-9_])");
        }

        return new Regex(builder.ToString(), RegexOptions.CultureInvariant);
    }

    /// <summary>Ein Ausdruck für viele Text-Muster (je Tabellenzeile eines), damit jede Zeile nur einmal geprüft wird.</summary>
    public static Regex BuildAny(IReadOnlyList<string> patterns) =>
        new(string.Join("|", patterns.Select(p => "(?:" + Build(p, MatchKind.Text) + ")")), RegexOptions.CultureInvariant);

    /// <summary>Innerhalb eines Tokens wie <c>a=b</c> ist Leerraum um Operatoren ebenfalls optional.</summary>
    private static string SplitAroundOperators(string token)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < token.Length; i++)
        {
            var c = token[i];
            var isOperator = Operators.Contains(c);
            if (isOperator && i > 0)
            {
                builder.Append(@"\s*");
            }

            builder.Append(Regex.Escape(c.ToString()));
            if (isOperator && i < token.Length - 1)
            {
                builder.Append(@"\s*");
            }
        }

        return builder.ToString();
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}

/// <summary>Prüfungen einer Zeilen-Regel für den Regel-Editor.</summary>
public static class LineRuleValidation
{
    public static IReadOnlyList<string> Warnings(LineRule rule)
    {
        var warnings = new List<string>();
        if (rule.Action == LineAction.Replace && rule.Match.Trim().Length > 0)
        {
            var pattern = TemplateRenderer.Render(rule.Match, new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = string.Empty });
            if (LinePattern.Build(pattern, rule.MatchKind).IsMatch(rule.Replacement))
            {
                warnings.Add(PatchTexts.SelfMatch(rule.Name));
            }
        }

        return warnings;
    }
}
