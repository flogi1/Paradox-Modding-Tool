namespace ParadoxModTool.ScriptCheck;

/// <summary>Wie oft ein Puls läuft: schneller als monatlich = täglich, vierteljährlich = monatlich, alle zwei Jahre und seltener = selten.</summary>
public enum PulseRate
{
    Daily,
    Monthly,
    Yearly,
    Rare,
}

/// <summary>Ein teurer globaler Iterator (<c>every_country</c>) und die engeren Iteratoren, die man stattdessen nehmen kann; null = keine bekannt.</summary>
public sealed record GlobalIteratorRule(string Name, IReadOnlyList<string>? Narrower);

/// <summary>Blöcke, die das Spiel in diesem Ordner dauernd auswertet (<c>common/decisions</c>: <c>is_shown</c>); nur direkte Kinder der Definition zählen.</summary>
/// <param name="Wrappers">Hüllschlüssel oberster Ebene, deren Kinder die Definitionen sind (EU IV <c>country_decisions = { my_dec = { … } }</c>); <c>*</c> = jeder Schlüssel oberster Ebene (HOI IV Kategorien); null = keine.</param>
/// <param name="NameField">
/// Feld, dessen Wert ein Kind einer Hülle benennt (HOI IV <c>focus = { id = X }</c> → <c>X</c>), in Dateien der Regel ebenso jeden sonstigen Block oberster
/// Ebene, der keine Definition ist (HOI IV <c>shared_focus = { id = X }</c>); fehlt es, gilt der Schlüssel; null = immer der Schlüssel.
/// </param>
/// <param name="ExcludeFolders">Unterordner von <paramref name="Folder"/>, deren Dateien die Regel gar nicht betrifft (HOI IV <c>common/decisions/categories</c>); null = keine.</param>
/// <remarks>Die Blöcke sind Bedingungs- oder Gewichtungskontexte (Trigger); die Auswertung verlässt sich darauf.</remarks>
public sealed record CheckedBlockRule(string Folder, IReadOnlyList<string> Blocks, IReadOnlyList<string>? Wrappers = null, string? NameField = null, IReadOnlyList<string>? ExcludeFolders = null)
{
    /// <summary>Eintrag in <see cref="Wrappers"/>: jeder Schlüssel oberster Ebene mit Block ist eine Hülle.</summary>
    public const string AnyWrapper = "*";
}

/// <summary>Regeln für teure globale Iteratoren (Objekt <c>performance</c> der Regeldatei); reine Daten.</summary>
public sealed class ScriptCheckPerformanceRules
{
    /// <summary>Teure Iteratoren, die über alle Objekte des Spiels laufen.</summary>
    public IReadOnlyList<GlobalIteratorRule> GlobalIterators { get; init; } = Array.Empty<GlobalIteratorRule>();

    /// <summary>Pulse-On-Actions mit ihrer Häufigkeit.</summary>
    public IReadOnlyDictionary<string, PulseRate> PulseOnActions { get; init; } = new Dictionary<string, PulseRate>();

    /// <summary>Schlüssel in On-Actions, deren Inhalt weitere On-Actions aufruft.</summary>
    public IReadOnlyList<string> OnActionCallLists { get; init; } = Array.Empty<string>();

    /// <summary>Ordner und Blöcke, die dauernd ausgewertet werden.</summary>
    public IReadOnlyList<CheckedBlockRule> AlwaysCheckedBlocks { get; init; } = Array.Empty<CheckedBlockRule>();

    /// <summary>Blöcke eines Events, die das Spiel regelmäßig abfragt (<c>trigger</c> bei Events ohne <c>is_triggered_only</c>).</summary>
    public IReadOnlyList<string> PolledEventBlocks { get; init; } = Array.Empty<string>();
}
