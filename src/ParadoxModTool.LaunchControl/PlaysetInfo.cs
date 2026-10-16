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

    /// <summary>Kennung des Playsets in der Datei: UUID in der Launcher-Datenbank, der Name in playsets.json. Zwei Playsets können denselben Namen tragen, die Kennung nicht.</summary>
    public string? Id { get; init; }

    public bool IsActive { get; init; }

    public int ModCount { get; init; }

    public int EnabledModCount { get; init; }

    /// <summary>Anzeige-Text für die Playset-Auswahl in der Werkzeugleiste, inkl. "aktiv"-Markierung und Mod-Anzahl.</summary>
    public string DisplayText =>
        IsActive
            ? PlaysetTexts.DisplayActive(Name, EnabledModCount, ModCount)
            : PlaysetTexts.Display(Name, EnabledModCount, ModCount);
}
