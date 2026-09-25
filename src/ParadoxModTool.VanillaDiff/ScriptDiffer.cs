namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Struktureller Diff zweier Paradox-Skript-ASTs (Abschnitt 7). Arbeitet auf
/// <see cref="ScriptBlock"/>-Bäumen statt auf Rohtext/Zeilen, damit semantisch gleichwertige,
/// aber anders formatierte Blöcke nicht als Unterschied gewertet werden. Liefert einen Baum
/// aus <see cref="EntryDiff"/>: Ebenen, in denen sich beidseitig ein gleichnamiger
/// Schlüssel mit verschachteltem Blockwert geändert hat, werden rekursiv aufgeklappt
/// ("Dateiarchitektur wird nebeneinander gelegt", nicht nur Zeile für Zeile).
/// </summary>
public static class ScriptDiffer
{
    public static IReadOnlyList<EntryDiff> Diff(ScriptBlock left, ScriptBlock right, DiffOptions? options = null)
    {
        options ??= new DiffOptions();
        return DiffEntries(left.Entries, right.Entries, options);
    }

    internal static List<EntryDiff> DiffEntries(IReadOnlyList<ScriptEntry> left, IReadOnlyList<ScriptEntry> right, DiffOptions options)
    {
        var matches = ComputeLcsMatches(left, right);
        var result = new List<EntryDiff>();
        var leftKeys = BuildKeyMap(left);
        var rightKeys = BuildKeyMap(right);

        var li = 0;
        var ri = 0;

        foreach (var (mi, mj) in matches)
        {
            ReconcileGap(left, li, mi, right, ri, mj, leftKeys, rightKeys, options, result);
            result.Add(new EntryDiff { Kind = DiffKind.Unchanged, Left = left[mi], Right = right[mj] });
            li = mi + 1;
            ri = mj + 1;
        }

        ReconcileGap(left, li, left.Count, right, ri, right.Count, leftKeys, rightKeys, options, result);

        return result;
    }

    /// <summary>
    /// Längste gemeinsame Teilfolge (klassisches LCS-DP) über die strukturelle Gleichheit
    /// der Einträge. Liefert Paare (linkerIndex, rechterIndex) unveränderter Einträge in
    /// Reihenfolge. Für sehr große, flache Blöcke ist dies O(n·m) — für die typische Größe
    /// einzelner Paradox-Skriptdateien (Abschnitt 16, Performance-Grundsätze) ausreichend;
    /// eine effizientere Variante kann bei Bedarf nachgezogen werden.
    /// </summary>
    private static List<(int Left, int Right)> ComputeLcsMatches(IReadOnlyList<ScriptEntry> left, IReadOnlyList<ScriptEntry> right)
    {
        var n = left.Count;
        var m = right.Count;
        var dp = new int[n + 1, m + 1];

        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                dp[i, j] = ScriptStructuralComparer.EntryEquals(left[i], right[j])
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var matches = new List<(int, int)>();
        var a = 0;
        var b = 0;

        while (a < n && b < m)
        {
            if (ScriptStructuralComparer.EntryEquals(left[a], right[b]))
            {
                matches.Add((a, b));
                a++;
                b++;
            }
            else if (dp[a + 1, b] >= dp[a, b + 1])
            {
                a++;
            }
            else
            {
                b++;
            }
        }

        return matches;
    }

    /// <summary>
    /// Behandelt eine Lücke zwischen zwei (oder vor der ersten/nach der letzten) LCS-
    /// Übereinstimmungen: Einträge mit demselben Schlüssel auf beiden Seiten werden als
    /// "geändert" gepaart (und bei beidseitigem Blockwert rekursiv verglichen), alles
    /// andere wird als reines Entfernen bzw. Hinzufügen gewertet.
    /// </summary>
    private static void ReconcileGap(
        IReadOnlyList<ScriptEntry> left, int leftStart, int leftEnd,
        IReadOnlyList<ScriptEntry> right, int rightStart, int rightEnd,
        Dictionary<ScriptEntry, string> leftKeys, Dictionary<ScriptEntry, string> rightKeys,
        DiffOptions options, List<EntryDiff> result)
    {
        var rightByKey = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);

        for (var j = rightStart; j < rightEnd; j++)
        {
            if (!rightKeys.TryGetValue(right[j], out var key))
            {
                continue;
            }

            if (!rightByKey.TryGetValue(key, out var queue))
            {
                queue = new Queue<int>();
                rightByKey[key] = queue;
            }

            queue.Enqueue(j);
        }

        var consumedRight = new HashSet<int>();

        for (var i = leftStart; i < leftEnd; i++)
        {
            int? pairedRight = null;

            if (leftKeys.TryGetValue(left[i], out var key) && rightByKey.TryGetValue(key, out var queue) && queue.Count > 0)
            {
                pairedRight = queue.Dequeue();
            }

            if (pairedRight is int rj)
            {
                consumedRight.Add(rj);
                result.Add(BuildModifiedOrUnchanged(left[i], right[rj], options));
            }
            else
            {
                result.Add(new EntryDiff { Kind = DiffKind.Removed, Left = left[i] });
            }
        }

        for (var j = rightStart; j < rightEnd; j++)
        {
            if (!consumedRight.Contains(j))
            {
                result.Add(new EntryDiff { Kind = DiffKind.Added, Right = right[j] });
            }
        }
    }

    private static EntryDiff BuildModifiedOrUnchanged(ScriptEntry leftEntry, ScriptEntry rightEntry, DiffOptions options)
    {
        if (ScriptStructuralComparer.EntryEquals(leftEntry, rightEntry))
        {
            return new EntryDiff { Kind = DiffKind.Unchanged, Left = leftEntry, Right = rightEntry };
        }

        if (BlockValueOf(leftEntry) is { } leftBlock && BlockValueOf(rightEntry) is { } rightBlock)
        {
            var children = DiffEntries(leftBlock.Entries, rightBlock.Entries, options);
            return new EntryDiff { Kind = DiffKind.Modified, Left = leftEntry, Right = rightEntry, Children = children };
        }

        return new EntryDiff { Kind = DiffKind.Modified, Left = leftEntry, Right = rightEntry };
    }

    private static ScriptBlock? BlockValueOf(ScriptEntry entry) => entry switch
    {
        ScriptStatement { Value: ScriptBlock block } => block,
        ScriptListItem { Value: ScriptBlock block } => block,
        _ => null
    };

    /// <summary>
    /// Schlüssel je Eintrag (Anweisungen UND "Kopfzeile + Rumpf"-Blöcke, siehe <see cref="EntryUnit"/>),
    /// damit auch GUI-Konstrukte wie "template name { ... }" als geändert gepaart und rekursiv
    /// verglichen werden statt als komplett entfernt + hinzugefügt zu erscheinen.
    /// </summary>
    private static Dictionary<ScriptEntry, string> BuildKeyMap(IReadOnlyList<ScriptEntry> entries)
    {
        var map = new Dictionary<ScriptEntry, string>(ReferenceEqualityComparer.Instance);
        foreach (var unit in EntryUnits.Build(entries))
        {
            if (unit.Kind != EntryUnit.UnitKind.Scalar)
            {
                map[unit.Entry] = unit.Key;
            }
        }

        return map;
    }
}
