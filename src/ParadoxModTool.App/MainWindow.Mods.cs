using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.Editor;
using ParadoxModTool.Mods;
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

        ModStatusText.Text = _mods.ActiveMod is { } mod
            ? $"Mod: {mod.Name}{ProjectSuffix(mod, (_modSourcePane ?? PanelPane).CurrentFilePath)}"
            : _mods.UnmanagedFolder is { } unmanaged
                ? $"Mod: — ({Path.GetFileName(unmanaged)} ist nicht angelegt, Klick öffnet die Mod-Verwaltung)"
                : "Mod: —";
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
            suffix += string.Equals(by, relative, StringComparison.OrdinalIgnoreCase) ? " · 🔒 Datei" : " · 🔒 Ordner";
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
                $"Der Ordner\n{folder}\nenthält eine Metadaten-Datei und sieht wie eine Mod aus.\n\nAls Mod anlegen?\n\nJa: anlegen\nNein: nicht mehr fragen\nAbbrechen: später",
                "Mod anlegen",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (answer == MessageBoxResult.Yes)
            {
                var name = TextPromptWindow.Ask(this, "Mod anlegen", "Name der Mod:", ModRegistry.SuggestName(folder, _mods.MetadataFile));
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
            MessageBox.Show(this, ex.Message, "Mod anlegen", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            _modsWindow = new ModsWindow(_mods.Registry, _mods.MetadataFile, OnModRegistryChanged, _mods.ReleaseNoteKeys, SteamHost(), OpenBranchSyncWindow, mod => OpenModCompatWindow(mod), OpenCompatchForMod) { Owner = this };
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

    private void ModStatus_Click(object sender, MouseButtonEventArgs e) => OpenModsWindow_Click(sender, e);
}
