using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Strukturvergleich wie <see cref="ScriptStructuralComparer"/>, aber Kommentareinträge zählen nicht.</summary>
public static class StructuralEquality
{
    public static bool Equal(ScriptValue? a, ScriptValue? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a is ScriptScalar sa && b is ScriptScalar sb)
        {
            return sa.IsQuoted == sb.IsQuoted && string.Equals(sa.Text, sb.Text, StringComparison.Ordinal);
        }

        return a is ScriptBlock ba && b is ScriptBlock bb && EntriesEqual(ba.Entries, bb.Entries);
    }

    public static bool EntryEqual(ScriptEntry a, ScriptEntry b) => (a, b) switch
    {
        (ScriptStatement sa, ScriptStatement sb) => string.Equals(sa.Key, sb.Key, StringComparison.Ordinal) && sa.Operator == sb.Operator && Equal(sa.Value, sb.Value),
        (ScriptListItem la, ScriptListItem lb) => Equal(la.Value, lb.Value),
        _ => false,
    };

    public static bool EntriesEqual(IReadOnlyList<ScriptEntry> a, IReadOnlyList<ScriptEntry> b)
    {
        var left = a.Where(e => e is not ScriptCommentEntry).ToList();
        var right = b.Where(e => e is not ScriptCommentEntry).ToList();
        return left.Count == right.Count && left.Zip(right).All(pair => EntryEqual(pair.First, pair.Second));
    }

    /// <summary>
    /// Gleich bis auf die Reihenfolge von Geschwistern, wo sie nicht zählt. Grundlage für "nicht nötig": Das Spiel hängt Injektionen in
    /// Ladereihenfolge an, der Merge webt sie in Einfügereihenfolge ein; beides ergibt dieselbe Wirkung. In Blöcken mit
    /// <c>if</c>/<c>else_if</c>/<c>else</c> oder wiederholten Schlüsseln zählt die Reihenfolge dagegen (Prüfbefund I6).
    /// </summary>
    public static bool Equivalent(IReadOnlyList<ScriptEntry> a, IReadOnlyList<ScriptEntry> b) =>
        Unordered(a).SequenceEqual(Unordered(b), StringComparer.Ordinal);

    private static readonly HashSet<string> ControlKeys = new(StringComparer.Ordinal) { "if", "else_if", "else" };

    /// <summary>Textformen der Einträge (ohne Kommentare); sortiert, außer die Reihenfolge zählt in diesem Block.</summary>
    public static List<string> Unordered(IReadOnlyList<ScriptEntry> entries)
    {
        var list = entries.Where(e => e is not ScriptCommentEntry).ToList();
        var texts = list.Select(UnorderedCanonical).ToList();
        return OrderMatters(list) ? texts : texts.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    /// <summary>Reihenfolge zählt bei Kontrollfluss-Schlüsseln und bei wiederholten Schlüsseln (die Position unterscheidet sie).</summary>
    public static bool OrderMatters(IReadOnlyList<ScriptEntry> entries)
    {
        var keys = entries.OfType<ScriptStatement>().Select(s => s.Key).ToList();
        return keys.Any(ControlKeys.Contains) || keys.Count != keys.Distinct(StringComparer.Ordinal).Count();
    }

    private static string UnorderedCanonical(ScriptEntry entry) => entry switch
    {
        ScriptStatement s => s.Key + (s.Operator == ScriptOperator.Equals ? "=" : "~" + (int)s.Operator + "~") + UnorderedCanonical(s.Value),
        ScriptListItem l => UnorderedCanonical(l.Value),
        _ => string.Empty,
    };

    private static string UnorderedCanonical(ScriptValue value) => value switch
    {
        ScriptBlock block => "{" + string.Join(" ", Unordered(block.Entries)) + "}",
        _ => SiblingMatcher.Canonical(value),
    };
}
