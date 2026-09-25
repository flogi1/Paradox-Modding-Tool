namespace ParadoxModTool.Session;

/// <summary>
/// Zustand eines einzelnen offenen Dokuments innerhalb einer Session (Abschnitt 12).
/// Nur Dokumente mit einem tatsächlichen Dateipfad werden gespeichert — ein noch nicht
/// gespeichertes neues ("Unbenannt")-Dokument kann beim nächsten Start nicht wiederhergestellt
/// werden, da es keine Datei zum erneuten Laden gibt.
/// </summary>
public sealed class DocumentSessionState
{
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Zeichenposition des Cursors im Dokument beim Speichern der Session — dient als
    /// robuste, zoom-/DPI-unabhängige Grundlage, um beim Wiederherstellen sowohl den Cursor
    /// als auch die ungefähre Scroll-Position (per Sprung zur zugehörigen Zeile) wiederherzustellen.
    /// </summary>
    public int CaretOffset { get; set; }

    /// <summary>
    /// Nutzer-Feedback Runde 5 (Bugfix): "Das Programm die Dateienmarkieren als Wichtig oder
    /// Hilfsdatei sich nicht merkt, wenn es neu gestartet wird." Speichert die manuelle
    /// Kategorie-Zuweisung (siehe OpenDocument.ManualCategory / SetManualCategory) als Enum-Name
    /// ("Important" oder "HelperFile"), damit sie beim nächsten Programmstart wiederhergestellt
    /// werden kann. <c>null</c> bedeutet: keine manuelle Zuweisung, es gilt weiterhin die
    /// automatische Kategorie aus der Dateiendung (OpenDocument.AutoCategory).
    /// </summary>
    public string? ManualCategory { get; set; }
}
