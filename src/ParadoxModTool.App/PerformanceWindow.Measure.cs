using System.Globalization;
using System.IO;
using System.Windows;
using ParadoxModTool.Performance;
using ParadoxModTool.SaveGames;
using Binding = System.Windows.Data.Binding;
using Clipboard = System.Windows.Clipboard;
using Controls = System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ParadoxModTool.App;

/// <summary>Was aus der Laufliste von Schritt 1 gewählt wurde; die Reiter Auswertung und Vergleich hängen sich an <see cref="PerformanceWindow.RunChosen"/>.</summary>
internal enum PerformanceRunChoice
{
    Analyze,
    CompareA,
    CompareB,
}

/// <summary>
/// Schritt 1 "Messung" (Spec, Ablauf im Fenster): links Spiel und Mod, Playset, Spielstand, Laufname, Monate, die Zeile aus continue_game.json, die
/// Checkliste und die Knöpfe; rechts die Statuszeile des <see cref="PerformanceService"/> und die bisherigen Läufe der Mod mit Kontextmenü.
/// </summary>
internal sealed partial class PerformanceWindow
{
    private const int DefaultMonths = 12;
    private const int MaxMonths = 120;

    private readonly Controls.TextBlock _gameModLine = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
    private readonly Controls.TextBlock _noModLine = new() { Text = PerformanceTexts.NoActiveMod, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly Controls.ComboBox _playsetBox = new() { Margin = new Thickness(0, 2, 0, 8) };
    private readonly Controls.ComboBox _saveBox = new() { Margin = new Thickness(0, 2, 0, 8) };
    private readonly Controls.TextBox _runNameBox = new() { Margin = new Thickness(0, 2, 0, 8) };
    private readonly Controls.TextBox _monthsBox = new() { Width = 70, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), Text = DefaultMonths.ToString(CultureInfo.CurrentCulture) };
    private readonly Controls.TextBlock _monthsHint = new() { Text = PerformanceTexts.MonthsInvalid, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _continueLine = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8) };
    private readonly Controls.TextBlock _blockerLine = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
    private readonly Controls.Grid _checklist = new() { Margin = new Thickness(0, 4, 0, 10) };
    private readonly Controls.Button _startButton = new() { Content = PerformanceTexts.StartButton, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 8, 6) };
    private readonly Controls.Button _cancelWaitButton = new() { Content = PerformanceTexts.CancelWaitButton, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 8, 6) };
    private readonly Controls.Button _importButton = new() { Content = PerformanceTexts.ImportManuallyButton, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 8, 6) };
    private readonly Controls.TextBlock _measureStatus = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
    private readonly Controls.ListView _runList = new() { SelectionMode = Controls.SelectionMode.Single };
    private string _lastDefaultRunName = string.Empty;
    private int _lastRunCount = -1;
    private IReadOnlyList<PerformanceRun>? _lastRuns;

    /// <summary>Ein Lauf wurde in der Laufliste zum Auswerten oder als A bzw. B gewählt (die Sitzung hat ihn schon übernommen).</summary>
    internal event Action<string, PerformanceRunChoice>? RunChosen;

    /// <summary>Eine Zeile der Laufliste.</summary>
    private sealed record RunRow(string Id, string Name, string Date, string Save, string Months);

    /// <summary>Playset-Eintrag: Id <c>null</c> = Start ohne Playset-Wechsel; <paramref name="Name"/> wird im Lauf vermerkt.</summary>
    private sealed record PlaysetChoice(string? Id, string? Name);

    private UIElement BuildMeasureTab()
    {
        _noModLine.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Warning");
        _blockerLine.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Warning");
        _monthsHint.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Danger");
        _continueLine.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _checklist.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = GridLength.Auto });
        _checklist.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _checklist.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = GridLength.Auto });

        var left = new Controls.StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        left.Children.Add(_gameModLine);
        left.Children.Add(_noModLine);
        left.Children.Add(new Controls.TextBlock { Text = PerformanceTexts.PlaysetLabel });
        left.Children.Add(_playsetBox);
        left.Children.Add(new Controls.TextBlock { Text = PerformanceTexts.SaveLabel });
        left.Children.Add(_saveBox);
        left.Children.Add(new Controls.TextBlock { Text = PerformanceTexts.RunNameLabel });
        left.Children.Add(_runNameBox);
        left.Children.Add(new Controls.TextBlock { Text = PerformanceTexts.MonthsLabel });
        left.Children.Add(_monthsBox);
        left.Children.Add(_monthsHint);
        left.Children.Add(_continueLine);
        left.Children.Add(new Controls.TextBlock { Text = PerformanceTexts.ChecklistTitle, FontWeight = FontWeights.SemiBold });
        left.Children.Add(_blockerLine);
        left.Children.Add(_checklist);
        var buttons = new Controls.WrapPanel();
        buttons.Children.Add(_startButton);
        buttons.Children.Add(_cancelWaitButton);
        buttons.Children.Add(_importButton);
        left.Children.Add(buttons);

        var grid = new Controls.GridView();
        grid.Columns.Add(RunColumn(PerformanceTexts.ColumnName, nameof(RunRow.Name), 200));
        grid.Columns.Add(RunColumn(PerformanceTexts.ColumnDate, nameof(RunRow.Date), 130));
        grid.Columns.Add(RunColumn(PerformanceTexts.ColumnSave, nameof(RunRow.Save), 160));
        grid.Columns.Add(RunColumn(PerformanceTexts.ColumnMonths, nameof(RunRow.Months), 70));
        _runList.View = grid;
        _runList.ContextMenu = BuildRunMenu();
        _runList.ContextMenuOpening += (_, e) => e.Handled = _runList.SelectedItem is not RunRow;

        var right = new Controls.DockPanel();
        var runsTitle = new Controls.TextBlock { Text = PerformanceTexts.RunsTitle, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        Controls.DockPanel.SetDock(_measureStatus, Controls.Dock.Top);
        Controls.DockPanel.SetDock(runsTitle, Controls.Dock.Top);
        right.Children.Add(_measureStatus);
        right.Children.Add(runsTitle);
        right.Children.Add(_runList);

        var root = new Controls.Grid { Margin = new Thickness(6) };
        root.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 300 });
        root.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        var leftScroll = new Controls.ScrollViewer { Content = left, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto };
        Controls.Grid.SetColumn(right, 1);
        root.Children.Add(leftScroll);
        root.Children.Add(right);

        _playsetBox.DropDownOpened += (_, _) => FillPlaysets();
        _saveBox.DropDownOpened += (_, _) => FillSaves(preferred: (_saveBox.SelectedItem as Controls.ComboBoxItem)?.Tag as string);
        _saveBox.SelectionChanged += (_, _) => UpdateChecklist();
        _monthsBox.TextChanged += (_, _) => UpdateChecklist();
        _startButton.Click += (_, _) => StartMeasurement();
        _cancelWaitButton.Click += (_, _) => _service.StopWatching();
        _importButton.Click += (_, _) => ImportDumpManually();
        _service.StatusChanged += (_, _) => UpdateMeasureStatus();
        _service.SessionChanged += (_, _) => UpdateRuns();
        _service.RunImported += (_, runId) => ShowImportedRun(runId);

        FillPlaysets();
        FillSaves(preferred: _service.ContinueTitle());
        UpdateRuns();
        UpdateMeasureStatus();
        return root;
    }

    /// <summary>Beim Aktivieren des Fensters: aktive Mod, Spielzustand und continue_game.json können sich geändert haben.</summary>
    private void RefreshMeasureTab()
    {
        _service.RefreshMod();
        UpdateChecklist();
    }

    /// <summary>
    /// "Neue Messung mit B als Basis" aus dem Vergleich: Spielstand (fehlt er inzwischen, "unbekannt") und Monate vorbelegen, dann zu Schritt 1
    /// wechseln. <paramref name="months"/> <c>null</c> setzt den Standard, sonst gilt der erlaubte Bereich 1 bis 120.
    /// </summary>
    internal void PrefillMeasurement(string? saveName, int? months)
    {
        FillSaves(preferred: saveName);
        _monthsBox.Text = (months is { } m ? Math.Clamp(m, 1, MaxMonths) : DefaultMonths).ToString(CultureInfo.CurrentCulture);
        UpdateChecklist();
        _tabs.SelectedItem = _measureTab;
    }

    private static Controls.GridViewColumn RunColumn(string header, string property, double width) =>
        new() { Header = header, Width = width, DisplayMemberBinding = new Binding(property) };

    private Controls.ContextMenu BuildRunMenu()
    {
        var menu = new Controls.ContextMenu();
        menu.Items.Add(MenuItem(PerformanceTexts.MenuRename, RenameRun));
        menu.Items.Add(MenuItem(PerformanceTexts.MenuDelete, DeleteRun));
        menu.Items.Add(new Controls.Separator());
        menu.Items.Add(MenuItem(PerformanceTexts.MenuAnalyze, run => ChooseRun(run, PerformanceRunChoice.Analyze)));
        menu.Items.Add(MenuItem(PerformanceTexts.MenuCompareA, run => ChooseRun(run, PerformanceRunChoice.CompareA)));
        menu.Items.Add(MenuItem(PerformanceTexts.MenuCompareB, run => ChooseRun(run, PerformanceRunChoice.CompareB)));
        return menu;
    }

    private Controls.MenuItem MenuItem(string header, Action<RunRow> action)
    {
        var item = new Controls.MenuItem { Header = header };
        item.Click += (_, _) =>
        {
            if (_runList.SelectedItem is RunRow run)
            {
                action(run);
            }
        };
        return item;
    }

    private void FillPlaysets()
    {
        var previous = (_playsetBox.SelectedItem as Controls.ComboBoxItem)?.Tag as PlaysetChoice;
        var playsets = _host.Playsets().Where(p => !string.IsNullOrEmpty(p.Id)).ToList();
        var active = playsets.FirstOrDefault(p => p.IsActive)?.Name;
        _playsetBox.Items.Clear();
        _playsetBox.Items.Add(new Controls.ComboBoxItem { Content = PerformanceTexts.PlaysetUnchanged(active), Tag = new PlaysetChoice(null, active) });
        foreach (var playset in playsets)
        {
            _playsetBox.Items.Add(new Controls.ComboBoxItem { Content = playset.DisplayText, Tag = new PlaysetChoice(playset.Id, playset.Name) });
        }

        _playsetBox.SelectedIndex = previous?.Id is { } id
            ? Math.Max(0, _playsetBox.Items.Cast<Controls.ComboBoxItem>().ToList().FindIndex(i => (i.Tag as PlaysetChoice)?.Id == id))
            : 0;
    }

    /// <summary>Spielstände neueste zuerst, davor "unbekannt"; gewählt wird <paramref name="preferred"/>, wenn es ihn gibt, sonst "unbekannt".</summary>
    private void FillSaves(string? preferred)
    {
        var names = new List<string>();
        var labels = new List<string>();
        if (_host.Profile.SaveGames is { } saveGames && _host.SaveFolder() is { } folder)
        {
            try
            {
                foreach (var save in SaveFiles.List(folder, saveGames))
                {
                    var file = Path.GetFileNameWithoutExtension(save.Path);
                    names.Add(file);
                    // Im Spiel heißt der Stand nach seinem Playthrough-Namen und Datum (Florian 2026-10-24); die Datei bleibt der Wert, weil continue_game.json sie nennt.
                    labels.Add(string.IsNullOrEmpty(save.PlaythroughName)
                        ? file
                        : PerformanceTexts.SaveDisplay(save.PlaythroughName, save.GameDate ?? string.Empty, file));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ordner nicht lesbar: nur "unbekannt".
            }
        }

        _saveBox.Items.Clear();
        _saveBox.Items.Add(new Controls.ComboBoxItem { Content = PerformanceTexts.SaveUnknown, Tag = null });
        for (var i = 0; i < names.Count; i++)
        {
            _saveBox.Items.Add(new Controls.ComboBoxItem { Content = labels[i], Tag = names[i], ToolTip = names[i] });
        }

        var index = preferred is null ? -1 : names.FindIndex(n => string.Equals(n, preferred, StringComparison.OrdinalIgnoreCase));
        _saveBox.SelectedIndex = index + 1;
    }

    private string? SelectedSaveName => (_saveBox.SelectedItem as Controls.ComboBoxItem)?.Tag as string;

    private PlaysetChoice SelectedPlayset => (_playsetBox.SelectedItem as Controls.ComboBoxItem)?.Tag as PlaysetChoice ?? new PlaysetChoice(null, null);

    /// <summary>Monate aus dem Feld: ganze Zahl von 1 bis 120, sonst <c>null</c>.</summary>
    private int? MonthsInput =>
        int.TryParse(_monthsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var months) && months is >= 1 and <= MaxMonths ? months : null;

    private string RunNameInput => string.IsNullOrWhiteSpace(_runNameBox.Text) ? DefaultRunName() : _runNameBox.Text.Trim();

    private string DefaultRunName() => PerformanceTexts.DefaultRunName((_service.Session?.Runs.Count ?? 0) + 1);

    /// <summary>Zeile aus continue_game.json, Hinweis bei laufendem Spiel ohne Debug-Modus und Checkliste zum gewählten Spielstand und den Monaten.</summary>
    private void UpdateChecklist()
    {
        var title = _service.ContinueTitle();
        _continueLine.Text = title is null ? PerformanceTexts.ContinueTitleNone : PerformanceTexts.ContinueTitleLine(title);
        var months = MonthsInput;
        _monthsHint.Visibility = months is null ? Visibility.Visible : Visibility.Collapsed;
        var plan = _service.Plan(SelectedSaveName, months ?? DefaultMonths);
        _blockerLine.Text = plan.Blocker ?? string.Empty;
        _blockerLine.Visibility = plan.Blocker is null ? Visibility.Collapsed : Visibility.Visible;

        _checklist.Children.Clear();
        _checklist.RowDefinitions.Clear();
        foreach (var step in plan.Steps)
        {
            var row = _checklist.RowDefinitions.Count;
            _checklist.RowDefinitions.Add(new Controls.RowDefinition { Height = GridLength.Auto });
            var marker = new Controls.TextBlock
            {
                Text = step.ByPmt ? PerformanceTexts.StepByPmt : PerformanceTexts.StepByYou,
                FontWeight = step.ByPmt ? FontWeights.SemiBold : FontWeights.Normal,
                Margin = new Thickness(0, 2, 10, 2),
            };
            marker.SetResourceReference(Controls.TextBlock.ForegroundProperty, step.ByPmt ? "Theme.Accent" : "Theme.TextMuted");
            var text = new Controls.TextBlock { Text = step.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
            Controls.Grid.SetRow(marker, row);
            Controls.Grid.SetRow(text, row);
            Controls.Grid.SetColumn(text, 1);
            _checklist.Children.Add(marker);
            _checklist.Children.Add(text);
            if (step.Command is { Length: > 0 } command)
            {
                var copy = new Controls.Button
                {
                    Content = PerformanceTexts.CopyCommand,
                    ToolTip = PerformanceTexts.CopyCommandTip,
                    Margin = new Thickness(8, 0, 0, 2),
                    Padding = new Thickness(6, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                };
                copy.Click += (_, _) => CopyCommandToClipboard(command);
                Controls.Grid.SetRow(copy, row);
                Controls.Grid.SetColumn(copy, 2);
                _checklist.Children.Add(copy);
            }
        }

        UpdateMeasureButtons();
    }

    /// <summary>Konsolenbefehl eines Checklisten-Schritts in die Zwischenablage (Florian 2026-10-24: im Spiel mit Strg+V einfügen).</summary>
    private void CopyCommandToClipboard(string command)
    {
        try
        {
            Clipboard.SetText(command);
            _measureStatus.Text = PerformanceTexts.CommandCopied(command);
            _measureStatus.Visibility = Visibility.Visible;
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        {
            _measureStatus.Text = PerformanceTexts.StatusActionFailed(ex.Message);
            _measureStatus.Visibility = Visibility.Visible;
        }
    }

    private void UpdateMeasureButtons()
    {
        var ready = _service.Session is not null && MonthsInput is not null;
        _startButton.IsEnabled = ready && !_service.Watching;
        _cancelWaitButton.IsEnabled = _service.Watching;
        _importButton.IsEnabled = ready;
    }

    private void UpdateMeasureStatus()
    {
        _measureStatus.Text = _service.Status;
        _measureStatus.Visibility = string.IsNullOrEmpty(_service.Status) ? Visibility.Collapsed : Visibility.Visible;
        UpdateMeasureButtons();
    }

    /// <summary>
    /// Kopfzeile und Laufliste aus der Sitzung; der Laufname springt nach einem neuen Lauf auf den nächsten Standardnamen. Läuft auch innerhalb
    /// eines Imports (über <see cref="PerformanceSession.Changed"/>): nichts darf hier herausfliegen, sonst würde der Dienst den Import wiederholen.
    /// </summary>
    private void UpdateRuns()
    {
        try
        {
            UpdateRunsCore();
        }
        catch (Exception ex)
        {
            _status.Text = PerformanceTexts.StatusActionFailed(ex.Message);
        }
    }

    private void UpdateRunsCore()
    {
        var session = _service.Session;
        var modName = _service.ModRoot is { } root
            ? _host.KnownMods().FirstOrDefault(m => string.Equals(m.Path, root, StringComparison.OrdinalIgnoreCase)).Name
                ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(root))
            : PerformanceTexts.SaveUnknown;
        _gameModLine.Text = PerformanceTexts.GameAndMod(_host.Profile.DisplayName, modName);
        _noModLine.Visibility = session is null ? Visibility.Visible : Visibility.Collapsed;

        // Select, Compare und Noise lassen die Liste der Sitzung unverändert (gleiche Referenz): dann bleibt die Liste samt Auswahl stehen.
        var runs = session?.Runs ?? Array.Empty<PerformanceRun>();
        if (ReferenceEquals(runs, _lastRuns))
        {
            return;
        }

        _lastRuns = runs;
        _runList.ItemsSource = runs.Select(r => new RunRow(
            r.Id,
            r.Name,
            r.CreatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
            string.IsNullOrEmpty(r.SaveName) ? PerformanceTexts.SaveUnknown : r.SaveName,
            (r.CountedMonths ?? r.PlannedMonths)?.ToString(CultureInfo.CurrentCulture) ?? Missing)).ToList();

        var currentName = _runNameBox.Text.Trim();
        if (runs.Count > _lastRunCount || currentName.Length == 0 || currentName == _lastDefaultRunName)
        {
            _lastDefaultRunName = DefaultRunName();
            _runNameBox.Text = _lastDefaultRunName;
        }

        _lastRunCount = runs.Count;
        UpdateChecklist();
    }

    private void StartMeasurement()
    {
        if (MonthsInput is not { } months)
        {
            return;
        }

        var playset = SelectedPlayset;
        _service.StartMeasurement(playset.Id, SelectedSaveName, months, RunNameInput, playset.Name);
        UpdateChecklist();
    }

    private void ImportDumpManually()
    {
        if (MonthsInput is not { } months)
        {
            return;
        }

        var dialog = new OpenFileDialog { Filter = PerformanceTexts.DumpFilter, Title = PerformanceTexts.ImportManuallyButton };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("performance.dump");
        if (dialog.ShowDialog(this) == true)
        {
            _service.ImportManually(dialog.FileName, RunNameInput, SelectedSaveName, months);
        }
    }

    private void RenameRun(RunRow run)
    {
        if (_service.Session is not { } session
            || TextPromptWindow.Ask(this, PerformanceTexts.RenameTitle, PerformanceTexts.RenamePrompt, run.Name) is not { } name
            || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        RunAction(() => session.Rename(run.Id, name.Trim()));
    }

    private void DeleteRun(RunRow run)
    {
        if (_service.Session is not { } session
            || MessageBox.Show(this, PerformanceTexts.ConfirmDelete(run.Name), PerformanceTexts.WindowTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        RunAction(() => session.Delete(run.Id));
    }

    /// <summary>"Auswerten", "Vergleich A" oder "Vergleich B": Sitzung setzen, <see cref="RunChosen"/> melden und zum passenden Reiter wechseln.</summary>
    private void ChooseRun(RunRow run, PerformanceRunChoice choice)
    {
        if (_service.Session is not { } session)
        {
            return;
        }

        WithWaitCursor(() =>
        {
            switch (choice)
            {
                case PerformanceRunChoice.Analyze:
                    session.Select(run.Id);
                    break;
                case PerformanceRunChoice.CompareA:
                    session.Compare(run.Id, session.B?.Id);
                    break;
                default:
                    session.Compare(session.A?.Id, run.Id);
                    break;
            }
        });

        RunChosen?.Invoke(run.Id, choice);
        _tabs.SelectedItem = choice == PerformanceRunChoice.Analyze ? _analyzeTab : _compareTab;
    }

    /// <summary>Nach einem Import (Dateiwächter oder manuell): den neuen Lauf auswerten und zum Reiter Auswertung wechseln; die Statuszeile bleibt.</summary>
    private void ShowImportedRun(string runId)
    {
        if (_service.Session is not { } session || !session.Runs.Any(r => r.Id == runId))
        {
            return;
        }

        try
        {
            WithWaitCursor(() => session.Select(runId));
            RunChosen?.Invoke(runId, PerformanceRunChoice.Analyze);
            _tabs.SelectedItem = _analyzeTab;
        }
        catch (Exception ex)
        {
            _status.Text = PerformanceTexts.StatusActionFailed(ex.Message);
        }
    }

    private void RunAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or InvalidDataException)
        {
            _measureStatus.Text = PerformanceTexts.StatusActionFailed(ex.Message);
            _measureStatus.Visibility = Visibility.Visible;
        }
    }
}
