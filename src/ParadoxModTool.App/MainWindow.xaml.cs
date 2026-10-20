using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
// Seit Abschnitt 5b (Suchen & Ersetzen) referenziert das Projekt zusätzlich Windows Forms
// (UseWindowsForms, nur für FolderBrowserDialog - siehe SearchReplaceWindow.xaml.cs). Dadurch
// sind mehrere in diesem Projekt genutzte Typnamen zwischen WPF/Microsoft.Win32 und
// System.Windows.Forms mehrdeutig (CS0104) - explizite using-Alias-Direktiven lösen das
// eindeutig auf, da Alias-Direktiven laut C#-Spezifikation Vorrang vor mehrdeutigen
// Namespace-Imports haben. Betrifft: KeyEventArgs, MessageBox, OpenFileDialog, SaveFileDialog.
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Microsoft.Win32;
using ParadoxModTool.Editor;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.Explorer;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.Notes;
using ParadoxModTool.SearchReplace;
using ParadoxModTool.Session;
using ParadoxModTool.Syntax;
using ParadoxModTool.Tools;
using ParadoxModTool.UndoRedo;
using ParadoxModTool.Updates;
using ICSharpCode.AvalonEdit.Highlighting;
using ParadoxModTool.Settings;
using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

public partial class MainWindow : Window
{
    private GameProfile? _activeProfile;
    private IHighlightingDefinition? _activeHighlighting;

    /// <summary>
    /// Vom Nutzer eingestellter Pfad zur Spiel-.exe (Konzept, Abschnitt 8). Wird über die Session
    /// wiederhergestellt (<see cref="AppSessionState.GameExecutablePath"/>) und beim Beenden wieder
    /// gespeichert; <see langword="null"/>, solange noch keiner eingestellt wurde - in diesem Fall
    /// fragt <see cref="LaunchGame_Click"/> selbst danach, statt den Start einfach fehlschlagen zu
    /// lassen.
    /// </summary>
    private string? _gameExecutablePath;

    /// <summary>
    /// Vom Nutzer im Vanilla-Diff-Fenster gewählter Ordner EINER konkreten Mod (siehe
    /// <see cref="AppSessionState.VanillaDiffModRootOverride"/>) - überschreibt den
    /// Sammelordner-Vorschlag aus dem aktiven Spielprofil, der für die automatische
    /// Vanilla-Dateierkennung falsch wäre. <see langword="null"/>, solange noch keine gewählt wurde.
    /// </summary>
    private string? _vanillaDiffModRootOverride;

    /// <summary>Zuletzt im Fenster "Suchen &amp; Ersetzen" durchsuchter Ordner (<see cref="AppSessionState.SearchLastFolder"/>).</summary>
    private string? _searchLastFolder;
    private ParadoxModTool.VersionManager.StorageSettings _storageSettings = new(null, false);

    /// <summary>
    /// Vom Nutzer eingestellter Pfad zu playsets.json (Nutzer-Feedback: "Es sollte noch zusätzlich
    /// bei der Startoption die Möglichkeit geben, das Playset direkt auszuwählen."). Diese Datei
    /// liegt NICHT im Mod-/Spielordner, sondern separat unter "Documents/Paradox Interactive/
    /// Europa Universalis V/", daher ein eigenes Feld statt Wiederverwendung von
    /// <see cref="_gameExecutablePath"/>. Wird über die Session gespeichert/wiederhergestellt; ist
    /// noch keiner eingestellt, versucht <see cref="TryGetDefaultPlaysetsFilePath"/> den Standardpfad
    /// automatisch zu finden (Konzept Abschnitt 1d: Grundeinstellungen möglichst automatisch
    /// vorschlagen).
    /// </summary>
    private string? _playsetsFilePath;

    /// <summary>
    /// Der zentrale Aktionsverlauf für Programmaktionen (Abschnitt 5), gemeinsam genutzt von
    /// BEIDEN Arbeitsbereichen im Split-View — deckt aktuell die "Speichern"-Aktion ab.
    /// Künftige Werkzeuge (Merge, Massenumbenennung, Formatierung, ...) führen ihre
    /// Änderungen ebenfalls über <see cref="_history"/> aus. Bekannte Einschränkung dieses
    /// Ausbauschritts: reine Texteingaben im Editor laufen weiterhin über AvalonEdits
    /// eigenen, davon unabhängigen Undo-Mechanismus (Strg+Z im Textbereich).
    /// </summary>
    private readonly ActionHistory _history = new();

    /// <summary>
    /// Das Suchen-&amp;-Ersetzen-Fenster (Abschnitt 5b) wird nur EINMAL angelegt und danach bei
    /// erneutem Öffnen (Strg+F, Werkzeugleiste) einfach wieder in den Vordergrund geholt,
    /// statt jedes Mal eine neue Instanz zu erzeugen (behält u. a. den zuletzt eingegebenen
    /// Suchbegriff über mehrere Öffnungen hinweg bei).
    /// </summary>
    private SearchReplaceWindow? _searchReplaceWindow;

    /// <summary>
    /// Das Vanilla-Diff-&amp;-Merge-Fenster (Abschnitt 7) wird — analog zu
    /// <see cref="_searchReplaceWindow"/> — nur EINMAL angelegt und danach bei erneutem Öffnen
    /// einfach wieder in den Vordergrund geholt, statt jedes Mal eine neue Instanz zu erzeugen.
    /// </summary>
    private VanillaDiffWindow? _vanillaDiffWindow;

    /// <summary>
    /// Die Trefferliste von "Suchen &amp; Ersetzen" (Abschnitt 5b) — geteilt zwischen dem modalen
    /// <see cref="SearchReplaceWindow"/> und der Ergebnisleiste hier im Hauptfenster (Nutzer-
    /// Feedback: "Suchergebnisse [...] in einem Fenster über meinem Arbeitsbereich anzeigen [...].
    /// Die Ergebnisse im Suchfenster können aber auf jeden Fall bleiben!"). EIN gemeinsames
    /// Objekt statt zweier Kopien, damit ein neuer Treffer automatisch in beiden Anzeigen erscheint.
    /// </summary>
    private readonly ObservableCollection<SearchResultRow> _searchResults = new();

    /// <summary>Globaler Zustand für "Nur gefundenen Code anzeigen" (Nutzer-Feedback), geteilt zwischen der Ergebnisleiste hier und dem modalen Suchen-Fenster.</summary>
    private bool _diffFoldingEnabled = true;

    /// <summary>Compare und Update Tool: zuletzt offener Reiter ("compare"/"update").</summary>
    private string _cuToolLastTab = "update";
    private bool _hideSearchResultDescriptions;
    private bool _suppressHideDescriptionSync;

    /// <summary>
    /// Aktuell wirksame Kategorie-Farbüberschreibungen (Nutzer-Feedback Runde 4 + Konzept: "Die
    /// Farben sollten frei konfigurierbar sein") - beim Start aus der Session geladen, über den
    /// "Kategorie-Farben…"-Dialog verändert und beim Beenden wieder gespeichert. Als
    /// Dictionary&lt;FileCategory,string&gt; gehalten (statt der auf Enum-Namen als String
    /// beruhenden AppSessionState.CategoryColorOverrides), damit
    /// FileCategoryClassifier.SetColorOverrides sie direkt verwenden kann, ohne bei jedem Zugriff
    /// neu zu übersetzen.
    /// </summary>

    public MainWindow()
    {
        InitializeComponent();
        InitializeSettings();
        InitializeFolderFilters();
        InitializeMenuTexts();
        InitializeTileBar();
        ApplySavedWindowBounds();
        ThemeManager.Attach(this);
        InitializeThemeSupport();
        Loaded += MainWindow_Loaded;
        ScriptHintSettings.Changed += (_, _) => SyncScriptHintControls();
        Closing += MainWindow_Closing;
        InitializeSelfUpdate();
        PreviewKeyDown += MainWindow_PreviewKeyDown;

        // Spiel-Anbindung (Plan 4): Pipe-Dienst nach dem Laden (die Sitzung ist dann wiederhergestellt), Ende mit dem Fenster.
        Loaded += (_, _) => StartEditorHandOff();
        Closed += (_, _) => StopEditorHandOff();

        _history.Changed += UpdateUndoRedoButtons;
        UpdateUndoRedoButtons();

        WireEditorPane(LeftEditor, RightEditor);
        WireEditorPane(RightEditor, LeftEditor);
        LeftEditor.PaneName = "A";
        RightEditor.PaneName = "B";

        // Verzeichnis-Übersicht (Nutzer-Feedback Runde 10) - siehe ExplorerPanel_OpenFileRequested/
        // ExplorerPanel_NewFileRequested weiter unten. NewFolderRequested/DeleteRequested kamen
        // mit der zweiten Rückmeldung zu Runde 10 hinzu ("neuen Ordner erstellen" /
        // Löschfunktion mit Tree-Refresh).
        ExplorerPanelControl.OpenFileRequested += ExplorerPanel_OpenFileRequested;
        ExplorerPanelControl.RootsChanged += (_, _) => RefreshFileOrigins();
        ExplorerPanelControl.NewFileRequested += ExplorerPanel_NewFileRequested;
        ExplorerPanelControl.NewFolderRequested += ExplorerPanel_NewFolderRequested;
        ExplorerPanelControl.DeleteRequested += ExplorerPanel_DeleteRequested;

        ResultsPanelList.ItemsSource = _searchResults;
        ResultsListCopy.Attach(ResultsPanelList, ResultsPanelDetail);

        // Ergebnisleiste automatisch einblenden, sobald ein neuer Treffer hinzukommt (z. B. durch
        // "Alle finden" im modalen Suchen-Fenster) - so muss der Nutzer sie nicht erst manuell
        // öffnen, kann sie aber jederzeit über den "✕"-Button schließen, ohne dass dabei die
        // Trefferdaten selbst verloren gehen.
        _searchResults.CollectionChanged += (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add)
            {
                ResultsPanel.Visibility = Visibility.Visible;
            }
        };
    }

    /// <summary>
    /// Der Arbeitsbereich, der gerade den Tastaturfokus enthält, sonst der zuletzt fokussierte (z. B. wenn der Fokus im Suchfenster liegt) — Grundlage
    /// dafür, in welchem Arbeitsbereich "Suchen"/"Ersetzen im aktuellen Dokument" wirkt, wenn
    /// Split-View aktiv ist. Ohne Fokus in einem der beiden (z. B. direkt nach dem Programmstart)
    /// wird der linke Arbeitsbereich als Standard verwendet.
    /// </summary>
    private EditorPane ActivePane => RightEditor.IsKeyboardFocusWithin ? RightEditor
        : LeftEditor.IsKeyboardFocusWithin ? LeftEditor
        : _lastFocusedPane == RightEditor && RightEditor.IsVisible ? RightEditor : LeftEditor;

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        StartupProgress.Report(0.15, MainTexts.StartupLoadingProfile);

        // Bewusst zwei getrennte try/catch-Blöcke (statt einem gemeinsamen): Spielprofil
        // (JSON) und Syntax-Highlighting (XSHD/XML) sind zwei unabhängige Ladevorgänge mit
        // unterschiedlichen Fehlerursachen — eine gemeinsame Fehlermeldung würde z. B. einen
        // XSHD-Fehler fälschlich als "Spielprofil konnte nicht geladen werden" anzeigen.
        try
        {
            if (GameContext.Profile is { } gameProfile)
            {
                _activeProfile = gameProfile;
                Title = $"Paradox Modding Tool — {_activeProfile.DisplayName}";

                // Verzeichnis-Übersicht (Nutzer-Feedback Runde 10): Standardfarben je
                // Ordnerkategorie kommen aus dem aktiven Spielprofil (FolderCategoryColorResolver).
                ExplorerPanelControl.SetGameProfile(_activeProfile, GameContext.Profiles.Select(e => e.Profile).ToList());
                InstallPrefixWarnings();

                // Startoptionen (Normal/Debug-Modus/Ignore-Crash) kommen direkt aus dem
                // Spielprofil (Konzept Abschnitt 8) - siehe GameProfile.LaunchOptions.
                LaunchOptionsCombo.ItemsSource = _activeProfile.LaunchOptions;
                if (_activeProfile.LaunchOptions.Count > 0)
                {
                    LaunchOptionsCombo.SelectedIndex = 0;
                }
            }
            else
            {
                MessageBox.Show(
                    MainTexts.ProfileNotFound(GameContext.GameId),
                    CommonTexts.Notice);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(MainTexts.ProfileLoadFailed(ex.Message), CommonTexts.Error);
        }

        StartupProgress.Report(0.3, MainTexts.StartupLoadingSyntax);
        try
        {
            var xshdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syntax-templates", "jomini-script.xshd");
            if (File.Exists(xshdPath))
            {
                _xshdPath = xshdPath;
                _activeHighlighting = XshdHighlightingLoader.LoadFromFile(xshdPath, CurrentSyntaxColors());
            }
            else
            {
                MessageBox.Show(
                    MainTexts.SyntaxFileMissing(xshdPath),
                    CommonTexts.Notice);
            }
        }
        catch (Exception ex)
        {
            // Ausführliche Diagnose (Typ + innere Exception + Stacktrace) statt nur ex.Message:
            // Manche WPF-Wert-Parser (z. B. bei ungültigen Farb-/Schriftgewicht-Angaben) liefern
            // nur die knappe Meldung "Token ist ungültig" ohne jeden Hinweis, welcher Wert genau
            // betroffen ist - der Stacktrace zeigt, welcher Konverter tatsächlich fehlschlägt.
            var details = $"{ex.GetType().FullName}: {ex.Message}";
            if (ex.InnerException is { } inner)
            {
                details += "\n\n" + MainTexts.SyntaxInnerException(inner.GetType().FullName ?? string.Empty, inner.Message);
            }
            details += $"\n\nStacktrace:\n{ex.StackTrace}";
            MessageBox.Show(details, MainTexts.SyntaxLoadFailedTitle);
        }

        // Bewusst erst NACH den beiden Ladevorgängen oben aufgerufen, damit wiederhergestellte
        // Dokumente sofort mit dem richtigen Syntax-Highlighting angezeigt werden.
        StartupProgress.Report(0.4, MainTexts.StartupRestoringSession);
        RestoreSession();

        StartupProgress.Report(0.9, MainTexts.StartupCheckingRecovery);

        // Ungespeicherte Änderungen aus der letzten Sitzung bzw. einem Absturz wieder einspielen
        // (siehe MainWindow.Recovery.cs) und danach die regelmäßige Sicherung starten.
        ApplyRecoveries();
        StartRecoveryTimer();
        Activated += (_, _) => CheckExternalChanges();

        // Rechtes Panel "Notizen & Lesezeichen" (siehe MainWindow.NotesPanel.cs).
        InitializeNotesPanel();
        InitializeMods();
        InitializeBranches();

        // Verzeichnis-Übersicht (Nutzer-Feedback Runde 10): Beim allerersten Start (keine aus der
        // Session wiederhergestellten Wurzeln) den Mod-Ordner aus dem aktiven Spielprofil
        // automatisch vorschlagen (Konzept Abschnitt 1d: "Grundeinstellungen möglichst automatisch
        // vorschlagen") - schlägt das fehl (kein Profil, kein Template, Ordner existiert noch
        // nicht), bleibt die Übersicht einfach leer, bis der Nutzer manuell eine Wurzel hinzufügt.
        if (!ExplorerPanelControl.HasAnyRoots
            && ResolveDefaultModRoot(_activeProfile?.DefaultModRootTemplate) is { } defaultModRoot)
        {
            var trimmed = defaultModRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var displayName = Path.GetFileName(trimmed);
            ExplorerPanelControl.AddRoot(new ExplorerRootDescriptor(
                defaultModRoot,
                string.IsNullOrEmpty(displayName) ? defaultModRoot : displayName,
                ExplorerRootKind.ModFolder));
        }

        // Best-effort-Vorauswahl für den "Ordner hinzufügen"-Dialog der Verzeichnis-Übersicht -
        // siehe ComputeGameFilesGuess weiter unten (keine bestätigte Konvention, daher bewusst nur
        // eine Vorauswahl, keine automatisch hinzugefügte Wurzel).
        ExplorerPanelControl.SetSuggestedInitialFolder(ComputeGameFilesGuess(_gameExecutablePath));

        // Kein in der Session gespeicherter/vom Nutzer eingestellter Pfad zu playsets.json?
        // Versuche den Standardpfad automatisch zu finden (Konzept Abschnitt 1d: Grundeinstellungen
        // möglichst automatisch vorschlagen) - schlägt das fehl, bleibt die Playset-Auswahl einfach
        // leer, bis der Nutzer sie manuell über "Einstellungen" → "Playset-Datei einstellen" setzt.
        _playsetsFilePath ??= TryGetDefaultPlaysetsFilePath();
        RefreshPlaysetsCombo();

        InitializeScriptHints();
        InitializeGuiWorkspace();
        InitializeErrorLog();
        InitializeDesync();
        InitializeSaveExplorer();

        StartupProgress.Report(0.97, MainTexts.StartupAlmostDone);
        StartBackgroundUpdateCheck();
    }

    /// <summary>
    /// Standardpfad zu playsets.json unter "Documents/Paradox Interactive/&lt;Spiel&gt;/", sonst zu launcher-v2.sqlite im selben Ordner
    /// (Paradox-Launcher v2, z. B. EU IV und CK3) - <see langword="null"/>, wenn dort keine der beiden Dateien gefunden wurde.
    /// </summary>
    private string? TryGetDefaultPlaysetsFilePath()
    {
        try
        {
            // playsets.json liegt neben dem Mod-Ordner des Spiels (Documents/Paradox Interactive/<Spiel>/), also im Elternordner
            // der Mod-Ordner-Vorlage des Spielprofils; ohne Profil gilt der Ordner von Europa Universalis V.
            string? gameFolder = null;
            if (_activeProfile?.DefaultModRootTemplate is { Length: > 0 } template)
            {
                var modFolder = Path.GetFullPath(Environment.ExpandEnvironmentVariables(template));
                gameFolder = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(modFolder));
            }

            gameFolder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Paradox Interactive", "Europa Universalis V");
            return new[] { "playsets.json", "launcher-v2.sqlite" }
                .Select(name => Path.Combine(gameFolder, name))
                .FirstOrDefault(File.Exists);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Liest die Playsets aus <see cref="_playsetsFilePath"/> neu ein und befüllt
    /// <see cref="PlaysetsCombo"/> - wählt dabei standardmäßig das aktuell aktive Playset vor
    /// (fällt auf das erste Playset zurück, falls keines als aktiv markiert ist). Aufgerufen nach
    /// dem Laden/Ändern des Playset-Dateipfads sowie nach jedem erfolgreichen Playset-Wechsel beim
    /// Starten, damit die Anzeige ("... , aktiv") aktuell bleibt.
    /// </summary>
    private void RefreshPlaysetsCombo()
    {
        var playsets = PlaysetStore.ListPlaysets(_playsetsFilePath);
        PlaysetsCombo.ItemsSource = playsets;

        if (playsets.Count == 0)
        {
            return;
        }

        var activeIndex = 0;
        for (var i = 0; i < playsets.Count; i++)
        {
            if (playsets[i].IsActive)
            {
                activeIndex = i;
                break;
            }
        }

        PlaysetsCombo.SelectedIndex = activeIndex;
    }

    /// <summary>
    /// Öffnet einen Dateiauswahldialog für playsets.json (Nutzer-Feedback: Playset-Auswahl beim
    /// Starten) und aktualisiert bei Bestätigung <see cref="_playsetsFilePath"/> sowie die
    /// Playset-Auswahl in der Werkzeugleiste.
    /// </summary>
    private void ChoosePlaysetsFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"playsets.json, launcher-v2.sqlite|playsets.json;launcher-v2.sqlite|{MainTexts.FilterAllFiles}|*.*",
            Title = MainTexts.PlaysetsChooseTitle,
        };

        var initialDirectory = _playsetsFilePath is { } existing && File.Exists(existing)
            ? Path.GetDirectoryName(existing)
            : Path.GetDirectoryName(TryGetDefaultPlaysetsFilePath());

        if (initialDirectory is not null)
        {
            dialog.InitialDirectory = ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(initialDirectory);
        }

        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("main.playsets");
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _playsetsFilePath = dialog.FileName;
        RefreshPlaysetsCombo();
        _errorLog?.RefreshSources();
    }

    /// <summary>
    /// Stellt die beim letzten Beenden gespeicherte Session wieder her (Abschnitt 12: "offene
    /// Dateien, Split-View-Aufteilung, aktives Spielprofil, Scroll-Position" — analog zu
    /// Notepad++). Fehlt die Session-Datei (z. B. beim allerersten Start) oder ist sie
    /// beschädigt, bleibt das Fenster einfach im Ausgangszustand (leer, Split-View aus) —
    /// <see cref="SessionStore.TryLoad"/> liefert in diesem Fall bewusst null statt eines Fehlers.
    /// </summary>
    private void RestoreSession()
    {
        var state = GameContext.Session.State;

        _restoreTotal = state.LeftPane.Documents.Count + state.RightPane.Documents.Count;

        _settings.Load(state, KnownThemeIds());

        LoadUpdateSettings(state);
        LoadGuiSettings(state);
        LoadCompatchSettings(state);
        LoadErrorLogSettings(state);

        // Kategorie-Farben gehören seit Paket 12b zum Theme (siehe ThemeManager, MainWindow.Theme.cs).

        NotesPanelControl.ShowDoneBugs = state.ShowDoneBugs;
        NotesPanelControl.BugsWholeProject = state.BugsPanelWholeProject;
        RefreshNotesPanel();
        ParadoxModTool.Editor.ScopeSettings.SetStatusBarEnabled(state.ScopeStatusBarEnabled);
        ParadoxModTool.Editor.ScopeSettings.SetHoverEnabled(state.ScopeHoverEnabled);
        ParadoxModTool.Editor.ModifierValueSettings.SetHoverEnabled(state.ModifierHoverEnabled);
        ParadoxModTool.Editor.ModifierValueSettings.SetNamesEnabled(state.ModifierNamesEnabled);
        ParadoxModTool.Editor.ScopeSettings.SetStatusBarTopEnabled(state.ScopeStatusBarTopEnabled);
        ParadoxModTool.Editor.ScopeSettings.SetWarningsEnabled(state.ScopeWarningsEnabled);
        _diffFoldingEnabled = state.DiffFoldingEnabled;
        _cuToolLastTab = state.CuToolLastTab;
        ParadoxModTool.Editor.ScopeSettings.SetPanelEnabled(state.ScopePanelEnabled);
        ParadoxModTool.Editor.BracketSettings.SetHighlightEnabled(state.BracketHighlightEnabled);
        ScriptHintSettings.SetHoverEnabled(state.ScriptHoverEnabled);
        ScriptHintSettings.SetAutoCompleteEnabled(state.ScriptAutoCompleteEnabled);
        ScriptHintSettings.SetCompleteCustomLocalizations(state.ScriptCompleteCustomLocalizations);
        SyncScriptHintControls();



        // Konzept Abschnitt 8: eingestellter Pfad zur Spiel-.exe soll die Session überdauern.
        _gameExecutablePath = state.GameExecutablePath;

        // Nutzer-Feedback: eingestellter Pfad zu playsets.json soll ebenfalls die Session überdauern.
        _playsetsFilePath = state.PlaysetsFilePath;

        // Zuletzt gewählte Startoption (z. B. Debug-Modus) wieder wählen; die Liste kommt schon in MainWindow_Loaded aus dem Spielprofil.
        if (_activeProfile is { } launchProfile)
        {
            LaunchOptionsCombo.SelectedIndex = LaunchOptionMemory.IndexOf(
                launchProfile.LaunchOptions.Select(o => o.Args).ToList(), state.LaunchOptionArgs);
        }

        // Bugfix Vanilla-Diff-Fenster: gewählter Mod-Ordner (einer konkreten Mod, nicht der
        // Sammelordner) soll ebenfalls die Session überdauern.
        _vanillaDiffModRootOverride = state.VanillaDiffModRootOverride;
        _activeModChoice = state.ActiveModChoice;
        _searchLastFolder = state.SearchLastFolder;
        _scriptDocsFolderOverride = state.ScriptDocsFolder;

        // Speicherorte für Snapshots/Vanilla-Historie (Nutzer-Feedback Task 14).
        _storageSettings = new ParadoxModTool.VersionManager.StorageSettings(
            state.StorageParentFolder,
            state.StoreInsideModFolder,
            _activeProfile?.EffectiveVanillaHistoryFolderName);

        // Verzeichnis-Übersicht (Nutzer-Feedback Runde 10): gespeicherte Wurzeln wiederherstellen -
        // String-Schlüssel aus der Session zurück in ExplorerRootKind übersetzen; ein unbekannter/
        // fehlerhafter Wert (z. B. aus einer von Hand bearbeiteten Session-Datei) fällt auf Custom
        // zurück, statt die Wiederherstellung der übrigen Wurzeln abzubrechen. Eine seither
        // gelöschte/umbenannte Wurzel überspringt ExplorerPanel.SetRoots selbst still.
        var explorerRoots = state.ExplorerRoots.Select(r => new ExplorerRootDescriptor(
            r.Path,
            r.DisplayName,
            Enum.TryParse<ExplorerRootKind>(r.Kind, out var explorerRootKind) ? explorerRootKind : ExplorerRootKind.Custom));
        ExplorerPanelControl.SetExpandedPaths(state.ExplorerExpandedPaths);
        ExplorerPanelControl.SetRoots(explorerRoots);

        TabIconSettings.LoadHidden(state.HiddenTabIcons);
        FilterBarSettings.LoadHidden(state.HiddenFilterEntries);
        LoadFolderFilters(state);
        RefreshFileOrigins();

        RestorePane(LeftEditor, state.LeftPane);
        RestorePane(RightEditor, state.RightPane);

        // Alte Vanilla-Fassungen für den Update-Abgleich (Compare und Update Tool) im Hintergrund ablegen.
        RecordVanillaSnapshotsInBackground();
    }

    /// <summary>
    /// Lädt die in <paramref name="state"/> gemerkten Dateien in <paramref name="pane"/> und
    /// stellt danach den zuletzt aktiven Tab (samt Cursorposition, siehe <see cref="EditorPane"/>)
    /// wieder her. Eine einzelne seit dem Speichern der Session gelöschte oder nicht mehr lesbare
    /// Datei bricht die Wiederherstellung der übrigen Dokumente nicht ab, sondern wird übersprungen.
    /// </summary>
    private int _restoreTotal;
    private int _restoreDone;

    private void RestorePane(EditorPane pane, PaneSessionState state)
    {
        // Sortierung vor dem Laden setzen, damit die Dokumente gleich an der sortierten Stelle landen.
        pane.TabSort = state.TabSort;

        // Den aktiven Tab über sein Dokument merken, nicht über den Index: übersprungene Dateien und die Sortierung (manuelle
        // Kategorie erst nach dem Einfügen, inzwischen geänderte Kategorie-Regeln oder Wurzelordner) verschieben die Indizes.
        OpenDocument? active = null;
        var index = -1;
        foreach (var documentState in state.Documents)
        {
            index++;
            _restoreDone++;
            StartupProgress.Report(
                0.4 + 0.5 * _restoreDone / Math.Max(1, _restoreTotal),
                MainTexts.StartupDocument(_restoreDone, _restoreTotal, Path.GetFileName(documentState.FilePath)));

            if (!File.Exists(documentState.FilePath))
            {
                continue;
            }

            try
            {
                // Ältere Sitzungen enthalten gemischte Pfade (…\Name/Documents/…/mod\Datei); der saubere Pfad steht im Tab-Tooltip und erreicht externe Programme.
                var document = pane.LoadFile(ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(documentState.FilePath), _activeHighlighting);
                document.CaretOffset = documentState.CaretOffset;

                // Manuelle Kategorie wiederherstellen. Eine Kennung, die es nicht mehr gibt (Kategorie gelöscht
                // oder Session von Hand bearbeitet), wird übersprungen; es gilt dann die automatische Kategorie.
                if (documentState.ManualCategory is { } manualCategoryId && FileCategoryClassifier.Exists(manualCategoryId))
                {
                    document.SetManualCategory(manualCategoryId);
                }

                UpdateHasNote(document);
                if (index == state.ActiveDocumentIndex)
                {
                    active = document;
                }
            }
            catch (IOException)
            {
                // Datei ist seit dem Speichern der Session unlesbar geworden - überspringen.
            }
            catch (UnauthorizedAccessException)
            {
                // Datei ist seit dem Speichern der Session unlesbar geworden - überspringen.
            }
        }

        if (active is not null)
        {
            pane.ActivateDocument(active);
        }
    }

    /// <summary>
    /// Speichert beim Beenden des Programms die aktuell offenen Dateien, die Split-View-
    /// Aufteilung, das aktive Spielprofil und die Cursorposition jedes offenen Dokuments
    /// (Abschnitt 12), damit <see cref="RestoreSession"/> sie beim nächsten Start automatisch
    /// wiederherstellen kann. Ein Fehler beim Speichern (z. B. kein Schreibzugriff auf
    /// %APPDATA%) darf das Beenden des Programms nicht verhindern.
    /// </summary>
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Ungespeicherte Änderungen still sichern - werden beim nächsten Start wieder eingespielt.
        SyncRecovery();

        // Der beim Start geladene Zustand wird angepasst (statt neu aufgebaut), damit alles, was dieses Fenster nicht ändert,
        // unverändert bleibt - wichtig für die Zusammenführung der globalen Einstellungen mit anderen offenen Fenstern.
        var state = GameContext.Session.State;
        // Das Einstellungsfenster schließt als Kindfenster erst nach diesem Ereignis; Größe und Position daher jetzt übernehmen.
        _settingsWindow?.SaveBounds();
        SaveErrorLogWindowBounds();
        SaveSaveExplorerWindowBounds();
        _myModsWindow?.SaveBounds();
        _settings.WriteTo(state);
        state.ActiveGameProfileId = _activeProfile?.Id ?? GameContext.GameId;
        state.LeftPane = CapturePaneState(LeftEditor);
        state.RightPane = CapturePaneState(RightEditor);
        state.ShowDoneBugs = NotesPanelControl.ShowDoneBugs;
        state.BugsPanelWholeProject = NotesPanelControl.BugsWholeProject;
        state.HiddenTabIcons = TabIconSettings.HiddenNames();
        state.HiddenFilterEntries = FilterBarSettings.HiddenKeys();
        SaveFolderFilters(state);
        state.Categories = FileCategoryClassifier.Categories.Select(c => c.Clone()).ToList();
        state.ScriptHoverEnabled = ScriptHintSettings.HoverEnabled;
        state.ScriptAutoCompleteEnabled = ScriptHintSettings.AutoCompleteEnabled;
        state.ScriptCompleteCustomLocalizations = ScriptHintSettings.CompleteCustomLocalizations;
        state.ScopeStatusBarEnabled = ParadoxModTool.Editor.ScopeSettings.StatusBarEnabled;
        state.ScopeStatusBarTopEnabled = ParadoxModTool.Editor.ScopeSettings.StatusBarTopEnabled;
        state.ScopeWarningsEnabled = ParadoxModTool.Editor.ScopeSettings.WarningsEnabled;
        state.DiffFoldingEnabled = _diffFoldingEnabled;
        state.CuToolLastTab = _cuToolLastTab;
        state.ScopePanelEnabled = ParadoxModTool.Editor.ScopeSettings.PanelEnabled;
        state.BracketHighlightEnabled = ParadoxModTool.Editor.BracketSettings.HighlightEnabled;
        state.ScopeHoverEnabled = ParadoxModTool.Editor.ScopeSettings.HoverEnabled;
        state.ModifierHoverEnabled = ParadoxModTool.Editor.ModifierValueSettings.HoverEnabled;
        state.ModifierNamesEnabled = ParadoxModTool.Editor.ModifierValueSettings.NamesEnabled;
        SaveGuiSettings(state);
        SaveCompatchSettings(state);
        CloseDesync();
        SaveErrorLogSettings(state);
        CloseSaveExplorer();
        state.GameExecutablePath = _gameExecutablePath;
        state.PlaysetsFilePath = _playsetsFilePath;
        state.LaunchOptionArgs = (LaunchOptionsCombo.SelectedItem as LaunchOption)?.Args;
        state.VanillaDiffModRootOverride = _vanillaDiffModRootOverride;
        state.ActiveModChoice = _activeModChoice;
        state.SearchLastFolder = _searchLastFolder;
        state.ScriptDocsFolder = _scriptDocsFolderOverride;
        state.StorageParentFolder = _storageSettings.ParentFolder;
        state.StoreInsideModFolder = _storageSettings.InsideModFolder;
        state.ExplorerRoots = ExplorerPanelControl.CaptureRoots()
            .Select(d => new ExplorerRootState { Path = d.FullPath, DisplayName = d.DisplayName, Kind = d.Kind.ToString() })
            .ToList();
        state.ExplorerExpandedPaths = ExplorerPanelControl.CaptureExpandedPaths().ToList();
        state.LastUpdateCheckUtc = _lastUpdateCheckUtc;
        state.SkippedUpdateVersion = _skippedUpdateVersion;
        state.Window = CaptureWindowBounds();
        ThemeManager.WriteTo(state);

        try
        {
            GameContext.Repository.Save(GameContext.GameId, GameContext.Session);
        }
        catch (IOException)
        {
            // Session konnte nicht gespeichert werden - Programm trotzdem normal beenden.
        }
        catch (UnauthorizedAccessException)
        {
            // Session konnte nicht gespeichert werden - Programm trotzdem normal beenden.
        }
    }

    /// <summary>
    /// Erfasst den aktuellen Zustand eines Arbeitsbereichs für die Session (Abschnitt 12). Nur
    /// Dokumente mit einem tatsächlichen Dateipfad werden gespeichert — ein noch nicht
    /// gespeichertes neues ("Unbenannt")-Dokument kann beim nächsten Start nicht wiederhergestellt
    /// werden, da es keine Datei zum erneuten Laden gibt.
    /// </summary>
    private static PaneSessionState CapturePaneState(EditorPane pane)
    {
        // Die Cursorposition des GERADE aktiven Dokuments wird erst beim Wegschalten auf einen
        // anderen Tab in dessen CaretOffset übernommen (siehe EditorPane.Tabs_SelectionChanged) -
        // beim Beenden des Programms passiert das nie, daher hier direkt aus dem Editor-
        // Steuerelement selbst übernehmen.
        if (pane.ActiveDocument is { } active)
        {
            active.CaretOffset = pane.CurrentCaretOffset;
        }

        var state = new PaneSessionState { TabSort = pane.TabSort };

        foreach (var document in pane.Documents)
        {
            if (document.FilePath is null)
            {
                continue;
            }

            if (ReferenceEquals(document, pane.ActiveDocument))
            {
                state.ActiveDocumentIndex = state.Documents.Count;
            }

            state.Documents.Add(new DocumentSessionState
            {
                FilePath = document.FilePath,
                CaretOffset = document.CaretOffset,
                // Nutzer-Feedback Runde 5 (Bugfix): "Das Programm die Dateienmarkieren als
                // Wichtig oder Hilfsdatei sich nicht merkt, wenn es neu gestartet wird." -
                // ManualCategory ist null, wenn keine manuelle Zuweisung besteht (dann gilt
                // weiterhin die automatische Kategorie aus der Dateiendung).
                ManualCategory = document.ManualCategory,
            });
        }

        return state;
    }

    /// <summary>
    /// Verbindet die "Requested"-Events eines Arbeitsbereichs mit den tatsächlichen
    /// Aktionen im Hauptfenster (Dateidialoge, gemeinsamer Undo-Verlauf) und dem jeweils
    /// ANDEREN Arbeitsbereich als Ziel für "Dokument verschieben".
    /// </summary>
    private void WireEditorPane(EditorPane pane, EditorPane otherPane)
    {
        pane.NewDocumentRequested += (_, _) => pane.NewDocument(_activeHighlighting);
        pane.OpenDocumentRequested += (_, _) => OpenFileInto(pane);
        pane.SaveDocumentRequested += (_, _) => SaveActiveDocumentOf(pane);
        pane.SaveAllRequested += (_, _) => SaveAllDocuments();
        pane.OpenLocationRequested += (_, e) => JumpToBookmarkInFile(e.Path, e.Line);
        WireGuiInspector(pane);
        pane.CloseDocumentRequested += (_, document) => CloseWithPrompt(pane, document);
        pane.MoveActiveDocumentRequested += (_, document) => MoveDocumentBetweenPanes(pane, otherPane, document, null);

        // Tab per Drag&Drop aus dem anderen Arbeitsbereich hierher gezogen (Nutzer-Feedback).
        pane.DocumentDropped += (_, args) => MoveDocumentBetweenPanes(otherPane, pane, args.Document, args.InsertBefore);
        // Nutzer-Feedback Runde 5: Notiz-Icon im Dateienreiter und "Notizen anzeigen"-Button
        // neben der Pfadanzeige lösen beide dasselbe Notizfenster aus wie der bisherige
        // Werkzeugleisten-/Menü-Button (siehe FileNote_Click) - gemeinsame Stelle ShowNoteFor.
        pane.ShowNoteRequested += (_, document) => ShowNoteFor(document);
        pane.ShowBugRequested += (_, document) => CycleBugs(document);
        pane.ShowErrorLogRequested += (_, document) => OpenErrorLogWindow(document.FilePath);
        pane.ManageCategoriesRequested += (_, _) => OpenCategoriesDialog();
        // Interface-Rework 2: Notizen/Lesezeichen/Bug in der Leiste des Bereichs wirken auf diesen Bereich, nicht auf ActivePane.
        pane.NoteRequested += (_, _) => FileNoteFor(pane);
        pane.BookmarkRequested += (_, _) => BookmarkWithCommentFor(pane);
        pane.BugRequested += (_, _) => CreateBug(pane, attach: false);
    }

    /// <summary>
    /// Dateifilter für "Öffnen…" (Nutzer-Feedback: "Die unterstützten Dateitypen sollten pauschal
    /// angezeigt werden ... Zeige pauschal alle Dateien an, die in dieser Ordnerstruktur liegen.").
    /// Vorher war "Paradox-Skriptdateien (*.txt)" voreingestellt, wodurch .gui-/.gfx-/.yml-/.lua-
    /// Dateien im Dialog standardmäßig unsichtbar blieben, obwohl sie zum Mod gehören können. Jetzt
    /// ist "Alle Dateien" der ERSTE (und damit voreingestellte) Eintrag, damit im Ordner-Explorer
    /// des Dialogs von Anfang an nichts ausgeblendet wird; "Alle unterstützten Dateitypen" bündelt
    /// zusätzlich alle von <see cref="FileCategoryClassifier"/> erkannten Endungen als bequeme
    /// Einschränkung, plus je ein Einzelfilter pro Kategorie.
    /// </summary>
    private static string BuildOpenFileDialogFilter()
    {
        var categories = FileCategoryClassifier.Categories.Where(c => c.Extensions.Count > 0).ToList();
        var all = categories.SelectMany(c => c.Extensions).Distinct().Select(x => "*" + x).ToList();

        var filter = new System.Text.StringBuilder($"{MainTexts.FilterAllFiles}|*.*");
        if (all.Count > 0)
        {
            filter.Append($"|{MainTexts.FilterAllSupported(string.Join(";", all))}|{string.Join(";", all)}");
        }

        foreach (var category in categories)
        {
            var patterns = string.Join(";", category.Extensions.Select(x => "*" + x));
            filter.Append($"|{ParadoxModTool.Categories.CategoryDefaults.DisplayName(category)} ({patterns})|{patterns}");
        }

        return filter.ToString();
    }

    private void OpenFileInto(EditorPane pane)
    {
        var dialog = new OpenFileDialog
        {
            Filter = BuildOpenFileDialogFilter()
        };

        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("main.openFile");
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var document = pane.LoadFile(dialog.FileName, _activeHighlighting);
        UpdateHasNote(document);
    }

    private void SaveActiveDocumentOf(EditorPane pane)
    {
        // Vanilla-.gui wird nie gespeichert (Plan 3b Ruling 3): ein Entwurf geht über "In Mod übernehmen", sonst ein Hinweis.
        if (pane.ActiveDocument is { } active && GuiSettings.IsVanillaGui(active))
        {
            if (active.IsGuiDraft)
            {
                TakeOverGuiDraft(active);
            }
            else
            {
                // "Für Mod bearbeiten" gibt es in beiden Formaten (klassisch seit Plan 2).
                MessageBox.Show(
                    this,
                    GuiTexts.DraftSaveVanilla,
                    GuiTexts.DraftTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return;
        }

        var targetPath = pane.CurrentFilePath;

        if (targetPath is null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = $"{MainTexts.FilterScriptFiles}|*.txt|{MainTexts.FilterAllFiles}|*.*"
            };

            dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("main.saveAs");
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            targetPath = dialog.FileName;
        }

        try
        {
            var action = new FileWriteUndoableAction(
                MainTexts.SaveUndoName(Path.GetFileName(targetPath)),
                targetPath,
                pane.CurrentText,
                ModFileEncodingFor(targetPath));

            _history.Execute(action);
            pane.SetCurrentFilePath(targetPath);
            SyncRecovery();
            NotifyGuiFileSaved(targetPath, pane.CurrentText);
        }
        catch (Exception ex)
        {
            MessageBox.Show(MainTexts.SaveFailed(ex.Message), CommonTexts.Error);
            return;
        }

        // Warnungen des letzten Ersetzens (OR/NOR/AND mit einem Eintrag) gegen den gespeicherten Stand aktualisieren.
        RefreshReplaceWarningsAfterSave(targetPath);
    }

    private void SplitViewToggle_Click(object sender, RoutedEventArgs e) => _settings.SplitViewEnabled = SplitViewToggle.IsChecked == true;

    private void SearchReplace_Click(object sender, RoutedEventArgs e) => OpenSearchReplaceWindow();

    /// <summary>
    /// Strg+F öffnet das Suchen-&amp;-Ersetzen-Fenster (Abschnitt 5b: "es wäre gut, wenn
    /// man für schnelle Navigation direkt aus dem Überarbeitungsbereich die Suchfunktion nutzen
    /// könnte"; Abschnitt 5f: an Notepad++ angelehnte Hotkeys); Suchen und Ersetzen sind dort in
    /// einem gemeinsamen Funktionskasten zusammengefasst. Strg+H öffnete früher dasselbe Fenster und
    /// gehört seit dem GUI-Inspektor dem Suchfenster "GUI-Element inspizieren" (Strg+Umschalt+I belegt
    /// Windows, Strg+Umschalt+G AMD Adrenalin; Nutzerentscheidung 2026-09-24).
    /// </summary>
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.P)
        {
            OpenScopePathDialog();
            e.Handled = true;
            return;
        }

        // Strg+Umschalt+D: GUI-Daten-Helfer (nur mit eingeschalteten GUI-Werkzeugen und im Jomini-Format).
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.D && GuiSettings.DataHelperOffered)
        {
            _ = OpenGuiDataHelperAsync();
            e.Handled = true;
            return;
        }

        // Strg+Umschalt+B: GUI-Bausteine (nur mit eingeschalteten GUI-Werkzeugen).
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.B && GuiSettings.ToolsEnabled)
        {
            OpenGuiBlocks();
            e.Handled = true;
            return;
        }

        // Strg+Umschalt+S: alle geänderten Dokumente beider Arbeitsbereiche speichern (wie "Alle speichern" im Arbeitsbereich).
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S)
        {
            SaveAllDocuments();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (e.Key == Key.F)
        {
            OpenSearchReplaceWindow();
            e.Handled = true;
        }
        // Strg+H: Suchfenster "GUI-Element inspizieren" (GUI-Inspektor, Plan 2b).
        else if (e.Key == Key.H)
        {
            if (ParadoxModTool.Editor.Gui.GuiSettings.ToolsEnabled)
            {
                OpenGuiElementSearch();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.G)
        {
            OpenGoToLineDialog();
            e.Handled = true;
        }
        // Strg+S: das aktive Dokument speichern (wie der Knopf "Speichern" im Arbeitsbereich).
        else if (e.Key == Key.S)
        {
            SaveActiveDocumentOf(PanelPane);
            e.Handled = true;
        }
        // Strg+B: zur passenden Klammer springen (Notepad++: "Go to matching brace").
        else if (e.Key == Key.B)
        {
            ActivePane.GoToMatchingBracket();
            e.Handled = true;
        }
        // Nutzer-Feedback: "die Option hinzu, dass man im Dokument eine Codestelle bookmarken
        // kann" - Strg+F2 ist Notepad++s eigener Standard-Hotkey für "Bookmark umschalten"
        // (Konzept Abschnitt 5f: "Nutze soweit du Zugriff darauf hast, die Hotkeys für die
        // Textverarbeitung, die Notepad++ ebenfalls nutzt").
        else if (e.Key == Key.F2)
        {
            ToggleBookmarkAtCursor();
            e.Handled = true;
        }
    }

    /// <summary>Siehe Strg+F2-Zweig in <see cref="MainWindow_PreviewKeyDown"/>. Ein neues, noch
    /// nicht gespeichertes Dokument hat noch keinen Dateipfad, unter dem ein Bookmark eindeutig
    /// zugeordnet werden könnte - in diesem Fall wird der Nutzer gebeten, zuerst zu speichern
    /// (dieselbe Einschränkung wie bei Notizen, siehe <see cref="FileNote_Click"/>).</summary>
    private void ToggleBookmarkAtCursor()
    {
        if (ActivePane.CurrentFilePath is not { } filePath)
        {
            MessageBox.Show(
                MainTexts.NoFileYet(MainTexts.NoFileYetBookmark),
                CommonTexts.Notice);
            return;
        }

        if (ActivePane.CurrentCaretLine is not { } line)
        {
            return;
        }

        var bookmarksPath = BookmarkStore.GetDefaultBookmarksFilePath();
        var preview = ActivePane.GetLineText(line).Trim();
        var bookmarks = BookmarkStore.TryLoad(bookmarksPath);
        try
        {
            bookmarks = BookmarkStore.Update(bookmarksPath, b => BookmarkStore.Toggle(b, filePath, line, preview));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Bookmark bleibt für die laufende Sitzung wirksam, wird nur nicht dauerhaft gespeichert.
            BookmarkStore.Toggle(bookmarks, filePath, line, preview);
        }

        // Icon im Tab sofort nachziehen.
        foreach (var openDocument in AllPanes.SelectMany(p => p.Documents).Where(d => EditorPane.PathsEqual(d.FilePath, filePath)))
        {
            openDocument.SetHasBookmark(BookmarkStore.GetBookmarks(bookmarks, filePath).Count > 0);
        }

        // Icon-Tooltip (Zeilen + Kommentare) und Panel nach dem Speichern aus dem Store nachziehen.
        foreach (var openDocument in AllPanes.SelectMany(p => p.Documents).Where(d => EditorPane.PathsEqual(d.FilePath, filePath)))
        {
            UpdateHasNote(openDocument);
        }

        RefreshNotesPanel();
    }

    /// <summary>
    /// Springt zu einer Bookmark-Zeile einer bestimmten Datei (vom Notizfenster übergeben, siehe
    /// <see cref="ShowNoteFor"/>) - sucht zuerst, ob die Datei bereits in einem der beiden
    /// Arbeitsbereiche offen ist (dann wird DIESER Tab aktiviert, nicht notwendigerweise
    /// <see cref="ActivePane"/> - das Notizfenster kann schließlich auch über das Notiz-Icon eines
    /// gerade NICHT aktiven Tabs geöffnet worden sein), und lädt die Datei sonst neu in den
    /// aktiven Arbeitsbereich.
    /// </summary>
    private void JumpToBookmarkInFile(string filePath, int line)
    {
        foreach (var pane in new[] { LeftEditor, RightEditor })
        {
            var index = pane.Documents.ToList().FindIndex(d => EditorPane.PathsEqual(d.FilePath, filePath));
            if (index >= 0)
            {
                pane.SelectDocumentAt(index);
                pane.GoToLine(line);
                return;
            }
        }

        var document = ActivePane.LoadFile(filePath, _activeHighlighting);
        UpdateHasNote(document);
        ActivePane.GoToLine(line);
    }

    private void GoToLine_Click(object sender, RoutedEventArgs e) => OpenGoToLineDialog();

    /// <summary>
    /// Öffnet den "Gehe zu Zeile"-Dialog (Nutzer-Feedback; Strg+G wie in Notepad++) für den
    /// gerade fokussierten Arbeitsbereich (<see cref="ActivePane"/>).
    /// </summary>
    private void OpenGoToLineDialog()
    {
        var dialog = new GoToLineWindow { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            ActivePane.GoToLine(dialog.LineNumber);
        }
    }

    private void OpenSearchReplaceWindow(string? path = null)
    {
        if (_searchReplaceWindow is null)
        {
            _searchReplaceWindow = new SearchReplaceWindow(
                GetActiveDocumentText,
                SelectInActiveDocument,
                ReplaceInActiveDocument,
                ExecuteFileReplacements,
                OpenSearchResult,
                _searchResults,
                GetHideSearchResultDescriptions,
                SetHideSearchResultDescriptions,
                _activeProfile,
                () => ResolveDefaultModRoot(_activeProfile?.DefaultModRootTemplate),
                () => ComputeGameContentRoot(_gameExecutablePath),
                () => _searchLastFolder,
                folder => _searchLastFolder = folder)
            {
                Owner = this,
            };
            _searchReplaceWindow.Closed += (_, _) => _searchReplaceWindow = null;
        }

        if (_searchReplaceWindow.IsVisible)
        {
            _searchReplaceWindow.Activate();
        }
        else
        {
            _searchReplaceWindow.Show();
        }

        if (path is not null)
        {
            _searchReplaceWindow.SearchInPath(path);
        }
        else
        {
            _searchReplaceWindow.FocusSearchBox(ActivePane.SelectedText);
        }
    }

    /// <summary>
    /// Erzeugt ein Suchen-&amp;-Ersetzen-Fenster für das Vanilla-Diff-Fenster (Nutzer-Feedback): der Modus
    /// "Aktuelles Dokument" arbeitet auf dessen Editoren, Ordner-Modus und Dateiersetzung laufen wie im
    /// Hauptfenster. Eigene Trefferliste, damit die Ergebnisleiste des Hauptfensters unberührt bleibt.
    /// </summary>
    private SearchReplaceWindow CreateSearchWindowForDiff(
        Func<string> getText, Action<int, int> select, Action<string> replace, Window owner)
    {
        return new SearchReplaceWindow(
            getText,
            select,
            replace,
            ExecuteFileReplacements,
            OpenSearchResult,
            new ObservableCollection<SearchResultRow>(),
            GetHideSearchResultDescriptions,
            SetHideSearchResultDescriptions,
            _activeProfile,
            () => ResolveDefaultModRoot(_activeProfile?.DefaultModRootTemplate),
            () => ComputeGameContentRoot(_gameExecutablePath),
            () => _searchLastFolder,
            folder => _searchLastFolder = folder)
        {
            Owner = owner,
        };
    }

    private void OpenVanillaDiffWindow_Click(object sender, RoutedEventArgs e) => OpenVanillaDiffWindow();

    private void OpenVanillaDiffWindow()
    {
        if (_vanillaDiffWindow is null)
        {
            _vanillaDiffWindow = new VanillaDiffWindow(
                _activeProfile,
                () => ResolveDefaultModRoot(_activeProfile?.DefaultModRootTemplate),
                () => ComputeGameContentRoot(_gameExecutablePath),
                () => _vanillaDiffModRootOverride,
                value =>
                {
                    _vanillaDiffModRootOverride = value;
                    RefreshFileOrigins();
                },
                () => _storageSettings,
                () => _diffFoldingEnabled,
                value => _diffFoldingEnabled = value,
                CreateSearchWindowForDiff,
                () => _cuToolLastTab,
                tab => _cuToolLastTab = tab,
                RecordVanillaSnapshots,
                VanillaUpdateModDirectory,
                OpenVanillaUpdateMerge,
                () => _compatchHiddenKinds ?? DefaultHiddenKinds,
                kinds => _compatchHiddenKinds = kinds.ToList(),
                modFolder => ParadoxModTool.SearchReplace.ReplaceLists.ReplaceListStore.FileFor(GameContext.Repository.GameDirectory(GameContext.GameId), VanillaUpdateModKey(modFolder)),
                ApplyReplaceList)
            {
                Owner = this,
            };
            _vanillaDiffWindow.Closed += (_, _) => _vanillaDiffWindow = null;
        }

        if (_vanillaDiffWindow.IsVisible)
        {
            _vanillaDiffWindow.Activate();
        }
        else
        {
            _vanillaDiffWindow.Show();
        }
    }

    private bool GetHideSearchResultDescriptions() => _hideSearchResultDescriptions;

    /// <summary>
    /// Setzt den globalen "Nur gefundenen Code anzeigen"-Zustand (Nutzer-Feedback), wendet ihn auf
    /// ALLE bereits vorhandenen Zeilen der geteilten Trefferliste an (damit sich bestehende
    /// Ergebnisse sofort mit aktualisieren, statt erst bei der nächsten Suche) und hält die
    /// Checkbox hier sowie im modalen Suchen-Fenster synchron.
    /// </summary>
    private void SetHideSearchResultDescriptions(bool hide)
    {
        if (_hideSearchResultDescriptions == hide)
        {
            return;
        }

        _hideSearchResultDescriptions = hide;

        foreach (var row in _searchResults)
        {
            row.SetHideDescription(hide);
        }

        _suppressHideDescriptionSync = true;
        try
        {
            ResultsPanelHideDescriptionCheckBox.IsChecked = hide;
            _searchReplaceWindow?.SetHideDescriptionCheckboxState(hide);
        }
        finally
        {
            _suppressHideDescriptionSync = false;
        }
    }

    private void ResultsPanelHideDescriptionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressHideDescriptionSync)
        {
            return;
        }

        SetHideSearchResultDescriptions(ResultsPanelHideDescriptionCheckBox.IsChecked == true);
    }

    /// <summary>Blendet die Ergebnisleiste aus (Nutzer-Feedback: eigenes Schließen-Kreuz), OHNE die geteilten Trefferdaten zu löschen — das modale Suchen-Fenster zeigt sie unverändert weiter an.</summary>
    private void CloseResultsPanel_Click(object sender, RoutedEventArgs e)
    {
        ResultsPanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Legt fest, ob die Suchergebnisleiste über oder unter dem Arbeitsbereich erscheint
    /// (Nutzer-Feedback: "soll per Default am unteren Rand erscheinen. Erhalte aber auch die
    /// Option, es oben anzufügen."). Der Grid-Editorbereich bleibt dabei unangetastet — er ist
    /// als LETZTES Kind des äußeren DockPanel immer das füllende Element (LastChildFill), egal
    /// welche Dock-Seite die Ergebnisleiste gerade verwendet.
    /// </summary>
    private void ApplyResultsPanelPosition(bool onTop)
    {
        DockPanel.SetDock(ResultsPanel, onTop ? Dock.Top : Dock.Bottom);
    }

    /// <summary>
    /// Öffnet "Kategorien verwalten" (Einstellungsfenster oder Kategorie-Menü im Editor). Bei OK wird die neue Liste
    /// übernommen, alle offenen Dokumente ordnen sich neu ein und die Theme-Farben der eingebauten Kategorien
    /// werden über den <see cref="ThemeManager"/> gespeichert.
    /// </summary>
    private void OpenCategoriesDialog()
    {
        var dialog = new CategoriesWindow(FileCategoryClassifier.Categories, ThemeManager.EffectiveCategoryColors, ThemeManager.Current.CategoryColors) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        FileCategoryClassifier.SetCategories(dialog.Categories);
        foreach (var pane in AllPanes)
        {
            pane.RefreshAutoCategories();
        }

        // Speichert je nach aktivem Theme in dessen Datei (eigenes Theme) oder als persönliche Anpassung (Hell/Dunkel);
        // das ausgelöste ThemeChanged aktualisiert Farben, Filterleisten und offene Tabs (siehe MainWindow.Theme.cs).
        ThemeManager.SetCategoryColors(dialog.ThemeColors);
    }

    /// <summary>
    /// Lässt alle bereits offenen Tabs in BEIDEN Arbeitsbereichen ihre farbabhängigen
    /// Eigenschaften neu auslesen (Nutzer-Feedback Runde 4: Farbeinstellungen sollen sofort
    /// wirken, nicht erst nach einem Tabwechsel oder Neustart) - z. B. nach Änderung der
    /// Kategorie-Farben oder der "ganze Reiter einfärben"-Einstellung.
    /// </summary>
    private void RefreshAllDocumentColors()
    {
        foreach (var document in LeftEditor.Documents.Concat(RightEditor.Documents))
        {
            document.NotifyColorSettingsChanged();
        }
    }

    /// <summary>
    /// Öffnet das Notizfenster (Nutzer-Feedback Runde 4: "Notizfunktion ... zu einer Datei
    /// explizit Notizen [machen], nicht in der Datei selbst gespeichert") für die aktive Datei
    /// des gerade fokussierten Arbeitsbereichs (<see cref="ActivePane"/>). Ein neues, noch nicht
    /// gespeichertes Dokument hat noch keinen Dateipfad, unter dem eine Notiz eindeutig
    /// zugeordnet werden könnte - in diesem Fall wird der Nutzer stattdessen gebeten, die Datei
    /// zuerst zu speichern.
    /// </summary>
    private void FileNote_Click(object sender, RoutedEventArgs e) => FileNoteFor(ActivePane);

    /// <summary>Notiz zum aktiven Dokument eines bestimmten Arbeitsbereichs (Knopf in dessen Leiste oder in der kleinen Leiste).</summary>
    private void FileNoteFor(EditorPane pane)
    {
        if (pane.ActiveDocument is { } document)
        {
            ShowNoteFor(document);
        }
        else
        {
            MessageBox.Show(
                MainTexts.NoFileYet(MainTexts.NoFileYetNote),
                CommonTexts.Notice);
        }
    }

    /// <summary>
    /// Öffnet das Notizfenster (Nutzer-Feedback Runde 4) für <paramref name="document"/> - die
    /// zentrale Stelle, die sowohl vom Werkzeugleisten-/Menü-Button (<see cref="FileNote_Click"/>)
    /// als auch vom neuen Notiz-Icon im Dateienreiter und dem "Notizen anzeigen"-Button neben der
    /// Pfadanzeige (Nutzer-Feedback Runde 5, siehe <see cref="EditorPane.ShowNoteRequested"/>)
    /// genutzt wird. Ein neues, noch nicht gespeichertes Dokument hat noch keinen Dateipfad,
    /// unter dem eine Notiz eindeutig zugeordnet werden könnte - in diesem Fall wird der Nutzer
    /// stattdessen gebeten, die Datei zuerst zu speichern. Aktualisiert nach dem Schließen des
    /// Dialogs das Notiz-Icon an JEDEM offenen Tab derselben Datei in BEIDEN Arbeitsbereichen
    /// (dieselbe Datei kann gleichzeitig links und rechts offen sein), nicht nur am ursprünglich
    /// angeklickten Tab.
    /// </summary>
    private void ShowNoteFor(OpenDocument document)
    {
        if (document.FilePath is not { } filePath)
        {
            MessageBox.Show(
                MainTexts.NoFileYet(MainTexts.NoFileYetNote),
                CommonTexts.Notice);
            return;
        }

        var dialog = new FileNoteWindow(
            filePath,
            FileNoteStore.GetDefaultNotesFilePath(),
            BookmarkStore.GetDefaultBookmarksFilePath(),
            line => JumpToBookmarkInFile(filePath, line))
        {
            Owner = this,
        };
        dialog.ShowDialog();

        foreach (var openDocument in LeftEditor.Documents.Concat(RightEditor.Documents))
        {
            if (EditorPane.PathsEqual(openDocument.FilePath, filePath))
            {
                UpdateHasNote(openDocument);
            }
        }

        RefreshNotesPanel();
    }

    /// <summary>
    /// Aktualisiert <see cref="OpenDocument.HasNote"/> anhand der auf der Platte gespeicherten
    /// Notizen (Nutzer-Feedback Runde 5: Icon im Dateienreiter, das anzeigt, ob es zu einer Datei
    /// eine Notiz gibt). Wird nach jedem Laden/Wiederherstellen eines Dokuments aufgerufen, damit
    /// das Icon von Anfang an den richtigen Zustand zeigt, sowie in <see cref="ShowNoteFor"/> nach
    /// dem Schließen des Notizfensters. Liest die Notizdatei jedes Mal frisch von der Platte statt
    /// einen zwischengespeicherten Stand zu verwenden - dieselbe Vorsicht wie in FileNoteWindow
    /// (siehe dort).
    /// </summary>
    private void UpdateHasNote(OpenDocument document)
    {
        if (document.FilePath is not { } filePath)
        {
            document.SetHasNote(false);
            document.SetHasBookmark(false);
            document.SetHasBug(false);
            return;
        }

        var notes = FileNoteStore.TryLoad(FileNoteStore.GetDefaultNotesFilePath());
        document.SetHasNote(!string.IsNullOrEmpty(FileNoteStore.GetNote(notes, filePath)));

        // Lesezeichen-Icon im Tab (Nutzer-Feedback) läuft bewusst mit derselben Aktualisierung mit.
        var bookmarks = BookmarkStore.TryLoad(BookmarkStore.GetDefaultBookmarksFilePath());
        var fileBookmarks = BookmarkStore.GetBookmarks(bookmarks, filePath);
        document.SetHasBookmark(
            fileBookmarks.Count > 0,
            string.Join("\n", fileBookmarks.Select(b => string.IsNullOrEmpty(b.Comment) ? b.LineText : $"{b.LineText}: {b.Comment}")));

        // Bug-Symbol im Tab: offene Meldungen mit einem Bezug auf diese Datei (auch aus Branch-Kopien desselben Projekts).
        var openBugs = BugQueries.ForFile(BugReportStore.Load(BugsPath), filePath, CurrentBugProjects).Where(match => match.Report.IsOpen).ToList();
        document.SetHasBug(
            openBugs.Count > 0,
            string.Join("\n", openBugs.Select(match => match.Report.Title)));
    }

    /// <summary>
    /// Öffnet den Formatierungs-Dialog (Konzept, Abschnitt 10: "Tool zum bereinigen überschüssiger
    /// Leerzeichen oder Tab Sprünge ... auch in der Lage, bereits existierenden Codes schöner
    /// darzustellen") für das aktive Dokument des gerade fokussierten Arbeitsbereichs
    /// (<see cref="ActivePane"/>) und wendet die vom Nutzer gewählten Optionen an. Läuft bewusst
    /// NACHEINANDER (erst Struktur, dann Whitespace) statt gleichzeitig, damit ein
    /// Struktur-Fehlschlag (z. B. bei einer .yml-Datei oder fehlerhaftem Skript) die komplette
    /// Aktion abbricht, OHNE irgendetwas am Dokument zu verändern — alles-oder-nichts, damit der
    /// Nutzer nie eine unerwartete Teiländerung vorfindet. Die eigentliche Textersetzung läuft über
    /// <see cref="ReplaceInActiveDocument"/> und damit über AvalonEdits eigenen Undo (Strg+Z).
    /// </summary>
    private void FormatDocument_Click(object sender, RoutedEventArgs e)
    {
        if (ActivePane.ActiveDocument is not { } document)
        {
            MessageBox.Show(MainTexts.NoDocumentOpen, CommonTexts.Notice);
            return;
        }

        var structuralFormattingAvailable = document.FilePath is { } filePath &&
            Path.GetExtension(filePath).ToLowerInvariant() is ".txt" or ".gui" or ".gfx";

        var dialog = new FormatDocumentWindow(structuralFormattingAvailable) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!dialog.CleanWhitespace && !dialog.PrettyPrintStructure)
        {
            return;
        }

        var text = GetActiveDocumentText();

        if (dialog.PrettyPrintStructure)
        {
            var result = ScriptPrettyPrinter.TryFormat(text);
            if (!result.Success)
            {
                MessageBox.Show(
                    MainTexts.FormatFailed(result.ErrorMessage ?? string.Empty),
                    MainTexts.FormatTitle);
                return;
            }

            text = result.FormattedText!;
        }

        if (dialog.CleanWhitespace)
        {
            text = WhitespaceCleaner.Clean(text);
        }

        ReplaceInActiveDocument(text);
    }

    /// <summary>
    /// Öffnet einen Dateiauswahldialog für die Spiel-.exe (Konzept Abschnitt 8) und übernimmt bei
    /// Bestätigung den gewählten Pfad in <see cref="_gameExecutablePath"/>. Zentrale Stelle, die
    /// sowohl vom expliziten Menüpunkt "Spielpfad einstellen…" als auch implizit von
    /// <see cref="LaunchGame_Click"/> genutzt wird, falls noch kein Pfad eingestellt ist (Konzept
    /// Abschnitt 1d: "Du kannst eine allgemeine Grundeinstellung konfigurieren, die den User
    /// auffordert bei der Auswahl des Spiels den Mod Ordner, bzw. den Hauptspielordner
    /// anzugeben ...").
    /// </summary>
    /// <returns><see langword="true"/>, wenn der Nutzer einen Pfad ausgewählt hat.</returns>
    private bool PromptForGameExecutablePath()
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{MainTexts.FilterPrograms}|*.exe|{MainTexts.FilterAllFiles}|*.*",
            Title = MainTexts.GameExeChooseTitle,
        };

        if (_gameExecutablePath is { } existing && File.Exists(existing))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(existing);
        }

        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("main.gameExe");
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        _gameExecutablePath = dialog.FileName;
        RefreshFileOrigins();
        _ = UpdateGuiWorkspaceAsync(force: false);
        _errorLog?.RefreshSources();

        // Verzeichnis-Übersicht (Nutzer-Feedback Runde 10): Vorauswahl für "Ordner hinzufügen"
        // anhand des neu eingestellten Spielpfads aktualisieren - siehe ComputeGameFilesGuess.
        ExplorerPanelControl.SetSuggestedInitialFolder(ComputeGameFilesGuess(_gameExecutablePath));

        return true;
    }

    /// <summary>Verschiebt ein Dokument von einem Arbeitsbereich in den anderen (⇄-Button bzw. Drag&amp;Drop eines Tabs).</summary>
    private void MoveDocumentBetweenPanes(EditorPane from, EditorPane to, OpenDocument document, OpenDocument? insertBefore)
    {
        if (!from.Documents.Contains(document))
        {
            return;
        }

        from.ReleaseDocument(document);
        to.AdoptDocument(document, insertBefore);

        // Nutzer-Feedback: Wird ein Dokument in den zweiten Arbeitsbereich verschoben,
        // während Split-View noch aus ist, landet es sonst in einem unsichtbaren Bereich
        // (Breite 0) - Split-View daher automatisch mit einschalten.
        _settings.SplitViewEnabled = true;
    }

    private void ApplyMultiRowTabs(bool multiRow)
    {
        LeftEditor.SetMultiRowTabs(multiRow);
        RightEditor.SetMultiRowTabs(multiRow);
    }

    private void OpenStorageSettingsDialog()
    {
        var dialog = new StorageSettingsWindow(_storageSettings) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _storageSettings = dialog.Result;
        }
    }

    /// <summary>
    /// Vor dem Spielstart: Sind Dateien einer Mod geändert und nicht gespeichert, sieht das Spiel die Änderungen nicht. Fragt dann, ob gespeichert werden soll;
    /// <see langword="false"/> = Start abbrechen (Abbrechen gewählt oder Speichern fehlgeschlagen).
    /// </summary>
    private bool ConfirmUnsavedModFilesBeforeLaunch()
    {
        var registry = _mods?.Registry;
        var unsaved = AllPanes
            .SelectMany(pane => pane.Documents.Select(document => (Pane: pane, Document: document)))
            .Where(x => x.Document.IsModified && x.Document.FilePath is { } path && (x.Document.Origin == ParadoxModTool.Categories.FileOrigin.Mod || registry?.Resolve(path) is not null))
            .ToList();
        if (unsaved.Count == 0)
        {
            return true;
        }

        var names = unsaved.Select(x => x.Document.DisplayName).Distinct().ToList();
        var list = string.Join("\n", names.Take(8).Select(n => "  " + n)) + (names.Count > 8 ? "\n  " + MainTexts.LaunchMore(names.Count - 8) : string.Empty);
        var answer = MessageBox.Show(
            this,
            MainTexts.LaunchUnsaved(list),
            MainTexts.LaunchTitle,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes);
        if (answer == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (answer == MessageBoxResult.Yes)
        {
            foreach (var (pane, document) in unsaved)
            {
                pane.ActivateDocument(document);
                SaveActiveDocumentOf(pane);
                if (document.IsModified)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Startet das Spiel mit der in <see cref="LaunchOptionsCombo"/> gewählten Option (Konzept
    /// Abschnitt 8: "Es wäre super, wenn man das Spiel direkt aus dem Programm starten könnte
    /// ... das Spiel direkt im Debug Mode zu starten ... die Option das Spiel weiter mit Mods zu
    /// starten, wenn es bei Loading Resources crasht"). Ist noch kein Pfad eingestellt (erster
    /// Start oder Datei seither gelöscht/verschoben), wird zuerst danach gefragt statt den Start
    /// einfach fehlschlagen zu lassen. Die eigentliche Startlogik liegt bewusst in
    /// <see cref="GameLauncher.Launch"/> (eigenes, von ParadoxModTool.GameProfiles unabhängiges
    /// Projekt) - hier wird nur die vom Nutzer gewählte <see cref="LaunchOption"/> aus dem
    /// aktiven Spielprofil dem Launcher als einfache Zeichenkette übergeben.
    /// </summary>
    private void LaunchGame_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmUnsavedModFilesBeforeLaunch())
        {
            return;
        }

        StartGame((LaunchOptionsCombo.SelectedItem as LaunchOption)?.Args ?? string.Empty);
    }

    /// <summary>
    /// Startkern für "Spiel starten" und "Im Spiel ansehen" (Plan 4 Ruling 7): Spiel-Exe erfragen, falls nötig; gewähltes Playset aktivieren; starten
    /// und einen Start mit -debug_mode merken (Ruling 8).
    /// </summary>
    private bool StartGame(string arguments)
    {
        if (_gameExecutablePath is null || !File.Exists(_gameExecutablePath))
        {
            if (!PromptForGameExecutablePath())
            {
                return false;
            }
        }

        // Nutzer-Feedback: gewähltes Playset vor dem Start als aktiv setzen, falls es nicht
        // ohnehin schon das aktive ist - ein Fehlschlag hier (Datei fehlt/nicht gefunden/nicht
        // schreibbar) bricht den Start bewusst AB, statt das Spiel dann ggf. mit dem falschen
        // Playset zu starten, ohne dass der Nutzer das bemerkt.
        if (PlaysetsCombo.SelectedItem is PlaysetInfo selectedPlayset)
        {
            var manager = CreatePlaysetManager();
            if (manager is not null && (!manager.UsesLauncherDatabase || manager.LoadFilePath is not null))
            {
                // Launcher-Spiele: Aktiv setzen und Lade-Datei schreiben bei jedem Start (das Spiel liest die Lade-Datei); EU V: nur, wenn das Playset noch nicht aktiv ist.
                if (manager.UsesLauncherDatabase || !selectedPlayset.IsActive)
                {
                    var all = manager.LoadAll();
                    var id = (selectedPlayset.Id is { } known && all.Any(p => p.Id == known) ? known : all.FirstOrDefault(p => p.Name == selectedPlayset.Name)?.Id);
                    var prepared = id is null ? new PlaysetResult(PlaysetStatus.PlaysetMissing, selectedPlayset.Name) : manager.PrepareStart(id);
                    if (!prepared.Success)
                    {
                        MessageBox.Show(PlaysetTexts.Describe(prepared), MainTexts.LaunchPlaysetFailed);
                        return false;
                    }

                    RefreshPlaysetsCombo();
                }
            }
            else if (!selectedPlayset.IsActive)
            {
                var playsetResult = PlaysetStore.SetActivePlayset(_playsetsFilePath, selectedPlayset.Name);
                if (!playsetResult.Success)
                {
                    MessageBox.Show(playsetResult.ErrorMessage, MainTexts.LaunchPlaysetFailed);
                    return false;
                }

                RefreshPlaysetsCombo();
            }
        }

        var result = GameLauncher.Launch(_gameExecutablePath, arguments);
        if (!result.Success)
        {
            MessageBox.Show(result.ErrorMessage, MainTexts.LaunchFailed);
            return false;
        }

        _debugLaunch.Launched(_gameExecutablePath!, arguments, DateTime.UtcNow);
        return true;
    }

    /// <summary>
    /// Doppelklick auf einen Treffer in der Ergebnisleiste springt zur Fundstelle — dieselbe
    /// Navigationslogik wie im modalen Suchen-Fenster (<see cref="OpenSearchResult"/>/
    /// <see cref="SelectInActiveDocument"/>), da beide Anzeigen dieselben <see cref="SearchResultRow"/>-
    /// Objekte der geteilten Liste zeigen.
    /// </summary>
    private void ResultsPanelList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsPanelList.SelectedItem is not SearchResultRow row)
        {
            return;
        }

        if (row.FilePath is { } filePath)
        {
            OpenSearchResult(filePath, row.Offset, row.Length);
        }
        else
        {
            SelectInActiveDocument(row.Offset, row.Length);
        }
    }

    private string GetActiveDocumentText() => ActivePane.CurrentText;

    private void SelectInActiveDocument(int offset, int length) => ActivePane.SelectRange(offset, length);

    private void ReplaceInActiveDocument(string newText) => ActivePane.ReplaceActiveDocumentText(newText);

    /// <summary>
    /// Öffnet einen Treffer aus dem Suchen-in-Dateien-Modus im gerade aktiven Arbeitsbereich und
    /// springt zur Fundstelle (Abschnitt 5b). Wird die Datei dabei neu geladen, ist sie noch
    /// nicht farblich eingefärbt, falls das Spielprofil-spezifische Highlighting fehlschlug -
    /// dasselbe bekannte Verhalten wie beim normalen "Öffnen…".
    /// </summary>
    private void OpenSearchResult(string filePath, int offset, int length)
    {
        var pane = ActivePane;
        var document = pane.LoadFile(filePath, _activeHighlighting);
        UpdateHasNote(document);
        pane.SelectRange(offset, length);
    }

    /// <summary>
    /// Schreibt die vom Suchen-&amp;-Ersetzen-Fenster berechneten Ersetzungen über mehrere
    /// Dateien hinweg tatsächlich auf die Platte — als EIN Eintrag im programmweiten
    /// Aktionsverlauf (Abschnitt 5: "alle Prozesse ... mit einer Funktion rückgängig gemacht
    /// werden"), damit "Rückgängig" die komplette Suchen-&amp;-Ersetzen-Operation über alle
    /// betroffenen Dateien in einem Schritt zurücknimmt. <see cref="FileWriteUndoableAction"/>
    /// merkt sich den bisherigen Inhalt jeder Datei selbst beim Ausführen, ein vorheriges
    /// Einlesen ist hier daher nicht nötig. Ist eine betroffene Datei in einem der beiden
    /// Arbeitsbereiche bereits offen, wird ihr Tab automatisch neu geladen (Nutzer-Feedback),
    /// damit er nicht den veralteten Stand von vor der Ersetzung zeigt.
    /// </summary>
    private int ExecuteFileReplacements(IReadOnlyList<FileReplacementPlan> plans, string description)
    {
        if (plans.Count == 0)
        {
            return 0;
        }

        try
        {
            var actions = plans
                .Select(plan => (IUndoableAction)new FileWriteUndoableAction(
                    MainTexts.ReplaceUndoName(Path.GetFileName(plan.FilePath)), plan.FilePath, plan.NewContent, ModFileEncodingFor(plan.FilePath)))
                .ToList();

            _history.Execute(new CompositeUndoableAction(description, actions));

            foreach (var plan in plans)
            {
                LeftEditor.ReloadIfOpen(plan.FilePath, plan.NewContent);
                RightEditor.ReloadIfOpen(plan.FilePath, plan.NewContent);
            }

            return plans.Count;
        }
        catch (Exception ex)
        {
            MessageBox.Show(MainTexts.ReplaceFailed(ex.Message), CommonTexts.Error);
            return 0;
        }
    }

    /// <summary>
    /// Blendet den zweiten Arbeitsbereich ein bzw. aus (Abschnitt 5, Split-View). Aus dem
    /// Klick-Handler herausgelöst, damit dieselbe Logik auch von <see cref="RestoreSession"/>
    /// beim Programmstart genutzt werden kann, ohne einen Klick auf den Umschalt-Button zu simulieren.
    /// </summary>
    private void ApplySplitViewState(bool enabled)
    {
        if (enabled)
        {
            SplitterColumn.Width = new GridLength(4);
            RightPaneColumn.Width = new GridLength(1, GridUnitType.Star);
            Splitter.Visibility = Visibility.Visible;
            RightEditor.Visibility = Visibility.Visible;
        }
        else
        {
            SplitterColumn.Width = new GridLength(0);
            RightPaneColumn.Width = new GridLength(0);
            Splitter.Visibility = Visibility.Collapsed;
            RightEditor.Visibility = Visibility.Collapsed;
        }
    }

    private void ExplorerToggle_Click(object sender, RoutedEventArgs e) => _settings.ExplorerVisible = ExplorerToggle.IsChecked == true;

    /// <summary>
    /// Blendet die Verzeichnis-Übersicht ein bzw. aus (Nutzer-Feedback Runde 10). Analog zu
    /// <see cref="ApplySplitViewState"/> umgesetzt: Spaltenbreite auf 0 statt nur die
    /// Sichtbarkeit umzuschalten, damit die freiwerdende Breite tatsächlich dem restlichen
    /// Arbeitsbereich zugutekommt. Der Zustand wird bewusst NICHT über die Session gespeichert -
    /// die Übersicht ist bei jedem Start zunächst sichtbar (wie in MainWindow.xaml voreingestellt).
    /// </summary>
    private void ApplyExplorerVisibleState(bool visible)
    {
        if (visible)
        {
            ExplorerColumn.Width = new GridLength(260);
            ExplorerSplitterColumn.Width = new GridLength(4);
            ExplorerPanelControl.Visibility = Visibility.Visible;
            ExplorerSplitter.Visibility = Visibility.Visible;
        }
        else
        {
            ExplorerColumn.Width = new GridLength(0);
            ExplorerSplitterColumn.Width = new GridLength(0);
            ExplorerPanelControl.Visibility = Visibility.Collapsed;
            ExplorerSplitter.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Öffnet eine über die ◀/▶-Buttons der Verzeichnis-Übersicht ausgewählte Datei im linken
    /// oder rechten Arbeitsbereich (Nutzer-Feedback Runde 10: "Man sollte direkt in dieser
    /// Verzeichnis Übersicht auswählen können, ob man die Datei im linken oder rechten
    /// Arbeitsbereich öffnet. Zwei verschiedene Buttons sollen das ermöglichen."). Ist Split-View
    /// noch aus und das Ziel der rechte Arbeitsbereich, wird Split-View automatisch mit
    /// eingeschaltet - dasselbe Verhalten wie beim Verschieben eines Dokuments zwischen den
    /// Arbeitsbereichen (siehe WireEditorPane, MoveActiveDocumentRequested).
    /// </summary>
    private void ExplorerPanel_OpenFileRequested(object? sender, ExplorerOpenFileEventArgs e)
    {
        EditorPane pane;

        // Nutzer-Feedback: "Doppelklick auf eine Datei in der Verzeichnis Übersicht sollte die
        // Datei in der gerade aktiven Arbeitsumgebung öffnen." - ExplorerOpenTarget.Active kennt
        // (anders als Left/Right) kein festes Ziel und schaltet daher auch NICHT automatisch
        // Split-View ein, sondern verwendet einfach den gerade fokussierten Arbeitsbereich.
        if (e.Target == ExplorerOpenTarget.Active)
        {
            pane = ActivePane;
        }
        else
        {
            if (e.Target == ExplorerOpenTarget.Right)
            {
                _settings.SplitViewEnabled = true;
            }

            pane = e.Target == ExplorerOpenTarget.Right ? RightEditor : LeftEditor;
        }

        var document = pane.LoadFile(e.FilePath, _activeHighlighting);
        UpdateHasNote(document);
    }

    /// <summary>
    /// Legt eine über den "Neue Datei anlegen…"-Dialog der Verzeichnis-Übersicht angeforderte
    /// neue Moddatei tatsächlich an (Nutzer-Feedback Runde 10: "Dort soll man dann auch direkt in
    /// den Moddateien neue Dateien entsprechend der von uns bekannten Typen anlegen können.").
    /// Läuft bewusst über den programmweiten Aktionsverlauf (<see cref="FileWriteUndoableAction"/>,
    /// Konzept Abschnitt 5: "alle Prozesse ... mit einer Funktion rückgängig gemacht werden
    /// können") statt die Datei direkt zu schreiben - beim Rückgängigmachen löscht
    /// FileWriteUndoableAction die neu angelegte Datei automatisch wieder, da sie vorher nicht
    /// existierte (keine Änderung an dieser Klasse nötig). Die neue Datei wird danach sofort zur
    /// Bearbeitung im gerade aktiven Arbeitsbereich (<see cref="ActivePane"/>) geöffnet.
    /// </summary>
    private void ExplorerPanel_NewFileRequested(object? sender, ExplorerNewFileEventArgs e)
    {
        var targetPath = Path.Combine(e.FolderPath, e.FileName);

        if (File.Exists(targetPath))
        {
            MessageBox.Show(MainTexts.NewFileExists(e.FileName), MainTexts.NewFileTitle);
            return;
        }

        try
        {
            var action = new FileWriteUndoableAction(MainTexts.NewFileUndoName(e.FileName), targetPath, string.Empty, ModFileEncodingFor(targetPath));
            _history.Execute(action);
        }
        catch (Exception ex)
        {
            MessageBox.Show(MainTexts.NewFileFailed(ex.Message), CommonTexts.Error);
            return;
        }

        ExplorerPanelControl.NotifyFolderChanged(e.FolderPath);

        var document = ActivePane.LoadFile(targetPath, _activeHighlighting);
        UpdateHasNote(document);
    }

    /// <summary>
    /// Legt einen über "Neuen Ordner anlegen…" der Verzeichnis-Übersicht angeforderten neuen
    /// Unterordner an (Nutzer-Feedback: "Es sollte auch die Möglichkeit geben über die
    /// Verzeichnis Übersicht einen neuen Ordner zu erstellen"). Läuft über
    /// <see cref="DelegateUndoableAction"/> statt <see cref="FileWriteUndoableAction"/> (Konzept
    /// Abschnitt 5: "alle Prozesse ... rückgängig machen") - beim Rückgängigmachen wird der neu
    /// angelegte Ordner nur dann wieder gelöscht, wenn er zu diesem Zeitpunkt noch LEER ist
    /// (bewusst konservativ: hat der Nutzer in der Zwischenzeit bereits Dateien hineingelegt,
    /// würde ein bedingungsloses Löschen unerwartet Nutzerdaten vernichten - in diesem Fall bleibt
    /// der Ordner beim Rückgängigmachen einfach stehen).
    /// </summary>
    private void ExplorerPanel_NewFolderRequested(object? sender, ExplorerNewFolderEventArgs e)
    {
        var targetPath = Path.Combine(e.ParentFolderPath, e.FolderName);

        if (Directory.Exists(targetPath) || File.Exists(targetPath))
        {
            MessageBox.Show(MainTexts.NewFolderExists(e.FolderName), MainTexts.NewFolderTitle);
            return;
        }

        try
        {
            var action = new DelegateUndoableAction(
                MainTexts.NewFolderUndoName(e.FolderName),
                doAction: () => Directory.CreateDirectory(targetPath),
                undoAction: () =>
                {
                    if (Directory.Exists(targetPath) && !Directory.EnumerateFileSystemEntries(targetPath).Any())
                    {
                        Directory.Delete(targetPath);
                    }
                });
            _history.Execute(action);
        }
        catch (Exception ex)
        {
            MessageBox.Show(MainTexts.NewFolderFailed(ex.Message), CommonTexts.Error);
            return;
        }

        ExplorerPanelControl.NotifyFolderChanged(e.ParentFolderPath);
    }

    /// <summary>
    /// Löscht eine über "Löschen…" der Verzeichnis-Übersicht angeforderte Datei/einen Ordner
    /// (Nutzer-Feedback-Kontext: "Das Löschen der Datei funktioniert auch, allerdings wird die
    /// Verzeichnis Übersicht nach einer Löschung nicht aktualisiert" - siehe ausführlicher
    /// Kommentar in ExplorerPanel.xaml beim "Löschen…"-Menüpunkt: eine ECHTE Löschfunktion gab es
    /// in der Verzeichnis-Übersicht bisher noch gar nicht). Bewusst NICHT über den programmweiten
    /// Aktionsverlauf (<see cref="_history"/>) - eine Löschung ist grundsätzlich einseitig/riskant
    /// genug, dass eine eigene, unvollständige Undo-Implementierung mehr Verwirrung stiften würde
    /// als sie nützt; stattdessen übernimmt der WINDOWS-PAPIERKORB selbst die Rolle des "Rückgängig"
    /// (<c>Microsoft.VisualBasic.FileIO.FileSystem</c>, Teil der .NET-Windows-Desktop-Runtime -
    /// keine zusätzliche Bibliothek, dieselbe "nur unbedenkliche/zertifizierte Bibliotheken"-
    /// Anforderung aus dem Konzept wie bei den übrigen WinForms-Dialogen dieses Programms).
    /// </summary>
    private void ExplorerPanel_DeleteRequested(object? sender, ExplorerDeleteEventArgs e)
    {
        try
        {
            if (e.IsDirectory)
            {
                if (Directory.Exists(e.FullPath))
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                        e.FullPath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
            }
            else if (File.Exists(e.FullPath))
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    e.FullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(MainTexts.DeleteFailed(ex.Message), CommonTexts.Error);
            return;
        }

        // Noch offene Tabs der gelöschten Datei in BEIDEN Arbeitsbereichen schließen, damit der
        // Nutzer nicht unbemerkt an einer bereits gelöschten Datei weiterarbeitet und sie über
        // "Speichern" versehentlich wiederherstellt.
        if (!e.IsDirectory)
        {
            foreach (var pane in new[] { LeftEditor, RightEditor })
            {
                var openDocument = pane.Documents.FirstOrDefault(d => EditorPane.PathsEqual(d.FilePath, e.FullPath));
                if (openDocument is not null)
                {
                    pane.CloseDocument(openDocument);
                }
            }
        }

        // Nutzer-Feedback: "wird die Verzeichnis Übersicht nach einer Löschung nicht
        // aktualisiert, sodass es so aussieht, als sei die Datei noch da" - der eigentliche Fix.
        ExplorerPanelControl.NotifyFolderChanged(e.ParentFolderPath);
    }

    /// <summary>
    /// Löst <see cref="GameProfile.DefaultModRootTemplate"/> (enthält Platzhalter wie
    /// "%USERPROFILE%", siehe profiles/eu5.json) zu einem tatsächlichen, existierenden Pfad auf -
    /// von <see cref="MainWindow_Loaded"/> genutzt, um beim allerersten Start (noch keine aus der
    /// Session wiederhergestellten Wurzeln) automatisch eine Mod-Wurzel in der
    /// Verzeichnis-Übersicht vorzuschlagen (Konzept Abschnitt 1d: "Grundeinstellungen möglichst
    /// automatisch vorschlagen"). Liefert <see langword="null"/>, wenn kein Profil aktiv ist, kein
    /// Template hinterlegt ist, oder der aufgelöste Ordner (noch) nicht existiert - in diesem Fall
    /// bleibt die Verzeichnis-Übersicht beim ersten Start einfach leer, bis der Nutzer manuell
    /// eine Wurzel hinzufügt.
    /// </summary>
    /// <summary>
    /// Bestimmt die Wurzelordner für die Herkunftserkennung und ordnet alle offenen Dokumente neu ein.
    /// Vanilla = Spielordner (aus dem Pfad der Spiel-.exe) und Explorer-Wurzeln der Art "Spieldateien";
    /// Mod = Explorer-Wurzeln der Art "Mod-Ordner" und der Mod-Ordner der Vanilla-Diff-Einstellung.
    /// </summary>
    private void RefreshFileOrigins()
    {
        var (vanillaRoots, modRoots) = CollectOriginRoots();
        FileOriginTracker.SetRoots(vanillaRoots, modRoots);
        foreach (var pane in AllPanes)
        {
            pane.RefreshOrigins();
        }

        RefreshActiveMod();
        RefreshLockDisplays();
        RefreshModifierFormats();
    }

    private static string? ResolveDefaultModRoot(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(template);
            // Vorlagen wie "%USERPROFILE%/Documents/..." ergeben gemischte Schrägstriche; GetFullPath vereinheitlicht sie.
            return Directory.Exists(expanded) ? Path.GetFullPath(expanded) : null;
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// Best-effort-Schätzung des Spieldateien-Ordners aus dem Pfad zur Spiel-.exe (Konzept
    /// Abschnitt 8; der Tooltip zum Spielpfad weist bereits darauf hin, dass die .exe z. B. unter
    /// ".../Europa Universalis 5/binaries/" liegt) - nur für
    /// <see cref="ExplorerPanel.SetSuggestedInitialFolder"/>, die damit LEDIGLICH den Startordner
    /// des "Ordner hinzufügen"-Dialogs vorbelegt, NICHT automatisch eine Wurzel hinzufügt. Anders
    /// als beim Mod-Ordner-Template (<see cref="ResolveDefaultModRoot"/>) gibt es hierfür keine
    /// über die Wiki bestätigte feste Konvention, wo die Spieldateien relativ zur .exe liegen -
    /// diese Schätzung (Elternordner des "binaries"-Ordners) ist daher bewusst nur eine
    /// unverbindliche Vorauswahl, die der Nutzer jederzeit überschreiben kann.
    /// </summary>
    private static string? ComputeGameFilesGuess(string? exePath)
    {
        return ParadoxModTool.Mods.GameContentRoot.InstallRoot(exePath);
    }

    /// <summary>
    /// Der tatsächliche Vanilla-Dateien-Wurzelordner (enthält common/gui/in_game/main_menu/...
    /// direkt), NICHT identisch mit <see cref="ComputeGameFilesGuess"/>s Ergebnis: bei einer
    /// echten EU5-Installation (vom Nutzer am 2026-09-17 bestätigt) liegt "binaries" (mit der
    /// .exe) als GESCHWISTER-Ordner neben "game" (mit den eigentlichen Spieldateien) direkt unter
    /// dem Installationsordner - "game" ist also NICHT im Elternordner von "binaries" selbst,
    /// sondern ein eigener Unterordner davon. Verwendet für <see cref="VanillaFileTracker"/>
    /// (VanillaDiffWindow) und die Schnellzugriff-Buttons in <see cref="SearchReplaceWindow"/>
    /// (in_game/main_menu/loading_screen) - beide brauchten vorher fälschlich
    /// <see cref="ComputeGameFilesGuess"/>s Ergebnis bzw. dessen "binaries"-Unterordner.
    /// </summary>
    private static string? ComputeGameContentRoot(string? exePath)
    {
        // EU V: binaries\ + game\ nebeneinander; EU IV: Exe und Dateien im selben Ordner (siehe GameContentRoot).
        return ParadoxModTool.Mods.GameContentRoot.Find(exePath);
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var action = _history.Undo();
            ReloadOpenTabsAffectedBy(action);
        }
        catch (Exception ex)
        {
            MessageBox.Show(MainTexts.UndoFailed(ex.Message), CommonTexts.Error);
        }
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var action = _history.Redo();
            ReloadOpenTabsAffectedBy(action);
        }
        catch (Exception ex)
        {
            MessageBox.Show(MainTexts.RedoFailed(ex.Message), CommonTexts.Error);
        }
    }

    /// <summary>
    /// Lädt nach einem Undo/Redo alle offenen Tabs neu, die eine der betroffenen Dateien
    /// anzeigen (Nutzer-Feedback: "Es ist [...] wichtig, dass man eine Ersetzung in Ordnern
    /// Aktion rückgängig machen kann. In dem Fall sollten sich dann auch aktive Dateien neu
    /// laden, sonst führt das zu Verwirrung."). Bewusst als GENERELLER Mechanismus für JEDES
    /// <see cref="FileWriteUndoableAction"/> umgesetzt (nicht nur für Massenersetzungen in
    /// Ordnern), da ein offener Tab nach JEDEM Undo/Redo den tatsächlichen Dateiinhalt zeigen
    /// sollte, nicht nur nach diesem einen Werkzeug. Ein einzelnes seither nicht mehr lesbares
    /// Betroffenes Element bricht das Neuladen der übrigen nicht ab.
    /// </summary>
    private void ReloadOpenTabsAffectedBy(IUndoableAction action)
    {
        var filePaths = new List<string>();
        CollectFilePaths(action, filePaths);

        var rebuildGuiIndex = false;

        foreach (var filePath in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    // Eine .gui-Datei ist verschwunden (z.B. Rückgängig einer Übernahme in die Mod): GUI-Index danach einmal neu aufbauen.
                    // Klassisch gilt das auch für eine .gfx-Datei (Rückgängig von "Sprite anlegen"): Sonst bliebe das Sprite in der Tabelle.
                    if (filePath.EndsWith(".gui", StringComparison.OrdinalIgnoreCase)
                        || (filePath.EndsWith(".gfx", StringComparison.OrdinalIgnoreCase) && CurrentGuiLayout.IsClassic))
                    {
                        rebuildGuiIndex = true;
                    }

                    continue;
                }

                // Dieselbe Regel wie beim Öffnen: .gui/.gfx in Windows-1252 (EU IV) sonst als Ersatzzeichen in Tab und GUI-Index.
                var content = ParadoxModTool.GuiDesign.Index.GuiTextReader.ReadFile(filePath);
                LeftEditor.ReloadIfOpen(filePath, content);
                RightEditor.ReloadIfOpen(filePath, content);

                // GUI-Index nachziehen (z.B. nach Rückgängig einer Übernahme in die Mod).
                NotifyGuiFileSaved(filePath, content);
            }
            catch (IOException)
            {
                // Datei ist gerade nicht lesbar - übrige betroffene Dateien trotzdem neu laden.
            }
            catch (UnauthorizedAccessException)
            {
                // Datei ist gerade nicht lesbar - übrige betroffene Dateien trotzdem neu laden.
            }
        }

        // Einmal für alle verschwundenen .gui-Dateien, nicht je Datei (3b-Nacharbeit: vorher N volle Neuaufbauten).
        if (rebuildGuiIndex)
        {
            _ = UpdateGuiWorkspaceAsync(force: true);
        }
    }

    /// <summary>
    /// Sammelt rekursiv alle Dateipfade, die von <paramref name="action"/> geschrieben wurden -
    /// eine <see cref="CompositeUndoableAction"/> (z. B. Ersetzen in mehreren Dateien) besteht aus
    /// mehreren <see cref="FileWriteUndoableAction"/>-Einzelschritten, die einzeln eingesammelt
    /// werden müssen. Andere Aktionstypen (z. B. <see cref="DelegateUndoableAction"/>) betreffen
    /// keine bekannte Datei und werden ignoriert.
    /// </summary>
    private static void CollectFilePaths(IUndoableAction action, List<string> filePaths)
    {
        switch (action)
        {
            case FileWriteUndoableAction fileWrite:
                filePaths.Add(fileWrite.FilePath);
                break;
            case CompositeUndoableAction composite:
                foreach (var inner in composite.Actions)
                {
                    CollectFilePaths(inner, filePaths);
                }

                break;
        }
    }

    private void UpdateUndoRedoButtons()
    {
        UndoButton.IsEnabled = _history.CanUndo;
        RedoButton.IsEnabled = _history.CanRedo;
        UndoButton.ToolTip = MenuTexts.UndoTip(_history.NextUndoDescription);
        RedoButton.ToolTip = MenuTexts.RedoTip(_history.NextRedoDescription);
    }
}
