using System.IO;
using System.Windows;
using Binding = System.Windows.Data.Binding;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.SaveGames;
using ParadoxModTool.Session;
using ParadoxModTool.Settings;
using ParadoxModTool.Editor.Controls;
using Controls = System.Windows.Controls;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// Fenster "Spielstand-Explorer" (Spec, Oberfläche): Kopfzeile mit Stand, Vergleich, Suche und Filtern, Reiter "Variablen" (Namen links,
/// Objekte mit Werten rechts) und "Objekte" (Baum links, Variablen rechts). Zeigt nur die Sitzung aus <see cref="SaveExplorerService"/> an;
/// Schließen beendet die Sitzung nicht. Ohne Besitzer ist es ein eigenständiges Fenster (eigene Einstellung).
/// </summary>
public sealed class SaveExplorerWindow : Window
{
    private readonly SaveExplorerService _service;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly DispatcherTimer _indexPoll = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly SearchableChoice _saves = new(90, 420) { Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.CheckBox _compare = new() { Content = SaveExplorerTexts.Compare, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
    private readonly SearchableChoice _comparison = new(90, 420) { Margin = new Thickness(4, 0, 12, 0), IsEnabled = false };
    private readonly Controls.Button _reload = new() { Content = SaveExplorerTexts.Reload, ToolTip = SaveExplorerTexts.ReloadTip, Padding = new Thickness(8, 1, 8, 1) };
    private readonly Controls.TextBox _search = new() { Width = 220, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.CheckBox _ownOnly = new() { Content = SaveExplorerTexts.OwnOnly, IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
    private readonly Controls.CheckBox _changesOnly = new() { Content = SaveExplorerTexts.ChangesOnly, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly Controls.Border _newer = new() { Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _newerText = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
    private readonly Controls.Button _loadNewer = new() { Content = SaveExplorerTexts.Load, Padding = new Thickness(8, 0, 8, 0) };
    private readonly Controls.Border _notice = new() { Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _noticeText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Controls.StackPanel _progressRow = new() { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.ProgressBar _progress = new() { Width = 180, Height = 14, Maximum = 1 };
    private readonly Controls.TextBlock _progressText = new() { Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.Button _cancel = new() { Content = SaveExplorerTexts.Cancel, Padding = new Thickness(8, 0, 8, 0) };
    private readonly Controls.TextBlock _empty = new() { FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 30, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 700 };
    private readonly Controls.TabControl _tabs = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly Controls.ListView _names = new();
    private readonly Controls.ListView _nameValues = new();
    private readonly Controls.TreeView _objects = new();
    private readonly SearchableChoice _tagFilter = new(70, 140) { Margin = new Thickness(4, 0, 0, 0) };
    private IReadOnlyList<string> _tags = Array.Empty<string>();
    private readonly Controls.ListView _objectValues = new();
    private readonly Controls.Button _nameCode = CodeButton();
    private readonly Controls.Button _objectCode = CodeButton();
    private readonly Controls.Button _nameErrorLog = new() { Content = SaveExplorerTexts.ShowInErrorLog, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 6), IsEnabled = false };
    private IReadOnlyDictionary<string, string> _errorIcons = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Controls.TextBlock _summary = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly HashSet<int> _expandedKinds = new() { 0 };
    private readonly Dictionary<string, bool> _ownCache = new(StringComparer.Ordinal);
    private readonly Dictionary<Controls.ListView, (string Key, bool Descending)> _sort = new();
    private SymbolIndex? _ownIndex;
    private SaveView? _shownView;
    private bool? _columnsForComparison;
    private bool _refreshPending;
    private string? _loadingName;
    private string? _pendingName;
    private string? _selectedOwner;

    public SaveExplorerWindow(SaveExplorerService service, AppSettings settings, Window? owner, Func<double, double, WindowBoundsState?> otherScreen)
    {
        _service = service;
        _settings = settings;
        Title = SaveExplorerTexts.WindowTitle(service.Host.GameName);
        Width = 1250;
        Height = 800;
        MinWidth = 760;
        MinHeight = 420;
        if (owner is not null)
        {
            Owner = owner;
        }

        ShowInTaskbar = owner is null;
        WindowBoundsPersistence.Apply(this, _settings.SaveExplorerWindowBounds, owner is null ? otherScreen(Width, Height) : null);
        foreach (var border in new[] { _newer, _notice })
        {
            border.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.NoticeBackground");
        }

        _summary.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _empty.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        Controls.ToolTipService.SetShowOnDisabled(_ownOnly, true);
        Controls.VirtualizingPanel.SetIsVirtualizing(_objects, true);
        Controls.VirtualizingPanel.SetVirtualizationMode(_objects, Controls.VirtualizationMode.Recycling);

        var top = new Controls.StackPanel();
        var header = new Controls.WrapPanel();
        header.Children.Add(Label(SaveExplorerTexts.SaveLabel));
        header.Children.Add(_saves);
        header.Children.Add(_compare);
        header.Children.Add(Label(SaveExplorerTexts.ComparisonLabel));
        header.Children.Add(_comparison);
        header.Children.Add(_reload);
        top.Children.Add(header);

        var filters = new Controls.WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        filters.Children.Add(Label(SaveExplorerTexts.SearchLabel));
        filters.Children.Add(_search);
        filters.Children.Add(_ownOnly);
        filters.Children.Add(_changesOnly);
        top.Children.Add(filters);

        var newerPanel = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        newerPanel.Children.Add(_newerText);
        newerPanel.Children.Add(_loadNewer);
        _newer.Child = newerPanel;
        top.Children.Add(_newer);
        _notice.Child = _noticeText;
        top.Children.Add(_notice);
        _progressRow.Children.Add(_progress);
        _progressRow.Children.Add(_progressText);
        _progressRow.Children.Add(_cancel);
        top.Children.Add(_progressRow);

        var nameButtons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        nameButtons.Children.Add(_nameCode);
        nameButtons.Children.Add(_nameErrorLog);
        _tabs.Items.Add(new Controls.TabItem { Header = SaveExplorerTexts.VariablesTab, Content = Split(_names, ValuePane(nameButtons, _nameValues)) });
        var tagRow = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        tagRow.Children.Add(Label(SaveExplorerTexts.CountryFilter));
        tagRow.Children.Add(_tagFilter);
        var objectPane = new Controls.DockPanel();
        Controls.DockPanel.SetDock(tagRow, Controls.Dock.Top);
        objectPane.Children.Add(tagRow);
        objectPane.Children.Add(_objects);
        _tabs.Items.Add(new Controls.TabItem { Header = SaveExplorerTexts.ObjectsTab, Content = Split(objectPane, ValuePane(_objectCode, _objectValues)) });

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        Controls.DockPanel.SetDock(top, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_summary, Controls.Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(_summary);
        var body = new Controls.Grid();
        body.Children.Add(_tabs);
        body.Children.Add(_empty);
        root.Children.Add(body);
        Content = root;

        _saves.SelectionChanged += (_, _) =>
        {
            if (_saves.SelectedTag is SaveFileInfo file && !SamePath(file, _service.Session.CurrentFile))
            {
                Load(file);
            }
        };
        _compare.Click += (_, _) => OnCompareToggled();
        _comparison.SelectionChanged += (_, _) => SetComparison(_comparison.SelectedTag as SaveFileInfo);
        _reload.Click += (_, _) =>
        {
            _service.Session.RefreshSaves();
            if (_service.Session.CurrentFile is { } current)
            {
                Load(_service.Session.Saves.FirstOrDefault(s => SamePath(s, current)) ?? current);
            }
        };
        _loadNewer.Click += (_, _) =>
        {
            if (_service.Session.NewerSave() is { } newer)
            {
                Load(newer);
            }
        };
        _cancel.Click += (_, _) => _service.Session.Cancel();
        _search.TextChanged += (_, _) =>
        {
            _searchDelay.Stop();
            _searchDelay.Start();
        };
        _searchDelay.Tick += (_, _) =>
        {
            _searchDelay.Stop();
            RefreshContent(true);
        };
        _ownOnly.Click += (_, _) => RefreshContent(true);
        _changesOnly.Click += (_, _) => RefreshContent(true);
        _names.SelectionChanged += (_, _) => RefreshNameValues();
        _objects.SelectedItemChanged += (_, _) => OnObjectSelected();
        _objects.KeyDown += (_, e) =>
        {
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && _objects.SelectedItem is Controls.TreeViewItem { Tag: SaveObjectRow { Object.Id: { } id } })
            {
                CopyText(id);
                e.Handled = true;
            }
        };
        _tagFilter.SelectionChanged += (_, _) => RefreshContent(true);
        _nameCode.Click += (_, _) => ShowInCode(_nameCode, (_names.SelectedItem as NameItem)?.Row.Name);
        _nameErrorLog.Click += (_, _) =>
        {
            if (_names.SelectedItem is NameItem { ErrorIcon.Length: > 0 } item)
            {
                _service.Host.ShowInErrorLog(item.Row.Name);
            }
        };
        _objectCode.Click += (_, _) => ShowInCode(_objectCode, (_objectValues.SelectedItem as ValueItem)?.Row.Name);
        _objectValues.SelectionChanged += (_, _) => RefreshCodeButtons();
        foreach (var list in new[] { _nameValues, _objectValues })
        {
            list.AddHandler(Controls.GridViewColumnHeader.ClickEvent, new RoutedEventHandler((_, e) => OnHeaderClick(list, e)));
        }

        _indexPoll.Tick += (_, _) =>
        {
            if (_service.Host.Index() is not null)
            {
                _indexPoll.Stop();
                RefreshContent(true);
            }
        };

        _service.Session.Changed += OnSessionChanged;
        _service.NewerSaveAvailable += OnNewerSave;
        Loaded += (_, _) => LoadNewestIfEmpty();
        Closing += (_, _) => SaveBounds();
        Closed += (_, _) =>
        {
            _searchDelay.Stop();
            _indexPoll.Stop();
            _service.Session.Changed -= OnSessionChanged;
            _service.NewerSaveAvailable -= OnNewerSave;
        };

        RefreshHeader();
        RefreshContent(true);
    }

    /// <summary>Merkt Position und Größe (auch das Hauptfenster ruft das vor dem Speichern der Einstellungen auf).</summary>
    public void SaveBounds()
    {
        if (WindowBoundsPersistence.Capture(this) is { } bounds)
        {
            _settings.SaveExplorerWindowBounds = bounds;
        }
    }

    /// <summary>Reiter "Variablen" mit diesem Namen (für das Fehlerprotokoll, Plan 2b): Suche auf den Namen, erste passende Zeile gewählt.</summary>
    public void ShowName(string name)
    {
        _tabs.SelectedIndex = 0;
        _pendingName = name;
        _searchDelay.Stop();
        _search.Text = name;
        _searchDelay.Stop();
        RefreshContent(true);
    }

    /// <summary>
    /// Reiter "Objekte" auf einem Objekt aus einem Scope-Befund (Scope-Verfolgung): Land über den Tag, Charakter und Ort über die Id.
    /// <see langword="false"/>, wenn kein Stand geladen ist oder das Objekt im Stand keine Variablen hat (dann steht es nicht in der Liste).
    /// </summary>
    public bool ShowObject(string kind, string idOrTag)
    {
        var prefix = kind switch
        {
            "Country" => "countries/",
            "Character" => "character_db/",
            "Location" => "locations/",
            _ => null,
        };
        if (prefix is null || _service.Session.View is not { } view)
        {
            return false;
        }

        var row = SaveExplorerQuery.Objects(view, new SaveFilter(string.Empty, null, false)).FirstOrDefault(r =>
            r.Object.OwnerPath.StartsWith(prefix, StringComparison.Ordinal)
            && (kind == "Country" ? r.Object.DisplayName == idOrTag || r.Object.Tag == idOrTag : r.Object.Id == idOrTag));
        if (row is null)
        {
            return false;
        }

        _tabs.SelectedIndex = 1;
        _searchDelay.Stop();
        _search.Text = string.Empty;
        _searchDelay.Stop();
        _selectedOwner = row.Object.OwnerPath;
        RefreshContent(true);
        return true;
    }

    private void LoadNewestIfEmpty()
    {
        if (_service.Session.CurrentFile is null && !_service.Session.IsLoading && _service.Session.Saves.FirstOrDefault(s => s.Readable) is { } newest)
        {
            Load(newest);
        }
    }

    private void Load(SaveFileInfo file)
    {
        _loadingName = Path.GetFileName(file.Path);
        _ = _service.Session.LoadAsync(file, Progress());
    }

    private void SetComparison(SaveFileInfo? file)
    {
        _loadingName = file is null ? null : Path.GetFileName(file.Path);
        _ = _service.Session.SetComparisonAsync(file, Progress());
    }

    private IProgress<double> Progress() => new Progress<double>(fraction =>
    {
        _progress.Value = fraction;
        _progressText.Text = SaveExplorerTexts.Loading(_loadingName ?? string.Empty, fraction);
    });

    private void OnCompareToggled()
    {
        var session = _service.Session;
        if (_compare.IsChecked == true)
        {
            _comparison.IsEnabled = true;
            if (session.CurrentFile is { } current && SaveFiles.DefaultComparison(session.Saves, current) is { } suggestion)
            {
                SetComparison(suggestion);
            }
        }
        else
        {
            _comparison.IsEnabled = false;
            SetComparison(null);
        }
    }

    private void OnNewerSave(object? sender, EventArgs e) => RefreshHeader();

    /// <summary>Laden meldet mehrere Änderungen hintereinander; Kopf und Inhalt werden einmal danach neu aufgebaut.</summary>
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
            RefreshHeader();
            RefreshContent(false);
        }));
    }

    private void RefreshHeader()
    {
        var session = _service.Session;
        _saves.SetItems(
            session.Saves.Select(save => new ChoiceItem(SaveEntryText(save), save, save.Readable, save.Readable ? save.Path : SaveExplorerTexts.DebugHint)).ToList(),
            session.Saves.FirstOrDefault(s => SamePath(s, session.CurrentFile)));

        var comparisons = new List<ChoiceItem> { new(SaveExplorerTexts.NoComparison, null) };
        comparisons.AddRange(session.Saves.Where(s => s.Readable && !SamePath(s, session.CurrentFile)).Select(save => new ChoiceItem(SaveEntryText(save), save)));
        _comparison.SetItems(comparisons, session.Saves.FirstOrDefault(s => SamePath(s, session.ComparisonFile)), pinned: 1);

        if (session.ComparisonFile is not null)
        {
            _compare.IsChecked = true;
        }

        _comparison.IsEnabled = _compare.IsChecked == true;

        var newer = session.NewerSave();
        _newerText.Text = newer is null ? string.Empty : SaveExplorerTexts.Newer(Path.GetFileName(newer.Path));
        _newer.Visibility = newer is null ? Visibility.Collapsed : Visibility.Visible;

        var notice = SaveExplorerTexts.Notice(session.Notice, session.Folder, session.NoticeDetail);
        _noticeText.Text = notice;
        _notice.Visibility = notice.Length > 0 && session.View is not null ? Visibility.Visible : Visibility.Collapsed;
        _empty.Text = notice.Length > 0 ? notice : session.IsLoading ? string.Empty : SaveExplorerTexts.DebugHint;

        _progressRow.Visibility = session.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        if (!session.IsLoading)
        {
            _progress.Value = 0;
        }

        _changesOnly.Visibility = session.View?.Comparison is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Baut Namen, Objekte und Werte neu auf; ohne <paramref name="force"/> nur, wenn sich der angezeigte Stand geändert hat.</summary>
    private void RefreshContent(bool force)
    {
        var view = _service.Session.View;
        RefreshOwnState();
        _tabs.Visibility = view is null ? Visibility.Collapsed : Visibility.Visible;
        _empty.Visibility = view is null ? Visibility.Visible : Visibility.Collapsed;
        _summary.Visibility = view is null ? Visibility.Collapsed : Visibility.Visible;
        if (view is null)
        {
            _shownView = null;
            _names.ItemsSource = null;
            _objects.Items.Clear();
            return;
        }

        if (!force && ReferenceEquals(view, _shownView))
        {
            return;
        }

        _shownView = view;
        EnsureColumns(view.Comparison is not null);
        var filter = CurrentFilter(view);

        var names = RefreshNames(view, filter);
        FillTags(view);
        var objects = SaveExplorerQuery.Objects(view, filter with { Tag = _tagFilter.SelectedTag as string });
        RebuildTree(objects);
        _summary.Text = SaveExplorerTexts.Summary(names.Count, objects.Count);
        RefreshNameValues();
        RefreshObjectValues();
    }

    /// <summary>Fehlerprotokoll-Symbole neu (nach jeder Änderung der Fehlerprotokoll-Sitzung); nur die Namensliste wird neu aufgebaut.</summary>
    public void RefreshErrorMarks()
    {
        if (_shownView is { } view)
        {
            RefreshNames(view, CurrentFilter(view));
        }
    }

    /// <summary>Namensliste mit Fehlerprotokoll-Symbolen; die Auswahl bleibt erhalten (bzw. springt auf einen angeforderten Namen).</summary>
    private List<NameItem> RefreshNames(SaveView view, SaveFilter filter)
    {
        _errorIcons = _service.Host.ErrorIcons();
        var previous = (_names.SelectedItem as NameItem)?.Row;
        var names = SaveExplorerQuery.Names(view, filter).Select(row => NameItem.From(row, _errorIcons.GetValueOrDefault(row.Name) ?? string.Empty)).ToList();
        _names.ItemsSource = names;
        var wanted = _pendingName is { } pending
            ? names.FirstOrDefault(n => n.Row.Name == pending)
            : previous is null ? null : names.FirstOrDefault(n => n.Row.Name == previous.Name && n.Row.Kind == previous.Kind);
        _pendingName = null;
        _names.SelectedItem = wanted;
        if (wanted is not null)
        {
            _names.ScrollIntoView(wanted);
        }

        return names;
    }

    private SaveFilter CurrentFilter(SaveView view) =>
        new(_search.Text.Trim(), OwnPredicate(), view.Comparison is not null && _changesOnly.IsChecked == true);

    /// <summary>"Nur Namen aus meinen Mods": ein Name zählt als eigener, wenn der Symbolindex ihn in einer eigenen Mod findet.</summary>
    private Func<string, bool>? OwnPredicate()
    {
        if (_ownOnly.IsChecked != true || _service.Host.Index() is not { } index)
        {
            return null;
        }

        if (!ReferenceEquals(index, _ownIndex))
        {
            _ownIndex = index;
            _ownCache.Clear();
        }

        return name =>
        {
            if (!_ownCache.TryGetValue(name, out var own))
            {
                own = index.Find(name).Any(o => o.Mod?.IsOwn == true);
                _ownCache[name] = own;
            }

            return own;
        };
    }

    /// <summary>Ohne Symbolindex sind Filter und "Im Code zeigen" aus; der Explorer prüft alle 2 s, ob der Index fertig ist.</summary>
    private void RefreshOwnState()
    {
        var ready = _service.Host.Index() is not null;
        _ownOnly.IsEnabled = ready;
        _ownOnly.ToolTip = ready ? SaveExplorerTexts.OwnOnlyTip : SaveExplorerTexts.OwnOnlyWaiting;
        if (!ready && IsLoaded && !_indexPoll.IsEnabled)
        {
            _indexPoll.Start();
        }

        RefreshCodeButtons();
    }

    private void RefreshCodeButtons()
    {
        var ready = _service.Host.Index() is not null;
        _nameCode.IsEnabled = ready && _names.SelectedItem is NameItem;
        _nameErrorLog.IsEnabled = _names.SelectedItem is NameItem { ErrorIcon.Length: > 0 };
        _objectCode.IsEnabled = ready && _objectValues.SelectedItem is ValueItem;
        _nameCode.ToolTip = _objectCode.ToolTip = ready ? SaveExplorerTexts.ShowInCodeTip : SaveExplorerTexts.OwnOnlyWaiting;
    }

    private void RefreshNameValues()
    {
        RefreshCodeButtons();
        if (_shownView is not { } view || _names.SelectedItem is not NameItem item)
        {
            _nameValues.ItemsSource = null;
            return;
        }

        var rows = SaveExplorerQuery.ValuesForName(view, item.Row.Name, item.Row.Kind, CurrentFilter(view).OnlyChanges);
        _nameValues.ItemsSource = Sorted(_nameValues, rows.Select(ValueItem.From));
    }

    private void OnObjectSelected()
    {
        if (_objects.SelectedItem is Controls.TreeViewItem { Tag: SaveObjectRow row })
        {
            _selectedOwner = row.Object.OwnerPath;
        }

        RefreshObjectValues();
    }

    private void RefreshObjectValues()
    {
        if (_shownView is not { } view || _objects.SelectedItem is not Controls.TreeViewItem { Tag: SaveObjectRow row })
        {
            _objectValues.ItemsSource = null;
            RefreshCodeButtons();
            return;
        }

        var rows = SaveExplorerQuery.ValuesForObject(view, row.Object.OwnerPath, CurrentFilter(view));
        _objectValues.ItemsSource = Sorted(_objectValues, rows.Select(ValueItem.From));
        RefreshCodeButtons();
    }

    /// <summary>Baum nach Objektart; Kinder entstehen erst beim Aufklappen (bis zu mehreren tausend Objekten je Art).</summary>
    private void RebuildTree(IReadOnlyList<SaveObjectRow> rows)
    {
        _objects.Items.Clear();
        foreach (var group in rows.GroupBy(r => r.Object.Kind).OrderBy(g => g.Key.Order))
        {
            var kind = group.Key;
            var children = group.ToList();
            var node = new Controls.TreeViewItem { Header = new Controls.TextBlock { Text = $"{kind.Label} ({children.Count})" }, Tag = kind };
            node.Items.Add(new Controls.TreeViewItem());
            node.Expanded += (_, e) =>
            {
                if (ReferenceEquals(e.OriginalSource, node))
                {
                    _expandedKinds.Add(kind.Order);
                    Fill(node, children);
                }
            };
            node.Collapsed += (_, e) =>
            {
                if (ReferenceEquals(e.OriginalSource, node))
                {
                    _expandedKinds.Remove(kind.Order);
                }
            };
            _objects.Items.Add(node);
            if (_expandedKinds.Contains(kind.Order) || children.Any(c => c.Object.OwnerPath == _selectedOwner))
            {
                node.IsExpanded = true;
            }
        }
    }

    private void Fill(Controls.TreeViewItem node, IReadOnlyList<SaveObjectRow> children)
    {
        if (node.Items.Count == 1 && node.Items[0] is Controls.TreeViewItem { Tag: null })
        {
            node.Items.Clear();
            foreach (var child in children)
            {
                var text = child.Change is SaveChange change && change != SaveChange.Unchanged
                    ? $"{child.Object.DisplayName}  ({SaveExplorerTexts.Change(change)})"
                    : child.Object.DisplayName;
                if (child.Object.Tag is { } tag && tag != child.Object.DisplayName)
                {
                    text += $" [{tag}]";
                }

                var item = new Controls.TreeViewItem { Header = new Controls.TextBlock { Text = text }, Tag = child, ToolTip = child.Object.OwnerPath, ContextMenu = CopyMenu(child.Object) };
                node.Items.Add(item);
                if (child.Object.OwnerPath == _selectedOwner)
                {
                    item.IsSelected = true;
                }
            }
        }
    }

    /// <summary>Auswahl "Land:" mit "Alle Länder" und den Tags des Stands; die Wahl bleibt erhalten, solange es den Tag noch gibt.</summary>
    private void FillTags(SaveView view)
    {
        var tags = SaveExplorerQuery.Tags(view);
        if (tags.SequenceEqual(_tags, StringComparer.Ordinal))
        {
            return;
        }

        _tags = tags;
        var items = new List<ChoiceItem> { new(SaveExplorerTexts.AllCountries, null) };
        items.AddRange(tags.Select(tag => new ChoiceItem(tag, tag)));
        _tagFilter.SetItems(items, _tagFilter.SelectedTag, pinned: 1);
    }

    /// <summary>Rechtsklick auf ein Objekt: "ID kopieren" und, wenn das Profil einen Skript-Verweis kennt, z.B. "character:38106 kopieren".</summary>
    private static Controls.ContextMenu CopyMenu(SaveObjectRef reference)
    {
        var menu = new Controls.ContextMenu();
        var copyId = new Controls.MenuItem { Header = SaveExplorerTexts.CopyId, InputGestureText = SaveExplorerTexts.CopyIdGesture, IsEnabled = reference.Id is not null };
        copyId.Click += (_, _) => CopyText(reference.Id!);
        menu.Items.Add(copyId);
        if (reference.ScriptReference is { } script)
        {
            var copyScript = new Controls.MenuItem { Header = new Controls.TextBlock { Text = SaveExplorerTexts.CopyScript(script) } };
            copyScript.Click += (_, _) => CopyText(script);
            menu.Items.Add(copyScript);
        }

        return menu;
    }

    private static void CopyText(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Zwischenablage gerade von einem anderen Programm belegt; der Nutzer kann es erneut versuchen.
        }
    }

    private void ShowInCode(Controls.Button button, string? name)
    {
        if (name is null || _service.Host.Index() is not { } index)
        {
            return;
        }

        var menu = new Controls.ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        var hits = index.Find(name).Take(30).ToList();
        if (hits.Count == 0)
        {
            menu.Items.Add(new Controls.MenuItem { Header = new Controls.TextBlock { Text = SaveExplorerTexts.ShowInCodeNone(name) }, IsEnabled = false });
        }

        foreach (var hit in hits)
        {
            var path = hit.Mod is { } mod ? Path.GetRelativePath(mod.Directory, hit.FilePath) : Path.GetFileName(hit.FilePath);
            var item = new Controls.MenuItem { Header = new Controls.TextBlock { Text = SaveExplorerTexts.ShowInCodeEntry(path, hit.Line, hit.Mod?.Name ?? ErrorLogTexts.VanillaName) } };
            item.Click += (_, _) => _service.Host.Jump(hit.FilePath, hit.Line);
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    /// <summary>Spalten der Listen; "Vorher" und "Änderung" nur mit Vergleich.</summary>
    private void EnsureColumns(bool comparison)
    {
        if (_columnsForComparison == comparison)
        {
            return;
        }

        _columnsForComparison = comparison;
        var names = new Controls.GridView();
        names.Columns.Add(new Controls.GridViewColumn
        {
            Header = new Controls.GridViewColumnHeader { Content = string.Empty, ToolTip = SaveExplorerTexts.ColumnErrorLog },
            DisplayMemberBinding = new Binding(nameof(NameItem.ErrorIcon)),
            Width = 30,
        });
        names.Columns.Add(Column(SaveExplorerTexts.ColumnName, nameof(NameItem.Name), 280, null));
        names.Columns.Add(Column(SaveExplorerTexts.ColumnNameKind, nameof(NameItem.KindText), 80, null));
        names.Columns.Add(Column(SaveExplorerTexts.ColumnCount, nameof(NameItem.Count), 60, null));
        if (comparison)
        {
            names.Columns.Add(Column(SaveExplorerTexts.ColumnChange, nameof(NameItem.ChangeText), 80, null));
        }

        _names.View = names;
        _nameValues.View = ValueColumns(comparison, byObject: false);
        _objectValues.View = ValueColumns(comparison, byObject: true);
    }

    private static Controls.GridView ValueColumns(bool comparison, bool byObject)
    {
        var view = new Controls.GridView();
        if (byObject)
        {
            view.Columns.Add(Column(SaveExplorerTexts.ColumnName, nameof(ValueItem.Name), 260, "name"));
            view.Columns.Add(Column(SaveExplorerTexts.ColumnNameKind, nameof(ValueItem.NameKindText), 80, "nameKind"));
        }
        else
        {
            view.Columns.Add(Column(SaveExplorerTexts.ColumnKind, nameof(ValueItem.KindText), 120, "kind"));
            view.Columns.Add(Column(SaveExplorerTexts.ColumnObject, nameof(ValueItem.ObjectText), 200, "object"));
        }

        view.Columns.Add(Column(SaveExplorerTexts.ColumnType, nameof(ValueItem.Type), 70, "type"));
        view.Columns.Add(Column(SaveExplorerTexts.ColumnValue, nameof(ValueItem.Value), 160, "value"));
        if (comparison)
        {
            view.Columns.Add(Column(SaveExplorerTexts.ColumnBefore, nameof(ValueItem.Before), 160, "before"));
            view.Columns.Add(Column(SaveExplorerTexts.ColumnChange, nameof(ValueItem.ChangeText), 80, "change"));
        }

        return view;
    }

    private static Controls.GridViewColumn Column(string header, string property, double width, string? sortKey) => new()
    {
        Header = new Controls.GridViewColumnHeader { Content = header, Tag = sortKey },
        DisplayMemberBinding = new Binding(property),
        Width = width,
    };

    private void OnHeaderClick(Controls.ListView list, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Controls.GridViewColumnHeader { Tag: string key })
        {
            return;
        }

        var descending = _sort.TryGetValue(list, out var current) && current.Key == key && !current.Descending;
        _sort[list] = (key, descending);
        if (list.ItemsSource is IEnumerable<ValueItem> items)
        {
            list.ItemsSource = Sorted(list, items.ToList());
        }
    }

    private IReadOnlyList<ValueItem> Sorted(Controls.ListView list, IEnumerable<ValueItem> items)
    {
        if (!_sort.TryGetValue(list, out var sort))
        {
            return items.ToList();
        }

        Func<ValueItem, string> key = sort.Key switch
        {
            "kind" => i => i.KindText,
            "object" => i => i.ObjectText,
            "name" => i => i.Name,
            "nameKind" => i => i.NameKindText,
            "type" => i => i.Type,
            "before" => i => i.Before,
            "change" => i => i.ChangeText,
            _ => i => i.Value,
        };
        var comparer = new NaturalComparer();
        return (sort.Descending ? items.OrderByDescending(key, comparer) : items.OrderBy(key, comparer)).ToList();
    }

    private string SaveEntryText(SaveFileInfo save) =>
        SaveExplorerTexts.SaveEntry(Path.GetFileName(save.Path), save.GameDate, save.LastWriteUtc.ToLocalTime());

    private static bool SamePath(SaveFileInfo a, SaveFileInfo? b) => b is not null && string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);

    private static Controls.TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    private static Controls.Button CodeButton() => new()
    {
        Content = SaveExplorerTexts.ShowInCode,
        Padding = new Thickness(8, 1, 8, 1),
        Margin = new Thickness(0, 0, 0, 6),
        HorizontalAlignment = HorizontalAlignment.Left,
        IsEnabled = false,
    };

    private static Controls.DockPanel ValuePane(FrameworkElement code, Controls.ListView values)
    {
        Controls.ToolTipService.SetShowOnDisabled(code, true);
        var pane = new Controls.DockPanel();
        Controls.DockPanel.SetDock(code, Controls.Dock.Top);
        pane.Children.Add(code);
        pane.Children.Add(values);
        return pane;
    }

    private static Controls.Grid Split(FrameworkElement left, FrameworkElement right)
    {
        var grid = new Controls.Grid { Margin = new Thickness(0, 6, 0, 0) };
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 260 });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(6) });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star), MinWidth = 360 });
        grid.Children.Add(left);
        var splitter = new Controls.GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        Controls.Grid.SetColumn(splitter, 1);
        grid.Children.Add(splitter);
        Controls.Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return grid;
    }

    /// <summary>Zeile der Namensliste mit fertigen Texten für die Spalten; <see cref="ErrorIcon"/> leer ohne Fehlerprotokoll-Meldung.</summary>
    private sealed record NameItem(string ErrorIcon, string Name, string KindText, int Count, string ChangeText, SaveNameRow Row)
    {
        public static NameItem From(SaveNameRow row, string errorIcon) =>
            new(errorIcon, row.Name, SaveExplorerTexts.NameKind(row.Kind), row.ObjectCount, row.Change is { } change ? SaveExplorerTexts.Change(change) : string.Empty, row);
    }

    /// <summary>Zeile der Wertetabellen mit fertigen Texten für die Spalten.</summary>
    private sealed record ValueItem(string KindText, string ObjectText, string Name, string NameKindText, string Type, string Value, string Before, string ChangeText, SaveValueRow Row)
    {
        public static ValueItem From(SaveValueRow row) => new(
            row.Object.Kind.Label,
            row.Object.DisplayName,
            row.Name,
            SaveExplorerTexts.NameKind(row.Kind),
            row.Type,
            row.Value,
            row.Before ?? string.Empty,
            row.Change is { } change ? SaveExplorerTexts.Change(change) : string.Empty,
            row);
    }

    /// <summary>Sortiert Zahlen als Zahlen (600 vor 1000), sonst ohne Groß-/Kleinschreibung.</summary>
    private sealed class NaturalComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (decimal.TryParse(x, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var a)
                && decimal.TryParse(y, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var b))
            {
                return a.CompareTo(b);
            }

            return StringComparer.OrdinalIgnoreCase.Compare(x, y);
        }
    }
}
