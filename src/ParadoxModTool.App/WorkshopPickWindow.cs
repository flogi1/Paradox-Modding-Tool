using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.Localization;
using ParadoxModTool.SteamIntegration;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ListBox = System.Windows.Controls.ListBox;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;

namespace ParadoxModTool.App;

/// <summary>Auswahl einer gefundenen Workshop-Seite, wenn mehrere passen oder keine genau (Titel, ID und Herkunft stehen in der Zeile).</summary>
public sealed class WorkshopPickWindow : Window
{
    private readonly ListBox _list = new();
    private readonly IReadOnlyList<WorkshopMatch> _items;

    private WorkshopPickWindow(string header, IReadOnlyList<WorkshopMatch> items)
    {
        _items = items;
        Title = PublishTexts.WorkshopPickTitle;
        Width = 560;
        Height = 380;
        MinHeight = 260;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _list.ItemsSource = items.Select(m => $"{m.Title}    (ID {m.Id}, {m.Source})").ToList();
        _list.SelectedIndex = 0;
        _list.MouseDoubleClick += (_, _) =>
        {
            if (_list.SelectedIndex >= 0)
            {
                DialogResult = true;
            }
        };

        var ok = new Button { Content = "OK", Width = 90, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        ok.Click += (_, _) =>
        {
            if (_list.SelectedIndex >= 0)
            {
                DialogResult = true;
            }
        };
        var cancel = new Button { Content = CommonTexts.Cancel, Width = 90, IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var text = new TextBlock { Text = header, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        var dock = new DockPanel { Margin = new Thickness(14) };
        DockPanel.SetDock(text, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        dock.Children.Add(text);
        dock.Children.Add(buttons);
        dock.Children.Add(_list);
        Content = dock;
    }

    /// <summary>Gibt die gewählte Seite zurück; <see langword="null"/> bei Abbruch.</summary>
    public static WorkshopMatch? Pick(Window owner, string header, IReadOnlyList<WorkshopMatch> items)
    {
        var window = new WorkshopPickWindow(header, items) { Owner = owner };
        return window.ShowDialog() == true && window._list.SelectedIndex >= 0 ? window._items[window._list.SelectedIndex] : null;
    }
}
