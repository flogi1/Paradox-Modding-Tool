using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>
/// 3-Wege-Vergleich Vanilla/A/B bis zum Teilschlüssel. Unveränderte Stellen übernimmt der Walker selbst. Jede Abweichung meldet er als
/// <see cref="MergePoint"/>; was dort ins Ergebnis kommt, entscheidet der Aufrufer (Vorauswahl beim Entscheiden, Nutzerwahl beim Anwenden).
/// </summary>
public static class MergeWalker
{
    public static List<ScriptEntry> Merge(IReadOnlyList<ScriptEntry> baseEntries, IReadOnlyList<ScriptEntry> a, IReadOnlyList<ScriptEntry> b, IReadOnlySet<string> logicKeys, MergePointResolver resolve) =>
        MergeLevel(baseEntries, a, b, Array.Empty<string>(), Array.Empty<string>(), false, logicKeys, resolve);

    private static List<ScriptEntry> MergeLevel(
        IReadOnlyList<ScriptEntry> baseEntries, IReadOnlyList<ScriptEntry> aEntries, IReadOnlyList<ScriptEntry> bEntries,
        IReadOnlyList<string> path, IReadOnlyList<string> display, bool inLogic, IReadOnlySet<string> logicKeys, MergePointResolver resolve)
    {
        var baseUnits = EntryUnits.Build(baseEntries);
        var rawA = EntryUnits.Build(aEntries);
        var rawB = EntryUnits.Build(bEntries);
        var repeated = SiblingMatcher.RepeatedBaseKeys(baseUnits, rawA, rawB);
        var alignedA = SiblingMatcher.Align(baseUnits, rawA, repeated);
        var alignedB = SiblingMatcher.Align(baseUnits, rawB, repeated);
        var byBase = baseUnits.ToDictionary(u => u.Key, StringComparer.Ordinal);
        var byA = alignedA.Units.ToDictionary(u => u.Key, StringComparer.Ordinal);
        var byB = alignedB.Units.ToDictionary(u => u.Key, StringComparer.Ordinal);

        var result = new List<ScriptEntry>();
        foreach (var key in OrderKeys(baseUnits, alignedA.Units, alignedB.Units))
        {
            byBase.TryGetValue(key, out var u0);
            byA.TryGetValue(key, out var ua);
            byB.TryGetValue(key, out var ub);
            var shown = (ua ?? ub ?? u0)!;
            var keyPath = path.Append(key).ToList();
            var keyDisplay = display.Append(shown.Display).ToList();
            var logic = inLogic || (shown.Entry is ScriptStatement statement && LogicFields.IsLogic(statement.Key, logicKeys));
            var ambiguous = alignedA.AmbiguousKeys.Contains(key) || alignedB.AmbiguousKeys.Contains(key);
            MergePoint Point(MergeOrigin? origin) =>
                new(string.Join("/", keyPath), keyPath, string.Join(".", keyDisplay), u0, ua, ub, origin, logic, ambiguous, inLogic);

            if (Same(ua, u0) && Same(ub, u0))
            {
                result.AddRange(MergeChoice.Emit(ua ?? u0));
                continue;
            }

            // Zwei Blöcke mit gleicher Kopfzeile: immer bis zum Teilschlüssel absteigen, auch wenn nur eine Seite geändert hat.
            // Sonst landet die Aktion auf dem ganzen Block (grobes Rückgängig, keine Logikblock-Erkennung darin).
            if (ua?.Value is ScriptBlock blockA && ub?.Value is ScriptBlock blockB
                && EntryUnits.HeaderText(ua.Header) == EntryUnits.HeaderText(ub.Header)
                && (u0 is null || u0.Value is ScriptBlock))
            {
                var nested = MergeLevel((u0?.Value as ScriptBlock)?.Entries ?? new List<ScriptEntry>(), blockA.Entries, blockB.Entries, keyPath, keyDisplay, logic, logicKeys, resolve);
                // Entspricht das Ergebnis einer Fassung, deren Originaleintrag übernehmen (behält die Textstelle für wörtliches Schreiben).
                var unchanged = new[] { ua, ub, u0 }.FirstOrDefault(c => c?.Value is ScriptBlock block && StructuralEquality.EntriesEqual(block.Entries, nested));
                if (unchanged is not null)
                {
                    result.AddRange(MergeChoice.Emit(unchanged));
                }
                else
                {
                    result.AddRange(ua.Header);
                    result.Add(EntryUnits.WithBlockValue(ua.Entry, new ScriptBlock { Entries = nested }));
                }

                continue;
            }

            if (Same(ua, ub))
            {
                result.AddRange(resolve(Point(MergeOrigin.Both)));
                continue;
            }

            if (Same(ua, u0))
            {
                result.AddRange(resolve(Point(MergeOrigin.B)));
                continue;
            }

            if (Same(ub, u0))
            {
                result.AddRange(resolve(Point(MergeOrigin.A)));
                continue;
            }

            result.AddRange(resolve(Point(null)));
        }

        return result;
    }

    private static bool Same(EntryUnit? x, EntryUnit? y) =>
        x is null
            ? y is null
            : y is not null && EntryUnits.HeaderText(x.Header) == EntryUnits.HeaderText(y.Header) && StructuralEquality.EntryEqual(x.Entry, y.Entry);

    /// <summary>Reihenfolge: A (oder Vanilla, falls A leer), dann fehlende Schlüssel von Vanilla, A und B hinter ihrem Vorgänger eingewebt.</summary>
    internal static List<string> OrderKeys(IReadOnlyList<EntryUnit> baseUnits, IReadOnlyList<EntryUnit> a, IReadOnlyList<EntryUnit> b)
    {
        var order = (a.Count > 0 ? a : baseUnits).Select(u => u.Key).ToList();
        void Weave(IReadOnlyList<EntryUnit> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                if (order.Contains(source[i].Key))
                {
                    continue;
                }

                var at = 0;
                for (var j = i - 1; j >= 0; j--)
                {
                    var index = order.IndexOf(source[j].Key);
                    if (index >= 0)
                    {
                        at = index + 1;
                        break;
                    }
                }

                order.Insert(at, source[i].Key);
            }
        }

        Weave(baseUnits);
        Weave(a);
        Weave(b);
        return order;
    }
}

/// <summary>Einträge einer Einheit bzw. einer Entscheidung an einer Abweichungsstelle.</summary>
public static class MergeChoice
{
    public static IReadOnlyList<ScriptEntry> Emit(EntryUnit? unit) =>
        unit is null ? Array.Empty<ScriptEntry>() : unit.Header.Append(unit.Entry).ToList();

    public static IReadOnlyList<ScriptEntry> Entries(MergePoint point, DecisionKind kind) => kind switch
    {
        DecisionKind.TakeA => Emit(point.A),
        DecisionKind.TakeB => Emit(point.B),
        DecisionKind.TakeVanilla => Emit(point.Base),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Eigener Text wird vom Aufrufer geparst."),
    };
}
