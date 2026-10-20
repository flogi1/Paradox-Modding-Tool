using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Zerlegt eine Datei in Schlüsselbeiträge und Dateirest, je nach Ordnerregel. Wirft nie; Parserfehler stehen in <see cref="FileParts.Error"/>.</summary>
public static class KeyPartReader
{
    private static readonly string[] ScriptExtensions = { ".txt", ".gui", ".gfx" };

    public static FileParts Read(MergeSide side, string relativePath, string fullPath, RuleMatch rule, IReadOnlyCollection<string> keyOperators, ParseCache cache)
    {
        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        FileParts NotScript() => new() { Side = side, RelativePath = relativePath, FullPath = fullPath, Rule = rule, IsScript = false };

        if (rule.Behavior == OverrideBehavior.WholeFile)
        {
            return NotScript();
        }

        if (rule.Behavior == OverrideBehavior.LocalizationKeys)
        {
            return extension == ".yml" ? ReadLocalization(side, relativePath, fullPath, rule, cache) : NotScript();
        }

        if (!ScriptExtensions.Contains(extension))
        {
            return NotScript();
        }

        var parsed = cache.Get(fullPath);
        if (parsed.Root is null)
        {
            return new FileParts { Side = side, RelativePath = relativePath, FullPath = fullPath, Rule = rule, IsScript = true, Error = parsed.Error ?? CompatchTexts.NoteUnreadable, Text = parsed.Text, EncodingName = parsed.EncodingName };
        }

        var result = new FileParts { Side = side, RelativePath = relativePath, FullPath = fullPath, Rule = rule, IsScript = true, Text = parsed.Text, EncodingName = parsed.EncodingName };
        var used = new HashSet<ScriptEntry>(ReferenceEqualityComparer.Instance);
        var constants = parsed.Root.Entries.OfType<ScriptStatement>()
            .Where(s => s.Key.StartsWith('@') && s.Value is ScriptScalar)
            .GroupBy(s => s.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (ScriptScalar)g.Last().Value, StringComparer.Ordinal);
        if (RuleKeys.Applies(rule))
        {
            ReadKeyed(result, parsed.Root, used, constants);
        }
        // GUI-Typen und -Templates: EU V "zuletzt geladen gewinnt", CK III "zuerst geladen gewinnt" (gemessen 2026-09-30); zerlegt wird gleich.
        else if (GuiUnits.Applies(rule, relativePath))
        {
            ReadGui(result, parsed.Root, used, constants);
        }
        else if (rule.Behavior != OverrideBehavior.AppendOnly)
        {
            ReadStatements(result, parsed.Root, keyOperators, used, constants);
        }

        foreach (var entry in parsed.Root.Entries)
        {
            if (entry is not ScriptCommentEntry && !used.Contains(entry))
            {
                result.Rest.Add(entry);
            }
        }

        result.FirstKeyStart = used.Select(e => e.SourceStart).Min();
        return result;
    }

    /// <summary>Legt einen Beitrag an; dateilokale <c>@</c>-Konstanten werden eingesetzt, die Originaleinträge bleiben für die Textstelle.</summary>
    private static KeyPart Make(FileParts result, OverrideOperator op, string key, IReadOnlyList<ScriptEntry> entries, IReadOnlyDictionary<string, ScriptScalar> constants, int? containerEnd = null)
    {
        var inlined = constants.Count == 0 ? entries : entries.Select(e => Inline(e, constants)).ToList();
        var changed = !inlined.SequenceEqual(entries, ReferenceEqualityComparer.Instance);
        return new KeyPart(result.Side, result.RelativePath, result.FullPath, op, EntryAddress.ForKey(result.Rule.Scope, key),
            changed ? inlined : entries, changed ? entries : null, containerEnd);
    }

    private static ScriptEntry Inline(ScriptEntry entry, IReadOnlyDictionary<string, ScriptScalar> constants)
    {
        switch (entry)
        {
            case ScriptStatement s:
                var value = InlineValue(s.Value, constants);
                return ReferenceEquals(value, s.Value)
                    ? s
                    : new ScriptStatement { Key = s.Key, Operator = s.Operator, Value = value, LeadingComments = s.LeadingComments, TrailingComment = s.TrailingComment };
            case ScriptListItem l:
                var item = InlineValue(l.Value, constants);
                return ReferenceEquals(item, l.Value) ? l : new ScriptListItem { Value = item, LeadingComments = l.LeadingComments, TrailingComment = l.TrailingComment };
            default:
                return entry;
        }
    }

    private static ScriptValue InlineValue(ScriptValue value, IReadOnlyDictionary<string, ScriptScalar> constants)
    {
        if (value is ScriptScalar { IsQuoted: false } scalar && scalar.Text.StartsWith('@') && constants.TryGetValue(scalar.Text, out var constant))
        {
            return constant;
        }

        if (value is not ScriptBlock block)
        {
            return value;
        }

        var entries = block.Entries.Select(e => Inline(e, constants)).ToList();
        return entries.SequenceEqual(block.Entries, ReferenceEqualityComparer.Instance) ? block : new ScriptBlock { Entries = entries };
    }

    private static void ReadStatements(FileParts result, ScriptBlock root, IReadOnlyCollection<string> keyOperators, HashSet<ScriptEntry> used, IReadOnlyDictionary<string, ScriptScalar> constants)
    {
        var rule = result.Rule;
        foreach (var statement in root.Entries.OfType<ScriptStatement>())
        {
            // inline_script auf oberster Ebene bindet ein Skript ein (Stellaris-Mods in districts, pop_jobs); was es definiert, steht erst in der
            // eingebundenen Datei. Als Schlüssel "inline_script" gäbe es nur Scheinkonflikte; die Definitionen darin vergleicht PMT nicht.
            if (statement.Key.StartsWith('@') || statement.Key == "inline_script" || (rule.Behavior == OverrideBehavior.FirstWins && statement.Key == "namespace"))
            {
                continue;
            }

            if (rule.Behavior == OverrideBehavior.PartialKeys)
            {
                if (statement.Value is not ScriptBlock block)
                {
                    continue;
                }

                used.Add(statement);
                var parentEnd = statement.SourceEnd is { } end ? end - 1 : (int?)null;
                foreach (var child in block.Entries.OfType<ScriptStatement>().Where(c => c.Value is ScriptScalar))
                {
                    result.Keys.Add(Make(result, OverrideOperator.None, statement.Key + "." + child.Key, new ScriptEntry[] { child }, constants, parentEnd));
                }

                continue;
            }

            var key = rule.Behavior == OverrideBehavior.PerKey
                ? (rule.Operators is { } folderOperators ? OverrideKey.ParseInFolder(statement.Key, folderOperators) : OverrideKey.ParseFor(statement.Key, keyOperators))
                : new OverrideKey(OverrideOperator.None, statement.Key);
            used.Add(statement);
            result.Keys.Add(Make(result, key.Operator, key.Key, new ScriptEntry[] { statement }, constants));
        }
    }

    /// <summary>Schlüssel nach <see cref="RuleKeys"/>; Einträge einer Hülle merken sich deren schließende Klammer für neue Geschwister.</summary>
    private static void ReadKeyed(FileParts result, ScriptBlock root, HashSet<ScriptEntry> used, IReadOnlyDictionary<string, ScriptScalar> constants)
    {
        var keyedStatements = RuleKeys.Read(root, result.Rule).ToList();

        // Eine Hülle mit Werten, die keine Schlüssel sind (HOI IV: economy = { law = yes … }, technologies = { @1936 = 2 … }), bleibt ganz im
        // Dateirest: beim Neuschreiben einzelner Einträge in ihrer Hülle gingen die Werte sonst verloren (Review 2026-10-16).
        var keepWhole = keyedStatements.Where(k => k.WrapperHasValues && k.Wrappers.Count > 0).Select(k => k.Wrappers[0])
            .ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var keyed in keyedStatements.Where(k => k.Wrappers.Count == 0 || !keepWhole.Contains(k.Wrappers[0])))
        {
            // Verbraucht ist der Eintrag der obersten Ebene (die äußere Hülle); neue Geschwister kommen in die innerste.
            used.Add(keyed.Wrappers.Count > 0 ? keyed.Wrappers[0] : keyed.Statement);
            var containerEnd = keyed.Wrapper?.SourceEnd is { } end ? end - 1 : (int?)null;
            result.Keys.Add(Make(result, OverrideOperator.None, keyed.Key, new ScriptEntry[] { keyed.Statement }, constants, containerEnd));
        }
    }

    /// <summary>GUI: Typen und Templates nach <see cref="GuiUnits"/> sind Schlüssel; alles andere ist Dateirest.</summary>
    private static void ReadGui(FileParts result, ScriptBlock root, HashSet<ScriptEntry> used, IReadOnlyDictionary<string, ScriptScalar> constants)
    {
        var read = GuiUnits.Read(root);
        foreach (var unit in read.Consumed)
        {
            used.Add(unit.Entry);
            foreach (var header in unit.Header)
            {
                used.Add(header);
            }
        }

        foreach (var key in read.Keys)
        {
            result.Keys.Add(Make(result, OverrideOperator.None, key.Key, key.Unit.Header.Append(key.Unit.Entry).ToList(), constants, key.ContainerEnd));
        }
    }

    private static FileParts ReadLocalization(MergeSide side, string relativePath, string fullPath, RuleMatch rule, ParseCache cache)
    {
        var participant = new CompatParticipant(side.ToString(), side.ToString(), string.Empty, 0, side == MergeSide.Vanilla);
        var contributions = ContributionExtractor.Extract(participant, relativePath, fullPath, rule, cache, out var error);
        var result = new FileParts { Side = side, RelativePath = relativePath, FullPath = fullPath, Rule = rule, IsScript = true, Error = error };
        foreach (var contribution in contributions)
        {
            var line = new ScriptStatement { Key = contribution.Key, Value = new ScriptScalar { Text = contribution.Text ?? string.Empty } };
            result.Keys.Add(new KeyPart(side, relativePath, fullPath, contribution.Operator, EntryAddress.ForKey(rule.Scope, contribution.Key), new ScriptEntry[] { line }));
        }

        return result;
    }
}
