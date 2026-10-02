using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>
/// Gibt übernommenem Text die Formatierung der Quelle zurück (Nutzertest 2026-10-01, CK III resources_top_right_bar). Die Vergleichsspalten
/// zeigen einheitlich formatiert (<see cref="EntryAligner"/>): eine Anweisung pro Zeile, keine Leerzeilen, Kommentare auf Wunsch ausgeblendet.
/// Aus markierten Zeilen entstand so ein Blocktext ohne Kommentare und Leerzeilen, mit <c>size = { 144 70 }</c> über vier Zeilen. Hier wird
/// jede Einheit, die inhaltsgleich in einer Quelle steht, durch den Eintrag der Quelle ersetzt (samt Quelltext, Kommentaren und Leerzeile
/// davor); geänderte Blöcke werden neu zusammengesetzt und ihre Kinder ebenso behandelt. Der Inhalt bleibt dabei genau der übernommene.
/// </summary>
public static class SourceFormat
{
    private static readonly ScriptWriteOptions Options = new() { ReindentVerbatim = true };

    /// <summary>
    /// Der Text in der Formatierung der Quellen; <paramref name="sources"/> in Vorrangreihenfolge (die Spalte, aus der übernommen wurde, zuerst).
    /// Ist der Text nicht lesbar oder weicht das Ergebnis inhaltlich ab, bleibt er unverändert.
    /// </summary>
    public static string Text(string taken, IReadOnlyList<IReadOnlyList<ScriptEntry>> sources)
    {
        IReadOnlyList<ScriptEntry> entries;
        try
        {
            entries = ScriptDocument.Parse(taken).Entries;
        }
        catch (ScriptParseException)
        {
            return taken;
        }

        if (sources.Count == 0 || entries.Count == 0)
        {
            return taken;
        }

        var text = Render(Adopt(entries, sources)).TrimEnd('\n');
        try
        {
            return StructuralEquality.EntriesEqual(ScriptDocument.Parse(text).Entries, entries) ? text : taken;
        }
        catch (ScriptParseException)
        {
            return taken;
        }
    }

    /// <summary>
    /// Text von Einträgen aus <see cref="Adopt"/>: Quelltext wörtlich, aber seine Folgezeilen relativ zur ersten Zeile eingerückt (Tiefe 0, vier
    /// Leerzeichen je Ebene), sonst stünden sie mit der Tiefe ihrer Datei im Text. Zeilenenden ohne Wagenrücklauf.
    /// </summary>
    public static string Render(IReadOnlyList<ScriptEntry> entries) =>
        ScriptWriter.Write(new ScriptBlock { Entries = entries.ToList() }, preferSourceVerbatim: true, Options).Replace("\r\n", "\n");

    /// <summary>Die Einträge von <paramref name="taken"/>, wo möglich durch inhaltsgleiche Einträge der Quellen ersetzt (siehe oben).</summary>
    public static List<ScriptEntry> Adopt(IReadOnlyList<ScriptEntry> taken, IReadOnlyList<IReadOnlyList<ScriptEntry>> sources)
    {
        var units = EntryUnits.Build(taken);
        var sourceUnits = sources.Select(s => (IReadOnlyList<EntryUnit>)EntryUnits.Build(s)).ToList();
        var repeated = SiblingMatcher.RepeatedBaseKeys(sourceUnits.Prepend(units).ToArray());
        var aligned = sourceUnits.Select(s => SiblingMatcher.Align(units, s, repeated).Units).ToList();
        var byEntry = units.ToDictionary(u => u.Entry, ReferenceEqualityComparer.Instance);
        var headers = units.SelectMany(u => u.Header).ToHashSet(ReferenceEqualityComparer.Instance);
        var used = new HashSet<ScriptEntry>(ReferenceEqualityComparer.Instance);

        var result = new List<ScriptEntry>();
        foreach (var entry in taken)
        {
            if (headers.Contains(entry))
            {
                continue;
            }

            result.AddRange(byEntry.TryGetValue(entry, out var unit) ? AdoptUnit(unit, aligned, used) : new[] { entry });
        }

        return result;
    }

    private static IEnumerable<ScriptEntry> AdoptUnit(EntryUnit unit, IReadOnlyList<List<EntryUnit>> aligned, HashSet<ScriptEntry> used)
    {
        var parts = Parts(unit);

        // Inhaltsgleich in einer Quelle, mit zusammenhängendem Quelltext: der Eintrag der Quelle. Zusammengeführte Einträge ohne Quelltext
        // zählen nicht, sonst käme die einheitliche Formatierung zurück.
        foreach (var side in aligned)
        {
            var match = side.Where(u => u.Key == unit.Key)
                .Concat(side.Where(u => u.BaseKey == unit.BaseKey && u.Key != unit.Key))
                .FirstOrDefault(u => !used.Contains(u.Entry) && HasSource(u) && StructuralEquality.EntriesEqual(Parts(u), parts));
            if (match is not null)
            {
                used.Add(match.Entry);
                return Parts(match);
            }
        }

        if (unit.Value is not ScriptBlock block)
        {
            return parts;
        }

        var matches = aligned.Select(side => side.FirstOrDefault(u => u.Key == unit.Key && u.Value is ScriptBlock)).OfType<EntryUnit>().ToList();
        if (matches.Count == 0)
        {
            return parts;
        }

        var first = matches[0];
        var firstBlock = (ScriptBlock)first.Value;
        var children = Adopt(block.Entries, matches.Select(m => (IReadOnlyList<ScriptEntry>)((ScriptBlock)m.Value).Entries).ToList());
        if (block.Entries.Count == 0 || block.Entries[^1] is not ScriptCommentEntry)
        {
            // Kommentare vor der schließenden Klammer (bei ausgeblendeten Kommentaren nicht im übernommenen Text).
            children.AddRange(firstBlock.Entries.Reverse<ScriptEntry>().TakeWhile(e => e is ScriptCommentEntry).Reverse());
        }

        var header = unit.Header.Count > 0 && StructuralEquality.EntriesEqual(first.Header, unit.Header) ? first.Header : unit.Header;
        return header.Append(Rebuilt(unit.Entry, first.Entry, new ScriptBlock { Entries = children }));
    }

    /// <summary>
    /// Neu zusammengesetzter Block: Kommentare der Quelle, wenn der übernommene Text keine hat (ausgeblendet), und Quelltext samt Anfang, damit
    /// die Leerzeile davor bleibt. Ohne Ende, damit der alte Rohtext nicht statt des neuen Inhalts geschrieben wird.
    /// </summary>
    private static ScriptEntry Rebuilt(ScriptEntry taken, ScriptEntry source, ScriptBlock block)
    {
        var comments = taken.LeadingComments.Count > 0 ? taken.LeadingComments : source.LeadingComments;
        var trailing = taken.TrailingComment ?? source.TrailingComment;
        return taken switch
        {
            ScriptStatement s => new ScriptStatement
            {
                Key = s.Key,
                Operator = s.Operator,
                Value = block,
                LeadingComments = comments,
                TrailingComment = trailing,
                SourceText = source.SourceText,
                SourceStart = source.SourceStart,
            },
            _ => new ScriptListItem
            {
                Value = block,
                LeadingComments = comments,
                TrailingComment = trailing,
                SourceText = source.SourceText,
                SourceStart = source.SourceStart,
            },
        };
    }

    private static List<ScriptEntry> Parts(EntryUnit unit) => unit.Header.Append(unit.Entry).ToList();

    /// <summary>Alle Teile der Einheit tragen ihren Quelltext aus derselben Datei (dann schreibt <see cref="ScriptWriter"/> ihn wörtlich).</summary>
    private static bool HasSource(EntryUnit unit)
    {
        var parts = Parts(unit);
        var source = parts[0].SourceText;
        return source is not null && parts.All(p => ReferenceEquals(p.SourceText, source) && p.SourceStart is not null && p.SourceEnd is not null);
    }
}
