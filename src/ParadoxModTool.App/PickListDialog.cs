using System.Windows;
using ParadoxModTool.Localization;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using Orientation = System.Windows.Controls.Orientation;
using SelectionMode = System.Windows.Controls.SelectionMode;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>Kleiner Auswahldialog: eine Liste von Texten, mit Suchfeld filterbar, einfach oder mehrfach wählbar. Gibt die gewählten Positionen in der ursprünglichen Liste zurück.</summary>
public sealed class PickListDialog : Window
{
    /// <summary>Ein Listeneintrag mit seiner Position in der ursprünglichen, ungefilterten Liste.</summary>
    private sealed record PickItem(string Text, int OriginalIndex)
    {
        public override string ToString() => Text;
    }

    private readonly ListBox _list;
    private readonly TextBox _search;
    private readonly IReadOnlyList<PickItem> _all;

    private PickListDialog(string title, string message, IReadOnlyList<string> items, bool multi)
    {
        Title = title;
        Width = 520;
        Height = 420;
        MinHeight = 240;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _all = items.Select((text, index) => new PickItem(text, index)).ToList();
        _list = new ListBox { SelectionMode = multi ? SelectionMode.Extended : SelectionMode.Single, ItemsSource = _all };
        _search = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        _search.TextChanged += (_, _) => ApplyFilter();

        var panel = new System.Windows.Controls.DockPanel { Margin = new Thickness(14) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var ok = new Button { Content = CommonTexts.Ok, Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) =>
        {
            if (_list.SelectedItems.Count > 0)
            {
                DialogResult = true;
            }
        };
        var cancel = new Button { Content = CommonTexts.Cancel, Width = 80, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        System.Windows.Controls.DockPanel.SetDock(buttons, System.Windows.Controls.Dock.Bottom);
        panel.Children.Add(buttons);

        var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        System.Windows.Controls.DockPanel.SetDock(text, System.Windows.Controls.Dock.Top);
        panel.Children.Add(text);

        System.Windows.Controls.DockPanel.SetDock(_search, System.Windows.Controls.Dock.Top);
        panel.Children.Add(_search);

        _list.MouseDoubleClick += (_, _) =>
        {
            if (!multi && _list.SelectedItems.Count > 0)
            {
                DialogResult = true;
            }
        };
        panel.Children.Add(_list);
        Content = panel;

        Loaded += (_, _) => _search.Focus();
    }

    /// <summary>Filtert die Liste live nach dem Suchtext (Teiltext, Groß-/Kleinschreibung egal); bereits gewählte, weiter passende Einträge bleiben gewählt.</summary>
    private void ApplyFilter()
    {
        var filter = _search.Text.Trim();
        var visible = filter.Length == 0
            ? _all
            : _all.Where(i => i.Text.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        var previouslySelected = _list.SelectedItems.Cast<PickItem>().ToHashSet();
        _list.ItemsSource = visible;
        foreach (var item in visible.Where(previouslySelected.Contains))
        {
            _list.SelectedItems.Add(item);
        }
    }

    /// <summary>Zeigt den Dialog. <see langword="null"/> bei Abbruch oder ohne Auswahl.</summary>
    public static IReadOnlyList<int>? Ask(Window owner, string title, string message, IReadOnlyList<string> items, bool multi)
    {
        var dialog = new PickListDialog(title, message, items, multi) { Owner = owner };
        return dialog.ShowDialog() == true
            ? dialog._list.SelectedItems.Cast<PickItem>().Select(i => i.OriginalIndex).OrderBy(i => i).ToList()
            : null;
    }
}
