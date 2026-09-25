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
        SettingsCategory.Toolbar => T("settings.category.toolbar", "Allgemeine Werkzeugleiste"),
        SettingsCategory.Workspace => T("settings.category.workspace", "Arbeitsbereich"),
        SettingsCategory.WorkspaceToolbar => T("settings.category.workspaceToolbar", "Werkzeugleiste Arbeitsbereich"),
        SettingsCategory.NotesPanel => T("settings.category.notesPanel", "Notiz/Lesezeichen-Panel"),
        SettingsCategory.Compatch => T("settings.category.compatch", "Komp-Patch Tool"),
        SettingsCategory.GuiTools => T("settings.category.guiTools", "GUI-Werkzeuge"),
        SettingsCategory.Layout => T("settings.category.layout", "Layout"),
        SettingsCategory.Language => T("settings.category.language", "Sprache"),
        SettingsCategory.Themes => T("settings.category.themes", "Themes"),
        SettingsCategory.Hotkeys => T("settings.category.hotkeys", "Hotkeys"),
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

    public static string RelearnScopeRulesTip => T("settings.general.relearnScope.tip", "Liest die Vanilla-Dateien erneut und lernt daraus die Start-Scopes der Felder (geschieht sonst automatisch nach Spiel-Updates und neuen Doku-Logs)");

    public static string ShowInToolbar => T("settings.general.showInToolbar", "In der Werkzeugleiste anzeigen");

    // 2. Allgemeine Werkzeugleiste
    public static string ToolbarHint => T("settings.toolbar.hint", "Blendet einzelne Einträge der allgemeinen Werkzeugleiste ein oder aus. Startoption, Playset und Spiel starten stehen unter Allgemein.");

    public static string ToolbarGroupLabel(ToolbarGroup group) => group switch
    {
        ToolbarGroup.History => T("settings.toolbarGroup.history", "Rückgängig und Wiederholen"),
        ToolbarGroup.Tools => T("settings.toolbarGroup.tools", "Werkzeuge"),
        ToolbarGroup.Toggles => T("settings.toolbarGroup.toggles", "Ansicht und Schalter"),
        ToolbarGroup.Launch => T("settings.toolbarGroup.launch", "Spiel starten"),
        _ => group.ToString(),
    };

    public static string ToolbarItemLabel(string id) => id switch
    {
        ToolbarItemIds.Undo => T("settings.toolbarItem.undo", "Rückgängig"),
        ToolbarItemIds.Redo => T("settings.toolbarItem.redo", "Wiederholen"),
        ToolbarItemIds.Search => T("settings.toolbarItem.search", "Suchen & Ersetzen"),
        ToolbarItemIds.GoToLine => T("settings.toolbarItem.goToLine", "Gehe zu Zeile"),
        ToolbarItemIds.GoToBracket => T("settings.toolbarItem.goToBracket", "Gehe zu Klammer"),
        ToolbarItemIds.ScopeFinder => T("settings.toolbarItem.scopeFinder", "Scope Finder"),
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

    public static string AutoCompleteTip => T("settings.workspace.autoComplete.tip", "Vorschlagsliste beim Tippen (ab 3 Zeichen) und mit Strg+Leertaste, aus der Script-Datenbank. Ausgeschaltet gibt es auch mit Strg+Leertaste keine Vorschläge.");

    public static string DocumentBar => T("settings.workspace.documentBar", "Dokumentenleiste mit Filtern anzeigen");

    public static string DocumentBarTip => T("settings.workspace.documentBar.tip", "Kategorie-Häkchen und Filter (mit Notiz/Lesezeichen) über den Tabs ein- oder ausblenden");

    public static string MultiRowTabs => T("settings.workspace.multiRowTabs", "Tabs mehrzeilig anzeigen");

    public static string MultiRowTabsTip => T("settings.workspace.multiRowTabs.tip", "Zeigt die offenen Dokumente in mehreren Zeilen statt in einer scrollbaren Zeile (Standard: aus)");

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

    public static string ResultsOnTop => T("settings.workspace.resultsOnTop", "Suchergebnisse oben anzeigen");

    public static string ResultsOnTopTip => T("settings.workspace.resultsOnTop.tip", "Legt fest, ob die Suchergebnisleiste über oder unter dem Arbeitsbereich erscheint (Standard: unten)");

    // 4. Werkzeugleiste Arbeitsbereich
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
        _ => id,
    };

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

    public static string CompatchDefaultView => T("settings.compatch.defaultView", "Standardansicht");

    // 8. Layout
    public static string ColorizeWholeTab => T("settings.layout.colorizeTab", "Farbcodes färben den ganzen Reiter ein");

    public static string ColorizeWholeTabTip => T("settings.layout.colorizeTab.tip", "Färbt den kompletten Tab-Reiter halbtransparent in der Kategorie-Farbe ein, statt nur den schmalen Balken links (Standard: aus)");

    public static string IconColorsHeading(string themeName) => F("settings.layout.iconHeading", "Symbolfarben im Theme „{0}“", themeName);

    public static string IconColorsHint => T("settings.layout.iconHint", "Gilt nur für das aktuelle Theme. Notiz und Vanilla werden ganz eingefärbt; bei Lesezeichen und Mod färbt die Farbe die Plakette hinter dem Symbol, bei Bug ihren Rand. Mod hat ohne eigene Farbe keine Plakette.");

    public static string PickColorTip => T("settings.layout.pick.tip", "Farbe wählen");

    public static string Reset => T("settings.layout.reset", "Zurücksetzen");

    // 9. Sprache
    public static string LanguageHint => T("settings.language.hint", "Wirkt nach einem Neustart. Ältere Fenster sind teilweise noch auf Deutsch.");

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
        HotkeyGroup.CuTool => T("settings.hotkeyGroup.cuTool", "C-U-Tool"),
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
        "cu.search" => T("settings.hotkey.cu.search", "Suchen & Ersetzen öffnen"),
        "cu.save" => T("settings.hotkey.cu.save", "Rechte Seite speichern"),
        "cu.version" => T("settings.hotkey.cu.version", "Im Versionsfeld: Version übernehmen"),
        "gui.inspect" => T("settings.hotkey.gui.inspect", "Gewähltes Element inspizieren"),
        "gui.toList" => T("settings.hotkey.gui.toList", "Vom Suchfeld in die Trefferliste wechseln"),
        "publish.saveDescription" => T("settings.hotkey.publish.saveDescription", "Beschreibung speichern"),
        "publish.version" => T("settings.hotkey.publish.version", "Im Versionsfeld: Version übernehmen"),
        "publish.saveRelease" => T("settings.hotkey.publish.saveRelease", "Release-Text speichern"),
        "templates.find" => T("settings.hotkey.templates.find", "Ins Suchfeld springen"),
        "templates.next" => T("settings.hotkey.templates.next", "Nächster Treffer"),
        "templates.enter" => T("settings.hotkey.templates.enter", "Im Suchfeld: nächster Treffer; in einem Listenfeld: Wert übernehmen"),
        "dialogs.cancel" => T("settings.hotkey.dialogs.cancel", "Eingabedialog abbrechen"),
        "bugs.reject" => T("settings.hotkey.bugs.reject", "Im Bugtracker: gewählte Meldung verwerfen"),
        _ => textId,
    };

    // Fortschrittsfenster (Neu-Lernen, GUI-Index)
    public static string ProgressHint => T("settings.progress.hint", "Das kann eine Weile dauern. Abbrechen behält den bisherigen Stand.");

    public static string ProgressFiles => T("settings.progress.files", "Dateien gelesen");

    public static string ProgressCount(string text, int done, int total) => F("settings.progress.count", "{0}: {1:N0} von {2:N0}", text, done, total);

    public static string Cancel => T("settings.progress.cancel", "Abbrechen");

    // 12. Update
    public static string UpdateOnStartup => T("settings.update.onStartup", "Beim Start nach Updates suchen");

    public static string UpdateOnStartupTip => T("settings.update.onStartup.tip", "Fragt beim Programmstart (höchstens einmal täglich) bei GitHub nach einer neuen Version. Es werden keine persönlichen Daten gesendet.");

    public static string UpdateNow => T("settings.update.now", "Jetzt suchen");

    public static string UpdatesUnavailable => T("settings.update.unavailable", "Dieser Build kennt kein GitHub-Repository; die Update-Suche ist nicht verfügbar.");

    private static string T(string key, string fallback) => UiText.Get(key, fallback);

    private static string F(string key, string fallback, params object?[] args) => UiText.Format(key, fallback, args);
}
