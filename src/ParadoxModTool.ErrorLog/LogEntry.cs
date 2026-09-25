namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Kategorien der in echten EU5-Error-Logs beobachteten Zeilenmuster
/// (siehe Architektur-Spezifikation, Abschnitt 9).
/// </summary>
public enum LogEntryKind
{
    /// <summary>Skript-Parsing-Fehler mit "in file: ... near line: N".</summary>
    ParsingError,

    /// <summary>GUI-Fehler (gemeldet von pdx_gui_widget_desc.h und/oder pdx_gui_factory.cpp).</summary>
    GuiError,

    /// <summary>Fehler ohne direkten Dateibezug, nur ein symbolischer Schlüssel.</summary>
    SymbolicError,

    /// <summary>Mehrzeiliger Laufzeit-Skriptfehler ("Script system error!" + Details).</summary>
    ScriptRuntimeError,

    /// <summary>Vorschlagszeile ("Possible match: '...'"), wird an den vorherigen Eintrag angehängt.</summary>
    SuggestionMatch,

    /// <summary>Zeile konnte keinem bekannten Muster zugeordnet werden.</summary>
    Unclassified
}

/// <summary>
/// Ein einzelner, geparster Log-Eintrag.
/// </summary>
/// <param name="Kind">Erkanntes Zeilenmuster.</param>
/// <param name="Timestamp">Zeitstempel als Rohtext (HH:MM:SS), sofern vorhanden.</param>
/// <param name="SourceComponent">Meldende Quellkomponente, z. B. "pdx_persistent_reader.cpp:289".</param>
/// <param name="Message">Fehlermeldungstext.</param>
/// <param name="FilePath">Bezug auf eine Mod-/Spieldatei, relativ zum Mod-/Spiel-Root, sofern vorhanden.</param>
/// <param name="LineNumber">Zeilennummer innerhalb der referenzierten Datei, sofern vorhanden.</param>
/// <param name="Suggestions">Angehängte Korrekturvorschläge ("Possible match"-Kandidaten).</param>
public sealed record LogEntry(
    LogEntryKind Kind,
    string Timestamp,
    string SourceComponent,
    string Message,
    string? FilePath,
    int? LineNumber,
    IReadOnlyList<string> Suggestions)
{
    /// <summary>True, wenn dieser Eintrag direkt zu einer Zeile in einer Datei springen kann.</summary>
    public bool HasFileReference => FilePath is not null && LineNumber is not null;
}
