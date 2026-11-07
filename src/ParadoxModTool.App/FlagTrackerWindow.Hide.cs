using System.IO;
using System.Windows;
using System.Windows.Threading;
using ParadoxModTool.FlagTracker;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Einträge ausblenden (Spec-Nachtrag 2026-10-28): Kontextmenü der Übersicht („Ausblenden“, „Ähnliche ausblenden …“, „Einblenden“), unter der
/// Statuszeile der Schalter „Ausgeblendete zeigen (n)“ und der Knopf „Ausblendregeln …“. Die Regeln gelten je aktiver Mod und liegen in
/// <c>&lt;Spieldatenordner&gt;/flag-tracker/hidden</c>; ohne aktive Mod gibt es keine Regeln, und alles hier ist gesperrt.
/// </summary>
internal sealed partial class FlagTrackerWindow
{
    private readonly Controls.CheckBox _showHiddenBox = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.Button _rulesButton = new() { Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(12, 0, 0, 0) };
    private readonly Controls.MenuItem _hideItem = new() { Header = FlagTrackerTexts.HideMenu };
    private readonly Controls.MenuItem _hideSimilarItem = new() { Header = FlagTrackerTexts.HideSimilar + " …" };
    private readonly Controls.MenuItem _unhideItem = new() { Header = FlagTrackerTexts.Unhide };

    /// <summary>Regeln der aktiven Mod des letzten Aufbaus; null ohne aktive Mod (Ausblenden gesperrt).</summary>
    private FlagHideList? _hide;

    /// <summary>Ordner der aktiven Mod, zu dem <see cref="_hide"/> gehört, und dessen Groß/Klein-Regel; neu geladen wird nur bei Wechsel.</summary>
    private string? _hideModDirectory;

    private bool _hideIgnoreCase;

    /// <summary>Höchstzahl der Zeilen, deren Auswahl nach einer Änderung der Regeln wiederhergestellt wird.</summary>
    private const int MaxRestoredSelection = 200;

    /// <summary>
    /// Letzter Fehler beim Speichern der Regeln; steht hinter dem Stand in der Statuszeile, bis die nächste Änderung der Regeln gespeichert
    /// werden konnte (so überschreibt ihn kein Statuswechsel des Aufbaus, bevor er gelesen ist).
    /// </summary>
    private string? _hideSaveError;

    /// <summary>
    /// Die aktive Mod im Hauptfenster kann gewechselt haben (wird bei jeder Neuerkennung aufgerufen): Ist es ein anderer Ordner als beim
    /// letzten Aufbau, werden die Regeln der neuen Mod geladen (im Aufbau) und neu aufgebaut. Vor dem ersten Aufbau passiert nichts.
    /// </summary>
    public void ActiveModChanged()
    {
        if (_started && !string.Equals(_host.ActiveModDirectory(), _lastActiveModDirectory, StringComparison.OrdinalIgnoreCase))
        {
            StartRefresh();
        }
    }

    /// <summary>Zeile unter der Statuszeile: Schalter und Knopf.</summary>
    private Controls.StackPanel BuildHideBar()
    {
        _showHiddenBox.Content = FlagTrackerTexts.ShowHidden(0);
        _showHiddenBox.ToolTip = FlagTrackerTexts.ShowHiddenTip;
        Controls.ToolTipService.SetShowOnDisabled(_showHiddenBox, true);
        _showHiddenBox.Checked += (_, _) => RunSearchNow();
        _showHiddenBox.Unchecked += (_, _) => RunSearchNow();
        _rulesButton.Content = FlagTrackerTexts.Rules + " …";
        Controls.ToolTipService.SetShowOnDisabled(_rulesButton, true);
        _rulesButton.Click += (_, _) => ShowRulesWindow();

        var bar = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        bar.Children.Add(_showHiddenBox);
        bar.Children.Add(_rulesButton);
        UpdateHideBar();
        return bar;
    }

    /// <summary>Kontextmenü der Übersicht; die Einträge werden beim Öffnen nach Auswahl und aktiver Mod freigegeben.</summary>
    private Controls.ContextMenu BuildOverviewMenu()
    {
        var menu = new Controls.ContextMenu();
        foreach (var item in new[] { _hideItem, _hideSimilarItem, _unhideItem })
        {
            Controls.ToolTipService.SetShowOnDisabled(item, true);
            menu.Items.Add(item);
        }

        _hideItem.Click += (_, _) => HideSelected();
        _hideSimilarItem.Click += (_, _) => HideSimilar();
        _unhideItem.Click += (_, _) => UnhideSelected();
        menu.Opened += (_, _) =>
        {
            var rows = SelectedOverviewRows();
            var noMod = _hide is null;
            var tip = noMod ? FlagTrackerTexts.HideNeedsActiveMod : null;
            _hideItem.IsEnabled = !noMod && rows.Any(r => !r.Hidden);
            _hideSimilarItem.IsEnabled = !noMod && rows.Count > 0;
            _unhideItem.IsEnabled = !noMod && rows.Any(r => r.Hidden);
            _hideItem.ToolTip = tip;
            _hideSimilarItem.ToolTip = tip;
            _unhideItem.ToolTip = tip;
        };
        return menu;
    }

    /// <summary>
    /// Lädt die Regeln neu, wenn sich die aktive Mod oder die Groß/Klein-Regel des Spiels geändert hat (vor jedem Aufbau im UI-Thread).
    /// Die Datei ist klein; Lesefehler ergeben eine leere Liste.
    /// </summary>
    private void ReloadHideList(string? activeModDirectory, bool ignoreCase)
    {
        if (_hide is not null && string.Equals(activeModDirectory, _hideModDirectory, StringComparison.OrdinalIgnoreCase))
        {
            // Gleiche Mod, nur andere Groß/Klein-Regel (z. B. andere ScriptDocs-Tabelle): Regeln behalten, Vergleicher neu bauen.
            _hideIgnoreCase = ignoreCase;
            _hide.SetIgnoreCase(ignoreCase);
            return;
        }

        _hideModDirectory = activeModDirectory;
        _hideIgnoreCase = ignoreCase;
        _hide = activeModDirectory is null ? null : FlagHideList.Load(HideDirectory, activeModDirectory, ignoreCase);
        UpdateHideBar();
    }

    private string HideDirectory => Path.Combine(_host.GameDirectory, "flag-tracker", "hidden");

    /// <summary>Schalter und Knopf: gesperrt ohne aktive Mod; der Schalter wirkt nur bei leerem Suchfeld. n = ausgeblendete Warnungen.</summary>
    private void UpdateHideBar()
    {
        var noMod = _hide is null;
        _showHiddenBox.Content = FlagTrackerTexts.ShowHidden(_session.HiddenWarningCount(_hide));
        _showHiddenBox.IsEnabled = !noMod && string.IsNullOrWhiteSpace(_searchBox.Text);
        _showHiddenBox.ToolTip = noMod ? FlagTrackerTexts.HideNeedsActiveMod : FlagTrackerTexts.ShowHiddenTip;
        _rulesButton.IsEnabled = !noMod;
        _rulesButton.ToolTip = noMod ? FlagTrackerTexts.HideNeedsActiveMod : null;
    }

    private bool ShowHiddenChecked => _showHiddenBox.IsChecked == true;

    private List<OverviewRow> SelectedOverviewRows() => _overviewList.SelectedItems.OfType<OverviewRow>().ToList();

    private void HideSelected()
    {
        if (_hide is not { } hide)
        {
            return;
        }

        var changed = false;
        foreach (var row in SelectedOverviewRows().Where(r => !r.Hidden))
        {
            changed |= hide.Add(row.Name);
        }

        if (changed)
        {
            ApplyHideChange();
        }
    }

    /// <summary>Fragt ein Muster ab, vorbelegt aus dem ersten gewählten Namen (<see cref="FlagHideList.SuggestPattern"/>).</summary>
    private void HideSimilar()
    {
        if (_hide is not { } hide || SelectedOverviewRows().FirstOrDefault() is not { } first)
        {
            return;
        }

        var rule = TextPromptWindow.Ask(this, FlagTrackerTexts.HideSimilar, FlagTrackerTexts.HideSimilarPrompt, FlagHideList.SuggestPattern(first.Name));
        if (rule is not null && hide.Add(rule))
        {
            ApplyHideChange();
        }
    }

    /// <summary>Entfernt jede Regel, die auf einen der gewählten Namen passt (auch Muster, die weitere Namen ausblenden).</summary>
    private void UnhideSelected()
    {
        if (_hide is not { } hide)
        {
            return;
        }

        var removed = 0;
        foreach (var row in SelectedOverviewRows())
        {
            removed += hide.RemoveMatching(row.Name);
        }

        if (removed > 0)
        {
            ApplyHideChange();
        }
    }

    private void ShowRulesWindow()
    {
        if (_hide is not { } hide || _hideModDirectory is not { } mod)
        {
            return;
        }

        new FlagHideRulesWindow(hide, Path.GetFileName(mod.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), ApplyHideChange) { Owner = this }.ShowDialog();
    }

    /// <summary>
    /// Nach jeder Änderung der Regeln: speichern (Fehler in die Statuszeile), neu suchen und die Auswahl erhalten. Ist der erste gewählte
    /// Name verschwunden, wird die Zeile an seiner alten Stelle gewählt, damit die Liste nicht nach oben springt.
    /// </summary>
    private void ApplyHideChange()
    {
        try
        {
            _hide?.Save();
            _hideSaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _hideSaveError = FlagTrackerTexts.SaveFailed(ex.Message);
        }

        UpdateHeader();

        var selected = SelectedOverviewRows().Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        var oldIndex = _overviewList.SelectedIndex;
        RunSearchNow();
        // OverviewRow ist ein record: gleich gebliebene Zeilen können nach dem Tausch der Liste schon gewählt sein.
        _overviewList.UnselectAll();

        var rows = _view.Overview;
        if (rows.Count == 0)
        {
            return;
        }

        var keep = rows.Where(r => selected.Contains(r.Name)).ToList();
        if (keep.Count == 0 && oldIndex >= 0)
        {
            keep.Add(rows[Math.Min(oldIndex, rows.Count - 1)]);
        }

        // Höchstens die ersten 200 wieder wählen: SelectedItems.Add ist je Zeile teuer, und mehr braucht die Bedienung nicht.
        if (keep.Count > MaxRestoredSelection)
        {
            keep.RemoveRange(MaxRestoredSelection, keep.Count - MaxRestoredSelection);
        }

        foreach (var row in keep)
        {
            _overviewList.SelectedItems.Add(row);
        }

        if (keep.Count > 0)
        {
            var first = keep[0];
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => _overviewList.ScrollIntoView(first));
        }
    }
}
