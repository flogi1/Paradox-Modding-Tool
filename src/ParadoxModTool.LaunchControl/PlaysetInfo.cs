namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Eine einzelne Playset-Zusammenstellung aus playsets.json (Nutzer-Feedback: "Es sollte noch
/// zusätzlich bei der Startoption die Möglichkeit geben, das Playset direkt auszuwählen."). Rein
/// für die Anzeige in der Werkzeugleiste gedacht - <see cref="PlaysetStore"/> arbeitet beim
/// tatsächlichen Umschalten bewusst NICHT mit diesem Typ, sondern direkt auf dem rohen JSON (siehe
/// dortiger Klassenkommentar), damit unbekannte/zukünftige Felder beim Schreiben nicht verloren
/// gehen.
/// </summary>
public sealed class PlaysetInfo
{
    public required string Name { get; init; }

    public bool IsActive { get; init; }

    public int ModCount { get; init; }

    public int EnabledModCount { get; init; }

    /// <summary>Anzeige-Text für die Playset-Auswahl in der Werkzeugleiste, inkl. "aktiv"-Markierung und Mod-Anzahl.</summary>
    public string DisplayText =>
        IsActive
            ? $"{Name} ({EnabledModCount}/{ModCount} Mods, aktiv)"
            : $"{Name} ({EnabledModCount}/{ModCount} Mods)";
}
