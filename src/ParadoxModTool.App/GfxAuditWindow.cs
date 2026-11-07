using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.GfxAudit;
using ParadoxModTool.Settings;
using Controls = System.Windows.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ParadoxModTool.App;

/// <summary>
/// Fenster "GFX-Prüfung": Kopf mit Mod-Auswahl, Suchfeld, Knopf "Aktualisieren"/"Abbrechen" und Fortschritt, darunter Stand und was nicht geprüft
/// wird, dann die Befundliste (<c>GfxAuditWindow.Findings.cs</c>). Der Aufbau läuft im Hintergrund über <see cref="GfxAuditSession"/>. Die
/// Bedienung folgt der Skript-Prüfung.
/// </summary>
internal sealed partial class GfxAuditWindow : Window
{
    /// <summary>Höchstens so viele nicht lesbare Dateien im Tooltip der Statuszeile.</summary>
    private const int MaxUnreadInTip = 40;

    private readonly GfxAuditWindowHost _host;
    private readonly AppSettings _settings;

    /// <summary>Eigene Sitzung des Fensters: Das Ergebnis gehört zur Mod und zum Playset des letzten Aufbaus.</summary>
    private readonly GfxAuditSession _session = new();
    private readonly Controls.ComboBox _modBox = new() { MinWidth = 260, Margin = new Thickness(6, 0, 0, 0), DisplayMemberPath = nameof(ModChoice.Name) };
    private readonly Controls.TextBox _search = new() { MinWidth = 220, Margin = new Thickness(12, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = GfxAuditTexts.SearchTip };
    private readonly Controls.Button _refreshButton = new() { Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(8, 0, 0, 0) };
    private readonly Controls.Button _moveOutButton = new() { Content = GfxAuditTexts.MoveOut, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(6, 0, 0, 0) };
    private readonly Controls.Button _archiveButton = new() { Content = GfxAuditTexts.Archive, ToolTip = GfxAuditTexts.ArchiveTip, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(6, 0, 0, 0) };
    private readonly Controls.ProgressBar _progressBar = new() { Width = 120, Height = 6, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBlock _phaseText = new() { Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, MinWidth = 160 };
    private readonly Controls.TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.TextBlock _coverage = new() { Text = GfxAuditTexts.Coverage, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
    private DispatcherTimer? _searchTimer;

    /// <summary>Abbruch des laufenden Aufbaus; null, wenn keiner läuft.</summary>
    private CancellationTokenSource? _cts;

    /// <summary>Zählt die Aufbauten; Fortschritt eines verdrängten Aufbaus wird nicht mehr gezeigt.</summary>
    private int _run;

    private bool _started;

    /// <summary>Setzt die Mod-Liste gerade der Code: kein Aufbau aus SelectionChanged.</summary>
    private bool _fillingMods;

    /// <summary>Gewählte Mod; null, wenn keine gewählt werden kann.</summary>
    private string? _chosenMod;

    /// <summary>Ordner der aktiven Mod beim letzten Füllen der Liste; nur ein Wechsel der aktiven Mod überschreibt die Wahl im Fenster.</summary>
    private string? _lastActiveMod;

    private string? _lastPlaysetName;

    /// <summary>Mod des zuletzt gestarteten Aufbaus; ein neues Ergebnis der Sitzung gehört zu ihr.</summary>
    private string? _runMod;

    /// <summary>Statustext der Sitzung beim Start des letzten Aufbaus: Er beschreibt das alte Ergebnis, solange kein neuer Text hinzukommt.</summary>
    private string? _staleText;

    /// <summary>Mod, zu der das gezeigte Ergebnis gehört; null, solange es kein Ergebnis gibt.</summary>
    private string? _resultMod;

    /// <summary>Das zuletzt übernommene Ergebnis der Sitzung (zum Erkennen eines neuen).</summary>
    private GfxAuditResult? _seenResult;

    /// <param name="owner">null: eigenständiges Fenster mit eigenem Eintrag in der Taskleiste.</param>
    public GfxAuditWindow(GfxAuditWindowHost host, AppSettings settings, Window? owner)
    {
        _host = host;
        _settings = settings;
        Title = GfxAuditTexts.WindowTitle;
        Width = 1100;
        Height = 720;
        MinWidth = 640;
        MinHeight = 360;
        if (owner is not null)
        {
            Owner = owner;
        }

        ShowInTaskbar = owner is null;
        WindowBoundsPersistence.Apply(this, _settings.GfxAuditWindowBounds, null);

        _status.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _coverage.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _phaseText.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");

        FillMods();
        _modBox.SelectionChanged += (_, _) => ModBoxChanged();
        _refreshButton.Click += (_, _) => RefreshOrCancel();
        _moveOutButton.Click += (_, _) => MoveOutSelected();
        _archiveButton.Click += (_, _) => OpenArchive();
        Controls.ToolTipService.SetShowOnDisabled(_moveOutButton, true);
        Controls.ToolTipService.SetShowOnDisabled(_archiveButton, true);
        _session.Changed += Session_Changed;

        _searchTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Input, (_, _) => ShowFindings(rebuildFilters: false), Dispatcher);
        _searchTimer.Stop();
        _search.TextChanged += (_, _) =>
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        };
        _search.KeyDown += OnSearchKeyDown;

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        var header = BuildHeader();
        var findings = BuildFindingsPanel();
        Controls.DockPanel.SetDock(header, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_status, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_coverage, Controls.Dock.Top);
        root.Children.Add(header);
        root.Children.Add(_status);
        root.Children.Add(_coverage);
        root.Children.Add(findings);
        Content = root;

        UpdateHeader();
        Loaded += (_, _) =>
        {
            if (!_started)
            {
                StartRefresh();
            }
        };
        Closing += (_, _) => SaveBounds();
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    /// <summary>Erneutes Öffnen: geänderte Dateien neu lesen, solange kein Aufbau läuft.</summary>
    public void RefreshIfIdle()
    {
        // Bei "Automatisch" meldet das Hauptfenster einen Wechsel der aktiven Mod nicht: Erneutes Öffnen übernimmt ihn.
        if (_started && _session.State != GfxAuditState.Building)
        {
            if (!string.Equals(_host.ActiveModDirectory(), _lastActiveMod, StringComparison.OrdinalIgnoreCase))
            {
                FillMods();
                ReloadHideList();
            }

            StartRefresh();
        }
    }

    /// <summary>Anderes Playset in der PMT-Werkzeugleiste: neu aufbauen, wenn sich der Name geändert hat.</summary>
    public void PlaysetChanged()
    {
        if (_started && !string.Equals(_host.SelectedPlaysetName() ?? _host.Mods.DefaultPlaysetName(), _lastPlaysetName, StringComparison.Ordinal))
        {
            StartRefresh();
        }
    }

    /// <summary>Andere aktive Mod im Hauptfenster: Die Wahl im Fenster folgt ihr; ohne echten Wechsel passiert nichts.</summary>
    public void ActiveModChanged()
    {
        if (!string.Equals(_host.ActiveModDirectory(), _lastActiveMod, StringComparison.OrdinalIgnoreCase))
        {
            var before = _chosenMod;
            FillMods();
            if (string.Equals(before, _chosenMod, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ReloadHideList();
            ShowFindings(rebuildFilters: true);
            UpdateHeader();
            if (_started)
            {
                StartRefresh();
            }
        }
    }

    /// <summary>Füllt die Mod-Liste: eigene Mods und die aktive; vorgewählt ist die aktive, sonst die erste.</summary>
    private void FillMods()
    {
        var active = _host.ActiveModDirectory();
        _lastActiveMod = active;
        var choices = _host.Mods.OwnMods().Select(m => new ModChoice(m.Name, m.Directory)).ToList();
        if (active is not null && !choices.Any(c => string.Equals(c.Directory, active, StringComparison.OrdinalIgnoreCase)))
        {
            choices.Insert(0, new ModChoice(Path.GetFileName(active.TrimEnd('\\', '/')), active));
        }

        _fillingMods = true;
        _modBox.ItemsSource = choices;
        _modBox.SelectedItem = choices.FirstOrDefault(c => string.Equals(c.Directory, active, StringComparison.OrdinalIgnoreCase)) ?? choices.FirstOrDefault();
        _fillingMods = false;
        _chosenMod = (_modBox.SelectedItem as ModChoice)?.Directory;
        _modBox.IsEnabled = choices.Count > 0;
        _modBox.ToolTip = choices.Count > 0 ? null : GfxAuditTexts.NoMods;
    }

    private void ModBoxChanged()
    {
        if (_fillingMods || _modBox.SelectedItem is not ModChoice choice || string.Equals(choice.Directory, _chosenMod, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _chosenMod = choice.Directory;
        ReloadHideList();
        ShowFindings(rebuildFilters: true);
        UpdateHeader();
        StartRefresh();
    }

    /// <summary>Startet einen Aufbau; ein laufender wird vorher abgebrochen (die Sitzung verwirft sein Ergebnis ohnehin). Ohne Mod gibt es nichts zu prüfen.</summary>
    private async void StartRefresh()
    {
        _started = true;
        if (_moving || _closed)
        {
            // Während des Auslagerns nicht lesen; danach baut das Auslagern selbst neu auf.
            return;
        }

        if (_chosenMod is null)
        {
            _cts?.Cancel();
            ShowFindings(rebuildFilters: true);
            UpdateHeader();
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var run = ++_run;
        var progress = new Progress<BuildProgress>(p =>
        {
            if (run == _run)
            {
                ShowProgress(p);
            }
        });

        // Momentaufnahme im UI-Thread; die Funktion läuft im Hintergrund.
        var playsetName = _host.SelectedPlaysetName() ?? _host.Mods.DefaultPlaysetName();
        _lastPlaysetName = playsetName;
        _runMod = _chosenMod;
        _staleText = _session.StatusText;
        var createRequest = AnalysisRequests.Gfx(_host, _chosenMod, playsetName);
        try
        {
            await _session.RefreshAsync(createRequest, progress, cts.Token);
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
            {
                _cts = null;
                cts.Dispose();
            }
        }
    }

    private void Session_Changed(object? sender, EventArgs e)
    {
        // Ein neues Ergebnis gehört zur Mod des zuletzt gestarteten Aufbaus (verdrängte Aufbauten setzen keines).
        if (_session.Result is not null && !ReferenceEquals(_session.Result, _seenResult))
        {
            _seenResult = _session.Result;
            _resultMod = _runMod;
        }

        UpdateHeader();
        ShowFindings(rebuildFilters: true);
    }

    private void RefreshOrCancel()
    {
        if (_session.State == GfxAuditState.Building && _cts is not null)
        {
            _cts.Cancel();
        }
        else
        {
            StartRefresh();
        }
    }

    private void UpdateHeader()
    {
        var building = _session.State == GfxAuditState.Building;
        _refreshButton.Content = building && _cts is not null ? GfxAuditTexts.Cancel : GfxAuditTexts.Refresh;
        _progressBar.Visibility = building ? Visibility.Visible : Visibility.Collapsed;
        _phaseText.Visibility = building ? Visibility.Visible : Visibility.Collapsed;
        if (!building)
        {
            _phaseText.Text = string.Empty;
            _progressBar.IsIndeterminate = false;
            _progressBar.Value = 0;
        }
        else if (string.IsNullOrEmpty(_phaseText.Text))
        {
            _progressBar.IsIndeterminate = true;
        }

        _status.Text = StatusText();
        _status.ToolTip = StatusToolTip();
        UpdateMoveButtons(building);
    }

    /// <summary>
    /// Text der Statuszeile: Stand der Sitzung, dahinter warum Auslagern gesperrt ist, das Ergebnis des letzten Auslagerns und ein Fehler beim
    /// Speichern der Ausblende-Liste, solange er besteht.
    /// </summary>
    private string StatusText()
    {
        string text;
        if (_chosenMod is null)
        {
            text = GfxAuditTexts.NoActiveMod;
        }
        else if (_session.State is GfxAuditState.Ready or GfxAuditState.Building && _session.Result is not null && !ResultIsForChosenMod() && (_staleText is null || _session.StatusText == _staleText))
        {
            // Nach einem Mod-Wechsel gehört der Stand der Sitzung noch zur alten Mod (auch während des Aufbaus); Fehler und Abbruch des neuen Aufbaus zeigen ihren eigenen Text.
            text = string.Empty;
        }
        else
        {
            text = _session.StatusText;
        }

        text = Append(text, MoveBlockForStatus());
        text = Append(text, MoveStatusForChosenMod());
        return Append(text, _hideSaveError);

        static string Append(string text, string? more) => more is null ? text : text.Length == 0 ? more : text + "; " + more;
    }

    /// <summary>Tooltip der Statuszeile: nicht verschobene Dateien des letzten Auslagerns, dann nicht lesbare Dateien.</summary>
    private string? StatusToolTip()
    {
        var parts = new[] { MoveStatusForChosenMod() is null ? null : _moveTip, UnreadToolTip() }.OfType<string>().ToList();
        return parts.Count == 0 ? null : string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    private bool ResultIsForChosenMod() => _chosenMod is not null && string.Equals(_resultMod, _chosenMod, StringComparison.OrdinalIgnoreCase);

    private string? UnreadToolTip()
    {
        if (_session.Index is not { UnreadFiles.Count: > 0 } index || !ResultIsForChosenMod())
        {
            return null;
        }

        var lines = new List<string> { GfxAuditTexts.UnreadTip(index.UnreadFiles.Count) };
        lines.AddRange(index.UnreadFiles.Take(MaxUnreadInTip));
        return string.Join(Environment.NewLine, lines);
    }

    private void ShowProgress(BuildProgress progress)
    {
        if (_session.State != GfxAuditState.Building)
        {
            return;
        }

        _phaseText.Text = GfxAuditTexts.Phase(progress.Phase, progress.Done, progress.Total);
        _progressBar.IsIndeterminate = progress.Total <= 0;
        _progressBar.Maximum = Math.Max(1, progress.Total);
        _progressBar.Value = Math.Min(progress.Done, _progressBar.Maximum);
    }

    private Controls.DockPanel BuildHeader()
    {
        var right = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        right.Children.Add(_refreshButton);
        right.Children.Add(_moveOutButton);
        right.Children.Add(_archiveButton);
        right.Children.Add(_progressBar);
        right.Children.Add(_phaseText);

        var left = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        left.Children.Add(new Controls.TextBlock { Text = GfxAuditTexts.ModLabel, VerticalAlignment = VerticalAlignment.Center });
        left.Children.Add(_modBox);
        left.Children.Add(_search);

        var header = new Controls.DockPanel { LastChildFill = false };
        Controls.DockPanel.SetDock(left, Controls.Dock.Left);
        header.Children.Add(left);
        header.Children.Add(right);
        return header;
    }

    /// <summary>F5 wie der Knopf "Aktualisieren", auch während ein Eingabefeld den Fokus hat.</summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            StartRefresh();
            e.Handled = true;
        }
    }

    /// <summary>Enter im Suchfeld sucht sofort, ohne die Wartezeit nach dem Tippen.</summary>
    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            ShowFindings(rebuildFilters: false);
            e.Handled = true;
        }
    }

    /// <summary>Mit dem Fenster endet ein laufender Aufbau, und es meldet sich bei der Sitzung ab.</summary>
    protected override void OnClosed(EventArgs e)
    {
        _searchTimer?.Stop();
        _closed = true;
        _cts?.Cancel();
        _session.Changed -= Session_Changed;
        base.OnClosed(e);
    }

    /// <summary>Merkt Position und Größe (auch das Hauptfenster ruft das vor dem Speichern der Einstellungen auf).</summary>
    public void SaveBounds()
    {
        if (WindowBoundsPersistence.Capture(this) is { } bounds)
        {
            _settings.GfxAuditWindowBounds = bounds;
        }
    }

    /// <summary>Eintrag der Mod-Auswahl.</summary>
    private sealed record ModChoice(string Name, string Directory);
}
