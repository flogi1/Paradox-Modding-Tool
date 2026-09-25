namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Eine für Diff und Merge eigenständig adressierbare Einheit innerhalb eines Blocks. Neben
/// echten Schlüssel/Wert-Anweisungen ("a = b") kennt der Jomini-/GUI-Dialekt Konstrukte ohne
/// Operator: "template name { ... }", "Types X { ... }", "type name = widget { ... }". Der
/// <see cref="ScriptParser"/> zerlegt diese in mehrere <see cref="ScriptListItem"/> (die
/// "Kopfzeilen-Wörter", z. B. "template" und "name") plus einen Listenelement-Block (den
/// "Rumpf"). Ohne besondere Behandlung wären Rümpfe für Diff und Merge unsichtbar - der
/// gesamte Inhalt einer GUI-Datei ginge im Merge verloren. Diese Klasse fasst Kopfzeile + Rumpf
/// zu EINER Einheit zusammen, deren Schlüssel der Kopfzeilentext ist.
/// </summary>
public sealed class EntryUnit
{
    public enum UnitKind
    {
        Statement,
        Body,
        Scalar
    }

    /// <summary>Eindeutiger Schlüssel innerhalb des Blocks (mit Art-Präfix und "#n" bei Wiederholungen).</summary>
    public required string Key { get; init; }

    /// <summary>Lesbarer Schlüssel für Anzeige-Pfade (ohne Art-Präfix, mit "#n" bei Wiederholungen).</summary>
    public required string Display { get; init; }

    /// <summary>Schlüssel ohne "#n" (Art-Präfix plus Schlüssel bzw. Kopfzeile). Gleich für alle Wiederholungen desselben Schlüssels.</summary>
    public string BaseKey { get; init; } = string.Empty;

    /// <summary>Lesbarer Schlüssel ohne "#n".</summary>
    public string BaseDisplay { get; init; } = string.Empty;

    /// <summary>Dieselbe Einheit unter einem anderen Schlüssel (siehe <see cref="SiblingMatcher"/>).</summary>
    public EntryUnit WithKey(string key, string display) => new()
    {
        Key = key,
        Display = display,
        BaseKey = BaseKey,
        BaseDisplay = BaseDisplay,
        Kind = Kind,
        Entry = Entry,
        Header = Header,
    };

    public required UnitKind Kind { get; init; }

    /// <summary>Die Anweisung, der Rumpf-Listeneintrag bzw. das Skalar-Listenelement.</summary>
    public required ScriptEntry Entry { get; init; }

    /// <summary>Kopfzeilen-Einträge (nur bei <see cref="UnitKind.Body"/>), in Quellreihenfolge.</summary>
    public IReadOnlyList<ScriptEntry> Header { get; init; } = Array.Empty<ScriptEntry>();

    public ScriptValue Value => Entry switch
    {
        ScriptStatement s => s.Value,
        ScriptListItem l => l.Value,
        _ => throw new InvalidOperationException("Kommentareinträge bilden keine Einheit.")
    };
}

public static class EntryUnits
{
    /// <summary>
    /// Zerlegt die Einträge eines Blocks in Einheiten (siehe <see cref="EntryUnit"/>). Reine
    /// Kommentareinträge gehören zu keiner Einheit.
    /// </summary>
    public static List<EntryUnit> Build(IReadOnlyList<ScriptEntry> entries)
    {
        var headerOfBodyIndex = new Dictionary<int, List<int>>();
        var consumedAsHeader = new HashSet<int>();

        for (var i = 0; i < entries.Count; i++)
        {
            if (!IsBody(entries[i]))
            {
                continue;
            }

            var header = FindHeader(entries, i);
            headerOfBodyIndex[i] = header;
            foreach (var index in header)
            {
                consumedAsHeader.Add(index);
            }
        }

        var units = new List<EntryUnit>();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            if (entry is ScriptCommentEntry || consumedAsHeader.Contains(i))
            {
                continue;
            }

            EntryUnit.UnitKind kind;
            string baseKey;
            string baseDisplay;
            IReadOnlyList<ScriptEntry> header = Array.Empty<ScriptEntry>();

            switch (entry)
            {
                case ScriptStatement stmt:
                    kind = EntryUnit.UnitKind.Statement;
                    baseKey = "S:" + stmt.Key;
                    baseDisplay = stmt.Key;
                    break;
                case ScriptListItem when headerOfBodyIndex.TryGetValue(i, out var headerIndexes):
                    kind = EntryUnit.UnitKind.Body;
                    header = headerIndexes.Select(index => entries[index]).ToList();
                    baseDisplay = header.Count == 0 ? "{}" : HeaderText(header);
                    baseKey = "B:" + baseDisplay;
                    break;
                case ScriptListItem { Value: ScriptScalar scalar }:
                    kind = EntryUnit.UnitKind.Scalar;
                    baseKey = "L:" + scalar.Text;
                    baseDisplay = scalar.Text;
                    break;
                default:
                    // Unbenannter Listenelement-Block ohne Kopfzeile: kein Bestandteil eines Schlüssels.
                    kind = EntryUnit.UnitKind.Body;
                    baseDisplay = "{}";
                    baseKey = "B:{}";
                    break;
            }

            occurrences.TryGetValue(baseKey, out var count);
            occurrences[baseKey] = count + 1;
            var suffix = count == 0 ? string.Empty : $"#{count}";

            units.Add(new EntryUnit
            {
                Key = baseKey + suffix,
                Display = baseDisplay + suffix,
                BaseKey = baseKey,
                BaseDisplay = baseDisplay,
                Kind = kind,
                Entry = entry,
                Header = header
            });
        }

        return units;
    }

    /// <summary>Neuer Eintrag desselben Typs mit ersetztem Blockwert - bewusst OHNE Quell-Span, damit "Original-Text übernehmen" den neuen Inhalt nicht überspringt.</summary>
    public static ScriptEntry WithBlockValue(ScriptEntry template, ScriptBlock block) => template switch
    {
        ScriptStatement s => new ScriptStatement
        {
            Key = s.Key,
            Operator = s.Operator,
            Value = block,
            LeadingComments = s.LeadingComments,
            TrailingComment = s.TrailingComment
        },
        ScriptListItem l => new ScriptListItem
        {
            Value = block,
            LeadingComments = l.LeadingComments,
            TrailingComment = l.TrailingComment
        },
        _ => template
    };

    public static string HeaderText(IReadOnlyList<ScriptEntry> header) =>
        string.Join(' ', header.Select(HeaderPart));

    private static string HeaderPart(ScriptEntry entry) => entry switch
    {
        ScriptStatement { Value: ScriptScalar v } s => $"{s.Key} = {v}",
        ScriptListItem { Value: ScriptScalar v } => v.ToString() ?? string.Empty,
        _ => string.Empty
    };

    private static bool IsBody(ScriptEntry entry) => entry is ScriptListItem { Value: ScriptBlock };

    /// <summary>
    /// Sucht rückwärts ab einem Rumpf die zugehörigen Kopfzeilen-Einträge: direkt davor
    /// stehende skalare Listenelemente, dazu höchstens eine skalare Anweisung an der
    /// unmittelbaren Position vor dem Rumpf ("type name = widget { ... }").
    /// </summary>
    private static List<int> FindHeader(IReadOnlyList<ScriptEntry> entries, int bodyIndex)
    {
        var header = new List<int>();
        var i = bodyIndex - 1;

        if (i >= 0 && entries[i] is ScriptStatement { Value: ScriptScalar })
        {
            header.Add(i);
            i--;
        }

        while (i >= 0 && entries[i] is ScriptListItem { Value: ScriptScalar })
        {
            header.Add(i);
            i--;
        }

        header.Reverse();
        return header;
    }
}
