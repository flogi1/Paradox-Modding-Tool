using System.IO;
using System.Windows;
using ParadoxModTool.Categories;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.Editor;
using ParadoxModTool.Editor.Gui;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Spiel-Anbindung (Plan 4): Pipe-Dienst für Dateien aus dem Spiel, "Im Spiel ansehen" und "PMT als Editor eintragen".
/// </summary>
public partial class MainWindow
{
    private readonly DebugLaunchTracker _debugLaunch = new();
    private readonly NewFileTracker _newModFiles = new();
    private CancellationTokenSource? _handOffServer;

    /// <summary>Datei aus dem Spiel, die dieses Fenster nach dem Laden öffnet (Start als externer Editor).</summary>
    internal EditorRequest? PendingEditorRequest { get; set; }

    private static string HandOffLogPath => EditorHandOffStartup.LogPath(GameContext.Repository);

    /// <summary>Startet den Pipe-Dienst dieses Spiels und öffnet eine beim Start übergebene Datei.</summary>
    private void StartEditorHandOff()
    {
        // Loaded kann mehrfach kommen; ein zweiter Dienst mit demselben Namen liefe nur im Fehler-Takt.
        if (_handOffServer is not null)
        {
            return;
        }

        _handOffServer = new CancellationTokenSource();
        var stop = _handOffServer.Token;
        _ = Task.Run(() => EditorHandOffPipe.ServeAsync(
            GameContext.GameId,
            request => Dispatcher.BeginInvoke(() => OpenEditorRequest(request)),
            stop));
        if (PendingEditorRequest is { } pending)
        {
            PendingEditorRequest = null;
            OpenEditorRequest(pending);
        }
    }

    private void StopEditorHandOff() => _handOffServer?.Cancel();

    /// <summary>
    /// Datei aus dem Spiel (Plan 4 Ruling 4): Fenster nach vorn, Datei an der Zeile öffnen; bei einer .gui-Datei mit Zeile und fertigem GUI-Index das
    /// Element dort im Inspektor.
    /// </summary>
    private void OpenEditorRequest(EditorRequest request)
    {
        if (request.Kind != HandOffKind.OpenFile)
        {
            RunModCommand(request);
            return;
        }

        HandOffLog.Append(HandOffLogPath, $"open: {request.Path}, line: {request.Line?.ToString() ?? "-"}", DateTime.Now);
        if (!File.Exists(request.Path))
        {
            MessageBox.Show(this, GuiTexts.HandOffMissing(request.Path), GuiTexts.GameEditorTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        try
        {
            JumpToBookmarkInFile(request.Path, request.Line ?? 1);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Gesperrt oder ohne Leserecht: Meldung statt Absturz (der Aufruf kommt aus Dispatcher.BeginInvoke).
            MessageBox.Show(this, GuiTexts.HandOffReadFailed(request.Path, ex.Message), GuiTexts.GameEditorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (request.Line is not { } line || !request.Path.EndsWith(".gui", StringComparison.OrdinalIgnoreCase) || GuiSettings.Workspace is not { } workspace)
        {
            return;
        }

        var file = workspace.Index.Files.FirstOrDefault(f => EditorPane.PathsEqual(f.FullPath, request.Path));
        GuiDocument document;
        try
        {
            document = file is not null ? workspace.Index.DocumentOf(file) : GuiParser.Parse(File.ReadAllText(request.Path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        var resolver = file is not null ? workspace.CreateResolver() : workspace.CreateResolver(document, request.Path);
        if (GuiElementPath.ElementAtLine(document, line, resolver) is { } node)
        {
            InspectGuiElement(new GuiInspectTarget(node, document, request.Path));
        }
    }

    /// <summary>
    /// "Im Spiel ansehen" (Spec, Plan 4 Ruling 7 und 8): Mod-Dateien speichern, dann starten (mit -debug_mode). Läuft das Spiel: Hinweis auf nötigen
    /// Neustart, wenn PMT seit dem Spielstart Dateien angelegt hat, sonst der neutrale Hinweis, wenn PMT es nicht selbst im Debug-Modus gestartet hat.
    /// </summary>
    private void ViewInGame()
    {
        var running = GameProcess.RunningSince(_gameExecutablePath);
        if (!SaveModDocumentsForGame())
        {
            var text = running.Count > 0 ? GuiTexts.ViewInGameUnsavedRunning : GuiTexts.ViewInGameUnsaved;
            MessageBox.Show(this, text, GuiTexts.ViewInGame, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (running.Count > 0)
        {
            // Neu angelegte Dateien lädt das Spiel erst nach einem Neustart (Spec-Testpunkt 1, Rückmeldung des Nutzers).
            if (_newModFiles.NeedRestart(running, File.Exists) is { Count: > 0 } newFiles)
            {
                MessageBox.Show(
                    this, GuiTexts.ViewInGameRestartNeeded(string.Join(", ", newFiles.Select(Path.GetFileName))), GuiTexts.ViewInGame, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (!_debugLaunch.IsDebugSession(_gameExecutablePath!, running))
            {
                MessageBox.Show(this, GuiTexts.ViewInGameRunningUnknown, GuiTexts.ViewInGame, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return;
        }

        _debugLaunch.Forget();
        StartGame(DebugLaunchTracker.DebugArguments(_activeProfile?.LaunchOptions.Select(o => o.Args) ?? Enumerable.Empty<string>()) ?? "-debug_mode");
    }

    /// <summary>
    /// Speichert alle geänderten Mod-Dateien ohne Rückfrage (Spec Schritt 1); ein Vanilla-Entwurf öffnet dabei "In Mod übernehmen". False, wenn danach
    /// noch eine davon ungespeichert ist (abgebrochen). Entwürfe kommen zuletzt: "In Mod übernehmen" sperrt sich, solange ein geändertes Ziel offen ist.
    /// </summary>
    private bool SaveModDocumentsForGame()
    {
        var registry = _mods?.Registry;
        var targets = AllPanes
            .SelectMany(pane => pane.Documents.Select(document => (Pane: pane, Document: document)))
            .Where(x => x.Document.IsModified
                && (x.Document.IsGuiDraft
                    || (x.Document.FilePath is { } path && (x.Document.Origin == FileOrigin.Mod || registry?.Resolve(path) is not null))))
            .OrderBy(x => x.Document.IsGuiDraft)
            .ToList();
        foreach (var (pane, document) in targets)
        {
            pane.ActivateDocument(document);
            SaveActiveDocumentOf(pane);
            if (document.IsModified)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// pdx_settings.json im Dokumentenordner des Spiels (Plan 4 Ruling 6): zuerst der Ordner der eingestellten playsets.json (stimmt auch bei nach
    /// OneDrive umgeleiteten Dokumenten), sonst der Elternordner des Standard-Mod-Ordners; null ohne beides.
    /// </summary>
    private string? PdxSettingsPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_playsetsFilePath) && Path.GetDirectoryName(_playsetsFilePath) is { Length: > 0 } playsetsFolder)
            {
                return Path.Combine(playsetsFolder, "pdx_settings.json");
            }

            return _activeProfile is { DefaultModRootTemplate.Length: > 0 } profile
                && Path.GetDirectoryName(Environment.ExpandEnvironmentVariables(profile.DefaultModRootTemplate).Replace('/', '\\').TrimEnd('\\')) is { } documents
                    ? Path.Combine(documents, "pdx_settings.json")
                    : null;
        }
    }

    private string GameEditorStateDirectory => GameContext.Repository.GameDirectory(GameContext.GameId);

    private string GameEditorStatusText()
    {
        if (PdxSettingsPath is not { } path)
        {
            return GuiTexts.GameEditorNoPath;
        }

        var current = GameEditorRegistration.CurrentEditor(path);
        return current.Problem switch
        {
            PdxSettingsProblem.Missing => GuiTexts.GameEditorUnavailable(path),
            PdxSettingsProblem.UnknownFormat => GuiTexts.GameEditorUnknownFormat(path),
            PdxSettingsProblem.Locked => GuiTexts.GameEditorLocked(path),
            _ when current.Editor is null => GuiTexts.GameEditorStatusNone,
            _ when GameEditorRegistration.SamePath(current.Editor, Environment.ProcessPath) => GuiTexts.GameEditorStatusPmt,
            _ => GuiTexts.GameEditorStatus(current.Editor),
        };
    }

    /// <summary>"PMT als Editor eintragen" (Spec, Plan 4 Ruling 5): nicht bei laufendem Spiel, nicht ohne Spiel-Exe, nicht unter dotnet.</summary>
    private void RegisterGameEditor()
    {
        if (PdxSettingsPath is not { } path)
        {
            return;
        }

        var pmt = Environment.ProcessPath;
        string? blocked = null;
        if (pmt is null || string.Equals(Path.GetFileNameWithoutExtension(pmt), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            blocked = GuiTexts.GameEditorDevHost;
        }
        else if (string.IsNullOrWhiteSpace(_gameExecutablePath))
        {
            blocked = GuiTexts.GameEditorNoExe;
        }
        else if (GameProcess.RunningSince(_gameExecutablePath).Count > 0)
        {
            blocked = GuiTexts.GameEditorGameRunning;
        }

        if (blocked is not null)
        {
            MessageBox.Show(this, blocked, GuiTexts.GameEditorTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ShowGameEditorResult(GameEditorRegistration.Register(path, pmt!, GameEditorStateDirectory));
    }

    private void RestoreGameEditor()
    {
        if (PdxSettingsPath is not { } path)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_gameExecutablePath) && GameProcess.RunningSince(_gameExecutablePath).Count > 0)
        {
            MessageBox.Show(this, GuiTexts.GameEditorGameRunning, GuiTexts.GameEditorTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ShowGameEditorResult(GameEditorRegistration.Restore(path, Environment.ProcessPath ?? string.Empty, GameEditorStateDirectory));
    }

    private void ShowGameEditorResult(GameEditorResult result)
    {
        var text = result.Outcome switch
        {
            GameEditorOutcome.Registered => GuiTexts.GameEditorRegistered,
            GameEditorOutcome.AlreadyRegistered => GuiTexts.GameEditorAlreadyRegistered,
            GameEditorOutcome.Restored => result.Detail is { } old ? GuiTexts.GameEditorRestored(old) : GuiTexts.GameEditorRestoredNone,
            GameEditorOutcome.NothingToRestore => GuiTexts.GameEditorNothingToRestore,
            GameEditorOutcome.EditorChangedSince => GuiTexts.GameEditorChangedSince(result.Detail ?? string.Empty),
            GameEditorOutcome.SettingsMissing => GuiTexts.GameEditorUnavailable(result.Detail ?? string.Empty),
            GameEditorOutcome.UnknownFormat => GuiTexts.GameEditorUnknownFormat(result.Detail ?? string.Empty),
            GameEditorOutcome.Locked => GuiTexts.GameEditorLocked(result.Detail ?? string.Empty),
            _ => GuiTexts.GameEditorWriteFailed(result.Detail ?? string.Empty),
        };
        var failed = result.Outcome is GameEditorOutcome.UnknownFormat or GameEditorOutcome.WriteFailed or GameEditorOutcome.EditorChangedSince or GameEditorOutcome.Locked;
        MessageBox.Show(this, text, GuiTexts.GameEditorTitle, MessageBoxButton.OK, failed ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }
}
