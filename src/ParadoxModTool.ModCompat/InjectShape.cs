using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModCompat;

/// <summary>Was <c>INJECT:</c> enthalten muss, damit aus einem Block ohne Patch der gewünschte Block entsteht (<see cref="InjectShape.Of"/>).</summary>
/// <param name="Entries">Die geänderten und die neuen Einträge, in dieser Reihenfolge.</param>
/// <param name="Changed">Anzahl der Einträge, die einen vorhandenen Teilschlüssel überschreiben.</param>
public sealed record InjectShape(IReadOnlyList<ScriptEntry> Entries, int Changed)
{
    /// <summary>
    /// Die Form, wenn der gewünschte Block aus dem vorhandenen entsteht, indem einfache Teilschlüssel einen neuen Wert bekommen (gleiche
    /// Stelle, gleicher Operator, Wert ein Skalar, Schlüssel nur einmal) und danach neue Einträge folgen; sonst <see langword="null"/>.
    /// <c>INJECT:</c> überschreibt einen vorhandenen Teilschlüssel an seiner Stelle (gemessen in EU V) und hängt neue hinten an, deshalb nur
    /// dann (Prüfbefund I5: ein eingefügtes <c>else_if</c> vor <c>else</c> darf nicht hinter <c>else</c> landen). Entfernte oder umgestellte
    /// Einträge, geänderte Unterblöcke und ein neuer Eintrag mit schon vorhandenem Schlüssel gehen nicht. Kommentareinträge zählen nicht.
    /// </summary>
    /// <param name="entryEqual">Strukturvergleich zweier Einträge.</param>
    public static InjectShape? Of(IReadOnlyList<ScriptEntry> have, IReadOnlyList<ScriptEntry> want, Func<ScriptEntry, ScriptEntry, bool> entryEqual)
    {
        var haveEntries = have.Where(e => e is not ScriptCommentEntry).ToList();
        var wantEntries = want.Where(e => e is not ScriptCommentEntry).ToList();
        if (wantEntries.Count < haveEntries.Count)
        {
            return null;
        }

        var inject = new List<ScriptEntry>();
        for (var i = 0; i < haveEntries.Count; i++)
        {
            if (entryEqual(haveEntries[i], wantEntries[i]))
            {
                continue;
            }

            if (!IsChangedScalar(haveEntries[i], wantEntries[i], haveEntries, wantEntries))
            {
                return null;
            }

            inject.Add(wantEntries[i]);
        }

        var changed = inject.Count;
        var extras = wantEntries.Skip(haveEntries.Count).ToList();
        var haveKeys = EntryUnits.Build(haveEntries).Select(u => u.BaseKey).ToHashSet(StringComparer.Ordinal);
        if (EntryUnits.Build(extras).Any(u => haveKeys.Contains(u.BaseKey)))
        {
            return null;
        }

        inject.AddRange(extras);
        return inject.Count == 0 ? null : new InjectShape(inject, changed);
    }

    private static bool IsChangedScalar(ScriptEntry have, ScriptEntry want, IReadOnlyList<ScriptEntry> haveEntries, IReadOnlyList<ScriptEntry> wantEntries) =>
        have is ScriptStatement { Value: ScriptScalar } h
        && want is ScriptStatement { Value: ScriptScalar } w
        && h.Key == w.Key
        && h.Operator == w.Operator
        && haveEntries.Count(e => e is ScriptStatement s && s.Key == h.Key) == 1
        && wantEntries.Count(e => e is ScriptStatement s && s.Key == h.Key) == 1;
}
