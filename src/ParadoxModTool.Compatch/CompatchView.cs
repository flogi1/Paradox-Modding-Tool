namespace ParadoxModTool.Compatch;

/// <summary>Die drei Ansichten des Merge-Fensters (Spec, "Oberfläche").</summary>
public enum CompatchView
{
    /// <summary>A: Aktionsbaum und Spalten.</summary>
    Actions,

    /// <summary>B: Ergebnisdokument.</summary>
    Document,

    /// <summary>C: Review-Karten.</summary>
    Cards,
}

/// <summary>Einstellungswert der Standardansicht: "A", "B" oder "C".</summary>
public static class CompatchViews
{
    public static CompatchView Parse(string? setting) => setting?.Trim().ToUpperInvariant() switch
    {
        "B" => CompatchView.Document,
        "C" => CompatchView.Cards,
        _ => CompatchView.Actions,
    };

    public static string ToSetting(CompatchView view) => view switch
    {
        CompatchView.Document => "B",
        CompatchView.Cards => "C",
        _ => "A",
    };
}
