using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.Editor;
using ParadoxModTool.Mods;
using System.Windows.Controls.Primitives;
using ParadoxModTool.Editor.Controls;
using ParadoxModTool.Settings;
using Controls = System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Mod-Verwaltung im Hauptfenster: aktive Mod erkennen, in der Statusleiste anzeigen, unbekannte Mod-Ordner anbieten, Fenster "Mods…" öffnen.</summary>
public partial class MainWindow
{
    private const string FallbackMetadataFile = ".metadata/metadata.json";

    private ModService? _mods;
    private ModsWindow? _modsWindow;
    private ModCompatWindow? _compatWindow;
    private readonly HashSet<string> _askedModFolders = new(StringComparer.OrdinalIgnoreCase);
    private bool _modPromptOpen;

    /// <summary>Der Arbeitsbereich, dessen aktive Datei die Mod bestimmt: der zuletzt fokussierte oder zuletzt gewechselte.</summary>
    private EditorPane? _modSourcePane;

    private void InitializeMods()
    {
        _mods = new ModService(
            GameContext.Repository.GameDirectory(GameContext.GameId),
            GameContext.Profile?.MetadataFile is { Length: > 0 } metadataFile ? metadataFile : FallbackMetadataFile);

        foreach (var pane in AllPanes)
        {
            var current = pane;
            current.ActiveDocumentChanged += (_, _) =>
            {
                _modSourcePane = current;
                RefreshActiveMod();
            };
            current.GotKeyboardFocus += (_, _) =>
            {
                if (!ReferenceEquals(_modSourcePane, current))
                {
                    _modSourcePane = current;
                    RefreshActiveMod();
                }
            };
        }

        // Feste Wahl aus der Sitzung; beim ersten Start mit dieser Funktion wird die gerade erkannte Mod fest gewählt.
        _mods.Choice = _activeModChoice;
        RefreshActiveMod();
        if (_activeModChoice is null)
        {
            SetActiveModChoice(ActiveModChoice.Initial(null, _mods.ActiveMod));
        }
    }

    /// <summary>Gespeicherte Wahl der aktiven Mod (Mod-Id oder <see cref="ActiveModChoice.Automatic"/>), siehe <see cref="AppSessionState.ActiveModChoice"/>.</summary>
    private string? _activeModChoice;

    /// <summary>Wählt die aktive Mod fest (Mod-Id) oder "Automatisch" und erkennt neu.</summary>
    private void SetActiveModChoice(string choice)
    {
        _activeModChoice = choice;
        if (_mods is not null)
        {
            _mods.Choice = choice;
        }

        RefreshActiveMod();
    }

    /// <summary>Erkennt die aktive Mod neu und aktualisiert die Statusleiste. Wird bei Tabwechsel und bei Änderung der Explorer-Ordner aufgerufen.</summary>
    private void RefreshActiveMod()
    {
        if (_mods is null)
        {
            return;
        }

        var (vanillaRoots, modRoots) = CollectOriginRoots();
        var candidates = new List<string?> { (_modSourcePane ?? PanelPane).CurrentFilePath };
        candidates.AddRange(modRoots);

        var unknownFolder = _mods.Refresh(candidates, vanillaRoots);

        var name = _mods.ActiveMod is { } mod
            ? mod.Name + ProjectSuffix(mod, (_modSourcePane ?? PanelPane).CurrentFilePath)
            : _mods.UnmanagedFolder is { } unmanaged
                ? MainTexts.ModStatusUnmanagedName(Path.GetFileName(unmanaged))
                : "—";
        ModStatusText.Text = (_mods.ChoiceLost ? "⚠ " + MainTexts.ModChoiceLost + " · " : string.Empty)
            + (_mods.IsAutomatic ? MainTexts.ModStatusAutomatic(name) : MainTexts.ModStatusFixed(name));
        if (_mods.Registry.LoadWarning is { } warning)
        {
            ModStatusText.Text += "   ⚠ " + warning;
        }

        if (unknownFolder is not null)
        {
            Dispatcher.BeginInvoke(new Action(() => AskToAddMod(unknownFolder)), DispatcherPriority.ApplicationIdle);
        }

        if (NotesPanelControl.BugsWholeProject)
        {
            RefreshNotesPanel();
        }

        UpdateBugTrackerButton();
        if (ActiveBugProjectId is { } bugProjectId)
        {
            RefreshBugsInBackground(bugProjectId);
        }

        // Aktive Mod kann gewechselt haben: GUI-Index nachziehen (baut nur neu, wenn sich Mod-Ordner oder Abhängigkeiten geändert haben).
        _ = UpdateGuiWorkspaceAsync(force: false);
    }

    /// <summary>Rolle und Sperren für die Statusleiste, z. B. " · Dev-Branch von Idea Variation · 🔒 Datei"; leer, wenn nichts zutrifft. Die Sperre der Datei oder ihres Ordners kommt aus <paramref name="filePath"/>.</summary>
    private string ProjectSuffix(ModInfo mod, string? filePath)
    {
        var suffix = _mods is not null && ModProjects.RoleText(_mods.Registry, mod) is { } role ? " · " + role : string.Empty;
        if (mod.IsLocked)
        {
            suffix += " · 🔒";
        }

        if (_mods is not null && filePath is not null
            && _mods.Registry.ToRelative(mod.Id, filePath) is { Length: > 0 } relative
            && ModLocks.LockedBy(mod, relative) is { } by)
        {
            suffix += " · 🔒 " + (string.Equals(by, relative, StringComparison.OrdinalIgnoreCase) ? MainTexts.ModStatusLockedFile : MainTexts.ModStatusLockedFolder);
        }

        return suffix;
    }

    /// <summary>Einmalige Rückfrage je Ordner und Sitzung. "Nein" wird gespeichert (nie wieder fragen), "Abbrechen" fragt erst beim nächsten Start wieder.</summary>
    private void AskToAddMod(string folder)
    {
        if (_mods is null || _modPromptOpen || !_askedModFolders.Add(folder))
        {
            return;
        }

        var registry = _mods.Registry;
        if (registry.IsIgnored(folder) || registry.Resolve(folder) is not null)
        {
            return;
        }

        _modPromptOpen = true;
        try
        {
            var answer = MessageBox.Show(
                this,
                MainTexts.AddModPrompt(folder),
                MainTexts.AddModTitle,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (answer == MessageBoxResult.Yes)
            {
                var name = TextPromptWindow.Ask(this, MainTexts.AddModTitle, MainTexts.AddModName, ModRegistry.SuggestName(folder, _mods.MetadataFile));
                if (!string.IsNullOrWhiteSpace(name))
                {
                    registry.Add(name, folder);
                    _mods.SyncNoteKeys();
                }
            }
            else if (answer == MessageBoxResult.No)
            {
                registry.Ignore(folder);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, MainTexts.AddModTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _modPromptOpen = false;
        }

        RefreshActiveMod();
    }

    /// <summary>Das Verzeichnis hat sich geändert (Mod angelegt, Ordner hinzugefügt oder neu verknüpft): Notiz-Schlüssel nachziehen, dann die aktive Mod neu erkennen.</summary>
    private void OnModRegistryChanged()
    {
        _mods?.SyncNoteKeys();
        RefreshActiveMod();
        RefreshLockDisplays();
        _errorLog?.RefreshSources();
    }

    /// <summary>Die Angaben dieses Spiels für das Veröffentlichen auf Steam.</summary>
    private static SteamPublishHost SteamHost() => new(
        GameContext.Repository.GameDirectory(GameContext.GameId),
        GameContext.Profile?.SteamAppId,
        GameContext.Profile?.DisplayName ?? GameContext.GameId);

    private void OpenModsWindow_Click(object sender, RoutedEventArgs e)
    {
        if (_mods is null)
        {
            return;
        }

        if (_modsWindow is null)
        {
            _modsWindow = new ModsWindow(_mods.Registry, _mods.MetadataFile, OnModRegistryChanged, _mods.ReleaseNoteKeys, SteamHost(), OpenBranchSyncWindow, mod => OpenModCompatWindow(mod), OpenCompatchForMod, PlaysetQuick()) { Owner = this };
            _modsWindow.Closed += (_, _) => _modsWindow = null;
        }

        if (_modsWindow.IsVisible)
        {
            _modsWindow.Activate();
        }
        else
        {
            _modsWindow.Show();
        }
    }

    private void OpenModCompat_Click(object sender, RoutedEventArgs e) => OpenModCompatWindow(null);

    /// <summary>Öffnet das Fenster "Mod-Kompatibilität" (nicht modal, nur eines je Fenster); <paramref name="add"/> kommt danach ins aktuelle Set.</summary>
    private void OpenModCompatWindow(ModInfo? add)
    {
        if (_mods is null)
        {
            return;
        }

        if (_compatWindow is null)
        {
            _compatWindow = new ModCompatWindow(new ModCompatHost(
                _mods.Registry,
                _mods.MetadataFile,
                GameContext.Repository.GameDirectory(GameContext.GameId),
                ComputeGameContentRoot(_gameExecutablePath),
                GameContext.Profile?.SteamAppId,
                () => _playsetsFilePath,
                GameContext.Profile ?? _activeProfile,
                OpenCompatFile,
                CompareCompatFiles,
                (a, b) => OpenCompatchStart(new CompatchPreset(a, b, null))))
            {
                Owner = this,
            };
            _compatWindow.Closed += (_, _) => _compatWindow = null;
        }

        if (_compatWindow.IsVisible)
        {
            _compatWindow.Activate();
        }
        else
        {
            _compatWindow.Show();
        }

        if (add is not null)
        {
            _compatWindow.AddMod(add);
        }
    }

    /// <summary>Öffnet eine Datei aus dem Kompatibilitätsfenster im aktiven Arbeitsbereich.</summary>
    private void OpenCompatFile(string path)
    {
        var document = ActivePane.LoadFile(path, _activeHighlighting);
        UpdateHasNote(document);
    }

    /// <summary>Zeigt zwei Dateien im Vergleichsfenster (nur lesend, auch für fremde Mods).</summary>
    private void CompareCompatFiles(string left, string right, string leftLabel, string rightLabel)
    {
        OpenVanillaDiffWindow();
        _vanillaDiffWindow?.OpenFilePair(left, right, leftLabel, rightLabel);
    }

    /// <summary>Klick auf "Mod: …" in der Statusleiste: Auswahl der aktiven Mod (aufgeklappt, Suchfeld oben), dazu "Mods verwalten".</summary>
    private void ModStatus_Click(object sender, MouseButtonEventArgs e)
    {
        if (_mods is null)
        {
            return;
        }

        var popup = new Popup { PlacementTarget = ModStatusText, Placement = PlacementMode.Top, StaysOpen = false, AllowsTransparency = false };
        var choice = new SearchableChoice(searchWidth: 140, choiceWidth: 320);
        var items = new List<(string Text, object? Tag)> { (MenuTexts.ModsAutomatic, ActiveModChoice.Automatic) };
        items.AddRange(ChoosableMods().Select(m => (m.Name, (object?)m.Id)));
        choice.SetItems(items, _mods.IsAutomatic && !_mods.ChoiceLost ? ActiveModChoice.Automatic : _mods.Choice, pinned: 1);
        choice.SelectionChanged += (_, _) =>
        {
            if (choice.SelectedTag is string picked && picked != _activeModChoice)
            {
                SetActiveModChoice(picked);
            }

            popup.IsOpen = false;
        };
        var manage = new Controls.Button { Content = new Controls.TextBlock { Text = MenuTexts.ModsManage }, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
        manage.Click += (_, _) =>
        {
            popup.IsOpen = false;
            OpenModsWindow_Click(manage, new RoutedEventArgs());
        };
        var row = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(8) };
        row.Children.Add(choice);
        row.Children.Add(manage);
        var border = new Controls.Border { Child = row, BorderThickness = new Thickness(1) };
        border.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.PanelBackground");
        border.SetResourceReference(Controls.Border.BorderBrushProperty, "Theme.ControlBorder");
        popup.Child = border;
        popup.IsOpen = true;

        // Liste gleich aufklappen; das Suchfeld oben darin hat dann den Fokus.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => choice.Choice.IsDropDownOpen = true);
    }

    /// <summary>Angelegte Mods des Spiels mit vorhandenem Ordner, nach Namen sortiert (Auswahl in Statusleiste und Menü).</summary>
    private IReadOnlyList<ModInfo> ChoosableMods() =>
        _mods?.Registry.Mods.Where(m => m.Paths.Any(Directory.Exists)).OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToList()
        ?? (IReadOnlyList<ModInfo>)Array.Empty<ModInfo>();

    /// <summary>Menü "Mods": "Mods verwalten", dann "Automatisch (offene Datei)" und alle Mods; die gültige Wahl hat ein Häkchen.</summary>
    private void ModsMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, ModsMenuItem))
        {
            return;
        }

        ModsMenuItem.Items.Clear();
        var manage = new Controls.MenuItem { Header = MenuTexts.ModsManage, ToolTip = MenuTexts.ModsManageTip };
        manage.Click += OpenModsWindow_Click;
        ModsMenuItem.Items.Add(manage);
        ModsMenuItem.Items.Add(new Controls.Separator());
        var automatic = _mods is null || (_mods.IsAutomatic && !_mods.ChoiceLost);
        var auto = new Controls.MenuItem { Header = MenuTexts.ModsAutomatic, IsCheckable = true, IsChecked = automatic, ToolTip = MenuTexts.ModsAutomaticTip };
        auto.Click += (_, _) => SetActiveModChoice(ActiveModChoice.Automatic);
        ModsMenuItem.Items.Add(auto);
        foreach (var mod in ChoosableMods())
        {
            var id = mod.Id;
            var item = new Controls.MenuItem { Header = mod.Name.Replace("_", "__"), IsCheckable = true, IsChecked = !automatic && _mods?.ActiveMod?.Id == id };
            item.Click += (_, _) => SetActiveModChoice(id);
            ModsMenuItem.Items.Add(item);
        }
    }
}
