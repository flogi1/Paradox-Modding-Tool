using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Blocks;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// "Platzhalter vorschlagen" (Plan-2b-Ruling 8): alle Vorschläge vorab angehakt, Suche über Schlüssel, Beschriftung und Fund, "Alle"/"Keine" für die
/// gezeigten Einträge. Die Liste virtualisiert (Vanilla-Code ergibt Hunderte Vorschläge); die Häkchen tragen einen TextBlock, weil die CheckBox-Vorlage
/// Zugriffstasten liest und Unterstriche sonst verschwänden.
/// </summary>
public sealed class GuiBlockSuggestWindow : Window
{
    private readonly List<Item> _items;
    private readonly TextBox _search = new() { Margin = new Thickness(0, 0, 0, 6) };
    private readonly ListBox _list = new();

    public GuiBlockSuggestWindow(IReadOnlyList<GuiBlockSuggestion> suggestions)
    {
        _items = suggestions.Select(s => new Item(s)).ToList();
        Title = GuiTexts.SuggestTitle;
        Width = 720;
        Height = 560;
        MinWidth = 480;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _search.ToolTip = GuiTexts.BlocksSearchTip;
        _search.TextChanged += (_, _) => Filter();
        _list.ItemTemplate = ItemTemplate();
        VirtualizingPanel.SetIsVirtualizing(_list, true);
        VirtualizingPanel.SetVirtualizationMode(_list, VirtualizationMode.Recycling);
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        Filter();

        var all = new Button { Content = GuiTexts.SuggestAll, MinWidth = 70, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        all.Click += (_, _) => SetAll(true);
        var none = new Button { Content = GuiTexts.SuggestNoneButton, MinWidth = 70, Padding = new Thickness(8, 2, 8, 2) };
        none.Click += (_, _) => SetAll(false);
        var apply = new Button { Content = GuiTexts.SuggestApply, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2) };
        apply.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = GuiTexts.SuggestCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };

        var buttons = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(all);
        left.Children.Add(none);
        DockPanel.SetDock(left, Dock.Left);
        buttons.Children.Add(left);
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(apply);
        right.Children.Add(cancel);
        DockPanel.SetDock(right, Dock.Right);
        buttons.Children.Add(right);

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(_search, Dock.Top);
        root.Children.Add(_search);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(_list);
        Content = root;
        Loaded += (_, _) => _search.Focus();
    }

    /// <summary>Angehakte Vorschläge in ursprünglicher Reihenfolge.</summary>
    public IReadOnlyList<GuiBlockSuggestion> Chosen => _items.Where(i => i.Checked).Select(i => i.Suggestion).ToList();

    internal string SearchText
    {
        get => _search.Text;
        set => _search.Text = value;
    }

    internal IReadOnlyList<GuiBlockSuggestion> Shown => _list.Items.Cast<Item>().Select(i => i.Suggestion).ToList();

    /// <summary>"Alle"/"Keine": nur die gerade gezeigten Einträge.</summary>
    internal void SetAll(bool value)
    {
        foreach (var item in _list.Items.Cast<Item>())
        {
            item.Checked = value;
        }
    }

    internal void SetChecked(string id, bool value)
    {
        foreach (var item in _items.Where(i => i.Suggestion.Id == id))
        {
            item.Checked = value;
        }
    }

    private void Filter()
    {
        var filter = _search.Text.Trim();
        _list.ItemsSource = _items
            .Where(i => filter.Length == 0
                || i.Suggestion.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || i.Suggestion.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || i.Suggestion.Found.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static DataTemplate ItemTemplate()
    {
        var check = new FrameworkElementFactory(typeof(CheckBox));
        check.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(Item.Checked)) { Mode = BindingMode.TwoWay });
        check.SetValue(MarginProperty, new Thickness(0, 1, 0, 1));
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(Item.Text)));
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        check.AppendChild(text);
        return new DataTemplate { VisualTree = check };
    }

    /// <summary>Eintrag der Liste: Vorschlag, Häkchen und Anzeigetext "Art · Schlüssel · Beschriftung · Fund (Anzahl×)".</summary>
    private sealed class Item : INotifyPropertyChanged
    {
        private bool _checked = true;

        public Item(GuiBlockSuggestion suggestion)
        {
            Suggestion = suggestion;
            var kind = suggestion.Kind == GuiBlockSuggestionKind.Repeat ? GuiTexts.SuggestKindRepeat : GuiTexts.SuggestKindValue;
            Text = $"{kind} · ${suggestion.Key}$ · {suggestion.Label} · {GuiTexts.SuggestFound(suggestion.Found, suggestion.Count)}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public GuiBlockSuggestion Suggestion { get; }

        public string Text { get; }

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Checked)));
                }
            }
        }
    }
}
