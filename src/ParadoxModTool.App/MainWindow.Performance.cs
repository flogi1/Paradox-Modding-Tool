using System.IO;
using System.Windows;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.Performance;
using ParadoxModTool.SaveGames;
using ErrorLogLocations = ParadoxModTool.ErrorLog.ErrorLogLocations;

namespace ParadoxModTool.App;

/// <summary>
/// Performance-Checker im Hauptfenster (Plan 2): Menüpunkt und Kachel für jedes Spiel, gesperrt mit Begründung, wenn das Profil keinen nutzbaren
/// Profiler hat; das Fenster entsteht beim ersten Öffnen und lebt bis zum Beenden von PMT (ein Spielwechsel öffnet ein eigenes PMT-Fenster).
/// Der Namespace <c>ParadoxModTool.ErrorLog</c> wird hier nicht eingebunden, weil es wie <c>ParadoxModTool.Performance</c> eine Klasse
/// <c>RunComparison</c> hat.
/// </summary>
public partial class MainWindow
{
    private PerformanceWindow? _performanceWindow;

    private GameProfile? PerformanceProfile => GameContext.Profile ?? _activeProfile;

    /// <summary>Das Spielprofil hat einen nutzbaren Profiler (Dump-Befehl und Dateimuster gemessen).</summary>
    private bool PerformanceAvailable => PerformanceProfile?.Performance?.IsUsable == true;

    /// <summary>Begründung für den gesperrten Menüpunkt und die gesperrte Kachel; null, wenn der Performance-Checker verfügbar ist.</summary>
    private string? PerformanceDisabledTip => PerformanceAvailable
        ? null
        : PerformanceProfile?.Performance is null ? PerformanceTexts.NotAvailableNoProfiler : PerformanceTexts.NotAvailableLocked;

    private void OpenPerformance_Click(object sender, RoutedEventArgs e) => OpenPerformanceWindow();

    /// <summary>Öffnet das Fenster "Performance-Checker" (nur eines); eigenständig oder über PMT wie das Fehlerprotokoll.</summary>
    private void OpenPerformanceWindow()
    {
        if (!PerformanceAvailable || PerformanceProfile is not { } profile)
        {
            return;
        }

        if (_performanceWindow is null)
        {
            var owner = _settings.ErrorLogStandalone ? null : this;
            _performanceWindow = new PerformanceWindow(CreatePerformanceHost(profile), _settings, owner);
            _performanceWindow.Closed += (_, _) => _performanceWindow = null;
        }

        if (!_performanceWindow.IsVisible)
        {
            _performanceWindow.Show();
            return;
        }

        if (_performanceWindow.WindowState == WindowState.Minimized)
        {
            _performanceWindow.WindowState = WindowState.Normal;
        }

        _performanceWindow.Activate();
    }

    private PerformanceWindowHost CreatePerformanceHost(GameProfile profile)
    {
        var gameId = GameContext.GameId;
        return new PerformanceWindowHost(
            gameId,
            profile,
            () => _mods?.ActiveMod?.Paths.FirstOrDefault(Directory.Exists),
            () => _mods?.ActiveMod?.Id,
            GeneratorKnownMods,
            // Fragt nur auf dem UI-Thread nach der Spiel-Exe; Hintergrund-Threads (Herkunfts-Klassifizierer je Thread) bekommen dann null.
            () => Dispatcher.CheckAccess() && !EnsureGameExecutable() ? null : ComputeGameContentRoot(_gameExecutablePath),
            () => PerformanceLogFolder(profile),
            () => profile.SaveGames is { } saveGames ? SaveGameFolder.For(profile.DefaultModRootTemplate, saveGames) : null,
            () => ContinueGamePathFor(profile),
            () => _gameExecutablePath,
            () => PlaysetStore.ListPlaysets(_playsetsFilePath),
            StartGameForPerformance,
            () => GameProcess.IsRunning(_gameExecutablePath),
            () => _gameExecutablePath is { } exe && _debugLaunch.IsDebugSession(exe, GameProcess.RunningSince(exe)),
            OpenPatchFileAtLine,
            GameContext.Repository.GameDirectory(gameId),
            GameContext.Session.State,
            ImportRecipeFromPerformanceAi);
    }

    /// <summary>
    /// "Als Rezept importieren" aus der KI-Analyse des Performance-Checkers: Patch-Rezepte für die Mod öffnen, dann derselbe Weg wie die Antwort im
    /// Fenster "Rezept mit KI erstellen" (prüfen, Kennung klären, speichern, Erklärung und Vorschau). Nichts wird mitgegeben, ein vorhandenes Rezept
    /// wird nur nach Rückfrage ersetzt.
    /// </summary>
    private async Task<IReadOnlyList<string>> ImportRecipeFromPerformanceAi(string answer, string? modRoot)
    {
        OpenPatchRecipes(modRoot);
        if (_patchRecipesWindow is not { } window)
        {
            return Array.Empty<string>();
        }

        var outcome = await new RecipeAiFlavor(window, window.Host).ApplyAnswerAsync(window, answer, modRoot, false);
        return outcome.Messages;
    }

    /// <summary>Logs-Ordner des Spiels: der im Fehlerprotokoll gewählte, sonst der aus dem Spielprofil.</summary>
    private string? PerformanceLogFolder(GameProfile profile) =>
        _errorLogFolderOverride ?? ErrorLogLocations.LogFolderFor(profile.DefaultModRootTemplate);

    /// <summary>
    /// Datei, die den letzten Spielstand nennt: liegt im Nutzerordner des Spiels neben <c>logs</c>. Abgeleitet aus demselben Logs-Ordner wie
    /// <see cref="PerformanceLogFolder"/>, damit ein im Fehlerprotokoll gewählter Ordner für beide gilt.
    /// </summary>
    private string? ContinueGamePathFor(GameProfile profile) =>
        profile.Performance is { } settings
        && PerformanceLogFolder(profile) is { } logs
        && Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(logs)) is { Length: > 0 } userFolder
            ? Path.Combine(userFolder, settings.ContinueGameFile)
            : null;

    /// <summary>
    /// Start aus dem Performance-Checker mit eigenen Argumenten (Debug-Modus, ggf. Spielstand laden): mit Playset-Id über denselben Weg wie die
    /// Playset-Verwaltung; ohne Id wie "Spiel starten", aber ohne Playset-Wechsel. Beide merken den Start mit -debug_mode.
    /// </summary>
    private bool StartGameForPerformance(string? playsetId, string arguments) =>
        playsetId is null
            ? ConfirmUnsavedModFilesBeforeLaunch() && StartGame(arguments, activateSelectedPlayset: false)
            : LaunchWithPlayset(manager => manager.PrepareStart(playsetId), arguments);

    /// <summary>Vor dem Schreiben der Einstellungen: Position und Größe des Fensters übernehmen.</summary>
    private void SavePerformanceWindowBounds() => _performanceWindow?.SaveBounds();

    /// <summary>Beim Schließen von PMT: Fenster schließen.</summary>
    private void ClosePerformance()
    {
        _performanceWindow?.Close();
        _performanceWindow = null;
    }
}
