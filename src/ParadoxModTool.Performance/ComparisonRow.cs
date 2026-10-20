namespace ParadoxModTool.Performance;

/// <summary>Urteil über eine Zeile beim Vergleich zweier Läufe.</summary>
public enum Verdict
{
    /// <summary>Gesamtzeit spürbar gesunken.</summary>
    Better,

    /// <summary>Gesamtzeit spürbar gestiegen.</summary>
    Worse,

    /// <summary>Änderung im Rauschen.</summary>
    Unchanged,

    /// <summary>Zeile nur in Lauf B.</summary>
    NewInB,

    /// <summary>Zeile nur in Lauf A.</summary>
    MissingInB,

    /// <summary>Aufrufzahl hat sich so stark geändert, dass die Zeiten nicht vergleichbar sind.</summary>
    BehaviorChanged,
}

/// <summary>Eine Zeile des Laufvergleichs; fehlt die Zeile in einem Lauf, sind dessen Werte <c>null</c>.</summary>
/// <param name="Origin">Herkunft aus Lauf A, sonst aus Lauf B.</param>
/// <param name="DeltaRatio">(TotalB - TotalA) / TotalA; <c>null</c> bei TotalA = 0 oder fehlender Seite.</param>
public sealed record ComparisonRow(
    string Key,
    RowOrigin Origin,
    long? CallsA,
    long? CallsB,
    double? TotalA,
    double? TotalB,
    double? DeltaRatio,
    Verdict Verdict);
