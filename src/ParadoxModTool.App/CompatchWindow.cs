using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ParadoxModTool.Compatch;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Mods;
using Controls = System.Windows.Controls;
using MergeAction = ParadoxModTool.Compatch.MergeAction;

namespace ParadoxModTool.App;

/// <summary>Merge-Fenster (Spec, "Oberfläche"): Werkzeugleiste, Zähler, Aktionsbaum links, Ansicht A rechts (Task 17), Reiter "Patch-Dateien" (Task 18).</summary>
public sealed partial class CompatchWindow : Window
{
    private readonly CompatchHost _host;
    private readonly CompatchState _state;
    private readonly CompatchStateStore _store;
    private readonly ParseCache _cache = new();
    private readonly DispatcherTimer _autosave = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly List<FileSystemWatcher> _watchers = new();
    private CompatchSession? _session;
    private readonly CompatchInput? _updateInput;
    private readonly Action? _afterUpdateWrite;
    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private int _runId;
    private DateTime _suppressWatchUntil = DateTime.MinValue;
    private readonly Controls.Button _rerun = new() { Content = CompatchTexts.Rerun, Padding = new Thickness(8, 1, 8, 1) };

    private readonly Controls.ComboBox _stageFilter = new() { Width = 150, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.ComboBox _originFilter = new() { Width = 130, Margin = new Thickness(4, 0, 12, 0) };
    // Optionen des Arbeitsstands im Menü "Einstellungen ▾" (Nutzerwunsch 2026-09-30); nur "Erledigte ausblenden" bleibt in der Leiste.
    private readonly Controls.MenuItem _uncertainOnly = new() { Header = CompatchTexts.FilterUncertain, ToolTip = CompatchTexts.FilterUncertainTip, IsCheckable = true, StaysOpenOnClick = true };
    private readonly Controls.MenuItem _mainAsBase = new() { Header = CompatchTexts.MainAsBase, ToolTip = CompatchTexts.MainAsBaseTip, IsCheckable = true, StaysOpenOnClick = true };
    private readonly Controls.MenuItem _fillMissing = new() { Header = CompatchTexts.FillMissing, ToolTip = CompatchTexts.FillMissingTip, IsCheckable = true, StaysOpenOnClick = true };
    private readonly Controls.MenuItem _hideFilled = new() { Header = CompatchTexts.FilterHideFilled, ToolTip = CompatchTexts.FilterHideFilledTip, IsCheckable = true, StaysOpenOnClick = true };
    private readonly Controls.MenuItem _hideReplaced = new() { Header = CompatchTexts.FilterHideReplaced, ToolTip = CompatchTexts.FilterHideReplacedTip, IsCheckable = true, StaysOpenOnClick = true };
    private Controls.ContextMenu? _settingsMenu;
    private readonly Controls.Button _settingsButton =new() { Content = CompatchTexts.SettingsMenu, ToolTip = CompatchTexts.SettingsMenuTip, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 12, 0) };
    private readonly Controls.CheckBox _hideEqual = new() { Content = new Controls.TextBlock { Text = CompatchTexts.FilterHideEqual }, ToolTip = CompatchTexts.FilterHideEqualTip, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.CheckBox _hideDecided = new() { Content = new Controls.TextBlock { Text = CompatchTexts.FilterHideDecided }, ToolTip = CompatchTexts.FilterHideDecidedTip, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private IReadOnlyList<string> _lastOrder = Array.Empty<string>();
    private IReadOnlyList<MergeAction> _lastVisible = Array.Empty<MergeAction>();
    private HashSet<string> _blockAddresses = new(StringComparer.Ordinal);
    private readonly Dictionary<Controls.TreeViewItem, ActionTreeNode> _pending = new();
    private bool _treeDirty;
    private CompatchRun? _sortedRun;
    private IReadOnlyList<MergeAction> _sorted = Array.Empty<MergeAction>();
    // Ein Suchfeld direkt über dem Baum der gerade gezeigten Ansicht (PlaceSearch); sucht in Dateipfad, Stelle und Schlüssel.
    private readonly Controls.TextBox _search = new() { ToolTip = CompatchTexts.SearchTip };
    private readonly Controls.DockPanel _searchBar = new() { Margin = new Thickness(0, 0, 6, 4) };
    private readonly Controls.Border _searchSlotActions = new();
    private readonly Controls.Border _searchSlotDocument = new();
    private readonly Controls.Border _searchSlotCardTree = new();
    private readonly Controls.Border _searchSlotCards = new();

    // Hauptfunktionen als Kacheln (Nutzerwunsch 2026-09-30).
    private readonly Controls.Button _recheck = new() { Margin = new Thickness(0, 0, 8, 0) };
    private readonly Controls.Button _saveButton = new() { Margin = new Thickness(0, 0, 8, 0) };
    private readonly Controls.Button _writeButton = new() { Margin = new Thickness(0, 0, 8, 0) };
    private readonly Controls.MenuItem _cautious = new() { Header = ParadoxModTool.Settings.SettingsTexts.CompatchCautious, ToolTip = CompatchTexts.MenuCautiousTip, IsCheckable = true, StaysOpenOnClick = true };
    private readonly Controls.TextBlock _counters = new() { Margin = new Thickness(0, 6, 0, 0) };
    // Ausgeblendete Dateiarten und gesperrte Dateien: als Liste untereinander oder nebeneinander (KindsSideBySide).
    private readonly Controls.Border _skipped = new() { Margin = new Thickness(0, 2, 0, 0) };
    private readonly Controls.Border _notice = new() { Visibility = Visibility.Collapsed, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.TextBlock _noticeText = new() { VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
    private readonly Controls.TabControl _tabs = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly Controls.TreeView _tree = new();
    private readonly Controls.Border _detailHost = new();
    private readonly Controls.Border _filesHost = new();
    private readonly Controls.ComboBox _viewPicker = new() { Width = 190, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.Border _actionsHost = new();
    private readonly Controls.Grid _listView = new();
    private readonly Controls.Border _cardsHost = new();
    private readonly Controls.Border _documentHost = new();
    private CompatchView _view;
    private bool _switchingView;
    private readonly List<CompatchCompareWindow> _compareWindows = new();
    private readonly Controls.TextBlock _status = new() { Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };

    /// <param name="updateInput">Update-Modus (Ziel <see cref="PatchTarget.UpdateMod"/>): die vorbereitete Eingabe aus <see cref="VanillaUpdateRun.Prepare"/>.</param>
    /// <param name="afterUpdateWrite">Update-Modus: nach erfolgreichem Schreiben (Update-Reiter lädt neu).</param>
    public CompatchWindow(CompatchHost host, CompatchState state, CompatchStateStore store, CompatchInput? updateInput = null, Action? afterUpdateWrite = null)
    {
        _host = host;
        _state = state;
        _store = store;
        _updateInput = updateInput;
        _afterUpdateWrite = afterUpdateWrite;
        var targetName = state.TargetModId is { } id && host.Registry.Get(id) is { } target ? target.Name : state.TargetFolder;
        Title = IsUpdate
            ? VanillaUpdateTexts.WindowTitle(state.UpdateNewVersion ?? string.Empty) + " – " + state.ModBName
            : CompatchTexts.WindowTitle(state.ModAName, state.ModBName, targetName);
        Width = 1300;
        Height = 820;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _cautious.IsChecked = state.Cautious;
        _mainAsBase.IsChecked = state.MainReplacesVanilla;
        _fillMissing.IsChecked = state.FillMissingFromMain;
        var mainOptions = MainSide() is null ? Visibility.Collapsed : Visibility.Visible;
        _mainAsBase.Visibility = mainOptions;
        _fillMissing.Visibility = mainOptions;
        _hideFilled.Visibility = mainOptions;
        _hideFilled.IsChecked = state.HideFilledMissing;
        _hideReplaced.IsChecked = state.HideReplacedFiles;
        _hideDecided.IsChecked = state.HideDecided;
        _hideEqual.IsChecked = state.HideEqual ?? state.Target == PatchTarget.UpdateMod;
        _uncertainOnly.IsChecked = state.UncertainOnly;
        _status.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _notice.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.NoticeBackground");

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        var top = new Controls.StackPanel();
        Controls.DockPanel.SetDock(top, Controls.Dock.Top);

        var tiles = new Controls.WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        tiles.Children.Add(Tile(_writeButton, "📝", CompatchTexts.WritePatch, bold: true));
        tiles.Children.Add(Tile(_saveButton, "💾", CompatchTexts.SaveState));
        tiles.Children.Add(Tile(_recheck, "🔄", CompatchTexts.RecheckAll));
        top.Children.Add(tiles);

        var toolbar = new Controls.WrapPanel();
        toolbar.Children.Add(Label(CompatchTexts.ViewLabel));
        toolbar.Children.Add(_viewPicker);
        toolbar.Children.Add(Label(CompatchTexts.FilterStage));
        toolbar.Children.Add(_stageFilter);
        toolbar.Children.Add(Label(CompatchTexts.FilterOrigin));
        toolbar.Children.Add(_originFilter);
        toolbar.Children.Add(_hideDecided);
        toolbar.Children.Add(_hideEqual);
        toolbar.Children.Add(_settingsButton);
        top.Children.Add(toolbar);

        var searchIcon = new Controls.TextBlock { Text = "🔍", VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        Controls.DockPanel.SetDock(searchIcon, Controls.Dock.Left);
        _searchBar.Children.Add(searchIcon);
        _searchBar.Children.Add(_search);
        _settingsButton.Click += (_, _) =>
        {
            // Einmal gebaut: die Einträge sind feste Felder und dürfen nur in einem Menü stecken.
            var menu = _settingsMenu ??= BuildSettingsMenu();
            menu.PlacementTarget = _settingsButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        };
        top.Children.Add(_counters);
        top.Children.Add(_skipped);
        var noticePanel = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        noticePanel.Children.Add(_noticeText);
        _rerun.Click += async (_, _) => await RunAsync();
        noticePanel.Children.Add(_rerun);
        _notice.Child = noticePanel;
        top.Children.Add(_notice);
        root.Children.Add(top);
        Controls.DockPanel.SetDock(_status, Controls.Dock.Bottom);
        root.Children.Add(_status);

        _listView.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 220 });
        _listView.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(6) });
        _listView.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(2.4, GridUnitType.Star) });
        var treeColumn = new Controls.DockPanel();
        Controls.DockPanel.SetDock(_searchSlotActions, Controls.Dock.Top);
        treeColumn.Children.Add(_searchSlotActions);
        treeColumn.Children.Add(_tree);
        _listView.Children.Add(treeColumn);
        var splitter = new Controls.GridSplitter { Width = 6, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
        Controls.Grid.SetColumn(splitter, 1);
        _listView.Children.Add(splitter);
        Controls.Grid.SetColumn(_detailHost, 2);
        _listView.Children.Add(_detailHost);
        _tabs.Items.Add(new Controls.TabItem { Header = CompatchTexts.TabActions, Content = _actionsHost });
        _tabs.Items.Add(new Controls.TabItem { Header = CompatchTexts.TabPatchFiles, Content = _filesHost });
        root.Children.Add(_tabs);
        Content = root;

        _stageFilter.ItemsSource = new[] { CompatchTexts.FilterAll, CompatchTexts.FilterOpen }.Concat(Enum.GetValues<MergeStage>().Select(CompatchTexts.Stage)).ToList();
        _stageFilter.SelectedIndex = 0;
        _originFilter.ItemsSource = new[] { CompatchTexts.FilterAll, CompatchTexts.Origin(MergeOrigin.A), CompatchTexts.Origin(MergeOrigin.B), CompatchTexts.Origin(MergeOrigin.Both), CompatchTexts.Origin(null) };
        _originFilter.SelectedIndex = 0;
        _viewPicker.ItemsSource = Enum.GetValues<CompatchView>().Select(CompatchTexts.View).ToList();
        _viewPicker.SelectionChanged += (_, _) =>
        {
            if (!_switchingView && _viewPicker.SelectedIndex >= 0)
            {
                var view = (CompatchView)_viewPicker.SelectedIndex;
                ShowView(view);
                _host.RememberView(view);
            }
        };
        _stageFilter.SelectionChanged += (_, _) => RefreshFilters();
        _originFilter.SelectionChanged += (_, _) => RefreshFilters();
        _search.TextChanged += (_, _) => RefreshFilters();
        _uncertainOnly.Click += (_, _) =>
        {
            _state.UncertainOnly = _uncertainOnly.IsChecked;
            ScheduleSave();
            RefreshFilters();
        };
        _mainAsBase.Click += (_, _) => MainOptionsChanged();
        _fillMissing.Click += (_, _) => MainOptionsChanged();
        _hideFilled.Click += (_, _) =>
        {
            _state.HideFilledMissing = _hideFilled.IsChecked == true;
            ScheduleSave();
            RefreshFilters();
        };
        _hideReplaced.Click += (_, _) =>
        {
            _state.HideReplacedFiles = _hideReplaced.IsChecked == true;
            ScheduleSave();
            RefreshFilters();
        };
        _hideDecided.Click += (_, _) =>
        {
            _state.HideDecided = _hideDecided.IsChecked == true;
            ScheduleSave();
            RefreshFilters();
        };
        _hideEqual.Click += (_, _) =>
        {
            _state.HideEqual = _hideEqual.IsChecked == true;
            ScheduleSave();
            RefreshFilters();
        };
        _tree.SelectedItemChanged += (_, _) => RefreshDetail();
        _tree.AddHandler(Controls.TreeViewItem.ExpandedEvent, new RoutedEventHandler((_, e) =>
        {
            if (e.OriginalSource is Controls.TreeViewItem item)
            {
                ExpandPending(item);
            }
        }));
        _tree.MouseDoubleClick += (_, _) =>
        {
            if ((SelectedAction ?? SelectedBlock?.Actions[0]) is { } action)
            {
                OpenCompare(action);
            }
        };
        _recheck.Click += (_, _) =>
        {
            _session?.RecheckAll();
            ClearUndo();
            AfterChange(rebuild: true);
        };
        _saveButton.Click += (_, _) =>
        {
            SaveStateNow();
            _status.Text = CompatchTexts.StateSaved;
        };
        _cautious.Click += (_, _) =>
        {
            _state.Cautious = _cautious.IsChecked == true;
            ScheduleSave();
        };
        _writeButton.Click += (_, _) => WriteClicked();
        _autosave.Tick += (_, _) =>
        {
            _autosave.Stop();
            SaveStateNow();
        };

        InitializeDetail();
        InitializeFiles();
        InitializeCards();
        InitializeDocument();
        ShowView(host.Defaults.View);
        Loaded += async (_, _) => await RunAsync();
        Closed += (_, _) =>
        {
            _cts?.Cancel();
            CloseCompareWindows();
            _autosave.Stop();
            SaveStateNow();
            foreach (var watcher in _watchers)
            {
                watcher.Dispose();
            }
        };
        Watch(state.ModAFolder, state.ModAName);
        Watch(state.ModBFolder, state.ModBName);
    }

    partial void InitializeDetail();

    partial void OnActionSelected(MergeAction? action);

    partial void InitializeFiles();

    partial void OnSessionChanged();

    partial void WriteClicked();

    partial void InitializeCards();

    partial void InitializeDocument();

    partial void RefreshCards(bool rebuild);

    partial void RefreshDocument(bool rebuild);

    private MergeAction? SelectedAction => (_tree.SelectedItem as Controls.TreeViewItem)?.Tag as MergeAction;

    /// <summary>Aktionen, die die Filter der Werkzeugleiste zeigen, in der Reihenfolge von Baum, Karten und ◀ ▶.</summary>
    private IReadOnlyList<MergeAction> FilteredActions()
    {
        if (_session is null)
        {
            return Array.Empty<MergeAction>();
        }

        var session = _session;
        // Sortiert wird je Lauf einmal (bei zehntausenden Stellen kostet das spürbar Zeit); Filtern behält die Reihenfolge.
        if (!ReferenceEquals(_sortedRun, session.Run))
        {
            _sortedRun = session.Run;
            _sorted = ActionOrder.Sort(session.Actions);
        }

        var hideFilled = _hideFilled.Visibility == Visibility.Visible && _hideFilled.IsChecked == true;
        var hideDecided = _hideDecided.IsChecked == true;
        var hideEqual = _hideEqual.IsChecked == true;
        var replaced = _hideReplaced.IsChecked == true ? ActionFilters.ReplacedFiles(session.Actions, session.Run.Resolved) : null;
        return _sorted.Where(a => Matches(a)
            && !(hideFilled && a.FillsMissing)
            && !(hideDecided && IsDone(session, a))
            && !(hideEqual && a.Origin == MergeOrigin.Both)
            && !(replaced?.Contains(TreePath.FileOf(a)) ?? false)).ToList();
    }

    private static Controls.TextBlock Label(string text) => new() { Text = text, VerticalAlignment = System.Windows.VerticalAlignment.Center };

    /// <summary>Großer Knopf mit Symbol über dem Text (Kachel) für die Hauptfunktionen; der Knopf bleibt dasselbe Objekt (SetBusy).</summary>
    private static Controls.Button Tile(Controls.Button button, string icon, string text, bool bold = false)
    {
        var content = new Controls.StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        content.Children.Add(new Controls.TextBlock { Text = icon, FontSize = 20, HorizontalAlignment = System.Windows.HorizontalAlignment.Center });
        content.Children.Add(new Controls.TextBlock
        {
            Text = text,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0),
        });
        button.Content = content;
        button.Width = 150;
        button.MinHeight = 58;
        button.Padding = new Thickness(8, 6, 8, 6);
        return button;
    }

    /// <summary>
    /// Menü "Einstellungen ▾": Optionen dieses Arbeitsstands (jeder Arbeitsstand merkt sie sich) und der Weg ins Einstellungsfenster,
    /// wo die Standardwerte für neue Arbeitsstände liegen.
    /// </summary>
    private Controls.ContextMenu BuildSettingsMenu()
    {
        var menu = new Controls.ContextMenu();
        foreach (var item in new[] { _mainAsBase, _fillMissing, _hideFilled, _hideReplaced, _uncertainOnly })
        {
            menu.Items.Add(item);
        }

        menu.Items.Add(new Controls.Separator());
        menu.Items.Add(_cautious);
        if (_host.OpenSettings is { } openSettings)
        {
            menu.Items.Add(new Controls.Separator());
            var open = new Controls.MenuItem { Header = CompatchTexts.OpenSettingsWindow, ToolTip = CompatchTexts.OpenSettingsWindowTip };
            open.Click += (_, _) => openSettings();
            menu.Items.Add(open);
        }

        return menu;
    }

    /// <summary>Setzt das Suchfeld über den Baum der gezeigten Ansicht (in C ohne Ordnerbereich über die Karten).</summary>
    private void PlaceSearch()
    {
        var slot = _view switch
        {
            CompatchView.Document => _searchSlotDocument,
            CompatchView.Cards => _cardTree.Visibility == Visibility.Visible ? _searchSlotCardTree : _searchSlotCards,
            _ => _searchSlotActions,
        };
        if (ReferenceEquals(_searchBar.Parent, slot))
        {
            return;
        }

        if (_searchBar.Parent is Controls.Border previous)
        {
            previous.Child = null;
        }

        slot.Child = _searchBar;
    }

    /// <summary>Liest einmal im Hintergrund und vergleicht; danach arbeitet alles mit <see cref="CompatchSession.Replan"/>.</summary>
    /// <remarks>
    /// Nur ein Lauf zur Zeit: Ein neuer Lauf bricht den alten ab und wartet auf ihn (der <see cref="ParseCache"/> ist nicht threadsicher).
    /// Während des Laufs sind Baum, Ansicht und Reiter gesperrt, weil der Hintergrund <see cref="CompatchState"/> liest; ein verspätet
    /// fertiger alter Lauf (Zähler <c>_runId</c>) setzt keine Sitzung mehr.
    /// </remarks>
    private async Task RunAsync()
    {
        _cts?.Cancel();
        if (_runTask is { } previous)
        {
            try
            {
                await previous;
            }
            catch (Exception)
            {
                // Der alte Lauf meldet seinen Fehler selbst; hier zählt nur, dass er fertig ist.
            }
        }

        var run = ++_runId;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _notice.Visibility = Visibility.Collapsed;
        SetBusy(true);
        CloseCompareWindows();
        _status.Text = CompatchTexts.Loading;
        var watch = Stopwatch.StartNew();
        var progressWindow = new TaskProgressWindow(CompatchTexts.ProgressTitle, CompatchTexts.ProgressHint, CompatchTexts.Cancel, CompatchTexts.ProgressCount, () => _cts?.Cancel()) { Owner = this };
        progressWindow.Show();
        try
        {
            var session = new CompatchSession(BuildInput(), _state, _store.LoadBase());
            var progress = new Progress<CompatProgress>(p =>
            {
                _status.Text = CompatchTexts.Loading + ": " + p.Text;
                progressWindow.Report(p);
            });
            _runTask = Task.Run(() => session.Load(progress, ct), ct);
            await _runTask;
            if (run != _runId)
            {
                return;
            }

            _session = session;
            ClearUndo();
            _status.Text = CompatchTexts.Loaded(session.Actions.Count, watch.Elapsed.TotalSeconds) + (_store.Warning is { } warning ? "  " + warning : string.Empty)
                + ForgetNotice(session);
            AfterChange(rebuild: true);
        }
        catch (OperationCanceledException)
        {
            _status.Text = CompatchTexts.Cancelled;
        }
        catch (Exception ex) when (ex is CompatchInputException or IOException or UnauthorizedAccessException)
        {
            _status.Text = CompatchTexts.RunFailed(ex.Message);
        }
        finally
        {
            progressWindow.CloseAfterFinished();
            if (run == _runId)
            {
                SetBusy(false);
            }
        }
    }

    /// <summary>
    /// Update-Modus: Hat die Sitzung Entscheidungen veränderter Dateien verworfen (<see cref="CompatchSession.ForgottenFiles"/>), Arbeitsstand
    /// und Basis gleich speichern, sonst verwürfe das nächste Öffnen auch die inzwischen neu getroffenen. Liefert den Hinweis für die Statuszeile.
    /// </summary>
    private string ForgetNotice(CompatchSession session)
    {
        if (session.ForgottenFiles.Count == 0 || session.Baseline is not { } baseline)
        {
            return string.Empty;
        }

        SaveStateNow();
        var notice = "  " + CompatchTexts.ForgottenFiles(session.ForgottenFiles.Count, string.Join(", ", session.ForgottenFiles));
        try
        {
            _store.SaveBase(baseline);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            notice += "  " + ex.Message;
        }

        return notice;
    }

    private void SetBusy(bool busy)
    {
        _recheck.IsEnabled = !busy;
        _writeButton.IsEnabled = !busy;
        _saveButton.IsEnabled = !busy;
        _rerun.IsEnabled = !busy;
        _skipped.IsEnabled = !busy;
        _tree.IsEnabled = !busy;
        _detailHost.IsEnabled = !busy;
        _filesHost.IsEnabled = !busy;
        _viewPicker.IsEnabled = !busy;
        _cardsHost.IsEnabled = !busy;
        _documentHost.IsEnabled = !busy;
        _mainAsBase.IsEnabled = !busy;
        _fillMissing.IsEnabled = !busy;
    }

    private CompatchInput BuildInput()
    {
        if (_updateInput is { } update)
        {
            // Update-Modus: Ordner und Regeln kommen aus VanillaUpdateRun.Prepare; nur der Zerlegungs-Cache gehört dem Fenster.
            return new CompatchInput
            {
                VanillaFolder = update.VanillaFolder,
                ModAFolder = update.ModAFolder,
                ModBFolder = update.ModBFolder,
                BLoadsAfterA = update.BLoadsAfterA,
                Rules = update.Rules,
                KeyOperators = update.KeyOperators,
                Filter = update.Filter,
                NoBasePaths = update.NoBasePaths,
                ExcludeOwnPatchFilesOf = update.ExcludeOwnPatchFilesOf,
                Cache = _cache,
            };
        }

        if (_host.VanillaRoot is null || !Directory.Exists(_host.VanillaRoot))
        {
            throw new CompatchInputException(CompatchTexts.ErrorNoVanilla);
        }

        var gameRules = OverrideRulesStore.Load(Path.Combine(_host.GameDirectory, "compat", "rules.json"));
        var rules = _host.Profile is { } profile ? OverrideRules.FromProfile(profile, gameRules) : new OverrideRules(Array.Empty<OverrideRule>(), gameRules);
        var hidden = CompatchFileFilter.DefaultHiddenExtensions
            .Where(e => _state.HiddenKinds.Contains(e.Value))
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
        var locked = new List<string>();
        foreach (var id in new[] { _state.ModAId, _state.ModBId, _state.TargetModId }.Where(i => i is not null).Distinct())
        {
            if (_host.Registry.Get(id!) is { } mod)
            {
                if (mod.IsLocked)
                {
                    locked.Add(string.Empty);
                }

                locked.AddRange(mod.LockedPaths);
            }
        }

        var metadataFile = _host.Profile?.MetadataFile ?? string.Empty;
        var hidesEarlier = _host.Profile?.ReplacePathHidesEarlierMods == true;
        var roots = _host.Profile?.ReplacePathRoots ?? new List<string>();
        return new CompatchInput
        {
            VanillaFolder = _host.VanillaRoot,
            ModAFolder = _state.ModAFolder,
            ModBFolder = _state.ModBFolder,
            BLoadsAfterA = _state.BLoadsAfterA,
            Rules = rules,
            KeyOperators = _host.Profile?.KeyOperators ?? new List<string>(),
            HiddenVanillaFolders = ReplacePaths.Of(new[] { _state.ModAFolder, _state.ModBFolder }, metadataFile, roots),
            // CK III, EU V und Victoria 3: replace_path der später ladenden Mod blendet auch die Dateien der früheren aus.
            HiddenFoldersOfA = hidesEarlier && _state.BLoadsAfterA ? ReplacePaths.Of(new[] { _state.ModBFolder }, metadataFile, roots) : Array.Empty<string>(),
            HiddenFoldersOfB = hidesEarlier && !_state.BLoadsAfterA ? ReplacePaths.Of(new[] { _state.ModAFolder }, metadataFile, roots) : Array.Empty<string>(),
            ReplacePathRoots = roots,
            Filter = new CompatchFileFilter(hidden, locked, _state.SkipLocked),
            ExcludeOwnPatchFilesOf = _state.Target switch
            {
                PatchTarget.BaseModA => MergeSide.A,
                PatchTarget.BaseModB => MergeSide.B,
                _ => null,
            },
            MainSide = MainSide(),
            MainAsBase = _state.MainReplacesVanilla,
            FillMissingFromMain = _state.FillMissingFromMain,
            Cache = _cache,
        };
    }

    /// <summary>Hauptmod beim Ziel "Basis-Mod": die Seite, die nicht das Ziel ist; sonst <see langword="null"/>.</summary>
    private MergeSide? MainSide() => _state.Target switch
    {
        PatchTarget.BaseModA => MergeSide.B,
        PatchTarget.BaseModB => MergeSide.A,
        _ => null,
    };

    private string MainName => MainSide() == MergeSide.B ? _state.ModBName : _state.ModAName;

    /// <summary>Plan 4, Ruling 9: Optionen umschalten = neu mergen mit den gelesenen Daten.</summary>
    private void MainOptionsChanged()
    {
        _state.MainReplacesVanilla = _mainAsBase.IsChecked == true;
        _state.FillMissingFromMain = _fillMissing.IsChecked == true;
        if (_session is null)
        {
            ScheduleSave();
            return;
        }

        try
        {
            _session.Remerge(BuildInput());
            ClearUndo();
        }
        catch (CompatchInputException ex)
        {
            _status.Text = ex.Message;
            return;
        }

        AfterChange(rebuild: true);
    }

    /// <summary>
    /// Nach jeder Änderung: Baum (Auswahl bleibt), Zähler, Zählzeilen, aktuelle Ansicht, Autospeichern, Reiter "Patch-Dateien".
    /// <paramref name="rebuild"/> nur nach einem Lauf und "Alles neu prüfen": Karten und Dokumentliste neu (Ruling 4).
    /// </summary>
    private void AfterChange(bool rebuild = false)
    {
        RefreshTree();
        RefreshCounters();
        RefreshCurrentView(rebuild);
        ScheduleSave();
        OnSessionChanged();
        foreach (var window in _compareWindows.ToList())
        {
            window.Refresh();
        }
    }

    /// <summary>Öffnet das Vergleichsfenster einer Stelle (Doppelklick in allen Ansichten, Knopf "Vergleich" auf Karten).</summary>
    internal void OpenCompare(MergeAction action)
    {
        var window = new CompatchCompareWindow(this, action);
        window.Closed += (_, _) => _compareWindows.Remove(window);
        _compareWindows.Add(window);
        window.Show();
    }

    /// <summary>Titel der zuschaltbaren Spalte: "Vanilla" oder "Basis: Hauptmod" (Plan 4).</summary>
    internal string BaseColumnTitle => IsUpdate
        ? VanillaUpdateTexts.OldVanillaColumn
        : _state.MainReplacesVanilla && MainSide() is not null ? CompatchTexts.ColumnBase(MainName) : CompatchTexts.ColumnVanilla;

    /// <summary>Update-Modus: Mod an neue Vanilla anpassen (Spec 2026-09-30-vanilla-update-merge).</summary>
    internal bool IsUpdate => _state.Target == PatchTarget.UpdateMod;

    private void CloseCompareWindows()
    {
        foreach (var window in _compareWindows.ToList())
        {
            window.Close();
        }
    }

    private void RefreshFilters()
    {
        RefreshTree();
        RefreshCurrentView(rebuild: true);
    }

    private void RefreshCurrentView(bool rebuild)
    {
        switch (_view)
        {
            case CompatchView.Cards:
                RefreshCards(rebuild);
                break;
            case CompatchView.Document:
                RefreshDocument(rebuild);
                break;
        }
    }

    /// <summary>Zeigt eine Ansicht im Reiter "Aktionen". Beim Einblenden werden Karten bzw. Dokumentliste neu aufgebaut.</summary>
    private void ShowView(CompatchView view)
    {
        _view = view;
        _actionsHost.Child = view switch
        {
            CompatchView.Cards => _cardsHost,
            CompatchView.Document => _documentHost,
            _ => _listView,
        };
        PlaceSearch();
        if (_viewPicker.SelectedIndex != (int)view)
        {
            _switchingView = true;
            _viewPicker.SelectedIndex = (int)view;
            _switchingView = false;
        }

        _tabs.SelectedIndex = 0;
        if (view == CompatchView.Actions && _treeDirty)
        {
            RefreshTree();
        }

        RefreshCurrentView(rebuild: true);
    }

    /// <summary>
    /// Springt in Ansicht A und wählt die Aktion im Baum (Kartenklick, "Bearbeiten" in B und C). Zählt nicht als Ansichtswahl (Ruling 5).
    /// Filtert der Baum die Aktion weg (z. B. "Offen" nach einer Entscheidung, oder Ansicht B zeigt alle Adressen eines Scopes),
    /// werden die Filter zurückgesetzt.
    /// </summary>
    private void JumpToAction(string id, bool edit = false)
    {
        ShowView(CompatchView.Actions);
        if (!SelectInTree(id, edit))
        {
            _stageFilter.SelectedIndex = 0;
            _originFilter.SelectedIndex = 0;
            _search.Text = string.Empty;
            _uncertainOnly.IsChecked = false;
            _hideFilled.IsChecked = false;
            _hideReplaced.IsChecked = false;
            _hideDecided.IsChecked = false;
            _hideEqual.IsChecked = false;
            _state.HideDecided = false;
            _state.HideEqual = false;
            _state.HideFilledMissing = false;
            _state.HideReplacedFiles = false;
            _state.UncertainOnly = false;
            RefreshTree();
            SelectInTree(id, edit);
        }
    }

    /// <summary>
    /// Wählt eine Stelle (Aktions-ID) oder einen Block (Adress-ID) im Baum. Die Knoten auf dem Weg dorthin werden aufgeklappt und entstehen
    /// dabei erst (siehe <see cref="RefreshTree"/>).
    /// </summary>
    private bool SelectInTree(string id, bool edit)
    {
        var action = _lastVisible.FirstOrDefault(a => a.Id == id);
        var blockTarget = false;
        if (action is null)
        {
            action = _lastVisible.FirstOrDefault(a => a.Address.Id == id);
            blockTarget = action is not null && _blockAddresses.Contains(id);
        }

        if (action is null)
        {
            return false;
        }

        var keys = ChainKeys(action, _blockAddresses.Contains(action.Address.Id)).ToList();
        if (!blockTarget)
        {
            keys.Add("a:" + action.Id);
        }

        var items = _tree.Items;
        Controls.TreeViewItem? found = null;
        for (var i = 0; i < keys.Count; i++)
        {
            found = items.OfType<Controls.TreeViewItem>().FirstOrDefault(item => string.Equals(ItemKey(item), keys[i], StringComparison.OrdinalIgnoreCase));
            if (found is null)
            {
                return false;
            }

            if (i < keys.Count - 1)
            {
                found.IsExpanded = true;
                ExpandPending(found);
                items = found.Items;
            }
        }

        found!.IsSelected = true;
        found.BringIntoView();
        if (edit)
        {
            ShowEditor(true);
        }

        return true;
    }

    /// <summary>Schlüssel der Knoten von der Wurzel bis zur Datei (und zum Block) einer Stelle: <c>d:</c>Ordner, <c>f:</c>Datei, <c>b:</c>Adresse.</summary>
    private static IEnumerable<string> ChainKeys(MergeAction action, bool inBlock)
    {
        var parts = TreePath.FileOf(action).Split('/');
        var path = string.Empty;
        for (var i = 0; i < parts.Length; i++)
        {
            path = i == 0 ? parts[0] : path + "/" + parts[i];
            yield return (i == parts.Length - 1 ? "f:" : "d:") + path;
        }

        if (inBlock)
        {
            yield return "b:" + action.Address.Id;
        }
    }

    /// <summary>Schlüssel eines Baumeintrags (wie <see cref="ChainKeys"/>, Stellen mit <c>a:</c>); <see langword="null"/> für Platzhalter.</summary>
    private static string? ItemKey(Controls.TreeViewItem item) => item.Tag switch
    {
        string key => key,
        CompatchBlock block => "b:" + block.Address.Id,
        MergeAction action => "a:" + action.Id,
        _ => null,
    };

    private void ScheduleSave()
    {
        _autosave.Stop();
        _autosave.Start();
    }

    private void SaveStateNow()
    {
        try
        {
            _store.Save(_state);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = ex.Message;
        }
    }

    private void RefreshCounters()
    {
        if (_session is null)
        {
            return;
        }

        var counts = _session.Counts;
        int Count(MergeStage stage) => counts.TryGetValue(stage, out var n) ? n : 0;
        var total = _session.Actions.Count;
        var main = Count(MergeStage.Conflict) + Count(MergeStage.Review) + Count(MergeStage.Automatic);
        _counters.Text = CompatchTexts.Counters(Count(MergeStage.Conflict), Count(MergeStage.Review), Count(MergeStage.Automatic), total - main, _session.SeenCount, total);
        RefreshSkipped();
    }

    /// <summary>
    /// Einstellung "Ausgeblendete Dateiarten" (<see cref="ParadoxModTool.Settings.AppSettings.CompatchKindsSideBySide"/>): nebeneinander statt
    /// als Liste untereinander. Das Hauptfenster setzt sie; offene Merge-Fenster zeichnen die Zeile sofort neu.
    /// </summary>
    internal static bool KindsSideBySide
    {
        get => _kindsSideBySide;
        set
        {
            if (_kindsSideBySide == value)
            {
                return;
            }

            _kindsSideBySide = value;
            foreach (var window in System.Windows.Application.Current?.Windows.OfType<CompatchWindow>() ?? Enumerable.Empty<CompatchWindow>())
            {
                window.RefreshSkipped();
            }
        }
    }

    private static bool _kindsSideBySide;

    /// <summary>Ausgeblendete Dateiarten und übersprungene gesperrte Dateien mit "einblenden".</summary>
    private void RefreshSkipped()
    {
        if (_session is null)
        {
            _skipped.Child = null;
            return;
        }

        Controls.Panel list = KindsSideBySide ? new Controls.WrapPanel() : new Controls.StackPanel();
        _skipped.Child = list;
        foreach (var skip in _session.Run.Skipped.Where(s => s.Reason != SkipReason.Metadata))
        {
            var row = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = KindsSideBySide ? new Thickness(0, 0, 16, 0) : new Thickness(0, 0, 0, 2) };
            row.Children.Add(new Controls.TextBlock { Text = CompatchTexts.Skip(skip.Reason, skip.Kind, skip.Count) });
            if (skip.Reason is SkipReason.HiddenKind or SkipReason.Locked)
            {
                var link = new Controls.Button { Content = CompatchTexts.ShowSkipped, Padding = new Thickness(4, 0, 4, 0), Margin = new Thickness(6, 0, 0, 0) };
                link.Click += async (_, _) =>
                {
                    if (skip.Reason == SkipReason.HiddenKind)
                    {
                        _state.HiddenKinds.Remove(skip.Kind);
                    }
                    else
                    {
                        _state.SkipLocked = false;
                    }

                    SaveStateNow();
                    await RunAsync();
                };
                row.Children.Add(link);
            }

            list.Children.Add(row);
        }
    }

    private bool Matches(MergeAction action)
    {
        var stageIndex = _stageFilter.SelectedIndex;
        if (stageIndex == 1 && !(action.Stage is MergeStage.Conflict or MergeStage.Review or MergeStage.Outdated && !_session!.IsExplicit(action.Id)))
        {
            return false;
        }

        if (stageIndex >= 2 && action.Stage != Enum.GetValues<MergeStage>()[stageIndex - 2])
        {
            return false;
        }

        var originOk = _originFilter.SelectedIndex switch
        {
            1 => action.Origin == MergeOrigin.A,
            2 => action.Origin == MergeOrigin.B,
            3 => action.Origin == MergeOrigin.Both,
            4 => action.Origin is null,
            _ => true,
        };
        if (!originOk || (_uncertainOnly.IsChecked == true && !action.Uncertain))
        {
            return false;
        }

        return ActionFilters.MatchesText(action, _search.Text);
    }

    /// <summary>Ordnerbaum Ordner → Datei → Stelle (Spec Nachtrag 2). Aufgeklappte Knoten und die Auswahl bleiben erhalten.</summary>
    private void RefreshTree()
    {
        if (_session is null)
        {
            return;
        }

        // Unsichtbar (Karten oder Dokument): nicht neu bauen, sondern beim nächsten Einblenden von Ansicht A (ShowView).
        if (_view != CompatchView.Actions)
        {
            _treeDirty = true;
            return;
        }

        _treeDirty = false;
        var session = _session;
        var keepId = SelectedAction?.Id;
        var keepBlock = SelectedBlock?.Address.Id;
        var keepNode = (_tree.SelectedItem as Controls.TreeViewItem)?.Tag as string;
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Collect(Controls.ItemCollection items)
        {
            foreach (Controls.TreeViewItem item in items)
            {
                if (item.IsExpanded && ItemKey(item) is { } key)
                {
                    expanded.Add(key);
                }

                Collect(item.Items);
            }
        }

        Collect(_tree.Items);
        var firstRun = _tree.Items.Count == 0;
        _tree.Items.Clear();
        _pending.Clear();
        var visible = FilteredActions();
        var visibleIds = visible.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        // Verschwindet die Auswahl (z. B. "Erledigte ausblenden" nach einer Entscheidung), wird der nächste Eintrag gewählt.
        var anchor = keepId ?? SelectedBlock?.Actions[0].Id;
        var stillThere = (keepId is not null && visibleIds.Contains(keepId)) || (keepBlock is not null && visible.Any(a => a.Address.Id == keepBlock));
        var fallback = anchor is null || stillThere ? null : ActionOrder.NextVisible(_lastOrder, anchor, visibleIds);
        _lastOrder = visible.Select(a => a.Id).ToList();
        _lastVisible = visible;
        var nodes = ActionTree.Build(visible, a => IsOpenConflict(session, a), a => a.Stage is MergeStage.Conflict or MergeStage.Review or MergeStage.Outdated && !session.IsExplicit(a.Id), presorted: true, isDone: a => IsDone(session, a));
        _blockAddresses = BlockPaths(nodes);

        // Was ausgewählt wird: die bisherige Auswahl oder der Nachfolger; der Weg dorthin wird aufgeklappt, damit der Eintrag entsteht.
        string? selectKey = null;
        if (keepId is null && keepBlock is null && keepNode is not null)
        {
            // Datei oder Ordner gewählt (Sammelknöpfe): Auswahl bleibt, solange der Knoten sichtbar ist.
            selectKey = keepNode;
        }
        else if (stillThere)
        {
            selectKey = keepBlock is not null ? "b:" + keepBlock : "a:" + keepId;
        }
        else if (fallback is not null && visible.FirstOrDefault(a => a.Id == fallback) is { } next)
        {
            selectKey = _blockAddresses.Contains(next.Address.Id) ? "b:" + next.Address.Id : "a:" + next.Id;
        }

        if (selectKey is not null && visible.FirstOrDefault(a => selectKey == "a:" + a.Id || selectKey == "b:" + a.Address.Id) is { } target)
        {
            foreach (var key in ChainKeys(target, _blockAddresses.Contains(target.Address.Id) && selectKey.StartsWith("a:", StringComparison.Ordinal)))
            {
                expanded.Add(key);
            }
        }

        var context = new TreeBuild(session, expanded, firstRun, selectKey);
        foreach (var node in nodes)
        {
            _tree.Items.Add(BuildItem(node, context));
        }

        if (context.Selected is not null)
        {
            context.Selected.IsSelected = true;
        }
        else
        {
            OnActionSelected(null);
        }
    }

    /// <summary>Platzhalter unter einem eingeklappten Knoten (damit der Pfeil erscheint); die echten Einträge entstehen beim Aufklappen.</summary>
    private static readonly object TreePlaceholder = new();

    /// <summary>Angaben für einen Aufbau des Baums: aufgeklappte Schlüssel, erster Lauf, gesuchte Auswahl und der gefundene Eintrag.</summary>
    private sealed class TreeBuild(CompatchSession session, IReadOnlySet<string> expanded, bool firstRun, string? selectKey)
    {
        public CompatchSession Session { get; } = session;

        public IReadOnlySet<string> Expanded { get; } = expanded;

        public bool FirstRun { get; } = firstRun;

        public string? SelectKey { get; } = selectKey;

        public Controls.TreeViewItem? Selected { get; set; }
    }

    /// <summary>
    /// Ein Eintrag des Baums. Ordner, Dateien und Blöcke bekommen ihre Kinder nur, wenn sie aufgeklappt sind; sonst einen Platzhalter, und die
    /// Kinder entstehen beim Aufklappen (<see cref="ExpandPending"/>). So entstehen nach einer Entscheidung nur die sichtbaren Einträge statt
    /// zehntausender (Nutzerwunsch Geschwindigkeit).
    /// </summary>
    private Controls.TreeViewItem BuildItem(ActionTreeNode node, TreeBuild context)
    {
        var session = context.Session;
        if (node.Kind == TreeNodeKind.Action)
        {
            var action = node.Action!;
            var marks = (action.Uncertain ? "  ⚠" : string.Empty)
                + (session.IsExplicit(action.Id) ? "  •" : string.Empty);
            // Eigenes Menü: ohne öffnete WPF beim Rechtsklick das Menü der Datei darüber, und "alle übernehmen" traf die ganze Datei.
            var leaf = new Controls.TreeViewItem { Header = CompatchTreeBuilder.ActionHeader(StageToken(session, action), action.DisplayPath + marks, IsDone(session, action), session.IsSeen(action.Id)), Tag = action, ContextMenu = BulkMenu(action.Id, TreeNodeKind.Action) };

            if (context.SelectKey == "a:" + action.Id)
            {
                context.Selected = leaf;
            }

            return leaf;
        }

        Controls.TreeViewItem item;
        string key;
        if (node.Kind == TreeNodeKind.Block)
        {
            var actions = node.Children.Select(c => c.Action!).ToList();
            var blockToken = actions.Any(a => IsOpenConflict(session, a))
                ? "CompatchConflict"
                : node.Open > 0 ? "CompatchReview" : "CompatchVanilla";
            key = "b:" + node.Path;
            item = new Controls.TreeViewItem
            {
                Header = CompatchTreeBuilder.ActionHeader(blockToken, CompatchTexts.BlockHeader(node.Name, node.Count), node.Done == node.Count, actions.All(a => session.IsSeen(a.Id))),
                Tag = new CompatchBlock(actions[0].Address, actions),
                ContextMenu = BulkMenu(node.Path, TreeNodeKind.Block),
            };
        }
        else
        {
            key = (node.Kind == TreeNodeKind.Folder ? "d:" : "f:") + node.Path;
            item = new Controls.TreeViewItem { Header = CompatchTreeBuilder.NodeHeader(node), Tag = key, ContextMenu = BulkMenu(node.Path, node.Kind) };
        }

        if (string.Equals(context.SelectKey, key, StringComparison.OrdinalIgnoreCase))
        {
            context.Selected = item;
        }

        // Erster Lauf: nur Ordner mit Konflikten aufklappen; Dateien und Blöcke mit tausenden Stellen erst auf Klick.
        if (context.Expanded.Contains(key) || (context.FirstRun && node.Kind == TreeNodeKind.Folder && node.OpenConflicts > 0))
        {
            foreach (var child in node.Children)
            {
                item.Items.Add(BuildItem(child, context));
            }

            item.IsExpanded = true;
        }
        else if (node.Children.Count > 0)
        {
            item.Items.Add(new Controls.TreeViewItem { Tag = TreePlaceholder });
            _pending[item] = node;
        }

        return item;
    }

    /// <summary>Beim Aufklappen: die echten Einträge unter einem Knoten anlegen (einmal je Aufbau des Baums).</summary>
    private void ExpandPending(Controls.TreeViewItem item)
    {
        if (_session is null || !_pending.Remove(item, out var node))
        {
            return;
        }

        item.Items.Clear();
        var context = new TreeBuild(_session, new HashSet<string>(), false, null);
        foreach (var child in node.Children)
        {
            item.Items.Add(BuildItem(child, context));
        }
    }

    /// <summary>Adress-IDs aller Blockknoten (ab zwei Stellen einer Adresse, siehe <see cref="ActionTree"/>).</summary>
    private static HashSet<string> BlockPaths(IEnumerable<ActionTreeNode> nodes)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        void Walk(IEnumerable<ActionTreeNode> list)
        {
            foreach (var node in list)
            {
                if (node.Kind == TreeNodeKind.Block)
                {
                    result.Add(node.Path);
                }
                else if (node.Kind != TreeNodeKind.Action)
                {
                    Walk(node.Children);
                }
            }
        }

        Walk(nodes);
        return result;
    }

    /// <summary>
    /// Rechtsklick auf Ordner, Datei oder Block (Nutzerwunsch): alle sichtbaren Stellen darunter als gelöst markieren (Vorschlag bestätigen) oder
    /// eine Seite übernehmen. Die Stellen werden beim Klick bestimmt, damit die aktuellen Filter gelten.
    /// </summary>
    private Controls.ContextMenu BulkMenu(string path, TreeNodeKind kind)
    {
        var menu = new Controls.ContextMenu();
        void Add(string header, Func<MergeAction, MergeDecision>? decisionFor)
        {
            var item = new Controls.MenuItem { Header = header };
            item.Click += (_, _) => DecideAll(ActionFilters.Under(FilteredActions(), path, kind), decisionFor);
            menu.Items.Add(item);
        }

        Add(CompatchTexts.BulkConfirm, null);
        Add(CompatchTexts.BulkTake(_state.ModAName), _ => new MergeDecision(DecisionKind.TakeA));
        Add(CompatchTexts.BulkTake(_state.ModBName), _ => new MergeDecision(DecisionKind.TakeB));
        menu.Items.Add(new Controls.Separator());
        var reset = new Controls.MenuItem { Header = CompatchTexts.BulkReset };
        reset.Click += (_, _) => ResetAll(path, kind);
        menu.Items.Add(reset);
        return menu;
    }

    /// <summary>
    /// Alle Stellen unter einem Knoten zurücksetzen (Nutzerwunsch 2026-10-01: Ausweg für eine ganze Datei). Gilt für alle Stellen, auch die von
    /// "Erledigte ausblenden" versteckten, denn genau die sind entschieden. Ab zwei Stellen mit Nachfrage; Strg+Z nimmt es zurück.
    /// </summary>
    internal void ResetAll(string path, TreeNodeKind kind)
    {
        if (_session is not { } session)
        {
            return;
        }

        var actions = ActionFilters.Under(session.Actions, path, kind);
        if (actions.Count == 0)
        {
            return;
        }

        var where = kind is TreeNodeKind.File or TreeNodeKind.Folder ? path : actions[0].Address.Key;
        if (actions.Count > 1 && System.Windows.MessageBox.Show(this, CompatchTexts.ResetQuestion(actions.Count, where), CompatchTexts.ResetTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        Change(actions.Select(a => a.Address).Distinct(), s => s.ResetMany(actions.Select(a => a.Id)));
    }

    /// <summary>Hinweis "A hat sich geändert" (Spec, Fehler und Grenzfälle). Eigene Schreibvorgänge unterdrücken ihn kurz.</summary>
    private void Watch(string folder, string name)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        var watcher = new FileSystemWatcher(folder) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName };
        void Changed(object sender, FileSystemEventArgs e)
        {
            if (DateTime.UtcNow < _suppressWatchUntil)
            {
                return;
            }

            Dispatcher.BeginInvoke(() =>
            {
                _noticeText.Text = CompatchTexts.ChangedNotice(name);
                _notice.Visibility = Visibility.Visible;
            });
        }

        watcher.Changed += Changed;
        watcher.Created += Changed;
        watcher.Deleted += Changed;
        watcher.Renamed += (s, e) => Changed(s, e);
        watcher.EnableRaisingEvents = true;
        _watchers.Add(watcher);
    }
}
