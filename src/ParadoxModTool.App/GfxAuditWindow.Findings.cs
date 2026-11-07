using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ParadoxModTool.Explorer;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.GfxAudit;
using ParadoxModTool.ScriptCheck;
using Binding = System.Windows.Data.Binding;
using Clipboard = System.Windows.Clipboard;
using Controls = System.Windows.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Befundliste: Knöpfe je Art mit Anzahl, "Ausgeblendete zeigen" und die virtualisierte Tabelle aus <see cref="GfxAuditView.Rows"/>. Doppelklick
/// oder Enter öffnet die Stelle, bei ungenutzten Bildern zeigt er die Datei im Explorer; Ausblenden gilt je Mod. Ein Speicherfehler der
/// Ausblende-Liste landet in der Statuszeile, die Wahl bleibt bis zum Schließen im Speicher. "Auslagern…" verschiebt gewählte ungenutzte Bilder
/// lokaler Mods ins Archiv (<see cref="GfxArchive"/>), "Archiv…" öffnet <see cref="GfxArchiveWindow"/> zum Zurückholen.
/// </summary>
internal sealed partial class GfxAuditWindow
{
    private readonly Controls.DataGrid _findingsGrid = new()
    {
        AutoGenerateColumns = false,
        IsReadOnly = true,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        SelectionMode = Controls.DataGridSelectionMode.Extended,
        SelectionUnit = Controls.DataGridSelectionUnit.FullRow,
        HeadersVisibility = Controls.DataGridHeadersVisibility.Column,
        GridLinesVisibility = Controls.DataGridGridLinesVisibility.None,
    };

    private readonly Controls.CheckBox _showHiddenBox = new() { Content = GfxAuditTexts.ShowHidden, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 0) };
    private readonly Controls.WrapPanel _kindButtons = new() { Margin = new Thickness(0, 6, 0, 6) };
    private readonly Controls.MenuItem _hideItem = new() { Header = GfxAuditTexts.Hide };
    private readonly Controls.MenuItem _unhideItem = new() { Header = GfxAuditTexts.Unhide };
    private readonly Controls.MenuItem _moveOutItem = new() { Header = GfxAuditTexts.MoveOut };

    /// <summary>Läuft gerade ein Auslagern im Hintergrund.</summary>
    private bool _moving;

    /// <summary>Das Fenster ist geschlossen: Ein noch laufendes Auslagern baut danach nicht mehr auf.</summary>
    private bool _closed;

    /// <summary>Ergebnis des letzten Auslagerns für die Statuszeile, gilt für <see cref="_moveStatusMod"/>.</summary>
    private string? _moveStatus;

    /// <summary>Nicht verschobene Dateien des letzten Auslagerns (Tooltip der Statuszeile); null ohne Fehler.</summary>
    private string? _moveTip;

    private string? _moveStatusMod;

    /// <summary>Gewählte Art; null = alle.</summary>
    private GfxFindingKind? _kindFilter;

    private ScriptCheckHideList? _hide;

    /// <summary>Für diese Mod ist <see cref="_hide"/> geladen.</summary>
    private string? _hideMod;

    private string? _hideSaveError;

    private Controls.DockPanel BuildFindingsPanel()
    {
        ReloadHideList();
        _findingsGrid.Columns.Add(new Controls.DataGridTextColumn
        {
            Header = GfxAuditTexts.ColKind,
            SortMemberPath = nameof(GfxFindingRow.Finding) + "." + nameof(GfxFinding.Severity),
            Width = 220,
            Binding = new MultiBinding
            {
                StringFormat = "{0}: {1}",
                Bindings = { new Binding(nameof(GfxFindingRow.SeverityText)), new Binding(nameof(GfxFindingRow.KindText)) },
            },
        });
        _findingsGrid.Columns.Add(new Controls.DataGridTextColumn { Header = GfxAuditTexts.ColName, Width = 280, Binding = new Binding(nameof(GfxFindingRow.Name)) });
        _findingsGrid.Columns.Add(new Controls.DataGridTextColumn { Header = GfxAuditTexts.ColPlace, Width = 300, Binding = new Binding(nameof(GfxFindingRow.Place)) });
        _findingsGrid.Columns.Add(new Controls.DataGridTextColumn { Header = GfxAuditTexts.ColRelated, Width = new Controls.DataGridLength(1, Controls.DataGridLengthUnitType.Star), Binding = new Binding(nameof(GfxFindingRow.Related)) });

        var rowStyle = new Style(typeof(Controls.DataGridRow), TryFindResource(typeof(Controls.DataGridRow)) as Style);
        var severityPath = nameof(GfxFindingRow.Finding) + "." + nameof(GfxFinding.Severity);

        // Die Farbe gehört in den Zellstil: Der Zellstil des Themes setzt die Textfarbe ausdrücklich und würde eine Farbe der Zeile überstimmen.
        // Ausgewählte Zellen behalten die Farbe der Auswahl.
        var cellStyle = new Style(typeof(Controls.DataGridCell), TryFindResource(typeof(Controls.DataGridCell)) as Style);
        void AddSeverityColour(FindingSeverity severity, string brush)
        {
            var trigger = new MultiDataTrigger();
            trigger.Conditions.Add(new Condition(new Binding(severityPath), severity));
            trigger.Conditions.Add(new Condition(new Binding(nameof(Controls.DataGridCell.IsSelected)) { RelativeSource = RelativeSource.Self }, false));
            trigger.Setters.Add(new Setter(Controls.Control.ForegroundProperty, new DynamicResourceExtension(brush)));
            cellStyle.Triggers.Add(trigger);
        }

        AddSeverityColour(FindingSeverity.Error, "Theme.Danger");
        AddSeverityColour(FindingSeverity.Warning, "Theme.Warning");
        AddSeverityColour(FindingSeverity.Hint, "Theme.TextMuted");
        _findingsGrid.CellStyle = cellStyle;
        var hidden = new DataTrigger { Binding = new Binding(nameof(GfxFindingRow.IsHidden)), Value = true };
        hidden.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55));
        rowStyle.Triggers.Add(hidden);
        _findingsGrid.RowStyle = rowStyle;

        Controls.VirtualizingPanel.SetIsVirtualizing(_findingsGrid, true);
        Controls.VirtualizingPanel.SetVirtualizationMode(_findingsGrid, Controls.VirtualizationMode.Recycling);
        _findingsGrid.MouseDoubleClick += OnFindingsDoubleClick;
        _findingsGrid.PreviewKeyDown += OnFindingsKeyDown;
        _findingsGrid.ContextMenu = BuildFindingsMenu();
        _findingsGrid.SelectionChanged += (_, _) => UpdateMoveButtons(_session.State == GfxAuditState.Building);
        _showHiddenBox.Click += (_, _) => ShowFindings(rebuildFilters: true);

        var panel = new Controls.DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        Controls.DockPanel.SetDock(_showHiddenBox, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_kindButtons, Controls.Dock.Top);
        panel.Children.Add(_showHiddenBox);
        panel.Children.Add(_kindButtons);
        panel.Children.Add(_findingsGrid);
        return panel;
    }

    private Controls.ContextMenu BuildFindingsMenu()
    {
        var open = new Controls.MenuItem { Header = GfxAuditTexts.Open };
        open.Click += (_, _) => OpenSelectedFinding();
        var explorer = new Controls.MenuItem { Header = GfxAuditTexts.ShowInExplorer };
        explorer.Click += (_, _) =>
        {
            if (_findingsGrid.SelectedItem is GfxFindingRow row)
            {
                ShowInExplorer(row.Finding.Location.FullPath);
            }
        };
        var copy = new Controls.MenuItem { Header = GfxAuditTexts.CopyName };
        copy.Click += (_, _) =>
        {
            if (_findingsGrid.SelectedItem is GfxFindingRow row)
            {
                try
                {
                    Clipboard.SetText(row.Name);
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    // Die Zwischenablage ist gerade belegt: Der Nutzer versucht es einfach noch einmal.
                }
            }
        };
        _hideItem.Click += (_, _) => SetSelectedHidden(true);
        _unhideItem.Click += (_, _) => SetSelectedHidden(false);
        _moveOutItem.Click += (_, _) => MoveOutSelected();

        var menu = new Controls.ContextMenu();
        menu.Items.Add(open);
        menu.Items.Add(explorer);
        menu.Items.Add(copy);
        menu.Items.Add(new Controls.Separator());
        menu.Items.Add(_hideItem);
        menu.Items.Add(_unhideItem);
        menu.Items.Add(new Controls.Separator());
        menu.Items.Add(_moveOutItem);
        menu.Opened += (_, _) =>
        {
            var selected = _findingsGrid.SelectedItems.OfType<GfxFindingRow>().ToList();
            _hideItem.IsEnabled = _hide is not null && selected.Any(r => !r.IsHidden);
            _unhideItem.IsEnabled = _hide is not null && selected.Any(r => r.IsHidden);
            _moveOutItem.IsEnabled = CanMoveOut(_session.State == GfxAuditState.Building);
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

        // Das Ergebnis des letzten Auslagerns gehört zur vorigen Mod.
        if (!string.Equals(_chosenMod, _moveStatusMod, StringComparison.OrdinalIgnoreCase))
        {
            _moveStatus = null;
            _moveTip = null;
            _moveStatusMod = null;
        }

        _hide = _chosenMod is null ? null : ScriptCheckHideList.Load(Path.Combine(_host.GameDirectory, "gfx-audit", "hidden"), _chosenMod);
        if (_hide is { ReadFailed: true })
        {
            _hideSaveError = GfxAuditTexts.HideListUnreadable;
        }
    }

    /// <summary>Füllt die Liste neu; <paramref name="rebuildFilters"/> auch die Knöpfe je Art (ihre Zahlen ändern sich nicht beim Suchen).</summary>
    private void ShowFindings(bool rebuildFilters)
    {
        _searchTimer?.Stop();
        // Nach einem Mod-Wechsel gehört das Ergebnis der Sitzung noch zur alten Mod: keine Zeilen statt falscher Funde.
        // Ohne Spielordner (kein Vanilla) wären die Funde falsch; die Statuszeile nennt den Grund.
        var findings = ResultIsForChosenMod() && GfxAuditView.HasVanilla(_session.Index) ? _session.Result?.Findings ?? Array.Empty<GfxFinding>() : Array.Empty<GfxFinding>();
        var showHidden = _showHiddenBox.IsChecked == true;
        if (rebuildFilters)
        {
            RebuildKindButtons(findings, showHidden);
        }

        _findingsGrid.ItemsSource = GfxAuditView.Rows(findings, _hide, showHidden, _kindFilter, _search.Text);
    }

    private void RebuildKindButtons(IReadOnlyList<GfxFinding> findings, bool showHidden)
    {
        var filters = GfxAuditView.KindFilters(findings, _hide, showHidden);
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

        var selectedIndex = _findingsGrid.SelectedIndex;
        foreach (var row in _findingsGrid.SelectedItems.OfType<GfxFindingRow>().ToList())
        {
            if (hidden)
            {
                _hide.Add(row.Finding.Key);
            }
            else
            {
                _hide.Remove(row.Finding.Key);
            }
        }

        try
        {
            _hide.Save();
            _hideSaveError = null;
        }
        catch (InvalidOperationException)
        {
            _hideSaveError = GfxAuditTexts.HideListUnreadable;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _hideSaveError = GfxAuditTexts.HideSaveFailed(ex.Message);
        }

        _status.Text = StatusText();
        ShowFindings(rebuildFilters: true);

        // Die Auswahl bleibt an der Stelle: Wer mehrere ausblendet, muss nicht jedes Mal neu klicken.
        if (_findingsGrid.Items.Count > 0)
        {
            _findingsGrid.SelectedIndex = Math.Min(selectedIndex, _findingsGrid.Items.Count - 1);
        }
    }

    /// <summary>Die gewählte Mod liegt im Mod-Ordner des Spiels (nur dann gibt es Auslagern und Archiv).</summary>
    private bool ChosenModIsLocal() => _chosenMod is not null && GfxArchive.IsLocalMod(_chosenMod, _host.ModRoot);

    /// <summary>Warum gerade nicht ausgelagert werden kann; null, wenn es geht.</summary>
    private string? MoveOutBlock(bool building) => GfxAuditView.MoveOutBlock(building, ChosenModIsLocal(), _session.Index, ResultIsForChosenMod());

    /// <summary>Pfade (relativ zur Mod) der gewählten ungenutzten Bilder, in der Reihenfolge der Tabelle.</summary>
    private List<string> SelectedImagePaths()
    {
        var selected = _findingsGrid.SelectedItems.OfType<GfxFindingRow>().Where(r => r.IsImage).ToHashSet();
        return selected.Count == 0
            ? new List<string>()
            : _findingsGrid.Items.OfType<GfxFindingRow>().Where(selected.Contains).Select(r => r.Finding.Location.RelativePath).ToList();
    }

    private bool CanMoveOut(bool building) => !_moving && MoveOutBlock(building) is null && _findingsGrid.SelectedItems.OfType<GfxFindingRow>().Any(r => r.IsImage);

    /// <summary>Knöpfe "Auslagern…" und "Archiv…": Ein gesperrtes Auslagern nennt den Grund im Tooltip.</summary>
    private void UpdateMoveButtons(bool building)
    {
        var block = MoveOutBlock(building);
        _moveOutButton.IsEnabled = CanMoveOut(building);
        _moveOutButton.ToolTip = block ?? GfxAuditTexts.MoveOutTip;
        _archiveButton.IsEnabled = !_moving && !building && ChosenModIsLocal();
        _refreshButton.IsEnabled = !_moving;
    }

    /// <summary>
    /// Grund der Sperre für die Statuszeile: nur wenn das Ergebnis der gewählten Mod ungenutzte Bilder hat und kein Aufbau läuft (der zeigt
    /// schon seinen Fortschritt); sonst null.
    /// </summary>
    private string? MoveBlockForStatus() =>
        _session.State != GfxAuditState.Building && ResultIsForChosenMod() && _session.Result is { } result && result.Findings.Any(f => f.Kind == GfxFindingKind.UnusedImage)
            ? MoveOutBlock(building: false)
            : null;

    /// <summary>Text des letzten Auslagerns, solange dieselbe Mod gewählt ist.</summary>
    private string? MoveStatusForChosenMod() =>
        _moveStatus is not null && string.Equals(_moveStatusMod, _chosenMod, StringComparison.OrdinalIgnoreCase) ? _moveStatus : null;

    /// <summary>Verschiebt die gewählten ungenutzten Bilder nach Rückfrage ins Archiv und prüft danach neu.</summary>
    private async void MoveOutSelected()
    {
        var building = _session.State == GfxAuditState.Building;
        if (_moving || _chosenMod is not { } mod)
        {
            return;
        }

        if (MoveOutBlock(building) is { } block)
        {
            _status.Text = block;
            return;
        }

        var paths = SelectedImagePaths();
        if (paths.Count == 0)
        {
            _status.Text = GfxAuditTexts.MoveNoImages;
            return;
        }

        var archive = new GfxArchive(mod);
        var answer = MessageBox.Show(
            this,
            GfxAuditTexts.MoveConfirm(paths.Count, archive.ArchiveDirectory, GfxAuditView.ConfirmList(paths)),
            GfxAuditTexts.MoveConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        _moving = true;
        UpdateMoveButtons(building);
        try
        {
            var result = await Task.Run(() => archive.MoveOut(paths));
            _moveStatus = GfxAuditTexts.MoveDone(result.Moved.Count, result.Failed.Count);
            _moveTip = GfxAuditView.FailureTip(result.Failed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _moveStatus = ex.Message;
            _moveTip = null;
        }
        finally
        {
            _moving = false;
        }

        _moveStatusMod = mod;
        if (!string.Equals(mod, _chosenMod, StringComparison.OrdinalIgnoreCase))
        {
            // Während des Verschiebens wurde eine andere Mod gewählt: Das Ergebnis gehört nicht mehr in die Statuszeile.
            _moveStatus = null;
            _moveTip = null;
            _moveStatusMod = null;
        }

        if (_closed)
        {
            return;
        }

        UpdateHeader();
        StartRefresh();
    }

    /// <summary>Öffnet das Archiv der gewählten Mod; hat das Zurückholen etwas verschoben, wird neu geprüft.</summary>
    private void OpenArchive()
    {
        if (_moving || _chosenMod is not { } mod || !ChosenModIsLocal() || _session.State == GfxAuditState.Building)
        {
            return;
        }

        var name = (_modBox.SelectedItem as ModChoice)?.Name ?? Path.GetFileName(mod.TrimEnd('\\', '/'));
        var window = new GfxArchiveWindow(new GfxArchive(mod), name, this);
        window.ShowDialog();
        if (window.Changed)
        {
            StartRefresh();
        }
    }

    /// <summary>Bild: Datei im Explorer zeigen; alle anderen Funde: Stelle im Editor öffnen.</summary>
    private void OpenSelectedFinding()
    {
        if (_findingsGrid.SelectedItem is not GfxFindingRow row)
        {
            return;
        }

        if (row.IsImage)
        {
            ShowInExplorer(row.Finding.Location.FullPath);
        }
        else
        {
            OpenLocation(row.Finding.Location);
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
            _status.Text = GfxAuditTexts.OpenFailed(ex.Message);
        }
    }

    /// <summary>Zeigt die Datei im Explorer; fehlt sie, öffnet sich ihr Ordner, und fehlt auch der, steht ein Hinweis in der Statuszeile.</summary>
    private void ShowInExplorer(string path)
    {
        var windowsPath = PathNormalization.ToWindowsPath(path);
        try
        {
            if (File.Exists(windowsPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{windowsPath}\"") { UseShellExecute = true });
            }
            else if (Path.GetDirectoryName(windowsPath) is { Length: > 0 } folder && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
            }
            else
            {
                _status.Text = GfxAuditTexts.FileGone(windowsPath);
            }
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        {
            _status.Text = GfxAuditTexts.OpenFailed(ex.Message);
        }
    }

    private void OnFindingsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && Controls.ItemsControl.ContainerFromElement(_findingsGrid, source) is Controls.DataGridRow)
        {
            OpenSelectedFinding();
        }
    }

    private void OnFindingsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            OpenSelectedFinding();
            e.Handled = true;
        }
    }
}
