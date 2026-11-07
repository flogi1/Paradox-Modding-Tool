using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Imaging;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Auswahl einer vorhandenen Textur (Jomini-Spiele) mit Suche, Vorschaubildern und Vorschau. Die Dateisuche läuft beim Öffnen im Hintergrund und wird
/// beim Schließen abgebrochen; EU V hat rund 13 000 Texturen. Die Liste virtualisiert, Vorschaubilder fragen nur sichtbare Zeilen beim eigenen
/// Bildpuffer des Fensters an (dekodiert wird dort im Hintergrund, nie hier). Nie beim gemeinsamen Puffer der Vorschau: volle Texturen verdrängten
/// dort die Bilder des Designers und füllten seine Warteschlange. Beim Schließen verwirft das Fenster die wartenden Aufträge seines Puffers.
/// </summary>
public sealed class JominiTexturePickerWindow : Window
{
    /// <summary>Pixeldaten im eigenen Puffer des Fensters.</summary>
    private const long ThumbnailCacheBytes = 64L * 1024 * 1024;

    private readonly Func<CancellationToken, IReadOnlyList<GuiTextureEntry>> _scan;
    private readonly string _current;
    private readonly CancellationTokenSource _cancel = new();
    private readonly TaskCompletionSource _scanned = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TextBox _search = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly ListView _list = new() { Height = 300, MinWidth = 640, SelectionMode = SelectionMode.Single };
    private readonly TextBlock _count = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly GuiImageCache _thumbnails = new(ThumbnailCacheBytes);
    private readonly TextureThumbnail _preview = new() { Width = 200, Height = 120 };
    private readonly TextBlock _detail = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 420, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
    private readonly Button _ok = new() { Content = GuiTexts.ClassicSpritePickerOk, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), IsEnabled = false };
    private readonly EventHandler _imageReady;
    private IReadOnlyList<GuiTextureEntry> _all = Array.Empty<GuiTextureEntry>();
    private IReadOnlyList<GuiTextureEntry> _shown = Array.Empty<GuiTextureEntry>();
    private bool _started;

    /// <summary>Hinweis statt der Anzahl: "wird gesucht" während der Suche, die Fehlermeldung nach einem Fehlschlag, sonst <see langword="null"/>.</summary>
    private string? _status;
    private int _redrawQueued;
    private bool _subscribed;

    /// <param name="scan">Sucht alle Texturen; läuft auf einem Hintergrund-Thread und soll den Abbruch beachten.</param>
    /// <param name="current">Der Wert im Texturfeld (mit oder ohne Anführungszeichen) oder <see langword="null"/>; ist er in der Liste, ist er vorgewählt.</param>
    public JominiTexturePickerWindow(Func<CancellationToken, IReadOnlyList<GuiTextureEntry>> scan, string? current)
    {
        _scan = scan;
        _current = (current ?? string.Empty).Trim().Trim('"').Replace('\\', '/');
        Title = GuiTexts.TexturePickerTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _search.ToolTip = GuiTexts.TexturePickerSearch;
        _count.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _detail.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _preview.Cache = _thumbnails;

        var grid = new GridView();
        grid.Columns.Add(new GridViewColumn { Width = 44, CellTemplate = ThumbnailTemplate(_thumbnails) });
        grid.Columns.Add(Column(GuiTexts.ClassicSpritePickerColFile, nameof(GuiTextureEntry.RelativePath), 430));
        grid.Columns.Add(Column(string.Empty, nameof(GuiTextureEntry.OriginLabel), 160));
        _list.View = grid;
        VirtualizingPanel.SetIsVirtualizing(_list, true);
        VirtualizingPanel.SetVirtualizationMode(_list, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(_list, true);
        _list.SelectionChanged += (_, _) => OnSelectionChanged();
        _list.MouseDoubleClick += OnDoubleClick;
        _search.TextChanged += (_, _) => ApplyFilter();

        _ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = GuiTexts.ClassicSpriteCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(_ok);
        buttons.Children.Add(cancel);

        var previewBox = new Border { BorderThickness = new Thickness(1), Child = _preview };
        previewBox.SetResourceReference(Border.BorderBrushProperty, "Theme.ControlBorder");
        var below = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
        below.Children.Add(previewBox);
        below.Children.Add(_detail);
        var footer = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(_search);
        panel.Children.Add(_list);
        panel.Children.Add(_count);
        panel.Children.Add(below);
        panel.Children.Add(footer);
        Content = panel;

        // ImageReady kommt von Hintergrund-Threads: einmal je Schub auf den UI-Thread holen und nur sichtbare Bilder neu zeichnen.
        _imageReady = (_, _) =>
        {
            if (Interlocked.Exchange(ref _redrawQueued, 1) == 0)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, RedrawThumbnails);
            }
        };

        // Angemeldet erst beim Anzeigen (OnClosed meldet ab und verwirft die Warteschlange). Jedes Öffnen beginnt einen neuen Lauf: geänderte
        // Texturen werden einmal gegen die Datei geprüft.
        Loaded += (_, _) =>
        {
            if (!_subscribed)
            {
                _subscribed = true;
                _thumbnails.NewRun();
                _thumbnails.ImageReady += _imageReady;
            }

            _search.Focus();
            StartScan();
        };
    }

    /// <summary>Der gewählte Pfad ab <c>gfx/</c>; leer, solange nichts gewählt wurde.</summary>
    public string Chosen { get; private set; } = string.Empty;

    /// <summary>Wird fertig, wenn das Ergebnis der Suche in der Liste steht (auch bei Fehlschlag oder Abbruch; dann bleibt die Liste leer).</summary>
    internal Task Scanned => _scanned.Task;

    internal string SearchText
    {
        get => _search.Text;
        set => _search.Text = value;
    }

    /// <summary>Die Pfade der gezeigten (gefilterten) Zeilen in Listenreihenfolge.</summary>
    internal IReadOnlyList<string> ShownPaths => _shown.Select(e => e.RelativePath).ToList();

    internal string CountText => _count.Text;

    internal string DetailText => _detail.Text;

    internal string? SelectedPath
    {
        get => (_list.SelectedItem as GuiTextureEntry)?.RelativePath;
        set => _list.SelectedItem = _shown.FirstOrDefault(e => string.Equals(e.RelativePath, value, StringComparison.OrdinalIgnoreCase));
    }

    internal Button OkButton => _ok;

    internal ListView List => _list;

    /// <summary>Der eigene Bildpuffer der Vorschaubilder und der Vorschau (für Tests).</summary>
    internal GuiImageCache ThumbnailCache => _thumbnails;

    /// <summary>Startet die Suche im Hintergrund (einmal); das Ergebnis kommt über den Dispatcher in die Liste.</summary>
    internal void StartScan()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _status = GuiTexts.TexturePickerSearching;
        _count.Text = _status;
        var token = _cancel.Token;
        var dispatcher = Dispatcher;
        Task.Run(() => _scan(token), token).ContinueWith(
            task =>
            {
                // Fehlschlag oder Abbruch: leere Liste; ein Fehlschlag zeigt seine Meldung statt der Anzahl. Die Ausnahme ist damit beobachtet.
                var entries = task.Status == TaskStatus.RanToCompletion ? task.Result : Array.Empty<GuiTextureEntry>();
                var error = task.Exception?.GetBaseException().Message;
                dispatcher.BeginInvoke(() => OnScanned(entries, error));
            },
            TaskScheduler.Default);
    }

    /// <summary>Übernimmt die gewählte Zeile als <see cref="Chosen"/>; <see langword="false"/>, wenn nichts gewählt ist.</summary>
    internal bool Choose()
    {
        if (_list.SelectedItem is not GuiTextureEntry entry)
        {
            return false;
        }

        Chosen = entry.RelativePath;
        return true;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_subscribed)
        {
            _thumbnails.ImageReady -= _imageReady;
        }

        // Wartende Vorschaubilder braucht niemand mehr; die Hintergrund-Threads des Puffers enden nach dem laufenden Bild.
        _thumbnails.CancelQueued();
        _cancel.Cancel();
        base.OnClosed(e);
    }

    private void OnScanned(IReadOnlyList<GuiTextureEntry> entries, string? error)
    {
        if (_cancel.IsCancellationRequested)
        {
            _scanned.TrySetResult();
            return;
        }

        _status = error is null ? null : GuiTexts.TexturePickerFailed(error);
        _all = entries;
        ApplyFilter();
        if (_current.Length > 0 && _list.SelectedItem is null)
        {
            SelectedPath = _current;
            if (_list.SelectedItem is { } selected)
            {
                _list.ScrollIntoView(selected);
            }
        }

        _scanned.TrySetResult();
    }

    private void Accept()
    {
        if (Choose() && IsVisible)
        {
            DialogResult = true;
        }
    }

    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Nur ein Doppelklick auf eine Zeile wählt (nicht auf Spaltenköpfe oder die Rollleiste).
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(_list, source) is ListViewItem item && item.Content is GuiTextureEntry entry)
        {
            _list.SelectedItem = entry;
            Accept();
        }
    }

    private void ApplyFilter()
    {
        var text = _search.Text.Trim().Replace('\\', '/');
        _shown = GuiTextureCatalog.Filter(_all, text);
        var selected = SelectedPath;
        _list.ItemsSource = _shown;
        if (selected is not null)
        {
            SelectedPath = selected;
        }

        _count.Text = _status ?? (_started ? GuiTexts.TexturePickerCount(_shown.Count, _all.Count) : string.Empty);
        OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        var entry = _list.SelectedItem as GuiTextureEntry;
        _ok.IsEnabled = entry is not null;
        _preview.FullPath = entry?.FullPath;
        if (entry is null)
        {
            _detail.Text = string.Empty;
            return;
        }

        // Nur der Kopf der gewählten Datei wird gelesen (Größe), nie das Bild.
        _detail.Text = TextureLocator.ReadSizeOfFile(entry.FullPath) is { } size
            ? GuiTexts.TexturePickerSize(size.Width, size.Height) + Environment.NewLine + entry.OriginLabel
            : entry.OriginLabel;
    }

    private void RedrawThumbnails()
    {
        Interlocked.Exchange(ref _redrawQueued, 0);
        _preview.InvalidateVisual();
        foreach (var thumbnail in Descendants<TextureThumbnail>(_list))
        {
            thumbnail.InvalidateVisual();
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var inner in Descendants<T>(child))
            {
                yield return inner;
            }
        }
    }

    private static DataTemplate ThumbnailTemplate(GuiImageCache cache)
    {
        var factory = new FrameworkElementFactory(typeof(TextureThumbnail));
        factory.SetValue(WidthProperty, 32.0);
        factory.SetValue(HeightProperty, 32.0);
        factory.SetValue(TextureThumbnail.CacheProperty, cache);
        factory.SetBinding(TextureThumbnail.FullPathProperty, new Binding(nameof(GuiTextureEntry.FullPath)));
        return new DataTemplate { VisualTree = factory };
    }

    private static GridViewColumn Column(string header, string member, double width) =>
        new() { Header = header, Width = width, DisplayMemberBinding = new Binding(member) };

    /// <summary>
    /// Ein Vorschaubild, eingepasst mit Seitenverhältnis. Fragt den Bildpuffer <see cref="Cache"/> (den des Fensters) erst beim Zeichnen (also nur
    /// sichtbar); solange das Bild aussteht oder unlesbar ist oder kein Puffer gesetzt ist, bleibt ein leerer Kasten. Das Fenster zeichnet nach
    /// <see cref="GuiImageCache.ImageReady"/> neu.
    /// </summary>
    internal sealed class TextureThumbnail : FrameworkElement
    {
        public static readonly DependencyProperty FullPathProperty = DependencyProperty.Register(
            nameof(FullPath), typeof(string), typeof(TextureThumbnail), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Der Bildpuffer; ohne ihn bleibt der Kasten leer (nie der gemeinsame Puffer der Vorschau).</summary>
        public static readonly DependencyProperty CacheProperty = DependencyProperty.Register(
            nameof(Cache), typeof(GuiImageCache), typeof(TextureThumbnail), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        private static readonly Pen Frame = CreateFrame();

        public string? FullPath
        {
            get => (string?)GetValue(FullPathProperty);
            set => SetValue(FullPathProperty, value);
        }

        public GuiImageCache? Cache
        {
            get => (GuiImageCache?)GetValue(CacheProperty);
            set => SetValue(CacheProperty, value);
        }

        protected override void OnRender(DrawingContext context)
        {
            var box = new Rect(0, 0, ActualWidth, ActualHeight);
            context.DrawRectangle(Brushes.Transparent, Frame, box);
            if (FullPath is not { Length: > 0 } path || Cache is not { } cache || box.Width <= 0 || box.Height <= 0)
            {
                return;
            }

            if (cache.TryGet(path, GuiTint.White, out BitmapSource? bitmap) != GuiImageState.Ready || bitmap is null || bitmap.PixelWidth == 0 || bitmap.PixelHeight == 0)
            {
                return;
            }

            var scale = Math.Min(box.Width / bitmap.PixelWidth, box.Height / bitmap.PixelHeight);
            var width = bitmap.PixelWidth * scale;
            var height = bitmap.PixelHeight * scale;
            context.DrawImage(bitmap, new Rect((box.Width - width) / 2, (box.Height - height) / 2, width, height));
        }

        private static Pen CreateFrame()
        {
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80)), 1);
            pen.Freeze();
            return pen;
        }
    }
}
