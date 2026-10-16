using System.IO;
using System.Windows;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.Localization;
using ParadoxModTool.Mods;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Playset-Verwaltung im Hauptfenster: Fenster öffnen, das Spiel mit einem Playset starten und der Schnellstart mit einer Testmod.</summary>
public partial class MainWindow
{
    private PlaysetsWindow? _playsetsWindow;

    private void OpenPlaysets_Click(object sender, RoutedEventArgs e)
    {
        if (_playsetsWindow is null)
        {
            _playsetsWindow = new PlaysetsWindow(new PlaysetsHost(
                CreatePlaysetManager,
                () => (PlaysetsCombo.SelectedItem as PlaysetInfo)?.Name,
                SelectPlaysetInCombo,
                CanStartPlayset,
                StartPlayset,
                EnsureGameExecutable,
                RefreshPlaysetsCombo))
            {
                Owner = this,
            };
            _playsetsWindow.Closed += (_, _) => _playsetsWindow = null;
        }

        if (_playsetsWindow.IsVisible)
        {
            _playsetsWindow.Activate();
        }
        else
        {
            _playsetsWindow.Show();
        }
    }

    /// <summary>Verwaltung für die eingestellte Playset-Datei; die Lade-Datei kommt aus dem Spielprofil. Null, wenn keine Datei gefunden wurde.</summary>
    private PlaysetManager? CreatePlaysetManager() =>
        PlaysetManagerFactory.Create(_playsetsFilePath, (GameContext.Profile ?? _activeProfile)?.PlaysetLoadFile, _gameExecutablePath);

    /// <summary>Ob das Spiel mit einem Playset aus PMT startbar ist: das Spiel liest die Playsets selbst (EU V) oder das Profil nennt die Lade-Datei.</summary>
    private bool CanStartPlayset() => CreatePlaysetManager() is { } manager && (!manager.UsesLauncherDatabase || manager.LoadFilePath is not null);

    /// <summary>Ob die Spiel-.exe bekannt ist; sonst wird danach gefragt. Ohne sie sieht PMT nicht, ob das Spiel läuft, und schreibt deshalb nichts.</summary>
    private bool EnsureGameExecutable() => (_gameExecutablePath is { } path && File.Exists(path)) || PromptForGameExecutablePath();

    private void SelectPlaysetInCombo(string name)
    {
        if (PlaysetsCombo.ItemsSource is IEnumerable<PlaysetInfo> items && items.FirstOrDefault(p => p.Name == name) is { } match)
        {
            PlaysetsCombo.SelectedItem = match;
        }
    }

    /// <summary>
    /// Startet das Spiel mit dem Playset der Id: Spiel-.exe erfragen, ungespeicherte Mod-Dateien melden, Playset aktiv setzen und Lade-Datei schreiben, dann
    /// mit der in der Werkzeugleiste gewählten Startoption starten. Bei einem Fehler startet nichts.
    /// </summary>
    private bool StartPlayset(string playsetId) => LaunchWithPlayset(manager => manager.PrepareStart(playsetId));

    /// <summary>Gemeinsamer Start: <paramref name="prepare"/> bereitet die Dateien vor (aktiv setzen, Lade-Datei); erst bei Erfolg wird das Spiel gestartet.</summary>
    private bool LaunchWithPlayset(Func<PlaysetManager, PlaysetResult> prepare)
    {
        if (!ConfirmUnsavedModFilesBeforeLaunch())
        {
            return false;
        }

        if ((_gameExecutablePath is null || !File.Exists(_gameExecutablePath)) && !PromptForGameExecutablePath())
        {
            return false;
        }

        if (CreatePlaysetManager() is not { } manager)
        {
            MessageBox.Show(this, PlaysetWindowTexts.NoFile, MainTexts.LaunchPlaysetFailed, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var prepared = prepare(manager);
        RefreshPlaysetsCombo();
        if (!prepared.Success)
        {
            MessageBox.Show(this, PlaysetTexts.Describe(prepared), MainTexts.LaunchPlaysetFailed, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var arguments = (LaunchOptionsCombo.SelectedItem as LaunchOption)?.Args ?? string.Empty;
        var result = GameLauncher.Launch(_gameExecutablePath, arguments);
        if (!result.Success)
        {
            MessageBox.Show(this, PlaysetWindowTexts.StartFailed(result.ErrorMessage ?? string.Empty), MainTexts.LaunchFailed, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _debugLaunch.Launched(_gameExecutablePath!, arguments, DateTime.UtcNow);
        return true;
    }

    private PlaysetQuickActions PlaysetQuick() => new(CanStartPlayset, QuickStartWithMod, QuickNewPlaysetWithMod);

    /// <summary>Playset und Mod-Eintrag für den Schnellstart; zeigt eine Meldung und liefert null, wenn etwas fehlt.</summary>
    private (PlaysetManager Manager, PlaysetData Playset, PlaysetModEntry Mod)? QuickContext(ModInfo mod)
    {
        if (CreatePlaysetManager() is not { } manager)
        {
            MessageBox.Show(this, PlaysetWindowTexts.NoFile, PlaysetWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        var selected = PlaysetsCombo.SelectedItem as PlaysetInfo;
        var all = manager.LoadAll();
        var playset = (selected?.Id is { } selectedId ? all.FirstOrDefault(p => p.Id == selectedId) : null) ?? all.FirstOrDefault(p => p.Name == selected?.Name);
        if (playset is null)
        {
            MessageBox.Show(this, PlaysetWindowTexts.QuickNoPlayset, PlaysetWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        var folder = BranchTransfer.WorkingFolder(mod);
        if (folder is null)
        {
            MessageBox.Show(this, ModsTexts.NoExistingFolder(mod.Name), PlaysetWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        return (manager, playset, new PlaysetModEntry(folder, mod.Name, true, null));
    }

    /// <summary>
    /// "Mit aktuellem Playset plus dieser Mod starten": bei Launcher-Spielen wird nur die Lade-Datei aus einer Kopie im Speicher geschrieben (nichts gespeichert);
    /// EU V liest die Playsets selbst, deshalb legt PMT dort nach Rückfrage das Playset "aktuelles + Mod" an und startet damit.
    /// </summary>
    private void QuickStartWithMod(ModInfo mod)
    {
        if (QuickContext(mod) is not var (manager, playset, entry))
        {
            return;
        }

        if (manager.LoadFilePath is not null)
        {
            LaunchWithPlayset(m => m.PrepareStartWithExtraMod(playset.Id, entry));
            return;
        }

        var name = FreePlaysetName(manager, $"{playset.Name} + {mod.Name}");
        if (MessageBox.Show(this, PlaysetWindowTexts.QuickJsonConfirm(name, mod.Name), PlaysetWindowTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        CreateWithModAndStart(manager, playset, entry, name, start: true);
    }

    /// <summary>"Neues Playset aus aktuellem Playset plus dieser Mod": legt es an (es bleibt stehen) und startet auf Wunsch gleich damit.</summary>
    private void QuickNewPlaysetWithMod(ModInfo mod)
    {
        if (QuickContext(mod) is not var (manager, playset, entry))
        {
            return;
        }

        var suggestion = FreePlaysetName(manager, $"{playset.Name} + {mod.Name}");
        if (TextPromptWindow.Ask(this, PlaysetWindowTexts.Title, PlaysetWindowTexts.QuickNewName(mod.Name), suggestion)?.Trim() is not { Length: > 0 } name)
        {
            return;
        }

        CreateWithModAndStart(manager, playset, entry, name, start: false);
    }

    private void CreateWithModAndStart(PlaysetManager manager, PlaysetData playset, PlaysetModEntry entry, string name, bool start)
    {
        if (!EnsureGameExecutable())
        {
            return;
        }

        manager = CreatePlaysetManager() ?? manager;
        var created = manager.WithExtraMod(playset.Id, entry, name);
        if (!created.Success)
        {
            MessageBox.Show(this, PlaysetTexts.Describe(created), PlaysetWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RefreshPlaysetsCombo();
        SelectPlaysetInCombo(name);
        _playsetsWindow?.Reload(selectName: name);
        var id = manager.LoadAll().FirstOrDefault(p => p.Name == name)?.Id;
        if (id is null || !CanStartPlayset())
        {
            return;
        }

        if (start || MessageBox.Show(this, PlaysetWindowTexts.Start, PlaysetWindowTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            StartPlayset(id);
        }
    }

    /// <summary>Der Name, falls frei, sonst mit Zähler dahinter.</summary>
    private static string FreePlaysetName(PlaysetManager manager, string wanted)
    {
        var existing = manager.LoadAll().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = wanted;
        for (var counter = 2; existing.Contains(name); counter++)
        {
            name = $"{wanted} {counter}";
        }

        return name;
    }
}
