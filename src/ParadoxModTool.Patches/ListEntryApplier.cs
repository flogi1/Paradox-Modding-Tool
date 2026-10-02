using System.Text.RegularExpressions;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>
/// Listen-Eintrag: fügt je Zeile (und Listenwert) einen Eintrag in eine Liste ein. Das Gedächtnis merkt sich die eigenen Einträge;
/// fremde bleiben immer stehen, von Hand gelöschte eigene werden nicht erneut eingefügt, eigene gehen mit ihrer Zeile.
/// </summary>
public static class ListEntryApplier
{
    private const char Separator = '\u0001';

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    public static void Apply(ListEntryRule rule, RuleRunContext ctx)
    {
        var workspace = ctx.Workspace;
        var target = rule.Target;
        if (workspace.Get(target) is null && rule.CreateCopy && workspace.CreateFromVanilla(target))
        {
            workspace.AddNote(target, PatchTexts.CopyCreated);
        }

        var original = workspace.Get(target);
        var remembered = ctx.OldMemory.ListEntries.TryGetValue(rule.Id, out var list) ? list : new List<ListEntryRecord>();
        var rows = ctx.RowsFor(rule);
        if (original is null)
        {
            foreach (var row in rows)
            {
                ctx.Skipped.Add(new SkippedRow(rule.Id, row.Key, PatchTexts.ListMissing(TemplateRenderer.Render(rule.ListPath, row.Values), target)));
            }

            return;
        }

        var text = original;
        try
        {
            ScriptDocument.Parse(text);
        }
        catch (ScriptParseException ex)
        {
            ctx.Warnings.Add(PatchTexts.FileSkipped(target, ex.Message));
            return;
        }

        var newLine = workspace.NewLineOf(target);
        var records = new List<ListEntryRecord>();
        var desired = new HashSet<(string Path, string Entry)>();
        int added = 0, removed = 0;

        foreach (var row in rows)
        {
            if (ctx.SkipForEmptyPlaceholders(rule, row, rule.ListPath, rule.Entry))
            {
                continue;
            }

            foreach (var instance in TemplateRenderer.RenderEach(rule.ListPath + Separator + rule.Entry, row.Values, ctx.Table.ListColumns))
            {
                var parts = instance.Text.Split(Separator);
                var path = parts[0].Trim();
                var entry = Normalize(parts[1]);
                if (FindList(text, path) is null)
                {
                    if (!ctx.Skipped.Any(s => s.RuleId == rule.Id && s.RowKey == row.Key))
                    {
                        ctx.Skipped.Add(new SkippedRow(rule.Id, row.Key, PatchTexts.ListMissing(path, target)));
                    }

                    continue;
                }

                desired.Add((path, entry));
                var record = new ListEntryRecord(target, path, entry, row.Key);
                var isRemembered = remembered.Any(r => SameEntry(r, record));
                if (FindEntry(text, path, entry) is not null)
                {
                    // Nur eigene Einträge merken: was schon vor dem Rezept in der Liste stand, bleibt fremd und wird nie entfernt.
                    if (isRemembered)
                    {
                        records.Add(record);
                    }
                }
                else if (isRemembered)
                {
                    records.Add(record);
                    workspace.AddNote(target, PatchTexts.UnitDeletedByHand(path + ": " + entry));
                }
                else
                {
                    text = Insert(text, path, entry, newLine);
                    records.Add(record);
                    added++;
                }
            }
        }

        // Einträge übersprungener Zeilen (Zeilenfehler, fehlende Liste) bleiben stehen und im Gedächtnis.
        var skippedRows = ctx.SkippedRowKeys(rule.Id);
        var ownHere = remembered.Where(r => string.Equals(r.File, target, StringComparison.OrdinalIgnoreCase) && !desired.Contains((r.ListPath, r.Entry))).ToList();
        records.AddRange(ownHere.Where(r => skippedRows.Contains(r.RowKey)));
        foreach (var record in ownHere.Where(r => !skippedRows.Contains(r.RowKey)))
        {
            if (FindEntry(text, record.ListPath, record.Entry) is { } span)
            {
                text = text.Remove(span.Start, span.End - span.Start);
                removed++;
            }
        }

        records.AddRange(remembered.Where(r => !string.Equals(r.File, target, StringComparison.OrdinalIgnoreCase)));
        ctx.NewMemory.ListEntries[rule.Id] = records;

        if (text != original)
        {
            workspace.Set(target, text);
        }

        if (added > 0)
        {
            workspace.AddNote(target, PatchTexts.EntriesAdded(added));
        }

        if (removed > 0)
        {
            workspace.AddNote(target, PatchTexts.EntriesRemoved(removed));
        }
    }

    private static bool SameEntry(ListEntryRecord a, ListEntryRecord b) =>
        string.Equals(a.File, b.File, StringComparison.OrdinalIgnoreCase) && a.ListPath == b.ListPath && a.Entry == b.Entry;

    private static string Normalize(string entry)
    {
        var collapsed = Whitespace.Replace(entry.Trim(), " ");
        return Regex.Replace(collapsed, @"\s*=\s*", " = ");
    }

    private static ScriptStatement? FindList(string text, string path)
    {
        var block = ScriptDocument.Parse(text);
        ScriptStatement? statement = null;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            statement = block.Entries.OfType<ScriptStatement>().FirstOrDefault(s => s.Key == segment && s.Value is ScriptBlock);
            if (statement is null)
            {
                return null;
            }

            block = (ScriptBlock)statement.Value;
        }

        return statement;
    }

    private static (int Start, int End)? FindEntry(string text, string path, string entry)
    {
        if (FindList(text, path) is not { Value: ScriptBlock block })
        {
            return null;
        }

        foreach (var child in block.Entries)
        {
            var normalized = child switch
            {
                ScriptListItem { Value: ScriptScalar scalar } => Normalize(scalar.ToString()),
                ScriptStatement { Value: ScriptScalar scalar, Operator: ScriptOperator.Equals } statement => Normalize(statement.Key + " = " + scalar),
                _ => null,
            };
            if (normalized == entry && child is { SourceStart: { } start, SourceEnd: { } end })
            {
                return ScriptSpans.LineExtent(text, start, end);
            }
        }

        return null;
    }

    private static string Insert(string text, string path, string entry, string newLine)
    {
        var list = FindList(text, path)!;
        var block = (ScriptBlock)list.Value;
        var listIndent = ScriptSpans.LineIndent(text, list.SourceStart!.Value);
        var lastChild = block.Entries.LastOrDefault(e => e.SourceStart is not null);
        var indent = lastChild is { SourceStart: { } childStart } && ScriptSpans.LineIndent(text, childStart) is { Length: > 0 } childIndent
            && text[(text.LastIndexOf('\n', Math.Max(0, childStart - 1)) + 1)..childStart].Trim().Length == 0
            ? childIndent
            : listIndent + "\t";
        return ScriptSpans.InsertBeforeBrace(text, list.SourceEnd!.Value - 1, indent + entry, listIndent, newLine);
    }
}
