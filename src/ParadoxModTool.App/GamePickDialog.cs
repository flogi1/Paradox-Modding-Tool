using System.Windows;
using ParadoxModTool.Localization;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;

namespace ParadoxModTool.App;

/// <summary>
/// Kleiner Dialog "Für welche Spiele?": "Alle Spiele" oder eine Auswahl der Spiele aus den Spielprofilen. Gibt die gewählten Kennungen zurück,
/// leer für "alle" (auch wenn einzeln alle Spiele angehakt sind, damit später hinzukommende Spiele dazugehören); <see langword="null"/> bei Abbruch.
/// </summary>
public sealed class GamePickDialog : Window
{
    private readonly CheckBox _all;
    private readonly List<(string Id, CheckBox Box)> _games = new();
    private readonly Button _ok;

    private GamePickDialog(string title, string message, string allText, IReadOnlyList<(string Id, string Name)> games, IReadOnlyCollection<string> current)
    {
        Title = title;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });

        _all = new CheckBox { Content = allText, IsChecked = current.Count == 0, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
        _all.Click += (_, _) => UpdateState();
        panel.Children.Add(_all);

        var list = new StackPanel { Margin = new Thickness(18, 0, 0, 0) };
        foreach (var (id, name) in games)
        {
            var box = new CheckBox { Content = name, IsChecked = current.Count == 0 || current.Contains(id, StringComparer.OrdinalIgnoreCase), Margin = new Thickness(0, 2, 0, 2) };
            box.Click += (_, _) => UpdateState();
            _games.Add((id, box));
            list.Children.Add(box);
        }

        panel.Children.Add(list);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        _ok = new Button { Content = CommonTexts.Ok, Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        _ok.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(_ok);
        buttons.Children.Add(new Button { Content = CommonTexts.Cancel, Width = 80, IsCancel = true });
        panel.Children.Add(buttons);

        Content = panel;
        UpdateState();
    }

    /// <summary>Mit "Alle Spiele" sind die einzelnen Spiele angehakt und gesperrt; ohne ein angehaktes Spiel gibt es kein OK.</summary>
    private void UpdateState()
    {
        var all = _all.IsChecked == true;
        foreach (var (_, box) in _games)
        {
            box.IsEnabled = !all;
            if (all)
            {
                box.IsChecked = true;
            }
        }

        _ok.IsEnabled = all || _games.Any(game => game.Box.IsChecked == true);
    }

    private List<string> Selection() =>
        _all.IsChecked == true || _games.All(game => game.Box.IsChecked == true)
            ? new List<string>()
            : _games.Where(game => game.Box.IsChecked == true).Select(game => game.Id).ToList();

    /// <param name="current">Bisherige Auswahl; leer = alle Spiele.</param>
    public static List<string>? Ask(Window? owner, string title, string message, string allText, IReadOnlyList<(string Id, string Name)> games, IReadOnlyCollection<string> current)
    {
        var dialog = new GamePickDialog(title, message, allText, games, current) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Selection() : null;
    }
}
