using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.ScriptCheck;
using Binding = System.Windows.Data.Binding;
using Clipboard = System.Windows.Clipboard;
using CheckFinding = ParadoxModTool.ScriptCheck.Finding;
using CheckRow = ParadoxModTool.ScriptCheck.FindingRow;
using Controls = System.Windows.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ParadoxModTool.App;

/// <summary>
/// Reiter "Befunde": Knöpfe je Art mit Anzahl, Suchfeld, "Ausgeblendete zeigen", "Als Bericht kopieren" und die virtualisierte Liste aus <see cref="ScriptCheckView.Rows"/>.
/// Doppelklick oder Enter öffnet die Stelle; Ausblenden gilt je Mod. Ein Speicherfehler der Ausblende-Liste landet in der Statuszeile, die Wahl bleibt
/// bis zum Schließen im Speicher.
/// </summary>
internal sealed partial class ScriptCheckWindow
{
    private readonly Controls.ListView _findingsList = new() { SelectionMode = Controls.SelectionMode.Extended };
    private readonly Controls.TextBox _findingsSearch = new() { MinWidth = 260, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = ScriptCheckTexts.SearchFindingsTip };
    private readonly Controls.CheckBox _showHiddenBox = new() { Content = ScriptCheckTexts.ShowHidden, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
    private readonly Controls.Button _copyReportButton = new() { Content = ScriptCheckTexts.CopyReport, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(12, 0, 0, 0), IsEnabled = false };
    private readonly Controls.WrapPanel _kindButtons = new() { Margin = new Thickness(0, 6, 0, 6) };
    private readonly Controls.MenuItem _hideItem = new() { Header = ScriptCheckTexts.Hide };
    private readonly Controls.MenuItem _unhideItem = new() { Header = ScriptCheckTexts.Unhide };
    private DispatcherTimer? _findingsTimer;

    /// <summary>Gewählte Art; null = alle.</summary>
    private FindingKind? _kindFilter;

    private ScriptCheckHideList? _hide;

    /// <summary>Für diese Mod ist <see cref="_hide"/> geladen.</summary>
    private string? _hideMod;

    private string? _hideSaveError;

    private Controls.DockPanel BuildFindingsTab()
    {
        ReloadHideList();
        ConfigureFindingsList(_findingsList, ScriptCheckTexts.ColRelated);
        _findingsList.ContextMenu = BuildFindingsMenu();
        _copyReportButton.Click += (_, _) => CopyReport(_findingsList, (_modBox.SelectedItem as ModChoice)?.Name ?? string.Empty);

        _findingsTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Input, (_, _) => ShowFindings(rebuildFilters: false), Dispatcher);
        _findingsTimer.Stop();
        _findingsSearch.TextChanged += (_, _) =>
        {
            _findingsTimer.Stop();
            _findingsTimer.Start();
        };
        _findingsSearch.KeyDown += OnFindingsSearchKeyDown;
        _showHiddenBox.Click += (_, _) => ShowFindings(rebuildFilters: true);

        var top = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        top.Children.Add(_findingsSearch);
        top.Children.Add(_showHiddenBox);
        top.Children.Add(_copyReportButton);

        var panel = new Controls.DockPanel();
        Controls.DockPanel.SetDock(top, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_kindButtons, Controls.Dock.Top);
        panel.Children.Add(top);
        panel.Children.Add(_kindButtons);
        panel.Children.Add(_findingsList);
        return panel;
    }

    /// <summary>
    /// Spalten, Zeilenstil (Farbe nach Schwere, Ausgeblendete blass, Vorschlag als Tooltip), Virtualisierung und Öffnen per Doppelklick oder Enter
    /// für eine Liste von <see cref="CheckRow"/>; <paramref name="relatedHeader"/> ist die Überschrift der letzten Spalte. Je Liste eine eigene GridView.
    /// </summary>
    private void ConfigureFindingsList(Controls.ListView list, string relatedHeader)
    {
        var grid = new Controls.GridView();
        grid.Columns.Add(Column(ScriptCheckTexts.ColSeverity, 80, nameof(CheckRow.SeverityText)));
        grid.Columns.Add(Column(ScriptCheckTexts.ColKind, 150, nameof(CheckRow.KindText)));
        grid.Columns.Add(Column(ScriptCheckTexts.ColName, 280, nameof(CheckRow.Name)));
        grid.Columns.Add(Column(ScriptCheckTexts.ColPlace, 300, nameof(CheckRow.Place)));
        grid.Columns.Add(Column(relatedHeader, 280, nameof(CheckRow.Related)));
        list.View = grid;

        var rowStyle = new Style(typeof(Controls.ListViewItem), TryFindResource(typeof(Controls.ListViewItem)) as Style);
        // Ohne Vorschlag (null) zeigt die Zeile keinen Tooltip.
        rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(nameof(CheckRow.Suggestion))));
        var error = new DataTrigger { Binding = new Binding(nameof(CheckRow.Finding) + "." + nameof(CheckFinding.Severity)), Value = FindingSeverity.Error };
        error.Setters.Add(new Setter(Controls.Control.ForegroundProperty, new DynamicResourceExtension("Theme.Danger")));
        rowStyle.Triggers.Add(error);
        var hint = new DataTrigger { Binding = new Binding(nameof(CheckRow.Finding) + "." + nameof(CheckFinding.Severity)), Value = FindingSeverity.Hint };
        hint.Setters.Add(new Setter(Controls.Control.ForegroundProperty, new DynamicResourceExtension("Theme.TextMuted")));
        rowStyle.Triggers.Add(hint);
        var hidden = new DataTrigger { Binding = new Binding(nameof(CheckRow.IsHidden)), Value = true };
        hidden.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55));
        rowStyle.Triggers.Add(hidden);
        list.ItemContainerStyle = rowStyle;
        Controls.VirtualizingPanel.SetIsVirtualizing(list, true);
        Controls.VirtualizingPanel.SetVirtualizationMode(list, Controls.VirtualizationMode.Recycling);
        list.MouseDoubleClick += OnFindingsDoubleClick;
        list.KeyDown += OnFindingsKeyDown;
    }

    private static Controls.GridViewColumn Column(string header, double width, string path) =>
        new() { Header = header, Width = width, DisplayMemberBinding = new Binding(path) };

    /// <summary>Text in die Zwischenablage; ist sie gerade belegt, passiert nichts (der Nutzer versucht es einfach noch einmal).</summary>
    private static void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Die Zwischenablage ist gerade belegt.
        }
    }

    /// <summary>Kopiert <see cref="PerformanceTexts.Report"/> der gerade gezeigten Zeilen der Art <see cref="FindingKind.ExpensiveIterator"/>.</summary>
    private void CopyReport(Controls.ListView list, string targetName)
    {
        var findings = ReportFindings(list);
        if (findings.Count > 0)
        {
            CopyToClipboard(PerformanceTexts.Report(findings, targetName, _host.Profile.DisplayName));
        }
    }

    /// <summary>Funde der gezeigten Zeilen (nach Suche, Art und Ausblenden) der Art <see cref="FindingKind.ExpensiveIterator"/>, in Listenreihenfolge.</summary>
    private static IReadOnlyList<CheckFinding> ReportFindings(Controls.ListView list) =>
        (list.ItemsSource as IEnumerable<CheckRow> ?? Array.Empty<CheckRow>())
            .Select(r => r.Finding)
            .Where(f => f.Kind == FindingKind.ExpensiveIterator && f.Performance is not null)
            .ToList();

    /// <summary>
    /// Blendet die gewählten Zeilen von <paramref name="list"/> in <paramref name="hide"/> aus oder ein und speichert die Liste. Rückgabe: Fehlertext für
    /// die Statuszeile, null bei Erfolg.
    /// </summary>
    private static string? ApplyHidden(Controls.ListView list, ScriptCheckHideList hide, bool hidden)
    {
        foreach (var row in list.SelectedItems.OfType<CheckRow>().ToList())
        {
            if (hidden)
            {
                hide.Add(row.Finding.Key);
            }
            else
            {
                hide.Remove(row.Finding.Key);
            }
        }

        try
        {
            hide.Save();
            return null;
        }
        catch (InvalidOperationException)
        {
            return ScriptCheckTexts.HideListUnreadable;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ScriptCheckTexts.HideSaveFailed(ex.Message);
        }
    }

    private Controls.ContextMenu BuildFindingsMenu()
    {
        var open = new Controls.MenuItem { Header = ScriptCheckTexts.OpenPlace };
        open.Click += (_, _) => OpenSelectedFinding(_findingsList);
        var copy = new Controls.MenuItem { Header = ScriptCheckTexts.CopyName };
        copy.Click += (_, _) =>
        {
            if (_findingsList.SelectedItem is CheckRow row)
            {
                CopyToClipboard(row.Name);
            }
        };
        var calls = new Controls.MenuItem { Header = ScriptCheckTexts.ShowCalls };
        calls.Click += (_, _) =>
        {
            if (_findingsList.SelectedItem is CheckRow row)
            {
                ShowCallsOf(ScriptCheckView.CallsName(row.Finding));
            }
        };
        _hideItem.Click += (_, _) => SetSelectedHidden(true);
        _unhideItem.Click += (_, _) => SetSelectedHidden(false);

        var menu = new Controls.ContextMenu();
        menu.Items.Add(open);
        menu.Items.Add(copy);
        menu.Items.Add(calls);
        menu.Items.Add(new Controls.Separator());
        menu.Items.Add(_hideItem);
        menu.Items.Add(_unhideItem);
        menu.Opened += (_, _) =>
        {
            var selected = _findingsList.SelectedItems.OfType<CheckRow>().ToList();
            _hideItem.IsEnabled = _hide is not null && selected.Any(r => !r.IsHidden);
            _unhideItem.IsEnabled = _hide is not null && selected.Any(r => r.IsHidden);
        };
        return menu;
    }

    /// <summary>Lädt die Ausblende-Liste der gewählten Mod; ohne Mod gibt es keine.</summary>
    private void ReloadHideList()
    {
        if (string.Equals(_chosenMod, _hideMod, StringComparison.OrdinalIgnoreCase) && _hide is not null)
        {
            return;
        }

        _hideMod = _chosenMod;
        _hideSaveError = null;
        _hide = _chosenMod is null ? null : ScriptCheckHideList.Load(Path.Combine(_host.GameDirectory, "script-check", "hidden"), _chosenMod);
        if (_hide is { ReadFailed: true })
        {
            _hideSaveError = ScriptCheckTexts.HideListUnreadable;
        }
    }

    /// <summary>Füllt die Liste neu; <paramref name="rebuildFilters"/> auch die Knöpfe je Art (ihre Zahlen ändern sich nicht beim Suchen).</summary>
    private void ShowFindings(bool rebuildFilters)
    {
        _findingsTimer?.Stop();
        var findings = UsableIndex is null ? Array.Empty<CheckFinding>() : _session.Result?.Findings ?? Array.Empty<CheckFinding>();
        var showHidden = _showHiddenBox.IsChecked == true;
        if (rebuildFilters)
        {
            RebuildKindButtons(findings, showHidden);
        }

        _findingsList.ItemsSource = ScriptCheckView.Rows(findings, _hide, showHidden, _kindFilter, _findingsSearch.Text);
        _copyReportButton.IsEnabled = ReportFindings(_findingsList).Count > 0;
    }

    private void RebuildKindButtons(IReadOnlyList<CheckFinding> findings, bool showHidden)
    {
        var filters = ScriptCheckView.KindFilters(findings, _hide, showHidden);
        if (_kindFilter is not null && !filters.Any(f => f.Kind == _kindFilter))
        {
            _kindFilter = null;
        }

        _kindButtons.Children.Clear();
        foreach (var (kind, text, count) in filters)
        {
            var button = new Controls.Primitives.ToggleButton
            {
                Content = text + " (" + count + ")",
                IsChecked = kind == _kindFilter,
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 0),
            };
            button.Click += (_, _) =>
            {
                _kindFilter = kind;
                foreach (var other in _kindButtons.Children.OfType<Controls.Primitives.ToggleButton>())
                {
                    other.IsChecked = ReferenceEquals(other, button);
                }

                ShowFindings(rebuildFilters: false);
            };
            _kindButtons.Children.Add(button);
        }
    }

    private void SetSelectedHidden(bool hidden)
    {
        if (_hide is null)
        {
            return;
        }

        var selectedIndex = _findingsList.SelectedIndex;
        _hideSaveError = ApplyHidden(_findingsList, _hide, hidden);
        _status.Text = StatusText();
        ShowFindings(rebuildFilters: true);

        // Die Auswahl bleibt an der Stelle: Wer mehrere ausblendet, muss nicht jedes Mal neu klicken.
        _findingsList.SelectedIndex = Math.Min(selectedIndex, _findingsList.Items.Count - 1);
    }

    private void OpenSelectedFinding(Controls.ListView list)
    {
        if (list.SelectedItem is CheckRow row)
        {
            OpenLocation(row.Finding.Location);
        }
    }

    /// <summary>Doppelklick auf eine Zeile einer Fundliste (Befunde oder fremde Ziele) öffnet ihre Stelle.</summary>
    private void OnFindingsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Controls.ListView list && e.OriginalSource is DependencyObject source && Controls.ItemsControl.ContainerFromElement(list, source) is Controls.ListViewItem)
        {
            OpenSelectedFinding(list);
        }
    }

    private void OnFindingsKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is Controls.ListView list && e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            OpenSelectedFinding(list);
            e.Handled = true;
        }
    }

    /// <summary>Enter im Suchfeld (Befunde oder fremde Ziele) sucht sofort, ohne die Wartezeit nach dem Tippen.</summary>
    private void OnFindingsSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (ReferenceEquals(sender, _perfSearch))
            {
                ShowPerformance();
            }
            else
            {
                ShowFindings(rebuildFilters: false);
            }

            e.Handled = true;
        }
    }
}
