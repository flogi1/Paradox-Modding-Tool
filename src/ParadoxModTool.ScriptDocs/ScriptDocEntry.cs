namespace ParadoxModTool.ScriptDocs;

/// <summary>Art eines Eintrags der Script-Datenbank. Jede Art stammt aus einer eigenen Log-Datei des Spiels.</summary>
public enum ScriptDocKind
{
    Modifier,
    Trigger,
    Effect,
    EventTarget,
    OnAction,
    CustomLocalization,
}

/// <summary>Zuordnung der Eintragsarten zu den Log-Dateien im <c>docs</c>-Ordner des Spiels.</summary>
public static class ScriptDocLogFiles
{
    public static string FileNameOf(ScriptDocKind kind) => kind switch
    {
        ScriptDocKind.Modifier => "modifiers.log",
        ScriptDocKind.Trigger => "triggers.log",
        ScriptDocKind.Effect => "effects.log",
        ScriptDocKind.EventTarget => "event_targets.log",
        ScriptDocKind.OnAction => "on_actions.log",
        ScriptDocKind.CustomLocalization => "custom_localization.log",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string DisplayNameOf(ScriptDocKind kind) => kind switch
    {
        ScriptDocKind.Modifier => "Modifier",
        ScriptDocKind.Trigger => "Trigger",
        ScriptDocKind.Effect => "Effect",
        ScriptDocKind.EventTarget => "Event-Target",
        ScriptDocKind.OnAction => "On-Action",
        ScriptDocKind.CustomLocalization => "Custom-Loc",
        _ => kind.ToString(),
    };
}

/// <summary>
/// Ein Eintrag der Script-Datenbank (ein Modifier, Trigger, Effect, Event-Target, eine On-Action oder Custom-Localization),
/// wie ihn das Spiel in seinen Doku-Logs beschreibt.
/// </summary>
public sealed class ScriptDocEntry
{
    public ScriptDocKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Freitext des Spiels (bei Custom-Loc die Liste der Einträge), Zeilen durch <c>\n</c> getrennt.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Scopes, in denen der Eintrag benutzbar ist (Supported/Input/Expected Scopes; bei Modifiern die Kategorien).</summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>Ziele bzw. Ausgabe-Scopes (Supported Targets bei Trigger/Effect, Output Scopes bei Event-Targets).</summary>
    public List<string> Targets { get; set; } = new();

    /// <summary>Weitere Angaben des Spiels als Schlüssel/Wert (z. B. "From Code", "Requires Data").</summary>
    public Dictionary<string, string> Extra { get; set; } = new();

    /// <summary>Kennung des Imports, in dem der Eintrag zum ersten Mal auftauchte.</summary>
    public int FirstSeenImport { get; set; }

    /// <summary>Kennung des letzten Imports, in dem der Eintrag noch enthalten war.</summary>
    public int LastSeenImport { get; set; }
}
