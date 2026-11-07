using System.IO;
using System.Windows;
using ParadoxModTool.Editor;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptDocs;

namespace ParadoxModTool.App;

/// <summary>
/// Flag-Tracker im Hauptfenster (Plan 2): Menüpunkt für jedes Spiel, gesperrt mit Begründung, wo es keine Befehlstabelle gibt (heute haben
/// alle sechs Spiele eine); das Fenster entsteht beim ersten Öffnen und lebt, bis es geschlossen wird. Erneutes Öffnen liest geänderte Dateien neu ein.
/// </summary>
public partial class MainWindow
{
    private FlagTrackerWindow? _flagTrackerWindow;

    /// <summary>Für dieses Spiel gibt es eine Befehlstabelle.</summary>
    private bool FlagTrackerAvailable => FlagCommandTable.For(GameContext.GameId) is not null;

    /// <summary>Beim Start: neue ScriptDocs-Daten bauen ein offenes Verzeichnis neu (Sperre des Menüpunkts: <see cref="ApplyToolAvailability"/>).</summary>
    private void InitializeFlagTracker()
    {
        // Die Flag-Familien von CK III kommen auch aus den ScriptDocs: Der Fingerabdruck der Tabelle ändert sich, die Bibliothek baut neu.
        ScriptDocs.DatabaseChanged += (_, _) => _flagTrackerWindow?.RestartIfCommandTableChanged();
        PlaysetsCombo.SelectionChanged += (_, _) => _flagTrackerWindow?.PlaysetChanged();
    }

    /// <summary>Kontextmenü "Im Flag-Tracker suchen" beider Arbeitsbereiche; der Editor kennt die Flag-Bibliothek nicht, das Hauptfenster liefert Abfrage und Text.</summary>
    private void WireFlagTrackerEditor(EditorPane pane)
    {
        pane.FlagTrackerOffered = () => FlagTrackerAvailable;
        pane.FlagQueryAt = FlagQuery.FromEditor;
        pane.FlagTrackerHeader = FlagTrackerTexts.ContextSearch;
        pane.FlagTrackerRequested += (_, word) => OpenFlagTrackerWindow(word);
    }

    private void OpenFlagTracker_Click(object sender, RoutedEventArgs e) => OpenFlagTrackerWindow();

    /// <summary>
    /// Öffnet das Fenster "Flag-Tracker" (nur eines); eigenständig oder über PMT wie das Fehlerprotokoll. Ein schon offenes Fenster liest
    /// geänderte Dateien neu, wenn gerade kein Aufbau läuft. Mit <paramref name="query"/> danach sofort suchen.
    /// </summary>
    private void OpenFlagTrackerWindow(string? query = null)
    {
        if (!FlagTrackerAvailable || (GameContext.Profile ?? _activeProfile) is not { } profile)
        {
            return;
        }

        if (_flagTrackerWindow is null)
        {
            var owner = _settings.ErrorLogStandalone ? null : this;
            _flagTrackerWindow = new FlagTrackerWindow(CreateFlagTrackerHost(profile), _sharedSessions, _settings, owner);
            _flagTrackerWindow.Closed += (_, _) => _flagTrackerWindow = null;
            _flagTrackerWindow.Show();
        }
        else
        {
            if (_flagTrackerWindow.WindowState == WindowState.Minimized)
            {
                _flagTrackerWindow.WindowState = WindowState.Normal;
            }

            _flagTrackerWindow.Activate();
            _flagTrackerWindow.RefreshIfIdle();
        }

        if (query is not null)
        {
            _flagTrackerWindow.Search(query);
        }
    }

    private FlagTrackerWindowHost CreateFlagTrackerHost(ParadoxModTool.GameProfiles.GameProfile profile)
    {
        var gameId = GameContext.GameId;
        var mods = new ModSourceCollector(
            () => _mods?.Registry,
            _mods?.MetadataFile ?? profile.MetadataFile ?? string.Empty,
            profile.SteamAppId,
            profile.DefaultModRootTemplate,
            () => _playsetsFilePath);
        return new FlagTrackerWindowHost(
            gameId,
            profile,
            () => _mods?.ActiveMod?.Paths.FirstOrDefault(Directory.Exists),
            mods,
            () => ParadoxModTool.Mods.GameContentRoot.InstallRoot(_gameExecutablePath),
            () => ComputeGameContentRoot(_gameExecutablePath),
            FlagTrackerScriptDocNames,
            OpenPatchFileAtLine,
            GameContext.Repository.GameDirectory(gameId),
            () => (PlaysetsCombo.SelectedItem as ParadoxModTool.LaunchControl.PlaysetInfo)?.Name);
    }

    /// <summary>Namen der nicht veralteten Effekte und Trigger aus der Script-Datenbank; leer, solange nichts importiert ist.</summary>
    private IReadOnlyList<string> FlagTrackerScriptDocNames()
    {
        var database = ScriptDocs.Database;
        return database.Entries
            .Where(entry => entry.Kind is ScriptDocKind.Effect or ScriptDocKind.Trigger && !database.IsOutdated(entry))
            .Select(entry => entry.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Vor dem Schreiben der Einstellungen: Position und Größe des Fensters übernehmen.</summary>
    private void SaveFlagTrackerWindowBounds() => _flagTrackerWindow?.SaveBounds();

    /// <summary>Beim Schließen von PMT: Fenster schließen (bricht einen laufenden Aufbau ab).</summary>
    private void CloseFlagTracker()
    {
        _flagTrackerWindow?.Close();
        _flagTrackerWindow = null;
    }
}
