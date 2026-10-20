using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Palette links im Designer: Engine-Widgets, darunter Typen und Templates nach Quelldatei (Mod-Einträge farbig), mit Suchfeld und Anzahl der
/// Verwendungen. Doppelklick oder "Einfügen" meldet <see cref="EntryActivated"/>, Ziehen liefert den Eintrag im Format
/// <see cref="GuiDesignerView.PaletteDragFormat"/>. Baut im Hintergrund, neu nur bei neuem Arbeitsbereich.
/// </summary>
public sealed class GuiPaletteView : UserControl
{
    private readonly TextBox _search = new() { Margin = new Thickness(2) };
    private readonly ListBox _list = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(4, 2, 4, 2), TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer _filterTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly MenuItem _inspect = new() { Header = GuiTexts.ContextInspect };
    private ListCollectionView? _view;
    private IReadOnlyList<GuiPaletteEntry> _indexEntries = Array.Empty<GuiPaletteEntry>();
    private IReadOnlyList<GuiPaletteEntry> _blockEntries = Array.Empty<GuiPaletteEntry>();
    private GuiWorkspace? _workspace;
    private CancellationTokenSource? _run;
    private Point? _dragStart;
    private bool _built;

    public GuiPaletteView()
    {
        _search.ToolTip = GuiTexts.PaletteSearchTip;
        _status.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _list.ToolTip = GuiTexts.PaletteTip;
        _list.SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
        _list.SetValue(VirtualizingPanel.IsVirtualizingWhenGroupingProperty, true);
        _list.SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
        _list.SetValue(VirtualizingPanel.ScrollUnitProperty, ScrollUnit.Pixel);
        _list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        _list.ItemTemplate = EntryTemplate();
        _list.GroupStyle.Add(new GroupStyle { HeaderTemplate = GroupHeaderTemplate() });

        _search.TextChanged += (_, _) =>
        {
            _filterTimer.Stop();
            _filterTimer.Start();
        };
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            _view?.Refresh();
        };

        _list.MouseDoubleClick += (_, e) =>
        {
            if (ItemAt(e.OriginalSource as DependencyObject) is not null && SelectedEntry is { } entry)
            {
                EntryActivated?.Invoke(this, entry);
            }
        };
        _list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && SelectedEntry is { } entry)
            {
                EntryActivated?.Invoke(this, entry);
                e.Handled = true;
            }
        };

        var insert = new MenuItem { Header = GuiTexts.PaletteInsert };
        insert.Click += (_, _) =>
        {
            if (SelectedEntry is { } entry)
            {
                EntryActivated?.Invoke(this, entry);
            }
        };
        _inspect.Click += (_, _) =>
        {
            if (SelectedEntry?.Definition is { } definition)
            {
                InspectRequested?.Invoke(this, definition);
            }
        };
        _list.ContextMenu = new ContextMenu { Items = { insert, _inspect } };
        _list.ContextMenuOpening += (_, e) =>
        {
            if (ItemAt(e.OriginalSource as DependencyObject) is not { } item)
            {
                e.Handled = true;
                return;
            }

            item.IsSelected = true;
            _inspect.IsEnabled = SelectedEntry?.Definition is not null;
        };

        _list.PreviewMouseLeftButtonDown += (_, e) => _dragStart = ItemAt(e.OriginalSource as DependencyObject) is null ? null : e.GetPosition(_list);
        _list.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragStart is not { } start || SelectedEntry is not { } entry)
            {
                return;
            }

            var delta = e.GetPosition(_list) - start;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            _dragStart = null;
            DragDrop.DoDragDrop(_list, new DataObject(GuiDesignerView.PaletteDragFormat, entry), DragDropEffects.Copy);
        };

        var root = new DockPanel();
        DockPanel.SetDock(_search, Dock.Top);
        root.Children.Add(_search);
        DockPanel.SetDock(_status, Dock.Top);
        root.Children.Add(_status);
        root.Children.Add(_list);
        Content = root;

        GuiSettings.Changed += (_, _) =>
        {
            if (_built && !ReferenceEquals(GuiSettings.Workspace, _workspace))
            {
                Rebuild();
            }
        };
        Loaded += (_, _) =>
        {
            if (!_built || !ReferenceEquals(GuiSettings.Workspace, _workspace))
            {
                Rebuild();
            }
            else
            {
                // Aktive Mod oder eigene Bausteine können sich geändert haben: nur die Bausteingruppe neu lesen.
                LoadBlocks();
                ShowEntries();
            }
        };
        GuiSettings.BlocksChanged += (_, _) =>
        {
            if (_built)
            {
                LoadBlocks();
                ShowEntries();
            }
        };
    }

    /// <summary>Doppelklick, Enter oder "Einfügen": an der Auswahl einfügen.</summary>
    public event EventHandler<GuiPaletteEntry>? EntryActivated;

    public event EventHandler<GuiDefinition>? InspectRequested;

    public GuiPaletteEntry? SelectedEntry => _list.SelectedItem as GuiPaletteEntry;

    /// <summary>Liste im Hintergrund neu aufbauen (nur bei neuem Arbeitsbereich; das Ereignis kommt auch beim Umschalten von Einstellungen).</summary>
    private void Rebuild()
    {
        _built = true;
        _workspace = GuiSettings.Workspace;
        _run?.Cancel();
        LoadBlocks();
        if (_workspace is not { } workspace)
        {
            _indexEntries = Array.Empty<GuiPaletteEntry>();
            ShowEntries();
            _status.Text = GuiSettings.Status;
            return;
        }

        var run = new CancellationTokenSource();
        _run = run;
        _status.Text = GuiTexts.PaletteBuilding;
        Task.Run(() => GuiPalette.Entries(workspace.Index, workspace.CreateResolver(), workspace.Catalog, run.Token), run.Token).ContinueWith(
            task =>
            {
                if (run.IsCancellationRequested)
                {
                    return;
                }

                if (!task.IsCompletedSuccessfully)
                {
                    _status.Text = GuiTexts.PaletteFailed(task.Exception?.GetBaseException().Message ?? string.Empty);
                    return;
                }

                _indexEntries = task.Result;
                ShowEntries();
                _status.Text = string.Empty;
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>Bausteine des aktiven Spiels (auf dem UI-Thread, Teil-2-Ruling 1); klassisch keine (<see cref="GuiSettings.BlocksOffered"/>).</summary>
    private void LoadBlocks() =>
        _blockEntries = GuiSettings.BlocksOffered && GuiSettings.BlockSource is { } source ? GuiPalette.BlockEntries(source()) : Array.Empty<GuiPaletteEntry>();

    /// <summary>Bausteingruppe oben, darunter die Einträge des Index; ListCollectionView gruppiert in Reihenfolge des ersten Auftretens.</summary>
    private void ShowEntries()
    {
        var view = new ListCollectionView(_blockEntries.Concat(_indexEntries).ToList());
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GuiPaletteEntry.Group)));
        view.Filter = item => GuiPalette.Matches((GuiPaletteEntry)item, _search.Text.Trim());
        _view = view;
        _list.ItemsSource = view;
    }

    private static ListBoxItem? ItemAt(DependencyObject? source)
    {
        for (var current = source; current is not null; current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
        {
            if (current is ListBoxItem item)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Farbiges Kästchen nach Art, Name (Mod-Einträge in Akzentfarbe) und Anzahl der Verwendungen. Namen als TextBlock (Unterstriche).</summary>
    private static DataTemplate EntryTemplate()
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var swatch = new FrameworkElementFactory(typeof(Border));
        swatch.SetValue(WidthProperty, 8.0);
        swatch.SetValue(HeightProperty, 8.0);
        swatch.SetValue(MarginProperty, new Thickness(0, 0, 5, 0));
        swatch.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        swatch.SetBinding(Border.BackgroundProperty, new Binding(nameof(GuiPaletteEntry.Category)) { Converter = new CategoryBrushConverter() });
        panel.AppendChild(swatch);

        var name = new FrameworkElementFactory(typeof(TextBlock), "EntryName");
        name.SetBinding(TextBlock.TextProperty, new Binding(nameof(GuiPaletteEntry.Name)));
        panel.AppendChild(name);

        var usages = new FrameworkElementFactory(typeof(TextBlock));
        usages.SetBinding(TextBlock.TextProperty, new Binding(nameof(GuiPaletteEntry.Usages)) { Converter = new UsagesConverter() });
        usages.SetValue(MarginProperty, new Thickness(6, 0, 0, 0));
        usages.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        panel.AppendChild(usages);

        var template = new DataTemplate { VisualTree = panel };
        var mod = new DataTrigger { Binding = new Binding(nameof(GuiPaletteEntry.IsMod)), Value = true };
        mod.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("Theme.Accent"), "EntryName"));
        template.Triggers.Add(mod);
        return template;
    }

    private static DataTemplate GroupHeaderTemplate()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding("Name") { Converter = new GroupConverter() });
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        text.SetValue(TextBlock.MarginProperty, new Thickness(2, 6, 0, 2));
        text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        return new DataTemplate { VisualTree = text };
    }

    private sealed class CategoryBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is GuiWidgetCategory category ? new SolidColorBrush(GuiSchematicCanvas.ColorOf(category)) : null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private sealed class UsagesConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is int count ? count < 0 ? string.Empty : GuiTexts.PaletteUsages(count) : null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private sealed class GroupConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is string { Length: 0 } ? GuiTexts.PaletteEngineGroup : value;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
