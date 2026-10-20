using System.IO;
using System.Windows;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.Editor;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.UndoRedo;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.Mods;
using ParadoxModTool.Session;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

public partial class MainWindow
{
    private GuiWorkspaceService? _guiService;

    /// <summary>Kontextordner und Überschreib-Reihenfolge des aktiven Spiels (EU V ohne Profil).</summary>
    private GuiLayout CurrentGuiLayout => GameContext.Profile is { } profile ? GuiRules.FromProfile(profile).Layout : GuiLayout.Default;

    private GuiWorkspaceService GuiService => _guiService ??= new GuiWorkspaceService(
        Path.Combine(GameContext.Repository.GameDirectory(GameContext.GameId), "gui", "catalog.json"),
        GameContext.Profile is { } profile ? GuiRules.FromProfile(profile) : GuiRules.Empty);

    /// <summary>Setzt die Texte des Menüs GUI und baut den GUI-Index gleich nach dem Start im Hintergrund auf.</summary>
    private void InitializeGuiWorkspace()
    {
        GuiMenuItem.Header = GuiTexts.MenuGui;
        GuiInspectMenuItem.Header = GuiTexts.MenuInspect;
        GuiInspectMenuItem.ToolTip = GuiTexts.MenuInspectTip;
        GuiInspectMenuItem.InputGestureText = GuiTexts.KeyInspect;
        GuiDataHelperMenuItem.Header = GuiDataTexts.Title;
        GuiDataHelperMenuItem.ToolTip = GuiDataTexts.MenuTip;
        GuiDataHelperMenuItem.InputGestureText = GuiDataTexts.Key;
        GuiBlocksMenuItem.Header = GuiTexts.MenuBlocks;
        GuiBlocksMenuItem.ToolTip = GuiTexts.MenuBlocksTip;
        GuiBlocksMenuItem.InputGestureText = GuiTexts.KeyBlocks;
        GuiNewFileMenuItem.Header = GuiTexts.MenuNewFile;
        GuiNewFileMenuItem.ToolTip = GuiTexts.MenuNewFileTip;
        GuiModSettingsMenuItem.Header = GuiTexts.MenuModSettings;
        GuiModSettingsMenuItem.ToolTip = GuiTexts.MenuModSettingsTip;
        GuiSettings.ColorPicker = PickGuiColor;
        GuiSettings.DataHelper = OpenGuiDataHelperFor;
        GuiSettings.ConditionHelper = OpenGuiConditionHelperFor;
        GuiSettings.BlockSource = () => LoadGuiBlockLibrary().Effective;
        GuiSettings.BlockPicker = PickGuiBlock;
        SyncGuiInspectMenu();
        _ = UpdateGuiWorkspaceAsync(force: false);
    }

    /// <summary>Gleicht den GUI-Index mit Spielpfad und aktiver Mod ab (baut nur neu, wenn sich etwas geändert hat oder <paramref name="force"/>). Bei ausgeschalteten GUI-Werkzeugen passiert nichts.</summary>
    private async Task UpdateGuiWorkspaceAsync(bool force, CancellationToken cancellation = default, IProgress<(int Done, int Total)>? progress = null)
    {
        // Installationsordner: Jomini mit game, jomini, clausewitz darin; klassisch (HOI IV, EU IV) der Exe-Ordner selbst, in dem interface/ liegt
        // (GameContentRoot.InstallRoot erkennt ihn an common/ ohne game/).
        var installDirectory = ComputeGameFilesGuess(_gameExecutablePath);

        // Schreibschutz für Vanilla-.gui (auch jomini, clausewitz; klassisch alles unter der Installation) schon vor dem Index (Plan-Ruling 3), auch bei
        // ausgeschalteten GUI-Werkzeugen.
        ApplyGuiProfile();
        GuiSettings.InstallDirectory = installDirectory;
        if (!GuiSettings.ToolsEnabled)
        {
            GuiSettings.SetWorkspace(null, GuiTexts.IndexToolsOff);
            return;
        }

        var (modRoot, dependencies) = CurrentGuiModRoots();
        await GuiService.UpdateAsync(installDirectory, modRoot, dependencies, force, cancellation, progress);
    }

    /// <summary>
    /// Format der .gui-Dateien und "Designer anbieten" aus dem Spielprofil. Ohne gui-Abschnitt (Stellaris) kein Designer; ohne Profil bleibt es beim
    /// bisherigen Verhalten (Jomini, Designer an).
    /// </summary>
    private void ApplyGuiProfile()
    {
        GuiSettings.Layout = CurrentGuiLayout;
        GuiSettings.ProfileHasGui = GameContext.Profile is not { } profile || profile.Gui is not null;
        GuiSettings.GameSettingsPath = ClassicScreenSettings.PathFor(GameContext.Profile?.DefaultModRootTemplate);
        // Benutzerordner (Elternordner des Mod-Ordners): dort liegen bei Jomini pdx_settings.txt bzw. pdx_settings.json.
        GuiSettings.GameUserFolder = GuiSettings.GameSettingsPath is { } settingsPath ? Path.GetDirectoryName(settingsPath) : null;
        GuiSettings.GameId = GameContext.Profile?.Id;
        GuiSettings.Explanations = ClassicExplanationsFor(GuiSettings.GameId, GuiSettings.Layout.IsClassic);
        WireClassicSpriteTools(GuiSettings.Layout.IsClassic);
    }

    private string? _explanationsGameId;
    private ClassicExplanations _explanations = ClassicExplanations.Empty;

    /// <summary>
    /// Erklärtexte des klassischen Designers (<c>profiles/guiexplain/classic.json</c> neben dem Programm) für das Spiel; Jomini-Spiele und Spiele ohne
    /// Profil bekommen die leere Menge. Je Spiel einmal gelesen: der Index baut oft neu, die Datei ändert sich nicht.
    /// </summary>
    private ClassicExplanations ClassicExplanationsFor(string? gameId, bool classic)
    {
        if (!classic || gameId is null)
        {
            return ClassicExplanations.Empty;
        }

        if (_explanationsGameId != gameId)
        {
            _explanations = ClassicExplanations.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles", "guiexplain", "classic.json"), gameId);
            _explanationsGameId = gameId;
        }

        return _explanations;
    }

    private string? _guiModRoot;

    /// <summary>
    /// Ordner der aktiven Mod (der, in dem die aktive Datei liegt) und bei einem Submod der Ordner seiner Hauptmod als Abhängigkeit.
    /// Klebend: Liegt die aktive Datei in keiner Mod (z.B. eine Vanilla-Datei zum Nachschlagen), bleibt die zuletzt erkannte Mod aktiv,
    /// damit ein Tabwechsel den Index nicht jedes Mal neu baut.
    /// </summary>
    private (string? ModRoot, IReadOnlyList<string> Dependencies) CurrentGuiModRoots()
    {
        var file = (_modSourcePane ?? PanelPane).CurrentFilePath;
        var candidate = DetectGuiModRoot(file);
        if (candidate is not null && file is not null && IsInsideFolder(file, candidate))
        {
            _guiModRoot = candidate;
        }

        // Nur der Ordner ist klebend; die Abhängigkeiten (je Mod einstellbar, Plan-Ruling 10) werden jedes Mal neu gelesen.
        var root = _guiModRoot ?? candidate;
        return (root, root is not null && _mods?.Registry.Resolve(root) is { } mod ? ModGuiDependencies.For(_mods.Registry, mod) : Array.Empty<string>());
    }

    private string? DetectGuiModRoot(string? file) =>
        _mods?.ActiveMod is not { } mod
            ? _mods?.UnmanagedFolder
            : mod.Paths.FirstOrDefault(p => file is not null && IsInsideFolder(file, p)) ?? mod.Paths.FirstOrDefault();

    private static bool IsInsideFolder(string file, string folder) =>
        Path.GetFullPath(file).StartsWith(
            Path.GetFullPath(folder).TrimEnd('\\', '/') + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Die Arbeitsbereiche existieren schon (XAML); <see cref="GuiSettings.Load"/> meldet die Werte, damit offene .gui-Tabs Ansicht und Aufteilung übernehmen.</summary>
    private void LoadGuiSettings(AppSessionState state)
    {
        // Vor GuiSettings.Load: dessen Meldung lässt wiederhergestellte .gui-Tabs Designer und Schreibschutz schon nach dem Spielprofil wählen.
        ApplyGuiProfile();
        GuiSettings.Load(
            state.GuiToolsEnabled,
            state.GuiDesignerEnabled,
            Enum.TryParse<GuiViewMode>(state.GuiDesignerMode, out var mode) ? mode : GuiViewMode.Code,
            state.GuiDesignerLayout,
            state.GuiBreadcrumbEnabled,
            state.GuiShownInEnabled);
        // Ein Prozess je Spiel (GameContext.LaunchWindow): die Schlüssel dieses Spiels werden nur hier, beim Start, geladen.
        GuiSettings.LoadHiddenConditionals(state.GuiHiddenConditionals);
        SyncGuiInspectMenu();
    }

    private static void SaveGuiSettings(AppSessionState state)
    {
        state.GuiToolsEnabled = GuiSettings.ToolsEnabled;
        state.GuiDesignerEnabled = GuiSettings.DesignerEnabled;
        state.GuiDesignerMode = GuiSettings.DefaultMode.ToString();
        state.GuiDesignerLayout = GuiSettings.DesignerLayout;
        state.GuiBreadcrumbEnabled = GuiSettings.BreadcrumbEnabled;
        state.GuiShownInEnabled = GuiSettings.ShownInEnabled;
        state.GuiHiddenConditionals = GuiSettings.HiddenConditionals.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Baut den GUI-Index mit Fortschrittsfenster neu auf; nach einem Abbruch bleibt der bisherige Index, ohne Meldung.</summary>
    private async Task RebuildGuiIndexAsync()
    {
        if (await RunWithProgressAsync(GuiTexts.MenuRebuild, (cancellation, progress) => UpdateGuiWorkspaceAsync(force: true, cancellation, progress)))
        {
            MessageBox.Show(this, GuiSettings.Status, GuiTexts.IndexTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>Nach dem Speichern: geänderte .gui-Dateien der Mod in den Index übernehmen, nach .yml die Vorschautexte neu lesen (nur bei eingeschalteten GUI-Werkzeugen).</summary>
    private void NotifyGuiFileSaved(string path, string text)
    {
        if (GuiSettings.ToolsEnabled)
        {
            GuiService.FileSaved(path, text);
        }
    }

    /// <summary>
    /// Hauptschalter der GUI-Werkzeuge. Aus: Index freigeben, offene GUI-Fenster schließen, Menüpunkt und Strg+H ohne Wirkung.
    /// An: Index sofort (im Hintergrund) aufbauen. Die einzelnen GUI-Schalter behalten ihren Wert.
    /// </summary>
    private void SetGuiToolsEnabled(bool enabled)
    {
        GuiSettings.SetToolsEnabled(enabled);
        if (enabled)
        {
            _ = UpdateGuiWorkspaceAsync(force: false);
        }
        else
        {
            if (_guiService is { } service)
            {
                service.Clear();
            }
            else
            {
                GuiSettings.SetWorkspace(null, GuiTexts.IndexToolsOff);
            }

            _guiInspector?.Close();
            _guiSearch?.Close();
            _guiShownIn?.Close();
        }

        SyncGuiInspectMenu();
    }

    /// <summary>
    /// Menüpunkt "GUI-Element inspizieren" nur bei eingeschalteten GUI-Werkzeugen; Daten-Helfer, Bausteine und "Neue GUI-Datei" außerdem nur im
    /// Jomini-Format (das Spiel wechselt nur mit einem Neustart, das Profil steht also schon fest).
    /// </summary>
    private void SyncGuiInspectMenu()
    {
        GuiInspectMenuItem.Visibility = GuiSettings.ToolsEnabled ? Visibility.Visible : Visibility.Collapsed;
        GuiDataHelperMenuItem.Visibility = GuiSettings.DataHelperOffered ? Visibility.Visible : Visibility.Collapsed;
        GuiBlocksMenuItem.Visibility = GuiSettings.BlocksOffered ? Visibility.Visible : Visibility.Collapsed;
        GuiNewFileMenuItem.Visibility = GuiSettings.NewFileOffered ? Visibility.Visible : Visibility.Collapsed;
        RebuildToolbar();
        TileBarControl.Rebuild();
    }

    private GuiInspectorWindow? _guiInspector;
    private GuiElementSearchWindow? _guiSearch;
    private GuiShownInWindow? _guiShownIn;

    /// <summary>Verbindet "Inspizieren" und "Wird angezeigt in (GUI)" eines Arbeitsbereichs mit den GUI-Fenstern.</summary>
    private void WireGuiInspector(EditorPane pane)
    {
        pane.GuiInspectRequested += (_, target) => InspectGuiElement(target);
        pane.GuiShownInRequested += (_, word) => ShowGuiReferences(word);
        pane.GuiDraftRequested += (_, document) => StartGuiDraft(pane, document);
        pane.GuiTakeOverRequested += (_, document) => TakeOverGuiDraft(document);
        pane.GuiDraftDiscardRequested += (_, document) => DiscardGuiDraft(document, ask: true);
        pane.GuiEditDefinitionRequested += (_, e) => OpenGuiInDesigner(e.Path, e.Line, startDraft: true);
        pane.GuiViewInGameRequested += (_, _) => ViewInGame();
        pane.GuiBlocksRequested += (_, _) => OpenGuiBlocks();
        pane.GuiSaveBlockRequested += (_, code) => SaveGuiBlock(code);
    }

    /// <summary>Zeigt ein Element im GUI-Inspektor (ein Fenster, das weiterverwendet wird).</summary>
    private void InspectGuiElement(GuiInspectTarget target)
    {
        if (_guiInspector is null)
        {
            _guiInspector = new GuiInspectorWindow { Owner = this };
            _guiInspector.OpenLocationRequested += (_, e) => JumpToBookmarkInFile(e.Path, e.Line);
            _guiInspector.PickElementRequested += (_, _) => OpenGuiElementSearch();
            _guiInspector.OpenInDesignerRequested += (_, e) => OpenGuiInDesigner(e.Path, e.Line, startDraft: true);
            _guiInspector.TakeOverAsTypeRequested += (_, target) => TakeOverAsNewType(target);
            _guiInspector.Closed += (_, _) => _guiInspector = null;
            _guiInspector.Show();
        }

        _guiInspector.Inspect(target);
        _guiInspector.Activate();
    }

    /// <summary>Suchfenster "GUI-Element inspizieren" (Menü und Strg+H).</summary>
    private void OpenGuiElementSearch()
    {
        if (_guiSearch is null)
        {
            _guiSearch = new GuiElementSearchWindow { Owner = this };
            _guiSearch.InspectRequested += (_, target) => InspectGuiElement(target);
            _guiSearch.Closed += (_, _) => _guiSearch = null;
            _guiSearch.Show();
        }

        _guiSearch.Activate();
    }

    private void ShowGuiReferences(string word)
    {
        if (_guiShownIn is null)
        {
            _guiShownIn = new GuiShownInWindow { Owner = this };
            _guiShownIn.InspectRequested += (_, target) => InspectGuiElement(target);
            _guiShownIn.OpenLocationRequested += (_, e) => JumpToBookmarkInFile(e.Path, e.Line);
            _guiShownIn.Closed += (_, _) => _guiShownIn = null;
            _guiShownIn.Show();
        }

        _guiShownIn.Search(word);
        _guiShownIn.Activate();
    }

    private void GuiInspectMenuItem_Click(object sender, RoutedEventArgs e) => OpenGuiElementSearch();

    /// <summary>
    /// "Neue GUI-Datei" (Plan 3a Ruling 10): Datei in &lt;Kontext&gt;/gui der aktiven Mod (Plan 3b Ruling 2) über den Aktionsverlauf anlegen
    /// (rückgängig machbar), im aktiven Arbeitsbereich in "Geteilt" öffnen und in den GUI-Index übernehmen.
    /// </summary>
    private void NewGuiFile_Click(object sender, RoutedEventArgs e)
    {
        // Klassisch (HOI IV, EU IV) läge die Datei in <mod>/gui, das weder Spiel noch Index lesen; kommt mit Plan 3.
        if (!GuiSettings.NewFileOffered)
        {
            MessageBox.Show(this, GuiTexts.ClassicBlocksLater, GuiTexts.NewFileTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (CurrentGuiModRoots().ModRoot is not { } modRoot)
        {
            MessageBox.Show(this, GuiTexts.NewFileNoMod, GuiTexts.NewFileTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var library = LoadGuiBlockLibrary();
        var dialog = new GuiNewFileWindow(modRoot, library.Effective, CurrentGuiLayout) { Owner = this };
        if (dialog.ShowDialog() != true || GuiBlockFileText(library, dialog.Block!, dialog.FileName) is not { } text)
        {
            return;
        }

        var folder = dialog.Folder;
        var path = Path.Combine(folder, dialog.FileName);
        try
        {
            Directory.CreateDirectory(folder);
            _history.Execute(new FileWriteUndoableAction(GuiTexts.NewFileAction(dialog.FileName), path, text, ModFileEncodingFor(path)));
            _newModFiles.Created(path, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, GuiTexts.NewFileFailed(ex.Message), GuiTexts.NewFileTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ExplorerPanelControl.NotifyFolderChanged(folder);
        var pane = ActivePane;
        var document = pane.LoadFile(path, _activeHighlighting);
        UpdateHasNote(document);
        pane.ShowGuiMode(document, GuiViewMode.Split);
        NotifyGuiFileSaved(path, text);
    }

    /// <summary>Farbdialog für das Eigenschaftenfeld des Designers (das Editor-Projekt hat kein WinForms).</summary>
    private System.Windows.Media.Color? PickGuiColor(System.Windows.Media.Color current)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B),
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? System.Windows.Media.Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B)
            : null;
    }
}
