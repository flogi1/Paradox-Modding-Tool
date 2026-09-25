using ParadoxModTool.Localization;

namespace ParadoxModTool.Settings;

/// <summary>
/// Texte der Menüleiste und beider Werkzeugleisten (UI-Reorganisation, Plan 3). Die Keys stehen in <c>locales/de.json</c> und
/// <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen (Test <c>MenuTextsTests</c>).
/// Die Beschriftungen der allgemeinen Werkzeugleiste stehen nur in <see cref="SettingsTexts.ToolbarItemLabel"/> (dieselben Namen wie im
/// Einstellungsfenster); hier kommen nur die Emojis davor. Einstellungen (<see cref="SettingsTexts.MenuSettings"/>) und GUI
/// (<c>GuiTexts</c>) behalten ihre eigenen Texte.
/// </summary>
public static class MenuTexts
{
    // Menüleiste
    public static string Games => T("menu.games", "Spielauswahl");

    public static string GamesHeading => T("menu.games.heading", "Spiel in neuem Fenster öffnen:");

    public static string GameThisWindow(string name) => F("menu.games.thisWindow", "{0} (dieses Fenster)", name);

    public static string GameAlreadyOpen(string name) => F("menu.games.alreadyOpen", "{0} (schon geöffnet)", name);

    public static string Mods => T("menu.mods", "Mods");

    public static string ModsTip => T("menu.mods.tip", "Mods dieses Spiels verwalten: umbenennen, Ordner verknüpfen, entfernen");

    public static string Project => T("menu.project", "Mod-Projekt");

    public static string ProjectTip => T("menu.project.tip", "Aktive Datei zwischen den Mods eines Projekts kopieren, verschieben oder vergleichen");

    public static string ProjectCopy => T("menu.project.copy", "Aktive Datei in andere Mod kopieren");

    public static string ProjectMove => T("menu.project.move", "Aktive Datei in andere Mod verschieben");

    public static string ProjectCompare => T("menu.project.compare", "Aktive Datei mit anderer Mod vergleichen");

    public static string ProjectSync => T("menu.project.sync", "Abgleich");

    public static string ProjectSyncTip => T("menu.project.sync.tip", "Zwei Mods des Projekts ordnerweit vergleichen und einzelne Dateien übernehmen");

    public static string CuTool => T("menu.cuTool", "C-U-Tool");

    public static string CuToolTip => T("menu.cuTool.tip", "Compare-Update-Tool: vergleicht Mod-Dateien mit Vanilla, mit Einklappen und optionalem 3-Wege-Merge nach Spiel-Updates");

    public static string ModCompat => T("menu.modCompat", "Mod-Kompatibilität prüfen");

    public static string ModCompatTip => T("menu.modCompat.tip", "Mods gegeneinander prüfen: Datei- und Schlüsselkonflikte, Sinnhaftigkeit von Überschreibungen und ein Bericht für Kompatibilitätspatches");

    public static string Compatch => T("menu.compatch", "Komp-Patch Tool");

    public static string CompatchTip => T("menu.compatch.tip", "Zwei Mods zusammenführen und das Ergebnis als Kompatibilitätspatch oder in eine eigene Mod schreiben");

    public static string ScriptDocs => T("menu.scriptDocs", "Script-Datenbank");

    public static string ScriptDocsTip => T("menu.scriptDocs.tip", "Modifier, Trigger, Effects, Event-Targets und mehr nachschlagen (aus den Doku-Logs des Spiels)");

    public static string Templates => T("menu.templates", "Template Generator");

    public static string TemplatesTip => T("menu.templates.tip", "Aus einem Beispiel-Snippet mehrere Blöcke (und passende Lokalisierung) mit hochgezählten Nummern erzeugen");

    public static string Help => T("menu.help", "Hilfe");

    public static string CheckUpdates => T("menu.help.checkUpdates", "Nach Updates suchen");

    public static string CheckUpdatesTip => T("menu.help.checkUpdates.tip", "Bei GitHub nach einer neueren Version suchen");

    public static string ReportBug => T("menu.help.reportBug", "Fehler im Tool melden");

    public static string ReportBugTip => T("menu.help.reportBug.tip", "Öffnet ein neues GitHub-Issue mit Programmversion und Systemangaben");

    public static string NoRepositoryTip => T("menu.help.noRepository", "Noch nicht verfügbar: In AppInfo.GitHubRepository ist kein GitHub-Repository eingetragen.");

    public static string About => T("menu.help.about", "Über Paradox Modding Tool");

    // Editor-Kontextmenü
    public static string EditorNewBug => T("menu.editor.newBug", "Neuer Bug hier");

    public static string EditorAttachBug => T("menu.editor.attachBug", "An Bugreport anhängen");

    // Allgemeine Werkzeugleiste
    public static string ToolbarLabel(string id) => id switch
    {
        ToolbarItemIds.Bookmark => "🔖 " + SettingsTexts.ToolbarItemLabel(id),
        ToolbarItemIds.Bug => "🐞 " + SettingsTexts.ToolbarItemLabel(id),
        ToolbarItemIds.BugTracker => "📋 " + SettingsTexts.ToolbarItemLabel(id),
        ToolbarItemIds.LaunchGame => "▶ " + SettingsTexts.ToolbarItemLabel(id),
        _ => SettingsTexts.ToolbarItemLabel(id),
    };

    public static string ToolbarTip(string id) => id switch
    {
        ToolbarItemIds.Undo => T("menu.toolbar.undo.tip", "Letzte Programmaktion rückgängig machen, z. B. Speichern oder Ersetzen in Dateien"),
        ToolbarItemIds.Redo => T("menu.toolbar.redo.tip", "Zuletzt rückgängig gemachte Programmaktion wiederholen"),
        ToolbarItemIds.Search => T("menu.toolbar.search.tip", "Suchen & Ersetzen, auch in allen Dateien eines Ordners (Strg+F)"),
        ToolbarItemIds.GoToLine => T("menu.toolbar.goToLine.tip", "Zu einer Zeile im aktiven Dokument springen (Strg+G)"),
        ToolbarItemIds.GoToBracket => T("menu.toolbar.goToBracket.tip", "Zur Klammer springen, die zu der am Cursor gehört (Strg+B); ein zweiter Sprung führt zurück"),
        ToolbarItemIds.ScopeFinder => T("menu.toolbar.scopeFinder.tip", "Wege zwischen Scopes vorschlagen, z. B. von root zu owner.capital; Einfügen per Doppelklick (Strg+Umschalt+P)"),
        ToolbarItemIds.Templates => TemplatesTip,
        ToolbarItemIds.Format => T("menu.toolbar.format.tip", "Überschüssige Leerzeichen und Leerzeilen bereinigen oder die Struktur des aktiven Dokuments neu formatieren; rückgängig mit Strg+Z"),
        ToolbarItemIds.Notes => T("menu.toolbar.notes.tip", "Persönliche Notiz zur aktiven Datei. Sie steht nicht in der Datei, nur im Programm."),
        ToolbarItemIds.Bookmark => T("menu.toolbar.bookmark.tip", "Lesezeichen mit Kommentar an der markierten Stelle oder Cursorzeile setzen, ändern oder entfernen; Strg+F2 setzt schnell eines ohne Kommentar"),
        ToolbarItemIds.Bug => T("menu.toolbar.bug.tip", "Neuen Bug zur aktiven Datei anlegen oder die Stelle an eine bestehende Meldung anhängen, mit Cursorzeile oder Markierung als Zeilenbezug"),
        ToolbarItemIds.BugTracker => T("menu.toolbar.bugTracker.tip", "Bugtracker mit allen Meldungen des Projekts öffnen; die Zahl zeigt neue Meldungen"),
        ToolbarItemIds.SplitView => T("menu.toolbar.splitView.tip", "Bildschirm in zwei Arbeitsbereiche teilen"),
        ToolbarItemIds.Explorer => T("menu.toolbar.explorer.tip", "Verzeichnis-Übersicht ein- oder ausblenden"),
        ToolbarItemIds.NotesPanel => T("menu.toolbar.notesPanel.tip", "Rechtes Panel mit Notiz und Lesezeichen der aktiven Datei ein- oder ausblenden"),
        ToolbarItemIds.AutoComplete => T("menu.toolbar.autoComplete.tip", "Vorschlagsliste beim Tippen (Autovervollständigung für Script-Namen) ein- oder ausschalten"),
        ToolbarItemIds.Hover => T("menu.toolbar.hover.tip", "Tooltip mit Beschreibung und Scopes beim Verweilen auf Modifiern, Triggern und Effects ein- oder ausschalten"),
        ToolbarItemIds.LaunchOption => T("menu.toolbar.launchOption.tip", "Startoption für das Spiel (aus dem aktiven Spielprofil)"),
        ToolbarItemIds.Playset => T("menu.toolbar.playset.tip", "Playset, mit dem gestartet wird (aus playsets.json); wird beim Start als aktives Playset gesetzt, falls abweichend"),
        ToolbarItemIds.LaunchGame => T("menu.toolbar.launchGame.tip", "Startet die eingestellte Spiel-.exe mit der gewählten Startoption"),
        _ => id,
    };

    /// <summary>Tooltip von "Rückgängig": nennt die nächste Programmaktion, sonst der allgemeine Tooltip.</summary>
    public static string UndoTip(string? next) => next is null ? ToolbarTip(ToolbarItemIds.Undo) : F("menu.toolbar.undo.next", "Rückgängig: {0}", next);

    /// <summary>Tooltip von "Wiederholen": nennt die nächste Programmaktion, sonst der allgemeine Tooltip.</summary>
    public static string RedoTip(string? next) => next is null ? ToolbarTip(ToolbarItemIds.Redo) : F("menu.toolbar.redo.next", "Wiederholen: {0}", next);

    // Arbeitsbereich-Werkzeugleiste
    public static string WorkspaceNew => T("menu.workspace.new", "Neu");

    public static string WorkspaceNewTip => T("menu.workspace.new.tip", "Neues, leeres Dokument in diesem Arbeitsbereich");

    public static string WorkspaceOpen => T("menu.workspace.open", "Öffnen");

    public static string WorkspaceOpenTip => T("menu.workspace.open.tip", "Datei in diesem Arbeitsbereich öffnen");

    public static string WorkspaceSave => T("menu.workspace.save", "Speichern");

    public static string WorkspaceSaveTip => T("menu.workspace.save.tip", "Aktives Dokument dieses Arbeitsbereichs speichern (Strg+S)");

    public static string WorkspaceSaveAll => T("menu.workspace.saveAll", "Alle speichern");

    public static string WorkspaceSaveAllTip => T("menu.workspace.saveAll.tip", "Alle ungespeicherten Dokumente beider Arbeitsbereiche speichern (Strg+Umschalt+S)");

    public static string WorkspaceClose => T("menu.workspace.close", "Schließen");

    public static string WorkspaceCloseTip => T("menu.workspace.close.tip", "Aktives Dokument dieses Arbeitsbereichs schließen");

    public static string WorkspaceMove => T("menu.workspace.move", "⇄");

    public static string WorkspaceMoveTip => T("menu.workspace.move.tip", "Aktives Dokument in den anderen Arbeitsbereich verschieben");

    public static string WorkspaceCategory => T("menu.workspace.category", "Kategorie ▾");

    public static string WorkspaceCategoryTip => T("menu.workspace.category.tip", "Kategorie des aktiven Dokuments festlegen (überschreibt die automatische Zuordnung)");

    public static string WorkspaceFilter => T("menu.workspace.filter", "Filter ▾");

    public static string WorkspaceFilterTip => T("menu.workspace.filter.tip", "Festlegen, welche Filter-Häkchen die Dokumentenleiste zeigt");

    public static string WorkspaceIcons => T("menu.workspace.icons", "Symbole ▾");

    public static string WorkspaceIconsTip => T("menu.workspace.icons.tip", "Festlegen, welche Symbole neben den Dokumenten stehen");

    private static string T(string key, string fallback) => UiText.Get(key, fallback);

    private static string F(string key, string fallback, params object?[] args) => UiText.Format(key, fallback, args);
}
