using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.SearchReplace.ReplaceLists;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace ParadoxModTool.App;

public enum ReplaceRuleRowState
{
    Ok,
    Invalid,
    Zero,
}

/// <summary>Eine Zeile der Regeltabelle; Änderungen an Aktiv, Suchen oder Ersetzen melden <see cref="Edited"/>.</summary>
public sealed class ReplaceRuleRow : INotifyPropertyChanged
{
    private bool _enabled = true;
    private string _search = string.Empty;
    private string _replace = string.Empty;
    private string _matches = string.Empty;
    private ReplaceRuleRowState _state;
    private string? _tip;
    private RuleMode _mode;
    private bool _safeComment;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? Edited;

    public bool Enabled { get => _enabled; set => SetEdited(ref _enabled, value); }

    public string Search { get => _search; set => SetEdited(ref _search, value ?? string.Empty); }

    public string Replace
    {
        get => _replace;
        set
        {
            if (Set(ref _replace, value ?? string.Empty))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SafeEnabled)));
                Edited?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public RuleMode Mode { get => _mode; set => SetEdited(ref _mode, value); }

    public bool SafeComment { get => _safeComment; set => SetEdited(ref _safeComment, value); }

    /// <summary>"Klammer schützen" wirkt nur bei einer Ersetzung, die mit # beginnt (Auskommentieren).</summary>
    public bool SafeEnabled => Replace.StartsWith('#');

    /// <summary>Auswahl der Regelart in der Tabelle.</summary>
    public sealed record ModeChoice(RuleMode Mode, string Text);

    public static IReadOnlyList<ModeChoice> ModeChoices => new[]
    {
        new ModeChoice(RuleMode.Word, ReplaceListTexts.ModeWord),
        new ModeChoice(RuleMode.Text, ReplaceListTexts.ModeText),
        new ModeChoice(RuleMode.Pattern, ReplaceListTexts.ModePattern),
    };

    public string Matches { get => _matches; set => Set(ref _matches, value); }

    public ReplaceRuleRowState State { get => _state; set => Set(ref _state, value); }

    public string? Tip { get => _tip; set => Set(ref _tip, value); }

    public ReplaceRule ToRule() => new() { Search = Search, Replace = Replace, Enabled = Enabled, Mode = Mode, SafeComment = SafeComment };

    public static ReplaceRuleRow From(ReplaceRule rule) =>
        new() { _enabled = rule.Enabled, _search = rule.Search ?? string.Empty, _replace = rule.Replace ?? string.Empty, _mode = rule.Mode, _safeComment = rule.SafeComment };

    private void SetEdited<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Set(ref field, value, name))
        {
            Edited?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

/// <summary>
/// Tab "Ersetzungslisten" im Compare und Update Tool (Spec 2026-09-30-replace-lists, Abschnitt 3). Jede Änderung wird sofort über
/// <see cref="ReplaceListStore.Update"/> gespeichert; die Treffer werden 500 ms nach der letzten Änderung im Hintergrund neu gezählt.
/// </summary>
public partial class ReplaceListTab : UserControl
{
    private readonly ObservableCollection<ReplaceRuleRow> _rows = new();
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private Func<string?> _modRoot = () => null;
    private Func<string, string> _storeFile = _ => string.Empty;
    private Func<IReadOnlyList<ReplaceFileChange>, string, string, FixOutcome> _apply = (_, _, _) => new FixOutcome(false, string.Empty);
    private ReplaceListStore? _store;
    private string? _root;
    private ReplaceList? _current;
    private bool _loading;
    private bool _dirty;
    private CancellationTokenSource? _count;
    private string _stateStatus = string.Empty;

    public ReplaceListTab()
    {
        InitializeComponent();
        RulesGrid.ItemsSource = _rows;
        RulesGrid.PreviewGotKeyboardFocus += (_, e) =>
        {
            if (e.NewFocus is FrameworkElement { DataContext: ReplaceRuleRow row })
            {
                RulesGrid.SelectedItem = row;
            }
        };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            SaveCurrent();
            Recount();
        };
    }

    public void Initialize(Func<string?> modRoot, Func<string, string> storeFile, Func<IReadOnlyList<ReplaceFileChange>, string, string, FixOutcome> apply)
    {
        _modRoot = modRoot;
        _storeFile = storeFile;
        _apply = apply;
    }

    /// <summary>Beim Anzeigen des Tabs und nach einem Wechsel des Mod-Ordners: Listen der aktuellen Mod neu laden.</summary>
    public void Refresh()
    {
        FlushPending();
        var root = _modRoot();
        _root = string.IsNullOrEmpty(root) || !Directory.Exists(root) ? null : root;
        _store = _root is null ? null : new ReplaceListStore(_storeFile(_root));
        var selected = _current?.Name;
        LoadLists(selected);
    }

    private void FlushPending()
    {
        if (_debounce.IsEnabled)
        {
            _debounce.Stop();
            SaveCurrent();
        }
    }

    private void LoadLists(string? select)
    {
        _loading = true;
        try
        {
            ListCombo.Items.Clear();
            var set = TryStore(s => s.Load());
            if (set is not null)
            {
                foreach (var list in set.Lists)
                {
                    ListCombo.Items.Add(list.Name);
                }
            }

            ListCombo.SelectedItem = ListCombo.Items.Cast<string>().FirstOrDefault(n => n == select)
                ?? ListCombo.Items.Cast<string>().FirstOrDefault();
            ShowList(set?.Lists.FirstOrDefault(l => l.Name == (string?)ListCombo.SelectedItem));
        }
        finally
        {
            _loading = false;
        }

        Recount();
    }

    private void ShowList(ReplaceList? list)
    {
        _current = list;
        foreach (var row in _rows)
        {
            row.Edited -= Row_Edited;
        }

        _rows.Clear();
        foreach (var rule in list?.Rules ?? new List<ReplaceRule>())
        {
            AddRow(ReplaceRuleRow.From(rule));
        }

        FolderText.Text = list is null ? string.Empty : string.IsNullOrEmpty(list.TargetFolder) ? "." : list.TargetFolder;
        UpdateEnabledState();
        UpdateProblems();
    }

    private void AddRow(ReplaceRuleRow row, int? index = null)
    {
        row.Edited += Row_Edited;
        _rows.Insert(index ?? _rows.Count, row);
    }

    private void Row_Edited(object? sender, EventArgs e) => Changed();

    /// <summary>Regeln geändert: Fehler sofort zeigen, speichern und zählen verzögert.</summary>
    private void Changed()
    {
        if (_loading)
        {
            return;
        }

        _dirty = true;
        _count?.Cancel();
        UpdateProblems();
        _debounce.Stop();
        _debounce.Start();
    }

    private void UpdateEnabledState()
    {
        var hasMod = _root is not null;
        var hasList = hasMod && _current is not null;
        ListCombo.IsEnabled = hasMod;
        NewButton.IsEnabled = hasMod;
        ImportButton.IsEnabled = hasMod;
        RenameButton.IsEnabled = hasList;
        DeleteButton.IsEnabled = hasList;
        ExportButton.IsEnabled = hasList;
        FolderButton.IsEnabled = hasList;
        RuleButtons.IsEnabled = hasList;
        RulesGrid.IsEnabled = hasList;
        RunButton.IsEnabled = hasList && TargetExists();
        var state = !hasMod ? ReplaceListTexts.NoMod
            : _current is null ? ReplaceListTexts.NoList
            : !TargetExists() ? ReplaceListTexts.FolderMissing(FolderText.Text)
            : string.Empty;

        // Eigene Zustandsmeldungen ersetzen; Ergebnis- und Fehlermeldungen (Ausführen, Speichern) bleiben stehen.
        if (state.Length > 0 || StatusText.Text == _stateStatus)
        {
            StatusText.Text = state;
        }

        _stateStatus = state;
    }

    private string? TargetPath() =>
        _root is null || _current is null ? null : Path.GetFullPath(Path.Combine(_root, ReplaceListStore.SafeTargetFolder(_current.TargetFolder).Replace('/', Path.DirectorySeparatorChar)));

    private bool TargetExists() => TargetPath() is { } path && Directory.Exists(path);

    private void UpdateProblems()
    {
        var problems = RuleMatcher.Validate(_rows.Select(r => r.ToRule()).ToList());
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var problem = problems[i];
            if (problem != RuleProblem.None)
            {
                row.State = ReplaceRuleRowState.Invalid;
                row.Tip = problem switch
                {
                    RuleProblem.EmptySearch => ReplaceListTexts.ProblemEmpty,
                    RuleProblem.NotAWord => ReplaceListTexts.ProblemNotAWord,
                    RuleProblem.Multiline => ReplaceListTexts.ProblemMultiline,
                    RuleProblem.BadCapture => ReplaceListTexts.ProblemBadCapture,
                    _ => ReplaceListTexts.ProblemDuplicate,
                };
                row.Matches = string.Empty;
            }
            else if (row.State == ReplaceRuleRowState.Invalid)
            {
                row.State = ReplaceRuleRowState.Ok;
                row.Tip = null;
            }
        }

        // Ungültige Regeln laufen nicht; die Statuszeile sagt das, damit es nicht nur am roten Text und Tooltip hängt.
        var invalid = problems.Count(p => p != RuleProblem.None);
        var invalidStatus = invalid > 0 ? ReplaceListTexts.InvalidCount(invalid) : string.Empty;
        if (StatusText.Text.Length == 0 || StatusText.Text == _invalidStatus)
        {
            StatusText.Text = invalidStatus;
        }

        _invalidStatus = invalidStatus;
    }

    private string _invalidStatus = string.Empty;

    private void SaveCurrent()
    {
        if (!_dirty || _current is null)
        {
            return;
        }

        _dirty = false;
        var name = _current.Name;
        var folder = _current.TargetFolder;
        var rules = _rows.Select(r => r.ToRule()).ToList();
        _current.Rules = rules;
        TryStore(s => s.Update(set =>
        {
            if (set.Lists.FirstOrDefault(l => l.Name == name) is { } list)
            {
                list.Rules = rules;
            }
            else
            {
                set.Lists.Add(new ReplaceList { Name = name, TargetFolder = folder, Rules = rules });
            }

            return set;
        }));
    }

    private T? TryStore<T>(Func<ReplaceListStore, T> action) where T : class
    {
        if (_store is null)
        {
            return null;
        }

        try
        {
            return action(_store);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            StatusText.Text = ReplaceListTexts.StoreFailed(ex.Message);
            return null;
        }
    }

    private void Recount()
    {
        _count?.Cancel();
        UpdateEnabledState();
        if (TargetPath() is not { } folder || !Directory.Exists(folder))
        {
            return;
        }

        var cancel = new CancellationTokenSource();
        _count = cancel;
        var rules = _rows.Select(r => r.ToRule()).ToList();
        var rows = _rows.ToList();
        if (StatusText.Text.Length == 0)
        {
            StatusText.Text = ReplaceListTexts.Counting;
        }
        Task.Run(() => ReplaceListRunner.Run(rules, ReplaceListRunner.ReadFolder(folder, cancel.Token), cancel.Token), cancel.Token)
            .ContinueWith(task => Dispatcher.BeginInvoke(() =>
            {
                if (cancel.IsCancellationRequested || !ReferenceEquals(_count, cancel))
                {
                    return;
                }

                if (StatusText.Text == ReplaceListTexts.Counting)
                {
                    StatusText.Text = task.Exception is { } ex ? ReplaceListTexts.StoreFailed(ex.GetBaseException().Message) : string.Empty;
                }

                if (task.Status != TaskStatus.RanToCompletion)
                {
                    return;
                }

                var problems = RuleMatcher.Validate(rules);
                for (var i = 0; i < rows.Count; i++)
                {
                    if (problems[i] != RuleProblem.None)
                    {
                        continue;
                    }

                    var count = task.Result.RuleCounts[i];
                    rows[i].Matches = rows[i].Enabled ? count.ToString(System.Globalization.CultureInfo.CurrentCulture) : string.Empty;
                    rows[i].State = rows[i].Enabled && count == 0 ? ReplaceRuleRowState.Zero : ReplaceRuleRowState.Ok;
                    rows[i].Tip = rows[i].State == ReplaceRuleRowState.Zero ? ReplaceListTexts.ZeroMatchesTip : null;
                }

                UpdateProblems();
            }), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private void ListCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        FlushPending();
        var name = ListCombo.SelectedItem as string;
        _loading = true;
        try
        {
            ShowList(TryStore(s => s.Load())?.Lists.FirstOrDefault(l => l.Name == name));
        }
        finally
        {
            _loading = false;
        }

        Recount();
    }

    private Window? Owner => Window.GetWindow(this);

    private string? AskName(string initial, string? currentName)
    {
        var set = TryStore(s => s.Load()) ?? new ReplaceListSet();
        while (true)
        {
            var name = TextPromptWindow.Ask(Owner!, ReplaceListTexts.NameTitle, ReplaceListTexts.NamePrompt, initial)?.Trim();
            if (name is null)
            {
                return null;
            }

            if (ReplaceListStore.IsValidName(set, name, currentName))
            {
                return name;
            }

            MessageBox.Show(Owner!, ReplaceListTexts.NameInvalid, ReplaceListTexts.NameTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            initial = name;
        }
    }

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        FlushPending();
        var set = TryStore(s => s.Load()) ?? new ReplaceListSet();
        if (AskName(ReplaceListStore.UniqueName(set, ReplaceListTexts.NewListName), null) is not { } name)
        {
            return;
        }

        TryStore(s => s.Update(current =>
        {
            current.Lists.Add(new ReplaceList { Name = ReplaceListStore.UniqueName(current, name) });
            return current;
        }));
        LoadLists(name);
    }

    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null)
        {
            return;
        }

        FlushPending();
        var old = _current.Name;
        if (AskName(old, old) is not { } name || name == old)
        {
            return;
        }

        TryStore(s => s.Update(set =>
        {
            if (set.Lists.FirstOrDefault(l => l.Name == old) is { } list)
            {
                list.Name = name;
            }

            return set;
        }));
        LoadLists(name);
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null
            || MessageBox.Show(Owner!, ReplaceListTexts.DeleteConfirm(_current.Name), ReplaceListTexts.Delete, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _debounce.Stop();
        _dirty = false;
        var name = _current.Name;
        TryStore(s => s.Update(set =>
        {
            set.Lists.RemoveAll(l => l.Name == name);
            return set;
        }));
        _current = null;
        LoadLists(null);
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null)
        {
            return;
        }

        FlushPending();
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = ReplaceListTexts.FileFilter, FileName = _current.Name };
        if (dialog.ShowDialog(Owner) != true)
        {
            return;
        }

        try
        {
            ReplaceListStore.Export(_current, dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = ReplaceListTexts.StoreFailed(ex.Message);
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        FlushPending();
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = ReplaceListTexts.FileFilter };
        if (dialog.ShowDialog(Owner) != true)
        {
            return;
        }

        ReplaceList imported;
        try
        {
            imported = ReplaceListStore.Import(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            StatusText.Text = ReplaceListTexts.ImportFailed(ex.Message);
            return;
        }

        var name = imported.Name;
        TryStore(s => s.Update(set =>
        {
            imported.Name = ReplaceListStore.UniqueName(set, string.IsNullOrWhiteSpace(name) ? ReplaceListTexts.NewListName : name);
            name = imported.Name;
            set.Lists.Add(imported);
            return set;
        }));
        LoadLists(name);
    }

    private void FolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_root is null || _current is null)
        {
            return;
        }

        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(TargetExists() ? TargetPath()! : _root),
            ClientGuid = GameDialogMemory.For("replaceList.folder"),
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var relative = Path.GetRelativePath(_root, dialog.SelectedPath);
        if (relative == ".." || Path.IsPathRooted(relative) || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            MessageBox.Show(Owner!, ReplaceListTexts.FolderOutsideMod, ReplaceListTexts.TargetFolderLabel.TrimEnd(':', ' '), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        FlushPending();
        var folder = relative == "." ? string.Empty : relative.Replace(Path.DirectorySeparatorChar, '/');
        var name = _current.Name;
        _current.TargetFolder = folder;
        TryStore(s => s.Update(set =>
        {
            if (set.Lists.FirstOrDefault(l => l.Name == name) is { } list)
            {
                list.TargetFolder = folder;
            }

            return set;
        }));
        FolderText.Text = folder.Length == 0 ? "." : folder;
        Recount();
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        var row = new ReplaceRuleRow();
        AddRow(row);
        RulesGrid.SelectedItem = row;
        RulesGrid.ScrollIntoView(row);
        Changed();
    }

    private void RemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not ReplaceRuleRow row)
        {
            return;
        }

        row.Edited -= Row_Edited;
        _rows.Remove(row);
        Changed();
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => Move(-1);

    private void MoveDown_Click(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int delta)
    {
        if (RulesGrid.SelectedItem is not ReplaceRuleRow row)
        {
            return;
        }

        var index = _rows.IndexOf(row);
        var target = index + delta;
        if (target < 0 || target >= _rows.Count)
        {
            return;
        }

        _rows.Move(index, target);
        RulesGrid.SelectedItem = row;
        Changed();
    }

    /// <summary>Strg+V mit Tabulator oder Zeilenumbruch in der Zwischenablage: je Zeile eine Regel (Spalte 1 Suchen, Spalte 2 Ersetzen durch).</summary>
    private void RulesGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control || _current is null || !Clipboard.ContainsText())
        {
            return;
        }

        var text = Clipboard.GetText();
        if (!text.Contains('\t') && !text.Contains('\n'))
        {
            return;
        }

        foreach (var line in text.Replace("\r\n", "\n").Split('\n').Where(l => l.Trim().Length > 0))
        {
            var columns = line.Split('\t');
            // Ein Suchtext, der kein Wort ist (Klammern, mehrere Wörter), bekommt gleich die passende Art: Platzhalter bei *, sonst Text.
            var search = columns[0].Trim();
            var mode = search.Length == 0 || RuleMatcher.IsValidSearchWord(search) ? RuleMode.Word : search.Contains('*') ? RuleMode.Pattern : RuleMode.Text;
            AddRow(new ReplaceRuleRow { Mode = mode, Search = search, Replace = columns.Length > 1 ? columns[1].Trim() : string.Empty });
        }

        e.Handled = true;
        Changed();
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (_root is not { } root || _current is null || TargetPath() is not { } folder || !Directory.Exists(folder))
        {
            return;
        }

        FlushPending();
        _count?.Cancel();
        var listName = _current.Name;
        var rules = _rows.Select(r => r.ToRule()).ToList();
        RunButton.IsEnabled = false;
        StatusText.Text = ReplaceListTexts.Counting;
        ReplaceListRun run;
        try
        {
            run = await Task.Run(() => ReplaceListRunner.Run(rules, ReplaceListRunner.ReadFolder(folder)));
        }
        finally
        {
            RunButton.IsEnabled = true;
        }

        var skipped = run.Skipped.Count > 0 ? " " + ReplaceListTexts.Skipped(run.Skipped.Count) : string.Empty;
        if (run.Changes.Count == 0)
        {
            Recount();
            StatusText.Text = ReplaceListTexts.NothingToReplace + skipped;
            return;
        }

        var preview = new ReplaceListPreviewWindow(run, root, listName) { Owner = Owner };
        preview.ShowDialog();
        if (!preview.Applied)
        {
            Recount();
            return;
        }

        var outcome = _apply(run.Changes, root, listName);
        Recount();
        StatusText.Text = outcome.Message + skipped;

        // Eine Regel kann einen OR-/NOR-/AND-Block auf einen Eintrag oder keinen geleert haben; dann den Stellen nachgehen lassen.
        if (outcome.Success)
        {
            ReplaceWarningHub.Report(
                run.Changes
                    .SelectMany(change => ParadoxModTool.SearchReplace.SingleEntryBlockFinder.NewIn(change.OldText, change.NewText).Select(block => new BlockWarningHit(Path.GetRelativePath(root, change.Path), block, change.Path)))
                    .ToList());
        }
    }
}
