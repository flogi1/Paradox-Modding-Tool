using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using ParadoxModTool.Desync;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.Settings;
using Binding = System.Windows.Data.Binding;
using Controls = System.Windows.Controls;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using OpenFolderDialog = Microsoft.Win32.OpenFolderDialog;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// Fenster "Desync-Analyse" (Spec, Abschnitt 2): links die Sitzungen mit ihren OOS-Paaren und die hinzugefügten Pfade, rechts Übersicht, Logs vor
/// dem OOS und Zustandsvergleich des gewählten Paares. Zeigt nur an, was <see cref="DesyncService"/> liefert; Lesen und Vergleichen laufen im
/// Hintergrund, Ergebnisse kommen über den Dispatcher zurück und gelten nur, solange dasselbe Paar gewählt ist. Es läuft höchstens ein
/// Vergleich zugleich (Speicherrahmen); er läuft weiter, wenn ein anderes Paar gewählt wird, und endet mit dem Fenster.
/// </summary>
public sealed partial class DesyncWindow : Window
{
    private static readonly int[] DayChoices = { 1, 3, 7, 14, 30 };
    private const int DefaultDays = 7;
    private const int MaxValueLength = 300;

    private readonly DesyncService _service;
    private readonly ErrorLogService _errorLog;

    // Links.
    private readonly Controls.TreeView _list = new();
    private readonly Controls.Button _addDump = SmallButton(DesyncTexts.AddDump);
    private readonly Controls.Button _addFolder = SmallButton(DesyncTexts.AddFolder);
    private readonly Controls.Button _refresh = SmallButton(DesyncTexts.Refresh);
    private readonly Controls.Button _remove = SmallButton(DesyncTexts.RemoveAdded);
    private readonly Controls.Border _problems = new() { Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _problemsText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Controls.ProgressBar _listBusy = new() { Height = 4, IsIndeterminate = true, Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed };

    // Rechts.
    private readonly Controls.TabControl _tabs = new();
    private readonly Controls.StackPanel _overview = new() { Margin = new Thickness(10) };
    private readonly Controls.ComboBox _days = new() { Width = 70, Margin = new Thickness(6, 0, 0, 0) };
    private readonly Controls.Border _logNotice = new() { Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _logNoticeText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Controls.ListView _logs = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.Button _start = SmallButton(DesyncTexts.StateStart);
    private readonly Controls.Button _cancel = SmallButton(DesyncTexts.StateCancel);
    private readonly Controls.CheckBox _showNoise = new() { Content = DesyncTexts.ShowNoise, Margin = new Thickness(12, 0, 0, 4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBlock _stateInfo = new() { Margin = new Thickness(12, 0, 0, 4), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly Controls.TextBlock _needsBoth = new() { Text = DesyncTexts.StateNeedsBoth, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.StackPanel _progressRow = new() { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.ProgressBar _progress = new() { Width = 220, Height = 14, Minimum = 0, Maximum = 100 };
    private readonly Controls.TextBlock _progressText = new() { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.Border _stateNotice = new() { Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _stateNoticeText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Controls.StackPanel _recurring = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.TreeView _stateTree = new() { Margin = new Thickness(0, 6, 0, 0) };

    private OosPair? _pair;
    private OosSession? _session;
    private string? _selectedAdded;
    private int _selection;
    private bool _rebuilding;
    private IReadOnlyList<TimelineEntry> _timeline = Array.Empty<TimelineEntry>();
    private bool _modsPending;
    private CancellationTokenSource? _compare;
    private string? _comparing;
    private DesyncPhase? _progressPhase;
    private double _progressFloor;
    private IReadOnlyList<RankedDiff>? _ranked;
    private readonly ConditionalWeakTable<DesyncDiffResult, IReadOnlyList<RankedDiff>> _rankings = new();

    /// <param name="settings">Programmeinstellungen; das Fenster merkt sich (noch) keine eigene Position und liest daraus derzeit nichts.</param>
    /// <param name="owner">null: eigenständiges Fenster mit eigenem Eintrag in der Taskleiste.</param>
    /// <param name="host">Bug-Tracker des Hauptfensters für den Bericht "PMT-Bugtracker".</param>
    public DesyncWindow(DesyncService service, ErrorLogService errorLog, AppSettings settings, Window? owner, DesyncWindowHost host)
    {
        _service = service;
        _errorLog = errorLog;
        _host = host;
        Title = DesyncTexts.WindowTitle(errorLog.Host.GameName);
        Width = 1250;
        Height = 800;
        MinWidth = 700;
        MinHeight = 400;
        if (owner is not null)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        // Wie das Fehlerprotokoll: nur das eigenständige Fenster hat einen eigenen Eintrag in der Taskleiste.
        ShowInTaskbar = owner is null;

        foreach (var border in new[] { _problems, _logNotice, _stateNotice })
        {
            border.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.NoticeBackground");
        }

        _stateInfo.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _problems.Child = _problemsText;
        _logNotice.Child = _logNoticeText;
        _stateNotice.Child = _stateNoticeText;

        var body = new Controls.Grid();
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 260 });
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(6) });
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(2.2, GridUnitType.Star), MinWidth = 380 });
        body.Children.Add(BuildLeft());
        var splitter = new Controls.GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        Controls.Grid.SetColumn(splitter, 1);
        body.Children.Add(splitter);
        BuildTabs();
        Controls.Grid.SetColumn(_tabs, 2);
        body.Children.Add(_tabs);

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        root.Children.Add(body);
        Content = root;

        _list.SelectedItemChanged += (_, _) => OnListSelection();
        _addDump.Click += (_, _) => AddDump();
        _addFolder.Click += (_, _) => AddFolder();
        _refresh.Click += (_, _) => _service.Refresh();
        _remove.Click += (_, _) => RemoveAdded();
        _days.SelectionChanged += (_, _) =>
        {
            if (!_rebuilding && _pair is not null)
            {
                LoadLogs(_pair, _selection);
            }
        };
        _logs.MouseDoubleClick += (_, _) => JumpToSelectedLog();
        _start.Click += (_, _) => StartCompare();
        _cancel.Click += (_, _) => _compare?.Cancel();
        _showNoise.Click += (_, _) => RenderStateTree();

        _service.Changed += OnServiceChanged;
        _errorLog.Session.Changed += OnErrorSessionChanged;
        Closed += (_, _) =>
        {
            _service.Changed -= OnServiceChanged;
            _errorLog.Session.Changed -= OnErrorSessionChanged;
            _compare?.Cancel();
        };

        ShowPairNone();
        RebuildList();
        _service.Refresh();
    }

    // ---------------------------------------------------------------- Aufbau

    private Controls.DockPanel BuildLeft()
    {
        var buttons = new Controls.WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        buttons.Children.Add(_addDump);
        buttons.Children.Add(_addFolder);
        buttons.Children.Add(_refresh);
        buttons.Children.Add(_remove);
        _remove.IsEnabled = false;

        var left = new Controls.DockPanel();
        foreach (var top in new UIElement[] { buttons, _listBusy, _problems })
        {
            Controls.DockPanel.SetDock(top, Controls.Dock.Top);
            left.Children.Add(top);
        }

        left.Children.Add(_list);
        return left;
    }

    private void BuildTabs()
    {
        _tabs.Items.Add(new Controls.TabItem
        {
            Header = DesyncTexts.TabOverview,
            Content = new Controls.ScrollViewer
            {
                Content = _overview,
                VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Controls.ScrollBarVisibility.Disabled,
            },
        });

        foreach (var days in DayChoices)
        {
            _days.Items.Add(new Controls.ComboBoxItem { Content = days.ToString(System.Globalization.CultureInfo.CurrentCulture), Tag = days });
        }

        _days.SelectedIndex = Array.IndexOf(DayChoices, DefaultDays);
        var daysRow = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        daysRow.Children.Add(Label(DesyncTexts.LogsWindow));
        daysRow.Children.Add(_days);
        var grid = new Controls.GridView();
        grid.Columns.Add(Column(DesyncTexts.LogsColumnDate, nameof(LogRow.Date), 95));
        // Breit genug für die längste Übersetzung von "2 (Client) – nur auf dieser Maschine" (Französisch, fett).
        grid.Columns.Add(Column(DesyncTexts.LogsColumnMachine, nameof(LogRow.Machine), 290));
        grid.Columns.Add(Column(DesyncTexts.LogsColumnSource, nameof(LogRow.Source), 190));
        grid.Columns.Add(Column(DesyncTexts.LogsColumnMessage, nameof(LogRow.Message), 520));
        grid.Columns.Add(Column(DesyncTexts.LogsColumnMod, nameof(LogRow.Mod), 160));
        _logs.View = grid;
        _logs.ItemContainerStyle = LogRowStyle(TryFindResource(typeof(Controls.ListViewItem)) as Style);
        var logs = new Controls.DockPanel { Margin = new Thickness(6) };
        Controls.DockPanel.SetDock(daysRow, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_logNotice, Controls.Dock.Top);
        logs.Children.Add(daysRow);
        logs.Children.Add(_logNotice);
        logs.Children.Add(_logs);
        _tabs.Items.Add(new Controls.TabItem { Header = DesyncTexts.TabLogs, Content = logs });

        var actions = new Controls.WrapPanel();
        actions.Children.Add(_start);
        actions.Children.Add(_cancel);
        actions.Children.Add(_showNoise);
        actions.Children.Add(_stateInfo);
        _cancel.Visibility = Visibility.Collapsed;
        _progressRow.Children.Add(_progress);
        _progressRow.Children.Add(_progressText);
        var state = new Controls.DockPanel { Margin = new Thickness(6) };
        foreach (var top in new UIElement[] { actions, _needsBoth, _progressRow, _stateNotice, _recurring })
        {
            Controls.DockPanel.SetDock(top, Controls.Dock.Top);
            state.Children.Add(top);
        }

        state.Children.Add(_stateTree);
        _tabs.Items.Add(new Controls.TabItem { Header = DesyncTexts.TabState, Content = state });
        _tabs.Items.Add(new Controls.TabItem { Header = DesyncTexts.TabReport, Content = BuildReportTab() });
        _tabs.Items.Add(new Controls.TabItem { Header = DesyncTexts.TabSafety, Content = BuildSafetyTab() });
    }

    private static Controls.GridViewColumn Column(string header, string property, double width) =>
        new() { Header = header, Width = width, DisplayMemberBinding = new Binding(property) };

    /// <summary>Spuren in der Hinweisfarbe, Meldungen nur einer Maschine fett; aufbauend auf dem Stil des Themas (Textfarbe, Zeilenvorlage).</summary>
    private static Style LogRowStyle(Style? themeStyle)
    {
        var style = new Style(typeof(Controls.ListViewItem), themeStyle);
        var lead = new DataTrigger { Binding = new Binding(nameof(LogRow.IsLead)), Value = true };
        lead.Setters.Add(new Setter(Controls.Control.BackgroundProperty, new DynamicResourceExtension("Theme.NoticeBackground")));
        style.Triggers.Add(lead);
        var only = new DataTrigger { Binding = new Binding(nameof(LogRow.OnlyHere)), Value = true };
        only.Setters.Add(new Setter(Controls.Control.FontWeightProperty, FontWeights.SemiBold));
        style.Triggers.Add(only);
        style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(nameof(LogRow.FullMessage))));
        return style;
    }

    // ---------------------------------------------------------------- Liste links

    private void OnServiceChanged(object? sender, EventArgs e) => RebuildList();

    /// <summary>Baut die Liste aus dem Katalog neu; die Auswahl (Paar oder Pfad) bleibt erhalten.</summary>
    private void RebuildList()
    {
        _listBusy.Visibility = _service.IsRefreshing ? Visibility.Visible : Visibility.Collapsed;
        _problemsText.Text = string.Join("\n", _service.Problems);
        _problems.Visibility = _service.Problems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var selectedPair = _pair?.OosId16;
        var selectedAdded = _selectedAdded;
        Controls.TreeViewItem? reselect = null;
        _rebuilding = true;
        try
        {
            _list.Items.Clear();
            foreach (var session in _service.Sessions)
            {
                var times = session.Pairs.Select(p => _service.InfoOf(p).Written).OfType<DateTime>().ToList();
                var sessionItem = new Controls.TreeViewItem
                {
                    Header = times.Count > 0 ? DesyncTexts.SessionLabel(times.Min(), session.Pairs.Count) : session.Key,
                    IsExpanded = true,
                    Tag = session,
                };
                foreach (var pair in session.Pairs)
                {
                    var pairItem = new Controls.TreeViewItem { Header = PairHeader(pair), Tag = new PairNode(session, pair) };
                    sessionItem.Items.Add(pairItem);
                    if (pair.OosId16 == selectedPair)
                    {
                        reselect = pairItem;
                    }
                }

                _list.Items.Add(sessionItem);
            }

            foreach (var source in _service.AddedSources)
            {
                var item = new Controls.TreeViewItem { Header = AddedHeader(source), Tag = source, ToolTip = source.Path };
                _list.Items.Add(item);
                if (string.Equals(source.Path, selectedAdded, StringComparison.OrdinalIgnoreCase))
                {
                    reselect = item;
                }
            }
        }
        finally
        {
            _rebuilding = false;
        }

        if (reselect is not null)
        {
            reselect.IsSelected = true;
        }
        else if (selectedPair is not null || selectedAdded is not null)
        {
            OnListSelection();
        }
    }

    private object PairHeader(OosPair pair)
    {
        var info = _service.InfoOf(pair);
        var panel = new Controls.StackPanel();
        panel.Children.Add(new Controls.TextBlock { Text = info.Written is { } time ? DesyncTexts.PairLabel(pair.OosId16, time) : pair.OosId16 });
        foreach (var machine in pair.MissingMachineIds)
        {
            // oos_player_name nennt den Spieler der abgewichenen Maschine; für andere Maschinen ist der Name unbekannt.
            var player = machine == pair.OosMachineId ? pair.Dumps.Select(d => d.PlayerName).FirstOrDefault(n => n is not null) : null;
            var folder = info.FolderName ?? pair.OosId16 + "_0";
            var missing = new Controls.TextBlock
            {
                Text = DesyncTexts.MissingMachine(machine.ToString(System.Globalization.CultureInfo.CurrentCulture), player ?? "?", folder),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420,
            };
            missing.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Warning");
            panel.Children.Add(missing);
        }

        return panel;
    }

    /// <summary>Hinzugefügter Pfad als Dateiname (der volle Pfad steht im Tooltip des Eintrags).</summary>
    private static object AddedHeader(DesyncAddedSource source)
    {
        var panel = new Controls.StackPanel();
        var name = Path.GetFileName(source.Path.TrimEnd('\\', '/'));
        var path = new Controls.TextBlock { Text = name.Length > 0 ? name : source.Path };
        if (!source.Exists)
        {
            path.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        }

        panel.Children.Add(path);
        if (source.Problem is { } problem)
        {
            var text = new Controls.TextBlock { Text = problem, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };
            text.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Warning");
            panel.Children.Add(text);
        }

        return panel;
    }

    private void OnListSelection()
    {
        if (_rebuilding)
        {
            return;
        }

        var tag = (_list.SelectedItem as Controls.TreeViewItem)?.Tag;
        _selectedAdded = (tag as DesyncAddedSource)?.Path;
        _remove.IsEnabled = _selectedAdded is not null;
        if (tag is PairNode node)
        {
            if (!ReferenceEquals(_pair, node.Pair))
            {
                ShowPair(node.Session, node.Pair);
            }
        }
        else if (tag is not OosSession)
        {
            if (_pair is not null && _selectedAdded is null)
            {
                ShowPairNone();
            }
        }
    }

    private void AddDump()
    {
        var dialog = new OpenFileDialog { Filter = DesyncTexts.ZipFilter, Multiselect = true, Title = DesyncTexts.AddDump };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("desync.dump");
        if (dialog.ShowDialog(this) == true)
        {
            AddPaths(dialog.FileNames);
        }
    }

    private void AddFolder()
    {
        var dialog = new OpenFolderDialog { Multiselect = true, Title = DesyncTexts.AddFolder };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("desync.folder");
        if (dialog.ShowDialog(this) == true)
        {
            AddPaths(dialog.FolderNames);
        }
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!Store(() => _service.AddPath(path)))
            {
                return;
            }
        }
    }

    private void RemoveAdded()
    {
        if (_selectedAdded is { } path)
        {
            Store(() => _service.RemovePath(path));
        }
    }

    /// <summary><c>sources.json</c> ließ sich nicht lesen oder schreiben: Meldung mit dem Grund des Systems, die Liste bleibt.</summary>
    private bool Store(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, DesyncTexts.SourcesSaveFailed(ex.Message), DesyncTexts.Menu, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    // ---------------------------------------------------------------- Auswahl

    private void ShowPairNone()
    {
        _selection++;
        _pair = null;
        _session = null;
        _overview.Children.Clear();
        _timeline = Array.Empty<TimelineEntry>();
        _logs.ItemsSource = null;
        _logNotice.Visibility = Visibility.Collapsed;
        _ranked = null;
        RenderStateControls();
        ClearStateResult();
        ClearReport();
        OnSafetyPairChanged();
    }

    private void ShowPair(OosSession session, OosPair pair)
    {
        var selection = ++_selection;
        _pair = pair;
        _session = session;
        _overview.Children.Clear();
        _timeline = Array.Empty<TimelineEntry>();
        _logs.ItemsSource = null;
        _logNotice.Visibility = Visibility.Collapsed;
        _ranked = null;
        LoadOverview(pair, selection);
        LoadLogs(pair, selection);
        RenderStateControls();
        ClearStateResult();
        LoadState(session, pair, selection);
        LoadReport(pair, selection);
        OnSafetyPairChanged();
    }

    private bool IsCurrent(int selection) => selection == _selection && IsLoaded;

    // ---------------------------------------------------------------- Übersicht

    private async void LoadOverview(OosPair pair, int selection)
    {
        OosSummary summary;
        try
        {
            summary = await Task.Run(() => _service.SummaryOf(pair));
        }
        catch (Exception ex) when (ex is OosDumpException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (IsCurrent(selection))
            {
                _overview.Children.Clear();
                _overview.Children.Add(Notice(DesyncTexts.DumpError(pair.OosId16, ex.Message)));
            }

            return;
        }

        if (IsCurrent(selection))
        {
            RenderOverview(pair, summary);
        }
    }

    private void RenderOverview(OosPair pair, OosSummary summary)
    {
        var panel = _overview;
        panel.Children.Clear();

        // Warnungen zuerst: unterschiedliche Versionen, DLCs und Mods sind häufige echte Ursachen.
        foreach (var warning in summary.Warnings)
        {
            var text = WarningText(warning.Kind);
            panel.Children.Add(Notice(string.IsNullOrWhiteSpace(warning.Detail) ? text : text + " " + warning.Detail));
        }

        panel.Children.Add(Text(DesyncTexts.OverviewOosId + " " + (summary.OosIdFull ?? summary.OosId16)));
        var date = summary.OosGameDate ?? summary.Machines.Select(m => m.Save.Date).FirstOrDefault(d => d is not null);
        panel.Children.Add(Text(DesyncTexts.OverviewGameDate + " " + (date ?? "?")));
        panel.Children.Add(Text(DesyncTexts.OverviewFrames(summary.StartFrame, summary.OosFrame)));

        var seeds = summary.Machines.Select(m => m.Save.RandomSeed?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?").Distinct().ToList();
        panel.Children.Add(Text(DesyncTexts.OverviewRandomSeed(string.Join(" / ", seeds))));

        // Gleiche Zählerstände beweisen nie, dass der Zufall gleich lief; der Hinweis steht immer daneben.
        var countValues = summary.Machines.Select(m => m.Save.RandomCount).ToList();
        var counts = string.Join(" / ", countValues.Select(c => c?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"));
        if (countValues.Count == 2 && countValues[0] is { } first && countValues[1] is { } second)
        {
            counts += " (Δ " + (first - second).ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        }
        var countLine = new Controls.WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
        countLine.Children.Add(new Controls.TextBlock { Text = DesyncTexts.OverviewRandomCount(counts) });
        countLine.Children.Add(Muted("  " + DesyncTexts.RandomCountNote));
        panel.Children.Add(countLine);

        AddHeading(panel, DesyncTexts.OverviewChecksums);
        foreach (var category in summary.ChecksumCategories)
        {
            if (_service.Profile.Describe(category) is { } info)
            {
                panel.Children.Add(new Controls.TextBlock
                {
                    Text = InLanguage(info.Name) is { } name ? name + " (" + category + ")" : category,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                });
                if (InLanguage(info.Explain) is { } explain)
                {
                    panel.Children.Add(Text(explain));
                }

                foreach (var cause in info.Causes.Select(InLanguage).OfType<string>())
                {
                    panel.Children.Add(Text("• " + cause));
                }
            }
            else
            {
                panel.Children.Add(Text(DesyncTexts.ChecksumUnknown(category)));
            }
        }

        foreach (var machine in summary.Machines)
        {
            // oos_player_name ist der Spieler der abgewichenen Maschine, auf allen Maschinen derselbe Wert.
            var player = machine.MachineId == pair.OosMachineId ? machine.PlayerName : null;
            AddHeading(panel, DesyncTexts.OverviewMachine(machine.MachineId.ToString(System.Globalization.CultureInfo.CurrentCulture), RoleText(machine.Role), player ?? "?"));
            var version = new[] { machine.Save.Version, Short(machine.GameCommit), Short(machine.EngineCommit) }.Where(v => !string.IsNullOrEmpty(v));
            panel.Children.Add(Text(DesyncTexts.OverviewVersion + " " + string.Join(" · ", version)));
            panel.Children.Add(Text(DesyncTexts.OverviewDlcs + " " + (machine.Save.EnabledDlcs.Count > 0 ? string.Join(", ", machine.Save.EnabledDlcs) : "–")));
            panel.Children.Add(Text(DesyncTexts.OverviewMods + " " + (machine.Mods.Count > 0 ? string.Join(", ", machine.Mods.Select(DesyncTexts.ModLabel)) : "–")));
            panel.Children.Add(Text(DesyncTexts.OverviewRandomLog(machine.HasRandomLog)));
            panel.Children.Add(Text(DesyncTexts.OverviewCallstacks(machine.HasCallstacks)));
        }
    }

    private static string WarningText(SummaryWarningKind kind) => kind switch
    {
        SummaryWarningKind.DifferentGameCommit => DesyncTexts.WarnGameCommit,
        SummaryWarningKind.DifferentEngineCommit => DesyncTexts.WarnEngineCommit,
        SummaryWarningKind.DifferentDlcs => DesyncTexts.WarnDlcs,
        SummaryWarningKind.DifferentMods => DesyncTexts.WarnMods,
        _ => DesyncTexts.WarnMissing,
    };

    private static string RoleText(string? role) => role switch
    {
        "Host" => DesyncTexts.RoleHost,
        "Client" => DesyncTexts.RoleClient,
        _ => role ?? "?",
    };

    private static string? Short(string? commit) => commit is { Length: > 12 } ? commit[..12] : commit;

    /// <summary>Text des Profils in der Sprache der geladenen Oberflächentexte (wie das Fehlerprotokoll), sonst Englisch, sonst Deutsch.</summary>
    private string? InLanguage(IReadOnlyDictionary<string, string> texts) =>
        texts.GetValueOrDefault(_errorLog.Session.Language) ?? texts.GetValueOrDefault("en") ?? texts.GetValueOrDefault("de") ?? texts.Values.FirstOrDefault();

    // ---------------------------------------------------------------- Logs vor dem OOS

    private async void LoadLogs(OosPair pair, int selection)
    {
        var days = (_days.SelectedItem as Controls.ComboBoxItem)?.Tag as int? ?? DefaultDays;
        IReadOnlyList<TimelineEntry> timeline;
        try
        {
            timeline = await Task.Run(() => _service.TimelineOf(pair, days));
        }
        catch (Exception ex) when (ex is OosDumpException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (IsCurrent(selection))
            {
                _logs.ItemsSource = null;
                ShowNotice(_logNotice, _logNoticeText, DesyncTexts.DumpError(pair.OosId16, ex.Message));
            }

            return;
        }

        // Ein späterer Wechsel der Spieltage überholt diesen Lauf: nur das Ergebnis der aktuellen Auswahl zählt.
        if (!IsCurrent(selection) || days != ((_days.SelectedItem as Controls.ComboBoxItem)?.Tag as int? ?? DefaultDays))
        {
            return;
        }

        _timeline = timeline;
        if (timeline.Count > 0 && timeline.All(e => e.GameDate is null))
        {
            ShowNotice(_logNotice, _logNoticeText, DesyncTexts.NoTicks);
        }
        else
        {
            _logNotice.Visibility = Visibility.Collapsed;
        }

        RenderLogs();
    }

    private void RenderLogs()
    {
        var session = _errorLog.Session;
        _logs.ItemsSource = _timeline.Select(entry => new LogRow(entry, entry.Error is { } error ? session.AttributionOf(error).Mod?.Name ?? string.Empty : string.Empty)).ToList();
    }

    /// <summary>
    /// Index und Mod-Liste des Fehlerprotokolls ändern die Zuordnung; die Spalte "Mod", die Hinweise des Berichts und die der Verdachtsliste
    /// (MP-Prüfung) werden einmal danach neu berechnet.
    /// </summary>
    private void OnErrorSessionChanged(object? sender, EventArgs e)
    {
        if (_modsPending)
        {
            return;
        }

        _modsPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _modsPending = false;
            RefreshReportLeads();
            RefreshSafetyLeads();
            if (_timeline.Count > 0)
            {
                var selected = (_logs.SelectedItem as LogRow)?.Entry;
                RenderLogs();
                if (selected is not null)
                {
                    _logs.SelectedItem = _logs.Items.OfType<LogRow>().FirstOrDefault(r => ReferenceEquals(r.Entry, selected));
                }
            }
        }));
    }

    private void JumpToSelectedLog()
    {
        if (_logs.SelectedItem is LogRow { Entry.Error: { } error } && _errorLog.Session.JumpTargetOf(error) is { } target)
        {
            _errorLog.Host.JumpTo(target.FilePath, target.Line);
        }
    }

    // ---------------------------------------------------------------- Zustandsvergleich

    private static bool HasBothSides(OosPair pair) => pair.Reference is not null && pair.Desynced is not null;

    /// <summary>Knöpfe, Fortschritt und Hinweis "beide Dumps nötig" für das gewählte Paar und den laufenden Vergleich.</summary>
    private void RenderStateControls()
    {
        var pair = _pair;
        var running = _compare is not null;
        var runningHere = running && pair is not null && pair.OosId16 == _comparing;
        _needsBoth.Visibility = pair is not null && !HasBothSides(pair) ? Visibility.Visible : Visibility.Collapsed;
        _start.IsEnabled = pair is not null && HasBothSides(pair) && !running;
        _cancel.Visibility = runningHere ? Visibility.Visible : Visibility.Collapsed;
        _progressRow.Visibility = runningHere ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClearStateResult()
    {
        _stateInfo.Text = string.Empty;
        _stateNotice.Visibility = Visibility.Collapsed;
        _recurring.Children.Clear();
        _stateTree.Items.Clear();
    }

    /// <summary>Bekanntes oder gespeichertes Ergebnis sofort zeigen ("ein zweites Öffnen ist sofort da"); sonst wartet der Knopf.</summary>
    private async void LoadState(OosSession session, OosPair pair, int selection)
    {
        if (!HasBothSides(pair))
        {
            return;
        }

        try
        {
            // Immer über den Dienst: er prüft den Stempel der Spielstände, ein ausgetauschter Dump gilt nicht als bekannt.
            var result = await _service.LoadCachedAsync(pair);
            if (result is not null && IsCurrent(selection))
            {
                await ShowResult(pair, result, selection);
            }

            // Ergebnisse der übrigen Paare der Sitzung für die Zeilen "weicht in N von M OOS ab".
            var loadedOther = false;
            foreach (var other in session.Pairs.Where(p => p.OosId16 != pair.OosId16 && HasBothSides(p)))
            {
                if (!IsCurrent(selection))
                {
                    return;
                }

                if (await _service.LoadCachedAsync(other) is not null)
                {
                    loadedOther = true;
                }
            }

            if (loadedOther && IsCurrent(selection) && _ranked is not null)
            {
                await RenderRecurring(session, selection);
            }
        }
        catch (Exception ex) when (ex is OosDumpException or IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            if (IsCurrent(selection))
            {
                ShowNotice(_stateNotice, _stateNoticeText, DesyncTexts.DumpError(pair.OosId16, ex.Message));
            }
        }
    }

    private async void StartCompare()
    {
        if (_pair is not { } pair || _compare is not null || !HasBothSides(pair))
        {
            return;
        }

        var cancel = new CancellationTokenSource();
        _compare = cancel;
        _comparing = pair.OosId16;
        _progressPhase = null;
        _progressFloor = 0;
        _progress.Value = 0;
        _progressText.Text = string.Empty;
        // Das alte Ergebnis gilt nicht mehr: "Rauschen anzeigen" während des Laufs zeichnet es sonst wieder.
        _ranked = null;
        ClearStateResult();
        RenderStateControls();
        var progress = new Progress<DesyncProgress>(p =>
        {
            if (ReferenceEquals(_compare, cancel))
            {
                ShowProgress(p);
            }
        });

        try
        {
            var result = await _service.CompareAsync(pair, progress, cancel.Token);
            Finish();
            if (_pair?.OosId16 == pair.OosId16 && IsLoaded)
            {
                await ShowResult(_pair, result, _selection);
            }
        }
        catch (OperationCanceledException)
        {
            Finish();
        }
        catch (Exception ex) when (ex is OosDumpException or IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            Finish();
            if (_pair?.OosId16 == pair.OosId16 && IsLoaded)
            {
                ShowNotice(_stateNotice, _stateNoticeText, DesyncTexts.DumpError(pair.OosId16, ex.Message));
            }
        }

        void Finish()
        {
            _compare = null;
            _comparing = null;
            cancel.Dispose();
            if (IsLoaded)
            {
                RenderStateControls();
            }
        }
    }

    /// <summary>Der Balken läuft je Abschnitt nur vorwärts: die beiden Seiten melden im zweiten Durchgang nebeneinander und können leicht zurückspringen.</summary>
    private void ShowProgress(DesyncProgress progress)
    {
        if (progress.Phase != _progressPhase)
        {
            _progressPhase = progress.Phase;
            _progressFloor = 0;
        }

        _progressFloor = Math.Max(_progressFloor, Math.Clamp(progress.Fraction, 0, 1) * 100);
        _progress.Value = _progressFloor;
        _progressText.Text = DesyncTexts.StateProgress(DesyncTexts.StatePhase(progress.Phase), _progressFloor);
    }

    private async Task ShowResult(OosPair pair, DesyncDiffResult result, int selection)
    {
        ClearStateResult();
        var notes = new List<string>();
        AddStateNote(notes, DesyncTexts.ColumnReference, result.ReferenceState, result.ReferenceStoppedAt);
        AddStateNote(notes, DesyncTexts.ColumnDesynced, result.DesyncedState, result.DesyncedStoppedAt);
        var note = _service.NoteOf(pair);
        if (note?.CacheSaveError is { } saveError)
        {
            notes.Add(DesyncTexts.CacheSaveFailed(saveError));
        }

        if (notes.Count > 0)
        {
            ShowNotice(_stateNotice, _stateNoticeText, string.Join("\n", notes));
        }

        if (note?.CachedAt is { } cachedAt)
        {
            _stateInfo.Text = DesyncTexts.Cached(cachedAt);
        }

        var ranked = await Task.Run(() => RankedOf(result));
        if (!IsCurrent(selection))
        {
            return;
        }

        _ranked = ranked;
        RenderStateTree();
        RebuildReport(selection);
        OnSafetyDiffChanged(pair, result);
        if (_session is { } session)
        {
            await RenderRecurring(session, selection);
        }
    }

    private static void AddStateNote(List<string> notes, string side, SaveReadState state, long? stoppedAt)
    {
        if (state == SaveReadState.Binary)
        {
            if (!notes.Contains(DesyncTexts.StateBinary))
            {
                notes.Add(DesyncTexts.StateBinary);
            }
        }
        else if (state == SaveReadState.Incomplete)
        {
            notes.Add(stoppedAt is { } offset ? DesyncTexts.StateIncompleteAt(side, offset) : DesyncTexts.StateIncomplete(side));
        }
    }

    /// <summary>Manager mit Ursachen in mehr als einem verglichenen Paar der Sitzung; nur bei mehr als einem verglichenen Paar.</summary>
    private async Task RenderRecurring(OosSession session, int selection)
    {
        var results = session.Pairs
            .Select(p => _service.ResultOf(p))
            .OfType<DesyncDiffResult>()
            .Where(r => r.ReferenceState == SaveReadState.Complete && r.DesyncedState == SaveReadState.Complete)
            .ToList();
        _recurring.Children.Clear();
        if (results.Count < 2)
        {
            return;
        }

        var recurring = await Task.Run(() => DesyncRanking.Recurring(results.Select(RankedOf).ToList()));
        if (!IsCurrent(selection))
        {
            return;
        }

        _recurring.Children.Clear();
        // Die oberste Ebene (Manager "") ist kein Objekttyp und wird nicht als wiederkehrend genannt.
        foreach (var (manager, count) in recurring.Where(r => r.Manager.Length > 0))
        {
            _recurring.Children.Add(Text(DesyncTexts.Recurring(manager, count, results.Count)));
        }
    }

    /// <summary>
    /// Gewichtete Liste eines Ergebnisses, je Ergebnis nur einmal berechnet (eine Auswahl rankt sonst alle Paare der Sitzung neu). Die Tabelle
    /// ist threadsicher und hält ein Ergebnis nicht am Leben; aufrufbar aus dem Hintergrund.
    /// </summary>
    private IReadOnlyList<RankedDiff> RankedOf(DesyncDiffResult result) =>
        _rankings.GetValue(result, r => DesyncRanking.Rank(r.Units, _service.Profile, new SaveNameResolver(r.Names, _service.Profile)));

    /// <summary>
    /// Baum nach Gewicht, Manager, Einheit und Feldern. Ursachen sind aufgeklappt, Folgen zu, Rauschen nur mit dem Schalter. Kinder entstehen erst
    /// beim Aufklappen (ein Paar kann tausende Einheiten mit je bis zu 200 Feldern haben).
    /// </summary>
    private void RenderStateTree()
    {
        _stateTree.Items.Clear();
        if (_ranked is not { } ranked)
        {
            return;
        }

        var showNoise = _showNoise.IsChecked == true;
        foreach (var group in ranked.GroupBy(d => d.Weight).OrderBy(g => g.Key))
        {
            if (group.Key == DiffWeight.Noise && !showNoise)
            {
                continue;
            }

            var units = group.ToList();
            var weightItem = Lazy(
                Header(WeightText(group.Key) + " (" + DesyncTexts.EntryCount(units.Count) + ")", FontWeights.SemiBold),
                item => FillManagers(item, units, showNoise, group.Key == DiffWeight.Cause));
            _stateTree.Items.Add(weightItem);
            weightItem.IsExpanded = group.Key == DiffWeight.Cause;
        }
    }

    private void FillManagers(Controls.TreeViewItem parent, IReadOnlyList<RankedDiff> units, bool showNoise, bool expand)
    {
        foreach (var manager in units.GroupBy(d => d.Manager, StringComparer.Ordinal))
        {
            var managerUnits = manager.ToList();
            var name = manager.Key.Length == 0 ? DesyncTexts.TopLevel : manager.Key;
            var item = Lazy(Header(name + " (" + DesyncTexts.EntryCount(managerUnits.Count) + ")", FontWeights.Normal),
                child => FillUnits(child, managerUnits, showNoise));
            parent.Items.Add(item);
            item.IsExpanded = expand;
        }
    }

    private void FillUnits(Controls.TreeViewItem parent, IReadOnlyList<RankedDiff> units, bool showNoise)
    {
        foreach (var unit in units)
        {
            var fields = VisibleFields(unit, showNoise);
            var header = Header(UnitText(unit, fields.Count), FontWeights.Normal);
            // Ausgetauschte Slots werden nie feldweise verglichen und haben keine Kinder.
            parent.Items.Add(unit.Unit.Change == UnitChange.SlotReused || fields.Count == 0
                ? new Controls.TreeViewItem { Header = header }
                : Lazy(header, child => FillFields(child, unit.Weight, fields)));
        }
    }

    private static void FillFields(Controls.TreeViewItem parent, DiffWeight unitWeight, IReadOnlyList<RankedField> fields)
    {
        foreach (var field in fields)
        {
            var block = new Controls.TextBlock { TextWrapping = TextWrapping.NoWrap };
            block.Inlines.Add(new Run(field.Field.Path.Length == 0 ? "/" : field.Field.Path) { FontWeight = FontWeights.SemiBold });
            block.Inlines.Add(MutedRun("   " + DesyncTexts.ColumnReference + ": "));
            block.Inlines.Add(new Run(Value(field.Field.ReferenceValue)));
            block.Inlines.Add(MutedRun("   " + DesyncTexts.ColumnDesynced + ": "));
            block.Inlines.Add(new Run(Value(field.Field.DesyncedValue)));
            block.ToolTip = (field.Field.ReferenceValue ?? "–") + "\n" + (field.Field.DesyncedValue ?? "–");
            if (field.Weight > unitWeight)
            {
                block.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
            }

            parent.Items.Add(new Controls.TreeViewItem { Header = block });
        }
    }

    /// <summary>Felder der Einheit, Ursachen zuerst (stabil, sonst in Vergleichsreihenfolge); Rauschen nur mit dem Schalter.</summary>
    private static IReadOnlyList<RankedField> VisibleFields(RankedDiff unit, bool showNoise) =>
        unit.Fields.Where(f => showNoise || f.Weight != DiffWeight.Noise).OrderBy(f => f.Weight).ToList();

    private static string UnitText(RankedDiff diff, int fieldCount)
    {
        var unit = diff.Unit;
        var path = diff.DisplayPath.Length == 0 ? DesyncTexts.TopLevel : diff.DisplayPath;
        var parts = new List<string>();
        switch (unit.Change)
        {
            case UnitChange.SlotReused:
                parts.Add(DesyncTexts.SlotReused(unit.ReferenceLabel ?? "?", unit.ReferenceGeneration, unit.DesyncedLabel ?? "?", unit.DesyncedGeneration));
                break;
            case UnitChange.OnlyReference:
                parts.Add(DesyncTexts.OnlyReference);
                break;
            case UnitChange.OnlyDesynced:
                parts.Add(DesyncTexts.OnlyDesynced);
                break;
            case UnitChange.TooLarge:
                parts.Add(DesyncTexts.TooLarge(Math.Max(unit.ReferenceSize, unit.DesyncedSize)));
                break;
            default:
                path += " (" + DesyncTexts.FieldCount(fieldCount) + ")";
                break;
        }

        if (unit.LineFallback)
        {
            parts.Add(DesyncTexts.LineFallback);
        }

        if (unit.FieldsTruncated)
        {
            parts.Add(DesyncTexts.FieldsTruncated);
        }

        return parts.Count == 0 ? path : path + " – " + string.Join(" – ", parts);
    }

    private static string WeightText(DiffWeight weight) => weight switch
    {
        DiffWeight.Cause => DesyncTexts.WeightCause,
        DiffWeight.Consequence => DesyncTexts.WeightConsequence,
        _ => DesyncTexts.WeightNoise,
    };

    private static string Value(string? value)
    {
        if (value is null)
        {
            return "–";
        }

        var flat = value.Replace("\r", string.Empty).Replace('\n', ' ').Replace('\t', ' ');
        return flat.Length > MaxValueLength ? flat[..MaxValueLength] + "…" : flat;
    }

    /// <summary>Eintrag mit Platzhalter-Kind, damit der Pfeil erscheint; <paramref name="fill"/> baut die echten Kinder beim ersten Aufklappen.</summary>
    private static Controls.TreeViewItem Lazy(object header, Action<Controls.TreeViewItem> fill)
    {
        var item = new Controls.TreeViewItem { Header = header };
        item.Items.Add(new Controls.TreeViewItem());
        var filled = false;
        item.Expanded += (_, e) =>
        {
            if (filled || !ReferenceEquals(e.OriginalSource, item))
            {
                return;
            }

            filled = true;
            item.Items.Clear();
            fill(item);
        };
        return item;
    }

    // ---------------------------------------------------------------- Hilfen

    private static void ShowNotice(Controls.Border border, Controls.TextBlock text, string message)
    {
        text.Text = message;
        border.Visibility = Visibility.Visible;
    }

    private static Controls.Border Notice(string text)
    {
        var border = new Controls.Border { Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 6) };
        border.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.NoticeBackground");
        border.Child = new Controls.TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        return border;
    }

    private static Controls.TextBlock Header(string text, FontWeight weight) => new() { Text = text, FontWeight = weight };

    private static Run MutedRun(string text)
    {
        var run = new Run(text);
        run.SetResourceReference(TextElement.ForegroundProperty, "Theme.TextMuted");
        return run;
    }

    private static Controls.Button SmallButton(string text) =>
        new() { Content = new Controls.TextBlock { Text = text }, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 6, 4) };

    private static Controls.TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    private static Controls.TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2) };

    private static Controls.TextBlock Muted(string text)
    {
        var block = new Controls.TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        return block;
    }

    private static void AddHeading(Controls.Panel panel, string text) =>
        panel.Children.Add(new Controls.TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 2), TextWrapping = TextWrapping.Wrap });

    private sealed record PairNode(OosSession Session, OosPair Pair);

    /// <summary>Zeile der Log-Liste; die Eigenschaften sind die Spalten (Bindung über den Namen).</summary>
    private sealed class LogRow
    {
        public LogRow(TimelineEntry entry, string mod)
        {
            Entry = entry;
            Mod = mod;
            var line = entry.Line;
            Date = entry.GameDate ?? line.Time;
            var machine = entry.MachineId.ToString(System.Globalization.CultureInfo.CurrentCulture) + (entry.Role is null ? string.Empty : " (" + RoleText(entry.Role) + ")");
            Machine = entry.OnlyThisMachine ? machine + " – " + DesyncTexts.OnlyOnThisMachine : machine;
            Source = DesyncTexts.LogSource(entry.Source) + (line.Source.Length > 0 ? " · " + line.Source + (line.SourceLine is { } at ? ":" + at : string.Empty) : string.Empty);
            var lead = entry.Lead switch
            {
                LeadKind.InvalidCommand => DesyncTexts.LeadInvalidCommand + ": ",
                LeadKind.ScriptError => DesyncTexts.LeadScriptError + ": ",
                _ => string.Empty,
            };
            // Skriptfehler stehen über mehrere Zeilen ("Script system error!" allein sagt nichts): in der Spalte als eine Zeile.
            FullMessage = line.Message;
            Message = lead + Value(string.Join(" · ", line.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
        }

        public TimelineEntry Entry { get; }

        public string Date { get; }

        public string Machine { get; }

        public string Source { get; }

        public string Message { get; }

        public string FullMessage { get; }

        public string Mod { get; }

        public bool IsLead => Entry.Lead != LeadKind.None;

        public bool OnlyHere => Entry.OnlyThisMachine;
    }
}
