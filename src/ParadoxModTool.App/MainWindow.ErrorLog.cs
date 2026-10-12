using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ParadoxModTool.Editor;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.Explorer;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.ScriptDocs;
using ParadoxModTool.Session;
using ParadoxModTool.Settings;

namespace ParadoxModTool.App;

/// <summary>
/// Fehlerprotokoll im Hauptfenster: Dienst mit Sitzung und Symbolindex, Menüpunkt, Fenster, gemerkter Logordner; ab Plan 2b auch
/// Markierungen an Tabs und im Explorer, Abschnitt im Notizbereich und "Im GUI-Inspektor zeigen".
/// </summary>
public partial class MainWindow
{
    private ErrorLogService? _errorLog;
    private ErrorLogWindow? _errorLogWindow;
    private string? _errorLogFolderOverride;
    private bool _errorDisplaysPending;

    private void LoadErrorLogSettings(AppSessionState state) => _errorLogFolderOverride = state.ErrorLogFolder;

    /// <summary>
    /// Beim Schließen von PMT (Plan-2b-Ruling 7): Logordner merken, Fenster schließen, Hintergrundarbeit beenden. Die Fensterposition hat
    /// <see cref="SaveErrorLogWindowBounds"/> vorher übernommen.
    /// </summary>
    private void SaveErrorLogSettings(AppSessionState state)
    {
        state.ErrorLogFolder = _errorLogFolderOverride;
        _errorLogWindow?.Close();
        _errorLog?.Dispose();
    }

    /// <summary>Vor dem Schreiben der Einstellungen: Position und Größe des Fehlerprotokoll-Fensters übernehmen.</summary>
    private void SaveErrorLogWindowBounds() => _errorLogWindow?.SaveBounds();

    /// <summary>
    /// Beim Start (nach Mods und GUI-Index): Markierungen, Notizbereich und Ereignisse verdrahten und, wenn das Spiel einen Fehlerkatalog hat,
    /// den letzten Lauf im Hintergrund laden (Plan-2b-Ruling 1).
    /// </summary>
    private void InitializeErrorLog()
    {
        TabErrorProvider.Provider = TabErrorMarkFor;
        ExplorerPanelControl.ErrorMarkProvider = ExplorerErrorMarkFor;
        ExplorerPanelControl.ErrorMarkClicked += (_, path) => OpenErrorLogWindow(path);
        NotesPanelControl.JumpToErrorRequested += (_, line) =>
        {
            if (PanelPane.CurrentFilePath is { } path)
            {
                JumpToBookmarkInFile(path, line);
            }
        };
        NotesPanelControl.ShowInErrorLogRequested += (_, _) => OpenErrorLogWindow(PanelPane.CurrentFilePath);
        TabIconSettings.Changed += (_, _) => ScheduleErrorDisplays();

        if ((GameContext.Profile ?? _activeProfile)?.ErrorLogRules is not null)
        {
            ErrorLogServiceFor();
        }
    }

    private void OpenErrorLog_Click(object sender, RoutedEventArgs e) => OpenErrorLogWindow(null);

    /// <summary>Öffnet das Fenster "Fehlerprotokoll" (nur eines); mit Pfad gefiltert auf diese Datei bzw. diesen Ordner.</summary>
    private void OpenErrorLogWindow(string? path)
    {
        var service = ErrorLogServiceFor();
        service.WantIndex();
        if (_errorLogWindow is null)
        {
            // Plan-2b-Ruling 5: Die Einstellung gilt ab dem nächsten Öffnen.
            var owner = _settings.ErrorLogStandalone ? null : this;
            _errorLogWindow = new ErrorLogWindow(service, _settings, owner, OtherScreenBounds);
            _errorLogWindow.Closed += (_, _) => _errorLogWindow = null;
        }

        _errorLogWindow.ShowPath(path);
        if (!_errorLogWindow.IsVisible)
        {
            _errorLogWindow.Show();
            return;
        }

        if (_errorLogWindow.WindowState == WindowState.Minimized)
        {
            _errorLogWindow.WindowState = WindowState.Normal;
        }

        _errorLogWindow.Activate();
    }

    /// <summary>Legt den Dienst beim ersten Bedarf an; er lebt danach bis zum Beenden von PMT.</summary>
    private ErrorLogService ErrorLogServiceFor()
    {
        if (_errorLog is not null)
        {
            return _errorLog;
        }

        var profile = GameContext.Profile ?? _activeProfile;
        var catalogPath = ErrorCatalog.PathFor(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles"), profile?.ErrorLogRules);
        var catalog = catalogPath is null ? ErrorCatalog.Empty : ErrorCatalog.Load(catalogPath);
        var host = new ErrorLogHost(
            GameContext.DisplayNameOf(GameContext.GameId),
            () => _mods?.Registry,
            _mods?.MetadataFile ?? profile?.MetadataFile ?? string.Empty,
            profile?.SteamAppId,
            profile?.DefaultModRootTemplate,
            () => ParadoxModTool.Mods.GameContentRoot.InstallRoot(_gameExecutablePath),
            () => ComputeGameContentRoot(_gameExecutablePath),
            () => _playsetsFilePath,
            EngineCommandKindOf,
            JumpToBookmarkInFile,
            InspectGuiAt,
            ReplaceNameAt,
            ApplyRename,
            AddModToPlaysetFor,
            ErrorLogSnapshotDirectory,
            profile?.SaveGames is not null,
            () => _saveExplorer?.Session.View?.Current,
            LoadNewestSaveForErrorLog,
            name => OpenSaveExplorerWindow(name),
            ApplyCommentOut,
            (plans, texts) => ApplyModEdits(plans, texts.Description, texts.SnapshotNote, texts.Nothing, texts.Failed, texts.Done),
            () => ParadoxModTool.LaunchControl.GameProcess.IsRunning(_gameExecutablePath),
            ShowSaveObject);
        _errorLog = new ErrorLogService(
            host,
            catalog,
            catalogPath is not null,
            ErrorLogLocations.SuppressionsPath(GameContext.Repository.GameDirectory(GameContext.GameId)),
            _errorLogFolderOverride ?? ErrorLogLocations.LogFolderFor(profile?.DefaultModRootTemplate));
        _errorLog.LogFolderChosen += (_, folder) => _errorLogFolderOverride = folder;
        _errorLog.Session.Changed += (_, _) => ScheduleErrorDisplays();
        _errorLog.LiveCountChanged += (_, _) => UpdateErrorLogMenuHeader();
        _errorLog.Start();
        return _errorLog;
    }

    /// <summary>Menüpunkt "Fehlerprotokoll (+N)", solange Live neue sichtbare Meldungen bringt (Spec, Live-Beobachtung).</summary>
    private void UpdateErrorLogMenuHeader()
    {
        var count = _errorLog?.LiveNewCount ?? 0;
        ErrorLogMenuItem.Header = count > 0 ? ErrorLogTexts.MenuWithCount(count) : ErrorLogTexts.Menu;
        TileBarControl.SetBadge(ParadoxModTool.Settings.TileIds.ErrorLog, count);
    }

    /// <summary>Jede Änderung der Sitzung zieht Tabs, Explorer und Notizbereich nach; mehrere Änderungen hintereinander nur einmal.</summary>
    private void ScheduleErrorDisplays()
    {
        if (_errorDisplaysPending)
        {
            return;
        }

        _errorDisplaysPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _errorDisplaysPending = false;
            foreach (var pane in AllPanes)
            {
                pane.RefreshErrorMarks();
            }

            ExplorerPanelControl.RefreshErrorMarks();
            _saveExplorerWindow?.RefreshErrorMarks();

            // Nur den Fehlerabschnitt: RefreshNotesPanel würde bei jeder Live-Portion Notizen, Lesezeichen und Bugs neu von der Platte lesen.
            NotesPanelControl.SetErrors(ErrorItemsFor(PanelPane.CurrentFilePath));
            UpdateNotesPanelVisibility();
        }));
    }

    private TabErrorMark? TabErrorMarkFor(string path) =>
        _errorLog?.Session.MarkForFile(path) is { } mark ? new TabErrorMark(mark.Count, ErrorLogTexts.MarkTooltip(mark)) : null;

    /// <summary>Markierung im Explorer; ausgeblendet mit dem Symbol "Fehlerprotokoll" (Plan-2b-Ruling 14).</summary>
    private ExplorerErrorMark? ExplorerErrorMarkFor(string path, bool isDirectory)
    {
        if (_errorLog is null || !TabIconSettings.IsVisible(TabIconKind.ErrorLog))
        {
            return null;
        }

        var mark = isDirectory ? _errorLog.Session.MarkForFolder(path) : _errorLog.Session.MarkForFile(path);
        return mark is null ? null : new ExplorerErrorMark("⚠" + mark.Count, ErrorLogTexts.MarkTooltip(mark));
    }

    /// <summary>Zeilen für den Abschnitt "⚠ Fehler" im Notizbereich (Plan-2b-Ruling 4).</summary>
    private IReadOnlyList<NotesErrorItem> ErrorItemsFor(string? path)
    {
        if (path is null || _errorLog is null)
        {
            return Array.Empty<NotesErrorItem>();
        }

        var session = _errorLog.Session;
        return session.EntriesForFile(path)
            .Select(e => new NotesErrorItem(
                ErrorLogTexts.SeverityIcon(e.Severity) + " " + ErrorLogTexts.GroupTitle(ErrorLogTexts.KindName(e.Kind, session.Language), e.Name ?? string.Empty),
                e.FileLine is int line ? ErrorLogTexts.NotesLine(line) + "  " + FirstLine(e.Message) : FirstLine(e.Message),
                e.FileLine))
            .ToList();
    }

    private static string FirstLine(string text)
    {
        var end = text.IndexOf('\n');
        return end < 0 ? text : text[..end];
    }

    /// <summary>Plan-2b-Ruling 6: Platz auf einem anderen Bildschirm als PMT; Arbeitsbereiche mit dem DPI-Faktor des Hauptfensters umgerechnet.</summary>
    private WindowBoundsState? OtherScreenBounds(double width, double height)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var screens = System.Windows.Forms.Screen.AllScreens
            .Select(s => new ScreenArea(
                s.WorkingArea.Left / dpi.DpiScaleX,
                s.WorkingArea.Top / dpi.DpiScaleY,
                s.WorkingArea.Width / dpi.DpiScaleX,
                s.WorkingArea.Height / dpi.DpiScaleY))
            .ToList();
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        return WindowPlacement.OnOtherScreen(screens, bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2), width, height);
    }

    /// <summary>Plan-2b-Ruling 13: GUI-Element in der Zeile der Datei im GUI-Inspektor zeigen; <see langword="false"/>, wenn das nicht geht.</summary>
    private bool InspectGuiAt(string path, int line)
    {
        if (!GuiSettings.ToolsEnabled)
        {
            return false;
        }

        GuiDocument document;
        try
        {
            document = GuiParser.Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        var resolver = GuiSettings.Workspace?.CreateResolver(document, path) ?? new GuiResolver(GuiIndex.Empty, GuiRules.Empty, document, path);
        if (GuiElementPath.ElementAtLine(document, line, resolver) is not { } node)
        {
            return false;
        }

        InspectGuiElement(new GuiInspectTarget(node, document, path));
        return true;
    }

    /// <summary>Plan-Ruling 12 (2a): Effekt oder Trigger der Engine laut Script-Datenbank; <see langword="null"/>, wenn der Name dort fehlt.</summary>
    private EngineCommandKind? EngineCommandKindOf(string name)
    {
        foreach (var entry in ScriptDocs.Database.Entries)
        {
            if (entry.Name != name)
            {
                continue;
            }

            if (entry.Kind == ScriptDocKind.Effect)
            {
                return EngineCommandKind.Effect;
            }

            if (entry.Kind == ScriptDocKind.Trigger)
            {
                return EngineCommandKind.Trigger;
            }
        }

        return null;
    }
}
