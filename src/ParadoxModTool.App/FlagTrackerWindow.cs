using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.ReferenceTree;
using ParadoxModTool.Settings;
using Controls = System.Windows.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ParadoxModTool.App;

/// <summary>
/// Fenster "Flag-Tracker" (Spec "Fenster in ParadoxModTool.App"): Kopf mit Suchfeld, Bereich, Knopf "Aktualisieren"/"Abbrechen" und
/// Fortschritt, darunter der Stand des Verzeichnisses und die Reiter "Übersicht" (<c>FlagTrackerWindow.Overview.cs</c>) und "Alle Stellen"
/// (<c>FlagTrackerWindow.Sites.cs</c>). Der Aufbau läuft im Hintergrund über <see cref="FlagTrackerSession"/>; die Suche läuft auf dem
/// gezeigten Verzeichnis im UI-Thread und blockiert nie auf den Aufbau. Ohne Besitzer ist es ein eigenständiges Fenster mit eigenem Eintrag
/// in der Taskleiste (Einstellung des Fehlerprotokolls, wie der Performance-Checker).
/// </summary>
internal sealed partial class FlagTrackerWindow : Window
{
    /// <summary>Höchstens so viele nicht lesbare Dateien im Tooltip der Statuszeile; die Anzahl steht in dessen erster Zeile.</summary>
    private const int MaxUnreadInTip = 40;

    private readonly FlagTrackerWindowHost _host;
    private readonly AppSettings _settings;
    private readonly SharedSessions _sessions;

    /// <summary>Sitzung des aktuellen Schlüssels (aktive Mod, Playset, Bereich) aus dem geteilten Register; wechselt in <see cref="StartRefresh"/>.</summary>
    private FlagTrackerSession _session;
    private readonly Controls.TextBox _searchBox = new() { VerticalContentAlignment = VerticalAlignment.Center, ToolTip = FlagTrackerTexts.SearchTip };
    private readonly Controls.ComboBox _scopeBox = new() { MinWidth = 180, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Controls.Button _refreshButton = new() { Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(8, 0, 0, 0) };
    private readonly Controls.ProgressBar _progressBar = new() { Width = 120, Height = 6, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBlock _phaseText = new() { Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, MinWidth = 160 };
    private readonly Controls.TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.TextBlock _truncated = new() { Text = FlagTrackerTexts.Truncated, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _warningsHint = new() { Text = FlagTrackerTexts.WarningsOnly, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.TabControl _tabs = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.TabItem _overviewTab = new() { Header = FlagTrackerTexts.TabOverview };
    private readonly Controls.TabItem _sitesTab = new() { Header = FlagTrackerTexts.TabSites };
    private readonly DispatcherTimer _searchTimer;

    /// <summary>Ergebnis der letzten Suche; beide Listen zeigen es.</summary>
    private FlagTrackerView _view = FlagTrackerView.Empty;

    /// <summary>Abbruch des laufenden Aufbaus; null, wenn keiner läuft. Vor jedem neuen Aufbau abgebrochen (beide schrieben dieselbe Datei).</summary>
    private CancellationTokenSource? _cts;

    /// <summary>Zählt die Aufbauten; Fortschritt eines verdrängten Aufbaus wird nicht mehr gezeigt.</summary>
    private int _run;

    private bool _started;

    /// <summary>Befehlstabelle des letzten (laufenden oder fertigen) Aufbaus; neue ScriptDocs-Daten bauen nur neu, wenn sich ihr Fingerabdruck ändert.</summary>
    private FlagCommandTable? _lastTable;

    /// <summary>Ordner der aktiven Mod beim letzten Aufbau; <see cref="ActiveModChanged"/> baut nur bei anderem Ordner neu.</summary>
    private string? _lastActiveModDirectory;

    /// <summary>Playset des letzten Aufbaus (Bereich "Playset"); ein Wechsel in der Werkzeugleiste baut nur bei anderem Namen neu.</summary>
    private string? _lastPlaysetName;

    /// <param name="sessions">Geteiltes Register des Hauptfensters; gleiche Werte teilen sich die Sitzung (z. B. mit dem Verweisbaum).</param>
    /// <param name="owner">null: eigenständiges Fenster mit eigenem Eintrag in der Taskleiste.</param>
    public FlagTrackerWindow(FlagTrackerWindowHost host, SharedSessions sessions, AppSettings settings, Window? owner)
    {
        _host = host;
        _sessions = sessions;
        _settings = settings;
        // Bis zum ersten Aufbau die Sitzung zu den jetzigen Werten (Bereich "Playset" ist vorgewählt), damit ihr Zustand nie fehlt.
        _session = sessions.Flags(SharedSessions.FlagKey(host.ActiveModDirectory(), host.SelectedPlaysetName() ?? host.Mods.DefaultPlaysetName(), isPlayset: true), this);
        Title = FlagTrackerTexts.WindowTitle;
        Width = 1100;
        Height = 720;
        MinWidth = 640;
        MinHeight = 360;
        if (owner is not null)
        {
            Owner = owner;
        }

        ShowInTaskbar = owner is null;
        WindowBoundsPersistence.Apply(this, _settings.FlagTrackerWindowBounds, null);

        _searchTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Input, (_, _) => RunSearchNow(), Dispatcher);
        _searchTimer.Stop();
        _status.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _truncated.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Warning");
        _warningsHint.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _phaseText.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");

        _scopeBox.Items.Add(FlagTrackerTexts.ScopePlayset);
        _scopeBox.Items.Add(FlagTrackerTexts.ScopeAll);
        _scopeBox.SelectedIndex = 0;

        // Erst nach der Vorauswahl verbinden, sonst startete das erste Öffnen zwei Aufbauten.
        _scopeBox.SelectionChanged += (_, _) => StartRefresh();
        _searchBox.TextChanged += (_, _) =>
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        };
        _searchBox.KeyDown += SearchBox_KeyDown;
        _refreshButton.Click += (_, _) => RefreshOrCancel();
        _session.Changed += Session_Changed;

        _overviewTab.Content = BuildOverviewTab();
        _sitesTab.Content = BuildSitesTab();
        _tabs.Items.Add(_overviewTab);
        _tabs.Items.Add(_sitesTab);

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        var header = BuildHeader();
        var hideBar = BuildHideBar();
        Controls.DockPanel.SetDock(header, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_status, Controls.Dock.Top);
        Controls.DockPanel.SetDock(hideBar, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_truncated, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_warningsHint, Controls.Dock.Top);
        root.Children.Add(header);
        root.Children.Add(_status);
        root.Children.Add(hideBar);
        root.Children.Add(_truncated);
        root.Children.Add(_warningsHint);
        root.Children.Add(_tabs);
        Content = root;

        UpdateHeader();
        Loaded += (_, _) =>
        {
            // Erstes Öffnen: Aufbau für den Bereich "Playset"; das Fenster bleibt währenddessen bedienbar.
            if (!_started && !JoinRunningBuild())
            {
                _started = true;
                StartRefresh();
            }
        };
        Closing += (_, _) => SaveBounds();
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    /// <summary>Füllt das Suchfeld, sucht sofort und zeigt die Übersicht.</summary>
    public void Search(string query)
    {
        _searchBox.Text = query;
        _searchBox.CaretIndex = query.Length;
        RunSearchNow();
        _tabs.SelectedItem = _overviewTab;
    }

    /// <summary>Erneutes Öffnen: geänderte Dateien neu lesen (Spec Ziel 6, "beim nächsten Öffnen"), solange kein Aufbau läuft.</summary>
    public void RefreshIfIdle()
    {
        if (_started && _session.State != FlagTrackerState.Building)
        {
            StartRefresh();
        }
    }

    /// <summary>
    /// Neue ScriptDocs-Daten: baut nur neu, wenn sich die Befehlstabelle (Fingerabdruck) gegenüber dem letzten Aufbau geändert hat.
    /// Ohne bisherigen Aufbau passiert nichts (der erste kommt aus Loaded).
    /// </summary>
    public void RestartIfCommandTableChanged()
    {
        if (!_started)
        {
            return;
        }

        var table = FlagCommandTable.For(_host.GameId, _host.ScriptDocNames());
        if (table is null || _lastTable is null || table.Fingerprint != _lastTable.Fingerprint)
        {
            StartRefresh();
        }
    }

    /// <summary>
    /// Anderes Playset in der PMT-Werkzeugleiste: im Bereich "Playset" neu aufbauen, wenn sich der Name gegenüber dem letzten Aufbau
    /// geändert hat (die Liste wird auch beim bloßen Neuladen neu gesetzt).
    /// </summary>
    public void PlaysetChanged()
    {
        if (_started && _scopeBox.SelectedIndex != 1
            && !string.Equals(_host.SelectedPlaysetName() ?? _host.Mods.DefaultPlaysetName(), _lastPlaysetName, StringComparison.Ordinal))
        {
            StartRefresh();
        }
    }

    /// <summary>
    /// Startet einen Aufbau für den gewählten Bereich. Ein laufender wird vorher abgebrochen: Er schriebe dieselbe Verzeichnisdatei, und die
    /// Sitzung verwirft sein Ergebnis ohnehin. Die Werte des Hauptfensters werden hier im UI-Thread gelesen.
    /// </summary>
    public async void StartRefresh()
    {
        // Ein Aufbau vor dem Laden (z. B. durch neue ScriptDocs-Daten) ersetzt den ersten Aufbau aus Loaded.
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

        var isPlayset = _scopeBox.SelectedIndex != 1;
        var createRequest = CreateRequestFactory(isPlayset);
        // Schlüssel aus denselben Werten, die die Fabrik eben festgehalten hat.
        UseSession(_sessions.Flags(SharedSessions.FlagKey(_lastActiveModDirectory, _lastPlaysetName, isPlayset), this));
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
    /// Setzt die letzten Werte und die Ausblendliste wie <see cref="StartRefresh"/> (<see cref="RememberRequestValues"/>), ohne eine Anfrage zu bauen.
    /// </summary>
    private bool JoinRunningBuild()
    {
        if (_cts is not null || _session.State != FlagTrackerState.Building)
        {
            return false;
        }

        var isPlayset = _scopeBox.SelectedIndex != 1;
        RememberRequestValues();
        var session = _sessions.Flags(SharedSessions.FlagKey(_lastActiveModDirectory, _lastPlaysetName, isPlayset), this);
        UseSession(session);
        if (session.State != FlagTrackerState.Building)
        {
            return false;
        }

        _started = true;
        UpdateHeader();
        return true;
    }

    /// <summary>
    /// Momentaufnahme im UI-Thread (Playset, ScriptDocs, aktive Mod; den Rest liest <see cref="AnalysisRequests.Flags"/>); die zurückgegebene
    /// Funktion läuft im Hintergrund und liest dort nur noch die installierten Mods und die Dateien.
    /// </summary>
    private Func<FlagTrackerRequest> CreateRequestFactory(bool isPlayset)
    {
        var (playsetName, table, activeModDirectory) = RememberRequestValues();
        return AnalysisRequests.Flags(_host, isPlayset, playsetName, activeModDirectory, table);
    }

    /// <summary>Hält Playset, Befehlstabelle und aktive Mod fest und lädt die Ausblendliste; liefert die Werte für die Anfrage.</summary>
    private (string? PlaysetName, FlagCommandTable? Table, string? ActiveModDirectory) RememberRequestValues()
    {
        // Florian 2026-10-28: immer das in PMT (Werkzeugleiste) gewählte Playset; ohne Auswahl das aktive des Launchers.
        var playsetName = _host.SelectedPlaysetName() ?? _host.Mods.DefaultPlaysetName();
        _lastPlaysetName = playsetName;
        var table = FlagCommandTable.For(_host.GameId, _host.ScriptDocNames());
        _lastTable = table;
        var activeModDirectory = _host.ActiveModDirectory();
        _lastActiveModDirectory = activeModDirectory;
        ReloadHideList(activeModDirectory, table?.NamesIgnoreCase ?? false);
        return (playsetName, table, activeModDirectory);
    }

    /// <summary>Wechselt zur Sitzung eines anderen Schlüssels: Anmeldung umhängen und die Anzeige einmal auf die neue Sitzung bringen.</summary>
    private void UseSession(FlagTrackerSession session)
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
        if (_session.State == FlagTrackerState.Building && _cts is not null)
        {
            _cts.Cancel();
        }
        else
        {
            StartRefresh();
        }
    }

    /// <summary>Nach jeder Zustandsänderung der Sitzung: Kopf und Stand neu, Suche mit dem Text im Feld auf dem (evtl. neuen) Verzeichnis.</summary>
    private void OnSessionChanged()
    {
        UpdateHeader();
        RunSearchNow();
    }

    private void UpdateHeader()
    {
        var building = _session.State == FlagTrackerState.Building;
        // Einen Aufbau des Verweisbaums (kein eigenes _cts) bricht das Fenster nicht ab: dann "Aktualisieren" statt "Abbrechen".
        _refreshButton.Content = building && _cts is not null ? FlagTrackerTexts.Cancel : FlagTrackerTexts.Refresh;
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
    }

    /// <summary>Text der Statuszeile: Stand der Sitzung, dahinter ein Fehler beim Speichern der Ausblendregeln, solange er besteht.</summary>
    private string StatusText() => _hideSaveError is null ? _session.StatusText : _session.StatusText + "; " + _hideSaveError;

    /// <summary>Tooltip der Statuszeile: Anzahl und die ersten nicht lesbaren Dateien; null, wenn alle lesbar waren.</summary>
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
        if (_session.State != FlagTrackerState.Building)
        {
            return;
        }

        _phaseText.Text = FlagTrackerTexts.Phase(progress.Phase, progress.Done, progress.Total);
        _progressBar.IsIndeterminate = progress.Phase == BuildPhase.Resolving || progress.Total <= 0;
        _progressBar.Maximum = Math.Max(1, progress.Total);
        _progressBar.Value = Math.Min(progress.Done, _progressBar.Maximum);
    }

    /// <summary>Sucht mit dem Text im Feld im gezeigten Verzeichnis (Millisekunden) und füllt beide Listen.</summary>
    private void RunSearchNow()
    {
        _searchTimer.Stop();
        _view = _session.Search(_searchBox.Text, _hide, ShowHiddenChecked);
        UpdateHideBar();
        _truncated.Visibility = _view.Truncated ? Visibility.Visible : Visibility.Collapsed;
        _warningsHint.Visibility = _session.Index is not null && string.IsNullOrWhiteSpace(_searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        ShowOverview();
        ShowSites();
    }

    private Controls.DockPanel BuildHeader()
    {
        var right = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        right.Children.Add(_scopeBox);
        right.Children.Add(_refreshButton);
        right.Children.Add(_progressBar);
        right.Children.Add(_phaseText);

        var header = new Controls.DockPanel { LastChildFill = true };
        Controls.DockPanel.SetDock(right, Controls.Dock.Right);
        header.Children.Add(right);
        header.Children.Add(_searchBox);
        return header;
    }

    /// <summary>Enter im Suchfeld sucht sofort, ohne die Wartezeit nach dem Tippen.</summary>
    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            RunSearchNow();
            e.Handled = true;
        }
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

    /// <summary>Öffnet eine Fundstelle im Editor des Hauptfensters; ein Lesefehler landet in der Statuszeile.</summary>
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
        _searchTimer.Stop();
        _cts?.Cancel();
        _session.Changed -= Session_Changed;
        _sessions.Release(this);
        base.OnClosed(e);
    }

    /// <summary>Merkt Position und Größe (auch das Hauptfenster ruft das vor dem Speichern der Einstellungen auf).</summary>
    public void SaveBounds()
    {
        if (WindowBoundsPersistence.Capture(this) is { } bounds)
        {
            _settings.FlagTrackerWindowBounds = bounds;
        }
    }
}
