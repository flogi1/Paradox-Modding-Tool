using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>
/// Richtet A, Ergebnis, B und optional Vanilla nach der Struktur aus statt nach Textzeilen (Nutzer-Feedback: Blöcke, die denselben
/// Kontext regeln, müssen strikt nebeneinanderstehen). Je Blockebene werden die Einheiten wie im Merge zugeordnet
/// (<see cref="EntryUnits"/>, wiederholte Schlüssel wie <c>skill_scaled_modifier</c> über <see cref="SiblingMatcher"/>), mit dem
/// Ergebnis als Bezug. Zugeordnete Blöcke werden Ebene für Ebene weiter ausgerichtet, sonst Zeile für Zeile nur innerhalb der
/// Einheit. Alle Spalten werden dafür einheitlich formatiert geschrieben (eine Anweisung pro Zeile, keine Leerzeilen), damit
/// Formatierungsunterschiede der Quelldateien keine Scheinzeilen erzeugen. Nur für die Anzeige; der Patch behält die Formatierung.
/// </summary>
public static class EntryAligner
{
    private static readonly ScriptWriteOptions Options = new();

    public static IReadOnlyList<AlignedRow> Align(
        IReadOnlyList<ScriptEntry> a,
        IReadOnlyList<ScriptEntry> result,
        IReadOnlyList<ScriptEntry> b,
        IReadOnlyList<ScriptEntry>? vanilla,
        bool hideComments = false)
    {
        var rows = new List<AlignedRow>();
        var context = new Context(rows, vanilla is not null, hideComments);
        AlignLevel(context, a, result, b, vanilla ?? Array.Empty<ScriptEntry>(), 0);
        return rows;
    }

    private sealed record Context(List<AlignedRow> Rows, bool WithVanilla, bool HideComments);

    /// <summary>Eine Einheit einer Seite samt der freistehenden Kommentarzeilen direkt davor.</summary>
    private sealed record Item(EntryUnit Unit, IReadOnlyList<ScriptEntry> Comments);

    private sealed record Level(List<Item> Items, IReadOnlyList<ScriptEntry> TrailingComments);

    private static void AlignLevel(Context context, IReadOnlyList<ScriptEntry> a, IReadOnlyList<ScriptEntry> result, IReadOnlyList<ScriptEntry> b, IReadOnlyList<ScriptEntry> vanilla, int depth)
    {
        var levels = new[] { Split(result), Split(a), Split(b), Split(vanilla) };
        var repeated = SiblingMatcher.RepeatedBaseKeys(levels.Select(l => (IReadOnlyList<EntryUnit>)l.Items.Select(i => i.Unit).ToList()).ToArray());
        var pivot = levels[0].Items.Select(i => i.Unit).ToList();
        var keyed = new Dictionary<string, Item>[4];
        var order = new List<string>[4];
        for (var side = 0; side < 4; side++)
        {
            var items = levels[side].Items;
            var units = side == 0 ? pivot : SiblingMatcher.Align(pivot, items.Select(i => i.Unit).ToList(), repeated).Units;
            keyed[side] = new Dictionary<string, Item>(StringComparer.Ordinal);
            order[side] = new List<string>();
            for (var i = 0; i < items.Count; i++)
            {
                // Gleicher Schlüssel zweimal auf einer Seite kommt nur bei inhaltsgleichen Wiederholungen vor; die zweite bekommt einen
                // eigenen Schlüssel, damit keine Zeile verloren geht.
                var key = units[i].Key;
                while (keyed[side].ContainsKey(key))
                {
                    key += "'";
                }

                keyed[side][key] = items[i] with { Unit = units[i] };
                order[side].Add(key);
            }
        }

        foreach (var key in Weave(order))
        {
            var group = new Item?[4];
            for (var side = 0; side < 4; side++)
            {
                group[side] = keyed[side].TryGetValue(key, out var item) ? item : null;
            }

            EmitGroup(context, group, depth);
        }

        EmitLines(context, levels.Select(l => RenderEntries(l.TrailingComments, depth, context.HideComments)).ToArray());
    }

    /// <summary>Reihenfolge der Schlüssel: die des Ergebnisses, Einheiten nur einer Seite hinter ihrem Vorgänger auf dieser Seite.</summary>
    private static List<string> Weave(List<string>[] order)
    {
        var sequence = new List<string>(order[0]);
        var known = new HashSet<string>(sequence, StringComparer.Ordinal);
        for (var side = 1; side < order.Length; side++)
        {
            var position = -1;
            foreach (var key in order[side])
            {
                if (known.Add(key))
                {
                    sequence.Insert(++position, key);
                }
                else
                {
                    position = sequence.IndexOf(key);
                }
            }
        }

        return sequence;
    }

    private static void EmitGroup(Context context, Item?[] group, int depth)
    {
        var present = group.Where(g => g is not null).Select(g => g!).ToList();
        var comments = group.Select(g => g is null ? string.Empty : RenderEntries(g.Comments, depth, context.HideComments)).ToArray();
        EmitLines(context, comments);

        if (group[0] is not null && present.All(g => g.Unit.Value is ScriptBlock { Entries.Count: > 0 }))
        {
            var open = group.Select(g => g is null ? ((string Before, string Open, string Close)?)null : OpenLines(g.Unit, depth, context.HideComments)).ToArray();
            EmitLines(context, open.Select(o => o is null ? string.Empty : o.Value.Before).ToArray());
            EmitRow(context, open.Select(o => o?.Open).ToArray());
            AlignLevel(context, Children(group[1]), Children(group[0]), Children(group[2]), Children(group[3]), depth + 1);
            EmitRow(context, open.Select(o => o?.Close).ToArray());
            return;
        }

        EmitLines(context, group.Select(g => g is null ? string.Empty : RenderUnit(g.Unit, depth, context.HideComments)).ToArray());
    }

    private static IReadOnlyList<ScriptEntry> Children(Item? item) =>
        item?.Unit.Value is ScriptBlock block ? block.Entries : Array.Empty<ScriptEntry>();

    /// <summary>Eine Zeile je Spalte (Ergebnis, A, B, Vanilla; null = fehlt), eingeordnet wie bei <see cref="ColumnAligner"/>.</summary>
    private static void EmitRow(Context context, string?[] cells)
    {
        var result = cells[0];
        if (result is null)
        {
            context.Rows.Add(new AlignedRow(cells[1], null, cells[2], context.WithVanilla ? cells[3] : null, RowOrigin.Gap));
            return;
        }

        var inA = cells[1] is { } lineA && lineA.Trim() == result.Trim();
        var inB = cells[2] is { } lineB && lineB.Trim() == result.Trim();
        var origin = inA && inB ? RowOrigin.Same : inA ? RowOrigin.FromA : inB ? RowOrigin.FromB : RowOrigin.Other;
        context.Rows.Add(new AlignedRow(cells[1], result, cells[2], context.WithVanilla ? cells[3] : null, origin));
    }

    /// <summary>Texte je Spalte (Ergebnis, A, B, Vanilla) Zeile für Zeile ausgerichtet; nur innerhalb einer Einheit.</summary>
    private static void EmitLines(Context context, string[] texts)
    {
        if (texts.All(t => t.Length == 0))
        {
            return;
        }

        // Einzeilige Einheiten (der häufigste Fall: "cost = 2" gegen "cost = 1") stehen immer in einer Zeile nebeneinander.
        if (texts.All(t => !t.Contains('\n')))
        {
            EmitRow(context, texts.Select(t => t.Length == 0 ? null : t).ToArray());
            return;
        }

        context.Rows.AddRange(ColumnAligner.Align(texts[1], texts[0], texts[2], context.WithVanilla ? texts[3] : null));
    }

    private static Level Split(IReadOnlyList<ScriptEntry> entries)
    {
        var units = EntryUnits.Build(entries);
        var index = new Dictionary<ScriptEntry, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < entries.Count; i++)
        {
            index[entries[i]] = i;
        }

        var items = new List<Item>();
        var next = 0;
        foreach (var unit in units)
        {
            var first = unit.Header.Count > 0 ? index[unit.Header[0]] : index[unit.Entry];
            var comments = new List<ScriptEntry>();
            for (var i = next; i < first; i++)
            {
                if (entries[i] is ScriptCommentEntry)
                {
                    comments.Add(entries[i]);
                }
            }

            items.Add(new Item(unit, comments));
            next = Math.Max(next, index[unit.Entry] + 1);
        }

        var trailing = entries.Skip(next).Where(e => e is ScriptCommentEntry).ToList();
        return new Level(items, trailing);
    }

    private static string RenderEntries(IReadOnlyList<ScriptEntry> entries, int depth, bool hideComments)
    {
        if (entries.Count == 0)
        {
            return string.Empty;
        }

        var text = Indent(ScriptWriter.Write(new ScriptBlock { Entries = entries.ToList() }, Options), depth);
        return hideComments ? ActionText.StripCommentLines(text) : text;
    }

    private static string RenderUnit(EntryUnit unit, int depth, bool hideComments) =>
        RenderEntries(unit.Header.Append(unit.Entry).ToList(), depth, hideComments);

    /// <summary>Kopfzeile eines Blocks ("key = {"), die Kommentarzeilen davor und die schließende Zeile.</summary>
    private static (string Before, string Open, string Close) OpenLines(EntryUnit unit, int depth, bool hideComments)
    {
        var empty = EntryUnits.WithBlockValue(unit.Entry, new ScriptBlock());
        var lines = RenderEntries(unit.Header.Append(empty).ToList(), depth, hideComments: false).Split('\n');
        var last = lines[^1];
        var brace = last.LastIndexOf("{ }", StringComparison.Ordinal);
        var open = brace >= 0 ? last[..(brace + 1)] : last;
        var trailing = string.IsNullOrEmpty(unit.Entry.TrailingComment) ? string.Empty : " # " + unit.Entry.TrailingComment;
        var before = string.Join("\n", lines[..^1]);
        return (hideComments ? ActionText.StripCommentLines(before) : before, open, IndentOf(depth) + "}" + trailing);
    }

    private static string IndentOf(int depth) => new(' ', depth * Options.IndentSize);

    private static string Indent(string text, int depth)
    {
        var lines = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var prefix = IndentOf(depth);
        return string.Join("\n", lines.Select(line => line.Length == 0 ? line : prefix + line));
    }
}
