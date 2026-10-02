using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>
/// Texte der Fenster "Mod-Kompatibilität" und des Fortschrittsfensters. Die Schlüssel stehen in <c>locales/de.json</c> und
/// <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class ModCompatWindowTexts
{
    public static string Title => T("modcompat.window.title", "Mod-Kompatibilität");

    public static string ProgressTitle => T("modcompat.window.progressTitle", "Mod-Kompatibilität wird geprüft");

    // Linke Hälfte: Set.
    public static string SetHeader => T("modcompat.window.setHeader", "Kompatibilitätsset");

    public static string NewSet => T("modcompat.window.newSet", "Neu");

    public static string EditDescription => T("modcompat.window.editDescription", "Beschreibung ändern");

    public static string LoadOrderHint => T("modcompat.window.loadOrderHint", "Weiter unten in der Liste = später geladen = gewinnt. Das Set gehört PMT und ist unabhängig vom Playset des Spiels; dort wird nichts geändert.");

    public static string FromRegisterTip => T("modcompat.window.fromRegister.tip", "Eigene Mods aus dem Fenster Mods hinzufügen (auch Dev-Branches und Submods)");

    public static string FromWorkshopTip => T("modcompat.window.fromWorkshop.tip", "Installierte Workshop-Mods hinzufügen (nur lesend)");

    public static string FolderTip => T("modcompat.window.folder.tip", "Einen beliebigen Mod-Ordner hinzufügen");

    public static string ImportPlayset => T("modcompat.window.importPlayset", "Playset importieren");

    public static string ImportPlaysetTip => T("modcompat.window.importPlayset.tip", "Die aktivierten Mods eines Spiel-Playsets als Kopie übernehmen (in dessen Ladereihenfolge)");

    public static string Up => T("modcompat.window.up", "Hoch");

    public static string Down => T("modcompat.window.down", "Runter");

    public static string CompareColumn => T("modcompat.window.compareColumn", "Vergleich");

    public static string CompareColumnTip => T("modcompat.window.compareColumn.tip", "Bestimmt bei Gegenseite „Markierte in der Liste“, welche Mods verglichen werden");

    public static string NumberColumn => T("modcompat.window.numberColumn", "Nr");

    public static string OriginColumn => T("modcompat.window.originColumn", "Herkunft");

    public static string OwnMod => T("modcompat.window.ownMod", "Eigene Mod");

    public static string Missing(string name) => F("modcompat.window.missing", "{0} (fehlt)", name);

    // Rechte Hälfte: Prüfung.
    public static string FocusTip => T("modcompat.window.focus.tip", "Die Mod, die geprüft wird (meist die eigene). Jeder Fund betrifft sie.");

    public static string Counterpart => T("modcompat.window.counterpart", "Gegenseite");

    public static string CounterpartTip => T("modcompat.window.counterpart.tip", "„Alle im Set“ vergleicht alle Mods, unabhängig vom Häkchen Vergleich. „Markierte in der Liste“ nur die Mods mit gesetztem Häkchen Vergleich.");

    public static string AllInSet => T("modcompat.window.allInSet", "Alle im Set");

    public static string MarkedInList => T("modcompat.window.markedInList", "Markierte in der Liste");

    public static string Run => T("modcompat.window.run", "Prüfen");

    public static string SenseOnly => T("modcompat.window.senseOnly", "Nur Meine Mod (Sinnhaftigkeit)");

    public static string SenseOnlyTip => T("modcompat.window.senseOnly.tip", "Nur Funde, die zeigen, wo Mod 1 mehr überschreibt als nötig");

    public static string HideUncertain => T("modcompat.window.hideUncertain", "Unsichere ausblenden");

    public static string HideUncertainTip => T("modcompat.window.hideUncertain.tip", "Funde ausblenden, die nicht durch das Wiki gesichert sind");

    public static string FilterLabel => T("modcompat.window.filterLabel", "Pfad oder Schlüssel enthält:");

    public static string SaveReport => T("modcompat.window.saveReport", "Bericht speichern");

    public static string SaveReportTip => T("modcompat.window.saveReport.tip", "Die angezeigten Funde als Textdatei (Markdown) speichern, als Planungshilfe für einen Kompatibilitätspatch");

    public static string Rules => T("modcompat.window.rules", "Überschreib-Regeln");

    public static string RulesTip => T("modcompat.window.rules.tip", "Regeltabelle für dieses Spiel ansehen und anpassen: welcher Ordner überschreibt wie (Datei, Schlüssel, erster oder letzter gewinnt)");

    public static string Compare => T("modcompat.window.compare", "Vergleichen");

    public static string CompareTip => T("modcompat.window.compare.tip", "Zwei Fassungen der Fundstelle im Vergleichsfenster öffnen (nur lesend, auch per Doppelklick)");

    public static string OpenInEditor => T("modcompat.window.openInEditor", "Im Editor öffnen");

    public static string OpenInEditorTip => T("modcompat.window.openInEditor.tip", "Die Datei der eigenen Mod im Editor öffnen (fremde Mods nur über Vergleichen)");

    public static string SeverityColumn => T("modcompat.window.severityColumn", "Schwere");

    public static string KindColumn => T("modcompat.window.kindColumn", "Art");

    public static string PathColumn => T("modcompat.window.pathColumn", "Pfad");

    public static string KeyColumn => T("modcompat.window.keyColumn", "Schlüssel");

    public static string WinnerColumn => T("modcompat.window.winnerColumn", "Gewinner");

    public static string SuggestionColumn => T("modcompat.window.suggestionColumn", "Empfehlung");

    public static string CertaintyColumn => T("modcompat.window.certaintyColumn", "Sicherheit");

    // Meldungen und Status.
    public static string DescriptionPrompt => T("modcompat.window.descriptionPrompt", "Beschreibung des Sets:");

    public static string NewSetTitle => T("modcompat.window.newSetTitle", "Neues Set");

    public static string SetNamePrompt => T("modcompat.window.setNamePrompt", "Name des Sets:");

    public static string SetExists(string name) => F("modcompat.window.setExists", "Ein Set \"{0}\" gibt es schon.", name);

    public static string RenameSetTitle => T("modcompat.window.renameSetTitle", "Set umbenennen");

    public static string DeleteSetConfirm(string name) => F("modcompat.window.deleteSetConfirm", "Das Set \"{0}\" löschen? Die Mods selbst bleiben unberührt.", name);

    public static string ModFolderNotFound(string mod) => F("modcompat.window.modFolderNotFound", "Der Ordner der Mod \"{0}\" wurde nicht gefunden.", mod);

    public static string AlreadyInSet(string mod, string set) => F("modcompat.window.alreadyInSet", "\"{0}\" ist schon im Set \"{1}\".", mod, set);

    public static string AddNewModLabel => T("modcompat.window.addNewMod", "+ Neue Mod hinzufügen (Ordner wählen)");

    public static string FromRegisterPrompt(string addNewLabel) => F("modcompat.window.fromRegisterPrompt", "Welche Mods sollen ins Set? Noch nicht verwaltete Mods können über \"{0}\" direkt angelegt werden.", addNewLabel);

    public static string SteamNotFound => T("modcompat.window.steamNotFound", "Der Steam-Ordner wurde nicht gefunden.");

    public static string NoWorkshopMods => T("modcompat.window.noWorkshopMods", "Es wurden keine (weiteren) installierten Workshop-Mods für dieses Spiel gefunden.");

    public static string FromWorkshopPrompt => T("modcompat.window.fromWorkshopPrompt", "Welche Workshop-Mods sollen ins Set? Sie werden nur gelesen.");

    public static string FolderAlreadyInSet => T("modcompat.window.folderAlreadyInSet", "Dieser Ordner ist schon im Set.");

    public static string NoPlaysets => T("modcompat.window.noPlaysets", "Es wurden keine Playsets gefunden. Stelle im Menü die Playset-Datei ein.");

    public static string ImportPlaysetPrompt => T("modcompat.window.importPlaysetPrompt", "Welches Playset soll als Kopie ins Set? Es werden nur aktivierte Mods in ihrer Ladereihenfolge übernommen.");

    public static string PlaysetImported(int added, int total) => F("modcompat.window.playsetImported", "{0} von {1} aktivierten Mods wurden übernommen (vorhandene und fehlende Ordner ausgelassen).", added, total);

    public static string PickFocus => T("modcompat.window.pickFocus", "Wähle Mod 1.");

    public static string NeedTwoMods => T("modcompat.window.needTwoMods", "Das Set braucht mindestens zwei Mods.");

    public static string CounterAll => T("modcompat.window.counterAll", "alle im Set");

    public static string CounterMarked(int count) => F("modcompat.window.counterMarked", "{0} markierte", count);

    public static string NeedCounterpart => T("modcompat.window.needCounterpart", "Setze bei mindestens einer anderen Mod das Häkchen \"Vergleich\" für die Gegenseite.");

    public static string Checking => T("modcompat.window.checking", "Prüfe…");

    public static string HideResolved => T("modcompat.window.hideResolved", "Gelöste ausblenden");

    public static string HideResolvedTip => T("modcompat.window.hideResolved.tip", "Funde ausblenden, die per Rechtsklick als gelöst markiert sind. Ändert eine beteiligte Mod die Stelle, erscheint der Fund wieder.");

    public static string MarkResolved => T("modcompat.window.markResolved", "Als gelöst markieren");

    public static string UnmarkResolved => T("modcompat.window.unmarkResolved", "Markierung aufheben");

    public static string ResolvedColumn => T("modcompat.window.resolvedColumn", "Gelöst");

    public static string ResolvedDetail => T("modcompat.window.resolvedDetail", "Als gelöst markiert.");

    public static string ResolvedSaveFailed(string error) => F("modcompat.window.resolvedSaveFailed", "Die Markierung konnte nicht gespeichert werden:\n{0}", error);

    public static string ShownCountResolved(int shown, int total, int hidden) => F("modcompat.window.shownCountResolved", "{0} von {1} Funden angezeigt, {2} als gelöst ausgeblendet.", shown, total, hidden);

    public static string Subtitle(string set, string focus, string counterpart, DateTime time) => F("modcompat.window.subtitle", "Set: {0} · Mod 1: {1} · Gegenseite: {2} · {3:yyyy-MM-dd HH:mm}", set, focus, counterpart, time);

    public static string CheckFailed(string error) => F("modcompat.window.checkFailed", "Die Prüfung ist fehlgeschlagen:\n{0}", error);

    public static string HiddenVanilla(int count) => F("modcompat.hiddenVanilla", "Mods blenden {0} Vanilla-Ordner aus (replace_path)", count);

    public static string HiddenVanillaAndMods(int count) => F("modcompat.hiddenVanillaAndMods", "Mods blenden {0} Ordner aus (replace_path; Vanilla und früher geladene Mods)", count);

    public static string ShownCount(int shown, int total) => F("modcompat.window.shownCount", "{0} von {1} Funden angezeigt.", shown, total);

    public static string NothingToSave => T("modcompat.window.nothingToSave", "Es gibt keine angezeigten Funde zum Speichern.");

    public static string ReportFileName(string set) => F("modcompat.window.reportFileName", "Kompatibilitaet {0}.md", set);

    public static string ReportSaved(string file) => F("modcompat.window.reportSaved", "Bericht gespeichert: {0}", file);

    public static string ReportFailed(string error) => F("modcompat.window.reportFailed", "Der Bericht konnte nicht gespeichert werden:\n{0}", error);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
