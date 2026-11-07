using System.Windows;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.Localization;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// „Ausblendregeln“ des Flag-Trackers: Liste der Regeln der aktiven Mod mit „Entfernen“ (Mehrfachauswahl) und „Schließen“. Jede Entfernung
/// wirkt sofort; <c>changed</c> speichert und sucht im Flag-Tracker neu. Keine eigenen Tastenkürzel (Escape über <see cref="Controls.Button.IsCancel"/>).
/// </summary>
internal sealed class FlagHideRulesWindow : Window
{
    private readonly FlagHideList _hide;
    private readonly Action _changed;
    private readonly Controls.ListBox _list = new() { SelectionMode = Controls.SelectionMode.Extended, MinHeight = 160, Margin = new Thickness(0, 0, 0, 10) };
    private readonly Controls.TextBlock _empty = new() { Text = FlagTrackerTexts.NoRules, Margin = new Thickness(0, 0, 0, 10) };
    private readonly Controls.Button _remove = new() { Content = CommonTexts.Remove, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };

    /// <param name="modName">Ordnername der aktiven Mod, nur zur Anzeige.</param>
    public FlagHideRulesWindow(FlagHideList hide, string modName, Action changed)
    {
        _hide = hide;
        _changed = changed;
        Title = FlagTrackerTexts.Rules;
        Width = 480;
        Height = 380;
        MinWidth = 320;
        MinHeight = 240;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var header = new Controls.TextBlock { Text = FlagTrackerTexts.RulesFor(modName), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        _empty.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _remove.Click += (_, _) => RemoveSelected();
        _list.SelectionChanged += (_, _) => _remove.IsEnabled = _list.SelectedItems.Count > 0;
        var close = new Controls.Button { Content = CommonTexts.Close, MinWidth = 90, IsCancel = true };

        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        buttons.Children.Add(_remove);
        buttons.Children.Add(close);

        var root = new Controls.DockPanel { Margin = new Thickness(14) };
        Controls.DockPanel.SetDock(header, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_empty, Controls.Dock.Top);
        Controls.DockPanel.SetDock(buttons, Controls.Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(_empty);
        root.Children.Add(buttons);
        root.Children.Add(_list);
        Content = root;
        Fill();
    }

    private void Fill()
    {
        _list.ItemsSource = _hide.Rules.ToList();
        _empty.Visibility = _hide.Rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _remove.IsEnabled = false;
    }

    private void RemoveSelected()
    {
        var removed = false;
        foreach (var rule in _list.SelectedItems.OfType<string>().ToList())
        {
            removed |= _hide.Remove(rule);
        }

        if (removed)
        {
            Fill();
            _changed();
        }
    }
}
