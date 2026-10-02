namespace ParadoxModTool.Compatch;

/// <summary>Eine Spalte der Vergleichsansichten im Kompatch-Merge.</summary>
public enum CompatchColumn
{
    A,
    Result,
    B,
    Vanilla,
}

/// <summary>
/// Reihenfolge der Spalten in allen Vergleichsansichten (Detail, Karten, Vergleichsfenster, Dokument). Standard: A | Ergebnis | B [| Vanilla].
/// Mit "Ergebnis ganz rechts" (Nutzerwunsch): A | B [| Vanilla] ‖ Ergebnis, mit einer Trennlinie zwischen Vergleich und neuem Stand.
/// </summary>
public static class CompatchColumns
{
    public static IReadOnlyList<CompatchColumn> Order(bool showVanilla, bool resultRight)
    {
        var columns = resultRight
            ? new List<CompatchColumn> { CompatchColumn.A, CompatchColumn.B }
            : new List<CompatchColumn> { CompatchColumn.A, CompatchColumn.Result, CompatchColumn.B };
        if (showVanilla)
        {
            columns.Add(CompatchColumn.Vanilla);
        }

        if (resultRight)
        {
            columns.Add(CompatchColumn.Result);
        }

        return columns;
    }

    /// <summary>Steht vor dieser Spalte die Trennlinie? Nur vor dem Ergebnis, wenn es ganz rechts steht.</summary>
    public static bool DividerBefore(CompatchColumn column, bool resultRight) => resultRight && column == CompatchColumn.Result;
}
