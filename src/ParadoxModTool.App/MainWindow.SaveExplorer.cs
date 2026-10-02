using System.Windows;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.SaveGames;

namespace ParadoxModTool.App;

/// <summary>
/// Spielstand-Explorer im Hauptfenster (Plan 2a): Menüpunkt, Dienst, Fenster. Der Dienst entsteht beim ersten Öffnen und lebt bis zum Beenden
/// von PMT oder bis zu einem Spielwechsel. Den Symbolindex liefert das Fehlerprotokoll.
/// </summary>
public partial class MainWindow
{
    private SaveExplorerService? _saveExplorer;
    private SaveExplorerWindow? _saveExplorerWindow;

    /// <summary>Block <c>saveGames</c> des aktuellen Spielprofils; null = Explorer für dieses Spiel nicht verfügbar.</summary>
    private SaveGameSettings? SaveExplorerSettings => (GameContext.Profile ?? _activeProfile)?.SaveGames;

    private void InitializeSaveExplorer() => SaveExplorerMenuItem.IsEnabled = SaveExplorerSettings is not null;

    private void OpenSaveExplorer_Click(object sender, RoutedEventArgs e) => OpenSaveExplorerWindow(null);

    /// <summary>Öffnet das Fenster "Spielstand-Explorer" (nur eines); mit Namen im Reiter "Variablen" auf diesen Namen gestellt.</summary>
    /// <summary>Scope-Verfolgung: Spielstand-Explorer auf einem Objekt aus dem Befund öffnen; <see langword="false"/>, wenn es dort nicht steht.</summary>
    private bool ShowSaveObject(string kind, string idOrTag)
    {
        OpenSaveExplorerWindow(null);
        return _saveExplorerWindow?.ShowObject(kind, idOrTag) == true;
    }

    private void OpenSaveExplorerWindow(string? name)
    {
        if (SaveExplorerServiceFor() is not { } service)
        {
            return;
        }

        service.Host.WantIndex();
        if (_saveExplorerWindow is null)
        {
            // Die Einstellung gilt ab dem nächsten Öffnen (wie beim Fehlerprotokoll).
            var owner = _settings.SaveExplorerStandalone ? null : this;
            _saveExplorerWindow = new SaveExplorerWindow(service, _settings, owner, OtherScreenBounds);
            _saveExplorerWindow.Closed += (_, _) => _saveExplorerWindow = null;
        }

        if (name is not null)
        {
            _saveExplorerWindow.ShowName(name);
        }

        if (!_saveExplorerWindow.IsVisible)
        {
            _saveExplorerWindow.Show();
            return;
        }

        if (_saveExplorerWindow.WindowState == WindowState.Minimized)
        {
            _saveExplorerWindow.WindowState = WindowState.Normal;
        }

        _saveExplorerWindow.Activate();
    }

    /// <summary>Dienst für das aktuelle Spiel; ein Dienst eines anderen Spiels wird mit seinem Fenster geschlossen.</summary>
    private SaveExplorerService? SaveExplorerServiceFor()
    {
        var gameId = GameContext.GameId;
        if (_saveExplorer is not null && _saveExplorer.Host.GameId != gameId)
        {
            CloseSaveExplorer();
        }

        if (_saveExplorer is not null)
        {
            return _saveExplorer;
        }

        var profile = GameContext.Profile ?? _activeProfile;
        if (profile?.SaveGames is not { } settings)
        {
            return null;
        }

        var host = new SaveExplorerHost(
            gameId,
            GameContext.DisplayNameOf(gameId),
            settings,
            SaveGameFolder.For(profile.DefaultModRootTemplate, settings),
            () => _errorLog is { Session.IndexReady: true } errorLog ? errorLog.Session.Index : null,
            () =>
            {
                if (profile.ErrorLogRules is not null)
                {
                    ErrorLogServiceFor().WantIndex();
                }
            },
            JumpToBookmarkInFile,
            () => _errorLog?.Session.NameSeverities().ToDictionary(p => p.Key, p => ErrorLogTexts.SeverityIcon(p.Value), StringComparer.Ordinal)
                ?? new Dictionary<string, string>(StringComparer.Ordinal),
            ShowNameInErrorLog);
        _saveExplorer = new SaveExplorerService(host);
        var session = _saveExplorer.Session;
        SaveView? shown = null;
        session.Changed += (_, _) =>
        {
            // Das Fehlerprotokoll zeigt den Befund des geladenen Stands; es baut nur bei einem anderen Stand neu auf.
            if (!ReferenceEquals(session.View, shown))
            {
                shown = session.View;
                _errorLog?.NotifySaveChanged();
            }
        };
        _saveExplorer.Start();
        return _saveExplorer;
    }

    /// <summary>
    /// "Mit Spielstand prüfen" im Fehlerprotokoll: lädt den neuesten lesbaren Stand über den Explorer-Dienst, ohne das Fenster zu öffnen.
    /// False, wenn es keinen lesbaren Stand gibt.
    /// </summary>
    private bool LoadNewestSaveForErrorLog()
    {
        if (SaveExplorerServiceFor() is not { } service)
        {
            return false;
        }

        if (service.Session.View is not null || service.Session.IsLoading)
        {
            return true;
        }

        if (service.Session.Saves.FirstOrDefault(s => s.Readable) is not { } newest)
        {
            return false;
        }

        _ = service.Session.LoadAsync(newest, null);
        return true;
    }

    /// <summary>"Im Fehlerprotokoll zeigen" im Explorer: Fehlerprotokoll öffnen und auf den Namen stellen.</summary>
    private void ShowNameInErrorLog(string name)
    {
        OpenErrorLogWindow(null);
        _errorLogWindow?.ShowName(name);
    }

    /// <summary>Vor dem Schreiben der Einstellungen: Position und Größe des Explorer-Fensters übernehmen.</summary>
    private void SaveSaveExplorerWindowBounds() => _saveExplorerWindow?.SaveBounds();

    /// <summary>Beim Beenden von PMT oder Spielwechsel: Fenster schließen, Ordnerbeobachtung und Ladeaufträge beenden.</summary>
    private void CloseSaveExplorer()
    {
        _saveExplorerWindow?.Close();
        _saveExplorerWindow = null;
        _saveExplorer?.Dispose();
        _saveExplorer = null;
    }
}
