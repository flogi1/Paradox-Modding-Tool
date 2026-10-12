using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.Editor;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Candidates;
using ParadoxModTool.Localization;
using Binding = System.Windows.Data.Binding;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using SelectionMode = System.Windows.Controls.SelectionMode;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// "Modifier ohne Idee" (Spec 2026-10-12-iv2-dynamic-ideas-3-candidates): links die Gruppen des Pakets mit Zahlen und Häkchen "ausblenden", rechts die Liste der
/// Kandidaten (Modifier, die die Mod noch nicht benutzt) mit Suche, "Nur neue", eigener Ausblend-Regex und den Knöpfen "Nicht als Idee" und "Anlegen…".
/// Die Auswahl links ersetzt keine Gruppierung der Liste: es gibt eine flache Liste, die nach der gewählten Gruppe gefiltert wird.
/// </summary>
internal sealed class CandidateWindow : Window
{
    private readonly GeneratorPackage _package;
    private readonly CandidatesDef _def;
    private readonly Func<string?> _modRoot;
    private readonly Func<IReadOnlyList<CandidateSource>> _sources;
    private readonly Func<IReadOnlyList<CandidateRow>, Task<bool>> _create;
    private readonly CandidateSettings _settings;
    private readonly List<GroupEntry> _groups = new();
    private readonly ListBox _groupList = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly ListView _list = new() { SelectionMode = SelectionMode.Extended };
    private readonly GridViewColumn _groupColumn = new() { Header = GeneratorTexts.CandidatesColumnGroup, Width = 170 };
    private readonly TextBox _search;
    private readonly CheckBox _onlyNew = new() { Content = new TextBlock { Text = GeneratorTexts.CandidatesOnlyNew }, ToolTip = GeneratorTexts.CandidatesOnlyNewTip, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _customHide = new() { Width = 170, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = GeneratorTexts.CandidatesCustomHideTip };
    private readonly TextBlock _customError = new() { Text = "⚠", Visibility = Visibility.Collapsed, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly CheckBox _showHidden = new() { Content = new TextBlock { Text = GeneratorTexts.CandidatesShowHidden }, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _notAsIdea = new() { Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 8, 0), ToolTip = GeneratorTexts.CandidatesNotAsIdeaTip };
    private readonly Button _createButton = new() { Content = new TextBlock { Text = GeneratorTexts.CandidatesCreate }, Padding = new Thickness(10, 2, 10, 2), ToolTip = GeneratorTexts.CandidatesCreateTip };
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer _customTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private IReadOnlyList<CandidateSource> _sourceList = Array.Empty<CandidateSource>();
    private IReadOnlySet<string>? _used;
    private IReadOnlyDictionary<string, string> _inStates = new Dictionary<string, string>();
    private IReadOnlyList<CandidateRow> _rows = Array.Empty<CandidateRow>();
    private IReadOnlyList<CandidateRow> _shown = Array.Empty<CandidateRow>();
    private string? _problem;
    private string? _selectedGroup = string.Empty;
    private int _version;
    private bool _closed;
    private bool _skipActivation = true;

    /// <param name="modRoot">Die Mod, deren Benutzung gilt (Arbeitsstand oder aktive Mod); wird bei jedem Neuladen neu gefragt.</param>
    /// <param name="sources">Die Quellen der Kandidatenliste; im UI-Thread aufgerufen.</param>
    /// <param name="create">Legt Arbeitsstände für die Zeilen an; <see langword="true"/>, wenn welche entstanden sind.</param>
    public CandidateWindow(Window owner, GeneratorPackage package, Func<string?> modRoot, Func<IReadOnlyList<CandidateSource>> sources, Func<IReadOnlyList<CandidateRow>, Task<bool>> create)
    {
        _package = package;
        _def = package.Candidates ?? throw new ArgumentException("Das Paket hat keinen Abschnitt candidates.", nameof(package));
        _modRoot = modRoot;
        _sources = sources;
        _create = create;
        _settings = CandidateSettingsStore.Load(package.DataFolder);
        Owner = owner;
        Title = GeneratorTexts.CandidatesTitle + " – " + package.Name;
        Width = 1120;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        BuildGroupEntries();
        BuildList();

        var (searchPanel, search) = SearchBox.Create();
        _search = search;
        _search.ToolTip = GeneratorTexts.CandidatesSearchTip;
        _customHide.Text = _settings.CustomHide;
        _customError.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
        _status.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _notAsIdea.Content = new TextBlock { Text = GeneratorTexts.CandidatesNotAsIdea };

        var filters = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        filters.Children.Add(_onlyNew);
        filters.Children.Add(new TextBlock { Text = GeneratorTexts.CandidatesCustomHide, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        filters.Children.Add(_customHide);
        filters.Children.Add(_customError);
        filters.Children.Add(_showHidden);

        var buttons = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var close = new Button { Content = new TextBlock { Text = CommonTexts.Close }, Padding = new Thickness(12, 2, 12, 2), IsCancel = true };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Right);
        buttons.Children.Add(close);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(_notAsIdea);
        actions.Children.Add(_createButton);
        buttons.Children.Add(actions);

        var right = new DockPanel();
        DockPanel.SetDock(searchPanel, Dock.Top);
        searchPanel.Margin = new Thickness(0, 0, 0, 6);
        right.Children.Add(searchPanel);
        DockPanel.SetDock(filters, Dock.Top);
        right.Children.Add(filters);
        DockPanel.SetDock(buttons, Dock.Bottom);
        right.Children.Add(buttons);
        DockPanel.SetDock(_status, Dock.Bottom);
        right.Children.Add(_status);
        right.Children.Add(_list);

        var root = new Grid { Margin = new Thickness(10) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_groupList, 0);
        root.Children.Add(_groupList);
        var splitter = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetColumn(splitter, 1);
        root.Children.Add(splitter);
        Grid.SetColumn(right, 2);
        root.Children.Add(right);
        Content = root;

        _groupList.SelectedIndex = 1;
        _selectedGroup = string.Empty;
        _groupList.SelectionChanged += GroupList_SelectionChanged;
        _search.TextChanged += (_, _) => Fill();
        _onlyNew.Click += (_, _) => Fill();
        _showHidden.Click += (_, _) => Fill();
        _customHide.TextChanged += (_, _) =>
        {
            _customTimer.Stop();
            _customTimer.Start();
        };
        _customTimer.Tick += (_, _) =>
        {
            _customTimer.Stop();
            ApplyCustomHide();
        };
        _list.SelectionChanged += (_, _) => UpdateButtons();
        _notAsIdea.Click += (_, _) => ToggleNotAsIdea();
        _createButton.Click += Create_Click;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
            {
                GeneratorPackageHelpWindow.Show(this, "generatorhelp.candidates");
                e.Handled = true;
            }
        };

        // Formate und Namen laden im Hintergrund, die Script-Datenbank auch: beides meldet ModifierValueSettings.Changed (die Datenbank über das Neuladen der Formate)
        ModifierValueSettings.Changed += OnIndexChanged;
        // Beim Zurückkehren aus dem Generator-Fenster kann die Mod neue Dateien haben ("Erzeugen") und die Datenbank neu sein
        Activated += (_, _) =>
        {
            if (_skipActivation)
            {
                _skipActivation = false;
                return;
            }

            Reload(reloadUsed: true);
        };
        Closed += (_, _) =>
        {
            _closed = true;
            ModifierValueSettings.Changed -= OnIndexChanged;
            if (_customTimer.IsEnabled)
            {
                _customTimer.Stop();
                ApplyCustomHide();
            }
        };
        Loaded += (_, _) => _search.Focus();
        UpdateButtons();
        Reload(reloadUsed: true);
    }

    /// <summary>Das Paket, aus dem das Fenster gebaut ist; das Generator-Fenster schließt es, wenn ein anderes Paketobjekt gewählt wird.</summary>
    public GeneratorPackage Package => _package;

    // ---------------------------------------------------------------- Gruppen links

    /// <summary>Eine Zeile der Gruppenliste. <see cref="Id"/>: <see langword="null"/> = "Alle", leer = "Allgemein", sonst die Kennung der Gruppe im Paket.</summary>
    private sealed class GroupEntry
    {
        public GroupEntry(string? id, string label, bool canHide)
        {
            Id = id;
            Label = label;
            CanHide = canHide;
        }

        public string? Id { get; }

        public string Label { get; }

        public bool CanHide { get; }

        public TextBlock Text { get; } = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };

        public CheckBox Box { get; } = new() { Content = new TextBlock { Text = GeneratorTexts.CandidatesHide }, ToolTip = GeneratorTexts.CandidatesHideTip, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    }

    private void BuildGroupEntries()
    {
        _groups.Add(new GroupEntry(null, GeneratorTexts.CandidatesGroupAll, canHide: false));
        _groups.Add(new GroupEntry(string.Empty, GeneratorTexts.CandidatesGroupGeneral, canHide: false));
        foreach (var group in _def.Groups)
        {
            _groups.Add(new GroupEntry(group.Id, group.Label, canHide: true));
        }

        foreach (var entry in _groups)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(entry.Text);
            if (entry.CanHide)
            {
                var id = entry.Id!;
                var box = entry.Box;
                Grid.SetColumn(box, 1);
                grid.Children.Add(box);
                box.Click += (_, _) =>
                {
                    _settings.HiddenGroups[id] = box.IsChecked == true;
                    SaveSettings();
                    Rebuild();
                };
            }

            entry.Text.Text = entry.Label;
            _groupList.Items.Add(new ListBoxItem { Content = grid, Tag = entry, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        }
    }

    private bool IsGroupHidden(string id) =>
        _settings.HiddenGroups.TryGetValue(id, out var hidden) ? hidden : _def.Groups.FirstOrDefault(g => g.Id == id)?.Hidden ?? false;

    /// <summary>Zahlen und Häkchen der Gruppen; eine ausgeblendete Gruppe bleibt stehen, grau und mit ihren Zahlen (ein neuer Modifier darin soll nicht unsichtbar sein).</summary>
    private void UpdateGroups()
    {
        foreach (var entry in _groups)
        {
            var rows = entry.Id is null ? _rows : _rows.Where(r => r.Group == entry.Id).ToList();
            var open = rows.Where(r => !r.NotAsIdea).ToList();
            entry.Text.Text = GeneratorTexts.CandidatesGroupCount(entry.Label, open.Count, open.Count(r => r.IsNew));
            var hidden = entry.CanHide && IsGroupHidden(entry.Id!);
            entry.Box.IsChecked = hidden;
            if (hidden)
            {
                entry.Text.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            }
            else
            {
                entry.Text.ClearValue(TextBlock.ForegroundProperty);
            }
        }
    }

    private void GroupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_groupList.SelectedItem is ListBoxItem { Tag: GroupEntry entry })
        {
            _selectedGroup = entry.Id;
            Fill();
        }
    }

    // ---------------------------------------------------------------- Liste rechts

    /// <summary>Eine Zeile der Liste mit den fertigen Anzeigetexten (die Spalten binden an diese Eigenschaften).</summary>
    private sealed record Item(CandidateRow Row, string New, string Name, string Key, string Group, string State, bool Muted);

    private void BuildList()
    {
        var view = new GridView();
        view.Columns.Add(new GridViewColumn { Header = GeneratorTexts.CandidatesColumnNew, DisplayMemberBinding = new Binding(nameof(Item.New)), Width = 46 });
        view.Columns.Add(new GridViewColumn { Header = GeneratorTexts.ModifierColumnName, DisplayMemberBinding = new Binding(nameof(Item.Name)), Width = 290 });
        view.Columns.Add(new GridViewColumn { Header = GeneratorTexts.ModifierColumnKey, DisplayMemberBinding = new Binding(nameof(Item.Key)), Width = 270 });
        _groupColumn.DisplayMemberBinding = new Binding(nameof(Item.Group));
        view.Columns.Add(_groupColumn);
        view.Columns.Add(new GridViewColumn { Header = GeneratorTexts.CandidatesColumnState, DisplayMemberBinding = new Binding(nameof(Item.State)), Width = 110 });
        _list.View = view;
        var muted = (Brush)FindResource("Theme.TextMuted");
        // Auf dem Zeilenstil des Themes aufbauen, sonst fehlen im Dark-Mode die Theme-Farben der Auswahl.
        var style = new Style(typeof(ListViewItem), TryFindResource(typeof(ListViewItem)) as Style);
        var mutedRow = new DataTrigger { Binding = new Binding(nameof(Item.Muted)), Value = true };
        mutedRow.Setters.Add(new Setter(ForegroundProperty, muted));
        style.Triggers.Add(mutedRow);
        _list.ItemContainerStyle = style;
    }

    /// <summary>
    /// Welche Zeilen die Liste zeigt. Eine Suche gilt immer für alle Gruppen. "Nicht als Idee" und von der eigenen Regex getroffene Zeilen erscheinen nur mit
    /// "Ausgeblendete zeigen"; Zeilen einer ausgeblendeten Gruppe außerdem, wenn diese Gruppe gewählt ist oder gesucht wird. Ausgeblendete Zeilen sind gedämpft.
    /// </summary>
    private void Fill()
    {
        var search = _search.Text.Trim();
        var scope = search.Length > 0 ? null : _selectedGroup;
        IEnumerable<CandidateRow> rows = _rows;
        if (scope is not null)
        {
            rows = rows.Where(r => r.Group == scope);
        }

        if (search.Length > 0)
        {
            rows = rows.Where(r => r.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) || r.Key.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (_onlyNew.IsChecked == true)
        {
            rows = rows.Where(r => r.IsNew);
        }

        // "Alle" und die Suche mischen die Gruppen: neue Zeilen nach oben (stabil, sonst bleibt die Reihenfolge von Build); in einer Gruppe stehen sie schon oben
        var inScope = (scope is null ? rows.OrderByDescending(r => r.IsNew) : rows).ToList();
        var showAll = _showHidden.IsChecked == true;
        // Zeilen einer ausgeblendeten Gruppe zeigt die Liste auch, wenn man diese Gruppe wählt oder sucht; "nicht als Idee" und die Regex nie ohne "Ausgeblendete zeigen"
        var revealGroups = scope is not null || search.Length > 0;
        var shown = inScope.Where(r => showAll || ShowsWithoutHidden(r, revealGroups)).ToList();
        _groupColumn.Width = scope is null ? 170 : 0;

        // Dieselben Zeilen: nichts neu zuweisen, sonst springt die Liste nach oben und die Auswahl geht verloren (z. B. beim Zurückkehren ins Fenster)
        if (!shown.SequenceEqual(_shown))
        {
            var selected = _list.SelectedItems.Cast<Item>().Select(i => i.Row.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var labels = _def.Groups.ToDictionary(g => g.Id, g => g.Label);
            var items = shown.Select(r => new Item(
                r,
                r.IsNew ? "✱" : string.Empty,
                r.Name,
                r.Key,
                r.Group.Length == 0 ? GeneratorTexts.CandidatesGroupGeneral : labels.GetValueOrDefault(r.Group, r.Group),
                r.StateKey ?? (r.NotAsIdea ? GeneratorTexts.CandidatesStateNotAsIdea : string.Empty),
                r.Hidden || r.NotAsIdea)).ToList();
            _shown = shown;
            _list.ItemsSource = items;
            foreach (var item in items.Where(i => selected.Contains(i.Row.Key)))
            {
                _list.SelectedItems.Add(item);
            }
        }

        _status.Text = StatusText(shown, inScope.Count - shown.Count);
        UpdateButtons();
    }

    /// <summary>
    /// Zeigt eine Zeile ohne "Ausgeblendete zeigen"? <see cref="CandidateRow.Hidden"/> mischt Gruppe und Regex: ist die Gruppe nicht ausgeblendet, kommt es von
    /// der Regex und die Zeile bleibt weg.
    /// </summary>
    private bool ShowsWithoutHidden(CandidateRow row, bool revealGroups)
    {
        if (row.NotAsIdea)
        {
            return false;
        }

        if (!row.Hidden)
        {
            return true;
        }

        return revealGroups && row.Group.Length > 0 && IsGroupHidden(row.Group);
    }

    private string StatusText(IReadOnlyList<CandidateRow> shown, int hidden)
    {
        if (_problem is not null)
        {
            return _problem;
        }

        if (_used is null)
        {
            return string.Empty;
        }

        return _sourceList.Count == 0 ? GeneratorTexts.ModifierNoneFound : GeneratorTexts.CandidatesStatus(shown.Count, shown.Count(r => r.IsNew), hidden);
    }

    private void UpdateButtons()
    {
        var selected = SelectedRows();
        _createButton.IsEnabled = selected.Count > 0;
        _notAsIdea.IsEnabled = selected.Count > 0;
        ((TextBlock)_notAsIdea.Content).Text = selected.Count > 0 && selected.All(r => r.NotAsIdea) ? GeneratorTexts.CandidatesShowAgain : GeneratorTexts.CandidatesNotAsIdea;
    }

    /// <summary>Die gewählten Zeilen in der Reihenfolge der Liste (nicht der Klicks), damit die Nummern der neuen Stände vorhersehbar folgen.</summary>
    private List<CandidateRow> SelectedRows()
    {
        var keys = _list.SelectedItems.Cast<Item>().Select(i => i.Row.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return keys.Count == 0 ? new List<CandidateRow>() : _shown.Where(r => keys.Contains(r.Key)).ToList();
    }

    // ---------------------------------------------------------------- Laden und Aufbauen

    private void OnIndexChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (_closed)
        {
            return;
        }

        // Ein Fehler hier landet sonst im Dispatcher und beendet PMT (es gibt keinen globalen Handler): in die Statuszeile
        try
        {
            _sourceList = _sources();
            Rebuild();
        }
        catch (Exception ex)
        {
            ShowFailure(ex);
        }
    });

    /// <summary>Zeigt einen Fehler beim Laden oder Aufbauen in der Statuszeile; die Liste bleibt, wie sie ist.</summary>
    private void ShowFailure(Exception ex)
    {
        _problem = ex.Message;
        _status.Text = _problem;
    }

    /// <summary>Lädt neu (bei Bedarf auch die benutzten Modifier der Mod); Fehler stehen in der Statuszeile.</summary>
    private async void Reload(bool reloadUsed)
    {
        try
        {
            await LoadAsync(reloadUsed);
        }
        catch (Exception ex)
        {
            // Rebuild darf hier nicht noch einmal werfen (async void: der Fehler ginge an den Dispatcher)
            ShowFailure(ex);
        }
    }

    private async Task LoadAsync(bool reloadUsed)
    {
        var version = ++_version;
        _sourceList = _sources();
        var modRoot = _modRoot();
        var package = _package;
        var needUsed = reloadUsed || _used is null;
        var (used, problem, inStates) = await Task.Run(() =>
        {
            IReadOnlySet<string>? usedNow = null;
            string? problemNow = null;
            if (needUsed)
            {
                if (modRoot is null || !Directory.Exists(modRoot))
                {
                    problemNow = GeneratorTexts.NoMod;
                }
                else
                {
                    try
                    {
                        usedNow = CandidateList.Used(package, modRoot);
                    }
                    catch (Exception ex) when (ex is GeneratorOutputException or IOException or UnauthorizedAccessException or ArgumentException)
                    {
                        problemNow = ex.Message;
                    }
                }
            }

            IReadOnlyDictionary<string, string> states;
            try
            {
                states = CandidateList.InWorkStates(package);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                states = new Dictionary<string, string>();
            }

            return (usedNow, problemNow, states);
        });
        if (version != _version || _closed)
        {
            return;
        }

        if (needUsed)
        {
            _used = used;
            _problem = problem;
        }

        _inStates = inStates;
        Rebuild();
    }

    /// <summary>Baut die Zeilen aus den gemerkten Quellen, benutzten Modifiern, Arbeitsständen und Einstellungen neu auf.</summary>
    private void Rebuild()
    {
        _rows = _used is null
            ? Array.Empty<CandidateRow>()
            : CandidateList.Build(_def, _sourceList, _used, _inStates, _settings, ModifierValueSettings.Names.Find);
        UpdateGroups();
        Fill();
    }

    // ---------------------------------------------------------------- Einstellungen und Knöpfe

    private void SaveSettings()
    {
        try
        {
            CandidateSettingsStore.Save(_package.DataFolder, _settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, GeneratorTexts.CandidatesTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Übernimmt die eigene Regex nach kurzer Pause; eine ungültige bekommt ein Warnzeichen und wird weder angewendet noch gespeichert.</summary>
    private void ApplyCustomHide()
    {
        var text = _customHide.Text;
        if (text.Trim().Length > 0)
        {
            try
            {
                _ = new Regex(text, RegexOptions.CultureInvariant, CandidateList.MatchTimeout);
            }
            catch (ArgumentException ex)
            {
                _customError.ToolTip = ex.Message;
                _customError.Visibility = Visibility.Visible;
                return;
            }
        }

        _customError.Visibility = Visibility.Collapsed;
        if (text == _settings.CustomHide)
        {
            return;
        }

        _settings.CustomHide = text;
        SaveSettings();
        if (!_closed)
        {
            Rebuild();
        }
    }

    /// <summary>"Nicht als Idee" auf die Auswahl; sind alle schon markiert, nimmt derselbe Knopf ("Wieder anzeigen") die Markierung zurück.</summary>
    private void ToggleNotAsIdea()
    {
        var selected = SelectedRows();
        if (selected.Count == 0)
        {
            return;
        }

        var keys = new HashSet<string>(selected.Select(r => r.Key), StringComparer.OrdinalIgnoreCase);
        if (selected.All(r => r.NotAsIdea))
        {
            _settings.NotAsIdea.RemoveAll(keys.Contains);
        }
        else
        {
            var marked = new HashSet<string>(_settings.NotAsIdea, StringComparer.OrdinalIgnoreCase);
            _settings.NotAsIdea.AddRange(selected.Where(r => marked.Add(r.Key)).Select(r => r.Key));
        }

        SaveSettings();
        Rebuild();
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var rows = SelectedRows();
        if (rows.Count == 0)
        {
            return;
        }

        // Während das Generator-Fenster fragt und liest, darf hier nichts mehr geändert werden
        IsEnabled = false;
        try
        {
            await _create(rows);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, GeneratorTexts.CandidatesTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
        }

        if (!_closed)
        {
            Reload(reloadUsed: false);
        }
    }
}

/// <summary>
/// Frage vor dem Anlegen (Abschnitt <c>candidates.create.ask</c>): je gefragtem Feld eine Auswahl (Feld vom Typ Auswahl) oder ein Textfeld, Vorgabe leer.
/// OK ist erst möglich, wenn jedes gefragte Schlüsselfeld einen Wert hat; die übrigen Felder dürfen leer bleiben (bei IV2 der Unter-Reiter).
/// </summary>
internal sealed class CandidateAskWindow : Window
{
    private readonly List<(string Id, Func<string> Value)> _inputs = new();
    private readonly Button _ok;
    private readonly List<string> _required = new();
    private Dictionary<string, string>? _result;

    private CandidateAskWindow(GeneratorPackage package, int count)
    {
        Title = GeneratorTexts.CandidatesAskTitle;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = GeneratorTexts.CandidatesAskMessage(count), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        foreach (var id in package.Candidates!.Create.Ask)
        {
            var field = package.Head.FirstOrDefault(f => f.Id == id);
            var key = package.KeyFields.Contains(id, StringComparer.Ordinal);
            panel.Children.Add(new TextBlock { Text = (field?.Label ?? id) + (key ? " *" : string.Empty), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2) });
            if (field?.Type == FieldType.Choice && field.Choices is { Count: > 0 } choices)
            {
                var combo = new ComboBox { ItemsSource = choices, SelectedIndex = -1, Margin = new Thickness(0, 0, 0, 10) };
                combo.SelectionChanged += (_, _) => UpdateOk();
                panel.Children.Add(combo);
                _inputs.Add((id, () => combo.SelectedItem as string ?? string.Empty));
            }
            else
            {
                var box = new TextBox { Margin = new Thickness(0, 0, 0, 10), VerticalContentAlignment = VerticalAlignment.Center };
                box.TextChanged += (_, _) => UpdateOk();
                panel.Children.Add(box);
                _inputs.Add((id, () => box.Text.Trim()));
            }

            if (key)
            {
                _required.Add(id);
            }
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _ok = new Button { Content = new TextBlock { Text = CommonTexts.Ok }, Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        _ok.Click += (_, _) =>
        {
            _result = _inputs.ToDictionary(i => i.Id, i => i.Value(), StringComparer.Ordinal);
            DialogResult = true;
        };
        var cancel = new Button { Content = new TextBlock { Text = CommonTexts.Cancel }, Width = 80, IsCancel = true };
        buttons.Children.Add(_ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
        UpdateOk();
    }

    private void UpdateOk() => _ok.IsEnabled = _required.All(id => _inputs.First(i => i.Id == id).Value().Length > 0);

    /// <summary>Fragt die Angaben ab; <see langword="null"/> bei Abbruch. Feldkennung → Wert (leer, wenn nichts gewählt).</summary>
    public static Dictionary<string, string>? Ask(Window owner, GeneratorPackage package, int count)
    {
        var window = new CandidateAskWindow(package, count) { Owner = owner };
        return window.ShowDialog() == true ? window._result : null;
    }
}
