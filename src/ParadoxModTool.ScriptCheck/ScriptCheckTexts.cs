using ParadoxModTool.Localization;

namespace ParadoxModTool.ScriptCheck;

/// <summary>
/// Texte der Skriptprüfung. Die Schlüssel stehen in <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche Text hier ist der
/// Rückfall und muss mit de.json übereinstimmen (Test <c>ScriptCheckTextsTests</c>). Jeder Aufruf von <c>T</c>/<c>F</c> steht in einer Zeile.
/// </summary>
public static class ScriptCheckTexts
{
    public static string Menu => T("scriptcheck.menu", "Skript-Prüfung");

    public static string MenuTip => T("scriptcheck.menuTip", "Findet Verweise ins Leere, tote Events, ungenutzte Bausteine, doppelte Event-IDs und Zirkel in der aktiven Mod.");

    public static string WindowTitle => T("scriptcheck.title", "Skript-Prüfung");

    public static string Kind(FindingKind kind) => kind switch
    {
        FindingKind.BrokenReference => T("scriptcheck.kind.brokenReference", "Verweis ins Leere"),
        FindingKind.DeadEvent => T("scriptcheck.kind.deadEvent", "Totes Event"),
        FindingKind.UnusedBlock => T("scriptcheck.kind.unusedBlock", "Ungenutzter Baustein"),
        FindingKind.DuplicateEvent => T("scriptcheck.kind.duplicateEvent", "Doppelte Event-ID"),
        FindingKind.EffectCycle => T("scriptcheck.kind.effectCycle", "Effekt-Zirkel"),
        FindingKind.EventCycle => T("scriptcheck.kind.eventCycle", "Event-Zirkel"),
        FindingKind.ExpensiveIterator => T("scriptcheck.kind.expensiveIterator", "Teurer Iterator"),
        _ => kind.ToString(),
    };

    public static string Severity(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Error => T("scriptcheck.severity.error", "Fehler"),
        FindingSeverity.Warning => T("scriptcheck.severity.warning", "Warnung"),
        FindingSeverity.Hint => T("scriptcheck.severity.hint", "Hinweis"),
        _ => severity.ToString(),
    };

    public static string Status(DateTime builtLocal, int files, int unread, int findings) => F("scriptcheck.status", "Stand {0:g}, {1} Dateien, {2} nicht lesbar, {3} Funde", builtLocal, files, unread, findings);

    public static string StatusFailed(string message) => F("scriptcheck.status.failed", "Prüfung fehlgeschlagen: {0}", message);

    public static string StatusCancelled => T("scriptcheck.status.cancelled", "Prüfung abgebrochen, der letzte Stand bleibt");

    public static string NoActiveMod => T("scriptcheck.noActiveMod", "Keine aktive Mod gewählt: Es gibt nichts zu prüfen.");

    public static string NoBuiltIns => T("scriptcheck.noBuiltIns", "Script-Doku fehlt: Verweise auf Effekte/Trigger werden nicht geprüft.");

    public static string NoRules => T("scriptcheck.noRules", "Für dieses Spiel gibt es noch keine Regeln für die Skript-Prüfung.");

    public static string NoPerformanceRules => T("scriptcheck.perf.noRules", "Keine Leistungsregeln für dieses Spiel");

    public static string TargetMissing(string directory) => F("scriptcheck.perf.targetMissing", "Ziel nicht gefunden oder leer: {0}", directory);

    public static string PerfNested => T("scriptcheck.perf.nested", "Verschachtelt");

    public static string PerfSuggestNarrower(string names) => F("scriptcheck.perf.suggest.narrower", "Engeren Iterator nutzen: {0}", names);

    public static string PerfSuggestCheapCheck => T("scriptcheck.perf.suggest.cheapCheck", "Billige Prüfung vor den Iterator ziehen");

    public static string PerfSuggestCache => T("scriptcheck.perf.suggest.cache", "Ergebnis in Variable oder Liste zwischenspeichern");

    public static string PerfSuggestRarerPulse => T("scriptcheck.perf.suggest.rarerPulse", "Arbeit auf einen selteneren Puls legen");

    public static string PerfRateDaily => T("scriptcheck.perf.rate.daily", "täglich");

    public static string PerfRateMonthly => T("scriptcheck.perf.rate.monthly", "monatlich");

    public static string PerfRateChecked => T("scriptcheck.perf.rate.checked", "ständig geprüft");

    public static string PerfRateYearly => T("scriptcheck.perf.rate.yearly", "jährlich");

    public static string PerfRateRare => T("scriptcheck.perf.rate.rare", "selten");

    public static string PerfReportHeader(string target, string game, int count) => F("scriptcheck.perf.report.header", "Leistungsbefunde: {0} ({1}), {2} Funde", target, game, count);

    public static string PerfReportPath => T("scriptcheck.perf.report.path", "Weg");

    public static string PerfReportInner => T("scriptcheck.perf.report.inner", "Innerer Iterator");

    public static string PerfReportRate => T("scriptcheck.perf.report.rate", "Takt");

    public static string PerfReportSuggestion => T("scriptcheck.perf.report.suggestion", "Vorschlag");

    public static string TabFindings => T("scriptcheck.tab.findings", "Befunde");

    public static string TabCalls => T("scriptcheck.tab.calls", "Wer ruft wen");

    public static string TabPerformance => T("scriptcheck.tab.performance", "Leistung anderer Ziele");

    public static string TargetLabel => T("scriptcheck.perf.targetLabel", "Ziel:");

    public static string PerfTargetKind(PerformanceTargetKind kind) => kind switch
    {
        PerformanceTargetKind.Vanilla => T("scriptcheck.perf.kind.vanilla", "Vanilla"),
        PerformanceTargetKind.PlaysetMod => T("scriptcheck.perf.kind.playset", "Playset"),
        _ => T("scriptcheck.perf.kind.installed", "Installiert"),
    };

    public static string CheckPerformance => T("scriptcheck.perf.check", "Leistung prüfen");

    public static string CopyReport => T("scriptcheck.perf.copyReport", "Als Bericht kopieren");

    public static string ColPath => T("scriptcheck.col.path", "Weg");

    public static string PerfNoResult => T("scriptcheck.perf.noResult", "Für dieses Ziel gibt es noch kein Ergebnis: \"Leistung prüfen\" startet die Prüfung.");

    public static string NoTargets => T("scriptcheck.perf.noTargets", "Kein Ziel zur Auswahl.");

    public static string ColSeverity => T("scriptcheck.col.severity", "Schwere");

    public static string ColKind => T("scriptcheck.col.kind", "Art");

    public static string ColName => T("scriptcheck.col.name", "Name");

    public static string ColPlace => T("scriptcheck.col.place", "Datei:Zeile");

    public static string ColRelated => T("scriptcheck.col.related", "Wer ruft / wird gerufen");

    public static string ColConditional => T("scriptcheck.col.conditional", "Bedingt");

    public static string Callers => T("scriptcheck.callers", "Aufrufer");

    public static string Callees => T("scriptcheck.callees", "Gerufene");

    public static string Definitions => T("scriptcheck.definitions", "Definiert in:");

    public static string Yes => T("scriptcheck.yes", "ja");

    public static string Refresh => T("scriptcheck.refresh", "Aktualisieren");

    public static string Cancel => T("scriptcheck.cancel", "Abbrechen");

    public static string ModLabel => T("scriptcheck.modLabel", "Mod:");

    public static string SearchFindingsTip => T("scriptcheck.searchFindingsTip", "Sucht in Name und Datei der Befunde");

    public static string SearchCallsTip => T("scriptcheck.searchCallsTip", "Name eingeben: genau oder als Teil, über alle Arten");

    public static string FilterAll => T("scriptcheck.filterAll", "Alle");

    public static string ShowHidden => T("scriptcheck.showHidden", "Ausgeblendete zeigen");

    public static string Hide => T("scriptcheck.hide", "Ausblenden");

    public static string Unhide => T("scriptcheck.unhide", "Wieder einblenden");

    public static string OpenPlace => T("scriptcheck.open", "Stelle öffnen");

    public static string CopyName => T("scriptcheck.copyName", "Namen kopieren");

    public static string ShowCalls => T("scriptcheck.showCalls", "Wer ruft wen zeigen");

    public static string NobodyCalls => T("scriptcheck.nobodyCalls", "niemand");

    public static string Coverage => T("scriptcheck.coverage", "Nicht geprüft: Blöcke außerhalb der Regellisten, fehlende Script Values.");

    public static string CallsOnlyEvents => T("scriptcheck.callsOnlyEvents", "Nur Event-Aufrufe bekannt: Erwähnungen werden nur für Namen der gewählten Mod gesammelt.");

    public static string NoCallers => T("scriptcheck.noCallers", "Keine Aufrufer gefunden.");

    public static string NoCallees => T("scriptcheck.noCallees", "Ruft nichts auf.");

    public static string NoMods => T("scriptcheck.noMods", "Keine Mod zur Auswahl.");

    /// <summary>Tooltip der Mod-Auswahl und von "Aktualisieren", solange der Reiter "Leistung anderer Ziele" gewählt ist.</summary>
    public static string HeaderOwnModOnly => T("scriptcheck.headerOwnModOnly", "Gilt für die eigene Mod (Reiter „Befunde“)");

    /// <summary>Art eines Namens; <see cref="CheckKind.Effect"/> steht bei Aufrufen für "Effekt oder Trigger".</summary>
    public static string OtherModResult => T("scriptcheck.otherModResult", "Für diese Mod gibt es noch kein Ergebnis: \"Aktualisieren\" startet die Prüfung.");

    public static string HideListUnreadable => T("scriptcheck.hideListUnreadable", "Die Ausblende-Liste ist nicht lesbar und wird nicht überschrieben.");

    public static string NoDefinition => T("scriptcheck.noDefinition", "Keine Definition gefunden (eingebauter Befehl oder Verweis ins Leere).");

    public static string NameKind(CheckKind kind) => kind switch
    {
        CheckKind.Event => T("scriptcheck.nameKind.event", "Event"),
        CheckKind.Effect => T("scriptcheck.nameKind.effect", "Effekt/Trigger"),
        CheckKind.Trigger => T("scriptcheck.nameKind.trigger", "Trigger"),
        CheckKind.Value => T("scriptcheck.nameKind.value", "Script Value"),
        CheckKind.OnAction => T("scriptcheck.nameKind.onAction", "on_action"),
        CheckKind.Named => T("scriptcheck.nameKind.named", "Benannter Block"),
        _ => T("scriptcheck.nameKind.other", "Sonstiges"),
    };

    public static string Phase(ParadoxModTool.FlagTracker.BuildPhase phase, int done, int total) => phase switch
    {
        ParadoxModTool.FlagTracker.BuildPhase.Reading => F("scriptcheck.phase.reading", "Dateien lesen {0}/{1}", done, total),
        ParadoxModTool.FlagTracker.BuildPhase.Calls => F("scriptcheck.phase.calls", "Erwähnungen suchen {0}/{1}", done, total),
        _ => F("scriptcheck.phase.resolving", "Eigene Mod auswerten {0}/{1}", done, total),
    };

    public static string HideSaveFailed(string message) => F("scriptcheck.hideSaveFailed", "Ausblende-Liste nicht gespeichert: {0}", message);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
