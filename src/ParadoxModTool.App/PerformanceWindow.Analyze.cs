using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using ParadoxModTool.Performance;
using Binding = System.Windows.Data.Binding;
using Clipboard = System.Windows.Clipboard;
using Controls = System.Windows.Controls;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// Schritt 2 "Auswertung" (Spec, Ablauf im Fenster): Rangliste der Hotspot-Zeilen des gewählten Laufs (<see cref="PerformanceSession.Selected"/>).
/// Kopf mit Eigenzeit gesamt und je Herkunft, Herkunftsfilter, Suche über den Schlüssel, sortierbare Spalten, Zusammenfassen je Datei:Zeile, Top-N,
/// Sprung in den Editor (Doppelklick oder Eingabe), "Bericht kopieren" und die Warnungen des Dump-Lesers. Rechts der Bereich "Hinweise" mit den
/// Hinweisen der Regeln (<see cref="PerformanceSession.SelectedHints"/>), ebenfalls mit Sprung in den Editor.
/// Die Auswahl kommt aus der Laufliste von Schritt 1 ("Auswerten"); <see cref="PerformanceSession.Select"/> meldet sich über
/// <see cref="PerformanceService.SessionChanged"/>, darauf hängt dieser Reiter.
/// </summary>
internal sealed partial class PerformanceWindow
{
    private const int DefaultTop = 100;

    private static readonly IReadOnlySet<RowOrigin> AllOrigins = Enum.GetValues<RowOrigin>().ToHashSet();
    private static readonly IReadOnlySet<RowOrigin> VanillaOrigins = new HashSet<RowOrigin> { RowOrigin.Vanilla };

    private readonly Controls.TextBlock _analyzeHeader = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2) };
    private readonly Controls.TextBlock _analyzeShares = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.TextBlock _analyzeEmpty = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Controls.DockPanel _analyzeContent = new();
    private readonly Controls.ComboBox _originFilter = new() { MinWidth = 140, Margin = new Thickness(0, 0, 12, 4), VerticalAlignment = VerticalAlignment.Center };
    private readonly (FrameworkElement Panel, Controls.TextBox Box) _analyzeSearch = SearchBox.Create(placeholderText: PerformanceTexts.AnalyzeSearchPlaceholder);
    private readonly Controls.CheckBox _byLocationBox = new() { Content = PerformanceTexts.AnalyzeByLocation, Margin = new Thickness(0, 0, 12, 4), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBox _topBox = new() { Width = 50, Margin = new Thickness(0, 0, 12, 4), VerticalAlignment = VerticalAlignment.Center, Text = DefaultTop.ToString(CultureInfo.CurrentCulture) };
    private readonly Controls.Button _copyReportButton = new() { Content = PerformanceTexts.AnalyzeCopyReport, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 12, 4) };
    private readonly Controls.Button _aiAnalyzeButton = new() { Content = PerformanceTexts.AiAnalyze, ToolTip = PerformanceTexts.AiAnalyzeTip, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 12, 4) };
    private readonly Controls.Button _aiAdviceButton = new() { Content = PerformanceTexts.AiShowAdvice, ToolTip = PerformanceTexts.AiShowAdviceTip, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 12, 4), Visibility = Visibility.Collapsed };
    private readonly Controls.TextBlock _readerWarnings = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed };
    private readonly Controls.ListView _analyzeList = new() { SelectionMode = Controls.SelectionMode.Single };
    private readonly Dictionary<Controls.GridViewColumn, (AnalyzeSort Sort, string Header)> _analyzeColumns = new();
    private readonly Controls.ListView _hintsList = new() { SelectionMode = Controls.SelectionMode.Single };
    private readonly Controls.TextBlock _hintsCount = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 4, 2, 0) };
    private readonly Controls.TextBlock _hintsNote = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 2, 2, 0), Visibility = Visibility.Collapsed };
    private IReadOnlyList<PerformanceHint>? _shownHints;
    private AnalyzeSort _analyzeSort = AnalyzeSort.Total;
    private bool _analyzeDescending = true;
    private RankBy _analyzeRankBy = RankBy.Total;
    private ProfilingSummary? _analyzedSummary;

    /// <summary>Spalte, nach der die Tabelle sortiert ist. Calls, Self und Total bestimmen auch, nach welchem Maß die Top-N gewählt werden.</summary>
    private enum AnalyzeSort
    {
        Rank,
        Location,
        Origin,
        Calls,
        Self,
        Total,
    }

    /// <summary>Eine Zeile der Tabelle: Anzeige-Texte in der aktuellen Kultur, daneben die Rohwerte zum Sortieren und für den Sprung.</summary>
    private sealed record AnalyzeRow(
        int Rank,
        string Key,
        string Origin,
        string Calls,
        string Self,
        string Total,
        RowOrigin OriginValue,
        long CallsValue,
        double SelfValue,
        double TotalValue,
        string Path,
        int Line);

    /// <summary>Eine Zeile der Hinweisliste: Zeichen der Schwere, Stelle <c>Pfad:Zeile</c>, Titel und der Tooltip (Stelle und Detail, umbrochen).</summary>
    private sealed record HintRow(string Severity, string Location, string Title, string ToolTip, string Path, int Line);

    private UIElement BuildAnalyzeTab()
    {
        _analyzeShares.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _analyzeEmpty.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _readerWarnings.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Warning");

        _originFilter.Items.Add(new Controls.ComboBoxItem { Content = PerformanceTexts.AnalyzeFilterModSide, Tag = OriginClassifier.ModSide });
        _originFilter.Items.Add(new Controls.ComboBoxItem { Content = PerformanceTexts.AnalyzeFilterAll, Tag = AllOrigins });
        _originFilter.Items.Add(new Controls.ComboBoxItem { Content = PerformanceTexts.AnalyzeFilterVanilla, Tag = VanillaOrigins });
        _originFilter.SelectedIndex = 0;
        _analyzeSearch.Panel.Width = 220;
        _analyzeSearch.Panel.Margin = new Thickness(0, 0, 12, 4);
        _analyzeSearch.Panel.VerticalAlignment = VerticalAlignment.Center;

        var tools = new Controls.WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        tools.Children.Add(ToolLabel(PerformanceTexts.AnalyzeOriginFilter));
        tools.Children.Add(_originFilter);
        tools.Children.Add(_analyzeSearch.Panel);
        tools.Children.Add(_byLocationBox);
        tools.Children.Add(ToolLabel(PerformanceTexts.AnalyzeTop));
        tools.Children.Add(_topBox);
        tools.Children.Add(_copyReportButton);
        tools.Children.Add(_aiAnalyzeButton);
        tools.Children.Add(_aiAdviceButton);
        tools.Children.Add(_readerWarnings);

        var grid = new Controls.GridView();
        grid.Columns.Add(AnalyzeColumn(AnalyzeSort.Rank, PerformanceTexts.AnalyzeColumnRank, nameof(AnalyzeRow.Rank), 60));
        grid.Columns.Add(AnalyzeColumn(AnalyzeSort.Location, PerformanceTexts.AnalyzeColumnLocation, nameof(AnalyzeRow.Key), 480));
        grid.Columns.Add(AnalyzeColumn(AnalyzeSort.Origin, PerformanceTexts.AnalyzeColumnOrigin, nameof(AnalyzeRow.Origin), 120));
        grid.Columns.Add(AnalyzeColumn(AnalyzeSort.Calls, PerformanceTexts.AnalyzeColumnCalls, nameof(AnalyzeRow.Calls), 90));
        grid.Columns.Add(AnalyzeColumn(AnalyzeSort.Self, PerformanceTexts.AnalyzeColumnSelf, nameof(AnalyzeRow.Self), 90));
        grid.Columns.Add(AnalyzeColumn(AnalyzeSort.Total, PerformanceTexts.AnalyzeColumnTotal, nameof(AnalyzeRow.Total), 90));
        _analyzeList.View = grid;
        _analyzeList.AddHandler(Controls.GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnAnalyzeHeaderClick));
        _analyzeList.MouseDoubleClick += OnAnalyzeDoubleClick;
        _analyzeList.KeyDown += OnAnalyzeKeyDown;
        var openItem = new Controls.MenuItem { Header = PerformanceTexts.AnalyzeOpenInEditor };
        openItem.Click += (_, _) => JumpToSelectedRow();
        _analyzeList.ContextMenu = new Controls.ContextMenu();
        _analyzeList.ContextMenu.Items.Add(openItem);
        _analyzeList.ContextMenuOpening += (_, e) => e.Handled = _analyzeList.SelectedItem is not AnalyzeRow;

        var hints = BuildHintsBox();

        Controls.DockPanel.SetDock(_analyzeHeader, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_analyzeShares, Controls.Dock.Top);
        Controls.DockPanel.SetDock(tools, Controls.Dock.Top);
        Controls.DockPanel.SetDock(hints, Controls.Dock.Right);
        _analyzeContent.Children.Add(_analyzeHeader);
        _analyzeContent.Children.Add(_analyzeShares);
        _analyzeContent.Children.Add(tools);
        _analyzeContent.Children.Add(hints);
        _analyzeContent.Children.Add(_analyzeList);

        var root = new Controls.Grid { Margin = new Thickness(6) };
        root.Children.Add(_analyzeEmpty);
        root.Children.Add(_analyzeContent);

        _originFilter.SelectionChanged += (_, _) => FillAnalyzeRows();
        _analyzeSearch.Box.TextChanged += (_, _) => FillAnalyzeRows();
        _byLocationBox.Checked += (_, _) => FillAnalyzeRows();
        _byLocationBox.Unchecked += (_, _) => FillAnalyzeRows();
        _topBox.TextChanged += (_, _) => FillAnalyzeRows();
        _copyReportButton.Click += (_, _) => CopyReport();
        _aiAnalyzeButton.Click += (_, _) => OpenAiAnalysis();
        _aiAdviceButton.Click += (_, _) => ShowAdvice();
        _service.SessionChanged += (_, _) => UpdateAnalysis();

        UpdateSortHeaders();
        UpdateAnalysis();
        return root;
    }

    /// <summary>
    /// Der Bereich "Hinweise": Liste mit Schwere, Stelle und Titel (Tooltip = Detail), darunter die Anzahl und die Notiz der Sitzung
    /// (<see cref="PerformanceSession.SelectedHintsNote"/>). Doppelklick, Eingabe oder das Kontextmenü öffnen die Stelle wie in der Rangliste.
    /// </summary>
    private Controls.GroupBox BuildHintsBox()
    {
        _hintsCount.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _hintsNote.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");

        var grid = new Controls.GridView();
        grid.Columns.Add(new Controls.GridViewColumn { Header = PerformanceTexts.AnalyzeHintsColumnSeverity, Width = 56, DisplayMemberBinding = new Binding(nameof(HintRow.Severity)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = PerformanceTexts.AnalyzeColumnLocation, Width = 140, DisplayMemberBinding = new Binding(nameof(HintRow.Location)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = PerformanceTexts.AnalyzeHintsColumnTitle, Width = 180, DisplayMemberBinding = new Binding(nameof(HintRow.Title)) });
        _hintsList.View = grid;
        var rowStyle = new Style(typeof(Controls.ListViewItem), TryFindResource(typeof(Controls.ListViewItem)) as Style);
        rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(nameof(HintRow.ToolTip))));
        _hintsList.ItemContainerStyle = rowStyle;
        _hintsList.MouseDoubleClick += OnHintsDoubleClick;
        _hintsList.KeyDown += OnHintsKeyDown;
        var openItem = new Controls.MenuItem { Header = PerformanceTexts.AnalyzeOpenInEditor };
        openItem.Click += (_, _) => JumpToSelectedHint();
        _hintsList.ContextMenu = new Controls.ContextMenu();
        _hintsList.ContextMenu.Items.Add(openItem);
        _hintsList.ContextMenuOpening += (_, e) => e.Handled = _hintsList.SelectedItem is not HintRow;

        var content = new Controls.DockPanel();
        Controls.DockPanel.SetDock(_hintsNote, Controls.Dock.Bottom);
        Controls.DockPanel.SetDock(_hintsCount, Controls.Dock.Bottom);
        content.Children.Add(_hintsNote);
        content.Children.Add(_hintsCount);
        content.Children.Add(_hintsList);

        // Der Tooltip des Bereichs zeigt sich über Kopf, Anzahl und leerer Fläche; über einer Zeile gilt deren eigener Tooltip.
        return new Controls.GroupBox { Header = PerformanceTexts.AnalyzeHintsTitle, Width = 400, Margin = new Thickness(10, 0, 0, 0), Content = content, ToolTip = PerformanceTexts.AnalyzeHintsTip };
    }

    private static Controls.TextBlock ToolLabel(string text) =>
        new() { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 4) };

    private Controls.GridViewColumn AnalyzeColumn(AnalyzeSort sort, string header, string property, double width)
    {
        var column = new Controls.GridViewColumn { Header = header, Width = width, DisplayMemberBinding = new Binding(property) };
        _analyzeColumns[column] = (sort, header);
        return column;
    }

    /// <summary>Top-N aus dem Feld: ganze Zahl ab 1, sonst der Standard 100.</summary>
    private int TopInput =>
        int.TryParse(_topBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var top) && top >= 1 ? top : DefaultTop;

    /// <summary>
    /// Kopf, Warnungen und (bei geänderter Zusammenfassung) die Tabelle aus der Sitzung. Läuft auch innerhalb eines Imports (über
    /// <see cref="PerformanceSession.Changed"/>): Lesefehler dürfen hier nicht herausfliegen, sonst würde der Dienst den Import wiederholen.
    /// </summary>
    private void UpdateAnalysis()
    {
        try
        {
            UpdateAnalysisCore();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = PerformanceTexts.StatusActionFailed(ex.Message);
        }
    }

    private void UpdateAnalysisCore()
    {
        var session = _service.Session;
        var run = session?.Selected;
        var summary = session?.SelectedSummary;
        if (session is null || run is null || summary is null)
        {
            _analyzedSummary = null;
            _analyzeList.ItemsSource = null;
            ShowHints(Array.Empty<PerformanceHint>(), null);
            _analyzeEmpty.Text = session is null || run is null
                ? PerformanceTexts.AnalyzeNoRun
                : PerformanceTexts.AnalyzeDumpUnreadable(run.Name, string.Join(" ", session.SelectedWarnings));
            _analyzeEmpty.Visibility = Visibility.Visible;
            _analyzeContent.Visibility = Visibility.Collapsed;
            UpdateAiButtons();
            return;
        }

        _analyzeEmpty.Visibility = Visibility.Collapsed;
        _analyzeContent.Visibility = Visibility.Visible;
        _analyzeHeader.Text = PerformanceTexts.AnalyzeHeader(run.Name, session.SelectedDuration?.Months, Time(summary.SelfTotal));
        _analyzeShares.Text = string.Join("    ", Enum.GetValues<RowOrigin>().Select(origin =>
            PerformanceTexts.AnalyzeOriginShare(origin, Time(summary.SelfByOrigin[origin]), Share(summary.SelfByOrigin[origin], summary.SelfTotal))));

        // Hinweise vor den Warnungen lesen: Erst ihre Berechnung liest das error.log, ein Lesefehler steht danach in SelectedWarnings.
        ShowHints(session.SelectedHints, session.SelectedHintsNote);
        var warnings = session.SelectedWarnings;
        _readerWarnings.Text = PerformanceTexts.AnalyzeReaderWarnings(warnings.Count);
        _readerWarnings.ToolTip = warnings.Count == 0 ? null : PerformanceTexts.AnalyzeReaderWarningsTitle + Environment.NewLine + string.Join(Environment.NewLine, warnings);
        _readerWarnings.Visibility = warnings.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        UpdateAiButtons();

        // Umbenennen, Vergleich und Rauschschwellen lassen die Zusammenfassung gleich: dann bleiben Liste, Auswahl und Bildlauf stehen.
        if (!ReferenceEquals(summary, _analyzedSummary))
        {
            _analyzedSummary = summary;
            FillAnalyzeRows();
        }
    }

    /// <summary>
    /// Rangliste nach dem Maß der Sortierspalte über alle Zeilen der gewählten Herkünfte, dann die Suche, dann die Top-N (so findet die Suche
    /// auch Zeilen hinter Platz N); die Platznummer bleibt die aus der vollen Rangliste. Zum Schluss die Sortierung der Anzeige.
    /// </summary>
    private void FillAnalyzeRows()
    {
        if (_analyzedSummary is not { } summary)
        {
            return;
        }

        var origins = (_originFilter.SelectedItem as Controls.ComboBoxItem)?.Tag as IReadOnlySet<RowOrigin> ?? OriginClassifier.ModSide;
        var ranked = _byLocationBox.IsChecked == true
            ? summary.RankByLocation(_analyzeRankBy, origins, int.MaxValue)
            : summary.Rank(_analyzeRankBy, origins, int.MaxValue);
        var search = _analyzeSearch.Box.Text.Trim();
        var rows = ranked
            .Where(r => search.Length == 0 || r.Row.Key.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Take(TopInput)
            .Select(r => new AnalyzeRow(
                r.Rank,
                r.Row.Key,
                PerformanceTexts.Origin(r.Origin),
                r.Row.Calls.ToString("N0", CultureInfo.CurrentCulture),
                Time(r.Row.Self),
                Time(r.Row.Total),
                r.Origin,
                r.Row.Calls,
                r.Row.Self,
                r.Row.Total,
                r.Row.Path,
                r.Row.Line));
        _analyzeList.ItemsSource = SortedRows(rows).ToList();
    }

    private IOrderedEnumerable<AnalyzeRow> SortedRows(IEnumerable<AnalyzeRow> rows)
    {
        var ordered = _analyzeSort switch
        {
            AnalyzeSort.Location => Order(rows, r => r.Key, StringComparer.OrdinalIgnoreCase),
            AnalyzeSort.Origin => Order(rows, r => r.OriginValue),
            AnalyzeSort.Calls => Order(rows, r => r.CallsValue),
            AnalyzeSort.Self => Order(rows, r => r.SelfValue),
            AnalyzeSort.Total => Order(rows, r => r.TotalValue),
            _ => Order(rows, r => r.Rank),
        };
        return ordered.ThenBy(r => r.Rank);
    }

    private IOrderedEnumerable<AnalyzeRow> Order<T>(IEnumerable<AnalyzeRow> rows, Func<AnalyzeRow, T> key, IComparer<T>? comparer = null) =>
        _analyzeDescending ? rows.OrderByDescending(key, comparer) : rows.OrderBy(key, comparer);

    /// <summary>Klick auf einen Spaltenkopf: gleiche Spalte kehrt die Richtung um, eine neue Spalte beginnt bei Zahlen absteigend, sonst aufsteigend.</summary>
    private void OnAnalyzeHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Controls.GridViewColumnHeader { Column: { } column } || !_analyzeColumns.TryGetValue(column, out var info))
        {
            return;
        }

        if (info.Sort == _analyzeSort)
        {
            _analyzeDescending = !_analyzeDescending;
        }
        else
        {
            _analyzeSort = info.Sort;
            _analyzeDescending = info.Sort is AnalyzeSort.Calls or AnalyzeSort.Self or AnalyzeSort.Total;
        }

        _analyzeRankBy = info.Sort switch
        {
            AnalyzeSort.Calls => RankBy.Calls,
            AnalyzeSort.Self => RankBy.Self,
            AnalyzeSort.Total => RankBy.Total,
            _ => _analyzeRankBy,
        };
        UpdateSortHeaders();
        FillAnalyzeRows();
    }

    private void UpdateSortHeaders()
    {
        foreach (var (column, info) in _analyzeColumns)
        {
            column.Header = info.Sort == _analyzeSort ? info.Header + (_analyzeDescending ? " ▼" : " ▲") : info.Header;
        }
    }

    /// <summary>Doppelklick nur auf einer Zeile (nicht auf Spaltenkopf oder Bildlaufleiste) springt in den Editor.</summary>
    private void OnAnalyzeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && Controls.ItemsControl.ContainerFromElement(_analyzeList, source) is Controls.ListViewItem)
        {
            JumpToSelectedRow();
            e.Handled = true;
        }
    }

    private void OnAnalyzeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _analyzeList.SelectedItem is AnalyzeRow)
        {
            JumpToSelectedRow();
            e.Handled = true;
        }
    }

    /// <summary>Öffnet die Moddatei der gewählten Zeile an ihrer Zeile im Editor; liegt die Datei nicht in der Mod (z. B. Vanilla), nur ein Hinweis.</summary>
    private void JumpToSelectedRow()
    {
        if (_analyzeList.SelectedItem is AnalyzeRow row)
        {
            JumpTo(row.Path, row.Line);
        }
    }

    /// <summary>Öffnet die Moddatei zu <paramref name="path"/> (Pfad wie im Dump) an <paramref name="line"/>; liegt sie nicht in der Mod, nur ein Hinweis.</summary>
    private void JumpTo(string path, int line)
    {
        if (_service.Session?.SelectedClassifier?.ResolveModFile(path) is not { } fullPath)
        {
            _status.Text = PerformanceTexts.AnalyzeNotInMod(path);
            return;
        }

        try
        {
            _host.OpenFileAtLine(fullPath, line);
            _status.Text = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = PerformanceTexts.StatusActionFailed(ex.Message);
        }
    }

    /// <summary>
    /// Füllt die Hinweisliste, die Anzahl und die Notiz. Dieselbe Liste der Sitzung (Hinweise hängen am Lauf und bleiben bei Umbenennen oder
    /// Vergleich gleich) wird nicht neu gesetzt, damit Auswahl und Bildlauf stehen bleiben.
    /// </summary>
    private void ShowHints(IReadOnlyList<PerformanceHint> hints, string? note)
    {
        if (!ReferenceEquals(hints, _shownHints))
        {
            _shownHints = hints;
            _hintsList.ItemsSource = hints.Select(h => new HintRow(
                SeverityIcon(h.Severity),
                $"{h.Path}:{h.Line.ToString(CultureInfo.InvariantCulture)}",
                h.Title,
                $"{h.Path}:{h.Line.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}{h.Title}{Environment.NewLine}{Wrap(h.Detail, 90)}",
                h.Path,
                h.Line)).ToList();
        }

        _hintsCount.Text = PerformanceTexts.AnalyzeHintsCount(hints.Count);
        _hintsNote.Text = note ?? string.Empty;
        _hintsNote.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Zeichen der Schwere in der Hinweisliste: ⚠ für Warnungen, ℹ für Hinweise.</summary>
    internal static string SeverityIcon(HintSeverity severity) => severity == HintSeverity.Warning ? "⚠" : "ℹ";

    /// <summary>Bricht einen langen Text an Leerzeichen nach höchstens <paramref name="width"/> Zeichen um (Tooltips umbrechen sonst nicht).</summary>
    internal static string Wrap(string text, int width)
    {
        var result = new System.Text.StringBuilder();
        var lineLength = 0;
        foreach (var word in text.Split(' '))
        {
            if (lineLength > 0 && lineLength + 1 + word.Length > width)
            {
                result.Append(Environment.NewLine);
                lineLength = 0;
            }
            else if (lineLength > 0)
            {
                result.Append(' ');
                lineLength++;
            }

            result.Append(word);
            lineLength += word.Length;
        }

        return result.ToString();
    }

    /// <summary>Doppelklick nur auf einer Zeile der Hinweisliste springt in den Editor.</summary>
    private void OnHintsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && Controls.ItemsControl.ContainerFromElement(_hintsList, source) is Controls.ListViewItem)
        {
            JumpToSelectedHint();
            e.Handled = true;
        }
    }

    private void OnHintsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _hintsList.SelectedItem is HintRow)
        {
            JumpToSelectedHint();
            e.Handled = true;
        }
    }

    private void JumpToSelectedHint()
    {
        if (_hintsList.SelectedItem is HintRow row)
        {
            JumpTo(row.Path, row.Line);
        }
    }

    /// <summary>
    /// Der Textbericht des Laufs (<see cref="PerformanceReport.Summary"/>) mit der Top-N aus dem Feld in die Zwischenablage, dazu die Hinweise
    /// und ihre Notiz.
    /// </summary>
    private void CopyReport()
    {
        if (_service.Session is not { Selected: { } run, SelectedSummary: { } summary, SelectedDuration: { } duration } session)
        {
            return;
        }

        try
        {
            Clipboard.SetText(PerformanceReport.Summary(run.Name, summary, duration, TopInput, session.SelectedHints, session.SelectedHintsNote));
            _status.Text = PerformanceTexts.AnalyzeReportCopied;
        }
        catch (ExternalException ex)
        {
            _status.Text = PerformanceTexts.StatusActionFailed(ex.Message);
        }
    }

    /// <summary>
    /// "Mit KI analysieren" nur mit ausgewertetem Lauf; "KI-Rat anzeigen" nur, wenn zum gewählten Lauf ein Rat gespeichert ist. Auch nach dem
    /// Übernehmen einer Antwort im KI-Fenster (das Speichern meldet keine Änderung der Sitzung).
    /// </summary>
    internal void UpdateAiButtons()
    {
        var session = _service.Session;
        _aiAnalyzeButton.IsEnabled = session is { Selected: not null, SelectedSummary: not null };
        bool hasAdvice;
        try
        {
            hasAdvice = session?.Selected is { } run && _service.Store.HasAttachment(run, PerformanceRunStore.AdviceFileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            hasAdvice = false;
        }

        _aiAdviceButton.Visibility = hasAdvice ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Höchstens ein KI-Fenster je Performance-Fenster; ein offenes wird nach vorn geholt. Es schließt mit diesem Fenster.</summary>
    private void OpenAiAnalysis()
    {
        if (_aiWindow is not null)
        {
            if (_aiWindow.WindowState == WindowState.Minimized)
            {
                _aiWindow.WindowState = WindowState.Normal;
            }

            _aiWindow.Activate();
            return;
        }

        if (_service.Session is not { Selected: not null, SelectedSummary: not null })
        {
            _status.Text = PerformanceTexts.AiNoRun;
            return;
        }

        var host = new AiTaskHost(_host.GameId, _host.KnownMods, () => _service.ModRoot ?? _host.ActiveModRoot(), _host.GameInstallRoot);
        _aiWindow = new AiTaskWindow(this, host, new PerformanceAiFlavor(this, _service, _host.ImportRecipeFromAi));
        _aiWindow.Closed += (_, _) => _aiWindow = null;
        _aiWindow.Show();
    }

    /// <summary>Zeigt den gespeicherten KI-Rat des gewählten Laufs nur zum Lesen.</summary>
    private void ShowAdvice()
    {
        if (_service.Session?.Selected is not { } run)
        {
            return;
        }

        string? advice;
        try
        {
            advice = _service.Store.ReadAttachment(run, PerformanceRunStore.AdviceFileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = PerformanceTexts.StatusActionFailed(ex.Message);
            return;
        }

        if (advice is null)
        {
            _status.Text = PerformanceTexts.AiAdviceMissing;
            UpdateAiButtons();
            return;
        }

        TextViewWindow.Show(this, PerformanceTexts.AiAdviceTitle(run.Name), advice);
    }

    private static string Time(double value) => value.ToString("N3", CultureInfo.CurrentCulture);

    private static string Share(double part, double total) => (total > 0 ? part / total : 0).ToString("P1", CultureInfo.CurrentCulture);
}
