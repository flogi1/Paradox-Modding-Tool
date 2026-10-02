using System.Text.RegularExpressions;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>
/// Block-Kopie: nimmt je Zeile den Vanilla-Block (optional mit Folgestufen über <c>next_building</c>), setzt Datei-Variablen
/// (<c>@name</c>) ein, bearbeitet ihn und schreibt ihn als Baustein in den erzeugten Bereich des Ziels.
/// </summary>
public static class BlockCopyApplier
{
    private static readonly Regex Variable = new(@"@(?<name>[A-Za-z_][A-Za-z0-9_]*)(?!\s*\[)", RegexOptions.CultureInvariant);

    public static void Apply(BlockCopyRule rule, RuleRunContext ctx)
    {
        var sources = new Dictionary<string, (string Text, ScriptBlock Root)?>(StringComparer.Ordinal);
        var modSources = new Dictionary<string, (string Text, ScriptBlock Root)?>(StringComparer.OrdinalIgnoreCase);
        var units = new List<RegionUnit>();
        var adopted = new Dictionary<string, string>(StringComparer.Ordinal);
        var adoptedSources = new Dictionary<string, List<(string File, string Key)>>(StringComparer.Ordinal);
        var allDefinitions = DuplicateChecker.BuildAllDefinitions(ctx.Workspace, rule.Target, ctx.Recipe.Id, rule.Id);
        var definitions = allDefinitions.ToDictionary(p => p.Key, p => p.Value[0], StringComparer.Ordinal);
        var region = GeneratedRegion.Read(ctx.Workspace.Get(rule.Target) ?? string.Empty, ctx.Recipe.Id, rule.Id);
        var regionKeys = region.Found && !region.Broken ? region.Units.Select(u => u.UnitKey).ToHashSet(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in ctx.RowsFor(rule))
        {
            var key = TemplateRenderer.Render(rule.BlockKey, row.Values).Trim();
            var blocks = new List<string>();
            var order = new List<string>();
            var vanillaBlocks = new Dictionary<string, string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>(new[] { key });
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                order.Add(current);
                if (Find(rule.SourceFiles, current, ctx, sources) is not { } found)
                {
                    continue;
                }

                var blockText = InlineVariables(found.Text, found.Root, found.File, ctx);
                foreach (var edit in rule.Edits.Where(e => e.Enabled))
                {
                    blockText = ApplyEdit(blockText, edit, row.Values);
                }

                blocks.Add(blockText);
                vanillaBlocks[current] = blockText;
                if (rule.FollowNext && found.Statement.Value is ScriptBlock body)
                {
                    foreach (var next in body.Entries.OfType<ScriptStatement>().Where(s => s.Key == "next_building" && s.Value is ScriptScalar))
                    {
                        queue.Enqueue(((ScriptScalar)next.Value).Text);
                    }
                }
            }

            if (blocks.Count == 0)
            {
                ctx.Skipped.Add(new SkippedRow(rule.Id, row.Key, PatchTexts.VanillaBlockMissing(key)));
                continue;
            }

            var duplicate = visited
                .Select(k => definitions.TryGetValue(k, out var hit) ? hit : ((string File, int Line)?)null)
                .FirstOrDefault(d => d is not null);
            if (duplicate is { } hit)
            {
                if (!rule.AdoptExisting)
                {
                    ctx.Skipped.Add(new SkippedRow(rule.Id, row.Key, PatchTexts.AlreadyDefined(hit.File, hit.Line), AlreadyDefined: true));
                    continue;
                }

                string? VanillaBlock(string chainKey)
                {
                    if (!vanillaBlocks.TryGetValue(chainKey, out var text) && Find(rule.SourceFiles, chainKey, ctx, sources) is { } extra)
                    {
                        text = InlineVariables(extra.Text, extra.Root, extra.File, ctx);
                        foreach (var edit in rule.Edits.Where(e => e.Enabled))
                        {
                            text = ApplyEdit(text, edit, row.Values);
                        }

                        vanillaBlocks[chainKey] = text;
                    }

                    return text;
                }

                if (regionKeys.Contains(row.Key))
                {
                    // Steht schon im Bereich und zusätzlich anderswo: übernehmen würde einen der beiden Blöcke verlieren.
                    ctx.Skipped.Add(new SkippedRow(rule.Id, row.Key, PatchTexts.AdoptBlockedRegion(hit.File, hit.Line)));
                    continue;
                }

                var adoption = Adopt(rule, row.Values, order, VanillaBlock, allDefinitions, ctx, modSources);
                if (adoption.Problem is { } problem)
                {
                    ctx.Skipped.Add(new SkippedRow(rule.Id, row.Key, problem));
                    continue;
                }

                adopted[row.Key] = LineRuleApplier.ApplyReplaces(ctx, rule.Target, adoption.Text);
                adoptedSources[row.Key] = adoption.Sources;
            }

            units.Add(new RegionUnit(row.Key, LineRuleApplier.ApplyReplaces(ctx, rule.Target, string.Join("\n\n", blocks))));
        }

        var written = UnitRegionWriter.WriteUnits(ctx, rule.Id, rule.Target, null, units, isLocalization: false, adopted: adopted);

        // Ein Block verlässt seine Datei nur, wenn er im Bereich gelandet ist und das Ziel ohne Konflikt geschrieben wird
        // (Konflikt-Dateien schreibt "Anwenden" nicht; sonst ginge der Block verloren).
        if (written.Written && !written.HasConflicts)
        {
            RemoveAdoptedBlocks(ctx, rule.Target, adoptedSources.Where(p => written.Placed.Contains(p.Key)).SelectMany(p => p.Value).ToList());
        }
    }

    /// <summary>
    /// "Bestehende Blöcke übernehmen": je Stufe der Kette der Block aus der Mod (mit Datei-Variablen und den Bearbeitungen der Regel, also umgebaut),
    /// sonst der Vanilla-Block. Die Kette folgt der Vanilla-Kette und zusätzlich dem eigenen <c>next_building</c> der Mod-Blöcke (eine Mod kann
    /// eigene Stufen anhängen). Ist eine Stufe in der Mod mehrfach definiert oder außerhalb erzeugter Bereiche nicht zu finden, wird nichts
    /// übernommen (<c>Problem</c>), damit kein Block verloren geht.
    /// </summary>
    private static (string Text, List<(string File, string Key)> Sources, string? Problem) Adopt(
        BlockCopyRule rule,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<string> order,
        Func<string, string?> vanillaBlock,
        IReadOnlyDictionary<string, List<(string File, int Line)>> definitions,
        RuleRunContext ctx,
        Dictionary<string, (string Text, ScriptBlock Root)?> modSources)
    {
        var parts = new List<string>();
        var sources = new List<(string File, string Key)>();
        var seen = new HashSet<string>(order, StringComparer.Ordinal);
        var queue = new Queue<string>(order);
        while (queue.Count > 0)
        {
            var chainKey = queue.Dequeue();
            if (definitions.TryGetValue(chainKey, out var places))
            {
                if (places.Count > 1)
                {
                    return (string.Empty, sources, PatchTexts.DefinedSeveralTimes(chainKey, string.Join(", ", places.Select(p => p.File + ":" + p.Line))));
                }

                var definition = places[0];
                if (ModBlock(definition.File, chainKey, ctx, modSources) is not { } own)
                {
                    return (string.Empty, sources, PatchTexts.AlreadyDefined(definition.File, definition.Line));
                }

                if (rule.FollowNext && ScriptSpans.FindTopLevel(own.Root, chainKey)?.Value is ScriptBlock ownBody)
                {
                    foreach (var next in ownBody.Entries.OfType<ScriptStatement>().Where(s => s.Key == "next_building" && s.Value is ScriptScalar))
                    {
                        if (seen.Add(((ScriptScalar)next.Value).Text))
                        {
                            queue.Enqueue(((ScriptScalar)next.Value).Text);
                        }
                    }
                }

                var text = InlineVariables(own.Text, own.Root, definition.File, ctx);
                foreach (var edit in rule.Edits.Where(e => e.Enabled))
                {
                    text = ApplyEdit(text, edit, values, skipExisting: true);
                }

                parts.Add(text);
                sources.Add((definition.File, chainKey));
            }
            else if (vanillaBlock(chainKey) is { } vanilla)
            {
                parts.Add(vanilla);
            }
        }

        return (string.Join("\n\n", parts), sources, null);
    }

    /// <summary>
    /// Der Block eines Schlüssels in einer Mod-Datei außerhalb aller erzeugten Bereiche (Text mit "\n"); <c>Root</c> ist der geparste Dateitext
    /// mit ausgeblendeten Bereichen, die Spannen gelten für den echten Text.
    /// </summary>
    private static (string Text, ScriptBlock Root, int Start, int End)? ModBlock(
        string file, string key, RuleRunContext ctx, Dictionary<string, (string Text, ScriptBlock Root)?> modSources)
    {
        if (!modSources.TryGetValue(file, out var source))
        {
            source = null;
            if (ctx.Workspace.Get(file) is { } text)
            {
                try
                {
                    source = (text, ScriptDocument.Parse(GeneratedRegion.MaskAll(text)));
                }
                catch (ScriptParseException ex)
                {
                    ctx.Warnings.Add(PatchTexts.FileSkipped(file, ex.Message));
                }
            }

            modSources[file] = source;
        }

        if (source is { } s && ScriptSpans.FindTopLevel(s.Root, key) is { SourceStart: { } start, SourceEnd: { } end })
        {
            return (s.Text[start..end].Replace("\r\n", "\n"), s.Root, start, end);
        }

        return null;
    }

    /// <summary>
    /// Entfernt die übernommenen Blöcke (ganze Zeilen) aus ihren Dateien und vermerkt die Anzahl. Die Blöcke werden im aktuellen Text neu gesucht
    /// (außerhalb erzeugter Bereiche), weil das Ziel dieselbe Datei sein kann und inzwischen geschrieben wurde.
    /// </summary>
    private static void RemoveAdoptedBlocks(RuleRunContext ctx, string target, IReadOnlyList<(string File, string Key)> blocks)
    {
        foreach (var group in blocks.GroupBy(b => b.File, StringComparer.OrdinalIgnoreCase))
        {
            var text = ctx.Workspace.Get(group.Key)!;
            var root = ScriptDocument.Parse(GeneratedRegion.MaskAll(text));
            var spans = group.Select(b => b.Key).Distinct(StringComparer.Ordinal)
                .Select(key => ScriptSpans.FindTopLevel(root, key))
                .Where(s => s is { SourceStart: not null, SourceEnd: not null })
                .Select(s => ScriptSpans.LineExtent(text, s!.SourceStart!.Value, s.SourceEnd!.Value))
                .OrderByDescending(s => s.Start)
                .ToList();
            foreach (var (start, end) in spans)
            {
                text = text.Remove(start, end - start);
            }

            ctx.Adopted.AddRange(group.Select(b => b.Key).Distinct(StringComparer.Ordinal).Select(key => new AdoptedBlock(key, group.Key, target)));
            ctx.Workspace.Set(group.Key, text);
            ctx.Workspace.AddNote(group.Key, PatchTexts.BlocksAdopted(spans.Count));
        }
    }

    private static (string Text, ScriptStatement Statement, ScriptBlock Root, string File)? Find(
        string glob, string key, RuleRunContext ctx, Dictionary<string, (string Text, ScriptBlock Root)?> sources)
    {
        foreach (var rel in ctx.Workspace.VanillaFiles(glob))
        {
            if (!sources.TryGetValue(rel, out var source))
            {
                source = null;
                var text = ctx.Workspace.ReadVanilla(rel);
                if (text is not null)
                {
                    text = text.Replace("\r\n", "\n");
                    try
                    {
                        source = (text, ScriptDocument.Parse(text));
                    }
                    catch (ScriptParseException ex)
                    {
                        ctx.Warnings.Add(PatchTexts.FileSkipped(rel, ex.Message));
                    }
                }

                sources[rel] = source;
            }

            if (source is { } s && ScriptSpans.FindTopLevel(s.Root, key) is { SourceStart: { } start, SourceEnd: { } end } statement)
            {
                return (s.Text[start..end], statement, s.Root, rel);
            }
        }

        return null;
    }

    private static string InlineVariables(string blockText, ScriptBlock root, string file, RuleRunContext ctx) =>
        Variable.Replace(blockText, m =>
        {
            var name = "@" + m.Groups["name"].Value;
            var definition = root.Entries.OfType<ScriptStatement>().FirstOrDefault(s => s.Key == name && s.Value is ScriptScalar);
            if (definition is null)
            {
                var warning = PatchTexts.VariableMissing(m.Groups["name"].Value, file);
                if (!ctx.Warnings.Contains(warning))
                {
                    ctx.Warnings.Add(warning);
                }

                return m.Value;
            }

            return ((ScriptScalar)definition.Value).ToString();
        });

    /// <summary>Wendet eine Bearbeitung auf den Text eines einzelnen Blocks <c>schlüssel = { … }</c> an.</summary>
    public static string ApplyEdit(string blockText, BlockEdit edit, IReadOnlyDictionary<string, string> values, bool skipExisting = false)
    {
        var root = ScriptDocument.Parse(blockText);
        if (root.Entries.OfType<ScriptStatement>().FirstOrDefault() is not { Value: ScriptBlock block, SourceStart: { } outerStart, SourceEnd: { } outerEnd })
        {
            return blockText;
        }

        var children = block.Entries.OfType<ScriptStatement>().ToList();
        var childIndent = children.FirstOrDefault(c => c.SourceStart is not null) is { SourceStart: { } firstChild }
            ? LineIndent(blockText, firstChild)
            : LineIndent(blockText, outerStart) + "\t";
        var text = TemplateRenderer.Render(edit.Text, values);
        if (edit.Kind != BlockEditKind.Remove && text.Trim().Length == 0)
        {
            // z. B. {?barony}…{/barony} bei Zeilen ohne Barony: keine leeren Blöcke oder Leerzeilen erzeugen
            return blockText;
        }

        if (skipExisting && edit.Kind != BlockEditKind.Remove)
        {
            // Übernommener Block: Anweisungen, die schon als direktes Kind im Zielblock stehen, nicht ein zweites Mal einfügen; es kommen nur die fehlenden hinzu.
            var container = edit.Kind == BlockEditKind.InsertInto
                ? children.FirstOrDefault(c => c.Key == edit.Key && c.Value is ScriptBlock)?.Value as ScriptBlock
                : block;
            if (container is not null)
            {
                text = MissingStatements(blockText, container, text);
                if (text.Trim().Length == 0)
                {
                    return blockText;
                }
            }
        }

        switch (edit.Kind)
        {
            case BlockEditKind.Remove:
                var spans = children
                    .Where(c => c.Key == edit.Key && (edit.Value is null || (c.Value is ScriptScalar scalar && scalar.Text == edit.Value)))
                    .Where(c => c.SourceStart is not null && c.SourceEnd is not null)
                    .Select(c => ScriptSpans.LineExtent(blockText, c.SourceStart!.Value, c.SourceEnd!.Value))
                    .OrderByDescending(s => s.Start);
                foreach (var (start, end) in spans)
                {
                    blockText = blockText.Remove(start, end - start);
                }

                return blockText;

            case BlockEditKind.InsertInto:
                var target = children.FirstOrDefault(c => c.Key == edit.Key && c.Value is ScriptBlock);
                if (target is { SourceStart: { } targetStart, SourceEnd: { } targetEnd })
                {
                    var inner = target.Value is ScriptBlock innerBlock && innerBlock.Entries.FirstOrDefault(e => e.SourceStart is not null) is { SourceStart: { } innerFirst }
                        ? LineIndent(blockText, innerFirst)
                        : LineIndent(blockText, targetStart) + "\t";
                    return InsertBeforeBrace(blockText, targetEnd - 1, Indent(text, inner), LineIndent(blockText, targetStart));
                }

                return InsertBeforeBrace(blockText, outerEnd - 1, Indent(edit.Key + " = {\n\t" + text.Replace("\n", "\n\t") + "\n}", childIndent), LineIndent(blockText, outerStart));

            case BlockEditKind.InsertAfter:
                var anchor = children.FirstOrDefault(c => c.Key == edit.Key);
                if (anchor is not { SourceStart: { } anchorStart, SourceEnd: { } anchorEnd })
                {
                    return blockText;
                }

                var (_, lineEnd) = ScriptSpans.LineExtent(blockText, anchorStart, anchorEnd);
                var insertAt = lineEnd > anchorEnd ? lineEnd : anchorEnd;
                var lead = lineEnd > anchorEnd ? string.Empty : "\n";
                return blockText.Insert(insertAt, lead + Indent(text, LineIndent(blockText, anchorStart)) + (lineEnd > anchorEnd ? "\n" : string.Empty));

            case BlockEditKind.InsertAtStart:
                var open = blockText.IndexOf('{', outerStart);
                return blockText.Insert(open + 1, "\n" + Indent(text, childIndent));

            case BlockEditKind.InsertAtEnd:
                return InsertBeforeBrace(blockText, outerEnd - 1, Indent(text, childIndent), LineIndent(blockText, outerStart));
        }

        return blockText;
    }

    private static string InsertBeforeBrace(string text, int brace, string indentedText, string braceIndent) =>
        ScriptSpans.InsertBeforeBrace(text, brace, indentedText, braceIndent, "\n");

    private static string Indent(string text, string indent) => ScriptSpans.IndentLines(text, indent, "\n");

    private static string Collapse(string text) => Regex.Replace(text.Trim(), @"\s+", " ");

    /// <summary>
    /// Die Anweisungen von <paramref name="text"/>, die (Leerraum egal) noch kein direktes Kind von <paramref name="container"/> sind, mit ihrem Originaltext, je Zeile eine
    /// Anweisung; leer, wenn alle schon da sind. Lässt sich <paramref name="text"/> nicht lesen, bleibt er unverändert.
    /// </summary>
    private static string MissingStatements(string blockText, ScriptBlock container, string text)
    {
        ScriptBlock inserted;
        try
        {
            inserted = ScriptDocument.Parse(text);
        }
        catch (ScriptParseException)
        {
            return text;
        }

        var existing = container.Entries.OfType<ScriptStatement>()
            .Where(s => s.SourceStart is not null && s.SourceEnd is not null)
            .Select(s => Collapse(blockText[s.SourceStart!.Value..s.SourceEnd!.Value]))
            .ToHashSet(StringComparer.Ordinal);
        var wanted = inserted.Entries.OfType<ScriptStatement>()
            .Where(s => s.SourceStart is not null && s.SourceEnd is not null)
            .Select(s => text[s.SourceStart!.Value..s.SourceEnd!.Value])
            .ToList();
        if (wanted.Count == 0)
        {
            return text;
        }

        return string.Join('\n', wanted.Where(statement => !existing.Contains(Collapse(statement))));
    }

    private static string LineIndent(string text, int position) => ScriptSpans.LineIndent(text, position);
}
