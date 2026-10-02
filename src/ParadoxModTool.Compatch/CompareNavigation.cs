namespace ParadoxModTool.Compatch;

/// <summary>
/// Springen im Vergleichsfenster (Nutzerwunsch, wie F7 im C-U-Tool): Haltepunkte an jeder Abweichung zwischen A und B und die Stelle einer
/// Aktion im ganzen Block, damit das Fenster beim Öffnen gleich dorthin springt.
/// </summary>
public static class CompareNavigation
{
    /// <summary>A und B unterscheiden sich in dieser Zeile, oder das Ergebnis steht in keiner der beiden.</summary>
    public static bool IsDifference(AlignedRow row) => !string.Equals(row.A, row.B, StringComparison.Ordinal) || row.Origin == RowOrigin.Other;

    /// <summary>Index der ersten Zeile jedes zusammenhängenden Abweichungsblocks.</summary>
    public static IReadOnlyList<int> Stops(IReadOnlyList<AlignedRow> rows)
    {
        var stops = new List<int>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (IsDifference(rows[i]) && (i == 0 || !IsDifference(rows[i - 1])))
            {
                stops.Add(i);
            }
        }

        return stops;
    }

    /// <summary>Nächster bzw. voriger Haltepunkt nach <paramref name="current"/>; <see langword="null"/> am Ende (kein Umlauf).</summary>
    public static int? Next(IReadOnlyList<int> stops, int current, bool forward) =>
        forward
            ? stops.Where(i => i > current).Select(i => (int?)i).FirstOrDefault()
            : stops.Where(i => i < current).Select(i => (int?)i).LastOrDefault();

    /// <summary>
    /// Wo die Abweichungszeilen von <paramref name="part"/> (die Zeilen einer Aktion) in <paramref name="whole"/> (der ganze Block) stehen: zuerst als
    /// zusammenhängende Folge, sonst die erste Zeile, die der ersten Abweichungszeile gleicht. A und B werden ohne Rand-Leerzeichen verglichen.
    /// </summary>
    public static int? RowOf(IReadOnlyList<AlignedRow> whole, IReadOnlyList<AlignedRow> part)
    {
        var wanted = part.Where(IsDifference).ToList();
        if (wanted.Count == 0)
        {
            wanted = part.ToList();
        }

        if (wanted.Count == 0)
        {
            return null;
        }

        for (var i = 0; i + wanted.Count <= whole.Count; i++)
        {
            if (wanted.Select((row, k) => Same(whole[i + k], row)).All(same => same))
            {
                return i;
            }
        }

        for (var i = 0; i < whole.Count; i++)
        {
            if (Same(whole[i], wanted[0]))
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>
    /// Markierte Zeilen übernehmen (Nutzerwunsch, genau die Zeilen): der Ergebnistext des ganzen Blocks, in dem die Zeilen <paramref name="from"/> bis
    /// <paramref name="to"/> (in beliebiger Reihenfolge) aus der Spalte <paramref name="column"/> stehen statt aus dem Ergebnis. Leere Zellen entfallen.
    /// </summary>
    public static string TakeRows(IReadOnlyList<AlignedRow> rows, int from, int to, CompatchColumn column)
    {
        var (first, last) = from <= to ? (from, to) : (to, from);
        var lines = new List<string>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var line = i >= first && i <= last
                ? column switch
                {
                    CompatchColumn.A => row.A,
                    CompatchColumn.B => row.B,
                    CompatchColumn.Vanilla => row.Vanilla,
                    _ => row.Result,
                }
                : row.Result;
            if (line is not null)
            {
                lines.Add(line);
            }
        }

        return string.Join("\n", lines);
    }

    private static bool Same(AlignedRow x, AlignedRow y) =>
        string.Equals(x.A?.Trim(), y.A?.Trim(), StringComparison.Ordinal) && string.Equals(x.B?.Trim(), y.B?.Trim(), StringComparison.Ordinal);
}
