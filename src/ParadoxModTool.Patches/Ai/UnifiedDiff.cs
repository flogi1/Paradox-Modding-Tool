namespace ParadoxModTool.Patches.Ai;

/// <summary>
/// Zeilen-Diff im Unified-Format für <c>pmt-patch plan --diff</c>. Gemeinsamer Anfang und gemeinsames Ende werden abgeschnitten, der Rest
/// läuft durch Myers O(ND); CRLF und LF gelten in den Hunks als gleich. Bei sehr vielen Unterschieden (mehr als <see cref="MaxEdits"/>) gilt der Mittelteil
/// als ganz ersetzt, damit Speicher und Zeit begrenzt bleiben.
/// </summary>
public static class UnifiedDiff
{
    /// <summary>Obergrenze für die Länge des Edit-Skripts, bis zu der Myers gerechnet wird (Speicher wächst mit dem Quadrat).</summary>
    private const int MaxEdits = 3000;

    private enum Op
    {
        Equal,
        Delete,
        Insert,
    }

    /// <param name="oldText">Null: die Datei ist neu (<c>--- /dev/null</c>, alle Zeilen als <c>+</c>).</param>
    /// <returns>
    /// Leer, wenn beide Texte gleich sind; sonst Kopfzeilen und Hunks mit je <paramref name="context"/> Zeilen Umgebung. Unterscheiden sich nur
    /// Zeilenenden oder der Zeilenumbruch am Ende, folgen auf die Kopfzeilen statt Hunks <c>#</c>-Zeilen, die das benennen.
    /// </returns>
    public static IReadOnlyList<string> Build(string path, string? oldText, string newText, int context = 2)
    {
        var oldLines = oldText is null ? Array.Empty<string>() : Split(oldText);
        var newLines = Split(newText);
        if (oldText is not null && oldLines.AsSpan().SequenceEqual(newLines))
        {
            return oldText == newText ? Array.Empty<string>() : new[] { "--- a/" + path, "+++ b/" + path }.Concat(LayoutNotes(oldText, newText)).ToList();
        }

        var result = new List<string>
        {
            oldText is null ? "--- /dev/null" : "--- a/" + path,
            "+++ b/" + path,
        };

        var ops = Script(oldLines, newLines);
        var changes = new List<int>();
        for (var i = 0; i < ops.Count; i++)
        {
            if (ops[i].Op != Op.Equal)
            {
                changes.Add(i);
            }
        }

        var index = 0;
        while (index < changes.Count)
        {
            // Änderungen gehören zu einem Hunk, solange dazwischen höchstens 2 * context gleiche Zeilen liegen.
            var last = index;
            while (last + 1 < changes.Count && changes[last + 1] - changes[last] - 1 <= 2 * context)
            {
                last++;
            }

            var start = Math.Max(0, changes[index] - context);
            var end = Math.Min(ops.Count - 1, changes[last] + context);
            var oldCount = 0;
            var newCount = 0;
            for (var i = start; i <= end; i++)
            {
                if (ops[i].Op != Op.Insert)
                {
                    oldCount++;
                }

                if (ops[i].Op != Op.Delete)
                {
                    newCount++;
                }
            }

            var oldStart = oldCount == 0 ? ops[start].OldBefore : ops[start].OldBefore + 1;
            var newStart = newCount == 0 ? ops[start].NewBefore : ops[start].NewBefore + 1;
            result.Add($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");
            for (var i = start; i <= end; i++)
            {
                result.Add((ops[i].Op switch { Op.Delete => "-", Op.Insert => "+", _ => " " }) + ops[i].Line);
            }

            index = last + 1;
        }

        return result;
    }

    /// <summary>Was sich bei gleichen Zeilen unterscheidet: die Art der Zeilenenden und/oder der Zeilenumbruch am Ende.</summary>
    private static List<string> LayoutNotes(string oldText, string newText)
    {
        var notes = new List<string>();
        var oldEnding = Ending(oldText);
        var newEnding = Ending(newText);
        if (oldEnding != newEnding && oldEnding != "none" && newEnding != "none")
        {
            notes.Add($"# only line endings differ: {oldEnding} -> {newEnding}");
        }

        var oldFinal = oldText.EndsWith('\n');
        if (oldFinal != newText.EndsWith('\n'))
        {
            notes.Add("# only the final line break differs: " + (oldFinal ? "removed" : "added"));
        }

        // Beide gemischt, nur an anderen Stellen
        if (notes.Count == 0)
        {
            notes.Add("# only line endings differ");
        }

        return notes;
    }

    private static string Ending(string text)
    {
        var crlf = 0;
        var lf = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                if (i > 0 && text[i - 1] == '\r')
                {
                    crlf++;
                }
                else
                {
                    lf++;
                }
            }
        }

        return (crlf, lf) switch
        {
            ( > 0, > 0) => "mixed",
            ( > 0, _) => "CRLF",
            (_, > 0) => "LF",
            _ => "none",
        };
    }

    private static string[] Split(string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        if (normalized.Length == 0)
        {
            return Array.Empty<string>();
        }

        var lines = normalized.Split('\n');
        // Ein abschließender Zeilenumbruch erzeugt kein eigenes leeres Element.
        return normalized.EndsWith('\n') ? lines[..^1] : lines;
    }

    private readonly record struct Entry(Op Op, string Line, int OldBefore, int NewBefore);

    /// <summary>Vollständiges Edit-Skript; <c>OldBefore</c>/<c>NewBefore</c> sind die Zahlen der alten/neuen Zeilen vor dem Eintrag.</summary>
    private static List<Entry> Script(string[] a, string[] b)
    {
        var prefix = 0;
        while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < a.Length - prefix && suffix < b.Length - prefix && a[^(suffix + 1)] == b[^(suffix + 1)])
        {
            suffix++;
        }

        var middleA = a[prefix..(a.Length - suffix)];
        var middleB = b[prefix..(b.Length - suffix)];
        var ops = new List<Entry>(a.Length + b.Length);
        int oldPos = 0, newPos = 0;

        void Add(Op op, string line)
        {
            ops.Add(new Entry(op, line, oldPos, newPos));
            if (op != Op.Insert)
            {
                oldPos++;
            }

            if (op != Op.Delete)
            {
                newPos++;
            }
        }

        for (var i = 0; i < prefix; i++)
        {
            Add(Op.Equal, a[i]);
        }

        foreach (var (op, line) in Myers(middleA, middleB))
        {
            Add(op, line);
        }

        for (var i = a.Length - suffix; i < a.Length; i++)
        {
            Add(Op.Equal, a[i]);
        }

        return ops;
    }

    private static IEnumerable<(Op Op, string Line)> Myers(string[] a, string[] b)
    {
        int n = a.Length, m = b.Length;
        if (n == 0 || m == 0)
        {
            return a.Select(l => (Op.Delete, l)).Concat(b.Select(l => (Op.Insert, l)));
        }

        var max = Math.Min(n + m, MaxEdits);
        var offset = max + 1;
        var v = new int[2 * max + 3];
        var trace = new List<int[]>();
        var found = -1;
        for (var d = 0; d <= max && found < 0; d++)
        {
            // Stand vor Runde d für k in [-d-1, d+1], Index k + d + 1.
            var snapshot = new int[2 * d + 3];
            Array.Copy(v, offset - d - 1, snapshot, 0, snapshot.Length);
            trace.Add(snapshot);
            for (var k = -d; k <= d; k += 2)
            {
                var x = k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]) ? v[offset + k + 1] : v[offset + k - 1] + 1;
                var y = x - k;
                while (x < n && y < m && a[x] == b[y])
                {
                    x++;
                    y++;
                }

                v[offset + k] = x;
                if (x >= n && y >= m)
                {
                    found = d;
                    break;
                }
            }
        }

        if (found < 0)
        {
            // Zu viele Unterschiede: der Mittelteil gilt als ganz ersetzt.
            return a.Select(l => (Op.Delete, l)).Concat(b.Select(l => (Op.Insert, l)));
        }

        var reversed = new List<(Op, string)>();
        int px = n, py = m;
        for (var d = found; d > 0; d--)
        {
            var before = trace[d];
            var k = px - py;
            var prevK = k == -d || (k != d && before[k - 1 + d + 1] < before[k + 1 + d + 1]) ? k + 1 : k - 1;
            var prevX = before[prevK + d + 1];
            var prevY = prevX - prevK;
            while (px > prevX && py > prevY)
            {
                px--;
                py--;
                reversed.Add((Op.Equal, a[px]));
            }

            if (px == prevX)
            {
                reversed.Add((Op.Insert, b[prevY]));
            }
            else
            {
                reversed.Add((Op.Delete, a[prevX]));
            }

            px = prevX;
            py = prevY;
        }

        while (px > 0 && py > 0)
        {
            px--;
            py--;
            reversed.Add((Op.Equal, a[px]));
        }

        reversed.Reverse();
        return reversed;
    }
}
