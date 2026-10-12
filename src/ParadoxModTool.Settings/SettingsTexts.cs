using ParadoxModTool.Localization;

namespace ParadoxModTool.Settings;

/// <summary>
/// Oberflächentexte der Einstellungen (Einstellungsfenster, Theme-Editor) an einer Stelle. Die Schlüssel stehen in <c>locales/de.json</c>
/// und <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen (Test <c>SettingsTextsTests</c>).
/// </summary>
public static class SettingsTexts
{
    // Theme-Editor: Klick auf ein Farbfeld eines eingebauten Themes.
    public static string ThemeReadOnlyTitle => T("settings.theme.readOnlyTitle", "Theme schreibgeschützt");

    public static string ThemeReadOnlyCopyPrompt(string themeName) => F("settings.theme.readOnlyCopyPrompt", "Das eingebaute Theme „{0}“ ist schreibgeschützt.\n\nJetzt eine eigene Kopie anlegen und diese Farbe darin ändern?", themeName);

    // Menü und Fenster
    public static string MenuSettings => T("settings.menu", "Einstellungen");

    public static string MenuSettingsTip => T("settings.menu.tip", "Öffnet das Einstellungsfenster. Änderungen wirken sofort.");

    public static string WindowTitle => T("settings.window.title", "Einstellungen");

    public static string Close => T("settings.close", "Schließen");

    public static string NotSet => T("settings.notSet", "nicht eingestellt");

    public static string Choose => T("settings.choose", "Auswählen");

    public static string CategoryLabel(SettingsCategory category) => category switch
    {
        SettingsCategory.General => T("settings.category.general", "Allgemein"),
        SettingsCategory.Workspace => T("settings.category.workspace", "Arbeitsbereich"),
        SettingsCategory.NotesPanel => T("settings.category.notesPanel", "Notiz/Lesezeichen-Panel"),
        SettingsCategory.Explorer => T("settings.category.explorer", "Verzeichnis-Übersicht"),
        SettingsCategory.Compatch => T("settings.category.compatch", "Komp-Patch Tool"),
        SettingsCategory.GuiTools => T("settings.category.guiTools", "GUI-Werkzeuge"),
        SettingsCategory.ErrorLog => T("settings.category.errorLog", "Fehlerprotokoll"),
        SettingsCategory.SaveExplorer => T("settings.category.saveExplorer", "Spielstand-Explorer"),
        SettingsCategory.Layout => T("settings.category.layout", "Layout"),
        SettingsCategory.Tiles => T("settings.category.tiles", "Kachelleiste"),
        SettingsCategory.Toolbars => T("settings.category.toolbars", "Werkzeugleisten"),
        SettingsCategory.Language => T("settings.category.language", "Sprache"),
        SettingsCategory.Themes => T("settings.category.themes", "Themes"),
        SettingsCategory.Hotkeys => T("settings.category.hotkeys", "Hotkeys"),
        SettingsCategory.BugTracker => T("settings.category.bugTracker", "Bugtracker"),
        SettingsCategory.Update => T("settings.category.update", "Update"),
        _ => category.ToString(),
    };

    // 1. Allgemein
    public static string GamePath => T("settings.general.gamePath", "Spielpfad");

    public static string GamePathTip => T("settings.general.gamePath.tip", "Wählt die .exe des Spiels aus (z. B. EU 5.exe im Ordner binaries). ▶ Spiel starten nutzt diesen Pfad.");

    public static string PlaysetFile => T("settings.general.playsets", "Playset-Datei");

    public static string PlaysetFileTip => T("settings.general.playsets.tip", "Wählt playsets.json aus (unter Dokumente/Paradox Interactive/<Spiel>). Wird für die Playset-Auswahl beim Starten gebraucht.");

    public static string DataHeading => T("settings.general.data", "Daten");

    public static string StorageLocations => T("settings.general.storage", "Speicherorte (Snapshots & Vanilla-Historie)");

    public static string StorageLocationsTip => T("settings.general.storage.tip", "Oberordner für „[Modname] Snapshots“ und die Vanilla-Historie wählen, oder (nicht empfohlen) im Mod-Ordner speichern");

    public static string RelearnLocProfiles => T("settings.general.relearnLoc", "Lokalisierungsprofile neu lernen");

    public static string RelearnLocProfilesTip => T("settings.general.relearnLoc.tip", "Liest die Skript- und Loc-Dateien des Spiels erneut und lernt daraus, welche Stellen (Objektnamen mit Endungen, Felder wie text) eine Lokalisierung brauchen (geschieht sonst automatisch nach Spiel-Updates)");

    public static string RelearnScopeRules => T("settings.general.relearnScope", "Scope-Regeln neu lernen");

    public static string RelearnScopeRulesTip => T("settings.general.relearnScope.tip", "Liest die Vanilla-Dateien erneut und lernt daraus die Start-Scopes der Felder und die Namen der Script-Values (geschieht sonst automatisch nach Spiel-Updates und neuen Doku-Logs)");

    public static string ShowInToolbar => T("settings.general.showInToolbar", "In der Werkzeugleiste anzeigen");

    // Werkzeugleisten: Gruppen und Einträge

    public static string ToolbarGroupLabel(ToolbarGroup group) => group switch
    {
        ToolbarGroup.Files => T("settings.toolbarGroup.files", "Dateien"),
        ToolbarGroup.History => T("settings.toolbarGroup.history", "Rückgängig und Wiederholen"),
        ToolbarGroup.Tools => T("settings.toolbarGroup.tools", "Werkzeuge"),
        ToolbarGroup.Toggles => T("settings.toolbarGroup.toggles", "Ansicht und Schalter"),
        ToolbarGroup.Launch => T("settings.toolbarGroup.launch", "Spiel starten"),
        _ => group.ToString(),
    };

    public static string ToolbarItemLabel(string id) => id switch
    {
        ToolbarItemIds.New => MenuTexts.WorkspaceNew,
        ToolbarItemIds.Open => MenuTexts.WorkspaceOpen,
        ToolbarItemIds.Save => MenuTexts.WorkspaceSave,
        ToolbarItemIds.SaveAll => MenuTexts.WorkspaceSaveAll,
        ToolbarItemIds.Close => MenuTexts.WorkspaceClose,
        ToolbarItemIds.Move => T("settings.toolbarItem.move", "In anderen Arbeitsbereich"),
        ToolbarItemIds.Undo => T("settings.toolbarItem.undo", "Rückgängig"),
        ToolbarItemIds.Redo => T("settings.toolbarItem.redo", "Wiederholen"),
        ToolbarItemIds.Search => T("settings.toolbarItem.search", "Suchen & Ersetzen"),
        ToolbarItemIds.GoToLine => T("settings.toolbarItem.goToLine", "Gehe zu Zeile"),
        ToolbarItemIds.GoToBracket => T("settings.toolbarItem.goToBracket", "Gehe zu Klammer"),
        ToolbarItemIds.ScopeFinder => T("settings.toolbarItem.scopeFinder", "Scope Finder"),
        ToolbarItemIds.GuiDataHelper => T("settings.toolbarItem.guiDataHelper", "GUI-Daten-Helfer"),
        ToolbarItemIds.Templates => T("settings.toolbarItem.templates", "Template Generator"),
        ToolbarItemIds.Notes => T("settings.toolbarItem.notes", "Notizen"),
        ToolbarItemIds.Bookmark => T("settings.toolbarItem.bookmark", "Lesezeichen"),
        ToolbarItemIds.Bug => T("settings.toolbarItem.bug", "Bug"),
        ToolbarItemIds.BugTracker => T("settings.toolbarItem.bugTracker", "Bugtracker"),
        ToolbarItemIds.Format => T("settings.toolbarItem.format", "Formatieren"),
        ToolbarItemIds.SplitView => T("settings.toolbarItem.splitView", "Split View"),
        ToolbarItemIds.Explorer => T("settings.toolbarItem.explorer", "Verzeichnis-Übersicht"),
        ToolbarItemIds.NotesPanel => T("settings.toolbarItem.notesPanel", "Notiz-Panel"),
        ToolbarItemIds.AutoComplete => T("settings.toolbarItem.autoComplete", "Vorschläge"),
        ToolbarItemIds.Hover => T("settings.toolbarItem.hover", "Tooltips"),
        ToolbarItemIds.ModifierReload => T("settings.toolbarItem.modifierReload", "Modifier-Werte neu laden"),
        ToolbarItemIds.LaunchOption => T("settings.toolbarItem.launchOption", "Startoption"),
        ToolbarItemIds.Playset => T("settings.toolbarItem.playset", "Playset"),
        ToolbarItemIds.LaunchGame => T("settings.toolbarItem.launchGame", "Spiel starten"),
        _ => id,
    };

    // 3. Arbeitsbereich
    public static string SplitView => T("settings.workspace.splitView", "Split View (zwei Arbeitsbereiche)");

    public static string SplitViewTip => T("settings.workspace.splitView.tip", "Bildschirm in zwei Arbeitsbereiche teilen (derselbe Schalter wie in der Werkzeugleiste)");

    public static string ExplorerVisible => T("settings.workspace.explorer", "Verzeichnis-Übersicht anzeigen");

    public static string ExplorerVisibleTip => T("settings.workspace.explorer.tip", "Blendet die Verzeichnis-Übersicht ein oder aus (derselbe Schalter wie in der Werkzeugleiste). Beim Programmstart ist sie immer sichtbar.");

    public static string BracketHighlight => T("settings.workspace.bracket", "Passende Klammer markieren");

    public static string BracketHighlightTip => T("settings.workspace.bracket.tip", "Färbt die Klammer am Cursor und ihr Gegenstück ein ({ }, ( ), [ ]). Der Sprung zur passenden Klammer mit Strg+B funktioniert auch bei ausgeschalteter Markierung.");

    public static string AutoComplete => T("settings.workspace.autoComplete", "Autovervollständigung für Script-Namen");

    public static string CompleteCustomLocalizations => T("settings.workspace.completeCustomLoc", "Custom Localizations in den Vorschlägen anzeigen");

    public static string CompleteCustomLocalizationsTip => T("settings.workspace.completeCustomLoc.tip", "Nimmt die Custom Localizations des Spiels (z. B. GetName) in die Vorschlagsliste auf (Standard: aus). Der Hover-Tooltip kennt sie unabhängig davon.");

    public static string AutoCompleteTip => T("settings.workspace.autoComplete.tip", "Vorschlagsliste beim Tippen (ab 3 Zeichen) und mit Strg+Leertaste, aus der Script-Datenbank. Ausgeschaltet gibt es auch mit Strg+Leertaste keine Vorschläge.");

    public static string DocumentBar => T("settings.workspace.documentBar", "Dokumentenleiste mit Filtern anzeigen");

    public static string DocumentBarTip => T("settings.workspace.documentBar.tip", "Kategorie-Häkchen und Filter (mit Notiz/Lesezeichen) über den Tabs ein- oder ausblenden");

    public static string MultiRowTabs => T("settings.workspace.multiRowTabs", "Tabs mehrzeilig anzeigen");

    public static string MultiRowTabsTip => T("settings.workspace.multiRowTabs.tip", "Zeigt die offenen Dokumente in mehreren Zeilen statt in einer scrollbaren Zeile (Standard: aus)");

    public static string ReplaceWarningsWindow => T("settings.workspace.replaceWarningsWindow", "Warnungen nach dem Ersetzen in eigenem Fenster zeigen");

    public static string ReplaceWarningsWindowTip => T("settings.workspace.replaceWarningsWindow.tip", "Meldet OR-, NOR- und AND-Blöcke, die nach dem Ersetzen nur noch einen oder keinen Eintrag haben, in einem eigenen Fenster statt im Notiz-Panel (Standard: aus). Ist das Notiz-Panel ausgeschaltet, kommt immer das Fenster.");

    public static string PathBar => T("settings.workspace.pathBar", "Pfadleiste anzeigen");

    public static string PathBarTip => T("settings.workspace.pathBar.tip", "Zeile über dem Editor mit Pfad, Kopieren, Ordner öffnen und Notizen anzeigen (Standard: an)");

    public static string ScopeStatusBar => T("settings.workspace.scopeStatusBar", "Scope in der Statusleiste anzeigen");

    public static string ScopeStatusBarTip => T("settings.workspace.scopeStatusBar.tip", "Zeigt unter dem Editor den Scope am Cursor samt dem Weg dorthin (Feld, Iteratoren, Event-Targets)");

    public static string ScopeStatusBarTop => T("settings.workspace.scopeStatusBarTop", "Scope unter der Pfadleiste anzeigen");

    public static string ScopeStatusBarTopTip => T("settings.workspace.scopeStatusBarTop.tip", "Zeigt dieselbe Scope-Zeile zusätzlich direkt unter der Pfadleiste über dem Editor (Standard: aus). Lässt sich unabhängig von der Leiste unter dem Editor ein- und ausschalten.");

    public static string ScopeWarnings => T("settings.workspace.scopeWarnings", "Scope-Warnungen im Text (Wellenlinie)");

    public static string ScopeWarningsTip => T("settings.workspace.scopeWarnings.tip", "Unterstreicht Trigger und Effects, die im aktuellen Scope nicht gelten. Gemeldet wird nur, wo der Scope sicher ist.");

    public static string ScriptHover => T("settings.workspace.scriptHover", "Script-Hover-Tipps anzeigen");

    public static string ScriptHoverTip => T("settings.workspace.scriptHover.tip", "Zeigt beim Verweilen mit der Maus auf einem bekannten Modifier/Trigger/Effect einen Tooltip mit Beschreibung und Scopes (aus der Script-Datenbank)");

    public static string ScopeHover => T("settings.workspace.scopeHover", "Scope im Hover-Tooltip anzeigen");

    public static string ScopeHoverTip => T("settings.workspace.scopeHover.tip", "Ergänzt den Hover-Tooltip um die Angabe, ob der Trigger/Effect im aktuellen Scope erlaubt ist");

    public static string ModifierHover => T("settings.workspace.modifierHover", "Modifier-Werte wie im Spiel anzeigen");

    public static string ModifierHoverTip => T("settings.workspace.modifierHover.tip", "Zeigt beim Überfahren einer Modifier-Zahl oder eines Script-Values, welcher Wert im Spiel angezeigt wird.");

    public static string ModifierNames => T("settings.workspace.modifierNames", "Modifier-Namen im Tooltip anzeigen");

    public static string ModifierNamesTip => T("settings.workspace.modifierNames.tip", "Zeigt hinter dem Wert im Modifier-Tooltip den übersetzten Namen des Modifiers (aus der Lokalisierung des Spiels).");

    public static string ResultsOnTop => T("settings.workspace.resultsOnTop", "Suchergebnisse oben anzeigen");

    public static string ResultsOnTopTip => T("settings.workspace.resultsOnTop.tip", "Legt fest, ob die Suchergebnisleiste über oder unter dem Arbeitsbereich erscheint (Standard: unten)");

    // Werkzeugleisten: Arbeitsbereich, Filter, Symbole
    public static string ManageCategories => T("settings.wsToolbar.categories", "Kategorien verwalten");

    public static string ManageCategoriesTip => T("settings.wsToolbar.categories.tip", "Dateikategorien anlegen, umbenennen, einfärben und mit Endungs- und Ordnerregeln versehen");

    public static string FilterHeading => T("settings.wsToolbar.filterHeading", "Filter in der Dokumentenleiste");

    public static string FilterHint => T("settings.wsToolbar.filterHint", "Dieselben Einträge wie das Menü „Filter“. Ein ausgeblendetes Häkchen filtert nicht mehr; seine Dateien sind immer sichtbar.");

    public static string OriginVanilla => T("settings.origin.vanilla", "Herkunft: Vanilla");

    public static string OriginMod => T("settings.origin.mod", "Herkunft: Mod");

    public static string OriginExternal => T("settings.origin.external", "Herkunft: Extern");

    public static string IconHeading => T("settings.wsToolbar.iconHeading", "Symbole in den Reitern");

    public static string IconHint => T("settings.wsToolbar.iconHint", "Dieselben Einträge wie das Menü „Symbole“. Schaltet nur die Anzeige; Filter wie „Nur mit Notiz“ arbeiten weiter.");

    public static string ButtonsHeading => T("settings.wsToolbar.buttons", "Knöpfe in der Werkzeugleiste");

    public static string WorkspaceFilterButton => T("settings.wsToolbar.filterButton", "Filter-Button in der Werkzeugleiste anzeigen");

    public static string WorkspaceFilterButtonTip => T("settings.wsToolbar.filterButton.tip", "Knopf „Filter ▾“ in der Werkzeugleiste jedes Arbeitsbereichs");

    public static string WorkspaceIconButton => T("settings.wsToolbar.iconButton", "Symbol-Button in der Werkzeugleiste anzeigen");

    public static string WorkspaceIconButtonTip => T("settings.wsToolbar.iconButton.tip", "Knopf „Symbole ▾“ in der Werkzeugleiste jedes Arbeitsbereichs");

    public static string IconLabel(string id) => id switch
    {
        "Note" => T("settings.icon.note", "Notiz"),
        "Bookmark" => T("settings.icon.bookmark", "Lesezeichen"),
        "Bug" => T("settings.icon.bug", "Bug"),
        "Vanilla" => T("settings.icon.vanilla", "Vanilla-Datei"),
        "Mod" => T("settings.icon.mod", "Mod-Datei"),
        "Lock" => T("settings.icon.lock", "Sperre"),
        "ErrorLog" => T("settings.icon.errorLog", "Fehlerprotokoll"),
        _ => id,
    };

    // Fehlerprotokoll (Kategorie nach den GUI-Werkzeugen)
    public static string ErrorLogStandalone => T("settings.errorLog.standalone", "Fehlerprotokoll als eigenes Fenster");

    public static string ErrorLogStandaloneTip => T("settings.errorLog.standalone.tip", "Das Fenster liegt nicht über PMT und hat einen eigenen Eintrag in der Taskleiste, z.B. für einen zweiten Bildschirm. Gilt ab dem nächsten Öffnen des Fensters.");

    public static string SaveExplorerStandalone => T("settings.saveExplorer.standalone", "Spielstand-Explorer als eigenes Fenster");

    public static string SaveExplorerStandaloneTip => T("settings.saveExplorer.standalone.tip", "Das Fenster liegt nicht über PMT und hat einen eigenen Eintrag in der Taskleiste, z.B. für einen zweiten Bildschirm. Gilt ab dem nächsten Öffnen des Fensters.");

    public static string SaveExplorerPlacementHint => T("settings.saveExplorer.placement", "PMT merkt sich Position und Größe des Fensters. Als eigenes Fenster öffnet es beim ersten Mal auf einem anderen Bildschirm als PMT, falls einer angeschlossen ist.");

    public static string ListSearchTip => T("settings.listSearch.tip", "Liste durchsuchen: Enter wählt den ersten Treffer, Pfeil nach unten geht in die Liste, Escape leert die Suche (ein zweites Escape schließt die Liste).");

    public static string ListSearchPlaceholder => T("settings.listSearch.placeholder", "Suchen");

    public static string ErrorLogPlacementHint => T("settings.errorLog.placement", "PMT merkt sich Position und Größe des Fensters. Als eigenes Fenster öffnet es beim ersten Mal auf einem anderen Bildschirm als PMT, falls einer angeschlossen ist.");

    // 5. Notiz/Lesezeichen-Panel
    public static string NotesPanelVisible => T("settings.notes.visible", "Panel anzeigen");

    public static string NotesPanelVisibleTip => T("settings.notes.visible.tip", "Rechtes Panel mit Notiz und Lesezeichen der aktiven Datei (Standard: an)");

    public static string NotesPanelKeepOpen => T("settings.notes.keepOpen", "Panel auch bei leeren Dateien offen halten");

    public static string NotesPanelKeepOpenTip => T("settings.notes.keepOpen.tip", "Ohne diese Einstellung blendet sich das Panel bei Dateien ohne Notiz und Lesezeichen aus. Mit ihr bleibt es offen, damit die Ansicht beim Dateiwechsel nicht springt.");

    public static string NotesPanelScope => T("settings.notes.scope", "Scope im Notiz-Panel anzeigen");

    public static string NotesPanelScopeTip => T("settings.notes.scope.tip", "Abschnitt „Scope“ im rechten Panel: Scope am Cursor, gespeicherte Scopes und die Warnungen der Datei");

    // 6. Komp-Patch Tool (Tooltips aus CompatchTexts)
    public static string CompatchCautious => T("settings.compatch.cautious", "Vorsichtig schreiben");

    public static string CompatchSkipLocked => T("settings.compatch.skipLocked", "Gesperrte Dateien nicht einlesen");

    public static string CompatchResultRight => T("settings.compatch.resultRight", "Ergebnis ganz rechts");

    public static string CompatchKindsLayout => T("settings.compatch.kindsLayout", "Ausgeblendete Dateiarten (Startfenster und Merge-Fenster)");

    public static string CompatchKindsList => T("settings.compatch.kindsList", "Als Liste untereinander");

    public static string CompatchKindsSideBySide => T("settings.compatch.kindsSideBySide", "Nebeneinander");

    public static string CompatchNewStateDefaults => T("settings.compatch.newStateDefaults", "Standard für neue Arbeitsstände");

    public static string CompatchNewStateDefaultsHint => T("settings.compatch.newStateDefaults.hint", "Jeder Arbeitsstand merkt sich diese Werte selbst. Ändern hier wirkt erst bei neuen Arbeitsständen; ein offenes Merge-Fenster änderst du über seinen Knopf „Einstellungen ▾“.");

    // Verzeichnis-Übersicht
    public static string ExplorerButtonsLeft => T("settings.explorer.buttonsLeft", "Buttons links");

    public static string ExplorerButtonsLeftTip => T("settings.explorer.buttonsLeft.tip", "Lupe und die Knöpfe für den linken und rechten Arbeitsbereich auf die linke Seite der Zeilen setzen");

    public static string ExplorerBugMarks => T("settings.explorer.bugMarks", "Bug-Markierungen");

    public static string ExplorerBugMarksTip => T("settings.explorer.bugMarks.tip", "Zeigt hinter Dateien und Ordnern die Zahl der Meldungen aus dem letzten Spiellauf (Fehlerprotokoll)");

    public static string ExplorerFoldersOnly => T("settings.explorer.foldersOnly", "Suche: nur Ordner");

    public static string ExplorerFoldersOnlyTip => T("settings.explorer.foldersOnly.tip", "Die Namenssuche findet nur Ordner. Ein Klick auf einen Ordner zeigt ihn im Baum mit allen Dateien");

    public static string ExplorerHideUgc => T("settings.explorer.hideUgc", "ugc-Verweise ausblenden");

    public static string ExplorerHideUgcTip => T("settings.explorer.hideUgc.tip", "Blendet die Dateien ugc_*.mod im Mod-Ordner aus. Sie stammen von der alten Workshop-Anbindung, der Launcher legt sie an und man bearbeitet sie nie selbst");

    public static string CompatchDefaultView => T("settings.compatch.defaultView", "Standardansicht");

    // 8. Layout
    public static string WindowLayoutHeading => T("settings.layout.windowLayout", "Fensterlayout");

    public static string WindowLayoutTiles => T("settings.layout.windowLayout.tiles", "Neues Layout (Kachelleiste und kleine Werkzeugleiste)");

    public static string WindowLayoutClassic => T("settings.layout.windowLayout.classic", "Klassisches Layout (alle Werkzeuge in Menü und einer Werkzeugleiste)");

    public static string WindowLayoutHint => T("settings.layout.windowLayout.hint", "Das klassische Layout behält alle Werkzeuge in der Menüleiste und einer Werkzeugleiste, dazu eine Leiste je Arbeitsbereich. Wirkt sofort.");

    public static string ToolbarPositionHeading => T("settings.layout.toolbarPosition", "Position der kleinen Werkzeugleiste");

    public static string ToolbarPositionTop => T("settings.layout.toolbarPosition.top", "Oben");

    public static string ToolbarPositionLeft => T("settings.layout.toolbarPosition.left", "Links");

    public static string ToolbarPositionRight => T("settings.layout.toolbarPosition.right", "Rechts");

    public static string ToolbarPositionBottom => T("settings.layout.toolbarPosition.bottom", "Unten");

    public static string ToolbarPositionHint => T("settings.layout.toolbarPosition.hint", "Links oder rechts angeheftet zeigt die Leiste nur Symbole; Tooltips tragen die Namen. Startoption und Playset werden dort zu Symbolknöpfen mit Liste.");

    public static string ColorizeWholeTab => T("settings.layout.colorizeTab", "Farbcodes färben den ganzen Reiter ein");

    public static string ColorizeWholeTabTip => T("settings.layout.colorizeTab.tip", "Färbt den kompletten Tab-Reiter halbtransparent in der Kategorie-Farbe ein, statt nur den schmalen Balken links (Standard: aus)");

    public static string IconColorsHeading(string themeName) => F("settings.layout.iconHeading", "Symbolfarben im Theme „{0}“", themeName);

    public static string IconColorsHint => T("settings.layout.iconHint", "Gilt nur für das aktuelle Theme. Notiz und Vanilla werden ganz eingefärbt; bei Lesezeichen und Mod färbt die Farbe die Plakette hinter dem Symbol, bei Bug ihren Rand. Mod hat ohne eigene Farbe keine Plakette.");

    public static string PickColorTip => T("settings.layout.pick.tip", "Farbe wählen");

    public static string Reset => T("settings.layout.reset", "Zurücksetzen");

    // 9. Sprache
    public static string LanguageHint => T("settings.language.hint", "Wirkt nach einem Neustart.");

    public static string LanguagePercent(int percent) => F("settings.language.percent", "{0} %", percent);

    public static string LanguageMissing(int count) => F("settings.language.missing", "{0} fehlen", count);

    public static string LanguageOriginUser => T("settings.language.origin.user", "Benutzerordner");

    public static string LanguageOriginProgram => T("settings.language.origin.program", "mitgeliefert");

    public static string LanguageMachine => T("settings.language.machine", "maschinell übersetzt, Korrekturen willkommen");

    public static string LanguageErrors(int count) => F("settings.language.errors", "{0} Fehler", count);

    public static string LanguageParseError(long line, long position, string message) => F("settings.language.error.parse", "Zeile {0}, Position {1}: {2}", line, position, message);

    public static string LanguagePlaceholderError(string key, string expected, string found) => F("settings.language.error.placeholder", "{0}: erwartet {1}, gefunden {2}", key, expected, found);

    public static string LanguageUnknownKey(string key) => F("settings.language.error.unknown", "Unbekannter Key: {0}", key);

    public static string LanguageMoreErrors(int count) => F("settings.language.error.more", "und {0} weitere", count);

    public static string LanguageRecheck => T("settings.language.recheck", "Neu prüfen");

    public static string LanguageOpenFolder => T("settings.language.openFolder", "Übersetzungsordner öffnen");

    public static string LanguageGuide => T("settings.language.guide", "Anleitung für Übersetzer");

    public static string LanguageRestart => T("settings.language.restart", "PMT neu starten");

    public static string LanguageRestartTip => T("settings.language.restart.tip", "Schließt dieses Fenster und öffnet es sofort wieder; ungespeicherte Änderungen werden wie beim normalen Schließen gesichert. Andere Spielfenster bleiben offen und behalten ihre Sprache bis zu ihrem eigenen Neustart.");

    // 10. Themes
    public static string ThemeSystem => T("settings.theme.system", "Wie Windows");

    public static string ThemeLight => T("settings.theme.light", "Hell");

    public static string ThemeDark => T("settings.theme.dark", "Dunkel");

    public static string ThemeEditor => T("settings.theme.editor", "Theme-Editor");

    public static string ThemeEditorTip => T("settings.theme.editor.tip", "Eigene Themes anlegen, bearbeiten, importieren und exportieren");

    // 11. Hotkeys
    public static string HotkeysHint => T("settings.hotkeys.hint", "Nur zum Nachschlagen; die Tastenkürzel lassen sich nicht ändern.");

    public static string HotkeyGroupLabel(HotkeyGroup group) => group switch
    {
        HotkeyGroup.Main => T("settings.hotkeyGroup.main", "Hauptfenster und Editor"),
        HotkeyGroup.GuiDesigner => T("settings.hotkeyGroup.guiDesigner", "GUI-Designer"),
        HotkeyGroup.Explorer => T("settings.hotkeyGroup.explorer", "Verzeichnis-Übersicht"),
        HotkeyGroup.Search => T("settings.hotkeyGroup.search", "Suchen & Ersetzen"),
        HotkeyGroup.CuTool => T("settings.hotkeyGroup.cuTool", "Compare und Update Tool"),
        HotkeyGroup.PatchRecipes => T("settings.hotkeyGroup.patchRecipes", "Patch-Rezepte"),
        HotkeyGroup.GeneratorPackages => T("settings.hotkeyGroup.generatorPackages", "Generator-Pakete"),
        HotkeyGroup.Compatch => T("settings.hotkeyGroup.compatch", "Kompatch-Merge (Ansicht A und Vergleichsfenster)"),
        HotkeyGroup.Gui => T("settings.hotkeyGroup.gui", "GUI-Inspektor"),
        HotkeyGroup.Publish => T("settings.hotkeyGroup.publish", "Veröffentlichen"),
        HotkeyGroup.Templates => T("settings.hotkeyGroup.templates", "Template Generator"),
        HotkeyGroup.Dialogs => T("settings.hotkeyGroup.dialogs", "Dialoge"),
        _ => group.ToString(),
    };

    public static string Gesture(HotkeyModifiers modifiers, string key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(HotkeyModifiers.Ctrl))
        {
            parts.Add(T("settings.key.ctrl", "Strg"));
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add(T("settings.key.shift", "Umschalt"));
        }

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add(T("settings.key.alt", "Alt"));
        }

        parts.Add(key switch
        {
            "Enter" => T("settings.key.enter", "Eingabe"),
            "Escape" => T("settings.key.escape", "Esc"),
            "Space" => T("settings.key.space", "Leertaste"),
            "Down" => T("settings.key.down", "Pfeil nach unten"),
            "Up" => T("settings.key.up", "Pfeil nach oben"),
            "Delete" => T("settings.key.delete", "Entf"),
            _ => key,
        });
        return string.Join("+", parts);
    }

    public static string HotkeyDescription(string textId) => textId switch
    {
        "main.search" => T("settings.hotkey.main.search", "Suchen & Ersetzen öffnen"),
        "main.guiInspector" => T("settings.hotkey.main.guiInspector", "GUI-Inspektor öffnen (nur mit eingeschalteten GUI-Werkzeugen)"),
        "main.goToLine" => T("settings.hotkey.main.goToLine", "Gehe zu Zeile"),
        "main.save" => T("settings.hotkey.main.save", "Aktives Dokument speichern"),
        "main.saveAll" => T("settings.hotkey.main.saveAll", "Alle ungespeicherten Dokumente beider Arbeitsbereiche speichern"),
        "main.bracket" => T("settings.hotkey.main.bracket", "Zur passenden Klammer springen"),
        "main.bookmark" => T("settings.hotkey.main.bookmark", "Lesezeichen an der Cursorzeile setzen oder entfernen"),
        "main.scopeFinder" => T("settings.hotkey.main.scopeFinder", "Scope Finder öffnen"),
        "main.guiDataHelper" => T("settings.hotkey.main.guiDataHelper", "GUI-Daten-Helfer öffnen (nur mit eingeschalteten GUI-Werkzeugen)"),
        "main.guiBlocks" => T("settings.hotkey.main.guiBlocks", "GUI-Bausteine öffnen (nur mit eingeschalteten GUI-Werkzeugen)"),
        "editor.autoComplete" => T("settings.hotkey.editor.autoComplete", "Vorschläge anzeigen"),
        "editor.undo" => T("settings.hotkey.editor.undo", "Rückgängig (Text im aktiven Dokument)"),
        "editor.redo" => T("settings.hotkey.editor.redo", "Wiederholen (Text im aktiven Dokument)"),
        "designer.undo" => T("settings.hotkey.designer.undo", "Rückgängig im Designer (nicht in Eingabefeldern)"),
        "designer.redo" => T("settings.hotkey.designer.redo", "Wiederholen im Designer (auch Strg+Umschalt+Z)"),
        "designer.delete" => T("settings.hotkey.designer.delete", "Im Baum: Element löschen"),
        "designer.duplicate" => T("settings.hotkey.designer.duplicate", "Im Baum: Element duplizieren"),
        "designer.moveUp" => T("settings.hotkey.designer.moveUp", "Im Baum: Element nach oben schieben"),
        "designer.moveDown" => T("settings.hotkey.designer.moveDown", "Im Baum: Element nach unten schieben"),
        "designer.paletteInsert" => T("settings.hotkey.designer.paletteInsert", "In der Palette: gewählten Eintrag einfügen"),
        "designer.propertyApply" => T("settings.hotkey.designer.propertyApply", "Im Eigenschaftenfeld: Wert übernehmen"),
        "designer.propertyReset" => T("settings.hotkey.designer.propertyReset", "Im Eigenschaftenfeld: Eingabe verwerfen"),
        "explorer.pin" => T("settings.hotkey.explorer.pin", "Gewählten Eintrag als Favorit anheften"),
        "explorer.clearSearch" => T("settings.hotkey.explorer.clearSearch", "Im Suchfeld: Suche leeren"),
        "explorer.searchNow" => T("settings.hotkey.explorer.searchNow", "Im Suchfeld: sofort suchen"),
        "search.find" => T("settings.hotkey.search.find", "Im Suchfeld: Suche starten"),
        "search.newLine" => T("settings.hotkey.search.newLine", "Im Suchfeld: neue Zeile beginnen"),
        "search.toggleMode" => T("settings.hotkey.search.toggleMode", "Außerhalb der Textfelder: zwischen Dokument- und Dateisuche umschalten (auch Umschalt+Tab)"),
        "cu.search" => T("settings.hotkey.cu.search", "Suchen & Ersetzen öffnen"),
        "cu.save" => T("settings.hotkey.cu.save", "Geänderte Seiten speichern"),
        "cu.version" => T("settings.hotkey.cu.version", "Im Versionsfeld: Version übernehmen"),
        "cu.nextDiff" => T("settings.hotkey.cu.nextDiff", "Zur nächsten Abweichung springen"),
        "cu.pasteRules" => T("settings.hotkey.cu.pasteRules", "Ersetzungslisten: Regeln aus einer Tabelle einfügen"),
        "cu.previousDiff" => T("settings.hotkey.cu.previousDiff", "Zur vorigen Abweichung springen"),
        "cu.take" => T("settings.hotkey.cu.take", "Markierte Zeilen in die andere Seite übernehmen"),
        "cu.clearMarks" => T("settings.hotkey.cu.clearMarks", "Markierung aufheben"),
        "cu.undoTake" => T("settings.hotkey.cu.undoTake", "Letzte Übernahme rückgängig machen"),
        "patch.guide" => T("settings.hotkey.patch.guide", "Anleitung öffnen"),
        "patch.take" => T("settings.hotkey.patch.take", "Markierte Zeilen in der Vorschau zurücknehmen"),
        "patch.clearMarks" => T("settings.hotkey.patch.clearMarks", "Markierung aufheben"),
        "patch.nextDiff" => T("settings.hotkey.patch.nextDiff", "Nächster Unterschied"),
        "patch.previousDiff" => T("settings.hotkey.patch.previousDiff", "Vorheriger Unterschied"),
        "patch.undoTake" => T("settings.hotkey.patch.undoTake", "Letzte Zurücknahme rückgängig machen"),
        "generator.applyCount" => T("settings.hotkey.generator.applyCount", "Eingegebene Anzahl übernehmen (im Anzahl-Feld)"),
        "generator.take" => T("settings.hotkey.generator.take", "Markierte Zeilen in der Vorschau zurücknehmen"),
        "generator.clearMarks" => T("settings.hotkey.generator.clearMarks", "Markierung aufheben"),
        "generator.nextDiff" => T("settings.hotkey.generator.nextDiff", "Nächster Unterschied"),
        "generator.previousDiff" => T("settings.hotkey.generator.previousDiff", "Vorheriger Unterschied"),
        "generator.undoTake" => T("settings.hotkey.generator.undoTake", "Letzte Zurücknahme rückgängig machen"),
        "generator.modifierInsert" => T("settings.hotkey.generator.modifierInsert", "Gewählten Modifier einfügen (Fenster \"Modifier suchen\")"),
        "generator.modifierList" => T("settings.hotkey.generator.modifierList", "Aus dem Suchfeld in die Liste wechseln (Fenster \"Modifier suchen\")"),
        "generator.lookupTake" => T("settings.hotkey.generator.lookupTake", "Gewählten Eintrag übernehmen (Fenster \"Ideen-Datenbank\")"),
        "generator.guide" => T("settings.hotkey.generator.guide", "Anleitung zu Generator-Paketen öffnen"),
        "packageEditor.save" => T("settings.hotkey.packageEditor.save", "Paket speichern (Paket-Editor)"),
        "packageEditor.guide" => T("settings.hotkey.packageEditor.guide", "Anleitung beim Kapitel der offenen Maske öffnen (Paket-Editor)"),
        "aiPackage.guide" => T("settings.hotkey.aiPackage.guide", "Anleitung beim Kapitel \"Mit KI erstellen\" öffnen (Paket mit KI erstellen)"),
        "candidates.guide" => T("settings.hotkey.candidates.guide", "Anleitung beim Kapitel \"Modifier ohne Idee\" öffnen (Modifier ohne Idee)"),
        "aiPackage.sourceSearch" => T("settings.hotkey.aiPackage.sourceSearch", "Ins Suchfeld springen (Codefenster beim Übernehmen von Codeblöcken)"),
        "aiPackage.sourceFindNext" => T("settings.hotkey.aiPackage.sourceFindNext", "Nächste Fundstelle im Suchfeld (Codefenster beim Übernehmen von Codeblöcken)"),
        "cu.compare" => T("settings.hotkey.cu.compare", "Dateien A und B vergleichen (in einem der beiden Felder)"),
        "compatch.nextDiff" => T("settings.hotkey.compatch.nextDiff", "Zur nächsten Abweichung zwischen den Mods springen"),
        "compatch.previousDiff" => T("settings.hotkey.compatch.previousDiff", "Zur vorigen Abweichung zwischen den Mods springen"),
        "compatch.takeRows" => T("settings.hotkey.compatch.takeRows", "Markierte Zeilen ins Ergebnis übernehmen"),
        "compatch.undoTake" => T("settings.hotkey.compatch.undoTake", "Letzte Entscheidung oder Übernahme rückgängig machen (jede Ansicht)"),
        "compatch.confirm" => T("settings.hotkey.compatch.confirm", "Ansicht A: gewählte Stelle oder gewählten Block mit \"Passt\" bestätigen"),
        "compatch.takeA" => T("settings.hotkey.compatch.takeA", "Ansicht A: gewählte Stelle oder gewählten Block aus Mod A übernehmen"),
        "compatch.takeB" => T("settings.hotkey.compatch.takeB", "Ansicht A: gewählte Stelle oder gewählten Block aus Mod B übernehmen"),
        "compatch.reset" => T("settings.hotkey.compatch.reset", "Ansicht A: gewählte Stelle oder gewählten Block zurücksetzen"),
        "compatch.compare" => T("settings.hotkey.compatch.compare", "Ansicht A: Vergleichsfenster zur gewählten Stelle öffnen"),
        "compatch.treePrevious" => T("settings.hotkey.compatch.treePrevious", "Vorschau: vorigen Eintrag im Baum wählen"),
        "compatch.treeNext" => T("settings.hotkey.compatch.treeNext", "Vorschau: nächsten Eintrag im Baum wählen"),
        "gui.inspect" => T("settings.hotkey.gui.inspect", "Gewähltes Element inspizieren"),
        "gui.toList" => T("settings.hotkey.gui.toList", "Vom Suchfeld in die Trefferliste wechseln"),
        "gui.dataHelperAppend" => T("settings.hotkey.gui.dataHelperAppend", "GUI-Daten-Helfer, Suchfeld bei \"Glied anhängen\": ersten Treffer anhängen"),
        "gui.condUndo" => T("settings.hotkey.gui.condUndo", "GUI-Daten-Helfer, Tab Bedingung: rückgängig"),
        "gui.condRedo" => T("settings.hotkey.gui.condRedo", "GUI-Daten-Helfer, Tab Bedingung: wiederholen"),
        "gui.condDelete" => T("settings.hotkey.gui.condDelete", "GUI-Daten-Helfer, Tab Bedingung: markierten Knoten entfernen"),
        "gui.condApply" => T("settings.hotkey.gui.condApply", "GUI-Daten-Helfer, Tab Bedingung: Textfeld übernehmen"),
        "publish.saveDescription" => T("settings.hotkey.publish.saveDescription", "Beschreibung speichern"),
        "publish.version" => T("settings.hotkey.publish.version", "Im Versionsfeld: Version übernehmen"),
        "publish.saveRelease" => T("settings.hotkey.publish.saveRelease", "Release-Text speichern"),
        "templates.find" => T("settings.hotkey.templates.find", "Ins Suchfeld springen"),
        "templates.next" => T("settings.hotkey.templates.next", "Nächster Treffer"),
        "templates.enter" => T("settings.hotkey.templates.enter", "Im Suchfeld: nächster Treffer; in einem Listenfeld: Wert übernehmen"),
        "dialogs.cancel" => T("settings.hotkey.dialogs.cancel", "Eingabedialog abbrechen"),
        "dialogs.scopeFinderAppend" => T("settings.hotkey.dialogs.scopeFinderAppend", "Scope Finder: gewähltes Glied anhängen; in den Suchfeldern von \"Glied anhängen\" und \"Wert am Ende\" den ersten Treffer übernehmen"),
        "bugs.reject" => T("settings.hotkey.bugs.reject", "Im Bugtracker: gewählte Meldung verwerfen"),
        "saveExplorer.copyId" => T("settings.hotkey.saveExplorer.copyId", "Im Spielstand-Explorer: ID des gewählten Objekts kopieren"),
        _ => textId,
    };

    // Fortschrittsfenster (Neu-Lernen, GUI-Index)
    public static string ProgressHint => T("settings.progress.hint", "Das kann eine Weile dauern. Abbrechen behält den bisherigen Stand.");

    public static string ProgressFiles => T("settings.progress.files", "Dateien gelesen");

    public static string ProgressCount(string text, int done, int total) => F("settings.progress.count", "{0}: {1:N0} von {2:N0}", text, done, total);

    public static string Cancel => T("settings.progress.cancel", "Abbrechen");

    // Bugtracker
    public static string BugTrackerGitHubHeading => T("settings.bugTracker.github", "GitHub");

    public static string BugTrackerGitHubHint => T("settings.bugTracker.githubHint", "Bugreports aus GitHub-Issues holt PMT über gh (GitHub CLI). PMT speichert kein Token; die Anmeldung gehört gh.");

    public static string BugTrackerChecking => T("settings.bugTracker.checking", "gh wird geprüft");

    public static string BugTrackerCheck => T("settings.bugTracker.check", "Status prüfen");

    public static string BugTrackerLogin => T("settings.bugTracker.login", "Bei GitHub anmelden");

    public static string BugTrackerGhMissing => T("settings.bugTracker.ghMissing", "gh ist nicht eingerichtet. Installiere es mit winget install GitHub.cli oder lade es im Fenster GitHub-Veröffentlichung herunter.");

    public static string BugTrackerLoggedIn(string path) => F("settings.bugTracker.loggedIn", "gh: {0}. Bei GitHub angemeldet.", path);

    public static string BugTrackerNotLoggedIn(string path) => F("settings.bugTracker.notLoggedIn", "gh: {0}. Nicht bei GitHub angemeldet.", path);

    public static string BugTrackerLoginStarted => T("settings.bugTracker.loginStarted", "gh läuft in einem eigenen Fenster. Folge dort dem Ablauf und klicke danach auf Status prüfen.");

    public static string BugTrackerLoginFailed(string reason) => F("settings.bugTracker.loginFailed", "gh konnte nicht gestartet werden: {0}", reason);

    public static string DiscordHeading => T("settings.bugTracker.discord", "Discord");

    public static string DiscordHint => T("settings.bugTracker.discordHint", "PMT liest Bugreports aus einem Forum-Channel über einen eigenen Bot. Du richtest ihn einmal ein; er reicht für alle deine Mods. Der Token wird verschlüsselt gespeichert und nie angezeigt, außer du klickst auf Anzeigen.");

    public static string DiscordStep1 => T("settings.bugTracker.discordStep1", "1. Im Discord-Developer-Portal eine Anwendung anlegen (New Application).");

    public static string DiscordOpenPortal => T("settings.bugTracker.discordOpenPortal", "Developer-Portal öffnen");

    public static string DiscordStep2 => T("settings.bugTracker.discordStep2", "2. Im Reiter Bot einen Token erzeugen (Reset Token) und hier eintragen.");

    public static string DiscordSaveToken => T("settings.bugTracker.discordSave", "Token speichern");

    public static string DiscordShowToken => T("settings.bugTracker.discordShow", "Anzeigen");

    public static string DiscordRemoveToken => T("settings.bugTracker.discordRemove", "Token entfernen");

    public static string DiscordStep3 => T("settings.bugTracker.discordStep3", "3. Im Reiter Bot den Schalter Message Content Intent einschalten und speichern.");

    public static string DiscordStep4 => T("settings.bugTracker.discordStep4", "4. Den Bot mit dem Einladungslink auf deinen Server holen. Der Link enthält genau die nötigen Rechte.");

    public static string DiscordOpenInvite => T("settings.bugTracker.discordInvite", "Einladungslink öffnen");

    public static string DiscordStep5 => T("settings.bugTracker.discordStep5", "5. Im Forum-Channel die Tags Offen, Erledigt und Verworfen anlegen. Die Zuordnung wählst du im Bugtracker unter Quellen.");

    public static string DiscordCheck => T("settings.bugTracker.discordCheck", "Einrichtung prüfen");

    public static string DiscordNoToken => T("settings.bugTracker.discordNoToken", "Kein Token gespeichert.");

    public static string DiscordTokenValid(string bot) => F("settings.bugTracker.discordTokenValid", "Token gültig (Bot {0}).", bot);

    public static string DiscordTokenInvalid => T("settings.bugTracker.discordTokenInvalid", "Discord lehnt den Token ab.");

    public static string DiscordIntentOn => T("settings.bugTracker.discordIntentOn", "Message Content Intent ist eingeschaltet.");

    public static string DiscordIntentOff => T("settings.bugTracker.discordIntentOff", "Message Content Intent ist aus. Ohne ihn sind die Texte der Beiträge leer.");

    public static string DiscordServers(int count, string names) => F("settings.bugTracker.discordServers", "Der Bot ist auf {0} Servern: {1}", count, names);

    public static string DiscordNoServers => T("settings.bugTracker.discordNoServers", "Der Bot ist noch auf keinem Server.");

    public static string DiscordCheckFailed(string reason) => F("settings.bugTracker.discordCheckFailed", "Prüfung fehlgeschlagen: {0}", reason);

    public static string DiscordTokenSaved => T("settings.bugTracker.discordSaved", "Token gespeichert (verschlüsselt).");

    public static string DiscordTokenEmpty => T("settings.bugTracker.discordEmpty", "Es wurde kein Token eingegeben.");

    public static string DiscordTokenSaveFailed(string reason) => F("settings.bugTracker.discordSaveFailed", "Der Token konnte nicht gespeichert werden: {0}", reason);

    public static string DiscordTokenRemoved => T("settings.bugTracker.discordRemoved", "Token entfernt.");

    // 12. Update
    public static string UpdateOnStartup => T("settings.update.onStartup", "Beim Start nach Updates suchen");

    public static string UpdateOnStartupTip => T("settings.update.onStartup.tip", "Fragt beim Programmstart (höchstens einmal täglich) bei GitHub nach einer neuen Version. Es werden keine persönlichen Daten gesendet.");

    public static string UpdateNow => T("settings.update.now", "Jetzt suchen");

    public static string UpdatesUnavailable => T("settings.update.unavailable", "Dieser Build kennt kein GitHub-Repository; die Update-Suche ist nicht verfügbar.");

    // Reihenfolge-Liste
    public static string ReorderUp => T("settings.reorder.up", "Nach oben");

    public static string ReorderDown => T("settings.reorder.down", "Nach unten");

    public static string ReorderDragHint => T("settings.reorder.hint", "Zeile ziehen oder mit den Pfeilen verschieben; Alt+↑/↓ mit der Tastatur");

    public static string ReorderRemove => T("settings.reorder.remove", "Entfernen");

    // Kachelleiste
    public static string TileBarVisible => T("settings.tiles.show", "Kachelleiste anzeigen");

    public static string TileSizeHeading => T("settings.tiles.size", "Standard-Kachelgröße");

    public static string TileSizeLarge => T("settings.tiles.size.large", "Groß");

    public static string TileSizeMedium => T("settings.tiles.size.medium", "Mittel");

    public static string TileSizeCompact => T("settings.tiles.size.compact", "Nur Symbole");

    public static string TileLabelsVisible => T("settings.tiles.labels", "Beschriftung unter den Symbolen");

    public static string TileOrderHeading => T("settings.tiles.orderHeading", "Kacheln: Reihenfolge, Sichtbarkeit, Größe");

    public static string TileOrderHint => T("settings.tiles.orderHint", "Reihenfolge per Ziehen einer Zeile oder mit den Pfeilen. „Nur Symbol“ verkleinert eine einzelne Kachel unabhängig von der Standardgröße. Kacheln mit Untermenü zeigen in der Leiste einen kleinen Pfeil.");

    public static string TileVisible => T("settings.tiles.visible", "Sichtbar");

    public static string TileIconOnly => T("settings.tiles.iconOnly", "Nur Symbol");

    public static string TileResetOrder => T("settings.tiles.resetOrder", "Standardreihenfolge");

    public static string TilesClassicHint => T("settings.tiles.classicHint", "Im klassischen Layout gibt es keine Kachelleiste. Die Einstellungen gelten, sobald das neue Layout gewählt ist.");

    public static string TileNeedsGuiTools => T("settings.tiles.needsGuiTools", "Nur mit eingeschalteten GUI-Werkzeugen (Kategorie GUI-Werkzeuge)");

    /// <summary>Name der Kachel "Desync-Analyse" (das Desync-Projekt ist von den Einstellungen aus nicht erreichbar).</summary>
    public static string TileDesync => T("settings.tiles.desync", "Desync-Analyse");

    // Werkzeugleisten
    public static string ToolbarItemsHeading => T("settings.toolbars.itemsHeading", "Einträge der kleinen Werkzeugleiste");

    public static string ToolbarLabelsVisible => T("settings.toolbars.labels", "Beschriftung neben den Symbolen (nur waagerecht)");

    public static string ToolbarItemsHint => T("settings.toolbars.itemsHint", "Reihenfolge per Ziehen einer Zeile oder mit den Pfeilen. Trennlinien sind eigene Einträge: verschieben, entfernen oder neue hinzufügen. Startoption, Playset und Spiel starten stehen zusätzlich unter Allgemein.");

    public static string ToolbarSeparator => T("settings.toolbars.separator", "Trennlinie");

    public static string ToolbarAddSeparator => T("settings.toolbars.addSeparator", "Trennlinie hinzufügen");

    public static string ToolbarResetOrder => T("settings.toolbars.resetOrder", "Standardreihenfolge");

    public static string ToolbarClassicHint => T("settings.toolbars.classicHint", "Im klassischen Layout hat die Werkzeugleiste feste Gruppen; hier lassen sich einzelne Einträge ausblenden.");

    public static string WorkspaceButtonsHeading => T("settings.toolbars.workspaceButtons", "Knöpfe je Arbeitsbereich");

    public static string WorkspaceButtonsHint => T("settings.toolbars.workspaceHint", "Kategorie, Filter, Symbole und Sortierung bleiben je Arbeitsbereich. Notizen, Lesezeichen und Bug können zusätzlich dort stehen; sie wirken auf das aktive Dokument dieses Arbeitsbereichs.");

    public static string WorkspaceCategoryButton => T("settings.toolbars.categoryButton", "Kategorie-Button in der Werkzeugleiste anzeigen");

    public static string WorkspaceSortButton => T("settings.toolbars.sortButton", "Sortierungs-Button in der Werkzeugleiste anzeigen");

    public static string WorkspaceMarkButtons => T("settings.toolbars.markButtons", "Notizen, Lesezeichen und Bug in der Werkzeugleiste anzeigen");

    private static string T(string key, string fallback) => UiText.Get(key, fallback);

    private static string F(string key, string fallback, params object?[] args) => UiText.Format(key, fallback, args);
}
