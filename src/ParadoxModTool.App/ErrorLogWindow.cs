using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.SaveGames;
using ParadoxModTool.Session;
using ParadoxModTool.Settings;
using Controls = System.Windows.Controls;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// Fenster "Fehlerprotokoll" (Spec, Oberfläche): Kopfzeile mit Lauf, Vergleich, Playset und Filtern, gruppierte Liste links, Einzelheiten
/// rechts. Zeigt nur die Sitzung aus <see cref="ErrorLogService"/> an (Plan-Ruling 3): Schließen beendet weder Sitzung noch Indexaufbau.
/// Eigene Mods sind aufgeklappt, alle anderen Bereiche zu; was der Nutzer auf- oder zuklappt, bleibt bis zum Schließen erhalten.
/// Plan 2b: Ohne Besitzer ist es ein eigenständiges Fenster (Einstellung); Position und Größe merkt es sich in beiden Fällen.
/// </summary>
public sealed class ErrorLogWindow : Window
{
    private const int MaxHintOccurrences = 20;

    private readonly ErrorLogService _service;
    private readonly HashSet<string> _opened = new(StringComparer.Ordinal);
    private readonly HashSet<string> _closed = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly Controls.ComboBox _runs = new() { Width = 230, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.ComboBox _comparison = new() { Width = 230, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.ComboBox _playsets = new() { Width = 230, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.Button _reload = new() { Content = ErrorLogTexts.Reload, ToolTip = ErrorLogTexts.ReloadTip, Padding = new Thickness(8, 1, 8, 1) };
    private readonly Controls.CheckBox _errors = Toggle(ErrorLogTexts.FilterErrors);
    private readonly Controls.CheckBox _warnings = Toggle(ErrorLogTexts.FilterWarnings);
    private readonly Controls.CheckBox _infos = Toggle(ErrorLogTexts.FilterInfos);
    private readonly Controls.CheckBox _new = Toggle(ErrorLogTexts.FilterNew);
    private readonly Controls.CheckBox _unchanged = Toggle(ErrorLogTexts.FilterUnchanged);
    private readonly Controls.CheckBox _fixed = Toggle(ErrorLogTexts.FilterFixed);
    private readonly Controls.ComboBox _kinds = new() { Width = 240, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.CheckBox _ownOnly = new() { Content = ErrorLogTexts.OwnModsOnly, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBox _search = new() { Width = 180, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.Button _export = new() { Content = ErrorLogTexts.Export, ToolTip = ErrorLogTexts.ExportTip, Padding = new Thickness(8, 1, 8, 1) };
    private readonly Controls.TextBlock _counts = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.Border _notice = new() { Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _noticeText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Controls.Button _chooseFolder = new() { Content = ErrorLogTexts.ChooseFolder, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Controls.StackPanel _progressRow = new() { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.ProgressBar _progress = new() { Width = 180, Height = 14 };
    private readonly Controls.TextBlock _progressText = new() { Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.Button _cancelIndex = new() { Content = ErrorLogTexts.IndexCancel, Padding = new Thickness(8, 0, 8, 0) };
    private readonly Controls.TreeView _tree = new();
    // Gut sichtbar (Nutzerwunsch): aufgeklappt, fette Überschrift, Liste etwa drei Mal so hoch wie zuvor.
    private readonly Controls.Expander _activeTraces = new() { Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed, IsExpanded = true };
    private readonly Controls.ListBox _traceList = new() { MinHeight = 150, MaxHeight = 480, SelectionMode = Controls.SelectionMode.Extended };
    private readonly Controls.Button _removeTrace = SmallButton(ErrorLogTexts.RemoveTrace);
    private readonly Controls.Button _removeAllTraces = SmallButton(ErrorLogTexts.RemoveAllTraces);
    private readonly Controls.StackPanel _detail = new() { Margin = new Thickness(10, 0, 4, 10) };
    private readonly AppSettings _settings;
    private readonly Controls.Border _pathFilter = new() { Padding = new Thickness(6, 1, 2, 1), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _pathFilterText = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
    private readonly Controls.Button _pathFilterClear = new() { Content = "✕", Padding = new Thickness(4, 0, 4, 0), ToolTip = ErrorLogTexts.PathFilterClearTip };
    private Controls.StackPanel _entryArea = new();
    private IReadOnlyList<ErrorSection> _sections = Array.Empty<ErrorSection>();
    private string? _selectedKey;
    private long? _selectedLine;
    private string? _path;
    private string? _shownDetail;
    private bool _updating;
    private bool _refreshPending;
    private readonly Controls.CheckBox _live = new() { Content = ErrorLogTexts.LiveToggle, ToolTip = ErrorLogTexts.LiveTip, Margin = new Thickness(12, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.Button _liveCount = new() { ToolTip = ErrorLogTexts.LiveCountTip, Padding = new Thickness(6, 0, 6, 0), Visibility = Visibility.Collapsed };

    /// <summary>
    /// <paramref name="owner"/> null: eigenständiges Fenster (Plan-2b-Ruling 5); <paramref name="otherScreen"/> liefert dann ohne gemerkte
    /// Position einen Platz auf einem anderen Bildschirm (Breite, Höhe).
    /// </summary>
    public ErrorLogWindow(ErrorLogService service, AppSettings settings, Window? owner, Func<double, double, WindowBoundsState?> otherScreen)
    {
        _service = service;
        _settings = settings;
        Title = ErrorLogTexts.WindowTitle(service.Host.GameName);
        Width = 1250;
        Height = 800;
        MinWidth = 700;
        MinHeight = 400;
        if (owner is not null)
        {
            Owner = owner;
        }

        // Plan-2b-Ruling 5: Nur das eigenständige Fenster hat einen eigenen Eintrag in der Taskleiste.
        ShowInTaskbar = owner is null;

        ApplyPlacement(owner is null ? otherScreen(Width, Height) : null);
        _notice.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.NoticeBackground");
        _pathFilter.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.NoticeBackground");
        _counts.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");

        var top = new Controls.StackPanel();
        var header = new Controls.WrapPanel();
        header.Children.Add(Label(ErrorLogTexts.RunLabel));
        header.Children.Add(_runs);
        header.Children.Add(Label(ErrorLogTexts.ComparisonLabel));
        header.Children.Add(_comparison);
        header.Children.Add(Label(ErrorLogTexts.PlaysetLabel));
        header.Children.Add(_playsets);
        header.Children.Add(_reload);
        header.Children.Add(_live);
        header.Children.Add(_liveCount);
        top.Children.Add(header);

        var filters = new Controls.WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var box in new[] { _errors, _warnings, _infos, _new, _unchanged, _fixed })
        {
            filters.Children.Add(box);
        }

        filters.Children.Add(Label(ErrorLogTexts.FilterKindLabel));
        filters.Children.Add(_kinds);
        filters.Children.Add(_ownOnly);
        filters.Children.Add(Label(ErrorLogTexts.SearchLabel));
        filters.Children.Add(_search);
        filters.Children.Add(_export);
        top.Children.Add(filters);
        top.Children.Add(_counts);
        var pathFilterPanel = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        pathFilterPanel.Children.Add(_pathFilterText);
        pathFilterPanel.Children.Add(_pathFilterClear);
        _pathFilter.Child = pathFilterPanel;
        top.Children.Add(_pathFilter);

        var noticePanel = new Controls.StackPanel();
        noticePanel.Children.Add(_noticeText);
        noticePanel.Children.Add(_chooseFolder);
        _notice.Child = noticePanel;
        top.Children.Add(_notice);
        _progressRow.Children.Add(_progress);
        _progressRow.Children.Add(_progressText);
        _progressRow.Children.Add(_cancelIndex);
        top.Children.Add(_progressRow);

        var body = new Controls.Grid { Margin = new Thickness(0, 8, 0, 0) };
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 260 });
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(6) });
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star), MinWidth = 320 });
        // Scope-Verfolgung: "Aktive Spuren" unter dem Fehlerbaum, nur sichtbar, wenn es Spuren gibt.
        var traceButtons = new Controls.WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        traceButtons.Children.Add(_removeTrace);
        traceButtons.Children.Add(_removeAllTraces);
        var tracePanel = new Controls.StackPanel();
        tracePanel.Children.Add(_traceList);
        tracePanel.Children.Add(traceButtons);
        _activeTraces.Content = tracePanel;
        var left = new Controls.DockPanel();
        Controls.DockPanel.SetDock(_activeTraces, Controls.Dock.Bottom);
        left.Children.Add(_activeTraces);
        left.Children.Add(_tree);
        body.Children.Add(left);
        var splitter = new Controls.GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        Controls.Grid.SetColumn(splitter, 1);
        body.Children.Add(splitter);
        var detailScroll = new Controls.ScrollViewer
        {
            Content = _detail,
            VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Controls.ScrollBarVisibility.Disabled,
        };
        Controls.Grid.SetColumn(detailScroll, 2);
        body.Children.Add(detailScroll);

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        Controls.DockPanel.SetDock(top, Controls.Dock.Top);
        root.Children.Add(top);
        root.Children.Add(body);
        Content = root;

        _runs.SelectionChanged += (_, _) =>
        {
            if (!_updating && Selected(_runs) is int index)
            {
                _service.SelectRun(index);
            }
        };
        _comparison.SelectionChanged += (_, _) =>
        {
            if (!_updating && _comparison.SelectedItem is not null)
            {
                _service.SelectComparison(Selected(_comparison) as int?);
            }
        };
        _playsets.SelectionChanged += (_, _) =>
        {
            if (!_updating && Selected(_playsets) is string name)
            {
                _service.SelectPlayset(name);
            }
        };
        _reload.Click += (_, _) => _service.Reload();
        _live.Click += (_, _) => _service.SetLive(_live.IsChecked == true);
        _liveCount.Click += (_, _) => _service.MarkLiveSeen();
        Activated += (_, _) =>
        {
            if (_service.LiveEnabled)
            {
                _service.MarkLiveSeen();
            }
        };
        _service.LiveCountChanged += OnLiveCountChanged;
        _chooseFolder.Click += (_, _) => ChooseFolder();
        _cancelIndex.Click += (_, _) => _service.CancelIndex();
        _export.Click += (_, _) => ExportList();
        _pathFilterClear.Click += (_, _) => ShowPath(null);
        foreach (var box in new[] { _errors, _warnings, _infos, _new, _unchanged, _fixed, _ownOnly })
        {
            box.Click += (_, _) => RefreshList();
        }

        _kinds.SelectionChanged += (_, _) =>
        {
            if (!_updating)
            {
                RefreshList();
            }
        };
        _search.TextChanged += (_, _) =>
        {
            _searchDelay.Stop();
            _searchDelay.Start();
        };
        _searchDelay.Tick += (_, _) =>
        {
            _searchDelay.Stop();
            RefreshList();
        };
        _tree.SelectedItemChanged += (_, _) => OnTreeSelection();
        _traceList.MouseDoubleClick += (_, _) =>
        {
            if (_traceList.SelectedItem is Controls.ListBoxItem { Tag: ActiveTrace trace })
            {
                _service.Host.JumpTo(trace.Path, trace.Line);
            }
        };
        _traceList.SelectionChanged += (_, _) => _removeTrace.IsEnabled = _traceList.SelectedItems.Count > 0;
        _removeTrace.Click += (_, _) => RemoveTraces(_traceList.SelectedItems.OfType<Controls.ListBoxItem>().Select(i => i.Tag).OfType<ActiveTrace>().ToList());
        _removeAllTraces.Click += (_, _) => RemoveTraces(_service.Session.Traces);

        _service.StateChanged += OnServiceChanged;
        _service.SaveChanged += OnSaveChanged;
        _service.Session.Changed += OnSessionChanged;
        Closing += (_, _) => SaveBounds();
        Closed += (_, _) =>
        {
            _searchDelay.Stop();
            _service.StateChanged -= OnServiceChanged;
            _service.Session.Changed -= OnSessionChanged;
            _service.SaveChanged -= OnSaveChanged;
            _service.LiveCountChanged -= OnLiveCountChanged;

            // Der Pfadfilter gehört zum Fenster; "+N" im Menü zählt danach wieder alles, was die übrigen Filter zeigen.
            _service.Filter = _service.Filter with { Path = null };
        };

        FillKinds();
        RefreshHeader();
        RefreshList();
    }

    private void OnServiceChanged(object? sender, EventArgs e) => RefreshHeader();

    /// <summary>Anderer Spielstand geladen: Einzelheiten der gewählten Gruppe neu (Befund-Zeile, Spielstand-Hinweise).</summary>
    private void OnSaveChanged(object? sender, EventArgs e) => OnTreeSelection();

    /// <summary>Laden eines Laufs meldet mehrere Änderungen hintereinander; die Liste wird einmal danach neu aufgebaut.</summary>
    private void OnSessionChanged(object? sender, EventArgs e)
    {
        if (_refreshPending)
        {
            return;
        }

        _refreshPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _refreshPending = false;
            RefreshList();
            RefreshActiveTraces();
        }));
    }

    private void RefreshHeader()
    {
        _updating = true;
        try
        {
            var current = _service.CurrentRun is null ? (int?)null : _service.CurrentRunPosition;
            Fill(_runs, _service.Runs.Select((r, i) => (ErrorLogTexts.RunText(r), (object?)i)), current);
            var comparisons = new List<(string, object?)> { (ErrorLogTexts.NoComparison, null) };
            comparisons.AddRange(_service.Runs.Select((r, i) => (r, i)).Where(p => p.i != current).Select(p => (ErrorLogTexts.RunText(p.r), (object?)p.i)));
            Fill(_comparison, comparisons, _service.ComparisonRunPosition);
            if (_service.Playsets.Count == 0)
            {
                Fill(_playsets, new[] { (ErrorLogTexts.PlaysetUnknown, (object?)null) }, null);
                _playsets.IsEnabled = false;
            }
            else
            {
                Fill(_playsets, _service.Playsets.Select(p => (p, (object?)p)), _service.PlaysetName);
                _playsets.IsEnabled = true;
            }
        }
        finally
        {
            _updating = false;
        }

        RefreshLive();
        var notices = _service.Notices();
        _noticeText.Text = string.Join("\n", notices);
        _chooseFolder.Visibility = _service.NeedsFolder ? Visibility.Visible : Visibility.Collapsed;
        _notice.Visibility = notices.Count > 0 || _service.NeedsFolder ? Visibility.Visible : Visibility.Collapsed;

        if (_service.IndexState is { } state)
        {
            _progressRow.Visibility = Visibility.Visible;
            _progress.IsIndeterminate = state.Total == 0;
            _progress.Maximum = Math.Max(1, state.Total);
            _progress.Value = state.Done;
            _progressText.Text = ErrorLogTexts.IndexStatus(state.Done, state.Total);
        }
        else
        {
            _progressRow.Visibility = Visibility.Collapsed;
        }
    }

    private void OnLiveCountChanged(object? sender, EventArgs e) => RefreshLive();

    /// <summary>Live-Schalter, Zähler "+N" und Fenstertitel (Plan-2b-Rulings 8 und 10).</summary>
    private void RefreshLive()
    {
        _live.IsChecked = _service.LiveEnabled;
        _live.IsEnabled = !_service.NeedsFolder;
        var count = _service.LiveNewCount;
        _liveCount.Content = ErrorLogTexts.LiveCountButton(count);
        _liveCount.Visibility = _service.LiveEnabled && count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Title = count > 0 ? ErrorLogTexts.WindowTitleWithCount(_service.Host.GameName, count) : ErrorLogTexts.WindowTitle(_service.Host.GameName);
    }

    private void FillKinds()
    {
        var language = _service.Session.Language;
        var items = new List<(string, object?)> { (ErrorLogTexts.AllKinds, null) };
        items.AddRange(_service.Session.Catalog.Kinds
            .Where(k => !k.AttachToPrevious)
            .Select(k => ($"{k.NameIn(language)} ({k.Code})", (object?)k.Code))
            .OrderBy(k => k.Item1, StringComparer.CurrentCultureIgnoreCase));
        _updating = true;
        try
        {
            Fill(_kinds, items, null);
        }
        finally
        {
            _updating = false;
        }
    }

    private ErrorFilter CurrentFilter() => new()
    {
        Errors = _errors.IsChecked == true,
        Warnings = _warnings.IsChecked == true,
        Infos = _infos.IsChecked == true,
        New = _new.IsChecked == true,
        Unchanged = _unchanged.IsChecked == true,
        Fixed = _fixed.IsChecked == true,
        Code = Selected(_kinds) as string,
        OwnModsOnly = _ownOnly.IsChecked == true,
        Search = _search.Text,
        Path = _path,
    };

    /// <summary>Aus dem Spielstand-Explorer (Plan 2b): Suche auf den Namen, erste Gruppe mit diesem Namen gewählt.</summary>
    public void ShowName(string name)
    {
        _searchDelay.Stop();
        _search.Text = name;
        _searchDelay.Stop();
        _selectedKey = null;
        RefreshList();
        foreach (var section in _tree.Items.OfType<Controls.TreeViewItem>())
        {
            if (section.Items.OfType<Controls.TreeViewItem>().FirstOrDefault(i => i.Tag is GroupItem g && g.Group.GroupName == name) is { } item)
            {
                section.IsExpanded = true;
                item.IsSelected = true;
                item.BringIntoView();
                return;
            }
        }
    }

    /// <summary>Zeigt nur Fehler dieser Datei bzw. unter diesem Ordner (aus Tab, Explorer oder Notizbereich); <see langword="null"/> hebt den Filter auf.</summary>
    public void ShowPath(string? path)
    {
        _path = path;
        _pathFilterText.Text = path is null ? string.Empty : ErrorLogTexts.PathFilter(System.IO.Path.GetFileName(path.TrimEnd('\\', '/')));
        _pathFilter.ToolTip = path;
        _pathFilter.Visibility = path is null ? Visibility.Collapsed : Visibility.Visible;
        RefreshList();
    }

    /// <summary>Merkt Position und Größe (auch das Hauptfenster ruft das vor dem Speichern der Einstellungen auf).</summary>
    public void SaveBounds()
    {
        if (WindowBoundsPersistence.Capture(this) is { } bounds)
        {
            _settings.ErrorLogWindowBounds = bounds;
        }
    }

    /// <summary>Gemerkte Position, sofern sichtbar; sonst der Platz auf einem anderen Bildschirm (eigenständig) oder mittig über PMT.</summary>
    private void ApplyPlacement(WindowBoundsState? otherScreen) => WindowBoundsPersistence.Apply(this, _settings.ErrorLogWindowBounds, otherScreen);

    private void RefreshList()
    {
        var filter = CurrentFilter();
        _service.Filter = filter;
        _sections = _service.Session.Sections(filter);
        _counts.Text = ErrorLogTexts.Counts(ErrorLogSession.CountsOf(_sections));

        // Neuaufbau (z.B. jede Live-Portion) soll die Liste nicht nach oben springen lassen.
        var scroll = FindScrollViewer(_tree);
        var offset = scroll?.VerticalOffset ?? 0;
        _tree.Items.Clear();
        Controls.TreeViewItem? selected = null;
        foreach (var section in _sections)
        {
            var sectionItem = new Controls.TreeViewItem
            {
                Header = new Controls.TextBlock { Text = ErrorLogTexts.Section(section), FontWeight = FontWeights.SemiBold },
                IsExpanded = IsOpen(section),
            };
            sectionItem.Expanded += (_, e) =>
            {
                if (ReferenceEquals(e.OriginalSource, sectionItem))
                {
                    Remember(section, true);
                }
            };
            sectionItem.Collapsed += (_, e) =>
            {
                if (ReferenceEquals(e.OriginalSource, sectionItem))
                {
                    Remember(section, false);
                }
            };
            foreach (var group in section.Groups)
            {
                var item = new Controls.TreeViewItem { Header = GroupHeader(section, group), Tag = new GroupItem(section, group) };
                if (group.Key == _selectedKey)
                {
                    selected = item;
                }

                sectionItem.Items.Add(item);
            }

            _tree.Items.Add(sectionItem);
        }

        if (selected is not null)
        {
            ((Controls.TreeViewItem)selected.Parent).IsExpanded = true;
            selected.IsSelected = true;
        }
        else
        {
            ShowNoSelection();
        }

        if (scroll is not null && offset > 0)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => scroll.ScrollToVerticalOffset(offset)));
        }
    }

    private static Controls.ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is Controls.ScrollViewer viewer)
            {
                return viewer;
            }

            if (FindScrollViewer(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private bool IsOpen(ErrorSection section) =>
        section.Kind == ErrorSectionKind.OwnMod ? !_closed.Contains(section.Key) : _opened.Contains(section.Key);

    private void Remember(ErrorSection section, bool open)
    {
        if (open)
        {
            _opened.Add(section.Key);
            _closed.Remove(section.Key);
        }
        else
        {
            _closed.Add(section.Key);
            _opened.Remove(section.Key);
        }
    }

    private Controls.TextBlock GroupHeader(ErrorSection section, ErrorGroupView group)
    {
        var session = _service.Session;
        var title = ErrorLogTexts.GroupTitle(ErrorLogTexts.KindName(group.Kind, session.Language), group.GroupName);
        var text = new Controls.TextBlock();
        text.Inlines.Add(new Run(ErrorLogTexts.SeverityIcon(group.Severity) + " " + title + "  "));
        string count;
        if (section.Kind == ErrorSectionKind.Fixed)
        {
            count = ErrorLogTexts.GroupChange(group.PreviousCount, 0);
            text.TextDecorations = TextDecorations.Strikethrough;
        }
        else if (session.HasComparison && group.Status == RunStatus.New)
        {
            count = ErrorLogTexts.GroupCount(group.Count) + "  " + ErrorLogTexts.Status(RunStatus.New);
        }
        else if (session.HasComparison && group.PreviousCount != group.Count)
        {
            count = ErrorLogTexts.GroupChange(group.PreviousCount, group.Count);
        }
        else
        {
            count = ErrorLogTexts.GroupCount(group.Count);
        }

        var live = group.Entries.Count(session.IsLiveNew);
        if (live > 0 && section.Kind != ErrorSectionKind.Fixed)
        {
            count += "  " + ErrorLogTexts.LiveMarker(live);
        }

        var countRun = new Run(count);
        countRun.SetResourceReference(TextElement.ForegroundProperty, "Theme.TextMuted");
        text.Inlines.Add(countRun);
        return text;
    }

    private void OnTreeSelection()
    {
        if (_tree.SelectedItem is not Controls.TreeViewItem { Tag: GroupItem item })
        {
            return;
        }

        // Plan-2b-Ruling 12: Die gewählte Stelle über die Zeile im Lauf halten (Order: Datei und Zeilennummer); Live klassifiziert neu.
        var entry = item.Group.Key == _selectedKey ? item.Group.Entries.FirstOrDefault(e => e.Line.Order == _selectedLine) : null;
        _selectedKey = item.Group.Key;
        entry ??= item.Group.Entries[0];
        _selectedLine = entry.Line.Order;

        // Unveränderte Gruppe (z.B. Live-Portion für eine andere Gruppe): Einzelheiten und Scrollstelle stehen lassen.
        var shown = $"{item.Section.Kind}|{item.Group.Key}|{item.Group.Count}|{_selectedLine}|{_service.Session.IndexReady}|{_service.SaveVersion}|{_service.Session.TraceVersion}";
        if (shown == _shownDetail)
        {
            return;
        }

        _shownDetail = shown;
        ShowDetail(item.Section, item.Group, entry);
    }

    private void ShowNoSelection()
    {
        _shownDetail = null;
        _detail.Children.Clear();
        _detail.Children.Add(Muted(ErrorLogTexts.NoSelection));
    }

    private void ShowDetail(ErrorSection section, ErrorGroupView group, ErrorEntry entry)
    {
        var session = _service.Session;
        var kindName = ErrorLogTexts.KindName(group.Kind, session.Language);
        _detail.Children.Clear();
        _detail.Children.Add(new Controls.TextBlock
        {
            Text = ErrorLogTexts.DetailHeading(kindName, group.Code),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        var status = ErrorLogTexts.Severity(group.Severity) + (session.HasComparison ? "  ·  " + ErrorLogTexts.Status(group.Status) : string.Empty);
        _detail.Children.Add(Muted(status));

        if (group.Entries.Count > 1)
        {
            AddOccurrences(section, group, entry, kindName);
        }

        _entryArea = new Controls.StackPanel();
        _detail.Children.Add(_entryArea);
        ShowEntry(section, entry, kindName);

        if (group.Kind is { } kind)
        {
            var explain = kind.ExplainIn(session.Language);
            if (explain.Length > 0)
            {
                AddHeading(_detail, ErrorLogTexts.DetailExplain);
                _detail.Children.Add(Text(explain));
            }

            var tips = kind.TipsIn(session.Language);
            if (tips.Count > 0)
            {
                AddHeading(_detail, ErrorLogTexts.DetailTips);
                foreach (var tip in tips)
                {
                    _detail.Children.Add(Text("• " + tip));
                }
            }
        }
    }

    /// <summary>Der Teil der Einzelheiten, der von der gewählten Stelle abhängt; die Stellen-Liste darüber bleibt stehen.</summary>
    private void ShowEntry(ErrorSection section, ErrorEntry entry, string kindName)
    {
        _entryArea.Children.Clear();
        AddHeading(_entryArea, ErrorLogTexts.DetailMessage);
        _entryArea.Children.Add(new Controls.TextBox
        {
            Text = entry.Message,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            BorderThickness = new Thickness(0),
        });
        AddFile(_entryArea, entry);
        if (section.Kind != ErrorSectionKind.Fixed)
        {
            AddTrace(_entryArea, entry);
            AddHints(_entryArea, entry);
            AddSuppression(_entryArea, section, entry, kindName);
        }
    }

    private void AddOccurrences(ErrorSection section, ErrorGroupView group, ErrorEntry selected, string kindName)
    {
        AddHeading(_detail, ErrorLogTexts.DetailOccurrences(group.Entries.Count));
        var list = new Controls.ListBox { MaxHeight = 220 };
        Controls.ScrollViewer.SetHorizontalScrollBarVisibility(list, Controls.ScrollBarVisibility.Disabled);
        foreach (var entry in group.Entries)
        {
            var item = new Controls.ListBoxItem { Content = new Controls.TextBlock { Text = OccurrenceText(entry) }, Tag = entry };
            list.Items.Add(item);
            if (ReferenceEquals(entry, selected))
            {
                list.SelectedItem = item;
            }
        }

        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is Controls.ListBoxItem { Tag: ErrorEntry entry } && entry.Line.Order != _selectedLine)
            {
                _selectedLine = entry.Line.Order;
                _shownDetail = $"{section.Kind}|{group.Key}|{group.Count}|{_selectedLine}|{_service.Session.IndexReady}|{_service.SaveVersion}|{_service.Session.TraceVersion}";
                ShowEntry(section, entry, kindName);
            }
        };
        list.MouseDoubleClick += (_, _) =>
        {
            if (list.SelectedItem is Controls.ListBoxItem { Tag: ErrorEntry entry } && _service.Session.JumpTargetOf(entry) is { } target)
            {
                _service.Host.JumpTo(target.FilePath, target.Line);
            }
        };
        _detail.Children.Add(list);
        if (list.SelectedItem is not null)
        {
            list.ScrollIntoView(list.SelectedItem);
        }
    }

    private void AddFile(Controls.Panel panel, ErrorEntry entry)
    {
        if (entry.FilePath is null)
        {
            return;
        }

        var session = _service.Session;
        AddHeading(panel, ErrorLogTexts.DetailFile);
        var source = session.AttributionOf(entry).Source;
        if (source is null)
        {
            panel.Children.Add(Text(ErrorLogTexts.FileNotFound(entry.FilePath)));
            return;
        }

        var target = session.JumpTargetOf(entry) ?? new JumpTarget(source.FullPath, 1, false);
        var row = new Controls.DockPanel();
        var jump = new Controls.Button
        {
            Content = ErrorLogTexts.Jump,
            Padding = new Thickness(8, 1, 8, 1),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        jump.Click += (_, _) => _service.Host.JumpTo(target.FilePath, target.Line);
        Controls.DockPanel.SetDock(jump, Controls.Dock.Right);
        row.Children.Add(jump);
        row.Children.Add(Text(ErrorLogTexts.At(target.FilePath, target.Line)));
        panel.Children.Add(row);
        if (target.MaybeMoved)
        {
            panel.Children.Add(Muted(ErrorLogTexts.MaybeMoved));
        }

        if (target.FilePath.EndsWith(".gui", StringComparison.OrdinalIgnoreCase))
        {
            // Plan-2b-Ruling 13: GUI-Fehler im GUI-Inspektor zeigen (Spec, Fehlerkatalog).
            var inspect = SmallButton(ErrorLogTexts.InspectGui);
            inspect.ToolTip = ErrorLogTexts.InspectGuiTip;
            inspect.Margin = new Thickness(0, 4, 0, 0);
            inspect.HorizontalAlignment = HorizontalAlignment.Left;
            inspect.Click += (_, _) =>
            {
                if (!_service.Host.InspectGui(target.FilePath, target.Line))
                {
                    MessageBox.Show(this, ErrorLogTexts.InspectGuiFailed, ErrorLogTexts.Menu, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            panel.Children.Add(inspect);
        }

        var excerpt = CodeExcerpt.Read(target.FilePath, target.Line);
        if (excerpt.Count > 0)
        {
            AddHeading(panel, ErrorLogTexts.DetailExcerpt);
            panel.Children.Add(Excerpt(excerpt));
        }
    }

    private void AddHints(Controls.Panel panel, ErrorEntry entry)
    {
        var session = _service.Session;
        if (entry.Kind is null)
        {
            return;
        }

        AddSaveSection(panel, entry);
        if (!session.IndexReady)
        {
            if (entry.Name is not null)
            {
                AddHeading(panel, ErrorLogTexts.DetailHints);
                panel.Children.Add(Muted(ErrorLogTexts.IndexLoading));
            }

            return;
        }

        var advisor = session.Advisor;
        var save = _service.Host.Save();
        var hints = advisor.HintsFor(entry, save);
        if (hints.Count > 0 || entry.Candidates.Count > 0)
        {
            AddHeading(panel, ErrorLogTexts.DetailHints);
            foreach (var hint in hints)
            {
                panel.Children.Add(Text(ErrorLogTexts.Hint(hint)));
                foreach (var occurrence in hint.Occurrences.Take(MaxHintOccurrences))
                {
                    panel.Children.Add(Link(ErrorLogTexts.Occurrence(occurrence), occurrence.FilePath, occurrence.Line));
                }

                if (hint.Occurrences.Count > MaxHintOccurrences)
                {
                    panel.Children.Add(Muted(ErrorLogTexts.More(hint.Occurrences.Count - MaxHintOccurrences)));
                }

                if (hint.Candidates.Count > 0)
                {
                    panel.Children.Add(Text(ErrorLogTexts.Candidates(hint.Candidates)));
                }
            }

            if (hints.Count == 0)
            {
                panel.Children.Add(Text(ErrorLogTexts.Candidates(entry.Candidates)));
            }
        }

        // Plan 3: Korrekturen, die Dateien ändern; FixAdvisor bietet sie nur für eigene Mods an.
        var fixes = advisor.FixesFor(entry, hints, session.AttributionOf(entry)).Where(f => f is not ("jump" or "suppress")).ToList();
        if (fixes.Count == 0)
        {
            return;
        }

        AddHeading(panel, ErrorLogTexts.DetailFixes);
        var buttons = new Controls.WrapPanel();
        foreach (var fix in fixes)
        {
            var button = SmallButton(ErrorLogTexts.Fix(fix, entry.Kind));
            switch (fix)
            {
                case "replaceWithCandidate":
                    ConfigureReplace(button, entry, advisor.CandidatesFor(entry, save));
                    break;
                case "renameEverywhere":
                    button.Click += (_, _) => RenameEverywhere(entry, advisor.CandidatesFor(entry, save));
                    break;
                case "commentOutSet":
                    button.Click += (_, _) => CommentOut(entry);
                    break;
                case "traceScope":
                    ConfigureTrace(button, entry);
                    break;
                case "addModToPlayset" when hints.FirstOrDefault(h => h.Kind == HintKind.DefinedOutsidePlayset)?.Mod is { } mod:
                    button.Click += (_, _) => AddModToPlayset(mod);
                    break;
                default:
                    continue;
            }

            buttons.Children.Add(button);
        }

        panel.Children.Add(buttons);
    }

    /// <summary>Plan-3-Ruling 1: Kandidaten als Menü; ohne Fundstelle deaktiviert mit Hinweis auf "Überall umbenennen".</summary>
    private void ConfigureReplace(Controls.Button button, ErrorEntry entry, IReadOnlyList<string> candidates)
    {
        var target = _service.Session.JumpTargetOf(entry);
        if (target is null || entry.Name is not { } name)
        {
            button.IsEnabled = false;
            button.ToolTip = ErrorLogTexts.FixNeedsLocation;
            Controls.ToolTipService.SetShowOnDisabled(button, true);
            return;
        }

        button.ToolTip = ErrorLogTexts.FixCandidatesTip;
        button.Click += (_, _) =>
        {
            var menu = new Controls.ContextMenu { PlacementTarget = button, Placement = Controls.Primitives.PlacementMode.Bottom };
            foreach (var candidate in candidates)
            {
                var item = new Controls.MenuItem { Header = new Controls.TextBlock { Text = candidate } };
                item.Click += (_, _) =>
                {
                    if (_service.Host.ReplaceName(target.FilePath, target.Line, name, candidate) is { } problem)
                    {
                        MessageBox.Show(this, problem, ErrorLogTexts.Menu, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
                menu.Items.Add(item);
            }

            menu.IsOpen = true;
        };
    }

    /// <summary>Plan-3-Ruling 2: alle eigenen Mods, in denen der Name vorkommt, plus die zugeordnete Mod.</summary>
    private List<ModSource> OwnModsWith(ErrorEntry entry, string name)
    {
        var session = _service.Session;
        return session.Index.Find(name).Select(o => o.Mod)
            .Append(session.AttributionOf(entry).Mod)
            .OfType<ModSource>()
            .Where(m => m.IsOwn)
            .DistinctBy(m => ErrorLogSession.PathKey(m.Directory), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Abschnitt "Spielstand" (Spielstand-Explorer Plan 2b) bei Fehlerarten mit <c>saveCheck</c>: Befund-Zeile und Sprung in den Explorer;
    /// ohne geladenen Stand der Knopf "Mit Spielstand prüfen". Ohne <c>saveGames</c>-Block im Spielprofil entfällt der Abschnitt.
    /// </summary>
    private void AddSaveSection(Controls.Panel panel, ErrorEntry entry)
    {
        if (!_service.Host.HasSaveGames || entry.Name is not { } name || entry.Kind is not { } kind || !kind.Fixes.Contains("saveCheck"))
        {
            return;
        }

        AddHeading(panel, ErrorLogTexts.SaveHeading);
        if (_service.Host.Save() is { } save)
        {
            panel.Children.Add(Text(ErrorLogTexts.EvidenceLine(SaveEvidence.For(save, name, kind.SaveNames ?? "variable"))));
            var show = SmallButton(ErrorLogTexts.SaveShow);
            show.HorizontalAlignment = HorizontalAlignment.Left;
            show.Click += (_, _) => _service.Host.ShowInSaveExplorer(name);
            panel.Children.Add(show);
            return;
        }

        var check = SmallButton(ErrorLogTexts.SaveCheck);
        check.HorizontalAlignment = HorizontalAlignment.Left;
        check.Click += (_, _) =>
        {
            var index = panel.Children.IndexOf(check);
            panel.Children.RemoveAt(index);
            panel.Children.Insert(index, Muted(_service.Host.LoadNewestSave() ? ErrorLogTexts.SaveLoading : SaveExplorerTexts.DebugHint));
        };
        panel.Children.Add(check);
    }

    /// <summary>"set_variable auskommentieren": Dialog mit Vorschau, danach Schreiben mit Sicherung und einem Schritt "Rückgängig".</summary>
    /// <summary>Liste "Aktive Spuren": Mod, Datei, Zeile, Treffer; beschädigte Spuren markiert. Die Auswahl bleibt über Neuaufbauten erhalten.</summary>
    private void RefreshActiveTraces()
    {
        var session = _service.Session;
        var selected = _traceList.SelectedItems.OfType<Controls.ListBoxItem>().Select(i => i.Tag).OfType<ActiveTrace>().Select(t => (t.Path, t.Id)).ToHashSet();
        _traceList.Items.Clear();
        foreach (var trace in session.Traces)
        {
            var text = ErrorLogTexts.ActiveTraceLine(trace.Mod.Name, System.IO.Path.GetRelativePath(trace.Mod.Directory, trace.Path), trace.Line, session.HitCountOf(trace));
            var item = new Controls.ListBoxItem { Content = new Controls.TextBlock { Text = trace.Broken ? $"{text}  ({ErrorLogTexts.TraceBroken})" : text }, Tag = trace, ToolTip = trace.Path };
            _traceList.Items.Add(item);
            item.IsSelected = selected.Contains((trace.Path, trace.Id));
        }

        _activeTraces.Header = new Controls.TextBlock { Text = $"{ErrorLogTexts.ActiveTracesHeading} ({session.Traces.Count})", FontWeight = FontWeights.SemiBold };
        _activeTraces.Visibility = session.Traces.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _removeTrace.IsEnabled = _traceList.SelectedItems.Count > 0;
    }

    /// <summary>Spuren entfernen: Rückfrage mit den Dateien, dann wie alle Korrekturen mit Sicherung und einem Schritt "Rückgängig".</summary>
    private void RemoveTraces(IReadOnlyList<ActiveTrace> traces)
    {
        if (traces.Count == 0)
        {
            return;
        }

        var plans = TraceMarkers.PlanRemove(traces);
        var files = string.Join("\n", plans.SelectMany(p => p.Files.Select(f => $"{p.ModName}: {System.IO.Path.GetRelativePath(p.Folder, f.Path)}")));
        if (MessageBox.Show(this, ErrorLogTexts.RemoveTracesConfirm(traces.Count, files), ErrorLogTexts.ActiveTracesHeading, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var outcome = _service.Host.ApplyTraceEdits(
            plans,
            new ModEditTexts(ErrorLogTexts.RemoveTracesAction(traces.Count), ErrorLogTexts.RemoveTracesSnapshotNote, ErrorLogTexts.TraceNothing, ErrorLogTexts.RemoveTracesFailed, ErrorLogTexts.RemoveTracesDone));
        MessageBox.Show(this, outcome.Message, ErrorLogTexts.ActiveTracesHeading, MessageBoxButton.OK, outcome.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        _ = _service.RefreshTracesAsync();
    }

    /// <summary>Scope-Verfolgung: "Ausgelöst von" mit den Scopes aus debug.log; Länder, Charaktere und Orte öffnen den Spielstand-Explorer.</summary>
    private void AddTrace(Controls.Panel panel, ErrorEntry entry)
    {
        // debug.log gehört nur zum aktuellen Spiellauf: Für ältere Läufe gibt es keinen Befund.
        if (_service.CurrentRunPosition != 0 || _service.CurrentRun is not { IsCurrent: true } || _service.Session.TraceResultFor(entry) is not { } result)
        {
            return;
        }

        AddHeading(panel, result.Kind == TraceMatchKind.Candidates ? ErrorLogTexts.TraceCandidatesHeading : ErrorLogTexts.TraceHeading);
        if (result.Scopes.Count == 0)
        {
            panel.Children.Add(Muted(ErrorLogTexts.TraceNoHits));
            return;
        }

        var saveLoaded = _service.Host.Save() is not null;
        foreach (var scope in result.Scopes)
        {
            var text = scope.Root is { } root
                ? ErrorLogTexts.TraceScopeLine(scope.Scope.ToString(), root.ToString(), scope.Count)
                : ErrorLogTexts.TraceScopeOnly(scope.Scope.ToString(), scope.Count);
            var block = new Controls.TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 0, 0, 2) };
            if (saveLoaded && scope.Scope.Kind is "Country" or "Character" or "Location")
            {
                var link = new Hyperlink(new Run(text));
                var target = scope.Scope;
                link.Click += (_, _) =>
                {
                    if (!_service.Host.ShowSaveObject(target.Kind, target.Id))
                    {
                        MessageBox.Show(this, ErrorLogTexts.TraceNoSaveObject, ErrorLogTexts.TraceDialogTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                };
                block.Inlines.Add(link);
            }
            else
            {
                block.Text = text;
            }

            panel.Children.Add(block);
            if (scope.Saved.Count > 0)
            {
                var saved = new Controls.StackPanel();
                foreach (var (name, target) in scope.Saved)
                {
                    saved.Children.Add(new Controls.TextBlock { Text = $"{name}: {target}", TextWrapping = TextWrapping.Wrap });
                }

                panel.Children.Add(new Controls.Expander { Header = ErrorLogTexts.TraceSaved, Content = saved, Margin = new Thickness(24, 0, 0, 4) });
            }
        }
    }

    /// <summary>"Scope verfolgen": gesperrt mit Grund, wenn an der Stelle keine Spur geht; sonst Vorschau, Prüfung und Schreiben.</summary>
    private void ConfigureTrace(Controls.Button button, ErrorEntry entry)
    {
        var session = _service.Session;
        var attribution = session.AttributionOf(entry);
        string? reason = null;
        if (attribution.Mod is not { IsOwn: true } mod || attribution.Source is not { } source || entry.FilePath is null || entry.FileLine is not int line)
        {
            reason = ErrorLogTexts.TraceNotOwn;
        }
        else
        {
            var probe = TraceMarkers.PlanInsert(mod, source.FullPath, entry.FilePath, line, entry.Detail, entry.Kind?.TraceGuards ?? Array.Empty<TraceGuard>(), session.Catalog.Trace, "0000");
            reason = probe.Plan.Status switch
            {
                TraceStatus.Ok => null,
                TraceStatus.NoEffectContext => ErrorLogTexts.TraceNoEffectContext,
                TraceStatus.AlreadyTraced => ErrorLogTexts.TraceAlready,
                _ => ErrorLogTexts.TraceLineMissing,
            };
            if (reason is null)
            {
                button.Click += (_, _) => InsertTrace(entry, mod, source.FullPath, line);
                return;
            }
        }

        button.IsEnabled = false;
        button.ToolTip = reason;
        Controls.ToolTipService.SetShowOnDisabled(button, true);
    }

    private void InsertTrace(ErrorEntry entry, ModSource mod, string path, int line)
    {
        var session = _service.Session;
        var guards = entry.Kind?.TraceGuards ?? Array.Empty<TraceGuard>();
        var id = TraceMarkers.NewId(session.Traces.Select(t => t.Id).Concat(session.TraceHitIds), Random.Shared);
        var insert = TraceMarkers.PlanInsert(mod, path, entry.FilePath!, line, entry.Detail, guards, session.Catalog.Trace, id);
        if (insert.Edit is not { } edit)
        {
            MessageBox.Show(this, ErrorLogTexts.TraceNothing, ErrorLogTexts.TraceDialogTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var file = System.IO.Path.GetRelativePath(mod.Directory, path);
        if (!TraceDialog.Ask(this, file, insert.Plan, edit.Files[0].Lines[0], TracePlanner.GuardFor(entry.Detail, guards), _service.Host.IsGameRunning()))
        {
            return;
        }

        if (!TraceMarkers.IsCurrent(insert))
        {
            MessageBox.Show(this, ErrorLogTexts.TraceFileChanged, ErrorLogTexts.TraceDialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var outcome = _service.Host.ApplyTraceEdits(
            new[] { edit },
            new ModEditTexts(ErrorLogTexts.TraceAction(id), ErrorLogTexts.TraceSnapshotNote, ErrorLogTexts.TraceNothing, ErrorLogTexts.TraceFailed, ErrorLogTexts.TraceDone));
        MessageBox.Show(this, outcome.Message, ErrorLogTexts.TraceDialogTitle, MessageBoxButton.OK, outcome.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        _ = _service.RefreshTracesAsync();
    }

    private void CommentOut(ErrorEntry entry)
    {
        if (entry.Name is not { } name || entry.Kind is not { SetEffects.Count: > 0 } kind)
        {
            return;
        }

        if (CommentOutDialog.Ask(this, name, kind.SetEffects, OwnModsWith(entry, name), _service.Session.Index, _service.Host.SnapshotDirectoryFor) is not { } plans)
        {
            return;
        }

        var outcome = _service.Host.ApplyCommentOut(plans, name, kind.SetEffects[0]);
        MessageBox.Show(this, outcome.Message, ErrorLogTexts.Fix("commentOutSet", kind), MessageBoxButton.OK, outcome.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void RenameEverywhere(ErrorEntry entry, IReadOnlyList<string> candidates)
    {
        var session = _service.Session;
        if (entry.Name is not { } name)
        {
            return;
        }

        var mods = OwnModsWith(entry, name);
        if (RenameDialog.Ask(this, name, mods, candidates, _service.Host.SnapshotDirectoryFor, n => session.Index.Find(n).Count > 0) is not { } result)
        {
            return;
        }

        var outcome = _service.Host.ApplyRename(result.Plans, name, result.NewName);
        MessageBox.Show(this, outcome.Message, ErrorLogTexts.Fix("renameEverywhere"), MessageBoxButton.OK, outcome.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    /// <summary>Plan-3-Rulings 6 und 7: nach Bestätigung ins gewählte Playset.</summary>
    private void AddModToPlayset(ModSource mod)
    {
        if (_service.PlaysetName is not { } playset)
        {
            MessageBox.Show(this, ErrorLogTexts.AddModNoPlayset, ErrorLogTexts.AddModTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // launcher-v2.sqlite liest PMT nur: gleich den Hinweis zeigen statt erst eine Bestätigung, die von playsets.json spricht.
        if (PlaysetStore.IsLauncherDatabase(_service.Host.PlaysetsFile()))
        {
            MessageBox.Show(this, PlaysetTexts.LauncherDatabaseReadOnly, ErrorLogTexts.AddModTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, ErrorLogTexts.AddModConfirm(mod.Name, playset), ErrorLogTexts.AddModTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var outcome = _service.Host.AddModToPlayset(mod);
        MessageBox.Show(this, outcome.Message, ErrorLogTexts.AddModTitle, MessageBoxButton.OK, outcome.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void AddSuppression(Controls.Panel panel, ErrorSection section, ErrorEntry entry, string kindName)
    {
        var session = _service.Session;
        if (section.Kind == ErrorSectionKind.Suppressed)
        {
            AddHeading(panel, ErrorLogTexts.SuppressHeading);
            if (ErrorLogSession.IsDefaultSuppressed(entry))
            {
                panel.Children.Add(Muted(ErrorLogTexts.DefaultSuppressed));
                return;
            }

            var restore = SmallButton(ErrorLogTexts.Restore);
            restore.Click += (_, _) => Save(() => session.Restore(entry));
            panel.Children.Add(restore);
            return;
        }

        var options = session.SuppressionOptions(entry);
        if (options.Count == 0)
        {
            return;
        }

        AddHeading(panel, ErrorLogTexts.SuppressHeading);
        var modName = session.AttributionOf(entry).Mod?.Name;
        var buttons = new Controls.WrapPanel();
        foreach (var option in options)
        {
            var button = SmallButton(ErrorLogTexts.SuppressOption(option, kindName, modName));
            button.Click += (_, _) => Save(() => session.Suppress(option));
            buttons.Children.Add(button);
        }

        panel.Children.Add(buttons);
    }

    private void Save(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ErrorLogTexts.SuppressFailed(ex.Message), ErrorLogTexts.Menu, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ChooseFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = ErrorLogTexts.ChooseFolderTitle,
            UseDescriptionForTitle = true,
            SelectedPath = ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(_service.LogFolder ?? string.Empty),
        };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("errorlog.folder");
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _service.SetLogFolder(dialog.SelectedPath);
        }
    }

    private void ExportList()
    {
        var dialog = new SaveFileDialog { Title = ErrorLogTexts.ExportTitle, Filter = ErrorLogTexts.ExportFilter, FileName = "errorlog" };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("errorlog.export");
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var format = dialog.FilterIndex == 2 || dialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? ExportFormat.Csv : ExportFormat.Text;
        var session = _service.Session;
        try
        {
            var runName = _service.CurrentRun is { } run ? ErrorLogTexts.RunText(run) : null;
            var text = ErrorLogExport.Write(_sections, format, session.Language, runName, session.HasComparison, session.RunFileNames);
            File.WriteAllText(dialog.FileName, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ErrorLogTexts.ExportFailed(ex.Message), ErrorLogTexts.Menu, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private Controls.TextBlock Link(string text, string path, int line)
    {
        var link = new Hyperlink(new Run(text));
        link.Click += (_, _) => _service.Host.JumpTo(path, line);
        var block = new Controls.TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 0, 0, 2) };
        block.Inlines.Add(link);
        return block;
    }

    private string OccurrenceText(ErrorEntry entry) => ErrorLogTexts.LogPosition(entry, _service.Session.RunFileNames);

    private static object? Selected(Controls.ComboBox combo) => (combo.SelectedItem as Controls.ComboBoxItem)?.Tag;

    private static void Fill(Controls.ComboBox combo, IEnumerable<(string Text, object? Tag)> items, object? selected)
    {
        combo.Items.Clear();
        foreach (var (text, tag) in items)
        {
            var item = new Controls.ComboBoxItem { Content = text, Tag = tag };
            combo.Items.Add(item);
            if (Equals(tag, selected))
            {
                combo.SelectedItem = item;
            }
        }
    }

    private static Controls.TextBlock Excerpt(IReadOnlyList<ExcerptLine> lines)
    {
        var text = new Controls.TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 12 };
        foreach (var line in lines)
        {
            var run = new Run($"{line.Number,5}  {line.Text.Replace("\t", "    ")}");
            if (line.IsTarget)
            {
                run.FontWeight = FontWeights.Bold;
                run.SetResourceReference(TextElement.BackgroundProperty, "Theme.Selection");
                run.SetResourceReference(TextElement.ForegroundProperty, "Theme.SelectionText");
            }

            text.Inlines.Add(run);
            text.Inlines.Add(new LineBreak());
        }

        return text;
    }

    private static Controls.CheckBox Toggle(string text) =>
        new() { Content = text, IsChecked = true, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };

    private static Controls.TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    private static Controls.TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2) };

    private static Controls.TextBlock Muted(string text)
    {
        var block = Text(text);
        block.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        return block;
    }

    /// <summary>Knopf mit TextBlock als Inhalt: Namen wie <c>cmf_suppress</c> verlieren sonst den Unterstrich (Zugriffstaste).</summary>
    private static Controls.Button SmallButton(string text) =>
        new() { Content = new Controls.TextBlock { Text = text }, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 6, 4) };

    private static void AddHeading(Controls.Panel panel, string text) =>
        panel.Children.Add(new Controls.TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 2) });

    private sealed record GroupItem(ErrorSection Section, ErrorGroupView Group);
}
