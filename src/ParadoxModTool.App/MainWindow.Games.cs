using System.IO;
using System.Windows;
using System.Windows.Controls;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using ParadoxModTool.Session;
using ParadoxModTool.Settings;

namespace ParadoxModTool.App;

/// <summary>
/// Menü "Spielauswahl": öffnet ein weiteres Spiel in einem eigenen Programmfenster (eigener Prozess). Jedes Fenster gehört zu genau einem
/// Spiel (siehe <see cref="GameContext"/>); ein Spiel, das schon offen ist, lässt sich nicht ein zweites Mal öffnen.
/// </summary>
public partial class MainWindow
{
    private void WindowMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, WindowMenuItem))
        {
            return;
        }

        WindowMenuItem.Items.Clear();
        WindowMenuItem.Items.Add(new MenuItem { Header = MenuTexts.GamesHeading, IsEnabled = false });
        WindowMenuItem.Items.Add(new Separator());

        foreach (var entry in GameContext.Profiles)
        {
            var id = entry.Profile.Id;
            var isThis = id == GameContext.GameId;
            var isOpen = isThis || GameContext.Repository.IsGameOpen(id);

            var name = entry.Profile.DisplayName.Replace("_", "__");
            var item = new MenuItem
            {
                Header = isThis ? MenuTexts.GameThisWindow(name) : isOpen ? MenuTexts.GameAlreadyOpen(name) : name,
                IsEnabled = !isOpen,
            };
            item.Click += (_, _) => OpenGameWindow(id);
            WindowMenuItem.Items.Add(item);
        }
    }

    /// <summary>Setzt Position, Größe und Maximiert-Zustand des letzten Schließens, sofern das Fenster damit auf einem vorhandenen Bildschirm sichtbar ist.</summary>
    private void ApplySavedWindowBounds()
    {
        if (GameContext.Session.State.Window is not { } saved || saved.Width < 300 || saved.Height < 200)
        {
            return;
        }

        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var titleBar = new Rect(saved.Left, saved.Top, Math.Min(saved.Width, 300), 40);
        if (!screen.IntersectsWith(titleBar))
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = saved.Left;
        Top = saved.Top;
        Width = saved.Width;
        Height = saved.Height;
        if (saved.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private WindowBoundsState CaptureWindowBounds()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        return new WindowBoundsState
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            IsMaximized = WindowState == WindowState.Maximized,
        };
    }

    private void OpenGameWindow(string gameId)
    {
        try
        {
            GameContext.LaunchWindow(gameId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            MessageBox.Show(this, $"Das Fenster konnte nicht gestartet werden: {ex.Message}", "Neues Fenster", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
