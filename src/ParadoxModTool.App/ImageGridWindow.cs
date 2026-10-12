using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ParadoxModTool.Generators;
using ParadoxModTool.Localization;
using ParadoxModTool.Session;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// "Wappen wählen" (Spec 2026-10-05-generator-packages-lookups, Abschnitt 3): alle Bilder einer Asset-Bibliothek als Raster mit Suche. Nur sichtbare Zellen lesen ihr Bild,
/// nacheinander im Hintergrund (das zuletzt sichtbar gewordene zuerst); fertige Bilder bleiben, solange das Fenster offen ist. Vorgewählt ist der aktuelle Wert, sonst
/// der Vorschlag zum Schlüssel.
/// </summary>
public sealed class ImageGridWindow : Window
{
    private const double CellWidth = 88;
    private const double ImageSize = 64;

    private readonly AssetLibrary _library;
    private readonly AppSessionState _session;
    private readonly ListBox _rows = new() { HorizontalContentAlignment = HorizontalAlignment.Left };
    private readonly TextBox _search;
    private readonly Dictionary<string, (ImageSource? Image, string? Error)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ContentControl>> _waiting = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<string> _queue = new();
    private readonly Dictionary<string, Border> _cells = new(StringComparer.Ordinal);
    private string _selected;
    private int _perRow;
    private bool _pumping;

    /// <param name="current">Aktueller Wert des Felds ("" = kein Bild).</param>
    /// <param name="suggestion">Vorschlag zum Schlüssel; gilt nur ohne aktuellen Wert.</param>
    public ImageGridWindow(Window owner, AssetLibrary library, string? current, string? suggestion, AppSessionState session)
    {
        _library = library;
        _session = session;
        _selected = !string.IsNullOrEmpty(current) ? current : suggestion ?? string.Empty;
        Owner = owner;
        Title = GeneratorTexts.ImageGridTitle;
        Width = 820;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        if (session.GeneratorImageGridBounds is { Width: > 200, Height: > 200 } bounds)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            (Left, Top, Width, Height) = (bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            WindowState = bounds.IsMaximized ? WindowState.Maximized : WindowState.Normal;
        }

        // Zeilen ohne eigene Markierung: ausgewählt wird eine Zelle, nicht die Zeile.
        var rowStyle = new Style(typeof(ListBoxItem));
        rowStyle.Setters.Add(new Setter(TemplateProperty, new ControlTemplate(typeof(ListBoxItem)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }));
        _rows.ItemContainerStyle = rowStyle;
        VirtualizingPanel.SetIsVirtualizing(_rows, true);
        VirtualizingPanel.SetVirtualizationMode(_rows, VirtualizationMode.Standard);
        ScrollViewer.SetCanContentScroll(_rows, true);
        ScrollViewer.SetHorizontalScrollBarVisibility(_rows, ScrollBarVisibility.Disabled);

        var (searchPanel, search) = SearchBox.Create();
        _search = search;
        _search.TextChanged += (_, _) => BuildRows();
        searchPanel.Margin = new Thickness(0, 0, 0, 6);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var take = new Button { Content = new TextBlock { Text = GeneratorTexts.ImageGridTake }, Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        take.Click += (_, _) => Take();
        var close = new Button { Content = new TextBlock { Text = CommonTexts.Close }, Padding = new Thickness(12, 2, 12, 2), IsCancel = true };
        buttons.Children.Add(take);
        buttons.Children.Add(close);

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(searchPanel, Dock.Top);
        root.Children.Add(searchPanel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(_rows);
        Content = root;

        SizeChanged += (_, _) =>
        {
            if (PerRow() != _perRow)
            {
                BuildRows();
            }
        };
        Loaded += (_, _) =>
        {
            BuildRows();
            _search.Focus();
        };
        Closing += (_, _) => _session.GeneratorImageGridBounds = new WindowBoundsState
        {
            Left = RestoreBounds.Left,
            Top = RestoreBounds.Top,
            Width = RestoreBounds.Width,
            Height = RestoreBounds.Height,
            IsMaximized = WindowState == WindowState.Maximized,
        };
    }

    /// <summary>Gewählter Eintrag nach "Übernehmen"; "" = kein Bild.</summary>
    public string? Picked { get; private set; }

    private int PerRow() => Math.Max(1, (int)((Math.Max(_rows.ActualWidth, 200) - 30) / CellWidth));

    private void BuildRows()
    {
        _perRow = PerRow();
        _cells.Clear();
        var query = _search.Text.Trim();
        var entries = new List<string> { string.Empty };
        entries.AddRange(_library.Entries.Where(e => query.Length == 0 || e.Contains(query, StringComparison.OrdinalIgnoreCase)));
        var rows = new List<StackPanel>();
        StackPanel? selectedRow = null;
        for (var i = 0; i < entries.Count; i += _perRow)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var entry in entries.Skip(i).Take(_perRow))
            {
                row.Children.Add(Cell(entry));
                if (entry == _selected)
                {
                    selectedRow = row;
                }
            }

            rows.Add(row);
        }

        _rows.ItemsSource = rows;
        if (selectedRow is not null)
        {
            Dispatcher.BeginInvoke(() => _rows.ScrollIntoView(selectedRow), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        MarkSelected();
    }

    private Border Cell(string entry)
    {
        var picture = new ContentControl { Width = ImageSize, Height = ImageSize, HorizontalAlignment = HorizontalAlignment.Center };
        var name = entry.Length == 0 ? GeneratorTexts.ImageNone : Path.GetFileNameWithoutExtension(entry);
        var label = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, Width = CellWidth - 8, ToolTip = entry.Length == 0 ? name : entry };
        var stack = new StackPanel();
        stack.Children.Add(picture);
        stack.Children.Add(label);
        var cell = new Border { Child = stack, Width = CellWidth - 4, Margin = new Thickness(2), Padding = new Thickness(2), BorderThickness = new Thickness(2), Background = Brushes.Transparent };
        cell.MouseLeftButtonDown += (_, e) =>
        {
            _selected = entry;
            MarkSelected();
            if (e.ClickCount == 2)
            {
                Take();
            }
        };
        if (entry.Length > 0)
        {
            cell.Loaded += (_, _) => RequestPicture(entry, picture);
        }

        _cells[entry] = cell;
        return cell;
    }

    private void MarkSelected()
    {
        foreach (var (entry, cell) in _cells)
        {
            if (entry == _selected)
            {
                cell.SetResourceReference(Border.BorderBrushProperty, "Theme.Accent");
            }
            else
            {
                cell.BorderBrush = Brushes.Transparent;
            }
        }
    }

    private void RequestPicture(string entry, ContentControl target)
    {
        if (_cache.TryGetValue(entry, out var done))
        {
            Show(target, done);
            return;
        }

        if (!_waiting.TryGetValue(entry, out var targets))
        {
            targets = new List<ContentControl>();
            _waiting[entry] = targets;
            _queue.Push(entry);
        }

        if (!targets.Contains(target))
        {
            targets.Add(target);
        }

        Pump();
    }

    /// <summary>Liest die angefragten Bilder nacheinander im Hintergrund (ein Zip-Zugriff zur Zeit).</summary>
    private async void Pump()
    {
        if (_pumping)
        {
            return;
        }

        _pumping = true;
        while (_queue.Count > 0 && IsLoaded)
        {
            var entry = _queue.Pop();
            var result = await Task.Run(() => Decode(entry));
            _cache[entry] = result;
            if (_waiting.Remove(entry, out var targets))
            {
                foreach (var target in targets)
                {
                    Show(target, result);
                }
            }
        }

        _pumping = false;
    }

    private (ImageSource? Image, string? Error) Decode(string entry)
    {
        try
        {
            return (ImageDecode.FromBytes(_library.Read(entry), entry), null);
        }
        catch (Exception ex) when (ex is GeneratorOutputException or IOException or NotSupportedException or InvalidDataException or ArgumentException)
        {
            return (null, ex.Message);
        }
    }

    private static void Show(ContentControl target, (ImageSource? Image, string? Error) result)
    {
        if (result.Image is { } image)
        {
            target.Content = new Image { Source = image, Stretch = Stretch.Uniform };
            return;
        }

        var warning = new TextBlock { Text = "⚠", FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ToolTip = result.Error ?? GeneratorTexts.NoPreview };
        warning.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
        target.Content = warning;
    }

    private void Take()
    {
        Picked = _selected;
        DialogResult = true;
    }
}
