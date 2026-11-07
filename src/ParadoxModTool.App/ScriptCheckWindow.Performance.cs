using System.IO;
using System.Windows;
using System.Windows.Threading;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;
using CheckFinding = ParadoxModTool.ScriptCheck.Finding;
using CheckRow = ParadoxModTool.ScriptCheck.FindingRow;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Reiter "Leistung anderer Ziele": Ziel (Vanilla, Playset-Mod, installierte Mod) wählen und nur mit den Leistungsregeln prüfen. Eigene Sitzung (nicht
/// aus <see cref="SharedSessions"/>) mit eigenem Abbruch; die Liste der eigenen Mod bleibt unberührt. Gebaut wird nur auf Knopfdruck; ein Zielwechsel
/// bricht einen laufenden Aufbau ab. Ausblenden gilt je Ziel (<c>script-check/hidden-performance</c>). In Vanilla oder eine fremde Mod schreibt PMT nie.
/// </summary>
internal sealed partial class ScriptCheckWindow
{
    private readonly ScriptCheckSession _perfSession = new();
    private readonly Controls.TabItem _perfTab = new() { Header = ScriptCheckTexts.TabPerformance };
    private readonly Controls.ComboBox _targetBox = new() { MinWidth = 320, Margin = new Thickness(6, 0, 0, 0), DisplayMemberPath = nameof(TargetChoice.Text) };
    private readonly Controls.Button _perfButton = new() { Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(8, 0, 0, 0) };
    private readonly Controls.ProgressBar _perfProgress = new() { Width = 120, Height = 6, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBlock _perfPhase = new() { Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, MinWidth = 160 };
    private readonly Controls.TextBlock _perfStatus = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.ListView _perfList = new() { SelectionMode = Controls.SelectionMode.Extended };
    private readonly Controls.TextBox _perfSearch = new() { MinWidth = 260, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = ScriptCheckTexts.SearchFindingsTip };
    private readonly Controls.CheckBox _perfShowHidden = new() { Content = ScriptCheckTexts.ShowHidden, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
    private readonly Controls.Button _perfCopyReport = new() { Content = ScriptCheckTexts.CopyReport, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(12, 0, 0, 0), IsEnabled = false };
    private readonly Controls.MenuItem _perfHideItem = new() { Header = ScriptCheckTexts.Hide };
    private readonly Controls.MenuItem _perfUnhideItem = new() { Header = ScriptCheckTexts.Unhide };
    private DispatcherTimer? _perfTimer;

    /// <summary>Abbruch des laufenden Aufbaus dieses Reiters; null, wenn keiner läuft. Getrennt von <see cref="_cts"/>.</summary>
    private CancellationTokenSource? _perfCts;

    /// <summary>Zählt Aufbauten und Zielwechsel mit Abbruch: Fortschritt eines verdrängten oder abgewählten Aufbaus wird nicht mehr gezeigt.</summary>
    private int _perfRun;

    /// <summary>
    /// Zählt nur gestartete Aufbauten: Der jüngste ordnet sein Ergebnis (auch nach einem Abbruch, der zu spät kam) seinem Ziel zu; ein älterer nie, denn
    /// die Sitzung hält nur das Ergebnis des jüngsten.
    /// </summary>
    private int _perfStarts;

    /// <summary>Ziel des laufenden Aufbaus; null, wenn keiner läuft.</summary>
    private PerformanceTarget? _perfBuildingTarget;

    /// <summary>Playset des laufenden Aufbaus (zählt nur bei einer Playset-Mod); gesetzt und geleert mit <see cref="_perfBuildingTarget"/>.</summary>
    private string? _perfBuildingPlayset;

    /// <summary>Die Zielliste muss beim nächsten Zeigen des Reiters neu gelesen werden (erstes Zeigen, Playset-Wechsel, erneutes Öffnen).</summary>
    private bool _perfTargetsStale = true;

    /// <summary>Setzt die Zielliste gerade der Code: kein Zielwechsel aus SelectionChanged.</summary>
    private bool _fillingTargets;

    /// <summary>Gewähltes Ziel; null, wenn es keins gibt.</summary>
    private PerformanceTarget? _perfTarget;

    /// <summary>Zu diesem Ziel (und Playset) gehört das Ergebnis der Sitzung; null ohne Ergebnis.</summary>
    private PerformanceTarget? _perfResultTarget;

    private string? _perfResultPlayset;

    /// <summary>Stand-Text der Sitzung, als das gezeigte Ergebnis entstand (Zeit, Dateien, Funde).</summary>
    private string? _perfResultStatus;

    /// <summary>Ziel und Playset des letzten gestarteten Aufbaus.</summary>
    private PerformanceTarget? _perfLastRunTarget;

    private string? _perfLastRunPlayset;

    /// <summary>Text des letzten Aufbaus, wenn er ohne neues Ergebnis endete (abgebrochen, fehlgeschlagen); null, solange er läuft oder wenn er ein Ergebnis brachte.</summary>
    private string? _perfLastRunText;

    private ScriptCheckHideList? _perfHide;

    /// <summary>Für dieses Ziel (Ordner) ist <see cref="_perfHide"/> geladen.</summary>
    private string? _perfHideTarget;

    private string? _perfHideSaveError;

    private static bool HasPerformanceRules(ScriptCheckRules rules) => rules.Performance is { GlobalIterators.Count: > 0, PulseOnActions.Count: > 0 };

    private Controls.DockPanel BuildPerformanceTab()
    {
        _perfStatus.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _perfPhase.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        Controls.ToolTipService.SetShowOnDisabled(_perfButton, true);

        ConfigureFindingsList(_perfList, ScriptCheckTexts.ColPath);
        _perfList.ContextMenu = BuildPerformanceMenu();
        _targetBox.SelectionChanged += (_, _) => TargetBoxChanged();
        _perfButton.Click += (_, _) => CheckOrCancelPerformance();
        _perfCopyReport.Click += (_, _) => CopyReport(_perfList, _perfTarget?.Name ?? string.Empty);
        _perfSession.Changed += PerfSession_Changed;

        _perfTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Input, (_, _) => ShowPerformance(), Dispatcher);
        _perfTimer.Stop();
        _perfSearch.TextChanged += (_, _) =>
        {
            _perfTimer.Stop();
            _perfTimer.Start();
        };
        _perfSearch.KeyDown += OnFindingsSearchKeyDown;
        _perfShowHidden.Click += (_, _) => ShowPerformance();

        var targetRow = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        targetRow.Children.Add(new Controls.TextBlock { Text = ScriptCheckTexts.TargetLabel, VerticalAlignment = VerticalAlignment.Center });
        targetRow.Children.Add(_targetBox);
        targetRow.Children.Add(_perfButton);
        targetRow.Children.Add(_perfProgress);
        targetRow.Children.Add(_perfPhase);

        var filterRow = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 6) };
        filterRow.Children.Add(_perfSearch);
        filterRow.Children.Add(_perfShowHidden);
        filterRow.Children.Add(_perfCopyReport);

        var panel = new Controls.DockPanel();
        Controls.DockPanel.SetDock(targetRow, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_perfStatus, Controls.Dock.Top);
        Controls.DockPanel.SetDock(filterRow, Controls.Dock.Top);
        panel.Children.Add(targetRow);
        panel.Children.Add(_perfStatus);
        panel.Children.Add(filterRow);
        panel.Children.Add(_perfList);

        // Die Zielliste liest installierte Mods (Metadaten): erst, wenn der Reiter gezeigt wird.
        UpdatePerfHeader();
        return panel;
    }

    /// <summary>Playset-Wechsel oder erneutes Öffnen: die Zielliste neu lesen, wenn der Reiter gerade gezeigt wird (sonst beim nächsten Zeigen).</summary>
    private void RefreshTargetsIfShown()
    {
        if (PerfTabSelected)
        {
            FillTargets();
        }
        else
        {
            _perfTargetsStale = true;
        }
    }

    private Controls.ContextMenu BuildPerformanceMenu()
    {
        var open = new Controls.MenuItem { Header = ScriptCheckTexts.OpenPlace };
        open.Click += (_, _) => OpenSelectedFinding(_perfList);
        var copy = new Controls.MenuItem { Header = ScriptCheckTexts.CopyName };
        copy.Click += (_, _) =>
        {
            if (_perfList.SelectedItem is CheckRow row)
            {
                CopyToClipboard(row.Name);
            }
        };
        _perfHideItem.Click += (_, _) => SetSelectedPerfHidden(true);
        _perfUnhideItem.Click += (_, _) => SetSelectedPerfHidden(false);

        var menu = new Controls.ContextMenu();
        menu.Items.Add(open);
        menu.Items.Add(copy);
        menu.Items.Add(new Controls.Separator());
        menu.Items.Add(_perfHideItem);
        menu.Items.Add(_perfUnhideItem);
        menu.Opened += (_, _) =>
        {
            var selected = _perfList.SelectedItems.OfType<CheckRow>().ToList();
            _perfHideItem.IsEnabled = _perfHide is not null && selected.Any(r => !r.IsHidden);
            _perfUnhideItem.IsEnabled = _perfHide is not null && selected.Any(r => r.IsHidden);
        };
        return menu;
    }

    /// <summary>
    /// Reiterwechsel: Kopfzeile grau oder wieder bedienbar; beim Wechsel auf diesen Reiter die Zielliste nur lesen, wenn sie veraltet ist (sonst bleibt
    /// sie). Auswahländerungen aus Listen im Reiter steigen hier auch auf.
    /// </summary>
    private void Tabs_SelectionChanged(object sender, Controls.SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, _tabs))
        {
            return;
        }

        ApplyHeaderEnabled();
        if (PerfTabSelected && _perfTargetsStale)
        {
            FillTargets();
        }
    }

    /// <summary>
    /// Füllt die Zielliste aus Spielordner, Playset (nur aktivierte Einträge) und installierten Mods (im UI-Thread). Die Wahl bleibt am Ordner; fällt
    /// das gewählte Ziel weg, gilt das erste, und ein laufender Aufbau endet.
    /// </summary>
    private void FillTargets()
    {
        _perfTargetsStale = false;
        var playsetName = PerfPlaysetName();
        var installed = ReadInstalledMods();
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in installed)
        {
            if (TryKey(mod.Directory) is { } key)
            {
                names.TryAdd(key, mod.Name);
            }
        }

        var playsetMods = (ReadPlaysetEntries(playsetName) ?? new List<PlaysetEntry>())
            .Where(e => e.Enabled && !string.IsNullOrWhiteSpace(e.Directory))
            .Select(e => (TryKey(e.Directory) is { } key && names.TryGetValue(key, out var name) ? name : Path.GetFileName(e.Directory.TrimEnd('\\', '/')), e.Directory))
            .ToList();
        var targets = PerformanceTarget.Choices(_host.ContentRoot(), playsetMods, installed.Select(m => (m.Name, m.Directory)).ToList());
        var choices = targets.Select(t => new TargetChoice(ScriptCheckTexts.PerfTargetKind(t.Kind) + ": " + t.Name, t)).ToList();

        _fillingTargets = true;
        _targetBox.ItemsSource = choices;
        _targetBox.SelectedItem = choices.FirstOrDefault(c => SameTarget(c.Target, _perfTarget)) ?? choices.FirstOrDefault();
        _fillingTargets = false;
        _targetBox.IsEnabled = choices.Count > 0;
        _targetBox.ToolTip = choices.Count > 0 ? null : ScriptCheckTexts.NoTargets;
        UseTarget((_targetBox.SelectedItem as TargetChoice)?.Target);
    }

    private string? PerfPlaysetName() => _host.SelectedPlaysetName() ?? _host.Mods.DefaultPlaysetName();

    private List<InstalledModFolder> ReadInstalledMods()
    {
        try
        {
            return _host.Mods.InstalledMods();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new List<InstalledModFolder>();
        }
    }

    private List<PlaysetEntry>? ReadPlaysetEntries(string? playsetName)
    {
        try
        {
            return _host.Mods.PlaysetEntries(playsetName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? TryKey(string directory)
    {
        try
        {
            return ErrorLogSources.Key(directory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>Gleiches Ziel: gleicher Ordner (über <see cref="ErrorLogSources.Key"/>) und gleiche Art.</summary>
    private static bool SameTarget(PerformanceTarget? a, PerformanceTarget? b) =>
        a is not null && b is not null && a.Kind == b.Kind
        && string.Equals(TryKey(a.Directory) ?? a.Directory, TryKey(b.Directory) ?? b.Directory, StringComparison.OrdinalIgnoreCase);

    private void TargetBoxChanged()
    {
        if (!_fillingTargets)
        {
            UseTarget((_targetBox.SelectedItem as TargetChoice)?.Target);
        }
    }

    /// <summary>Wechselt das gewählte Ziel: ein laufender Aufbau endet, die Ausblende-Liste des Ziels wird geladen. Ohne echten Wechsel nur neu zeigen.</summary>
    private void UseTarget(PerformanceTarget? target)
    {
        if (!SameTarget(target, _perfTarget))
        {
            if (_perfCts is not null)
            {
                _perfCts.Cancel();
                // Fortschritt des abgebrochenen Aufbaus nicht mehr zeigen.
                _perfRun++;
            }

            _perfTarget = target;
            ReloadPerfHideList();
        }
        else
        {
            // Gleicher Ordner, vielleicht neuer Name aus den Metadaten.
            _perfTarget = target;
        }

        UpdatePerfHeader();
        ShowPerformance();
    }

    private void ReloadPerfHideList()
    {
        var directory = _perfTarget?.Directory;
        if (string.Equals(directory, _perfHideTarget, StringComparison.OrdinalIgnoreCase) && _perfHide is not null)
        {
            return;
        }

        _perfHideTarget = directory;
        _perfHideSaveError = null;
        _perfHide = directory is null ? null : ScriptCheckHideList.Load(Path.Combine(_host.GameDirectory, "script-check", "hidden-performance"), directory);
        if (_perfHide is { ReadFailed: true })
        {
            _perfHideSaveError = ScriptCheckTexts.HideListUnreadable;
        }
    }

    /// <summary>Knopf und F5: bricht den Aufbau des gewählten Ziels ab oder liest die Zielliste neu und prüft; fiel das Ziel dabei weg, nur neu zeigen.</summary>
    private void CheckOrCancelPerformance()
    {
        if (BuildingSelected)
        {
            _perfCts?.Cancel();
            return;
        }

        var chosen = _perfTarget;
        FillTargets();
        if (SameTarget(chosen, _perfTarget))
        {
            StartPerformance();
        }
    }

    /// <summary>Startet die Prüfung des gewählten Ziels; ein laufender Aufbau dieses Reiters wird vorher abgebrochen.</summary>
    private async void StartPerformance()
    {
        if (_perfTarget is not { } target || !HasPerformanceRules(_host.Rules))
        {
            return;
        }

        var playsetName = PerfPlaysetName();
        Func<ScriptCheckRequest> createRequest;
        try
        {
            // Momentaufnahme im UI-Thread (eigene Mods, Playset-Datei): Ein Lesefehler landet in der Statuszeile des Reiters.
            createRequest = AnalysisRequests.Performance(_host, target, playsetName);
        }
        catch (Exception ex)
        {
            // Zählt als Start: Ein älterer Lauf endet wie bei einem neuen Start (abgebrochen, ohne Fortschritt) und überschreibt den Fehlertext nicht
            // mehr; sein finally gibt das Abbruch-Token frei.
            _perfCts?.Cancel();
            _perfRun++;
            _perfStarts++;
            _perfLastRunTarget = target;
            _perfLastRunPlayset = playsetName;
            _perfLastRunText = ScriptCheckTexts.StatusFailed(ex.Message);
            UpdatePerfHeader();
            return;
        }

        _perfCts?.Cancel();
        _perfCts?.Dispose();
        var cts = new CancellationTokenSource();
        _perfCts = cts;
        var run = ++_perfRun;
        var start = ++_perfStarts;
        var progress = new Progress<BuildProgress>(p =>
        {
            if (run == _perfRun)
            {
                ShowPerfProgress(p);
            }
        });

        var before = _perfSession.Index;
        _perfBuildingTarget = target;
        _perfBuildingPlayset = playsetName;
        _perfLastRunTarget = target;
        _perfLastRunPlayset = playsetName;
        _perfLastRunText = null;
        string? failure = null;
        try
        {
            await _perfSession.RefreshAsync(createRequest, progress, cts.Token);
        }
        catch (Exception ex)
        {
            // async void: nichts darf durchlaufen (z. B. aus Handlern von Changed); der Fehler landet in der Statuszeile des Reiters.
            failure = ScriptCheckTexts.StatusFailed(ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_perfCts, cts))
            {
                _perfCts = null;
                cts.Dispose();
            }
        }

        // Nur der jüngste Aufbau ordnet zu: ein neues Ergebnis samt Stand seinem Ziel, sonst (abgebrochen, fehlgeschlagen) seinen Text dem letzten Lauf.
        if (start == _perfStarts)
        {
            _perfBuildingTarget = null;
            _perfBuildingPlayset = null;
            _perfLastRunTarget = target;
            _perfLastRunPlayset = playsetName;
            if (failure is not null)
            {
                _perfLastRunText = failure;
            }
            else if (!ReferenceEquals(_perfSession.Index, before))
            {
                _perfResultTarget = target;
                _perfResultPlayset = playsetName;
                _perfResultStatus = _perfSession.StatusText;
                _perfLastRunText = null;
            }
            else
            {
                _perfLastRunText = _perfSession.StatusText;
            }
        }

        UpdatePerfHeader();
        ShowPerformance();
    }

    /// <summary>Gleiches Ziel und, bei einer Playset-Mod, gleiches Playset wie jetzt gewählt.</summary>
    private bool IsSelected(PerformanceTarget? target, string? playset) =>
        SameTarget(target, _perfTarget)
        && (_perfTarget!.Kind != PerformanceTargetKind.PlaysetMod || string.Equals(playset, PerfPlaysetName(), StringComparison.Ordinal));

    /// <summary>Läuft gerade ein eigener Aufbau für das gewählte Ziel (bei einer Playset-Mod auch für das jetzige Playset)?</summary>
    private bool BuildingSelected =>
        _perfSession.State == ScriptCheckState.Building && _perfCts is not null && IsSelected(_perfBuildingTarget, _perfBuildingPlayset);

    private void PerfSession_Changed(object? sender, EventArgs e)
    {
        UpdatePerfHeader();
        ShowPerformance();
    }

    /// <summary>Gehört das Ergebnis der Sitzung zum gewählten Ziel? Bei einer Playset-Mod auch zum selben Playset.</summary>
    private bool PerfResultIsCurrent =>
        _perfSession.Result is not null && _perfResultStatus is not null && IsSelected(_perfResultTarget, _perfResultPlayset);

    private void UpdatePerfHeader()
    {
        // Ein Aufbau für ein anderes (abgewähltes) Ziel zählt hier nicht: Der Knopf zeigt "Leistung prüfen", kein Fortschritt.
        var building = BuildingSelected;
        var hasRules = HasPerformanceRules(_host.Rules);
        _perfButton.Content = building ? ScriptCheckTexts.Cancel : ScriptCheckTexts.CheckPerformance;
        _perfButton.IsEnabled = building || (hasRules && _perfTarget is not null);
        _perfButton.ToolTip = !hasRules ? ScriptCheckTexts.NoPerformanceRules : _perfTarget is null ? ScriptCheckTexts.NoTargets : null;
        _perfProgress.Visibility = building ? Visibility.Visible : Visibility.Collapsed;
        _perfPhase.Visibility = building ? Visibility.Visible : Visibility.Collapsed;
        if (!building)
        {
            _perfPhase.Text = string.Empty;
            _perfProgress.IsIndeterminate = false;
            _perfProgress.Value = 0;
        }
        else if (string.IsNullOrEmpty(_perfPhase.Text))
        {
            _perfProgress.IsIndeterminate = true;
        }

        _perfStatus.Text = PerfStatusText();
    }

    /// <summary>
    /// Statuszeile des Reiters, mit derselben Prüfung wie die Liste (<see cref="PerfResultIsCurrent"/>): Name des Ziels, dahinter der Stand des gezeigten
    /// Ergebnisses. Endete der letzte Lauf für dieses Ziel ohne neues Ergebnis (abgebrochen, fehlgeschlagen), folgt sein Text mit "; " (ohne Ergebnis allein).
    /// Während eines Aufbaus nur der Stand des Ergebnisses (ohne Ergebnis nur der Name), ohne alles der Hinweis auf "Leistung prüfen". Ein Fehler der
    /// Ausblende-Liste hängt hinten an.
    /// </summary>
    private string PerfStatusText()
    {
        if (_perfTarget is not { } target)
        {
            return ScriptCheckTexts.NoTargets;
        }

        var result = PerfResultIsCurrent ? _perfResultStatus : null;
        var lastRun = !BuildingSelected && _perfLastRunText is not null && IsSelected(_perfLastRunTarget, _perfLastRunPlayset) ? _perfLastRunText : null;
        var text = BuildingSelected
            ? result ?? string.Empty
            : (result, lastRun) switch
            {
                ({ } r, { } l) => r + "; " + l,
                ({ } r, null) => r,
                (null, { } l) => l,
                _ => ScriptCheckTexts.PerfNoResult,
            };
        var line = string.IsNullOrEmpty(text) ? target.Name : target.Name + ": " + text;
        return _perfHideSaveError is null ? line : line + "; " + _perfHideSaveError;
    }

    private void ShowPerfProgress(BuildProgress progress)
    {
        if (_perfSession.State != ScriptCheckState.Building)
        {
            return;
        }

        _perfPhase.Text = ScriptCheckTexts.Phase(progress.Phase, progress.Done, progress.Total);
        _perfProgress.IsIndeterminate = progress.Total <= 0;
        _perfProgress.Maximum = Math.Max(1, progress.Total);
        _perfProgress.Value = Math.Min(progress.Done, _perfProgress.Maximum);
    }

    /// <summary>Füllt die Liste neu: nur das Ergebnis des gewählten Ziels, nach Suche und Ausblenden.</summary>
    private void ShowPerformance()
    {
        _perfTimer?.Stop();
        var findings = PerfResultIsCurrent ? _perfSession.Result!.Findings : Array.Empty<CheckFinding>();
        _perfList.ItemsSource = ScriptCheckView.Rows(findings, _perfHide, _perfShowHidden.IsChecked == true, null, _perfSearch.Text);
        _perfCopyReport.IsEnabled = ReportFindings(_perfList).Count > 0;
    }

    private void SetSelectedPerfHidden(bool hidden)
    {
        if (_perfHide is null)
        {
            return;
        }

        var selectedIndex = _perfList.SelectedIndex;
        _perfHideSaveError = ApplyHidden(_perfList, _perfHide, hidden);
        _perfStatus.Text = PerfStatusText();
        ShowPerformance();
        _perfList.SelectedIndex = Math.Min(selectedIndex, _perfList.Items.Count - 1);
    }

    /// <summary>Eintrag der Zielauswahl: "{Art}: {Name}".</summary>
    private sealed record TargetChoice(string Text, PerformanceTarget Target);
}
