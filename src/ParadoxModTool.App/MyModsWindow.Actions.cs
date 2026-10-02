using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.ModOverview;
using Controls = System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Schnellaktionen der Übersicht "Meine Mods" (Knöpfe in der Kachel und Rechtsklick-Menü). Aktionen, die das Register ändern können, laufen im PMT
/// des Spiels der Mod: direkt im eigenen Spiel, sonst über die Pipe (PMT wird dafür bei Bedarf gestartet).
/// </summary>
internal sealed partial class MyModsWindow
{
    private bool _handOffRunning;

    private FrameworkElement BuildActions(OverviewEntry entry)
    {
        var panel = new Controls.WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var (label, tip, enabled, action) in Actions(entry))
        {
            var button = new Controls.Button { Content = label, ToolTip = tip, IsEnabled = enabled, FontSize = 11, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 4, 4) };
            button.Click += (_, _) => action();
            panel.Children.Add(button);
        }

        return panel;
    }

    private Controls.ContextMenu BuildContextMenu(OverviewEntry entry)
    {
        var menu = new Controls.ContextMenu();
        foreach (var (label, tip, enabled, action) in Actions(entry))
        {
            var item = new Controls.MenuItem { Header = label, ToolTip = tip, IsEnabled = enabled };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        return menu;
    }

    /// <summary>Die Aktionen einer Mod: Beschriftung, Tooltip, aktiv, Ausführung.</summary>
    private IEnumerable<(string Label, string? Tip, bool Enabled, Action Run)> Actions(OverviewEntry entry)
    {
        var mod = entry.Mod;
        var gameId = mod.GameId;
        var modId = mod.Mod.Id;
        yield return (OverviewTexts.ActionOpenInPmt, OverviewTexts.ActionOpenInPmtTip, true, () => _ = RunInGame(gameId, HandOffKind.OpenMod, modId));

        var folder = mod.Mod.Paths.FirstOrDefault(Directory.Exists);
        yield return (OverviewTexts.ActionOpenFolder, folder, folder is not null, () => OpenFolder(folder));
        yield return (OverviewTexts.ActionReleases, OverviewTexts.ActionReleasesTip, true, () => _ = RunInGame(gameId, HandOffKind.OpenReleases, modId));
        if (mod.ProjectRoot.Mod.BugSources.Count > 0)
        {
            var rootId = mod.ProjectRoot.Mod.Id;
            yield return (OverviewTexts.ActionBugs, OverviewTexts.ActionBugsTip, true, () => _ = RunInGame(gameId, HandOffKind.OpenBug, rootId));
        }

        yield return (OverviewTexts.ActionEditLinks, null, true, () => EditLinks(mod));
    }

    /// <summary>
    /// Führt eine Aktion im PMT des Spiels aus: im eigenen Spiel direkt, sonst über die Pipe; läuft das PMT des Spiels nicht, wird es gestartet und die
    /// Anfrage bis zu 20 Sekunden lang wiederholt.
    /// </summary>
    private async Task RunInGame(string gameId, HandOffKind kind, string modId, string? bugId = null)
    {
        if (gameId == _host.CurrentGameId)
        {
            switch (kind)
            {
                case HandOffKind.OpenMod:
                    Owner?.Activate();
                    _host.ShowModInPmt(modId);
                    break;
                case HandOffKind.OpenReleases:
                    _host.OpenReleasesFor(modId);
                    break;
                case HandOffKind.OpenBug:
                    _host.OpenBugsFor(modId, bugId);
                    break;
            }

            return;
        }

        if (_handOffRunning)
        {
            return;
        }

        _handOffRunning = true;
        Cursor = System.Windows.Input.Cursors.AppStarting;
        var name = _snapshot?.Games.FirstOrDefault(g => g.Id == gameId)?.DisplayName ?? gameId;
        try
        {
            if (!GameContext.Repository.IsGameOpen(gameId))
            {
                GameContext.LaunchWindow(gameId);
            }

            if (!await EditorHandOffPipe.SendAsync(gameId, EditorRequest.ForMod(kind, modId, bugId), TimeSpan.FromSeconds(20)))
            {
                MessageBox.Show(this, OverviewTexts.HandOffTimeout(name), OverviewTexts.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException)
        {
            MessageBox.Show(this, ex.Message, OverviewTexts.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _handOffRunning = false;
            Cursor = null;
        }
    }

    private void OpenFolder(string? folder)
    {
        if (folder is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(folder)}\"") { UseShellExecute = false });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, OverviewTexts.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void EditLinks(OverviewMod mod)
    {
        var paths = new OverviewPaths(_host.AppDataRoot);
        var current = ModLinkStore.Find(ModLinkStore.Load(paths.LinksFile), mod.GameId, mod.Mod.Id);
        if (!ModLinksDialog.Edit(this, mod.Mod.Name, current, mod.Links, out var result))
        {
            return;
        }

        try
        {
            ModLinkStore.Set(paths.LinksFile, mod.GameId, mod.Mod.Id, result.Forum, result.Discord, result.GitHub);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, OverviewTexts.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _ = ReloadAsync(fetchSteam: false);
    }
}
