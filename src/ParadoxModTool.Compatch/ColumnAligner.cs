namespace ParadoxModTool.Compatch;

public enum RowOrigin
{
    /// <summary>Die Ergebniszeile steht so in A und B.</summary>
    Same,

    FromA,

    FromB,

    /// <summary>Weder in A noch in B (von Hand oder aus dem Patch).</summary>
    Other,

    /// <summary>Keine Ergebniszeile, nur Zeilen der anderen Spalten.</summary>
    Gap,
}

public sealed record AlignedRow(string? A, string? Result, string? B, string? Vanilla, RowOrigin Origin);

/// <summary>Richtet A, Ergebnis, B und optional Vanilla zeilenweise aus (längste gemeinsame Teilfolge je Seite gegen das Ergebnis).</summary>
public static class ColumnAligner
{
    /// <summary>Oberhalb dieser Zellenzahl (Zeilen × Zeilen) wird nicht mehr ausgerichtet, sondern nur nebeneinander gestellt.</summary>
    private const long MaxCells = 4_000_000;

    public static IReadOnlyList<AlignedRow> Align(string a, string result, string b, string? vanilla)
    {
        var res = Lines(result);
        var sides = new[] { Lines(a), Lines(b), vanilla is null ? Array.Empty<string>() : Lines(vanilla) };
        var matches = sides.Select(side => Match(res, side)).ToArray();
        var next = new int[3];
        var rows = new List<AlignedRow>();

        void Flush(int[] until)
        {
            var pending = new List<string>[3];
            for (var s = 0; s < 3; s++)
            {
                pending[s] = new List<string>();
                while (next[s] < until[s])
                {
                    pending[s].Add(sides[s][next[s]++]);
                }
            }

            var count = pending.Max(p => p.Count);
            for (var i = 0; i < count; i++)
            {
                rows.Add(new AlignedRow(
                    i < pending[0].Count ? pending[0][i] : null,
                    null,
                    i < pending[1].Count ? pending[1][i] : null,
                    vanilla is not null && i < pending[2].Count ? pending[2][i] : null,
                    RowOrigin.Gap));
            }
        }

        for (var r = 0; r < res.Length; r++)
        {
            var until = new int[3];
            for (var s = 0; s < 3; s++)
            {
                until[s] = matches[s][r] >= 0 ? matches[s][r] : next[s];
            }

            Flush(until);
            string? Cell(int s)
            {
                if (matches[s][r] < 0)
                {
                    return null;
                }

                next[s] = matches[s][r] + 1;
                return sides[s][matches[s][r]];
            }

            var inA = matches[0][r] >= 0;
            var inB = matches[1][r] >= 0;
            var origin = inA && inB ? RowOrigin.Same : inA ? RowOrigin.FromA : inB ? RowOrigin.FromB : RowOrigin.Other;
            var cellA = Cell(0);
            var cellB = Cell(1);
            var cellV = Cell(2);
            rows.Add(new AlignedRow(cellA, res[r], cellB, vanilla is null ? null : cellV, origin));
        }

        Flush(sides.Select(s => s.Length).ToArray());
        return rows;
    }

    private static string[] Lines(string text)
    {
        if (text.Length == 0)
        {
            return Array.Empty<string>();
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');
        return lines.Length > 0 && lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    /// <summary>Für jede Ergebniszeile die Zeile der Seite (oder -1), monoton steigend.</summary>
    private static int[] Match(string[] result, string[] side)
    {
        var map = Enumerable.Repeat(-1, result.Length).ToArray();
        if (result.Length == 0 || side.Length == 0)
        {
            return map;
        }

        var x = result.Select(l => l.Trim()).ToArray();
        var y = side.Select(l => l.Trim()).ToArray();
        if ((long)x.Length * y.Length > MaxCells)
        {
            var j = 0;
            for (var i = 0; i < x.Length && j < y.Length; i++)
            {
                var k = Array.IndexOf(y, x[i], j);
                if (k >= 0)
                {
                    map[i] = k;
                    j = k + 1;
                }
            }

            return map;
        }

        var dp = new int[x.Length + 1, y.Length + 1];
        for (var i = x.Length - 1; i >= 0; i--)
        {
            for (var k = y.Length - 1; k >= 0; k--)
            {
                dp[i, k] = x[i] == y[k] ? dp[i + 1, k + 1] + 1 : Math.Max(dp[i + 1, k], dp[i, k + 1]);
            }
        }

        int p = 0, q = 0;
        while (p < x.Length && q < y.Length)
        {
            if (x[p] == y[q])
            {
                map[p++] = q++;
            }
            else if (dp[p + 1, q] >= dp[p, q + 1])
            {
                p++;
            }
            else
            {
                q++;
            }
        }

        return map;
    }
}
