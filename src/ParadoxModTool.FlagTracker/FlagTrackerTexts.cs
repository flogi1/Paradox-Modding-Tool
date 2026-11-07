using System.Globalization;
using ParadoxModTool.Localization;

namespace ParadoxModTool.FlagTracker;

/// <summary>
/// Texte des Flag-Trackers. Die Schlüssel stehen in <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche Text hier ist der
/// Rückfall und muss mit de.json übereinstimmen (Test <c>FlagTrackerTextsTests</c>). Jeder Aufruf von <c>T</c>/<c>F</c> steht in einer Zeile.
/// </summary>
public static class FlagTrackerTexts
{
    public static string Menu => T("flagtracker.menu", "Flag-Tracker");

    public static string MenuTip => T("flagtracker.menuTip", "Zeigt, wo ein Flag oder eine Variable gesetzt, gelesen und gelöscht wird.");

    public static string NotAvailable => T("flagtracker.notAvailable", "Der Flag-Tracker kommt für dieses Spiel in einer späteren Runde.");

    public static string WindowTitle => T("flagtracker.title", "Flag-Tracker");

    public static string SearchTip => T("flagtracker.searchTip", "Name mit * als Platzhalter, z. B. iv2_ideagroup_select_*");

    public static string ScopePlayset => T("flagtracker.scope.playset", "Playset");

    public static string ScopeAll => T("flagtracker.scope.all", "Alle installierten Mods");

    public static string Refresh => T("flagtracker.refresh", "Aktualisieren");

    public static string Cancel => T("flagtracker.cancel", "Abbrechen");

    public static string TabOverview => T("flagtracker.tab.overview", "Übersicht");

    public static string TabSites => T("flagtracker.tab.sites", "Alle Stellen");

    public static string ColName => T("flagtracker.col.name", "Name");

    public static string ColKind => T("flagtracker.col.kind", "Art");

    public static string ColSet => T("flagtracker.col.set", "gesetzt");

    public static string ColRead => T("flagtracker.col.read", "gelesen");

    public static string ColRemove => T("flagtracker.col.remove", "gelöscht");

    public static string ColHint => T("flagtracker.col.hint", "Hinweis");

    public static string ColOrigin => T("flagtracker.col.origin", "Herkunft");

    public static string ColRole => T("flagtracker.col.role", "Rolle");

    public static string ColFile => T("flagtracker.col.file", "Datei");

    public static string ColVia => T("flagtracker.col.via", "Aufruf");

    /// <param name="family">Nur für <see cref="FlagKind.ScopeFlag"/> genutzt (Flag-Familie); null ergibt "?".</param>
    public static string Kind(FlagKind kind, string? family) => kind switch
    {
        FlagKind.Variable => T("flagtracker.kind.variable", "Variable"),
        FlagKind.GlobalVariable => T("flagtracker.kind.globalVariable", "globale Variable"),
        FlagKind.LocalVariable => T("flagtracker.kind.localVariable", "lokale Variable"),
        FlagKind.VariableList => T("flagtracker.kind.variableList", "Variablenliste"),
        FlagKind.GlobalVariableList => T("flagtracker.kind.globalVariableList", "globale Variablenliste"),
        FlagKind.LocalVariableList => T("flagtracker.kind.localVariableList", "lokale Variablenliste"),
        FlagKind.ScopeFlag => F("flagtracker.kind.scopeFlag", "Flag ({0})", family ?? "?"),
        FlagKind.DefinitionFlag => T("flagtracker.kind.definitionFlag", "Definitionsflag"),
        FlagKind.SymbolValue => T("flagtracker.kind.symbolValue", "Symbolwert"),
        FlagKind.EventTarget => T("flagtracker.kind.eventTarget", "Event-Target"),
        FlagKind.GlobalEventTarget => T("flagtracker.kind.globalEventTarget", "globales Event-Target"),
        FlagKind.TempVariable => T("flagtracker.kind.tempVariable", "temporäre Variable"),
        FlagKind.Array => T("flagtracker.kind.array", "Array"),
        FlagKind.TempArray => T("flagtracker.kind.tempArray", "temporäres Array"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string Role(FlagRole role) => role switch
    {
        FlagRole.Set => T("flagtracker.role.set", "gesetzt"),
        FlagRole.Read => T("flagtracker.role.read", "gelesen"),
        FlagRole.Remove => T("flagtracker.role.remove", "gelöscht"),
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public static string Origin(FlagOrigin origin) => origin switch
    {
        FlagOrigin.ActiveMod => T("flagtracker.origin.activeMod", "Mod"),
        FlagOrigin.OtherMod => T("flagtracker.origin.otherMod", "Anderer Mod"),
        FlagOrigin.Vanilla => T("flagtracker.origin.vanilla", "Vanilla"),
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
    };

    public static string NeverSet => T("flagtracker.hint.neverSet", "nie gesetzt");

    public static string NeverRead => T("flagtracker.hint.neverRead", "nie gelesen");

    public static string MaybeSetByPattern => T("flagtracker.hint.maybeSet", "evtl. über Muster gesetzt");

    public static string MaybeReadByPattern => T("flagtracker.hint.maybeRead", "evtl. über Muster gelesen");

    public static string PatternGroup(int n) => F("flagtracker.group.pattern", "über Muster (nicht sicher) ({0})", n);

    public static string NameGroup(string name, int n) => F("flagtracker.group.name", "{0} ({1})", name, n);

    public static string Via(string definition, string file, int line) => F("flagtracker.via", "über {0} – im Effekt {1}:{2}", definition, file, line.ToString(CultureInfo.InvariantCulture));

    /// <param name="builtLocal">Zeitpunkt des Aufbaus in Ortszeit; wird mit der aktuellen Kultur formatiert (wie die anderen Stand-Texte).</param>
    public static string Status(DateTime builtLocal, int files, int unread) => F("flagtracker.status", "Stand {0:g}, {1} Dateien, {2} nicht lesbar", builtLocal, files, unread);

    public static string StatusNoIndex => T("flagtracker.status.none", "Verzeichnis wird aufgebaut");

    public static string StatusFailed(string message) => F("flagtracker.status.failed", "Aufbau fehlgeschlagen: {0}", message);

    public static string StatusCancelled => T("flagtracker.status.cancelled", "Aufbau abgebrochen, der letzte Stand bleibt");

    public static string StatusCancelledNoIndex => T("flagtracker.status.cancelledNoIndex", "Aufbau abgebrochen");

    public static string NoActiveMod => T("flagtracker.status.noActiveMod", "Keine aktive Mod gewählt, Warnungen sind aus");

    public static string NoGameFolder => T("flagtracker.status.noGameFolder", "Spielordner unbekannt, Vanilla fehlt im Verzeichnis");

    public static string Phase(BuildPhase phase, int done, int total) => phase switch
    {
        BuildPhase.Reading => F("flagtracker.phase.reading", "Dateien lesen {0}/{1}", done, total),
        BuildPhase.Calls => F("flagtracker.phase.calls", "Aufrufe sammeln {0}/{1}", done, total),
        BuildPhase.Resolving => T("flagtracker.phase.resolving", "Namen auflösen"),
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    public static string Truncated => T("flagtracker.truncated", "Nur die ersten 5000 Namen, bitte genauer suchen");

    public static string WarningsOnly => T("flagtracker.warningsOnly", "Leeres Suchfeld: alle Namen mit Warnung aus der aktiven Mod");

    public static string PlaysetNote(string name) => F("flagtracker.status.playset", "Playset: {0}", name);

    public static string OpenFailed(string message) => F("flagtracker.openFailed", "Datei konnte nicht geöffnet werden: {0}", message);

    public static string ContextSearch(string word) => F("flagtracker.context", "Im Flag-Tracker suchen: {0}", word);

    public static string UnreadTip(int n) => F("flagtracker.unreadTip", "{0} Dateien konnten nicht gelesen werden", n);

    /// <summary>Hinweis eines ausgeblendeten Namens: „ausgeblendet“ ohne Warnung, sonst die Warnung mit „(ausgeblendet)“ dahinter.</summary>
    public static string HiddenSuffix(string? hint) => hint is null ? Hidden : F("flagtracker.hide.suffix", "{0} (ausgeblendet)", hint);

    public static string Hidden => T("flagtracker.hide.hidden", "ausgeblendet");

    public static string HideMenu => T("flagtracker.hide.menu", "Ausblenden");

    /// <summary>Menüeintrag und Fenstertitel; im Menü hängt das Fenster " …" an (Texte enden nie mit Auslassungspunkten).</summary>
    public static string HideSimilar => T("flagtracker.hide.similar", "Ähnliche ausblenden");

    public static string HideSimilarPrompt => T("flagtracker.hide.similarPrompt", "Name oder Muster mit * als Platzhalter. Alle passenden Namen verschwinden aus der Warnliste dieser Mod.");

    public static string Unhide => T("flagtracker.hide.unhide", "Einblenden");

    public static string HideNeedsActiveMod => T("flagtracker.hide.noActiveMod", "Ausblenden braucht eine aktive Mod; die Regeln gelten je Mod.");

    public static string ShowHidden(int n) => F("flagtracker.hide.showHidden", "Ausgeblendete zeigen ({0})", n);

    public static string ShowHiddenTip => T("flagtracker.hide.showHiddenTip", "Zeigt bei leerem Suchfeld auch die ausgeblendeten Warnungen. Eine gezielte Suche zeigt sie immer.");

    /// <summary>Knopf und Fenstertitel; am Knopf hängt das Fenster " …" an.</summary>
    public static string Rules => T("flagtracker.hide.rules", "Ausblendregeln");

    public static string RulesFor(string mod) => F("flagtracker.hide.rulesFor", "Regeln der aktiven Mod {0}", mod);

    public static string NoRules => T("flagtracker.hide.noRules", "Noch keine Regeln.");

    public static string SaveFailed(string message) => F("flagtracker.hide.saveFailed", "Ausblendregeln konnten nicht gespeichert werden: {0}", message);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
