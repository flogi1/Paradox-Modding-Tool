using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Desync;

/// <summary>Art einer Feldabweichung in <see cref="FieldDiff"/>.</summary>
public enum FieldChangeKind
{
    /// <summary>Das Feld steht in beiden Spielständen, mit verschiedenem Wert.</summary>
    Changed,

    /// <summary>Das Feld steht nur im Referenz-Spielstand.</summary>
    OnlyReference,

    /// <summary>Das Feld steht nur im abweichenden Spielstand.</summary>
    OnlyDesynced
}

/// <summary>
/// Eine Abweichung in einer Einheit. <paramref name="Path"/> ist relativ zur Einheit (ohne deren eigenen Schlüssel; die Einheit selbst ist ""),
/// Segmente mit "/" getrennt. Mehrfach vorkommende Schlüssel heißen wie im Index (<see cref="SaveIndexer"/>):
/// das erste Vorkommen <c>key</c>, spätere <c>key#2</c>, <c>key#3</c> (immer, unabhängig vom anderen Spielstand).
/// Listeneinträge ohne Schlüssel heißen <c>[n]</c> nach Position (ab 0). Im Zeilen-Rückfall ist der Pfad <c>line n</c>.
/// </summary>
public sealed record FieldDiff(string Path, string? ReferenceValue, string? DesyncedValue, FieldChangeKind Kind);

/// <summary>Ergebnis von <see cref="UnitFieldDiffer.Diff"/>: <paramref name="Truncated"/> bei mehr als <see cref="UnitFieldDiffer.MaxFields"/> Abweichungen.</summary>
public sealed record FieldDiffResult(IReadOnlyList<FieldDiff> Fields, bool Truncated, bool LineFallback);

/// <summary>
/// Vergleicht den Text derselben Einheit in zwei Spielständen feldweise. Beide Texte werden mit dem Skript-Parser aus VanillaDiff
/// geparst; Anweisungen paaren sich nach (Schlüssel, Auftreten) und Blöcke werden rekursiv verglichen. Lässt sich ein Text nicht
/// parsen, vergleicht der Rückfall die Zeilen.
/// </summary>
public static class UnitFieldDiffer
{
    /// <summary>Mehr Abweichungen je Einheit werden nicht mehr aufgelistet (<see cref="FieldDiffResult.Truncated"/>).</summary>
    public const int MaxFields = 200;

    /// <summary>Höchstzahl der Zellen der Zeilen-LCS-Tabelle; darüber vergleicht der Rückfall nur Zeile für Zeile an gleicher Stelle.</summary>
    private const int MaxLcsCells = 4_000_000;

    public static FieldDiffResult Diff(string referenceText, string desyncedText)
    {
        if (string.Equals(referenceText, desyncedText, StringComparison.Ordinal))
        {
            return new FieldDiffResult(Array.Empty<FieldDiff>(), false, false);
        }

        var sink = new Sink();
        ScriptEntry reference;
        ScriptEntry desynced;
        try
        {
            reference = UnitEntry(ScriptDocument.Parse(referenceText));
            desynced = UnitEntry(ScriptDocument.Parse(desyncedText));
        }
        catch (ScriptParseException)
        {
            LineDiff(referenceText, desyncedText, sink);
            return new FieldDiffResult(sink.Fields, sink.Truncated, true);
        }

        DiffEntries(string.Empty, reference, desynced, sink);
        return new FieldDiffResult(sink.Fields, sink.Truncated, false);
    }

    /// <summary>Sammelt Abweichungen bis <see cref="MaxFields"/>; jede weitere setzt nur das Abgeschnitten-Zeichen.</summary>
    private sealed class Sink
    {
        public List<FieldDiff> Fields { get; } = new();

        public bool Truncated { get; private set; }

        public bool Full => Truncated;

        public void Add(FieldDiff diff)
        {
            if (Fields.Count >= MaxFields)
            {
                Truncated = true;
                return;
            }

            Fields.Add(diff);
        }
    }

    /// <summary>
    /// Der Eintrag der Einheit: bei genau einer Anweisung (oder einem Listeneintrag) auf oberster Ebene diese selbst, sonst die Wurzel als Listeneintrag.
    /// Sein Schlüssel gehört nicht zum Pfad, sein Operator zum Vergleich.
    /// </summary>
    private static ScriptEntry UnitEntry(ScriptBlock root)
    {
        var entries = root.Entries.Where(e => e is not ScriptCommentEntry).ToList();
        return entries.Count == 1 && entries[0] is ScriptStatement or ScriptListItem
            ? entries[0]
            : new ScriptListItem { Value = root };
    }

    private static void DiffValues(string path, ScriptValue reference, ScriptValue desynced, Sink sink)
    {
        if (sink.Full)
        {
            return;
        }

        if (reference is ScriptBlock referenceBlock && desynced is ScriptBlock desyncedBlock)
        {
            DiffBlocks(path, referenceBlock, desyncedBlock, sink);
            return;
        }

        var referenceText = SiblingMatcher.Canonical(reference);
        var desyncedText = SiblingMatcher.Canonical(desynced);
        if (!string.Equals(referenceText, desyncedText, StringComparison.Ordinal))
        {
            sink.Add(new FieldDiff(path, referenceText, desyncedText, FieldChangeKind.Changed));
        }
    }

    private static void DiffBlocks(string path, ScriptBlock reference, ScriptBlock desynced, Sink sink)
    {
        var referenceEntries = reference.Entries.Where(e => e is not ScriptCommentEntry).ToList();
        var desyncedEntries = desynced.Entries.Where(e => e is not ScriptCommentEntry).ToList();

        // Eine Liste aus lauter Skalaren ist ein einziger Wert.
        if (IsScalarList(referenceEntries) && IsScalarList(desyncedEntries))
        {
            var referenceText = JoinScalars(referenceEntries);
            var desyncedText = JoinScalars(desyncedEntries);
            if (!string.Equals(referenceText, desyncedText, StringComparison.Ordinal))
            {
                sink.Add(new FieldDiff(path, referenceText, desyncedText, FieldChangeKind.Changed));
            }

            return;
        }

        var referenceSlots = Slots(referenceEntries);
        var desyncedSlots = Slots(desyncedEntries);
        var desyncedByIdentity = desyncedSlots.ToDictionary(s => (s.Key, s.Occurrence));
        var paired = new HashSet<(string?, int)>();
        foreach (var slot in referenceSlots)
        {
            if (sink.Full)
            {
                return;
            }

            var slotPath = Join(path, SegmentOf(slot));
            if (!desyncedByIdentity.TryGetValue((slot.Key, slot.Occurrence), out var partner))
            {
                sink.Add(new FieldDiff(slotPath, Show(slot.Entry), null, FieldChangeKind.OnlyReference));
                continue;
            }

            paired.Add((slot.Key, slot.Occurrence));
            DiffEntries(slotPath, slot.Entry, partner.Entry, sink);
        }

        foreach (var slot in desyncedSlots)
        {
            if (sink.Full)
            {
                return;
            }

            if (!paired.Contains((slot.Key, slot.Occurrence)))
            {
                sink.Add(new FieldDiff(Join(path, SegmentOf(slot)), null, Show(slot.Entry), FieldChangeKind.OnlyDesynced));
            }
        }
    }

    private static void DiffEntries(string path, ScriptEntry reference, ScriptEntry desynced, Sink sink)
    {
        if (reference is ScriptStatement referenceStatement && desynced is ScriptStatement desyncedStatement
            && referenceStatement.Operator != desyncedStatement.Operator)
        {
            // Anderer Operator: ein Wertwechsel, mit dem Operator in der Anzeige.
            sink.Add(new FieldDiff(path, WithOperator(referenceStatement), WithOperator(desyncedStatement), FieldChangeKind.Changed));
            return;
        }

        DiffValues(path, ValueOf(reference), ValueOf(desynced), sink);
    }

    /// <summary>Ein Eintrag mit seinem Paarungsschlüssel: Anweisungen (Schlüssel, Auftreten), Listeneinträge (null, Position).</summary>
    private sealed record Slot(string? Key, int Occurrence, ScriptEntry Entry);

    private static List<Slot> Slots(List<ScriptEntry> entries)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var items = 0;
        var slots = new List<Slot>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry is ScriptStatement statement)
            {
                seen.TryGetValue(statement.Key, out var occurrence);
                seen[statement.Key] = occurrence + 1;
                slots.Add(new Slot(statement.Key, occurrence, entry));
            }
            else
            {
                slots.Add(new Slot(null, items++, entry));
            }
        }

        return slots;
    }

    private static string SegmentOf(Slot slot) => slot.Key switch
    {
        null => $"[{slot.Occurrence}]",
        var key when slot.Occurrence == 0 => key,
        var key => $"{key}#{slot.Occurrence + 1}",
    };

    private static string Join(string path, string segment) => path.Length == 0 ? segment : path + "/" + segment;

    private static bool IsScalarList(List<ScriptEntry> entries) =>
        entries.All(e => e is ScriptListItem { Value: ScriptScalar });

    private static string JoinScalars(List<ScriptEntry> entries) =>
        string.Join(" ", entries.Select(e => SiblingMatcher.Canonical(((ScriptListItem)e).Value)));

    private static ScriptValue ValueOf(ScriptEntry entry) => entry switch
    {
        ScriptStatement s => s.Value,
        ScriptListItem l => l.Value,
        _ => new ScriptBlock(),
    };

    private static string Show(ScriptEntry entry) => SiblingMatcher.Canonical(ValueOf(entry));

    private static string WithOperator(ScriptStatement statement) =>
        SiblingMatcher.Canonical(statement)[statement.Key.Length..];

    // ---- Zeilen-Rückfall ----

    private static void LineDiff(string referenceText, string desyncedText, Sink sink)
    {
        var reference = Lines(referenceText);
        var desynced = Lines(desyncedText);

        var lead = 0;
        while (lead < reference.Length && lead < desynced.Length && reference[lead] == desynced[lead])
        {
            lead++;
        }

        var trail = 0;
        while (trail < reference.Length - lead && trail < desynced.Length - lead
            && reference[^(trail + 1)] == desynced[^(trail + 1)])
        {
            trail++;
        }

        var n = reference.Length - lead - trail;
        var m = desynced.Length - lead - trail;
        if ((long)(n + 1) * (m + 1) > MaxLcsCells)
        {
            // Zu groß für die Tabelle: nur Zeile für Zeile an gleicher Stelle vergleichen.
            for (var i = 0; i < Math.Max(n, m) && !sink.Full; i++)
            {
                Unmatched(i < n ? lead + i : -1, i < m ? lead + i : -1, reference, desynced, sink);
            }

            return;
        }

        // LCS-Längen von hinten, damit der Weg von vorn gelesen werden kann.
        var width = m + 1;
        var table = new int[(n + 1) * width];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                table[i * width + j] = reference[lead + i] == desynced[lead + j]
                    ? table[(i + 1) * width + j + 1] + 1
                    : Math.Max(table[(i + 1) * width + j], table[i * width + j + 1]);
            }
        }

        var a = 0;
        var b = 0;
        var removed = new List<int>();
        var added = new List<int>();
        while (a < n || b < m)
        {
            if (a < n && b < m && reference[lead + a] == desynced[lead + b])
            {
                FlushHunk(removed, added, reference, desynced, sink);
                a++;
                b++;
            }
            else if (b >= m || (a < n && table[(a + 1) * width + b] >= table[a * width + b + 1]))
            {
                removed.Add(lead + a++);
            }
            else
            {
                added.Add(lead + b++);
            }

            if (sink.Full)
            {
                return;
            }
        }

        FlushHunk(removed, added, reference, desynced, sink);
    }

    /// <summary>Gleichlange Reihen entfernter und hinzugekommener Zeilen paaren sich der Reihe nach; der Rest steht allein.</summary>
    private static void FlushHunk(List<int> removed, List<int> added, string[] reference, string[] desynced, Sink sink)
    {
        for (var i = 0; i < Math.Max(removed.Count, added.Count); i++)
        {
            Unmatched(i < removed.Count ? removed[i] : -1, i < added.Count ? added[i] : -1, reference, desynced, sink);
        }

        removed.Clear();
        added.Clear();
    }

    /// <summary>Eine abweichende Zeile oder ein Zeilenpaar; -1 heißt: auf dieser Seite gibt es keine Zeile. Der Pfad nennt die Zeilennummer (ab 1) der Referenz, sonst die des abweichenden Stands.</summary>
    private static void Unmatched(int referenceIndex, int desyncedIndex, string[] reference, string[] desynced, Sink sink)
    {
        if (referenceIndex >= 0 && desyncedIndex >= 0)
        {
            sink.Add(new FieldDiff($"line {referenceIndex + 1}", reference[referenceIndex], desynced[desyncedIndex], FieldChangeKind.Changed));
        }
        else if (referenceIndex >= 0)
        {
            sink.Add(new FieldDiff($"line {referenceIndex + 1}", reference[referenceIndex], null, FieldChangeKind.OnlyReference));
        }
        else if (desyncedIndex >= 0)
        {
            sink.Add(new FieldDiff($"line {desyncedIndex + 1}", null, desynced[desyncedIndex], FieldChangeKind.OnlyDesynced));
        }
    }

    /// <summary>Zeilen ohne Einrückung und Zeilenende; ein abschließender Zeilenumbruch erzeugt keine Leerzeile.</summary>
    private static string[] Lines(string text)
    {
        var lines = text.Split('\n').Select(l => l.Trim()).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines.ToArray();
    }
}
