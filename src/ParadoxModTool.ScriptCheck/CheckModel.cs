namespace ParadoxModTool.ScriptCheck;

/// <summary>Art einer Definition im Skript; <see cref="Other"/> = kein Definitionsordner (nur als Besitzer eines Verweises).</summary>
public enum CheckKind
{
    Event,
    Effect,
    Trigger,
    Value,
    OnAction,
    Other,

    /// <summary>Benannter Block eines Ordners mit bekannten Namen (z. B. <c>common/scripted_modifiers</c>): Aufrufe zeigen auf ihn, er wird nie als unbenutzt gemeldet. Hinten angehängt, damit gespeicherte Zahlen gültig bleiben.</summary>
    Named,
}

/// <summary>Eine Definition (Event, Skripteffekt, Skripttrigger, Skriptwert, On-Action) mit 1-basierter Zeile.</summary>
/// <param name="SelfFiring">Nur bei Events: löst sich selbst aus (z. B. <c>mean_time_to_happen</c>, ohne <c>is_triggered_only = yes</c>, Engine-Event).</param>
/// <param name="Soft">Weiche Definition: Event-ID-förmiger Parameterwert eines <c>inline_script</c> (kann auch nur ein Verweis sein); zählt nie für Duplikate.</param>
/// <param name="EndLine">1-basierte Zeile der schließenden Klammer der Definition; bei weichen Definitionen gleich <paramref name="Line"/>; 0 nur in alten Speicherständen.</param>
/// <param name="Polled">Nur bei Events: das Spiel fragt es regelmäßig ab (Feld aus <see cref="ScriptCheckRules.SelfFiringFields"/>, oder ohne <see cref="ScriptCheckRules.TriggeredOnlyField"/> <c>= yes</c>). Engine-Events allein zählen nicht.</param>
public sealed record CheckDefinition(string Name, CheckKind Kind, int Line, bool SelfFiring = false, bool Soft = false, int EndLine = 0, bool Polled = false);

/// <summary>
/// Ein Verweis im Skript: Event-Aufruf, Erwähnung eines bekannten Namens oder Aufrufform eines Skripteffekts/-triggers.
/// <paramref name="TargetKind"/> ist bei Event-Aufrufen <see cref="CheckKind.Event"/>, bei Aufrufformen des Mod-Durchgangs
/// <see cref="CheckKind.Effect"/> (Effekt oder Trigger), bei Erwähnungen die Art der erwähnten Definition.
/// </summary>
/// <param name="Line">1-basierte Zeile.</param>
/// <param name="Owner">Name der umschließenden Definition auf oberster Ebene, sonst der Schlüssel oberster Ebene; null bei anonymen Blöcken.</param>
/// <param name="Conditional">Ein Vorfahr unterhalb des Besitzers ist ein Bedingungsschlüssel, oder der Verweis steht in <c>random_events</c>.</param>
/// <param name="CallForm">Der Verweis steht in Aufrufform (Event-Aufruf, Schlüssel im Skriptkontext oder in Effekt-/Trigger-Dateien).</param>
/// <param name="Parent">Nur Aufrufformen des Mod-Durchgangs: Schlüssel des direkten Elternknotens (null bei anonymen Blöcken).</param>
/// <param name="CheckedBlock">
/// Äußerster Vorfahr-Schlüssel unterhalb des Besitzers, der für die Datei ein ständig geprüfter Block ist (<see cref="ScriptCheckPerformanceRules.AlwaysCheckedBlocks"/>
/// des Ordners, in Event-Dateien <see cref="ScriptCheckPerformanceRules.PolledEventBlocks"/>); sonst null.
/// </param>
public sealed record CheckRef(string Target, CheckKind TargetKind, int Line, string? Owner, CheckKind OwnerKind, bool Conditional, bool CallForm, string? Parent = null, string? CheckedBlock = null);

/// <summary>Ein Block mit einem teuren globalen Iterator (<see cref="ScriptCheckPerformanceRules.GlobalIterators"/>).</summary>
/// <param name="Line">1-basierte Zeile des Iterator-Schlüssels.</param>
/// <param name="Owner">Wie <see cref="CheckRef.Owner"/>.</param>
/// <param name="CheckedBlock">Wie <see cref="CheckRef.CheckedBlock"/>.</param>
/// <param name="Inner">Name des ersten globalen Iterators in diesem (Textreihenfolge); null = keiner.</param>
/// <param name="UnderGlobal">Die Stelle liegt in einem anderen globalen Iterator.</param>
public sealed record IteratorSite(string Iterator, int Line, string? Owner, CheckKind OwnerKind, string? CheckedBlock, string? Inner, bool UnderGlobal);

/// <summary>Ergebnis des Lesens einer Datei: Definitionen und Event-Aufrufe. Lässt sich mit System.Text.Json speichern und laden.</summary>
public sealed class CheckScan
{
    public List<CheckDefinition> Definitions { get; init; } = new();

    /// <summary>Event-Aufrufe; Ziele mit <c>$</c> nur als <c>namespace.…$…</c> (Parameter-Aufruf, steht für irgendein Event des Namespace).</summary>
    public List<CheckRef> EventCalls { get; init; } = new();

    /// <summary>Über Parameter zusammengesetzte Namen der Datei (<c>rvt_tmpl_$X$_effect</c>), je Wort und Besitzer einmal; siehe <see cref="TemplateMatcher"/>.</summary>
    public List<CheckTemplate> Templates { get; init; } = new();

    /// <summary>Jeder Block mit einem globalen Iterator aus <see cref="ScriptCheckRules.Performance"/>; leer ohne Leistungsregeln.</summary>
    public List<IteratorSite> Iterators { get; init; } = new();

    /// <summary>
    /// Nur in On-Action-Dateien: Aufrufe weiterer On-Actions aus den Listen <see cref="ScriptCheckPerformanceRules.OnActionCallLists"/>
    /// (<see cref="CheckRef.TargetKind"/> = <see cref="CheckKind.OnAction"/>); leer ohne Leistungsregeln.
    /// </summary>
    public List<CheckRef> OnActionCalls { get; init; } = new();

    /// <summary>
    /// Aufrufformen in ständig geprüften Blöcken außerhalb des Skriptkontexts (Gewichtungsblöcke wie <c>ai_will_do = { modifier = { x = yes } }</c>):
    /// Schlüssel mit <c>yes</c>/<c>no</c> oder Parameterblock, wie <see cref="ScriptCheckReader.ReadBlockCalls"/> sie erkennt
    /// (<see cref="CheckRef.TargetKind"/> = <see cref="CheckKind.Effect"/>, <see cref="CheckRef.CheckedBlock"/> gesetzt). Eingebaute Namen bleiben drin;
    /// nur <see cref="PerformanceAnalyzer"/> liest die Liste und verknüpft nur Namen mit Definition. Leer ohne Leistungsregeln.
    /// </summary>
    public List<CheckRef> CheckedCalls { get; init; } = new();
}

/// <summary>Ein Vorlagen-Wort mit festem Text (<see cref="TemplateMatcher.WordsOf"/>) und der Definition oder dem Schlüssel oberster Ebene, in dem es steht.</summary>
public sealed record CheckTemplate(string Token, string? Owner, CheckKind OwnerKind);
