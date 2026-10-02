using System.IO;
using System.Windows;
using ParadoxModTool.Explorer;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.Settings;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.ModOverview;
using ParadoxModTool.Mods;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Was das Fenster "Meine Mods" vom Hauptfenster braucht.</summary>
internal interface IMyModsHost
{
    string CurrentGameId { get; }

    string AppDataRoot { get; }

    IReadOnlyList<GameProfile> Profiles { get; }

    AppSettings Settings { get; }

    void ShowModInPmt(string modId);

    void OpenReleasesFor(string modId);

    void OpenBugsFor(string modId, string? bugId);

    void OpenUrl(string url);
}

/// <summary>
/// Übersicht "Meine Mods": öffnet das Fenster und führt Befehle aus, die es (dieses oder das eines anderen Spiels über die Pipe) an das PMT des
/// Spiels der Mod schickt. Nur dieses PMT darf das Register seines Spiels ändern.
/// </summary>
public partial class MainWindow : IMyModsHost
{
    private MyModsWindow? _myModsWindow;

    string IMyModsHost.CurrentGameId => GameContext.GameId;

    string IMyModsHost.AppDataRoot => GameContext.Repository.Root;

    IReadOnlyList<GameProfile> IMyModsHost.Profiles => GameContext.Profiles.Select(e => e.Profile).ToList();

    AppSettings IMyModsHost.Settings => _settings;

    void IMyModsHost.OpenUrl(string url) => OpenInBrowser(url);

    private void OpenMyMods_Click(object sender, RoutedEventArgs e)
    {
        if (_myModsWindow is null)
        {
            _myModsWindow = new MyModsWindow(this) { Owner = this };
            _myModsWindow.Closed += (_, _) => _myModsWindow = null;
            _myModsWindow.Show();
            return;
        }

        if (_myModsWindow.WindowState == WindowState.Minimized)
        {
            _myModsWindow.WindowState = WindowState.Normal;
        }

        _myModsWindow.Activate();
    }

    /// <summary>Befehl der Übersicht aus der Pipe: Fenster nach vorn, dann Mod zeigen, Releases oder Bugtracker öffnen.</summary>
    private void RunModCommand(EditorRequest request)
    {
        HandOffLog.Append(HandOffLogPath, $"command: {request.Kind}, mod: {request.ModId}, bug: {request.BugId ?? "-"}", DateTime.Now);
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        if (request.ModId is not { } modId)
        {
            return;
        }

        switch (request.Kind)
        {
            case HandOffKind.OpenMod:
                ShowModInPmt(modId);
                break;
            case HandOffKind.OpenReleases:
                OpenReleasesFor(modId);
                break;
            case HandOffKind.OpenBug:
                OpenBugsFor(modId, request.BugId);
                break;
        }
    }

    /// <summary>Die Mod dieses Spiels; ohne Treffer ein Hinweis und <see langword="null"/>.</summary>
    private ModInfo? RequireMod(string modId)
    {
        if (_mods?.Registry.Get(modId) is { } mod)
        {
            return mod;
        }

        MessageBox.Show(this, OverviewTexts.HandOffModMissing, OverviewTexts.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        return null;
    }

    /// <summary>Macht die Mod aktiv: ihr erster vorhandener Ordner kommt (falls nötig) in die Verzeichnis-Übersicht und wird dort gezeigt.</summary>
    public void ShowModInPmt(string modId)
    {
        if (RequireMod(modId) is not { } mod || mod.Paths.FirstOrDefault(Directory.Exists) is not { } path)
        {
            return;
        }

        var inside = ExplorerPanelControl.CaptureRoots().Any(root => IsSameOrInside(path, root.FullPath));
        if (!inside)
        {
            ExplorerPanelControl.AddRoot(new ExplorerRootDescriptor(path, mod.Name, ExplorerRootKind.ModFolder));
        }

        _settings.ExplorerVisible = true;
        ExplorerPanelControl.RevealInTree(path);
        RefreshActiveMod();
    }

    /// <summary>Releases-Fenster der Mod, wie im Mods-Fenster.</summary>
    public void OpenReleasesFor(string modId)
    {
        if (_mods is null || RequireMod(modId) is not { } mod)
        {
            return;
        }

        new ReleasesWindow(_mods.Registry, mod, _mods.MetadataFile, SteamHost()) { Owner = this }.ShowDialog();
    }

    /// <summary>Bugtracker auf dem Projekt der Mod, optional mit gewählter Meldung.</summary>
    public void OpenBugsFor(string modId, string? bugId)
    {
        if (_mods is null || RequireMod(modId) is not { } mod)
        {
            return;
        }

        // Die Übersicht kann die Bug-Datei gerade geändert haben (Abruf, "Gesehen"): erst frisch lesen, dann auswählen.
        RefreshBugViews();
        OpenBugTracker(bugId, ModProjects.Root(_mods.Registry, mod).Id);
    }

    private static bool IsSameOrInside(string path, string root)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var parent = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }
}
