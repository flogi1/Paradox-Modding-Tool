using System.IO;
using System.Windows;
using System.Windows.Input;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.ReferenceTree;
using ParadoxModTool.ScriptCheck;
using ParadoxModTool.Settings;
using Controls = System.Windows.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ParadoxModTool.App;

/// <summary>
/// Fenster "Skript-Prüfung": Kopf mit Mod-Auswahl, Knopf "Aktualisieren"/"Abbrechen" und Fortschritt, darunter Stand und was nicht geprüft wird,
/// dann die Reiter "Befunde" (<c>ScriptCheckWindow.Findings.cs</c>), "Wer ruft wen" (<c>ScriptCheckWindow.Calls.cs</c>) und "Leistung anderer Ziele"
/// (<c>ScriptCheckWindow.Performance.cs</c>, eigene Sitzung). Der Aufbau läuft im
/// Hintergrund über <see cref="ScriptCheckSession"/>. Die Bedienung folgt dem Flag-Tracker.
/// </summary>
internal sealed partial class ScriptCheckWindow : Window
{
    /// <summary>Höchstens so viele nicht lesbare Dateien im Tooltip der Statuszeile.</summary>
    private const int MaxUnreadInTip = 40;

    private readonly ScriptCheckWindowHost _host;
    private readonly AppSettings _settings;
    private readonly SharedSessions _sessions;

    /// <summary>Sitzung des aktuellen Schlüssels (gewählte Mod, Playset) aus dem geteilten Register; wechselt in <see cref="StartRefresh"/>.</summary>
    private ScriptCheckSession _session;
    private readonly Controls.ComboBox _modBox = new() { MinWidth = 260, Margin = new Thickness(6, 0, 0, 0), DisplayMemberPath = nameof(ModChoice.Name) };
    private readonly Controls.Button _refreshButton = new() { Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(8, 0, 0, 0) };
    private readonly Controls.ProgressBar _progressBar = new() { Width = 120, Height = 6, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBlock _phaseText = new() { Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, MinWidth = 160 };
    private readonly Controls.TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.TextBlock _coverage = new() { Text = ScriptCheckTexts.Coverage, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
    private readonly Controls.TabControl _tabs = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.TabItem _findingsTab = new() { Header = ScriptCheckTexts.TabFindings };
    private readonly Controls.TabItem _callsTab = new() { Header = ScriptCheckTexts.TabCalls };

    /// <summary>Abbruch des laufenden Aufbaus; null, wenn keiner läuft.</summary>
    private CancellationTokenSource? _cts;

    /// <summary>Zählt die Aufbauten; Fortschritt eines verdrängten Aufbaus wird nicht mehr gezeigt.</summary>
    private int _run;

    private bool _started;

    /// <summary>Setzt die Mod-Liste gerade der Code: kein Aufbau aus SelectionChanged.</summary>
    private bool _fillingMods;

    /// <summary>Gewählte Mod; null, wenn keine gewählt werden kann.</summary>
    private string? _chosenMod;

    /// <summary>Die Mod-Liste hat Einträge (beim letzten Füllen); ob die Auswahl bedienbar ist, entscheidet <see cref="ApplyHeaderEnabled"/>.</summary>
    private bool _modsAvailable;

    /// <summary>Ordner der aktiven Mod beim letzten Füllen der Liste; nur ein Wechsel der aktiven Mod überschreibt die Wahl im Fenster.</summary>
    private string? _lastActiveMod;

    private string? _lastPlaysetName;

    /// <summary>Anzahl der eingebauten Befehle beim letzten Aufbau; neue Script-Doku baut nur neu, wenn sie sich geändert hat.</summary>
    private int _lastBuiltInCount;

    /// <param name="sessions">Geteiltes Register des Hauptfensters; gleiche Werte teilen sich die Sitzung (z. B. mit dem Verweisbaum).</param>
    /// <param name="owner">null: eigenständiges Fenster mit eigenem Eintrag in der Taskleiste.</param>
    public ScriptCheckWindow(ScriptCheckWindowHost host, SharedSessions sessions, AppSettings settings, Window? owner)
    {
        _host = host;
        _sessions = sessions;
        _settings = settings;
        Title = ScriptCheckTexts.WindowTitle;
        Width = 1100;
        Height = 720;
        MinWidth = 640;
        MinHeight = 360;
        if (owner is not null)
        {
            Owner = owner;
        }

        ShowInTaskbar = owner is null;
        WindowBoundsPersistence.Apply(this, _settings.ScriptCheckWindowBounds, null);

        _status.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _coverage.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _phaseText.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");

        FillMods();
        // Bis zum ersten Aufbau die Sitzung zu den jetzigen Werten, damit ihr Zustand nie fehlt.
        _session = sessions.Checks(SharedSessions.CheckKey(_chosenMod, host.SelectedPlaysetName() ?? host.Mods.DefaultPlaysetName()), this);
        _modBox.SelectionChanged += (_, _) => ModBoxChanged();
        _refreshButton.Click += (_, _) => RefreshOrCancel();
        Controls.ToolTipService.SetShowOnDisabled(_modBox, true);
        Controls.ToolTipService.SetShowOnDisabled(_refreshButton, true);
        _session.Changed += Session_Changed;

        _findingsTab.Content = BuildFindingsTab();
        _callsTab.Content = BuildCallsTab();
        _perfTab.Content = BuildPerformanceTab();
        _tabs.Items.Add(_findingsTab);
        _tabs.Items.Add(_callsTab);
        _tabs.Items.Add(_perfTab);
        _tabs.SelectionChanged += Tabs_SelectionChanged;

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        var header = BuildHeader();
        Controls.DockPanel.SetDock(header, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_status, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_coverage, Controls.Dock.Top);
        root.Children.Add(header);
        root.Children.Add(_status);
        root.Children.Add(_coverage);
        root.Children.Add(_tabs);
        Content = root;

        UpdateHeader();
        Loaded += (_, _) =>
        {
            if (!_started && !JoinRunningBuild())
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
        RefreshTargetsIfShown();

        // Bei "Automatisch" meldet das Hauptfenster einen Wechsel der aktiven Mod nicht: Erneutes Öffnen übernimmt ihn.
        if (_started && _session.State != ScriptCheckState.Building)
        {
            if (!string.Equals(_host.ActiveModDirectory(), _lastActiveMod, StringComparison.OrdinalIgnoreCase))
            {
                FillMods();
                ReloadHideList();
                ClearCallSelection();
            }

            StartRefresh();
        }
    }

    /// <summary>Anderes Playset in der PMT-Werkzeugleiste: neu aufbauen, wenn sich der Name geändert hat; die Ziele fremder Leistungsprüfung neu lesen.</summary>
    public void PlaysetChanged()
    {
        RefreshTargetsIfShown();
        if (_started && !string.Equals(_host.SelectedPlaysetName() ?? _host.Mods.DefaultPlaysetName(), _lastPlaysetName, StringComparison.Ordinal))
        {
            StartRefresh();
        }
    }

    /// <summary>Neue Script-Doku: neu auswerten, wenn sich die eingebauten Befehle geändert haben.</summary>
    public void ScriptDocsChanged()
    {
        if (_started && ScriptCheckBuiltIns.FromDatabase(_host.ScriptDocs()).Count != _lastBuiltInCount)
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
            ClearCallSelection();
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
        _modsAvailable = choices.Count > 0;
        ApplyHeaderEnabled();
    }

    private bool PerfTabSelected => ReferenceEquals(_tabs.SelectedItem, _perfTab);

    /// <summary>
    /// Einzige Stelle für Bedienbarkeit und Tooltip von Mod-Auswahl und "Aktualisieren": Im Reiter "Leistung anderer Ziele" sind beide grau (sie gelten
    /// für die eigene Mod); sonst ist die Auswahl bedienbar, wenn es Mods gibt. Ein laufender eigener Aufbau läuft dabei weiter.
    /// </summary>
    private void ApplyHeaderEnabled()
    {
        var perf = PerfTabSelected;
        _modBox.IsEnabled = !perf && _modsAvailable;
        _modBox.ToolTip = perf ? ScriptCheckTexts.HeaderOwnModOnly : _modsAvailable ? null : ScriptCheckTexts.NoMods;
        _refreshButton.IsEnabled = !perf;
        _refreshButton.ToolTip = perf ? ScriptCheckTexts.HeaderOwnModOnly : null;
    }

    private void ModBoxChanged()
    {
        if (_fillingMods || _modBox.SelectedItem is not ModChoice choice || string.Equals(choice.Directory, _chosenMod, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _chosenMod = choice.Directory;
        ReloadHideList();
        ClearCallSelection();
        ShowFindings(rebuildFilters: true);
        UpdateHeader();
        StartRefresh();
    }

    /// <summary>Startet einen Aufbau; ein laufender wird vorher abgebrochen (die Sitzung verwirft sein Ergebnis ohnehin).</summary>
    private async void StartRefresh()
    {
        _started = true;
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

        var createRequest = CreateRequestFactory();
        // Schlüssel aus denselben Werten, die die Fabrik eben festgehalten hat.
        UseSession(_sessions.Checks(SharedSessions.CheckKey(_chosenMod, _lastPlaysetName), this));
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

    /// <summary>
    /// Erstes Öffnen, während der Verweisbaum für dieselben Werte schon aufbaut: Das Fenster folgt diesem Aufbau über <c>Changed</c>, statt ihn
    /// zu verdrängen (der Aufbau läuft nur einmal; der Knopf zeigt "Aktualisieren", weil das Fenster kein eigenes <c>_cts</c> hat).
    /// Setzt die letzten Werte wie <see cref="StartRefresh"/> (<see cref="RememberRequestValues"/>), ohne eine Anfrage zu bauen.
    /// </summary>
    private bool JoinRunningBuild()
    {
        if (_cts is not null || _session.State != ScriptCheckState.Building)
        {
            return false;
        }

        RememberRequestValues();
        var session = _sessions.Checks(SharedSessions.CheckKey(_chosenMod, _lastPlaysetName), this);
        UseSession(session);
        if (session.State != ScriptCheckState.Building)
        {
            return false;
        }

        _started = true;
        UpdateHeader();
        return true;
    }

    /// <summary>Momentaufnahme im UI-Thread (über <see cref="AnalysisRequests.Checks(ScriptCheckWindowHost, string, string?, IReadOnlySet{string})"/>); die zurückgegebene Funktion läuft im Hintergrund.</summary>
    private Func<ScriptCheckRequest> CreateRequestFactory()
    {
        var (playsetName, builtIns) = RememberRequestValues();
        return AnalysisRequests.Checks(_host, _chosenMod ?? string.Empty, playsetName, builtIns);
    }

    /// <summary>Hält Playset und Zahl der eingebauten Befehle fest (Vergleich bei späteren Wechseln); liefert die Werte für die Anfrage.</summary>
    private (string? PlaysetName, IReadOnlySet<string> BuiltIns) RememberRequestValues()
    {
        var playsetName = _host.SelectedPlaysetName() ?? _host.Mods.DefaultPlaysetName();
        _lastPlaysetName = playsetName;
        var builtIns = ScriptCheckBuiltIns.FromDatabase(_host.ScriptDocs());
        _lastBuiltInCount = builtIns.Count;
        return (playsetName, builtIns);
    }

    /// <summary>Wechselt zur Sitzung eines anderen Schlüssels: Anmeldung umhängen und die Anzeige einmal auf die neue Sitzung bringen.</summary>
    private void UseSession(ScriptCheckSession session)
    {
        if (ReferenceEquals(session, _session))
        {
            return;
        }

        _session.Changed -= Session_Changed;
        _session = session;
        _session.Changed += Session_Changed;
        OnSessionChanged();
    }

    private void Session_Changed(object? sender, EventArgs e) => OnSessionChanged();

    /// <summary>
    /// Bricht den eigenen Aufbau ab oder startet einen. Läuft ein Aufbau, den der Verweisbaum gestartet hat (kein eigenes <c>_cts</c>), startet
    /// das Fenster einen neuen, der ihn verdrängt: Den Aufbau eines anderen Nutzers der Sitzung bricht es nie ab.
    /// </summary>
    private void RefreshOrCancel()
    {
        if (_session.State == ScriptCheckState.Building && _cts is not null)
        {
            _cts.Cancel();
        }
        else
        {
            StartRefresh();
        }
    }

    private void OnSessionChanged()
    {
        UpdateHeader();
        ShowFindings(rebuildFilters: true);
        ShowCalls();
    }

    private void UpdateHeader()
    {
        var building = _session.State == ScriptCheckState.Building;
        // Einen Aufbau des Verweisbaums (kein eigenes _cts) bricht das Fenster nicht ab: dann "Aktualisieren" statt "Abbrechen".
        _refreshButton.Content = building && _cts is not null ? ScriptCheckTexts.Cancel : ScriptCheckTexts.Refresh;
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
        _status.ToolTip = UnreadToolTip();
        ApplyHeaderEnabled();
    }

    /// <summary>Text der Statuszeile: Stand der Sitzung, dahinter ein Fehler beim Speichern der Ausblende-Liste, solange er besteht.</summary>
    private string StatusText()
    {
        // Nach einem Mod-Wechsel gehört der Stand der Sitzung noch zur alten Mod.
        var text = _session.State != ScriptCheckState.Building && ScriptCheckView.StateOf(_session.Index, _chosenMod) == ScriptCheckResultState.OtherMod
            ? ScriptCheckTexts.OtherModResult
            : _session.StatusText;
        return _hideSaveError is null ? text : text + "; " + _hideSaveError;
    }

    /// <summary>Das Ergebnis, wenn es zur gewählten Mod gehört und Vanilla kennt; sonst null: keine Zeilen statt falscher Funde.</summary>
    private ScriptCheckIndex? UsableIndex => ScriptCheckView.StateOf(_session.Index, _chosenMod) == ScriptCheckResultState.Usable ? _session.Index : null;

    private string? UnreadToolTip()
    {
        if (_session.Index is not { UnreadFiles.Count: > 0 } index)
        {
            return null;
        }

        var lines = new List<string> { FlagTrackerTexts.UnreadTip(index.UnreadFiles.Count) };
        lines.AddRange(index.UnreadFiles.Take(MaxUnreadInTip));
        return string.Join(Environment.NewLine, lines);
    }

    private void ShowProgress(BuildProgress progress)
    {
        if (_session.State != ScriptCheckState.Building)
        {
            return;
        }

        _phaseText.Text = ScriptCheckTexts.Phase(progress.Phase, progress.Done, progress.Total);
        _progressBar.IsIndeterminate = progress.Total <= 0;
        _progressBar.Maximum = Math.Max(1, progress.Total);
        _progressBar.Value = Math.Min(progress.Done, _progressBar.Maximum);
    }

    private Controls.DockPanel BuildHeader()
    {
        var right = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        right.Children.Add(_refreshButton);
        right.Children.Add(_progressBar);
        right.Children.Add(_phaseText);

        var left = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        left.Children.Add(new Controls.TextBlock { Text = ScriptCheckTexts.ModLabel, VerticalAlignment = VerticalAlignment.Center });
        left.Children.Add(_modBox);

        var header = new Controls.DockPanel { LastChildFill = false };
        Controls.DockPanel.SetDock(left, Controls.Dock.Left);
        header.Children.Add(left);
        header.Children.Add(right);
        return header;
    }

    /// <summary>
    /// F5 wie der Knopf "Aktualisieren", auch während ein Eingabefeld den Fokus hat; im Reiter "Leistung anderer Ziele" wie "Leistung prüfen"/"Abbrechen"
    /// (nur, wenn der Knopf bedienbar ist).
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (!PerfTabSelected)
            {
                StartRefresh();
            }
            else if (_perfButton.IsEnabled)
            {
                CheckOrCancelPerformance();
            }

            e.Handled = true;
        }
    }

    /// <summary>Öffnet eine Stelle im Editor des Hauptfensters; ein Lesefehler landet in der Statuszeile.</summary>
    private void OpenLocation(SiteLocation location)
    {
        try
        {
            _host.OpenFileAtLine(location.FullPath, location.Line);
            _status.Text = StatusText();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = FlagTrackerTexts.OpenFailed(ex.Message);
        }
    }

    /// <summary>
    /// Mit dem Fenster endet ein laufender Aufbau; das Fenster gibt seine Sitzung im Register frei (ohne weiteren Nutzer, z. B. den
    /// Verweisbaum, fällt sie heraus) und meldet sich dort nicht mehr.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        StopTimers();
        _cts?.Cancel();
        _perfCts?.Cancel();
        _session.Changed -= Session_Changed;
        _perfSession.Changed -= PerfSession_Changed;
        _sessions.Release(this);
        base.OnClosed(e);
    }

    /// <summary>Merkt Position und Größe (auch das Hauptfenster ruft das vor dem Speichern der Einstellungen auf).</summary>
    public void SaveBounds()
    {
        if (WindowBoundsPersistence.Capture(this) is { } bounds)
        {
            _settings.ScriptCheckWindowBounds = bounds;
        }
    }

    /// <summary>Eintrag der Mod-Auswahl.</summary>
    private sealed record ModChoice(string Name, string Directory);
}
