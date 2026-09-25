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
    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private int _runId;
    private DateTime _suppressWatchUntil = DateTime.MinValue;
    private readonly Controls.Button _rerun = new() { Content = CompatchTexts.Rerun, Padding = new Thickness(8, 1, 8, 1) };

    private readonly Controls.ComboBox _stageFilter = new() { Width = 150, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.ComboBox _originFilter = new() { Width = 130, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.TextBox _folderFilter = new() { Width = 140, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.CheckBox _uncertainOnly = new() { Content = CompatchTexts.FilterUncertain, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.CheckBox _mainAsBase = new() { Content = CompatchTexts.MainAsBase, ToolTip = CompatchTexts.MainAsBaseTip, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.CheckBox _fillMissing = new() { Content = CompatchTexts.FillMissing, ToolTip = CompatchTexts.FillMissingTip, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.TextBox _search = new() { Width = 160, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.Button _recheck = new() { Content = CompatchTexts.RecheckAll, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
    private readonly Controls.Button _saveButton = new() { Content = CompatchTexts.SaveState, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
    private readonly Controls.Button _writeButton = new() { Content = CompatchTexts.WritePatch, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.Bold };
    private readonly Controls.CheckBox _cautious = new() { Content = CompatchTexts.CautiousToggle, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.Button _cancel = new() { Content = CompatchTexts.Cancel, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0), IsEnabled = false };
    private readonly Controls.ProgressBar _progress = new() { Width = 120, Height = 14, IsIndeterminate = false, Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _counters = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.WrapPanel _skipped = new() { Margin = new Thickness(0, 2, 0, 0) };
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

    public CompatchWindow(CompatchHost host, CompatchState state, CompatchStateStore store)
    {
        _host = host;
        _state = state;
        _store = store;
        var targetName = state.TargetModId is { } id && host.Registry.Get(id) is { } target ? target.Name : state.TargetFolder;
        Title = CompatchTexts.WindowTitle(state.ModAName, state.ModBName, targetName);
        Width = 1300;
        Height = 820;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _cautious.IsChecked = state.Cautious;
        _mainAsBase.IsChecked = state.MainReplacesVanilla;
        _fillMissing.IsChecked = state.FillMissingFromMain;
        var mainOptions = MainSide() is null ? Visibility.Collapsed : Visibility.Visible;
        _mainAsBase.Visibility = mainOptions;
        _fillMissing.Visibility = mainOptions;
        _status.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _notice.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.NoticeBackground");

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        var top = new Controls.StackPanel();
        Controls.DockPanel.SetDock(top, Controls.Dock.Top);
        var toolbar = new Controls.WrapPanel();
        toolbar.Children.Add(Label(CompatchTexts.ViewLabel));
        toolbar.Children.Add(_viewPicker);
        toolbar.Children.Add(Label(CompatchTexts.FilterStage));
        toolbar.Children.Add(_stageFilter);
        toolbar.Children.Add(Label(CompatchTexts.FilterOrigin));
        toolbar.Children.Add(_originFilter);
        toolbar.Children.Add(Label(CompatchTexts.FilterFolder));
        toolbar.Children.Add(_folderFilter);
        toolbar.Children.Add(_uncertainOnly);
        toolbar.Children.Add(_mainAsBase);
        toolbar.Children.Add(_fillMissing);
        toolbar.Children.Add(Label(CompatchTexts.Search));
        toolbar.Children.Add(_search);
        toolbar.Children.Add(_recheck);
        toolbar.Children.Add(_saveButton);
        toolbar.Children.Add(_cautious);
        toolbar.Children.Add(_writeButton);
        toolbar.Children.Add(_cancel);
        toolbar.Children.Add(_progress);
        top.Children.Add(toolbar);
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
        _listView.Children.Add(_tree);
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
        _folderFilter.TextChanged += (_, _) => RefreshFilters();
        _search.TextChanged += (_, _) => RefreshFilters();
        _uncertainOnly.Click += (_, _) => RefreshFilters();
        _mainAsBase.Click += (_, _) => MainOptionsChanged();
        _fillMissing.Click += (_, _) => MainOptionsChanged();
        _tree.SelectedItemChanged += (_, _) => OnActionSelected(SelectedAction);
        _tree.MouseDoubleClick += (_, _) =>
        {
            if (SelectedAction is { } action)
            {
                OpenCompare(action);
            }
        };
        _recheck.Click += (_, _) =>
        {
            _session?.RecheckAll();
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
        _cancel.Click += (_, _) => _cts?.Cancel();
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
    private IReadOnlyList<MergeAction> FilteredActions() =>
        _session is null ? Array.Empty<MergeAction>() : ActionOrder.Sort(_session.Actions.Where(Matches));

    private static Controls.TextBlock Label(string text) => new() { Text = text, VerticalAlignment = System.Windows.VerticalAlignment.Center };

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
            _status.Text = CompatchTexts.Loaded(session.Actions.Count, watch.Elapsed.TotalSeconds) + (_store.Warning is { } warning ? "  " + warning : string.Empty);
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

    private void SetBusy(bool busy)
    {
        _progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        _progress.IsIndeterminate = busy;
        _cancel.IsEnabled = busy;
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

        return new CompatchInput
        {
            VanillaFolder = _host.VanillaRoot,
            ModAFolder = _state.ModAFolder,
            ModBFolder = _state.ModBFolder,
            BLoadsAfterA = _state.BLoadsAfterA,
            Rules = rules,
            KeyOperators = _host.Profile?.KeyOperators ?? new List<string>(),
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
    internal string BaseColumnTitle => _state.MainReplacesVanilla && MainSide() is not null ? CompatchTexts.ColumnBase(MainName) : CompatchTexts.ColumnVanilla;

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
        if (_viewPicker.SelectedIndex != (int)view)
        {
            _switchingView = true;
            _viewPicker.SelectedIndex = (int)view;
            _switchingView = false;
        }

        _tabs.SelectedIndex = 0;
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
            _folderFilter.Text = string.Empty;
            _search.Text = string.Empty;
            _uncertainOnly.IsChecked = false;
            RefreshTree();
            SelectInTree(id, edit);
        }
    }

    private bool SelectInTree(string id, bool edit)
    {
        bool Find(Controls.ItemCollection items, List<Controls.TreeViewItem> ancestors)
        {
            foreach (Controls.TreeViewItem item in items)
            {
                if (item.Tag is MergeAction action && action.Id == id)
                {
                    foreach (var ancestor in ancestors)
                    {
                        ancestor.IsExpanded = true;
                    }

                    item.IsSelected = true;
                    item.BringIntoView();
                    if (edit)
                    {
                        ShowEditor(true);
                    }

                    return true;
                }

                if (Find(item.Items, ancestors.Append(item).ToList()))
                {
                    return true;
                }
            }

            return false;
        }

        return Find(_tree.Items, new List<Controls.TreeViewItem>());
    }

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

        _skipped.Children.Clear();
        foreach (var skip in _session.Run.Skipped.Where(s => s.Reason != SkipReason.Metadata))
        {
            var row = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 0) };
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

            _skipped.Children.Add(row);
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

        if (_folderFilter.Text.Length > 0 && !TreePath.FileOf(action).Contains(_folderFilter.Text.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _search.Text.Length == 0
            || action.DisplayPath.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase)
            || action.Address.Key.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ordnerbaum Ordner → Datei → Stelle (Spec Nachtrag 2). Aufgeklappte Knoten und die Auswahl bleiben erhalten.</summary>
    private void RefreshTree()
    {
        if (_session is null)
        {
            return;
        }

        var session = _session;
        var keepId = SelectedAction?.Id;
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Collect(Controls.ItemCollection items)
        {
            foreach (Controls.TreeViewItem item in items)
            {
                if (item.IsExpanded && item.Tag is string key)
                {
                    expanded.Add(key);
                }

                Collect(item.Items);
            }
        }

        Collect(_tree.Items);
        var firstRun = _tree.Items.Count == 0;
        _tree.Items.Clear();
        Controls.TreeViewItem? select = null;
        var nodes = ActionTree.Build(FilteredActions(), a => IsOpenConflict(session, a), a => a.Stage is MergeStage.Conflict or MergeStage.Review or MergeStage.Outdated && !session.IsExplicit(a.Id));

        Controls.TreeViewItem Build(ActionTreeNode node, List<Controls.TreeViewItem> ancestors)
        {
            if (node.Kind == TreeNodeKind.Action)
            {
                var action = node.Action!;
                var marks = (action.Uncertain ? "  ⚠" : string.Empty)
                    + (session.IsExplicit(action.Id) ? "  •" : string.Empty)
                    + (session.IsSeen(action.Id) ? "  ✓" : string.Empty);
                var leaf = new Controls.TreeViewItem { Header = CompatchTreeBuilder.ActionHeader(StageToken(session, action), action.DisplayPath + marks), Tag = action };
                if (action.Id == keepId)
                {
                    select = leaf;
                    foreach (var ancestor in ancestors)
                    {
                        ancestor.IsExpanded = true;
                    }
                }

                return leaf;
            }

            var key = (node.Kind == TreeNodeKind.Folder ? "d:" : "f:") + node.Path;
            var item = new Controls.TreeViewItem
            {
                Header = CompatchTreeBuilder.Header(node),
                Tag = key,
                IsExpanded = expanded.Contains(key) || (firstRun && node.OpenConflicts > 0),
            };
            var chain = ancestors.Append(item).ToList();
            foreach (var child in node.Children)
            {
                item.Items.Add(Build(child, chain));
            }

            return item;
        }

        foreach (var node in nodes)
        {
            _tree.Items.Add(Build(node, new List<Controls.TreeViewItem>()));
        }

        if (select is not null)
        {
            select.IsSelected = true;
        }
        else
        {
            OnActionSelected(null);
        }
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
