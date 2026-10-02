using System.Windows;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.ModOverview;
using ParadoxModTool.Notes;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Detailansicht eines Bugreports in der Übersicht "Meine Mods" (Seitenleiste rechts): Titel, Quelle, Autor, Datum, Text und die Aktionen "Gesehen",
/// "Im Browser öffnen" (nur GitHub/Discord mit gültiger Adresse) und "Im Bugtracker öffnen" (PMT des Spiels).
/// </summary>
internal sealed partial class MyModsWindow
{
    private readonly Controls.Border _detailPane = CreateDetailPane();

    private static Controls.Border CreateDetailPane()
    {
        var pane = new Controls.Border { Width = 360, Margin = new Thickness(10, 0, 0, 0), Padding = new Thickness(12, 10, 12, 10), BorderThickness = new Thickness(1, 0, 0, 0), Visibility = Visibility.Collapsed };
        pane.SetResourceReference(Controls.Border.BorderBrushProperty, "Theme.ControlBorder");
        pane.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.PanelBackground");
        return pane;
    }

    private void ShowBugDetail(OverviewMod root, BugReport report)
    {
        var panel = new Controls.StackPanel();

        var header = new Controls.DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var close = new Controls.Button { Content = "✕", ToolTip = OverviewTexts.BugsClose, Padding = new Thickness(6, 0, 6, 1), VerticalAlignment = VerticalAlignment.Top };
        close.Click += (_, _) => CloseBugDetail();
        Controls.DockPanel.SetDock(close, Controls.Dock.Right);
        header.Children.Add(close);
        header.Children.Add(new Controls.TextBlock { Text = report.Title, FontWeight = FontWeights.Bold, FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 8, 0) });
        panel.Children.Add(header);

        var meta = new Controls.TextBlock
        {
            Text = string.Join(" · ", new[] { BugTexts.Source(report.Source), report.Author, report.CreatedUtc.ToLocalTime().ToString("g"), root.Mod.Name }.Where(s => !string.IsNullOrWhiteSpace(s))),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };
        meta.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        panel.Children.Add(meta);

        var body = new Controls.TextBlock { Text = string.IsNullOrWhiteSpace(report.Body) ? OverviewTexts.BugsNoBody : report.Body, TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(new Controls.ScrollViewer { Content = body, MaxHeight = 420, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 10) });

        var buttons = new Controls.WrapPanel();
        buttons.Children.Add(DetailButton(OverviewTexts.BugsSeen, async () =>
        {
            await MarkSeenAsync(new[] { report.Id });
            CloseBugDetail();
        }));
        if (report.Source != BugSource.Local && ModLinkStore.IsValidUrl(report.Url))
        {
            buttons.Children.Add(DetailButton(OverviewTexts.BugsOpenBrowser, () =>
            {
                _host.OpenUrl(report.Url);
                return Task.CompletedTask;
            }));
        }

        buttons.Children.Add(DetailButton(OverviewTexts.BugsOpenTracker, () => RunInGame(root.GameId, HandOffKind.OpenBug, root.Mod.Id, report.Id)));
        panel.Children.Add(buttons);

        _detailPane.Child = panel;
        _detailPane.Visibility = Visibility.Visible;
    }

    private void CloseBugDetail()
    {
        _detailPane.Visibility = Visibility.Collapsed;
        _detailPane.Child = null;
    }

    private static Controls.Button DetailButton(string text, Func<Task> click)
    {
        var button = new Controls.Button { Content = text, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 6) };
        button.Click += async (_, _) => await click();
        return button;
    }
}
