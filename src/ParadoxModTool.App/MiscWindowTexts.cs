using ParadoxModTool.Localization;
using ParadoxModTool.Settings;

namespace ParadoxModTool.App;

/// <summary>
/// Texte kleinerer Fenster und Leisten (Über, Startbild, Theme-Editor, Kategorien, Notizen/Lesezeichen, Formatieren, Gehe zu Zeile,
/// Script-Datenbank, Speicherorte, Vanilla-Diff). Die Keys stehen in <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche
/// Text hier ist der Rückfall und muss mit de.json übereinstimmen. Jeder Aufruf steht auf einer Zeile.
/// </summary>
public static class MiscWindowTexts
{
    // Über-Fenster
    public static string AboutTitle => T("about.title", "Über Paradox Modding Tool");

    public static string AboutTagline => T("about.tagline", "Editor und Werkzeugkasten für Mods der Paradox-Spiele (Schwerpunkt Europa Universalis V).");

    public static string AboutAiLabel => T("about.ai.label", "Hinweis zu KI:");

    public static string AboutAiText => T("about.ai.text", "Dieses Programm wurde mit Unterstützung von KI (Claude von Anthropic) entwickelt.");

    public static string AboutLicenseLabel => T("about.license.label", "Lizenz:");

    public static string AboutLicenseText => T("about.license.text", "MIT-Lizenz, Copyright (c) 2026 flogi.");

    public static string AboutLicenseFull => T("about.license.full", "Volltext der Lizenz");

    public static string AboutComponentsLabel => T("about.components.label", "Verwendete Komponenten:");

    public static string AboutComponentsText => T("about.components.text", "AvalonEdit (MIT-Lizenz, ICSharpCode).");

    public static string AboutProjectPage => T("about.projectPage", "Projektseite:");

    public static string AboutSupport => T("about.support", "Unterstützen:");

    public static string AboutNotAffiliated => T("about.notAffiliated", "Paradox Modding Tool ist ein inoffizielles Werkzeug von Moddern für Modder und steht in keiner Verbindung zu Paradox Interactive. Spielnamen und Marken gehören ihren jeweiligen Inhabern.");

    public static string AboutCreatedBy(string creator) => F("about.createdBy", "Erstellt von {0}", creator);

    public static string AboutBrowserFailed(string message) => F("about.browserFailed", "Der Browser konnte nicht geöffnet werden: {0}", message);

    // Startbild
    public static string SplashStarting => T("splash.starting", "Wird gestartet…");

    public static string SplashBuildingWindow => T("splash.buildingWindow", "Fenster wird aufgebaut…");

    // Theme-Editor
    public static string ThemeEditorTitle => SettingsTexts.ThemeEditor;

    public static string ThemeNewCopy => T("theme.editor.newCopy", "Neu (Kopie)");

    public static string ThemeNewCopyTip => T("theme.editor.newCopy.tip", "Legt eine eigene, bearbeitbare Kopie des gewählten Themes an");

    public static string ThemeDeleteTip => T("theme.editor.delete.tip", "Löscht das gewählte eigene Theme");

    public static string ThemeImport => T("theme.editor.import", "Importieren");

    public static string ThemeImportTip => T("theme.editor.import.tip", "Liest ein Theme aus einer Datei (JSON) als neues eigenes Theme");

    public static string ThemeExport => T("theme.editor.export", "Exportieren");

    public static string ThemeExportTip => T("theme.editor.export.tip", "Speichert das gewählte Theme als Datei zum Weitergeben");

    public static string ThemeDark => T("theme.editor.dark", "Dunkles Theme");

    public static string ThemeDarkTip => T("theme.editor.dark.tip", "Schaltet die dunkle Windows-Titelleiste ein und bestimmt, aus welchem eingebauten Theme fehlende Farben ergänzt werden");

    public static string ThemeBuiltInInfo => T("theme.editor.builtInInfo", "Eingebaute Themes sind schreibgeschützt. Mit „Neu (Kopie)“ entsteht eine eigene Version, die du frei anpassen kannst.");

    public static string ThemeColorsHint => T("theme.editor.colorsHint", "Farben: #RRGGBB, bei einigen Feldern auch #AARRGGBB (mit Transparenz). Die Farben der Dateikategorien stellst du unter Einstellungen → Kategorie-Farben ein.");

    public static string ThemeBuiltInEntry(string name) => F("theme.editor.builtInEntry", "{0} (eingebaut)", name);

    public static string ThemePickColorTip => SettingsTexts.PickColorTip;

    public static string ThemeCopyName(string name) => F("theme.editor.copyName", "{0} (Kopie)", name);

    public static string ThemeDeleteConfirm(string name) => F("theme.editor.deleteConfirm", "Das Theme „{0}“ wirklich löschen?", name);

    public static string ThemeDeleteTitle => T("theme.editor.deleteTitle", "Theme löschen");

    public static string ThemeImportFilter => T("theme.editor.importFilter", "Theme-Datei (*.json)|*.json|Alle Dateien (*.*)|*.*");

    public static string ThemeImportTitle => T("theme.editor.importTitle", "Theme importieren");

    public static string ThemeInvalidFile => T("theme.editor.invalidFile", "Die Datei ist kein gültiges Theme.");

    public static string ThemeImportFailedTitle => T("theme.editor.importFailedTitle", "Import nicht möglich");

    public static string ThemeExportFilter => T("theme.editor.exportFilter", "Theme-Datei (*.json)|*.json");

    public static string ThemeExportTitle => T("theme.editor.exportTitle", "Theme exportieren");

    public static string ThemeWriteFailed(string message) => F("theme.editor.writeFailed", "Die Datei konnte nicht geschrieben werden: {0}", message);

    // Kategorien verwalten und Kategorie-Filterleiste
    public static string CategoriesTitle => SettingsTexts.ManageCategories;

    public static string CategoriesRestoreDefaults => T("categories.restoreDefaults", "Standard wiederherstellen");

    public static string CategoriesRestoreDefaultsTip => T("categories.restoreDefaults.tip", "Setzt die Liste auf die sieben eingebauten Kategorien mit ihren Standardregeln und -farben zurück (eigene Kategorien entfallen)");

    public static string CategoriesListHeading => T("categories.listHeading", "Kategorien (Reihenfolge = Priorität der Regeln)");

    public static string CategoriesNew => T("categories.new", "Neu");

    public static string CategoriesMoveUpTip => T("categories.moveUp.tip", "Nach oben (höhere Priorität)");

    public static string CategoriesMoveDownTip => T("categories.moveDown.tip", "Nach unten (niedrigere Priorität)");

    public static string CategoriesColorLabel => T("categories.colorLabel", "Farbe (Hex, z. B. #1E90FF)");

    public static string CategoriesPickColorTip => T("categories.pickColor.tip", "Farbe auswählen");

    public static string CategoriesExtensionsLabel => T("categories.extensionsLabel", "Dateiendungen (mit Komma getrennt, z. B. .txt, .gui)");

    public static string CategoriesPatternsLabel => T("categories.patternsLabel", "Muster (eines pro Zeile, * = beliebig viele Zeichen, ? = ein Zeichen)");

    public static string CategoriesRulesHint => T("categories.rulesHint", "Ein Muster gilt ab einer beliebigen Ordnergrenze, z. B. 'events/*' oder '*_decisions.txt'. Ohne Endung und Muster wird die Kategorie nur manuell vergeben. Die erste passende Kategorie in der Liste gewinnt; eine manuelle Zuweisung schlägt jede Regel.");

    public static string CategoriesColorHintBuiltIn => T("categories.colorHint.builtIn", "Eingebaute Kategorie: die Farbe gilt für das aktuelle Theme.");

    public static string CategoriesColorHintCustom => T("categories.colorHint.custom", "Eigene Kategorie: die Farbe gilt in allen Themes.");

    public static string CategoriesColorHintOther(string name) => F("categories.colorHint.other", "'{0}' erhält alle Dateien ohne passende Regel und hat selbst keine Regeln.", name);

    public static string CategoriesNewName => T("categories.newName", "Neue Kategorie");

    public static string CategoriesMessageTitle => T("categories.messageTitle", "Kategorien");

    public static string CategoriesNameRequired => T("categories.nameRequired", "Jede Kategorie braucht einen Namen.");

    public static string CategoriesInvalidColor(string name, string color) => F("categories.invalidColor", "Ungültiger Farbcode für '{0}': \"{1}\"\nBitte einen Hex-Farbcode angeben, z. B. #1E90FF.", name, color);

    public static string CategoriesInvalidColorTitle => T("categories.invalidColorTitle", "Ungültige Farbe");

    public static string CategoriesDuplicateName(string name) => F("categories.duplicateName", "Der Name '{0}' kommt mehrfach vor. Bitte eindeutige Namen wählen.", name);

    public static string CategoryFilterLabel => T("categories.filter.label", "Kategorie:");

    public static string CategoryFilterToggleTip(string name) => F("categories.filter.toggle.tip", "'{0}' ein-/ausblenden", name);

    public static string CategoryFilterHideFromBar => T("categories.filter.hideFromBar", "Aus Leiste ausblenden");

    public static string CategoryFilterButton => MenuTexts.WorkspaceFilter;

    // Notizfenster, Lesezeichen-Dialog und Notiz-Panel
    public static string NotesWindowTitle => T("notes.window.title", "Notizen");

    public static string NotesTabNote => T("notes.tab.note", "Notiz");

    public static string NotesBookmarksHint => T("notes.bookmarksHint", "Mit Strg+F2 an der Cursorposition im Editor gesetzt/entfernt. Doppelklick oder \"Springe zu\" springt zur Stelle und schließt dieses Fenster.");

    public static string NotesJumpTo => T("notes.jumpTo", "Springe zu");

    public static string NotesNoteFor(string file) => F("notes.noteFor", "Notiz zu: {0}", file);

    public static string NotesSaveFailed(string message) => F("notes.saveFailed", "Notiz konnte nicht gespeichert werden: {0}", message);

    public static string BookmarkLineWithPreview(int line, string preview) => F("bookmark.lineWithPreview", "Zeile {0}: {1}", line, preview);

    public static string BookmarkRemoveFailed(string message) => F("bookmark.removeFailed", "Bookmark konnte nicht entfernt werden: {0}", message);

    public static string BookmarkTitle => T("bookmark.title", "Lesezeichen");

    public static string BookmarkCommentLabel => T("bookmark.commentLabel", "Kommentar:");

    public static string NotesPanelEmpty => T("notes.panel.empty", "Keine Notiz, keine Lesezeichen und keine Bugs.");

    public static string NotesPanelEdit => T("notes.panel.edit", "Bearbeiten");

    public static string NotesPanelNoteHeading => T("notes.panel.noteHeading", "🗒 Notiz");

    public static string NotesPanelSavedScopes => T("notes.panel.savedScopes", "Gespeicherte Scopes");

    public static string NotesPanelSavedScopesTip => T("notes.panel.savedScopes.tip", "Namen, die an dieser Stelle mit scope:name benutzt werden können, samt Typ. Aus der Datei (save_scope_as) oder aus den gelernten Regeln (vom Spiel bereitgestellt).");

    public static string NotesPanelJumpTip => T("notes.panel.jumpTip", "Doppelklick springt zur Stelle");

    public static string NotesPanelLinePrefix => T("notes.panel.linePrefix", "Zeile ");

    public static string NotesPanelBookmarksHeading => T("notes.panel.bookmarksHeading", "🔖 Lesezeichen");

    public static string NotesPanelComment => T("notes.panel.comment", "Kommentar");

    public static string NotesPanelHeader => T("notes.panel.header", "Notizen & Lesezeichen");

    public static string NotesScopeOutside => T("notes.scope.outside", "Scope: außerhalb eines Objekts");

    public static string NotesScopeField(string path) => F("notes.scope.field", "Feld: {0}", path);

    public static string NotesScopeStart(string scope, double support) => F("notes.scope.start", "(Start-Scope {0}, Zustimmung {1:P0})", scope, support);

    public static string NotesScopeWarnings(int count) => F("notes.scope.warnings", "⚠ Warnungen ({0})", count);

    // Formatieren und Gehe zu Zeile
    public static string FormatTitle => T("format.title", "Formatieren");

    public static string FormatIntro => T("format.intro", "Wird auf das AKTIVE Dokument angewendet (über Strg+Z rückgängig zu machen, solange die Datei noch nicht gespeichert wurde).");

    public static string FormatCleanWhitespace => T("format.cleanWhitespace", "Leerzeichen/Tabs am Zeilenende entfernen, mehrfache Leerzeilen auf eine reduzieren");

    public static string FormatCleanWhitespaceTip => T("format.cleanWhitespace.tip", "Entfernt NUR überschüssigen Whitespace am Zeilenende und reduziert Läufe von 2+ Leerzeilen auf genau eine - einzelne Leerzeilen als optische Trennung bleiben erhalten, mehrfache Leerzeichen INNERHALB einer Zeile (z. B. zur Ausrichtung) bleiben ebenfalls unangetastet.");

    public static string FormatPrettyPrint => T("format.prettyPrint", "Struktur neu formatieren (einheitliche Einrückung)");

    public static string FormatPrettyPrintHint => T("format.prettyPrint.hint", "Baut den Aufbau der Datei (Schlüssel/Wert-Paare, Blöcke) komplett neu auf und vereinheitlicht dabei die Einrückung. Schlägt fehl, ohne etwas zu ändern, falls die Datei nicht als gültiges Paradox-Skript gelesen werden kann.");

    public static string FormatPrettyPrintUnavailable => T("format.prettyPrint.unavailable", "Für diesen Dateityp nicht verfügbar - die Struktur-Neuformatierung versteht nur das Paradox-Skriptformat (.txt/.gui/.gfx/.vml), nicht z. B. YML-Lokalisierungs- oder LUA-Dateien.");

    public static string GoToLineTitle => T("settings.toolbarItem.goToLine", "Gehe zu Zeile");

    public static string GoToLineLineNumber => T("format.goToLine.lineNumber", "Zeilennummer:");

    // Script-Datenbank und Scope-Regeln
    public static string ScriptDocsTitle => T("scriptdocs.title", "Script-Datenbank");

    public static string ScriptDocsChooseFolder => T("scriptdocs.chooseFolder", "Ordner wählen");

    public static string ScriptDocsChooseFolderTip => T("scriptdocs.chooseFolder.tip", "Ordner mit den Doku-Logs des Spiels (docs) festlegen");

    public static string ScriptDocsReload => T("scriptdocs.reload", "Neu einlesen");

    public static string ScriptDocsReloadTip => T("scriptdocs.reload.tip", "Die Logs erneut lesen (sonst geschieht das automatisch beim Öffnen, wenn sie sich geändert haben)");

    public static string ScriptDocsSearchTip => T("scriptdocs.search.tip", "Teil des Namens oder der Beschreibung; mit * und ? als Platzhalter wird der ganze Name verglichen (z. B. add_*_modifier)");

    public static string ScriptDocsHideOutdated => T("scriptdocs.hideOutdated", "Veraltete ausblenden");

    public static string ScriptDocsHideOutdatedTip => T("scriptdocs.hideOutdated.tip", "Veraltet: der Eintrag stand im letzten Einlesen der Logs nicht mehr. Das kann auch am aktiven Playset liegen (Einträge deaktivierter Mods fehlen dann).");

    public static string ScriptDocsScopeTip => T("scriptdocs.scope.tip", "Nur Einträge, die in diesem Scope benutzbar sind");

    public static string ScriptDocsInsert => T("scriptdocs.insert", "In Editor einfügen");

    public static string ScriptDocsInsertTip => T("scriptdocs.insert.tip", "Fügt den Namen an der Cursorposition des aktiven Dokuments ein (auch Doppelklick auf den Eintrag)");

    public static string ScriptDocsFindExamples => T("scriptdocs.findExamples", "Beispiele suchen");

    public static string ScriptDocsFindExamplesTip => T("scriptdocs.findExamples.tip", "Sucht den Namen in den Vanilla- und Mod-Dateien und merkt sich das Ergebnis");

    public static string ScriptDocsKindToggleTip(string kind) => F("scriptdocs.kind.toggle.tip", "{0}-Einträge ein-/ausblenden", kind);

    public static string ScriptDocsAllScopes => T("scriptdocs.allScopes", "(alle Scopes)");

    public static string ScriptDocsLoading => T("scriptdocs.loading", "Datenbank wird geladen…");

    public static string ScriptDocsSourceWiki(string date) => F("scriptdocs.sourceWiki", "Quelle: Wiki, Stand {0}", date);

    public static string ScriptDocsUnknown => T("scriptdocs.unknown", "unbekannt");

    public static string ScriptDocsAsOf(DateTime date) => F("scriptdocs.asOf", "Stand vom {0:g}", date);

    public static string ScriptDocsReloaded => T("scriptdocs.reloaded", "Neu eingelesen.");

    public static string ScriptDocsStatus(int entries, string source, int outdated, int isNew) => F("scriptdocs.status", "{0:N0} Einträge · {1} · {2:N0} veraltet · {3:N0} neu", entries, source, outdated, isNew);

    public static string ScriptDocsHintWikiMissing => T("scriptdocs.hint.wikiMissing", "Die mit PMT mitgelieferte Wiki-Datei für dieses Spiel fehlt oder ist beschädigt. Die Script-Datenbank bleibt deshalb leer. Eine Neuinstallation von PMT stellt die Datei wieder her.");

    public static string ScriptDocsHintWikiSource => T("scriptdocs.hint.wikiSource", "Diese Script-Datenbank stammt aus der offiziellen Spiel-Wiki (mit PMT mitgeliefert), nicht aus Doku-Logs des Spiels. Eine Aktualisierung erfolgt durch ein neues PMT-Release, nicht durch \"Neu einlesen\".");

    public static string ScriptDocsHintFolderUnknown => T("scriptdocs.hint.folderUnknown", "Der Ordner mit den Doku-Logs des Spiels ist unbekannt. Wähle ihn über \"Ordner wählen\".");

    public static string ScriptDocsHintNoLogs(string folder) => F("scriptdocs.hint.noLogs", "Im Ordner {0} liegen keine Doku-Logs (modifiers.log, triggers.log, effects.log …). Das Spiel schreibt diese Dateien selbst: Starte es einmal (bei Europa Universalis V ggf. im Debug-Modus) und klicke danach auf \"Neu einlesen\", oder wähle den richtigen Ordner über \"Ordner wählen\".", folder);

    public static string ScriptDocsCount(int shown, int total) => F("scriptdocs.count", "{0:N0} von {1:N0} Einträgen", shown, total);

    public static string ScriptDocsOutdated => T("scriptdocs.outdated", "veraltet");

    public static string ScriptDocsNew => T("scriptdocs.new", "neu");

    public static string ScriptDocsSelectEntry => T("scriptdocs.selectEntry", "Wähle links einen Eintrag.");

    public static string ScriptDocsOutdatedDetail => T("scriptdocs.outdatedDetail", "veraltet (stand im letzten Einlesen nicht mehr in den Logs)");

    public static string ScriptDocsNewDetail => T("scriptdocs.newDetail", "neu seit dem vorletzten Einlesen");

    public static string ScriptDocsCategories(string list) => F("scriptdocs.categories", "Kategorien: {0}", list);

    public static string ScriptDocsResult(string list) => F("scriptdocs.result", "Ergebnis: {0}", list);

    public static string ScriptDocsTargets(string list) => F("scriptdocs.targets", "Ziele: {0}", list);

    public static string ScriptDocsEntries => T("scriptdocs.entries", "Einträge:");

    public static string ScriptDocsNoExampleSearch => T("scriptdocs.noExampleSearch", "Noch keine Beispielsuche für diesen Eintrag.");

    public static string ScriptDocsSearchedAt(DateTime date, int hits) => F("scriptdocs.searchedAt", "Gesucht am {0:g}: {1:N0} Treffer", date, hits);

    public static string ScriptDocsTruncated(int max) => F("scriptdocs.truncated", "(gekürzt auf {0:N0})", max);

    public static string ScriptDocsSearchAgain => T("scriptdocs.searchAgain", "Neu suchen");

    public static string ScriptDocsNoRoots => T("scriptdocs.noRoots", "Es sind noch keine Vanilla- oder Mod-Ordner bekannt. Stelle den Spielpfad ein oder füge im Explorer einen Mod-Ordner hinzu.");

    public static string ScriptDocsSearching(string file) => F("scriptdocs.searching", "Suche… {0}", file);

    public static string ScriptDocsSearchCancelled => T("scriptdocs.searchCancelled", "Suche abgebrochen.");

    public static string ScriptDocsFolderDialog => T("scriptdocs.folderDialog", "Ordner mit den Doku-Logs des Spiels wählen (mit modifiers.log, triggers.log und weiteren; EU V: \"docs\", CK III: \"logs\")");

    public static string ScopeRulesFailed => T("scriptdocs.scopeRules.failed", "Die Scope-Regeln konnten nicht gelernt werden.");

    public static string ScopeRulesDuration(double seconds) => F("scriptdocs.scopeRules.duration", "Dauer: {0:0.0} s", seconds);

    public static string ScopeRulesTitle => T("scriptdocs.scopeRules.title", "Scope-Regeln");

    // Speicherorte
    public static string StorageTitle => T("storage.title", "Speicherorte für Snapshots und Vanilla-Historie");

    public static string StorageExternal => T("storage.external", "Außerhalb des Mod-Ordners speichern (empfohlen)");

    public static string StorageParentFolder => T("storage.parentFolder", "Oberordner:");

    public static string StorageInside => T("storage.inside", "Direkt im Mod-Ordner speichern (nicht empfohlen)");

    public static string StorageInsideWarning => T("storage.insideWarning", "Achtung: Steam lädt beim Veröffentlichen den GESAMTEN Inhalt des Mod-Ordners hoch. Snapshots (ZIP-Dateien mit dem kompletten Mod) und die Vanilla-Historie würden dann mit veröffentlicht und blähen den Workshop-Upload stark auf.");

    public static string StorageParentRequired => T("storage.parentRequired", "Bitte einen Oberordner angeben.");

    public static string StorageWillCreate(string historyFolder, string snapshotsFolder) => F("storage.willCreate", "Es werden angelegt:\n  {0}\n  {1}", historyFolder, snapshotsFolder);

    public static string StorageModNamePlaceholder => T("storage.modNamePlaceholder", "[Modname]");

    public static string StorageBrowseDescription => T("storage.browseDescription", "Oberordner für Snapshots und Vanilla-Historie wählen");

    public static string StorageInsideConfirm => T("storage.insideConfirm", "Snapshots und Vanilla-Historie im Mod-Ordner werden von Steam mit hochgeladen. Trotzdem im Mod-Ordner speichern?");

    public static string StorageNotRecommended => T("storage.notRecommended", "Nicht empfohlen");

    public static string StorageInvalidFolder => T("storage.invalidFolder", "Bitte einen gültigen absoluten Ordnerpfad angeben.");

    public static string StorageInvalidFolderTitle => T("storage.invalidFolderTitle", "Ungültiger Ordner");

    // Vanilla-Diff und Übersichtsleiste
    public static string DiffOverviewTip => T("diff.overview.tip", "Übersicht der Unterschiede - Klick springt an die Stelle");

    public static string DiffTitle => T("diff.title", "Compare und Update Tool");

    public static string DiffTabCompare => T("diff.tab.compare", "Compare");

    public static string DiffTabUpdate => T("diff.tab.update", "Update");

    public static string DiffModFolderLabel => T("diff.modFolderLabel", "Mod-Ordner:");

    public static string DiffChooseModFolderShort => T("diff.chooseModFolderShort", "Wählen");

    public static string DiffGameVersionLabel => T("diff.gameVersionLabel", "Spielversion:");

    public static string DiffFilterTip => T("diff.filter.tip", "Dateien filtern: Text im Pfad, Groß-/Kleinschreibung egal");

    public static string DiffFilteredCount(int shown, int total) => F("diff.filteredCount", "{0} von {1} überschriebenen Dateien", shown, total);

    public static string DiffBrowseFolder => T("diff.browseFolder", "Ordner");

    public static string DiffChooseFolderA => T("diff.chooseFolderA", "Ordner A wählen");

    public static string DiffChooseFolderB => T("diff.chooseFolderB", "Ordner B wählen");

    public static string DiffPathFieldTip => T("diff.pathField.tip", "Datei oder Ordner. Zwei Ordner vergleicht \"Vergleichen\" Datei für Datei (auch Unterordner). Schnellzugriff-Knöpfe lassen sich hierher ziehen.");

    public static string DiffHideSame => T("diff.hideSame", "Gleiche ausblenden");

    public static string DiffFolderComparing => T("diff.folderComparing", "Ordner werden verglichen");

    public static string DiffFolderProgressTitle => T("diff.folderProgress.title", "Compare und Update Tool: Ordner werden verglichen");

    public static string DiffFolderProgressHint => T("diff.folderProgress.hint", "Bei großen Ordnern wie dem Spielverzeichnis kann das eine Weile dauern.");

    public static string DiffFolderProgressCount(string file, int done, int total) => F("diff.folderProgress.count", "{0} ({1}/{2})", file, done, total);

    public static string DiffFolderSearching => T("diff.folderProgress.searching", "Dateien werden gesucht");

    public static string DiffFolderCancelled => T("diff.folderCancelled", "Ordnervergleich abgebrochen.");

    public static string DiffFolderSummary(int different, int onlyA, int onlyB, int same) => F("diff.folderSummary", "{0} unterschiedlich, {1} nur in A, {2} nur in B, {3} gleich", different, onlyA, onlyB, same);

    public static string DiffFolderCount(int shown, int total) => F("diff.folderCount", "{0} von {1} Dateien", shown, total);

    public static string DiffMixedPaths => T("diff.mixedPaths", "A und B müssen beide Dateien oder beide Ordner sein.");

    public static string DiffBinary(string status) => F("diff.binary", "Binärdatei ({0}): kein Textvergleich, verglichen wurden die Bytes.", status);

    public static string DiffStatusDifferent => T("diff.status.different", "unterschiedlich");

    public static string DiffStatusOnlyA => T("diff.status.onlyA", "nur in A");

    public static string DiffStatusOnlyB => T("diff.status.onlyB", "nur in B");

    public static string DiffStatusSame => T("diff.status.same", "gleich");

    public static string DiffTakeMenu => T("diff.take.menu", "Markierte Zeilen übernehmen");

    public static string DiffClearMarksMenu => T("diff.take.clear", "Markierung aufheben");

    public static string DiffTakePaused => T("diff.take.paused", "Übernehmen geht erst wieder, wenn beide Seiten als Skript lesbar sind (Vergleich pausiert).");

    public static string DiffTakeNothing => T("diff.take.nothing", "Nichts markiert: erst Zeilen im Zeilennummern-Rand markieren (mit gedrückter linker Maustaste ziehen), dann U.");

    public static string DiffTakeReadOnly(string side) => F("diff.take.readOnly", "Die Seite \"{0}\" ist schreibgeschützt, dorthin wird nichts übernommen.", side);

    public static string DiffTaken(int lines) => F("diff.take.done", "{0} Zeile(n) übernommen. Strg+Z macht es rückgängig.", lines);

    public static string DiffChooseModFolderTip => T("diff.chooseModFolder.tip", "Ordner EINER konkreten Mod wählen (der Ordner mit common/gui/... direkt drin) - der Sammelordner aus dem Spielprofil reicht nicht, da jede Mod normalerweise in einem eigenen Unterordner liegt.");

    public static string DiffSearch => SearchTexts.Title;

    public static string DiffSearchTip => T("diff.search.tip", "Suchen & Ersetzen (Strg+F / Strg+H) - Suchen in beiden Seiten, Ersetzen nur rechts (Mod bzw. Datei B)");

    public static string DiffDirtyTip => T("diff.dirty.tip", "Ungespeicherte Änderungen in der rechten Datei");

    public static string DiffSaveTip => T("diff.save.tip", "Geänderte Seiten speichern (Strg+S): rechts immer, links bei frei gewählten Dateien - vor dem ersten Speichern pro Datei wird ein Snapshot des Mods angelegt");

    public static string DiffPairsTip => T("diff.pairs.tip", "Automatisch erkannte Vanilla/Mod-Dateipaare des gewählten Mod-Ordners");

    public static string DiffChooseFileA => T("diff.chooseFileA", "Datei A wählen");

    public static string DiffChooseFileB => T("diff.chooseFileB", "Datei B wählen");

    public static string DiffCompareButton => T("diff.compare.button", "Vergleichen");

    public static string DiffCompareBrowse => T("diff.compare.browse", "Durchsuchen");

    public static string DiffCompareMissing(string path) => F("diff.compare.missing", "Datei nicht gefunden: {0}", path);

    public static string DiffPrevious => T("diff.previous", "Vorige Abweichung");

    public static string DiffPreviousTip => T("diff.previous.tip", "Zur vorigen Abweichung springen (Umschalt+F7)");

    public static string DiffNext => T("diff.next", "Nächste Abweichung");

    public static string DiffNextTip => T("diff.next.tip", "Zur nächsten Abweichung springen (F7)");

    public static string DiffNoFurther => T("diff.noFurther", "Keine weitere Abweichung in dieser Richtung.");

    public static string DiffMergeAfterUpdate => T("diff.mergeAfterUpdate", "Merge nach Spiel-Update");

    public static string DiffVersionTip => T("diff.version.tip", "Spielversion manuell eingeben, falls automatisch nicht erkennbar (launcher-settings.json nicht gefunden). Wird mit Enter oder beim Verlassen des Feldes übernommen.");

    public static string DiffFolding => T("diff.folding", "Blöcke einklappbar");

    public static string DiffFoldingTip => T("diff.folding.tip", "Eingeklappte Blöcke verschieben die Ausrichtung der beiden Seiten. Für eine durchgehend ausgerichtete Ansicht das Einklappen ausschalten.");

    public static string DiffFoldingNote => T("diff.folding.note", "Einklappen verschiebt die Ausrichtung");

    public static string DiffCopyPathTip => T("diff.copyPath.tip", "Pfad kopieren");

    public static string DiffOpenFolderTip => T("diff.openFolder.tip", "Ordner der Datei im Windows-Explorer öffnen");

    public static string DiffNoModFolder => T("diff.noModFolder", "Kein Mod-Ordner gewählt.");

    public static string DiffNoAutoDetect => T("diff.noAutoDetect", "Kein aktives Spielprofil, Mod-Ordner oder Spielpfad bekannt - automatische Erkennung nicht möglich. Manuelle Dateiwahl funktioniert trotzdem.");

    public static string DiffNoOverridden => T("diff.noOverridden", "Keine von Vanilla überschriebenen Dateien im gewählten Mod-Ordner gefunden.");

    public static string DiffFileNotOverridden(string file) => F("diff.fileNotOverridden", "{0} ist keine Mod-Kopie einer Vanilla-Datei", file);

    public static string DiffOverriddenCount(int count) => F("diff.overriddenCount", "{0} von Vanilla überschriebene Datei(en) gefunden.", count);

    public static string DiffModFolderDialog => T("diff.modFolderDialog", "Ordner der zu bearbeitenden Mod auswählen (der Ordner direkt unterhalb von \"mod\", der \"common\"/\"gui\"/... enthält)");

    public static string DiffLeftTip(string path) => F("diff.left.tip", "Links: {0}", path);

    public static string DiffRightTip(string path) => F("diff.right.tip", "Rechts: {0}", path);

    public static string DiffFileA => T("diff.fileA", "Datei A");

    public static string DiffFileB => T("diff.fileB", "Datei B");

    public static string DiffPaused(string message) => F("diff.paused", "Vergleich pausiert - Syntaxfehler: {0}", message);

    public static string DiffMergeDone => T("diff.mergeDone", "Merge abgeschlossen - Sicherheits-Snapshot der vorherigen Moddatei wurde angelegt.");

    public static string DiffMergeAutoOnly => T("diff.mergeAutoOnly", "Merge nur für automatisch erkannte Vanilla/Mod-Paare möglich.");

    public static string DiffVersionUnknown => T("diff.versionUnknown", "Spielversion konnte nicht automatisch erkannt werden - bitte manuell eintragen, um Merge zu ermöglichen.");

    public static string DiffMergeAvailable(string oldVersion, string currentVersion) => F("diff.mergeAvailable", "Merge verfügbar: alte Version {0} -> aktuelle Version {1}.", oldVersion, currentVersion);

    public static string DiffBaselineRecorded(string version) => F("diff.baselineRecorded", "Basis für Version {0} aufgezeichnet.", version);

    public static string DiffUnsavedPrompt(string file) => F("diff.unsavedPrompt", "Ungespeicherte Änderungen in \"{0}\".\n\nSpeichern?", file);

    public static string DiffUnsavedTitle => T("diff.unsavedTitle", "Ungespeicherte Änderungen");

    public static string DiffExplorerFailed(string message) => F("diff.explorerFailed", "Explorer konnte nicht geöffnet werden: {0}", message);

    public static string DiffSaved(string file) => F("diff.saved", "Gespeichert: {0}", file);

    public static string DiffSaveFailed(string message) => F("diff.saveFailed", "Datei konnte nicht gespeichert werden: {0}", message);

    public static string DiffNoSnapshotPrompt(string file) => F("diff.noSnapshotPrompt", "\"{0}\" wurde frei gewählt und liegt nicht in einem erkannten Mod-Ordner - es wird KEIN Snapshot angelegt.\n\nTrotzdem speichern?", file);

    public static string DiffNoSnapshotTitle => T("diff.noSnapshotTitle", "Ohne Snapshot speichern");

    public static string DiffLeftReadOnly => T("diff.leftReadOnly", "Die linke Vanilla-Seite ist schreibgeschützt. Klicke in die rechte Seite (Mod), um dort zu ersetzen.");

    public static string DiffReplaceNotPossible => T("diff.replaceNotPossible", "Ersetzen nicht möglich");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
