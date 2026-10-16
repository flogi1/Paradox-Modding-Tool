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

    public static string ModsTip => T("menu.mods.tip", "Aktive Mod wählen (fest oder automatisch nach offener Datei) und Mods dieses Spiels verwalten");

    public static string ModsManage => T("menu.mods.manage", "Mods verwalten");

    public static string ModsManageTip => T("menu.mods.manage.tip", "Mods dieses Spiels verwalten: umbenennen, Ordner verknüpfen, entfernen");

    public static string ModsAutomatic => T("menu.mods.automatic", "Automatisch (offene Datei)");

    public static string ModsAutomaticTip => T("menu.mods.automatic.tip", "Die aktive Mod folgt der Datei, die im Editor offen ist; bei einer Vanilla-Datei gilt der erste Mod-Ordner der Verzeichnis-Übersicht");

    public static string MyMods => T("menu.myMods", "Meine Mods");

    public static string MyModsTip => T("menu.myMods.tip", "Übersicht über die eigenen Mods aller Spiele: Steam-Zahlen, Bugs, Releases und Hinweise");

    /// <summary>Interface-Rework 2: Menüpunkt für das Fenster "Meine Mods" (spielübergreifende Übersicht).</summary>
    public static string Overview => T("menu.overview", "Mod-Übersicht");

    public static string OverviewTip => MyModsTip;

    /// <summary>Interface-Rework 2: Menü mit der aktiven Mod und der Mod-Verwaltung, mit dem Spielkürzel aus dem Profil.</summary>
    public static string MyGameMods(string shortName) => F("menu.myGameMods", "Meine {0} Mods", shortName);

    public static string Project => T("menu.project", "Mod-Projekt");

    public static string ProjectTip => T("menu.project.tip", "Aktive Datei zwischen den Mods eines Projekts kopieren, verschieben oder vergleichen");

    public static string ProjectCopy => T("menu.project.copy", "Aktive Datei in andere Mod kopieren");

    public static string ProjectMove => T("menu.project.move", "Aktive Datei in andere Mod verschieben");

    public static string ProjectCompare => T("menu.project.compare", "Aktive Datei mit anderer Mod vergleichen");

    public static string ProjectSync => T("menu.project.sync", "Abgleich");

    public static string ProjectSyncTip => T("menu.project.sync.tip", "Zwei Mods des Projekts ordnerweit vergleichen und einzelne Dateien übernehmen");

    public static string CuTool => T("menu.cuTool", "Compare und Update Tool");

    public static string CuToolTip => T("menu.cuTool.tip", "Compare und Update Tool: zwei Dateien oder Ordner vergleichen (Compare) und Mod-Dateien mit Vanilla abgleichen, mit 3-Wege-Merge nach Spiel-Updates (Update). Markierte Zeilen übernimmt U in die andere Seite. Ersetzungslisten ersetzen gespeicherte Wörter in einem Mod-Ordner mit einem Knopf, z.B. nach einem Spiel-Patch.");

    public static string PatchRecipes => T("menu.patchRecipes", "Patch-Rezepte");

    public static string PatchRecipesTip => T("menu.patchRecipes.tip", "Patch-Rezepte: wiederholbare Regel-Patches für die Mod (Tabelle plus Regeln), nach jedem Vanilla-Update erneut anwendbar");

    public static string GeneratorPackages => T("menu.generatorPackages", "Generator-Pakete");

    public static string PackageEditor => T("menu.packageEditor", "Paket-Editor");

    public static string PackageEditorTip => T("menu.packageEditor.tip", "Paket-Editor: Generator-Pakete mit Masken anlegen und bearbeiten (Formular, Ausgaben, Vorlagen), prüfen, exportieren und importieren");

    public static string GeneratorPackagesTip => T("menu.generatorPackages.tip", "Generator-Pakete: aus einem Formular viele Mod-Dateien auf einmal erzeugen (z. B. ein Nationale-Ideen-Set), mit Vorschau und einem Rückgängig-Schritt");

    public static string ModCompat => T("menu.modCompat", "Mod-Kompatibilität prüfen");

    public static string ModCompatTip => T("menu.modCompat.tip", "Mods gegeneinander prüfen: Datei- und Schlüsselkonflikte, Sinnhaftigkeit von Überschreibungen und ein Bericht für Kompatibilitätspatches");

    public static string Playsets => T("menu.playsets", "Playsets verwalten");

    public static string PlaysetsTip => T("menu.playsets.tip", "Playsets des Spiels anlegen, ändern, löschen und das Spiel damit starten (Mods, DLCs, Sicherungen)");

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

    public static string Changelog => T("menu.help.changelog", "Changelog");

    public static string ChangelogTip => T("menu.help.changelog.tip", "Öffnet die Liste der Änderungen je Version (Readme\\Changelog.txt)");

    public static string ChangelogMissingTip => T("menu.help.changelog.missing", "Readme\\Changelog.txt fehlt im Programmordner.");

    public static string NoRepositoryTip => T("menu.help.noRepository", "Noch nicht verfügbar: In AppInfo.GitHubRepository ist kein GitHub-Repository eingetragen.");

    // Menü "Unterstützen"
    public static string Support => T("menu.support", "Unterstützen");

    public static string SupportTip => T("menu.support.tip", "PMT bleibt kostenlos; eine Spende hilft bei der Weiterentwicklung.");

    public static string SupportGitHub => T("menu.support.github", "GitHub Sponsors");

    public static string SupportGitHubTip => T("menu.support.github.tip", "Öffnet die GitHub-Sponsors-Seite im Browser");

    public static string SupportKofi => T("menu.support.kofi", "Ko-fi");

    public static string SupportKofiTip => T("menu.support.kofi.tip", "Öffnet die Ko-fi-Seite im Browser");

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
        ToolbarItemIds.ModifierReload => "↻ " + SettingsTexts.ToolbarItemLabel(id),
        _ => SettingsTexts.ToolbarItemLabel(id),
    };

    /// <summary>
    /// Beschriftung einer Kachel (<see cref="TileIds"/>): die heutigen Menütexte, für "Projekt verwalten" und "GUI" eigene Schlüssel.
    /// Texte aus anderen Projekten (Error-Log, Desync, Save Explorer) liefert <paramref name="external"/>; ohne Treffer steht die Kennung.
    /// </summary>
    public static string TileLabel(string tileId, Func<string, string?>? external = null) => tileId switch
    {
        TileIds.Project => T("menu.tile.project", "Projekt verwalten"),
        TileIds.CuTool => CuTool,
        TileIds.Compatch => Compatch,
        TileIds.ModCompat => ModCompat,
        TileIds.Playsets => Playsets,
        TileIds.PatchRecipes => PatchRecipes,
        TileIds.ScriptDocs => ScriptDocs,
        TileIds.ScopeFinder => SettingsTexts.ToolbarItemLabel(ToolbarItemIds.ScopeFinder),
        TileIds.Gui => T("menu.tile.gui", "GUI"),
        TileIds.GuiDataHelper => SettingsTexts.ToolbarItemLabel(ToolbarItemIds.GuiDataHelper),
        TileIds.Templates => Templates,
        TileIds.GeneratorPackages => GeneratorPackages,
        TileIds.PackageEditor => PackageEditor,
        TileIds.BugTracker => SettingsTexts.ToolbarItemLabel(ToolbarItemIds.BugTracker),
        _ => external?.Invoke(tileId) ?? tileId,
    };

    /// <summary>Tooltip einer Kachel; Quellen wie bei <see cref="TileLabel"/>.</summary>
    public static string TileTip(string tileId, Func<string, string?>? external = null) => tileId switch
    {
        TileIds.Project => ProjectTip,
        TileIds.CuTool => CuToolTip,
        TileIds.Compatch => CompatchTip,
        TileIds.ModCompat => ModCompatTip,
        TileIds.Playsets => PlaysetsTip,
        TileIds.PatchRecipes => PatchRecipesTip,
        TileIds.ScriptDocs => ScriptDocsTip,
        TileIds.ScopeFinder => ToolbarTip(ToolbarItemIds.ScopeFinder),
        TileIds.Gui => T("menu.tile.gui.tip", "GUI-Inspektor, Datenhelfer, Bausteine, neue GUI-Datei und GUI-Einstellungen der Mod"),
        TileIds.GuiDataHelper => ToolbarTip(ToolbarItemIds.GuiDataHelper),
        TileIds.Templates => TemplatesTip,
        TileIds.GeneratorPackages => GeneratorPackagesTip,
        TileIds.PackageEditor => PackageEditorTip,
        TileIds.BugTracker => ToolbarTip(ToolbarItemIds.BugTracker),
        _ => external?.Invoke(tileId) ?? tileId,
    };

    public static string ToolbarTip(string id) => id switch
    {
        ToolbarItemIds.New => WorkspaceNewTip,
        ToolbarItemIds.Open => WorkspaceOpenTip,
        ToolbarItemIds.Save => WorkspaceSaveTip,
        ToolbarItemIds.SaveAll => WorkspaceSaveAllTip,
        ToolbarItemIds.Close => WorkspaceCloseTip,
        ToolbarItemIds.Move => WorkspaceMoveTip,
        ToolbarItemIds.Undo => T("menu.toolbar.undo.tip", "Letzte Programmaktion rückgängig machen, z. B. Speichern oder Ersetzen in Dateien"),
        ToolbarItemIds.Redo => T("menu.toolbar.redo.tip", "Zuletzt rückgängig gemachte Programmaktion wiederholen"),
        ToolbarItemIds.Search => T("menu.toolbar.search.tip", "Suchen & Ersetzen, auch in allen Dateien eines Ordners (Strg+F)"),
        ToolbarItemIds.GoToLine => T("menu.toolbar.goToLine.tip", "Zu einer Zeile im aktiven Dokument springen (Strg+G)"),
        ToolbarItemIds.GoToBracket => T("menu.toolbar.goToBracket.tip", "Zur Klammer springen, die zu der am Cursor gehört (Strg+B); ein zweiter Sprung führt zurück"),
        ToolbarItemIds.ScopeFinder => T("menu.toolbar.scopeFinder.tip", "Scope-Ketten bauen oder Wege zwischen Scopes suchen, z. B. von root zu owner.capital (Strg+Umschalt+P)"),
        ToolbarItemIds.GuiDataHelper => T("menu.toolbar.guiDataHelper.tip", "GUI-Datentypen nachschlagen und einen Ausdruck wie [Country.GetCapital.GetName] bauen (Strg+Umschalt+D)"),
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
        ToolbarItemIds.ModifierReload => T("menu.toolbar.modifierReload.tip", "Liest Formate, Script-Values, Konstanten und Modifier-Namen von Spiel und Mod neu ein, z. B. nach Änderungen an script_values-Dateien"),
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

    /// <summary>Notizen-Knopf der Arbeitsbereich-Leiste: Emoji wie bei Lesezeichen und Bug, Text aus den Werkzeugleisten-Namen (kein eigener Schlüssel).</summary>
    public static string WorkspaceNotes => "🗒 " + SettingsTexts.ToolbarItemLabel(ToolbarItemIds.Notes);

    /// <summary>Interface-Rework 2: Menü "Sortierung ▾" der Arbeitsbereich-Leiste (Werte siehe <c>TabSorting</c> im Editor-Projekt).</summary>
    public static string WorkspaceSort => T("menu.workspace.sort", "Sortierung ▾");

    public static string WorkspaceSortTip => T("menu.workspace.sort.tip", "Reihenfolge der Tabs dieses Arbeitsbereichs: manuell (Ziehen) oder automatisch nach Name, Kategorie oder Herkunft");

    public static string WorkspaceSortManual => T("menu.workspace.sort.manual", "Manuell (Ziehen)");

    public static string WorkspaceSortName => T("menu.workspace.sort.name", "Name");

    public static string WorkspaceSortCategory => T("menu.workspace.sort.category", "Kategorie");

    public static string WorkspaceSortOrigin => T("menu.workspace.sort.origin", "Herkunft (Vanilla, Mod, Extern)");

    private static string T(string key, string fallback) => UiText.Get(key, fallback);

    private static string F(string key, string fallback, params object?[] args) => UiText.Format(key, fallback, args);
}
