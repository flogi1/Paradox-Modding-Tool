using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using ParadoxModTool.Performance;
using Binding = System.Windows.Data.Binding;
using Clipboard = System.Windows.Clipboard;
using Controls = System.Windows.Controls;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// Schritt 3 "Vergleich" (Spec, Ablauf im Fenster): Läufe A (vorher) und B (nachher) wählen, Prüfzeile aus
/// <see cref="PerformanceSession.ComparisonWarnings"/>, Rauschschwellen (gemerkt je Spiel im Sitzungszustand), Summenzeile Mod-Self A gegen B,
/// Delta-Tabelle der Mod-Seite je Zeile oder je Datei mit eingefärbter Bewertung, "Vergleich kopieren" und "Neue Messung mit B als Basis".
/// A und B kommen aus den Auswahllisten hier oder aus der Laufliste von Schritt 1 ("Vergleich A/B"); beides geht über
/// <see cref="PerformanceSession.Compare"/> und meldet sich über <see cref="PerformanceService.SessionChanged"/>.
/// </summary>
internal sealed partial class PerformanceWindow
{
    private const int CompareReportTop = 100;
    private const string Missing = "-";

    private readonly Controls.ComboBox _compareA = new() { MinWidth = 240, Margin = new Thickness(0, 0, 12, 4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.ComboBox _compareB = new() { MinWidth = 240, Margin = new Thickness(0, 0, 12, 4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.Button _newFromBButton = new() { Content = PerformanceTexts.CompareNewFromB, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 12, 4) };
    private readonly Controls.TextBox _noiseRelativeBox = new() { Width = 60, Margin = new Thickness(0, 0, 12, 4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBox _noiseFloorBox = new() { Width = 60, Margin = new Thickness(0, 0, 12, 4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBlock _compareWarnings = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) };
    private readonly Controls.TextBlock _compareEmpty = new() { Text = PerformanceTexts.CompareEmpty, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Controls.TextBlock _compareSummary = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.RadioButton _viewRows = new() { Content = PerformanceTexts.CompareViewRows, IsChecked = true, Margin = new Thickness(0, 0, 10, 4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.RadioButton _viewFiles = new() { Content = PerformanceTexts.CompareViewFiles, Margin = new Thickness(0, 0, 12, 4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBlock _fileTotalsNote = new() { Text = PerformanceTexts.FileTotalsInclusive, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 4), Visibility = Visibility.Collapsed };
    private readonly Controls.Button _copyCompareButton = new() { Content = PerformanceTexts.CompareCopy, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 12, 4) };
    private readonly Controls.ListView _compareList = new() { SelectionMode = Controls.SelectionMode.Single };
    private readonly Controls.DockPanel _compareContent = new();
    private readonly Dictionary<Controls.GridViewColumn, (CompareSort Sort, string Header)> _compareColumns = new();
    private CompareSort _compareSort = CompareSort.Library;
    private bool _compareDescending;
    private bool _fillingCompareChoices;
    private IReadOnlyList<PerformanceRun>? _compareRuns;
    private RunComparison? _shownComparison;
    private NoiseSettings? _shownNoise;

    /// <summary>Spalte, nach der die Tabelle sortiert ist; <see cref="Library"/> ist die Reihenfolge des Vergleichs (größere Gesamtzeit von A und B zuerst).</summary>
    private enum CompareSort
    {
        Library,
        Location,
        Origin,
        CallsA,
        CallsB,
        TotalA,
        TotalB,
        Delta,
        Verdict,
    }

    /// <summary>Eine Zeile der Tabelle: Anzeige-Texte in der aktuellen Kultur, daneben die Rohwerte zum Sortieren und für die Farbe der Bewertung.</summary>
    private sealed record CompareRow(
        int Order,
        string Key,
        string Origin,
        string CallsA,
        string CallsB,
        string TotalA,
        string TotalB,
        string Delta,
        string VerdictText,
        Verdict Verdict,
        RowOrigin OriginValue,
        long? CallsAValue,
        long? CallsBValue,
        double? TotalAValue,
        double? TotalBValue,
        double? DeltaValue);

    private UIElement BuildCompareTab()
    {
        _compareEmpty.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _fileTotalsNote.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");

        var choices = new Controls.WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
        choices.Children.Add(ToolLabel(PerformanceTexts.CompareLabelA));
        choices.Children.Add(_compareA);
        choices.Children.Add(ToolLabel(PerformanceTexts.CompareLabelB));
        choices.Children.Add(_compareB);
        choices.Children.Add(_newFromBButton);

        var noise = new Controls.WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
        noise.Children.Add(ToolLabel(PerformanceTexts.CompareNoiseRelative));
        noise.Children.Add(_noiseRelativeBox);
        noise.Children.Add(ToolLabel(PerformanceTexts.CompareNoiseFloor));
        noise.Children.Add(_noiseFloorBox);

        var tools = new Controls.WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        tools.Children.Add(_viewRows);
        tools.Children.Add(_viewFiles);
        tools.Children.Add(_fileTotalsNote);
        tools.Children.Add(_copyCompareButton);

        var grid = new Controls.GridView();
        grid.Columns.Add(CompareColumn(CompareSort.Location, PerformanceTexts.AnalyzeColumnLocation, nameof(CompareRow.Key), 420));
        grid.Columns.Add(CompareColumn(CompareSort.Origin, PerformanceTexts.AnalyzeColumnOrigin, nameof(CompareRow.Origin), 120));
        grid.Columns.Add(CompareColumn(CompareSort.CallsA, PerformanceTexts.CompareColumnCallsA, nameof(CompareRow.CallsA), 90));
        grid.Columns.Add(CompareColumn(CompareSort.CallsB, PerformanceTexts.CompareColumnCallsB, nameof(CompareRow.CallsB), 90));
        grid.Columns.Add(CompareColumn(CompareSort.TotalA, PerformanceTexts.CompareColumnTotalA, nameof(CompareRow.TotalA), 90));
        grid.Columns.Add(CompareColumn(CompareSort.TotalB, PerformanceTexts.CompareColumnTotalB, nameof(CompareRow.TotalB), 90));
        grid.Columns.Add(CompareColumn(CompareSort.Delta, PerformanceTexts.CompareColumnDelta, nameof(CompareRow.Delta), 70));
        grid.Columns.Add(VerdictColumn(PerformanceTexts.CompareColumnVerdict, 170));
        _compareList.View = grid;
        _compareList.AddHandler(Controls.GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnCompareHeaderClick));

        Controls.DockPanel.SetDock(_compareSummary, Controls.Dock.Top);
        Controls.DockPanel.SetDock(tools, Controls.Dock.Top);
        _compareContent.Children.Add(_compareSummary);
        _compareContent.Children.Add(tools);
        _compareContent.Children.Add(_compareList);

        var root = new Controls.DockPanel { Margin = new Thickness(6) };
        foreach (var top in new UIElement[] { choices, noise, _compareWarnings, _compareEmpty })
        {
            Controls.DockPanel.SetDock(top, Controls.Dock.Top);
            root.Children.Add(top);
        }

        root.Children.Add(_compareContent);

        _compareA.SelectionChanged += (_, _) => OnCompareChoiceChanged();
        _compareB.SelectionChanged += (_, _) => OnCompareChoiceChanged();
        _noiseRelativeBox.LostFocus += (_, _) => ApplyNoise();
        _noiseFloorBox.LostFocus += (_, _) => ApplyNoise();
        _noiseRelativeBox.KeyDown += OnNoiseKeyDown;
        _noiseFloorBox.KeyDown += OnNoiseKeyDown;
        _viewRows.Checked += (_, _) => FillCompareRowsSafe();
        _viewFiles.Checked += (_, _) => FillCompareRowsSafe();
        _copyCompareButton.Click += (_, _) => CopyComparison();
        _newFromBButton.Click += (_, _) => NewMeasurementFromB();
        _service.SessionChanged += (_, _) => UpdateCompare();

        UpdateCompareSortHeaders();
        UpdateCompare();
        return root;
    }

    private Controls.GridViewColumn CompareColumn(CompareSort sort, string header, string property, double width)
    {
        var column = new Controls.GridViewColumn { Header = header, Width = width, DisplayMemberBinding = new Binding(property) };
        _compareColumns[column] = (sort, header);
        return column;
    }

    /// <summary>Bewertung in der Farbe des Themas: besser grün, schlechter rot, unverändert gedämpft, neu, fehlt und Verhalten geändert als Hinweis.</summary>
    private Controls.GridViewColumn VerdictColumn(string header, double width)
    {
        var style = new Style(typeof(Controls.TextBlock));
        foreach (var (verdict, token) in new[]
                 {
                     (Verdict.Better, "Theme.DiffAdded"), (Verdict.Worse, "Theme.Danger"), (Verdict.Unchanged, "Theme.TextMuted"),
                     (Verdict.NewInB, "Theme.Warning"), (Verdict.MissingInB, "Theme.Warning"), (Verdict.BehaviorChanged, "Theme.Warning"),
                 })
        {
            var trigger = new DataTrigger { Binding = new Binding(nameof(CompareRow.Verdict)), Value = verdict };
            trigger.Setters.Add(new Setter(Controls.TextBlock.ForegroundProperty, new DynamicResourceExtension(token)));
            style.Triggers.Add(trigger);
        }

        var text = new FrameworkElementFactory(typeof(Controls.TextBlock));
        text.SetBinding(Controls.TextBlock.TextProperty, new Binding(nameof(CompareRow.VerdictText)));
        text.SetValue(FrameworkElement.StyleProperty, style);
        var column = new Controls.GridViewColumn { Header = header, Width = width, CellTemplate = new DataTemplate { VisualTree = text } };
        _compareColumns[column] = (CompareSort.Verdict, header);
        return column;
    }

    /// <summary>
    /// Auswahllisten, Rauschfelder, Prüfzeile und (bei geändertem Vergleich) die Tabelle aus der Sitzung. Läuft auch innerhalb eines Imports (über
    /// <see cref="PerformanceSession.Changed"/>): nichts darf hier herausfliegen, sonst würde der Dienst den Import wiederholen.
    /// </summary>
    private void UpdateCompare()
    {
        try
        {
            UpdateCompareCore();
        }
        catch (Exception ex)
        {
            _status.Text = PerformanceTexts.StatusActionFailed(ex.Message);
        }
    }

    private void UpdateCompareCore()
    {
        var session = _service.Session;
        FillCompareChoices(session);
        UpdateNoiseBoxes(session);

        var comparison = session?.Comparison;
        var warnings = session?.ComparisonWarnings ?? Array.Empty<string>();
        _compareWarnings.Inlines.Clear();
        foreach (var warning in warnings)
        {
            AddCompareCheckText(warning, "Theme.Warning", newLine: true);
        }

        if (comparison is not null && session is { A: { } runA, B: { } runB })
        {
            var first = true;
            foreach (var (text, confirmed) in ComparisonChecks(runA, runB, comparison))
            {
                if (!first)
                {
                    AddCompareCheckText(", ", "Theme.TextMuted", newLine: false);
                }

                AddCompareCheckText(text, confirmed ? "Theme.DiffAdded" : "Theme.TextMuted", newLine: first);
                first = false;
            }
        }

        _compareWarnings.Visibility = _compareWarnings.Inlines.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        _compareEmpty.Visibility = session?.A is null || session.B is null ? Visibility.Visible : Visibility.Collapsed;
        _compareContent.Visibility = comparison is null ? Visibility.Collapsed : Visibility.Visible;
        _newFromBButton.IsEnabled = session?.B is not null;
        _copyCompareButton.IsEnabled = comparison is not null;
        if (comparison is not null)
        {
            _compareSummary.Text = PerformanceTexts.CompareModSelfLine(
                Time(comparison.ModSelfA),
                Time(comparison.ModSelfB),
                DeltaText(comparison.ModSelfDeltaRatio),
                VerdictText(comparison.ModSelfVerdict, comparison.SameMonths));
        }

        // Umbenennen, Auswahl in Schritt 2 und Importe lassen den Vergleich gleich: dann bleiben Tabelle, Auswahl und Bildlauf stehen.
        if (!ReferenceEquals(comparison, _shownComparison))
        {
            _shownComparison = comparison;
            FillCompareRows();
        }
    }

    /// <summary>Ein Stück der Prüfzeile in der Farbe des Themas; <paramref name="newLine"/> beginnt eine neue Zeile (außer ganz am Anfang).</summary>
    private void AddCompareCheckText(string text, string brushKey, bool newLine)
    {
        if (newLine && _compareWarnings.Inlines.Count > 0)
        {
            _compareWarnings.Inlines.Add(new System.Windows.Documents.LineBreak());
        }

        var run = new System.Windows.Documents.Run(text);
        run.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, brushKey);
        _compareWarnings.Inlines.Add(run);
    }

    /// <summary>
    /// Die Prüfzeile, Punkt für Punkt: ein Häkchen nur für das, was wirklich verglichen wurde und gleich ist. Fehlt ein Wert (Save, Playset,
    /// Stand der Moddateien) bei A oder B, heißt der Punkt "nicht geprüft"; Ungleiches steht schon in den Warnungen der Sitzung.
    /// </summary>
    private static List<(string Text, bool Confirmed)> ComparisonChecks(PerformanceRun a, PerformanceRun b, RunComparison comparison)
    {
        var checks = new List<(string Text, bool Confirmed)>();
        if (string.IsNullOrEmpty(a.SaveName) || string.IsNullOrEmpty(b.SaveName))
        {
            checks.Add((PerformanceTexts.CheckSaveUnknown, false));
        }
        else if (string.Equals(a.SaveName, b.SaveName, StringComparison.OrdinalIgnoreCase))
        {
            checks.Add((PerformanceTexts.CheckSaveSame, true));
        }

        if (comparison.SameMonths)
        {
            checks.Add((PerformanceTexts.CheckMonthsSame, true));
        }

        if (string.IsNullOrEmpty(a.PlaysetName) || string.IsNullOrEmpty(b.PlaysetName))
        {
            checks.Add((PerformanceTexts.CheckPlaysetUnknown, false));
        }
        else if (string.Equals(a.PlaysetName, b.PlaysetName, StringComparison.OrdinalIgnoreCase))
        {
            checks.Add((PerformanceTexts.CheckPlaysetSame, true));
        }

        if (string.IsNullOrEmpty(a.ModFilesHash) || string.IsNullOrEmpty(b.ModFilesHash))
        {
            checks.Add((PerformanceTexts.CheckModUnknown, false));
        }
        else if (a.ModFilesHash == b.ModFilesHash)
        {
            checks.Add((PerformanceTexts.CheckModSame, true));
        }

        return checks;
    }

    /// <summary>Die Einträge nur bei geänderter Laufliste neu aufbauen; die Auswahl folgt A und B der Sitzung, ohne selbst einen Vergleich auszulösen.</summary>
    private void FillCompareChoices(PerformanceSession? session)
    {
        _fillingCompareChoices = true;
        try
        {
            _compareA.IsEnabled = session is not null;
            _compareB.IsEnabled = session is not null;
            var runs = session?.Runs ?? Array.Empty<PerformanceRun>();
            if (!ReferenceEquals(runs, _compareRuns))
            {
                _compareRuns = runs;
                foreach (var box in new[] { _compareA, _compareB })
                {
                    box.Items.Clear();
                    foreach (var run in runs)
                    {
                        var date = run.CreatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
                        box.Items.Add(new Controls.ComboBoxItem { Content = PerformanceTexts.CompareRunChoice(run.Name, date), Tag = run.Id });
                    }
                }
            }

            SelectChoice(_compareA, session?.A?.Id);
            SelectChoice(_compareB, session?.B?.Id);
        }
        finally
        {
            _fillingCompareChoices = false;
        }
    }

    private static void SelectChoice(Controls.ComboBox box, string? runId)
    {
        var items = box.Items.Cast<Controls.ComboBoxItem>().ToList();
        box.SelectedIndex = runId is null ? -1 : items.FindIndex(i => (string?)i.Tag == runId);
    }

    private static string? ChoiceId(Controls.ComboBox box) => (box.SelectedItem as Controls.ComboBoxItem)?.Tag as string;

    private void OnCompareChoiceChanged()
    {
        if (_fillingCompareChoices || _service.Session is not { } session)
        {
            return;
        }

        try
        {
            WithWaitCursor(() => session.Compare(ChoiceId(_compareA), ChoiceId(_compareB)));
        }
        catch (Exception ex)
        {
            _status.Text = PerformanceTexts.StatusActionFailed(ex.Message);
        }
    }

    /// <summary>Die Felder zeigen die Schwellen der Sitzung (der Dienst liest sie beim Aufbau aus dem Sitzungszustand); nur bei geänderten Werten neu, damit eine laufende Eingabe stehen bleibt.</summary>
    private void UpdateNoiseBoxes(PerformanceSession? session)
    {
        _noiseRelativeBox.IsEnabled = session is not null;
        _noiseFloorBox.IsEnabled = session is not null;
        if (session is not null && !Equals(session.Noise, _shownNoise))
        {
            WriteNoiseBoxes(session.Noise);
        }
    }

    private void WriteNoiseBoxes(NoiseSettings noise)
    {
        _shownNoise = noise;
        _noiseRelativeBox.Text = Math.Round(noise.RelativeThreshold * 100, 6).ToString("G", CultureInfo.CurrentCulture);
        _noiseFloorBox.Text = noise.AbsoluteFloor.ToString("G", CultureInfo.CurrentCulture);
    }

    private void OnNoiseKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyNoise();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Übernimmt die Rauschfelder (Verlassen des Felds oder Eingabe): relativ in Prozent 0 bis 100, Boden ab 0, gelesen in der aktuellen Kultur.
    /// Nicht lesbare Eingaben fallen auf den bisherigen Wert zurück. Nur geänderte Werte setzen die Sitzung (baut den Vergleich neu) und den
    /// Sitzungszustand des Spiels.
    /// </summary>
    private void ApplyNoise()
    {
        if (_service.Session is not { } session)
        {
            return;
        }

        try
        {
            var noise = session.Noise;
            var relative = ParseNoise(_noiseRelativeBox.Text.Trim().TrimEnd('%')) is { } percent ? Math.Clamp(percent, 0, 100) / 100 : noise.RelativeThreshold;
            var floor = ParseNoise(_noiseFloorBox.Text) is { } value ? Math.Max(value, 0) : noise.AbsoluteFloor;
            if (relative != noise.RelativeThreshold || floor != noise.AbsoluteFloor)
            {
                // Zustand zuerst: wirft ein anderer Changed-Abonnent im Setter, sind Sitzung und gemerkter Wert trotzdem gleich.
                _host.State.PerformanceNoiseRelative = relative;
                _host.State.PerformanceNoiseFloor = floor;
                session.Noise = noise with { RelativeThreshold = relative, AbsoluteFloor = floor };
            }

            WriteNoiseBoxes(session.Noise);
        }
        catch (Exception ex)
        {
            _status.Text = PerformanceTexts.StatusActionFailed(ex.Message);
        }
    }

    private static double? ParseNoise(string text) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var value) && double.IsFinite(value) ? value : null;

    private void FillCompareRowsSafe()
    {
        try
        {
            FillCompareRows();
        }
        catch (Exception ex)
        {
            _status.Text = PerformanceTexts.StatusActionFailed(ex.Message);
        }
    }

    /// <summary>Die Zeilen der Mod-Seite je Zeile oder je Datei; die Reihenfolge des Vergleichs bleibt als Rückfall der Sortierung erhalten.</summary>
    private void FillCompareRows()
    {
        var byFile = _viewFiles.IsChecked == true;
        _fileTotalsNote.Visibility = byFile ? Visibility.Visible : Visibility.Collapsed;
        if (_shownComparison is not { } comparison)
        {
            _compareList.ItemsSource = null;
            return;
        }

        var rows = byFile ? comparison.RowsByFile(OriginClassifier.ModSide) : comparison.Rows(OriginClassifier.ModSide);
        var display = rows.Select((r, index) => new CompareRow(
            index,
            r.Key,
            PerformanceTexts.Origin(r.Origin),
            Count(r.CallsA),
            Count(r.CallsB),
            TimeOrMissing(r.TotalA),
            TimeOrMissing(r.TotalB),
            DeltaText(r.DeltaRatio),
            VerdictText(r.Verdict, comparison.SameMonths),
            r.Verdict,
            r.Origin,
            r.CallsA,
            r.CallsB,
            r.TotalA,
            r.TotalB,
            r.DeltaRatio));
        _compareList.ItemsSource = SortedCompareRows(display).ToList();
    }

    private IEnumerable<CompareRow> SortedCompareRows(IEnumerable<CompareRow> rows)
    {
        if (_compareSort == CompareSort.Library)
        {
            return rows;
        }

        var ordered = _compareSort switch
        {
            CompareSort.Location => OrderCompare(rows, r => r.Key, StringComparer.OrdinalIgnoreCase),
            CompareSort.Origin => OrderCompare(rows, r => r.OriginValue),
            CompareSort.CallsA => OrderCompare(rows, r => r.CallsAValue),
            CompareSort.CallsB => OrderCompare(rows, r => r.CallsBValue),
            CompareSort.TotalA => OrderCompare(rows, r => r.TotalAValue),
            CompareSort.TotalB => OrderCompare(rows, r => r.TotalBValue),
            CompareSort.Delta => OrderCompare(rows, r => r.DeltaValue),
            _ => OrderCompare(rows, r => r.Verdict),
        };
        return ordered.ThenBy(r => r.Order);
    }

    private IOrderedEnumerable<CompareRow> OrderCompare<T>(IEnumerable<CompareRow> rows, Func<CompareRow, T> key, IComparer<T>? comparer = null) =>
        _compareDescending ? rows.OrderByDescending(key, comparer) : rows.OrderBy(key, comparer);

    /// <summary>Klick auf einen Spaltenkopf: gleiche Spalte kehrt die Richtung um, eine neue Spalte beginnt bei Zahlen absteigend, sonst aufsteigend.</summary>
    private void OnCompareHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Controls.GridViewColumnHeader { Column: { } column } || !_compareColumns.TryGetValue(column, out var info))
        {
            return;
        }

        if (info.Sort == _compareSort)
        {
            _compareDescending = !_compareDescending;
        }
        else
        {
            _compareSort = info.Sort;
            _compareDescending = info.Sort is CompareSort.CallsA or CompareSort.CallsB or CompareSort.TotalA or CompareSort.TotalB or CompareSort.Delta;
        }

        UpdateCompareSortHeaders();
        FillCompareRowsSafe();
    }

    private void UpdateCompareSortHeaders()
    {
        foreach (var (column, info) in _compareColumns)
        {
            column.Header = info.Sort == _compareSort ? info.Header + (_compareDescending ? " ▼" : " ▲") : info.Header;
        }
    }

    /// <summary>
    /// Der Textbericht des Vergleichs (<see cref="PerformanceReport.Comparison"/>, Top 100) in die Zwischenablage, davor die Warnungen der
    /// Sitzung (je eine Zeile; ohne Warnungen nichts zusätzlich).
    /// </summary>
    private void CopyComparison()
    {
        if (_service.Session is not { A: { } a, B: { } b, Comparison: { } comparison } session)
        {
            return;
        }

        try
        {
            var warnings = string.Concat(session.ComparisonWarnings.Select(w => w + Environment.NewLine));
            Clipboard.SetText(warnings + PerformanceReport.Comparison(a.Name, b.Name, comparison, CompareReportTop));
            _status.Text = PerformanceTexts.CompareCopied;
        }
        catch (ExternalException ex)
        {
            _status.Text = PerformanceTexts.StatusActionFailed(ex.Message);
        }
    }

    /// <summary>Zu Schritt 1 mit dem Spielstand und den Monaten von B (geplant, sonst gezählt).</summary>
    private void NewMeasurementFromB()
    {
        if (_service.Session?.B is not { } b)
        {
            return;
        }

        try
        {
            PrefillMeasurement(b.SaveName, b.PlannedMonths ?? b.CountedMonths);
        }
        catch (Exception ex)
        {
            _status.Text = PerformanceTexts.StatusActionFailed(ex.Message);
        }
    }

    /// <summary>Urteilswort; bei verschieden langen Läufen oder unbekannter Dauer mit dem Zusatz "(Dauer ungleich oder unbekannt)".</summary>
    private static string VerdictText(Verdict verdict, bool sameMonths) =>
        PerformanceTexts.VerdictName(verdict) + (sameMonths ? string.Empty : PerformanceTexts.VerdictMonthsDiffer);

    private static string DeltaText(double? ratio) => ratio is { } r ? r.ToString("+0%;-0%;0%", CultureInfo.CurrentCulture) : Missing;

    private static string TimeOrMissing(double? value) => value is { } v ? Time(v) : Missing;

    private static string Count(long? value) => value is { } v ? v.ToString("N0", CultureInfo.CurrentCulture) : Missing;
}
