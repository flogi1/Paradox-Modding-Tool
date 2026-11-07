using System.IO;
using System.Windows;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.App;

/// <summary>
/// Skript-Prüfung im Hauptfenster: Menüpunkt und Kachel (Sperre über <see cref="ApplyToolAvailability"/>); das Fenster entsteht beim ersten
/// Öffnen und lebt, bis es geschlossen wird. Erneutes Öffnen liest geänderte Dateien neu ein.
/// </summary>
public partial class MainWindow
{
    private ScriptCheckWindow? _scriptCheckWindow;

    private ScriptCheckRules? _scriptCheckRules;
    private bool _scriptCheckRulesRead;

    /// <summary>Für dieses Spiel gibt es Regeln (ein PMT-Fenster gehört zu einem Spiel: einmal gelesen).</summary>
    private bool ScriptCheckAvailable => ScriptCheckRulesOfGame() is not null;

    private ScriptCheckRules? ScriptCheckRulesOfGame()
    {
        if (!_scriptCheckRulesRead && (GameContext.Profile ?? _activeProfile) is { } profile)
        {
            _scriptCheckRulesRead = true;
            try
            {
                _scriptCheckRules = ScriptCheckRules.ForProfile(profile, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                // Eine kaputte Regeldatei sperrt nur dieses Werkzeug.
                _scriptCheckRules = null;
            }
        }

        return _scriptCheckRules;
    }

    /// <summary>Beim Start: Playset und Script-Doku erreichen ein offenes Fenster.</summary>
    private void InitializeScriptCheck()
    {
        ScriptDocs.DatabaseChanged += (_, _) => _scriptCheckWindow?.ScriptDocsChanged();
        PlaysetsCombo.SelectionChanged += (_, _) => _scriptCheckWindow?.PlaysetChanged();
    }

    private void OpenScriptCheck_Click(object sender, RoutedEventArgs e) => OpenScriptCheckWindow();

    /// <summary>Öffnet das Fenster "Skript-Prüfung" (nur eines); ein schon offenes liest geänderte Dateien neu, wenn gerade kein Aufbau läuft.</summary>
    private void OpenScriptCheckWindow()
    {
        if (ScriptCheckRulesOfGame() is not { } rules || (GameContext.Profile ?? _activeProfile) is not { } profile)
        {
            return;
        }

        if (_scriptCheckWindow is null)
        {
            var owner = _settings.ErrorLogStandalone ? null : this;
            _scriptCheckWindow = new ScriptCheckWindow(CreateScriptCheckHost(profile, rules), _sharedSessions, _settings, owner);
            _scriptCheckWindow.Closed += (_, _) => _scriptCheckWindow = null;
            _scriptCheckWindow.Show();
            return;
        }

        if (_scriptCheckWindow.WindowState == WindowState.Minimized)
        {
            _scriptCheckWindow.WindowState = WindowState.Normal;
        }

        _scriptCheckWindow.Activate();
        _scriptCheckWindow.RefreshIfIdle();
    }

    private ScriptCheckWindowHost CreateScriptCheckHost(ParadoxModTool.GameProfiles.GameProfile profile, ScriptCheckRules rules)
    {
        var gameId = GameContext.GameId;
        var mods = new ModSourceCollector(
            () => _mods?.Registry,
            _mods?.MetadataFile ?? profile.MetadataFile ?? string.Empty,
            profile.SteamAppId,
            profile.DefaultModRootTemplate,
            () => _playsetsFilePath);
        return new ScriptCheckWindowHost(
            gameId,
            profile,
            rules,
            () => _mods?.ActiveMod?.Paths.FirstOrDefault(Directory.Exists),
            mods,
            () => ParadoxModTool.Mods.GameContentRoot.InstallRoot(_gameExecutablePath),
            () => ComputeGameContentRoot(_gameExecutablePath),
            () => ScriptDocs.Database,
            OpenPatchFileAtLine,
            GameContext.Repository.GameDirectory(gameId),
            () => (PlaysetsCombo.SelectedItem as ParadoxModTool.LaunchControl.PlaysetInfo)?.Name);
    }

    /// <summary>Vor dem Schreiben der Einstellungen: Position und Größe des Fensters übernehmen.</summary>
    private void SaveScriptCheckWindowBounds() => _scriptCheckWindow?.SaveBounds();

    /// <summary>Beim Schließen von PMT: Fenster schließen (bricht einen laufenden Aufbau ab).</summary>
    private void CloseScriptCheck()
    {
        _scriptCheckWindow?.Close();
        _scriptCheckWindow = null;
    }
}
