using ParadoxModTool.Categories;

namespace ParadoxModTool.Session;

/// <summary>
/// Gesamter beim Beenden gespeicherter und beim nächsten Start automatisch wiederhergestellter
/// Programmzustand (Architektur-Spezifikation, Abschnitt 12): "offene Dateien, Split-View-Aufteilung,
/// aktives Spielprofil, Scroll-Position" — analog zu Notepad++.
/// </summary>
public sealed class AppSessionState
{
    public bool SplitViewEnabled { get; set; }

    /// <summary>
    /// Id des zuletzt aktiven Spielprofils (z. B. "eu5"). Wird für eine künftige Ausbaustufe mit
    /// mehreren wählbaren Spielprofilen bereits mitgespeichert; aktuell lädt die App ohnehin immer
    /// nur das eine vorhandene EU5-Profil, daher ist dieses Feld momentan noch nicht wirksam.
    /// </summary>
    public string? ActiveGameProfileId { get; set; }

    public PaneSessionState LeftPane { get; set; } = new();

    public PaneSessionState RightPane { get; set; } = new();

    /// <summary>
    /// Einstellung aus dem neuen "Einstellungen"-Menü (Nutzer-Feedback): ob die Suchergebnisleiste
    /// über oder unter dem Arbeitsbereich angezeigt wird. Standard <see langword="false"/> (unten) —
    /// eine alte, vor dieser Einstellung gespeicherte Session-Datei ohne dieses Feld deserialisiert
    /// automatisch zu diesem Standardwert.
    /// </summary>
    public bool ResultsPanelOnTop { get; set; }

    /// <summary>
    /// Frei konfigurierbare Kategorie-Farbcodes (Nutzer-Feedback Runde 4: "selbst definierbarer
    /// Farbcode" für die "Wichtig"-Kategorie; Konzept: "Die Farben sollten frei konfigurierbar
    /// sein"). Schlüssel ist der Enum-Name von ParadoxModTool.Editor.FileCategory (z. B. "Text",
    /// "Important") als einfacher String, damit dieses Projekt (ParadoxModTool.Session) NICHT auf
    /// ParadoxModTool.Editor verweisen muss - der Aufrufer (MainWindow) übersetzt beim Laden/
    /// Speichern zwischen Enum und String. Eine fehlende Kategorie bedeutet "Standardfarbe
    /// verwenden".
    /// </summary>
    public Dictionary<string, string> CategoryColorOverrides { get; set; } = new();

    /// <summary>
    /// Einstellung aus dem "Einstellungen"-Menü (Nutzer-Feedback Runde 4: "Schaffe die Möglichkeit
    /// ... dass die Farbcodes oben in der Dokumentenliste den ganzen Reiter einfärben."). Standard
    /// <see langword="false"/> (nur der schmale Balken links, wie in Runde 1-3) — eine alte, vor
    /// dieser Einstellung gespeicherte Session-Datei ohne dieses Feld deserialisiert automatisch
    /// zu diesem Standardwert.
    /// </summary>
    public bool ColorizeWholeTab { get; set; }

    /// <summary>
    /// Vom Nutzer eingestellter Pfad zur Spiel-.exe (Konzept, Abschnitt 8: "Es wäre super, wenn man
    /// das Spiel direkt aus dem Programm starten könnte"). Wird beim ersten Startversuch (falls noch
    /// nicht gesetzt) sowie über "Einstellungen &gt; Spielpfad einstellen…" erfragt und danach hier
    /// gespeichert, damit der Nutzer den Pfad nicht bei jedem Programmstart erneut angeben muss.
    /// <see langword="null"/>, solange noch kein Pfad eingestellt wurde.
    /// </summary>
    public string? GameExecutablePath { get; set; }

    /// <summary>
    /// Vom Nutzer eingestellter Pfad zu playsets.json (Nutzer-Feedback: "Es sollte noch zusätzlich
    /// bei der Startoption die Möglichkeit geben, das Playset direkt auszuwählen."). Diese Datei
    /// liegt NICHT im Mod-Ordner, sondern separat unter "Documents/Paradox Interactive/&lt;Spiel&gt;/",
    /// daher ein eigenes Feld statt Wiederverwendung von <see cref="GameExecutablePath"/>.
    /// <see langword="null"/>, solange noch keiner eingestellt wurde bzw. kein Standardpfad
    /// automatisch gefunden werden konnte.
    /// </summary>
    public string? PlaysetsFilePath { get; set; }

    /// <summary>Vom Nutzer gewählter Logordner des Fehlerprotokolls; <see langword="null"/> = Ordner "logs" neben dem Mod-Ordner des Spiels.</summary>
    public string? ErrorLogFolder { get; set; }

    /// <summary>
    /// Wurzeln der Verzeichnis-Übersicht (Nutzer-Feedback Runde 10: "eine Verzeichnis Übersicht
    /// wie bei Notepad++ ... weitere Ordnerstrukturen ... bei Bedarf hinzufügen können"). Rein
    /// additiv - eine alte, vor dieser Erweiterung gespeicherte Session-Datei ohne dieses Feld
    /// deserialisiert automatisch zu einer leeren Liste (dann schlägt MainWindow beim ersten
    /// Start ohne gespeicherte Wurzeln automatisch den Mod-Ordner aus dem aktiven Spielprofil
    /// vor, siehe MainWindow.xaml.cs).
    /// </summary>
    public List<ExplorerRootState> ExplorerRoots { get; set; } = new();

    /// <summary>Verzeichnis-Übersicht: Lupe und die Knöpfe für den linken und rechten Arbeitsbereich links vor dem Namen. Standard: aus (rechts).</summary>
    public bool ExplorerButtonsOnLeft { get; set; }

    /// <summary>Verzeichnis-Übersicht: Fehlermarkierungen des letzten Spiellaufs hinter Dateien und Ordnern. Standard: an.</summary>
    public bool ExplorerShowErrorMarks { get; set; } = true;

    /// <summary>Verzeichnis-Übersicht: Namenssuche nur nach Ordnern. Standard: aus.</summary>
    public bool ExplorerSearchFoldersOnly { get; set; }

    /// <summary>Verzeichnis-Übersicht: Workshop-Verweise <c>ugc_*.mod</c> im Mod-Ordner ausblenden. Standard: an.</summary>
    public bool ExplorerHideUgcReferences { get; set; } = true;

    /// <summary>Aufgeklappte Ordner der Verzeichnis-Übersicht; nach einem Neustart erscheinen sie wieder aufgeklappt.</summary>
    public List<string> ExplorerExpandedPaths { get; set; } = new();

    /// <summary>
    /// Frei gewählte Farbe (Hex) für das Notiz-Icon im Dateienreiter und den "Notizen anzeigen"-
    /// Button (Nutzer-Feedback: "Ermögliche mir für den kleinen Notiz Button im
    /// Offene-Dokumente-Reiter eine extra Farbe auszuwählen. Der Notizen anzeigen Button sollte
    /// die gleiche Farbe haben."). <see langword="null"/>, solange noch keine eigene Farbe gewählt
    /// wurde. Seit der UI-Reorganisation nur noch Quelle der einmaligen Übernahme nach <see cref="IconColorsByTheme"/>;
    /// wird nicht mehr geschrieben.
    /// </summary>
    public string? NoteIconColorHex { get; set; }

    /// <summary>
    /// Eigene Farben der Tab-Symbole pro Theme: Theme-Kennung → Symbol (Note, Bookmark, Bug, Vanilla, Mod) → Hex. Fehlt ein Eintrag, gilt
    /// die Standardfarbe. <see langword="null"/> = die alte <see cref="NoteIconColorHex"/> wurde noch nicht übernommen.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>>? IconColorsByTheme { get; set; }

    /// <summary>
    /// Vom Nutzer im Vanilla-Diff-Fenster gewählter Ordner EINER konkreten Mod (Bugfix, Abschnitt
    /// 7): das aktive Spielprofil kennt nur den übergeordneten "mod"-Sammelordner (mehrere Mods je
    /// als eigener Unterordner, Standard-Paradox-Konvention), aber die automatische
    /// Vanilla-Dateierkennung braucht den Wurzelordner GENAU EINER Mod, um relative Pfade korrekt
    /// gegen den Spielordner abzugleichen. <see langword="null"/>, solange der Nutzer noch keinen
    /// eigenen Mod-Ordner gewählt hat - in diesem Fall bleibt der Sammelordner die (meist falsche)
    /// Rückfalloption.
    /// </summary>
    public string? VanillaDiffModRootOverride { get; set; }

    /// <summary>Ordner oder Datei, in dem zuletzt im Fenster "Suchen &amp; Ersetzen" gesucht wurde; das Fenster zeigt ihn beim nächsten Öffnen wieder.</summary>
    public string? SearchLastFolder { get; set; }

    /// <summary>
    /// Oberordner für Snapshots ("[Modname] Snapshots") und Vanilla-Historie ("EU V Vanilla
    /// History"), Nutzer-Feedback Task 14. <see langword="null"/> = Standard-Oberordner
    /// (Dokumente/Paradox Modding Tool).
    /// </summary>
    public string? StorageParentFolder { get; set; }

    /// <summary>
    /// <see langword="true"/> = Snapshots und Historie direkt im Mod-Ordner ablegen (nicht empfohlen,
    /// Steam lädt den gesamten Mod-Ordnerinhalt hoch). Standard <see langword="false"/>.
    /// </summary>
    public bool StoreInsideModFolder { get; set; }

    /// <summary>Tab-Leiste in mehreren umbrechenden Zeilen statt einer scrollenden Zeile (Nutzer-Feedback). Standard: aus.</summary>
    public bool MultiRowTabs { get; set; }

    /// <summary>Pfadleiste über dem Editor (Pfad, Kopieren, Ordner öffnen, Notizen anzeigen) in beiden Arbeitsbereichen. Standard: an.</summary>
    public bool PathBarVisible { get; set; } = true;

    /// <summary>Warnungen nach dem Ersetzen in einem eigenen Fenster statt im Notiz-Panel. Standard: aus.</summary>
    public bool ReplaceWarningsInWindow { get; set; }

    /// <summary>Rechtes Panel "Notizen &amp; Lesezeichen" eingeschaltet (Nutzer-Feedback). Standard: an.</summary>
    public bool NotesPanelVisible { get; set; } = true;

    /// <summary>Notiz/Lesezeichen-Panel auch dann zeigen, wenn die aktive Datei weder Notiz noch Lesezeichen hat (kein Auto-Ausblenden). Standard: aus.</summary>
    public bool NotesPanelAlwaysVisible { get; set; }

    /// <summary>
    /// Frei editierbare Dateikategorien (Name, Farbe, Regeln). <see langword="null"/> in einer alten Session
    /// bedeutet: die sieben Standardkategorien. Eingebaute Kategorien behalten ihre alten Enum-Namen als Kennung.
    /// </summary>
    public List<CategoryDefinition>? Categories { get; set; }

    /// <summary>Namen der Tab-Symbole (Note, Bookmark, Bug, Vanilla, Mod), die in der Dokumentenleiste ausgeblendet sind. Leer = alle sichtbar.</summary>
    public List<string> HiddenTabIcons { get; set; } = new();

    /// <summary>Aus der Filterleiste ausgeblendete Häkchen (Schlüssel wie "origin:External" oder "category:Text"). Leer = alle sichtbar.</summary>
    public List<string> HiddenFilterEntries { get; set; } = new();

    /// <summary>Ordner-Filter der Filterleiste (Name und Ordnerbaum); gehören zum Spiel, weil die Ordner spielbezogen sind.</summary>
    public List<FolderFilterDefinition> FolderFilters { get; set; } = new();

    /// <summary>Erledigte Bugs im Bug-Abschnitt des Panels zeigen. Standard: an.</summary>
    public bool ShowDoneBugs { get; set; } = true;

    /// <summary>Bug-Abschnitt der Notizleiste zeigt das ganze Projekt statt nur die aktive Datei. Standard: aus.</summary>
    public bool BugsPanelWholeProject { get; set; }

    /// <summary>Dokumentenleiste mit Kategorie- und Eigenschaftsfiltern sichtbar. Standard: an.</summary>
    public bool DocumentBarVisible { get; set; } = true;

    /// <summary>Beim Programmstart automatisch (höchstens einmal täglich) nach einer neuen Version suchen. Standard: an.</summary>
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    /// <summary>Zeitpunkt (UTC) der letzten Update-Prüfung - Grundlage für die tägliche Begrenzung des Auto-Checks.</summary>
    public DateTime? LastUpdateCheckUtc { get; set; }

    /// <summary>Version, für die der Nutzer "Diese Version überspringen" gewählt hat - der Hinweis erscheint erst bei einer noch neueren.</summary>
    public string? SkippedUpdateVersion { get; set; }

    /// <summary>
    /// Ausgewähltes Theme: "system" (der Windows-Einstellung folgen, Standard), "builtin-light", "builtin-dark" oder die
    /// Kennung eines eigenen Themes.
    /// </summary>
    public string ThemeId { get; set; } = "system";

    /// <summary>
    /// Persönliche Kategorie-Farb-Anpassungen an den EINGEBAUTEN Themes (Theme-Kennung auf Kategorie-Name auf Hex-Farbe).
    /// Eigene Themes speichern ihre Kategorie-Farben in ihrer eigenen Datei. Ersetzt die frühere globale
    /// <see cref="CategoryColorOverrides"/>-Liste, die nur noch zum einmaligen Übernehmen alter Sessions gelesen wird.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> CategoryColorOverridesByTheme { get; set; } = new();

    /// <summary>Hover-Tipps mit Beschreibung und Scopes im Editor. Standard: an.</summary>
    public bool ScriptHoverEnabled { get; set; } = true;

    /// <summary>Autovervollständigung von Script-Namen im Editor (ab 3 Zeichen und mit Strg+Leertaste). Standard: an.</summary>
    public bool ScriptAutoCompleteEnabled { get; set; } = true;

    /// <summary>Custom Localizations in der Vorschlagsliste der Autovervollständigung. Standard: aus.</summary>
    public bool ScriptCompleteCustomLocalizations { get; set; }

    /// <summary>Die beiden Schalter für Vorschläge und Hover-Tipps in der Werkzeugleiste anzeigen. Standard: an.</summary>
    public bool ScriptHintButtonsVisible { get; set; } = true;

    /// <summary>
    /// Ausgeblendete Einträge der allgemeinen Werkzeugleiste (Kennungen aus ParadoxModTool.Settings.ToolbarItemIds).
    /// <see langword="null"/> = noch nicht aus <see cref="ScriptHintButtonsVisible"/> übernommen (Sitzung vor der UI-Reorganisation).
    /// </summary>
    public List<string>? HiddenToolbarItems { get; set; }

    /// <summary>Statusleiste mit dem Scope am Cursor unter dem Editor. Standard: an.</summary>
    public bool ScopeStatusBarEnabled { get; set; } = true;

    /// <summary>Dieselbe Scope-Zeile zusätzlich unter der Pfadleiste über dem Editor. Standard: aus.</summary>
    public bool ScopeStatusBarTopEnabled { get; set; }

    /// <summary>Rote Wellenlinie unter Triggern und Effects, die im sicheren Scope nicht gelten. Standard: an.</summary>
    public bool ScopeWarningsEnabled { get; set; } = true;

    /// <summary>Vergleichsfenster: Blöcke einklappbar. Standard: an; aus für eine durchgehend ausgerichtete Ansicht.</summary>
    public bool DiffFoldingEnabled { get; set; } = true;

    /// <summary>Compare und Update Tool: zuletzt offener Reiter, "compare" oder "update". Standard: update.</summary>
    public string CuToolLastTab { get; set; } = "update";

    /// <summary>Abschnitt "Scope" im Notiz-Panel. Standard: an.</summary>
    public bool ScopePanelEnabled { get; set; } = true;

    /// <summary>Färbt die Klammer am Cursor und ihr Gegenstück ein. Standard: an.</summary>
    public bool BracketHighlightEnabled { get; set; } = true;

    /// <summary>Scope-Zeile ("Hier: country – erlaubt") im Hover-Tooltip. Standard: an.</summary>
    public bool ScopeHoverEnabled { get; set; } = true;

    /// <summary>Tooltip "Im Spiel: +10 %" beim Hover über eine Modifier-Zahl. Standard: an.</summary>
    public bool ModifierHoverEnabled { get; set; } = true;

    /// <summary>Übersetzter Modifier-Name hinter dem Wert im Tooltip "Im Spiel: +10 %". Standard: an.</summary>
    public bool ModifierNamesEnabled { get; set; } = true;

    /// <summary>GUI-Designer für .gui-Dateien anbieten (Umschalter Code | Designer | Geteilt). Standard: an.</summary>
    public bool GuiDesignerEnabled { get; set; } = true;

    /// <summary>Zuletzt gewählte Ansicht für .gui-Dateien: "Code", "Designer" oder "Split". Standard: Code.</summary>
    public string GuiDesignerMode { get; set; } = "Code";

    /// <summary>Aufteilung der Designer-Bereiche (siehe GuiSettings.Layout); null = Standard.</summary>
    public string? GuiDesignerLayout { get; set; }

    /// <summary>Sprache der Oberflächentexte (Datei locales/&lt;sprache&gt;.json). Standard: de.</summary>
    public string UiLanguage { get; set; } = "de";

    /// <summary>Brotkrumen-Leiste über dem Code von .gui-Dateien. Standard: an.</summary>
    public bool GuiBreadcrumbEnabled { get; set; } = true;

    /// <summary>Kontextmenü "Wird angezeigt in (GUI)" in Skriptdateien. Standard: an.</summary>
    public bool GuiShownInEnabled { get; set; } = true;

    /// <summary>GUI-Werkzeuge (Index, Designer, Brotkrumen, Inspektor, "Wird angezeigt in") eingeschaltet. Standard: an.</summary>
    public bool GuiToolsEnabled { get; set; } = true;

    /// <summary>Zuletzt gezeigte Kategorie des Einstellungsfensters (Name aus <c>SettingsCategory</c>); <see langword="null"/> vor dem ersten Öffnen. Gilt für alle Spiele.</summary>
    public string? SettingsWindowCategory { get; set; }

    /// <summary>Position und Größe des Einstellungsfensters; <see langword="null"/> vor dem ersten Schließen. Gilt für alle Spiele.</summary>
    public WindowBoundsState? SettingsWindowBounds { get; set; }

    /// <summary>Fehlerprotokoll als eigenes Fenster statt über dem Hauptfenster. Standard: aus. Gilt für alle Spiele.</summary>
    public bool ErrorLogStandalone { get; set; }

    /// <summary>Position und Größe des Fensters "Fehlerprotokoll"; <see langword="null"/> vor dem ersten Schließen. Gilt für alle Spiele.</summary>
    public WindowBoundsState? ErrorLogWindowBounds { get; set; }

    /// <summary>Spielstand-Explorer als eigenes Fenster statt über dem Hauptfenster. Standard: aus. Gilt für alle Spiele.</summary>
    public bool SaveExplorerStandalone { get; set; }

    /// <summary>Position und Größe des Fensters "Spielstand-Explorer"; <see langword="null"/> vor dem ersten Schließen. Gilt für alle Spiele.</summary>
    public WindowBoundsState? SaveExplorerWindowBounds { get; set; }

    /// <summary>Meine Mods: Ansicht "Tiles" (Kacheln) oder "List". Gilt für alle Spiele.</summary>
    public string MyModsView { get; set; } = "Tiles";

    /// <summary>Meine Mods: "Games" (Abschnitt je Spiel) oder "All" (eine Liste). Gilt für alle Spiele.</summary>
    public string MyModsGrouping { get; set; } = "Games";

    /// <summary>Meine Mods: Sortierung "Name", "Subscribers", "OpenBugs" oder "LastRelease". Gilt für alle Spiele.</summary>
    public string MyModsSort { get; set; } = "Name";

    /// <summary>Meine Mods: absteigend sortieren. Gilt für alle Spiele.</summary>
    public bool MyModsSortDescending { get; set; }

    /// <summary>Meine Mods: Filter "Nur veröffentlichte". Gilt für alle Spiele.</summary>
    public bool MyModsOnlyPublished { get; set; }

    /// <summary>Meine Mods: Filter "Nur mit offenen Bugs". Gilt für alle Spiele.</summary>
    public bool MyModsOnlyOpenBugs { get; set; }

    /// <summary>Meine Mods: Filter "Nur mit Warnungen". Gilt für alle Spiele.</summary>
    public bool MyModsOnlyWarnings { get; set; }

    /// <summary>Meine Mods: Reihenfolge der Spiele (Kennungen) im Modus "Nach Spielen"; nicht genannte folgen danach. Gilt für alle Spiele.</summary>
    public List<string> MyModsGameOrder { get; set; } = new();

    /// <summary>Meine Mods: ausgeblendete Spiele (Kennungen). Gilt für alle Spiele.</summary>
    public List<string> MyModsHiddenGames { get; set; } = new();

    /// <summary>Meine Mods: Hinweisbereich eingeklappt. Gilt für alle Spiele.</summary>
    public bool MyModsAlertsCollapsed { get; set; }

    /// <summary>Position und Größe des Fensters "Meine Mods"; <see langword="null"/> vor dem ersten Schließen. Gilt für alle Spiele.</summary>
    public WindowBoundsState? MyModsWindowBounds { get; set; }

    /// <summary>Meine Mods: ausgeblendete Hinweisarten (Kennungen aus OverviewAlertFilter.Categories). Gilt für alle Spiele.</summary>
    public List<string> MyModsHiddenAlertKinds { get; set; } = new();

    /// <summary>Meine Mods: beim Öffnen als Vollbild (maximiert) statt in der gemerkten Fenstergröße. Standard: an. Gilt für alle Spiele.</summary>
    public bool MyModsStartMaximized { get; set; } = true;

    /// <summary>Knopf "Filter ▾" in der Werkzeugleiste jedes Arbeitsbereichs anzeigen. Standard: an. Gilt für alle Spiele.</summary>
    public bool WorkspaceFilterButtonVisible { get; set; } = true;

    /// <summary>Knopf "Symbole ▾" in der Werkzeugleiste jedes Arbeitsbereichs anzeigen. Standard: an. Gilt für alle Spiele.</summary>
    public bool WorkspaceIconButtonVisible { get; set; } = true;

    /// <summary>Kompatch-Merge: Schreibmodus "Vorsichtig" als Voreinstellung (für alle Spiele).</summary>
    public bool CompatchCautiousWrite { get; set; }

    /// <summary>Kompatch-Merge: gesperrte Dateien eigener Mods standardmäßig nicht einlesen (für alle Spiele).</summary>
    public bool CompatchSkipLocked { get; set; }

    /// <summary>Kompatch-Merge: Ergebnis-Spalte ganz rechts statt in der Mitte, mit Trennlinie (für alle Spiele). Standard: aus.</summary>
    public bool CompatchResultRight { get; set; }

    /// <summary>Kompatch, Standard für neue Arbeitsstände: Hauptmod als Basis.</summary>
    public bool CompatchMainAsBase { get; set; }

    /// <summary>Kompatch, Standard für neue Arbeitsstände: Fehlendes aus dem Hauptmod ergänzen.</summary>
    public bool CompatchFillMissing { get; set; }

    /// <summary>Kompatch, Standard für neue Arbeitsstände: Ergänzte fehlende Blöcke ausblenden.</summary>
    public bool CompatchHideFilled { get; set; }

    /// <summary>Kompatch, Standard für neue Arbeitsstände: Einfach ersetzte doppelte Dateien ausblenden.</summary>
    public bool CompatchHideReplaced { get; set; }

    /// <summary>Kompatch, Standard für neue Arbeitsstände: Erledigte ausblenden.</summary>
    public bool CompatchHideDecided { get; set; }

    /// <summary>Kompatch, Standard für neue Arbeitsstände: Nur unsichere Stellen zeigen.</summary>
    public bool CompatchUncertainOnly { get; set; }

    /// <summary>Kompatch-Startfenster: Dateiarten nebeneinander statt als Liste untereinander. Standard: aus (Liste).</summary>
    public bool CompatchKindsSideBySide { get; set; }

    /// <summary>Kompatch-Merge: Ansicht, mit der das Merge-Fenster öffnet ("A", "B" oder "C"), zugleich die zuletzt gewählte (für alle Spiele). Standard: A.</summary>
    public string CompatchDefaultView { get; set; } = "A";

    /// <summary>Kompatch-Merge, Ansicht C: Ordnerbereich links anzeigen (für alle Spiele). Standard: aus.</summary>
    public bool CompatchCardsFolderPane { get; set; }

    /// <summary>Kompatch-Merge: Kommentarzeilen (#) in allen Code-Anzeigen ausblenden (für alle Spiele). Standard: an.</summary>
    public bool CompatchHideComments { get; set; } = true;

    /// <summary>Kompatch-Merge: ausgeblendete Dateiarten dieses Spiels; <see langword="null"/> = Grafik, Modelle, Audio, Schriften.</summary>
    public List<string>? CompatchHiddenKinds { get; set; }

    /// <summary>Kennung des zuletzt geöffneten Spiels (Startspiel, wenn das Programm ohne Angabe gestartet wird). Gilt für alle Spiele.</summary>
    public string? LastGameProfileId { get; set; }

    /// <summary>Vom Nutzer gewählter Ordner mit den Doku-Logs des Spiels (docs); <see langword="null"/> = aus dem Spielprofil abgeleitet.</summary>
    public string? ScriptDocsFolder { get; set; }

    /// <summary>Position und Größe des Hauptfensters für dieses Spiel; <see langword="null"/> vor dem ersten Schließen.</summary>
    public WindowBoundsState? Window { get; set; }

    /// <summary>Kompatch-Merge: die Angaben des zuletzt gestarteten Laufs dieses Spiels (Vorbelegung des Startdialogs); <see langword="null"/> vor dem ersten Start.</summary>
    public CompatchStartMemory? CompatchLastStart { get; set; }
}

/// <summary>Angaben des Kompatch-Startdialogs, die PMT sich merkt: Ordner von Mod A und B, Zielart (<c>new</c>, <c>existing</c>, <c>base</c>), Ziel-Mod und Name.</summary>
public sealed class CompatchStartMemory
{
    public string? FolderA { get; set; }

    public string? FolderB { get; set; }

    public string TargetMode { get; set; } = "new";

    public string? TargetModId { get; set; }

    public string? NewName { get; set; }

    public bool BLoadsAfterA { get; set; } = true;

    /// <summary>Schreibmodus "Vorsichtig" des letzten Starts.</summary>
    public bool Cautious { get; set; }

    /// <summary>"Gesperrte Dateien nicht einlesen" des letzten Starts.</summary>
    public bool SkipLocked { get; set; }

    /// <summary>Ausgeblendete Dateiarten des letzten Starts; <see langword="null"/> = keine Angabe (Voreinstellung gilt).</summary>
    public List<string>? HiddenKinds { get; set; }

    /// <summary>Ziel "Basis-Mod", Option "Hauptmod ersetzt Vanilla" des letzten Starts.</summary>
    public bool MainReplacesVanilla { get; set; }

    /// <summary>Ziel "Basis-Mod", Option "Fehlende Blöcke der Hauptmod übernehmen" des letzten Starts.</summary>
    public bool FillMissingFromMain { get; set; }
}

/// <summary>Position und Größe eines Fensters (Normalzustand, auch wenn es maximiert war).</summary>
public sealed class WindowBoundsState
{
    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public bool IsMaximized { get; set; }
}
