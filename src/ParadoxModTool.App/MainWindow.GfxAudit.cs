using System.IO;
using System.Windows;
using ParadoxModTool.GfxAudit;

namespace ParadoxModTool.App;

/// <summary>
/// GFX-Prüfung im Hauptfenster: Menüpunkt und Kachel (Sperre über <see cref="ApplyToolAvailability"/>); das Fenster entsteht beim ersten
/// Öffnen und lebt, bis es geschlossen wird. Erneutes Öffnen liest geänderte Dateien neu ein.
/// </summary>
public partial class MainWindow
{
    private GfxAuditWindow? _gfxAuditWindow;

    private GfxAuditRules? _gfxAuditRules;
    private bool _gfxAuditRulesRead;

    /// <summary>Für dieses Spiel gibt es Regeln (ein PMT-Fenster gehört zu einem Spiel: einmal gelesen).</summary>
    private bool GfxAuditAvailable => GfxAuditRulesOfGame() is not null;

    private GfxAuditRules? GfxAuditRulesOfGame()
    {
        if (!_gfxAuditRulesRead && (GameContext.Profile ?? _activeProfile) is { } profile)
        {
            _gfxAuditRulesRead = true;
            try
            {
                _gfxAuditRules = GfxAuditRules.ForProfile(profile, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                // Eine kaputte Regeldatei sperrt nur dieses Werkzeug.
                _gfxAuditRules = null;
            }
        }

        return _gfxAuditRules;
    }

    /// <summary>Beim Start: Ein Playset-Wechsel erreicht ein offenes Fenster.</summary>
    private void InitializeGfxAudit()
    {
        PlaysetsCombo.SelectionChanged += (_, _) => _gfxAuditWindow?.PlaysetChanged();
    }

    private void OpenGfxAudit_Click(object sender, RoutedEventArgs e) => OpenGfxAuditWindow();

    /// <summary>Öffnet das Fenster "GFX-Prüfung" (nur eines); ein schon offenes liest geänderte Dateien neu, wenn gerade kein Aufbau läuft.</summary>
    private void OpenGfxAuditWindow()
    {
        if (GfxAuditRulesOfGame() is not { } rules || (GameContext.Profile ?? _activeProfile) is not { } profile)
        {
            return;
        }

        if (_gfxAuditWindow is null)
        {
            var owner = _settings.ErrorLogStandalone ? null : this;
            _gfxAuditWindow = new GfxAuditWindow(CreateGfxAuditHost(profile, rules), _settings, owner);
            _gfxAuditWindow.Closed += (_, _) => _gfxAuditWindow = null;
            _gfxAuditWindow.Show();
            return;
        }

        if (_gfxAuditWindow.WindowState == WindowState.Minimized)
        {
            _gfxAuditWindow.WindowState = WindowState.Normal;
        }

        _gfxAuditWindow.Activate();
        _gfxAuditWindow.RefreshIfIdle();
    }

    private GfxAuditWindowHost CreateGfxAuditHost(ParadoxModTool.GameProfiles.GameProfile profile, GfxAuditRules rules)
    {
        var gameId = GameContext.GameId;
        var mods = new ModSourceCollector(
            () => _mods?.Registry,
            _mods?.MetadataFile ?? profile.MetadataFile ?? string.Empty,
            profile.SteamAppId,
            profile.DefaultModRootTemplate,
            () => _playsetsFilePath);
        return new GfxAuditWindowHost(
            gameId,
            profile,
            rules,
            () => _mods?.ActiveMod?.Paths.FirstOrDefault(Directory.Exists),
            mods,
            () => ParadoxModTool.Mods.GameContentRoot.InstallRoot(_gameExecutablePath),
            () => ComputeGameContentRoot(_gameExecutablePath),
            OpenPatchFileAtLine,
            GameContext.Repository.GameDirectory(gameId),
            () => (PlaysetsCombo.SelectedItem as ParadoxModTool.LaunchControl.PlaysetInfo)?.Name,
            string.IsNullOrWhiteSpace(profile.DefaultModRootTemplate) ? null : Environment.ExpandEnvironmentVariables(profile.DefaultModRootTemplate));
    }

    /// <summary>Vor dem Schreiben der Einstellungen: Position und Größe des Fensters übernehmen.</summary>
    private void SaveGfxAuditWindowBounds() => _gfxAuditWindow?.SaveBounds();

    /// <summary>Beim Schließen von PMT: Fenster schließen (bricht einen laufenden Aufbau ab).</summary>
    private void CloseGfxAudit()
    {
        _gfxAuditWindow?.Close();
        _gfxAuditWindow = null;
    }
}
