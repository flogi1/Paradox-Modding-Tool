using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.ScriptCheck;

/// <summary>Art eines Fundes der Skriptprüfung.</summary>
public enum FindingKind
{
    BrokenReference,
    DeadEvent,
    UnusedBlock,
    DuplicateEvent,
    EffectCycle,
    EventCycle,

    /// <summary>Teurer globaler Iterator in häufig laufendem Code (<see cref="PerformanceAnalyzer"/>); hinten angehängt.</summary>
    ExpensiveIterator,
}

/// <summary>Schwere eines Fundes; die Reihenfolge ist die Sortierreihenfolge.</summary>
public enum FindingSeverity
{
    Error,
    Warning,
    Hint,
}

/// <summary>
/// Ein Fund in der gewählten Mod.
/// </summary>
/// <param name="Name">Name des Verweises oder der Definition; bei Zyklen die Mitglieder sortiert, mit <c> → </c> verbunden.</param>
/// <param name="NameKind">Art des Namens (bei Effekt-Zyklen die Art der Definition am <see cref="Location"/>, Effekt oder Trigger; bei Event-Zyklen <see cref="CheckKind.Event"/>).</param>
/// <param name="Location">Ort des Verweises bzw. der Definition in der gewählten Mod.</param>
/// <param name="Related">
/// Kaputte Verweise: die Aufrufer als <c>besitzer (pfad:zeile)</c>; Duplikate: alle Definitionen als <c>pfad:zeile (Herkunft)</c>, die gewinnende zuerst;
/// Zyklen: die Mitglieder; tote Events und unbenutzte Blöcke: leer; teure Iteratoren: der Weg (<see cref="PerformanceInfo.Path"/>).
/// </param>
/// <param name="Key">Schlüssel für die Ausblendliste: <c>Art|Name</c>; bei teuren Iteratoren <c>ExpensiveIterator|Besitzer|Iterator</c> (Besitzer = relativer Pfad ohne Besitzer).</param>
public sealed record Finding(
    FindingKind Kind,
    FindingSeverity Severity,
    string Name,
    CheckKind NameKind,
    SiteLocation Location,
    IReadOnlyList<string> Related,
    string Key)
{
    /// <summary>Nur bei <see cref="FindingKind.ExpensiveIterator"/>: Takt, Weg und Vorschläge; sonst null.</summary>
    public PerformanceInfo? Performance { get; init; }

    /// <summary>Fund mit <see cref="Key"/> = <c>Art|Name</c>.</summary>
    public static Finding Create(FindingKind kind, FindingSeverity severity, string name, CheckKind nameKind, SiteLocation location, IReadOnlyList<string> related) =>
        new(kind, severity, name, nameKind, location, related, kind + "|" + name);
}

/// <summary>Ergebnis der Auswertung.</summary>
/// <param name="Findings">Funde nach Schwere, relativem Pfad und Zeile sortiert.</param>
/// <param name="BlockReferencesChecked">false, wenn keine eingebauten Namen bekannt sind: kaputte Effekt-/Trigger-Aufrufe wurden dann nicht geprüft.</param>
public sealed record AnalyzerResult(IReadOnlyList<Finding> Findings, bool BlockReferencesChecked);
