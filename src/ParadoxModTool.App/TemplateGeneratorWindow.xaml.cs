using System.IO;
using ParadoxModTool.GameProfiles;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ParadoxModTool.Mods;
using ParadoxModTool.SearchReplace;
using ParadoxModTool.Templates;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ParadoxModTool.App;

/// <summary>
/// Fenster "Templates erzeugen": siehe <c>ParadoxModTool.Templates</c> für die Logik. Das Fenster analysiert das Snippet nach kurzer Pause neu, zeigt Zählpunkte (Prüfmodus),
/// die Lokalisierungstabelle und die Vorschau und behält den letzten Stand je Spiel.
/// </summary>
public partial class TemplateGeneratorWindow : Window
{
    private static readonly Brush EnabledBrush = Frozen(0x70, 0xFF, 0xC1, 0x07);
    private static readonly Brush SelectedBrush = Frozen(0xB0, 0xFF, 0x57, 0x22);

    /// <summary>Ausgewählt, aber die Stelle zählt nicht: nur ein Rahmen ohne Füllung (die Füllung ist so schwach, dass sie unsichtbar bleibt).</summary>
    private static readonly Brush SelectedOutlineBrush = Frozen(0x0A, 0xFF, 0x57, 0x22);
    private static readonly Brush MatchBrush = Frozen(0x66, 0x03, 0x9B, 0xE5);
    private const int MaxMatches = 5000;

    private readonly TemplateGeneratorHost _host;
    private readonly TemplateStore _store;
    private bool _updatingMods;
    private readonly DispatcherTimer _analysisTimer;
    private readonly PointHighlightRenderer _renderer = new();
    private readonly PointHighlightRenderer _snippetMatchRenderer = new();
    private readonly PointHighlightRenderer _previewMatchRenderer = new();
    private readonly PointHighlightRenderer _previewChangeRenderer = new();
    private readonly bool _ready;

    private bool _loading;
    private bool _highlightsStale;
    private TemplateModel? _model;
    private TemplateModel? _defaults;
    private List<PointOverride> _pendingOverrides = new();
    private Dictionary<string, List<int>> _shapeNumbers = new();
    private bool _suppressScroll;
    private List<RenderedSpan> _previewSpans = new();
    private ICollectionView? _pointView;
    private HashSet<VariationPoint> _listedPoints = new();
    private readonly RuleStore _ruleStore;
    private readonly RuleWorkspace _workspace;
    private RulesWindow? _rulesWindow;
    private RuleHelpWindow? _helpWindow;
    private RuleFile _templateRules = new();
    private string _locProfileSignature = string.Empty;
    private bool _updatingProfileBox;
    private IReadOnlyList<EffectiveRule> _effectiveRules = ShippedRules.Effective();
    private bool _started;
    private int _shownWarnings;
    private LocalizationSettings _loc = new();
    private List<PointRow> _pointRows = new();
    private List<(int Start, int Length, Brush Brush)> _snippetMatches = new();
    private List<(int Start, int Length, Brush Brush)> _previewMatches = new();

    public TemplateGeneratorWindow(TemplateGeneratorHost host, TemplateStore store)
    {
        InitializeComponent();
        _host = host;
        _store = store;
        _ruleStore = new RuleStore(host.GlobalRulesFile, host.GameRulesFile, host.GameId);
        _workspace = new RuleWorkspace(_ruleStore, () => _templateRules, () => RefreshRules());
        SnippetEditor.PreviewMouseRightButtonDown += (_, e) => MoveCaretToMouse(e);

        var highlighting = host.Highlighting();
        SnippetEditor.SyntaxHighlighting = highlighting;
        CodePreview.SyntaxHighlighting = highlighting;
        SnippetEditor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "Theme.EditorSelection");
        CodePreview.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "Theme.EditorSelection");

        _renderer.Source = HighlightSegments;
        SnippetEditor.TextArea.TextView.BackgroundRenderers.Add(_renderer);
        _snippetMatchRenderer.Source = () => _snippetMatches;
        _previewMatchRenderer.Source = () => _previewMatches;
        SnippetEditor.TextArea.TextView.BackgroundRenderers.Add(_snippetMatchRenderer);
        CodePreview.TextArea.TextView.BackgroundRenderers.Add(_previewChangeRenderer);
        _previewChangeRenderer.Source = ChangedSpans;
        CodePreview.TextArea.TextView.BackgroundRenderers.Add(_previewMatchRenderer);
        SnippetEditor.TextArea.GotKeyboardFocus += (_, _) => FindTargetBox.SelectedIndex = 0;
        SnippetEditor.TextArea.PreviewMouseLeftButtonUp += (_, _) => Dispatcher.BeginInvoke(new Action(SelectPointAtCaret), DispatcherPriority.Background);
        CodePreview.TextArea.PreviewMouseLeftButtonUp += (_, _) => Dispatcher.BeginInvoke(new Action(SelectPointInPreview), DispatcherPriority.Background);
        CodePreview.TextArea.GotKeyboardFocus += (_, _) => FindTargetBox.SelectedIndex = 1;
        EventHandler onThemeChanged = (_, _) =>
        {
            SnippetEditor.TextArea.TextView.Redraw();
            CodePreview.TextArea.TextView.Redraw();
        };
        ThemeManager.ThemeChanged += onThemeChanged;
        Closed += (_, _) => ThemeManager.ThemeChanged -= onThemeChanged;

        _analysisTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _analysisTimer.Tick += (_, _) =>
        {
            _analysisTimer.Stop();
            Reanalyze();
        };
        SnippetEditor.TextChanged += (_, _) =>
        {
            if (!_loading)
            {
                _highlightsStale = true;
                SnippetEditor.TextArea.TextView.Redraw();
                UpdateSearchHighlights();
                _analysisTimer.Stop();
                _analysisTimer.Start();
            }
        };

        _ready = true;
        Activated += (_, _) =>
        {
            RefreshModChoices(selectDetected: false);
            RefreshRules();
            SyncProfileIfChanged();
        };
        RefreshModChoices(selectDetected: true);
        RefreshTemplateList();
        ApplyState(_store.Load(TemplateStore.LastName) ?? new SavedTemplate { Snippet = string.Empty });
        _started = true;
        _ = EnsureProfilesAsync();
    }

    // ---- Zustand --------------------------------------------------------------------------------------------------------------------------------------

    private SavedTemplate CaptureState()
    {
        SyncLocFromControls();
        return new SavedTemplate
        {
            Snippet = SnippetEditor.Text,
            Count = ParseCount(),
            CollapseExamples = CollapseBox.IsChecked == true,
            ReviewMode = ReviewBox.IsChecked == true,
            MultiNumbers = MultiRule(),
            ShapeNumbers = _shapeNumbers.ToDictionary(k => k.Key, k => new List<int>(k.Value)),
            MarkChanges = MarkBox.IsChecked == true,
            HideIgnored = HideIgnoredItem.IsChecked == true,
            Rules = _templateRules.Rules.Select(r => r.Clone()).ToList(),
            DisabledRuleIds = new List<string>(_templateRules.DisabledRuleIds),
            HideSingleNumbers = HideNumbersItem.IsChecked == true,
            HideTexts = HideTextsItem.IsChecked == true,
            HideRuleDisabled = HideRuleItem.IsChecked == true,
            ShowMode = Math.Max(0, PointShowBox.SelectedIndex),
            PointOverrides = CurrentOverrides(),
            Localization = _loc,
        };
    }

    private void ApplyState(SavedTemplate state)
    {
        _loading = true;
        SnippetEditor.Text = state.Snippet;
        CountBox.Text = Math.Clamp(state.Count, 1, 1000).ToString();
        CollapseBox.IsChecked = state.CollapseExamples;
        ReviewBox.IsChecked = state.ReviewMode;
        MultiBox.SelectedIndex = (int)state.MultiNumbers;
        MarkBox.IsChecked = state.MarkChanges;
        HideIgnoredItem.IsChecked = state.HideIgnored;
        _templateRules = new RuleFile
        {
            Rules = (state.Rules ?? new List<CountingRule>()).Select(r => r.Clone()).ToList(),
            DisabledRuleIds = new List<string>(state.DisabledRuleIds ?? new List<string>()),
        };
        RefreshRules(reanalyze: false);
        HideNumbersItem.IsChecked = state.HideSingleNumbers;
        HideTextsItem.IsChecked = state.HideTexts;
        HideRuleItem.IsChecked = state.HideRuleDisabled;
        PointShowBox.SelectedIndex = Math.Clamp(state.ShowMode, 0, 3);
        _shapeNumbers = state.ShapeNumbers is null ? new Dictionary<string, List<int>>() : state.ShapeNumbers.ToDictionary(k => k.Key, k => new List<int>(k.Value));
        _pendingOverrides = state.PointOverrides ?? new List<PointOverride>();
        _loc = state.Localization ?? new LocalizationSettings();
        _loc.ProfileChoices ??= new Dictionary<string, bool>();
        _loc.ExtraSuffixes ??= new List<string>();
        _loc.Rows ??= new List<LocalizationRow>();
        _loc.Overrides ??= new Dictionary<string, string>();
        LocBox.IsChecked = _loc.Enabled;
        LanguageBox.Text = _loc.Language;
        DefaultTextBox.Text = _loc.DefaultText;
        RefreshProfileBox();
        _model = null;
        _defaults = null;
        _loading = false;
        ApplyVisibility();
        Reanalyze();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            _store.Save(TemplateStore.LastName, CaptureState());
        }
        catch (IOException)
        {
            // Der letzte Stand ist nur eine Bequemlichkeit; ein Fehler beim Schreiben soll das Schließen nicht verhindern.
        }
    }

    private MultiNumberRule MultiRule() => MultiBox.SelectedIndex is >= 0 and <= 2 ? (MultiNumberRule)MultiBox.SelectedIndex : MultiNumberRule.First;

    private void MarkBox_Click(object sender, RoutedEventArgs e) => CodePreview.TextArea.TextView.Redraw();

    /// <summary>Die Stellen im Ergebnis, an denen ein Zählpunkt eingesetzt wurde, wenn der Prüfmodus und die Markierung an sind.</summary>
    private IEnumerable<(int Start, int Length, Brush Brush)> ChangedSpans()
    {
        if (ReviewBox.IsChecked != true || MarkBox.IsChecked != true)
        {
            yield break;
        }

        var selected = (PointList.SelectedItem as PointRow)?.Point;
        foreach (var span in _previewSpans)
        {
            if (span.Point == selected)
            {
                yield return (span.Start, span.Length, SelectedBrush);
            }
            else if (_listedPoints.Contains(span.Point))
            {
                yield return (span.Start, span.Length, EnabledBrush);
            }
        }
    }

    private int ParseCount() => int.TryParse(CountBox.Text.Trim(), out var n) ? Math.Clamp(n, 1, 1000) : 3;

    private List<PointOverride> CurrentOverrides()
    {
        if (_model is null || _defaults is null)
        {
            return _pendingOverrides;
        }

        var diff = TemplateStore.DiffOverrides(_defaults, _model);
        var keep = _pendingOverrides.Where(o => _model.Points.All(p => p.Key != o.Key)).Take(200);
        return diff.Concat(keep).ToList();
    }

    // ---- Analyse und Anzeige --------------------------------------------------------------------------------------------------------------------------

    private void Reanalyze()
    {
        if (!_ready || _loading)
        {
            return;
        }

        _highlightsStale = false;
        var text = SnippetEditor.Text;
        var options = new AnalyzeOptions(CollapseBox.IsChecked == true, MultiRule(), _shapeNumbers.ToDictionary(k => k.Key, k => (IReadOnlyCollection<int>)k.Value), _effectiveRules);
        _pendingOverrides = CurrentOverrides();
        _defaults = TemplateAnalyzer.Analyze(text, options);
        _model = TemplateAnalyzer.Analyze(text, options, _pendingOverrides);

        var context = new PointRowContext
        {
            Instances = ParseCount,
            IsChanged = IsChangedFromDefault,
            RepeatCount = index => index >= 0 && index < _model.Groups.Count ? _model.Groups[index].Count : 1,
            GetShape = shape => _model?.Shapes.FirstOrDefault(x => x.Shape == shape),
            SetShapeNumbers = SetShapeNumbers,
            MultiRuleText = () => (MultiBox.SelectedItem as ComboBoxItem)?.Content as string ?? TemplateWindowTexts.MultiFirst,
            DefaultEnabled = point => _defaults?.Points.FirstOrDefault(p => p.Key == point.Key)?.Enabled,
        };
        _pointRows = _model.Points.Select(p => new PointRow(p, OnPointChanged, context)).ToList();
        _pointView = CollectionViewSource.GetDefaultView(_pointRows);
        _pointView.Filter = PointVisible;
        PointList.ItemsSource = _pointView;
        UpdatePointCount();
        GroupList.ItemsSource = _model.Groups.Select((g, i) => new GroupRow(g, DescribeGroup(_model, g, i), Regenerate)).ToList();
        ApplyVisibility();

        RefreshLoc();
        Regenerate();
        UpdateSearchHighlights();
        _rulesWindow?.Refresh();
    }

    private static string DescribeGroup(TemplateModel model, RepeatGroup group, int index)
    {
        var body = model.Text[group.Start..group.End];
        var oneLine = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return (index + 1) + ": " + (oneLine.Length > 60 ? oneLine[..60] + "…" : oneLine);
    }

    private void ApplyVisibility()
    {
        var review = ReviewBox.IsChecked == true;
        var loc = LocBox.IsChecked == true;
        PointsHeaderPanel.Visibility = PointList.Visibility = review ? Visibility.Visible : Visibility.Collapsed;
        GroupsPanel.Visibility = review && GroupList.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        LocPanel.Visibility = loc ? Visibility.Visible : Visibility.Collapsed;
        LocTab.Visibility = loc ? Visibility.Visible : Visibility.Collapsed;
        if (!loc)
        {
            PreviewTabs.SelectedIndex = 0;
        }

        var showRight = review || loc;
        RightPanel.Visibility = showRight ? Visibility.Visible : Visibility.Collapsed;
        ColumnSplitter.Visibility = showRight ? Visibility.Visible : Visibility.Collapsed;
        RightColumn.Width = showRight ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        RightColumn.MinWidth = showRight ? 360 : 0;

        // Bei sichtbarer Prüfliste teilen sich Zählpunkte und Lokalisierung die Höhe, sonst bekommt die sichtbare den ganzen Platz.
        var grid = (Grid)RightPanel;
        grid.RowDefinitions[1].Height = review ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        grid.RowDefinitions[3].Height = loc ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        SnippetEditor.TextArea.TextView.Redraw();
    }

    private void OnPointChanged()
    {
        RefreshLoc();
        Regenerate();
        SnippetEditor.TextArea.TextView.Redraw();
    }

    private bool IsChangedFromDefault(VariationPoint point)
    {
        var baseline = _defaults?.Points.FirstOrDefault(p => p.Key == point.Key);
        return baseline is not null
            && (point.Enabled != baseline.Enabled || point.Start != baseline.Start || point.Step != baseline.Step || point.InnerStep != baseline.InnerStep);
    }

    /// <summary>Alle Zeilen der Prüfliste zeigen ihre Werte neu an (Werte, Hinweis, Änderungsmarke).</summary>
    private void RefreshPointRows()
    {
        foreach (var row in _pointRows)
        {
            row.RaiseAll();
        }

        UpdatePointCount();
    }

    /// <summary>
    /// Legt fest, welche Zahlen der Bezeichner einer Form zählen, oder stellt mit <see langword="null"/> die allgemeine Regel wieder her. Einzelne Häkchen, die du an Punkten dieser Form gesetzt hast, werden verworfen, damit die Regel wirkt;
    /// Start und Schritt bleiben.
    /// </summary>
    private void SetShapeNumbers(string shape, IReadOnlyCollection<int>? numbers)
    {
        if (_model is null)
        {
            return;
        }

        var shapeKeys = _model.Points.Where(p => p.Shape == shape).Select(p => p.Key).ToHashSet();
        var current = CurrentOverrides();
        if (numbers is not null)
        {
            _shapeNumbers[shape] = numbers.ToList();
        }
        else
        {
            _shapeNumbers.Remove(shape);
        }

        _pendingOverrides = current
            .Select(o => shapeKeys.Contains(o.Key) ? o with { Enabled = null } : o)
            .Where(o => o.Enabled.HasValue || o.Start.HasValue || o.Step.HasValue || o.InnerStep.HasValue)
            .ToList();
        _model = null;
        _defaults = null;

        // Erst nach dem Ereignis der Auswahlliste neu aufbauen, sonst wird die Liste unter dem ausgelösten Steuerelement ersetzt.
        Dispatcher.BeginInvoke(new Action(Reanalyze), DispatcherPriority.Background);
    }

    private bool PointVisible(object item)
    {
        if (item is not PointRow row)
        {
            return true;
        }

        var point = row.Point;
        if ((HideIgnoredItem.IsChecked == true && point.IgnoredReason.Length > 0 && !point.Enabled)
            || (HideNumbersItem.IsChecked == true && point.Kind == PointKind.Number)
            || (HideTextsItem.IsChecked == true && point.Kind == PointKind.Text)
            || (HideRuleItem.IsChecked == true && point.RuleDisabled && point.IgnoredReason.Length == 0))
        {
            return false;
        }

        var show = PointShowBox.SelectedIndex;
        if ((show == 1 && !point.Enabled) || (show == 2 && point.Enabled) || (show == 3 && !IsChangedFromDefault(point)))
        {
            return false;
        }

        var filter = PointFilterBox.Text.Trim();
        return filter.Length == 0
            || row.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || row.Note.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || row.Values.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdatePointCount()
    {
        _listedPoints = _pointRows.Where(PointVisible).Select(r => r.Point).ToHashSet();
        var visible = _listedPoints.Count;
        PointCountText.Text = visible == _pointRows.Count ? TemplateWindowTexts.SpotCount(visible) : TemplateWindowTexts.SpotCountFiltered(visible, _pointRows.Count);

        // Die Markierungen in Snippet und Ergebnis folgen der Liste.
        SnippetEditor.TextArea.TextView.Redraw();
        CodePreview.TextArea.TextView.Redraw();
    }

    private void ApplyPointFilter()
    {
        if (!_ready || _loading)
        {
            return;
        }

        _pointView?.Refresh();
        UpdatePointCount();
    }

    private void PointFilter_Changed(object sender, TextChangedEventArgs e) => ApplyPointFilter();

    private void PointShow_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource == PointShowBox)
        {
            ApplyPointFilter();
        }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideButton.ContextMenu.PlacementTarget = HideButton;
        HideButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        HideButton.ContextMenu.IsOpen = true;
    }

    private void HideItem_Click(object sender, RoutedEventArgs e) => ApplyPointFilter();

    private void ResetShapeRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PointRow row })
        {
            row.ResetRule();
        }
    }

    /// <summary>Ein Klick auf eine markierte Zahl im Snippet wählt den zugehörigen Zählpunkt in der Prüfliste aus (siehe <see cref="SelectPoint"/>).</summary>
    private void SelectPointAtCaret()
    {
        if (ReviewBox.IsChecked != true || _model is null || _highlightsStale || SnippetEditor.SelectionLength > 0)
        {
            return;
        }

        var caret = SnippetEditor.CaretOffset;
        SelectPoint(_model.Points.FirstOrDefault(p => p.Occurrences.Any(o => SourceOffset(o.Start) is var start and >= 0 && caret >= start && caret <= start + o.Length)));
    }

    /// <summary>Ein Klick auf eine markierte Zahl im erzeugten Code wählt den Zählpunkt aus, der sie eingesetzt hat.</summary>
    private void SelectPointInPreview()
    {
        if (ReviewBox.IsChecked != true || _model is null || CodePreview.SelectionLength > 0)
        {
            return;
        }

        var caret = CodePreview.CaretOffset;
        SelectPoint(_previewSpans.Where(s => caret >= s.Start && caret <= s.Start + s.Length).Select(s => s.Point).FirstOrDefault());
    }

    /// <summary>
    /// Wählt den Punkt in der Prüfliste aus, scrollt zu ihm und nennt in der Statuszeile die Regel, die über ihn entscheidet. Ist er durch einen Filter ausgeblendet,
    /// werden genau die Filter gelockert, die ihn verbergen. Punkte, die weder zählen noch in der Liste stehen (nicht markiert), bleiben unberührt.
    /// </summary>
    private void SelectPoint(VariationPoint? point)
    {
        var row = point is null ? null : _pointRows.FirstOrDefault(r => r.Point == point);
        if (point is null || row is null || !(point.Enabled || _listedPoints.Contains(point)))
        {
            return;
        }

        var relaxed = new List<string>();
        void Relax(bool hidesIt, MenuItem item, string name)
        {
            if (hidesIt)
            {
                item.IsChecked = false;
                relaxed.Add(name);
            }
        }

        if (!PointVisible(row))
        {
            if (PointFilterBox.Text.Length > 0)
            {
                PointFilterBox.Text = string.Empty;
                relaxed.Add(TemplateWindowTexts.RelaxTextFilter);
            }

            if (PointShowBox.SelectedIndex != 0)
            {
                PointShowBox.SelectedIndex = 0;
                relaxed.Add(TemplateWindowTexts.RelaxShowAll);
            }

            Relax(HideIgnoredItem.IsChecked == true && point.IgnoredReason.Length > 0 && !point.Enabled, HideIgnoredItem, TemplateWindowTexts.RelaxIgnored);
            Relax(HideNumbersItem.IsChecked == true && point.Kind == PointKind.Number, HideNumbersItem, TemplateWindowTexts.RelaxNumbers);
            Relax(HideTextsItem.IsChecked == true && point.Kind == PointKind.Text, HideTextsItem, TemplateWindowTexts.HideTexts);
            Relax(HideRuleItem.IsChecked == true && point.RuleDisabled && point.IgnoredReason.Length == 0, HideRuleItem, TemplateWindowTexts.RelaxRule);
            ApplyPointFilter();
        }

        _suppressScroll = true;
        try
        {
            PointList.SelectedItem = row;
            PointList.ScrollIntoView(row);
        }
        finally
        {
            _suppressScroll = false;
        }

        StatusText.Text = row.Label + " – " + row.Note
            + (relaxed.Count > 0 ? TemplateWindowTexts.RelaxedBy(string.Join(", ", relaxed)) : string.Empty);
    }

    private void ResetPoints_Click(object sender, RoutedEventArgs e)
    {
        _shapeNumbers.Clear();
        _pendingOverrides = new List<PointOverride>();
        _model = null;
        _defaults = null;
        Reanalyze();
        StatusText.Text = TemplateWindowTexts.PointsReset;
    }

    private void SyncLocFromControls()
    {
        _loc.Enabled = LocBox.IsChecked == true;
        _loc.Language = string.IsNullOrWhiteSpace(LanguageBox.Text) ? "english" : LanguageBox.Text.Trim();
    }

    private void RefreshLoc()
    {
        if (_model is null)
        {
            return;
        }

        var newDefault = DefaultTextBox.Text;
        if (_loc.DefaultText != newDefault)
        {
            // Zeilen, die noch den alten Standardtext tragen, folgen dem neuen.
            foreach (var row in _loc.Rows.Where(r => r.TextPattern == _loc.DefaultText))
            {
                row.TextPattern = newDefault;
            }

            _loc.DefaultText = newDefault;
        }

        SyncLocFromControls();
        _loc.Rows = LocalizationBuilder.Suggest(_model, _loc, CurrentLocProfile());
        LocRowList.ItemsSource = _loc.Rows.Select(r => new LocRow(r, Regenerate)).ToList();
    }

    private void Regenerate()
    {
        if (!_ready || _loading || _model is null)
        {
            return;
        }

        var count = ParseCount();
        _previewSpans = new List<RenderedSpan>();
        CodePreview.Text = TemplateEngine.Render(_model, count, _previewSpans);

        if (LocBox.IsChecked == true)
        {
            SyncLocFromControls();
            EntryList.ItemsSource = LocalizationBuilder.Build(_model, _loc, count).Select(e => new EntryRow(e, (key, text) => _loc.Overrides[key] = text)).ToList();
        }

        UpdateStatus(count);
        RefreshPointRows();
    }

    private void UpdateStatus(int count)
    {
        if (_model is null)
        {
            return;
        }

        var parts = new List<string>();
        if (_model.ExampleCount >= 2)
        {
            parts.Add(TemplateWindowTexts.ExamplesMerged(_model.ExampleCount));
        }

        if (_model.Groups.Count > 0)
        {
            parts.Add(TemplateWindowTexts.RepeatedGroups(_model.Groups.Count));
        }

        parts.Add(TemplateWindowTexts.PointsActive(_model.Points.Count(p => p.Enabled), _model.Points.Count));
        parts.Add(TemplateWindowTexts.Passes(count));
        if (_model.Points.Any(p => p.Irregular))
        {
            parts.Add(TemplateWindowTexts.IrregularWarning);
        }

        StatusText.Text = string.Join(" · ", parts);
    }

    private IEnumerable<(int Start, int Length, Brush Brush)> HighlightSegments()
    {
        if (ReviewBox.IsChecked != true || _model is null || _highlightsStale)
        {
            yield break;
        }

        var selected = (PointList.SelectedItem as PointRow)?.Point;
        foreach (var point in _model.Points)
        {
            if (point != selected && !(point.Enabled && _listedPoints.Contains(point)))
            {
                continue;
            }

            var brush = point == selected ? (point.Enabled ? SelectedBrush : SelectedOutlineBrush) : EnabledBrush;
            foreach (var occurrence in point.Occurrences)
            {
                var start = SourceOffset(occurrence.Start);
                if (start >= 0)
                {
                    yield return (start, occurrence.Length, brush);
                }
            }
        }
    }

    private int SourceOffset(int modelOffset) =>
        _model is not null && modelOffset >= 0 && modelOffset < _model.SourceOffsets.Length ? _model.SourceOffsets[modelOffset] : -1;

    // ---- Steuerelemente -------------------------------------------------------------------------------------------------------------------------------

    private void CountBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Regenerate();
        UpdateSearchHighlights();
    }

    private void MultiBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && !_loading && e.OriginalSource == MultiBox)
        {
            Reanalyze();
        }
    }

    private void Options_Click(object sender, RoutedEventArgs e)
    {
        if (sender == CollapseBox || _model is null)
        {
            Reanalyze();
            return;
        }

        ApplyVisibility();
        RefreshLoc();
        Regenerate();
    }

    private void Language_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready && !_loading)
        {
            SyncLocFromControls();
        }
    }

    private void LocOptions_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready || _loading)
        {
            return;
        }

        RefreshLoc();
        Regenerate();
    }

    private void PreviewTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Auswahländerungen der Listen im Tab steigen bis hierher auf; hier ist nichts zu tun.
        e.Handled = true;
    }

    private void PointList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SnippetEditor.TextArea.TextView.Redraw();
        CodePreview.TextArea.TextView.Redraw();
        if (!_suppressScroll && PointList.SelectedItem is PointRow row && row.Point.Occurrences.Count > 0 && SourceOffset(row.Point.Occurrences[0].Start) is var offset and >= 0)
        {
            var location = SnippetEditor.Document.GetLocation(Math.Min(offset, SnippetEditor.Document.TextLength));
            SnippetEditor.ScrollTo(location.Line, location.Column);
        }
    }

    private void TogglePointAtCaret_Click(object sender, RoutedEventArgs e)
    {
        if (_model is null)
        {
            return;
        }

        var caret = SnippetEditor.CaretOffset;
        var text = SnippetEditor.Text;

        VariationPoint? found = null;
        foreach (var point in _model.Points)
        {
            foreach (var occurrence in point.Occurrences)
            {
                var start = SourceOffset(occurrence.Start);
                if (start >= 0 && caret >= start && caret <= start + occurrence.Length)
                {
                    found = point;
                }
            }
        }

        if (found is null)
        {
            // Cursor irgendwo im Wort: den Zählpunkt nehmen, dessen Ziffern in diesem Wort liegen.
            var left = caret;
            var right = caret;
            while (left > 0 && IsWordChar(text[left - 1]))
            {
                left--;
            }

            while (right < text.Length && IsWordChar(text[right]))
            {
                right++;
            }

            found = _model.Points.FirstOrDefault(p => p.Occurrences.Any(o => SourceOffset(o.Start) is var s and >= 0 && s >= left && s + o.Length <= right));
        }

        if (found is null)
        {
            StatusText.Text = TemplateWindowTexts.NoPointHere;
            return;
        }

        found.Enabled = !found.Enabled;
        PointList.Items.Refresh();
        OnPointChanged();
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or ':';

    // ---- Suchen und Ersetzen ----------------------------------------------------------------------------------------------------------------------------

    private TextEditor FindTarget => FindTargetBox.SelectedIndex == 1 ? CodePreview : SnippetEditor;

    private SearchOptions SearchSettings() => new()
    {
        MatchCase = MatchCaseBox.IsChecked == true,
        WholeWord = WholeWordBox.IsChecked == true,
        IgnoreFormatting = IgnoreFormattingBox.IsChecked == true,
    };

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Enter übernimmt den Wert eines Feldes in den Listen sofort, nicht erst beim Verlassen des Feldes.
        if (e.Key == Key.Enter && e.OriginalSource is TextBox box && box != FindBox && box != ReplaceBox
            && BindingOperations.GetBindingExpression(box, TextBox.TextProperty) is { } binding)
        {
            binding.UpdateSource();
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.F or Key.H)
        {
            if (CodePreview.TextArea.IsKeyboardFocusWithin)
            {
                FindTargetBox.SelectedIndex = 1;
            }
            else if (SnippetEditor.TextArea.IsKeyboardFocusWithin)
            {
                FindTargetBox.SelectedIndex = 0;
            }

            FindBox.Focus();
            FindBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            FindNext();
            e.Handled = true;
        }
    }

    private void FindBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindNext();
            e.Handled = true;
        }
    }

    private void FindBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateSearchHighlights();

    private void FindOptions_Click(object sender, RoutedEventArgs e) => UpdateSearchHighlights();

    private void FindTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource == FindTargetBox)
        {
            UpdateSearchHighlights();
        }
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => FindNext();

    /// <summary>Markiert alle Treffer im Snippet und in der Vorschau und zählt die im gewählten Editor.</summary>
    private void UpdateSearchHighlights()
    {
        if (!_ready)
        {
            return;
        }

        var query = FindBox.Text;
        _snippetMatches = FindMatches(SnippetEditor, query);
        _previewMatches = FindMatches(CodePreview, query);
        SnippetEditor.TextArea.TextView.Redraw();
        CodePreview.TextArea.TextView.Redraw();

        var count = (FindTarget == CodePreview ? _previewMatches : _snippetMatches).Count;
        FindStatusText.Text = query.Length == 0 ? string.Empty : count == 0 ? TemplateWindowTexts.NoMatch : count >= MaxMatches ? TemplateWindowTexts.MoreThanMatches(MaxMatches) : TemplateWindowTexts.Matches(count);
    }

    private List<(int Start, int Length, Brush Brush)> FindMatches(TextEditor editor, string query)
    {
        var result = new List<(int, int, Brush)>();
        if (query.Length == 0)
        {
            return result;
        }

        foreach (var match in ParadoxModTool.SearchReplace.TextSearch.FindAll(editor.Text, query, SearchSettings()).Take(MaxMatches))
        {
            result.Add((match.Offset, match.Length, MatchBrush));
        }

        return result;
    }

    private bool FindNext()
    {
        var editor = FindTarget;
        if (FindBox.Text.Length == 0)
        {
            return false;
        }

        var from = editor.SelectionStart + editor.SelectionLength;
        var match = ParadoxModTool.SearchReplace.TextSearch.FindNext(editor.Text, FindBox.Text, from, SearchSettings());
        if (match is null)
        {
            FindStatusText.Text = TemplateWindowTexts.NoMatch;
            return false;
        }

        editor.Select(match.Offset, match.Length);
        var location = editor.Document.GetLocation(match.Offset);
        editor.ScrollTo(location.Line, location.Column);
        return true;
    }

    private bool CheckReplaceAllowed()
    {
        if (FindTarget != CodePreview)
        {
            return true;
        }

        FindStatusText.Text = TemplateWindowTexts.PreviewSearchOnly;
        return false;
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (FindBox.Text.Length == 0 || !CheckReplaceAllowed())
        {
            return;
        }

        var selected = SnippetEditor.SelectedText;
        var matches = selected.Length == 0 ? null : ParadoxModTool.SearchReplace.TextSearch.FindAll(selected, FindBox.Text, SearchSettings());
        if (matches is { Count: 1 } && matches[0].Offset == 0 && matches[0].Length == selected.Length)
        {
            var (replaced, _) = ParadoxModTool.SearchReplace.TextSearch.ReplaceAll(selected, FindBox.Text, ReplaceBox.Text, SearchSettings());
            SnippetEditor.SelectedText = replaced;
        }

        FindNext();
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        if (FindBox.Text.Length == 0 || !CheckReplaceAllowed())
        {
            return;
        }

        var (newText, count) = ParadoxModTool.SearchReplace.TextSearch.ReplaceAll(SnippetEditor.Text, FindBox.Text, ReplaceBox.Text, SearchSettings());
        if (count > 0)
        {
            SnippetEditor.Document.Replace(0, SnippetEditor.Document.TextLength, newText);
        }

        FindStatusText.Text = count == 0 ? TemplateWindowTexts.NoMatch : TemplateWindowTexts.Replaced(count);
    }

    // ---- Vorlagen speichern und laden -----------------------------------------------------------------------------------------------------------------

    /// <summary>Löst die wirksamen Regeln aller Ebenen neu auf und analysiert das Snippet neu, wenn sich etwas geändert hat.</summary>
    private void RefreshRules(bool reanalyze = true)
    {
        var resolved = _ruleStore.Resolve(_templateRules);
        var changed = RuleSignature(resolved) != RuleSignature(_effectiveRules);
        _effectiveRules = resolved;

        if (_ruleStore.Warnings.Count > _shownWarnings)
        {
            _shownWarnings = _ruleStore.Warnings.Count;
            StatusText.Text = string.Join(" ", _ruleStore.Warnings);
        }

        if (changed && reanalyze && _started)
        {
            Reanalyze();
        }
    }

    private static string RuleSignature(IReadOnlyList<EffectiveRule> rules) =>
        string.Join("\n", rules.Select(r => string.Join("|", r.Layer, r.Rule.Id, r.Rule.Name, r.Rule.Where, r.Rule.WhereKey, r.Rule.CaseSensitive, r.Rule.Action, string.Join("¦", r.Rule.Patterns))));

    private void RefreshTemplateList()
    {
        TemplateBox.ItemsSource = _store.ListEntries().Select(e => new TemplateListItem(e.Name, e.Layer)).ToList();
    }

    /// <summary>Füllt die Mod-Auswahl neu. Beim Öffnen wird die erkannte Mod gewählt, danach bleibt die eigene Wahl erhalten (falls es die Mod noch gibt).</summary>
    private void RefreshModChoices(bool selectDetected)
    {
        var detectedId = _host.ActiveMod()?.Id;
        var keepId = selectDetected ? detectedId : (ModBox.SelectedItem as ModChoice)?.Mod?.Id;
        var choices = new List<ModChoice> { new(null, false) };
        choices.AddRange(_host.Mods().Select(m => new ModChoice(m, m.Id == detectedId)));

        _updatingMods = true;
        try
        {
            ModBox.ItemsSource = choices;
            ModBox.SelectedItem = choices.FirstOrDefault(c => c.Mod is not null && c.Mod.Id == keepId) ?? choices[0];
        }
        finally
        {
            _updatingMods = false;
        }

        ApplyModChoice();
    }

    private void ModBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingMods)
        {
            ApplyModChoice();
        }
    }

    /// <summary>Überträgt die gewählte Mod auf den Speicher; die Liste wird nur neu aufgebaut, wenn sich der Ordner der Modebene ändert.</summary>
    private void ApplyModChoice()
    {
        var mod = (ModBox.SelectedItem as ModChoice)?.Mod;
        var directory = mod is null ? null : _host.ModTemplateDirectory(mod);
        var changed = !string.Equals(directory, _store.ModDirectory, StringComparison.OrdinalIgnoreCase);
        _store.ModDirectory = directory;
        _ruleStore.ModFile = mod is null ? null : _host.ModRulesFile(mod);
        RefreshRules();

        LayerBox.IsEnabled = directory is not null;
        if (directory is null)
        {
            LayerBox.SelectedIndex = 0;
        }

        if (changed || TemplateBox.ItemsSource is null)
        {
            RefreshTemplateList();
        }
    }

    private StoreLayer SelectedLayer => LayerBox.IsEnabled && LayerBox.SelectedIndex == 1 ? StoreLayer.Mod : StoreLayer.Game;

    /// <summary>
    /// Die Ebene, auf die sich Laden und Löschen für diesen Namen beziehen: die der gewählten Listenzeile (wenn der Name noch zu ihr passt),
    /// sonst die speziellste Ebene, die den Namen hat.
    /// </summary>
    private StoreLayer? TargetLayer(string name)
    {
        if (TemplateBox.SelectedItem is TemplateListItem item && string.Equals(item.Name, TemplateStore.SafeName(name), StringComparison.OrdinalIgnoreCase))
        {
            return item.Layer;
        }

        return _store.Resolve(name) is { } found ? found.Layer : null;
    }

    private void TemplateBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateBox.SelectedItem is TemplateListItem item)
        {
            TemplateNameBox.Text = item.Name;
            if (item.Layer == StoreLayer.Game || LayerBox.IsEnabled)
            {
                LayerBox.SelectedIndex = (int)item.Layer;
            }
        }
    }

    private void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        var name = TemplateNameBox.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, TemplateWindowTexts.EnterName, TemplateWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var layer = SelectedLayer;
        var where = layer == StoreLayer.Mod ? " (Mod)" : string.Empty;
        if (_store.List(layer).Contains(TemplateStore.SafeName(name), StringComparer.OrdinalIgnoreCase)
            && MessageBox.Show(this, TemplateWindowTexts.ConfirmOverwrite(name, where), TemplateWindowTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _store.Save(name, CaptureState(), layer);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, TemplateWindowTexts.SaveFailed(ex.Message), TemplateWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RefreshTemplateList();
        StatusText.Text = TemplateWindowTexts.Saved(name, where);
    }

    private void LoadTemplate_Click(object sender, RoutedEventArgs e)
    {
        var name = TemplateNameBox.Text.Trim();
        var layer = name.Length == 0 ? null : TargetLayer(name);
        var template = layer is { } found ? _store.Load(name, found) : null;
        if (template is null)
        {
            MessageBox.Show(this, TemplateWindowTexts.NotFound, TemplateWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ApplyState(template);
        StatusText.Text = TemplateWindowTexts.Loaded(name, layer == StoreLayer.Mod ? " (Mod)" : string.Empty);
    }

    private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        var name = TemplateNameBox.Text.Trim();
        var layer = name.Length == 0 ? null : TargetLayer(name);
        if (layer is not { } found || _store.Load(name, found) is null)
        {
            return;
        }

        var where = found == StoreLayer.Mod ? " (Mod)" : string.Empty;
        if (MessageBox.Show(this, TemplateWindowTexts.ConfirmDelete(name, where), TemplateWindowTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _store.Delete(name, found);
        TemplateNameBox.Text = string.Empty;
        RefreshTemplateList();
    }

    // ---- Zählregeln -----------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Die Ebenen, auf denen der Nutzer Regeln anlegen darf: Vorlage, Mod (nur bei gewählter Mod), Spiel, Alle Spiele.</summary>
    private IReadOnlyList<LayerChoice> LayerChoices()
    {
        var choices = new List<LayerChoice> { new(RuleLayer.Template, RuleLayerNames.Of(RuleLayer.Template)) };
        if (_ruleStore.HasLayer(RuleLayer.Mod))
        {
            choices.Add(new LayerChoice(RuleLayer.Mod, "Mod: " + ((ModBox.SelectedItem as ModChoice)?.Mod?.Name ?? "Mod")));
        }

        choices.Add(new LayerChoice(RuleLayer.Game, RuleLayerNames.Of(RuleLayer.Game)));
        choices.Add(new LayerChoice(RuleLayer.AllGames, RuleLayerNames.Of(RuleLayer.AllGames)));
        return choices;
    }

    private void RulesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rulesWindow is null)
        {
            _rulesWindow = new RulesWindow(
                _workspace,
                LayerChoices,
                () => _model?.RuleHits ?? new Dictionary<string, int>(),
                () => SnippetEditor.Text,
                _host.Games,
                ShowRuleHelp)
            { Owner = this };
            _rulesWindow.Closed += (_, _) => _rulesWindow = null;
        }

        if (_rulesWindow.IsVisible)
        {
            _rulesWindow.Activate();
        }
        else
        {
            _rulesWindow.Show();
        }
    }

    private void ShowRuleHelp()
    {
        if (_helpWindow is null)
        {
            _helpWindow = new RuleHelpWindow { Owner = this };
            _helpWindow.Closed += (_, _) => _helpWindow = null;
        }

        if (_helpWindow.IsVisible)
        {
            _helpWindow.Activate();
        }
        else
        {
            _helpWindow.Show();
        }
    }

    /// <summary>Ein Rechtsklick im Snippet setzt den Cursor an die Klickstelle, damit das Kontextmenü sich auf die Zahl darunter bezieht.</summary>
    private void MoveCaretToMouse(MouseButtonEventArgs e)
    {
        if (SnippetEditor.SelectionLength == 0 && SnippetEditor.GetPositionFromPoint(e.GetPosition(SnippetEditor)) is { } position)
        {
            SnippetEditor.CaretOffset = SnippetEditor.Document.GetOffset(position.Location);
        }
    }

    private VariationPoint? PointAtCaret()
    {
        if (_model is null)
        {
            return null;
        }

        var caret = SnippetEditor.CaretOffset;
        return _model.Points.FirstOrDefault(p => p.Occurrences.Any(o => SourceOffset(o.Start) is var start and >= 0 && caret >= start && caret <= start + o.Length));
    }

    private void CreateRuleFromNumber_Click(object sender, RoutedEventArgs e)
    {
        if (PointAtCaret() is { } point)
        {
            CreateRuleFor(point);
        }
        else
        {
            StatusText.Text = TemplateWindowTexts.PutCaretOnNumber;
        }
    }

    private void CreateRuleFromSelected_Click(object sender, RoutedEventArgs e)
    {
        if (PointList.SelectedItem is PointRow row)
        {
            CreateRuleFor(row.Point);
        }
        else
        {
            StatusText.Text = TemplateWindowTexts.ChooseNumberFirst;
        }
    }

    /// <summary>Öffnet den Dialog mit einer Regel, die aus dem Namen dieses Zählpunkts gebaut ist (die Zahl als <c>[#]</c>, Aktion "Nur markierte zählt").</summary>
    private void CreateRuleFor(VariationPoint point)
    {
        var rule = CountingRule.Create(TemplateWindowTexts.RuleFor(point.Label));
        rule.Patterns.Add(RulePatternBuilder.FromShape(point.Shape, point.RunIndex));
        rule.Action = RuleAction.OnlyMarkedCounts;

        var choices = LayerChoices();
        var layer = choices.Any(c => c.Layer == RuleLayer.Mod) ? RuleLayer.Mod : RuleLayer.Game;
        var edited = RuleEditWindow.Edit(this, rule, layer, choices, _host.Games(), () => SnippetEditor.Text, ShowRuleHelp, out var chosen);
        if (edited is null)
        {
            return;
        }

        try
        {
            _workspace.Modify(chosen, f => RuleEditing.Add(f, edited));
            StatusText.Text = TemplateWindowTexts.RuleCreated(edited.Name, RuleLayerNames.Of(chosen));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, TemplateWindowTexts.RuleSaveFailed(ex.Message), TemplateWindowTexts.RulesTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---- Lokalisierungs-Profile -----------------------------------------------------------------------------------------------------------------------

    /// <summary>Das Profil, das für diese Vorlage gilt: das gewählte, sonst das des Ordners der zuletzt aktiven Datei, sonst der Rückfall.</summary>
    private ResolvedLocProfile CurrentLocProfile() => LocalizationProfileResolver.Resolve(_host.LocProfiles(), _loc, _host.SourceRelativeFolder());

    /// <summary>Stößt das Lernen der Profile an (falls nötig) und aktualisiert danach Auswahl, Statuszeile und Vorschläge.</summary>
    private async Task EnsureProfilesAsync()
    {
        ProfileStatusText.Text = _host.LocProfileStatus();
        try
        {
            await _host.EnsureLocProfiles();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Ohne Profile gilt der Rückfall; die Statuszeile nennt den Grund.
        }

        SyncProfileIfChanged(force: true);
    }

    /// <summary>Liest Profil und Status neu und schlägt neu vor, wenn sich etwas geändert hat (neue Profile, andere Datei im Editor, andere Haken).</summary>
    private void SyncProfileIfChanged(bool force = false)
    {
        if (!_ready || _loading)
        {
            return;
        }

        var status = _host.LocProfileStatus();
        var signature = CurrentLocProfile().Signature + "|" + status;
        if (!force && signature == _locProfileSignature)
        {
            return;
        }

        _locProfileSignature = signature;
        ProfileStatusText.Text = status;
        RefreshProfileBox();
        RefreshLoc();
        Regenerate();
    }

    private void RefreshProfileBox()
    {
        var learned = _host.LocProfiles();
        var automatic = LocalizationProfileResolver.FindAutomatic(learned, _host.SourceRelativeFolder());
        var items = new List<LocProfileChoice>
        {
            new(LocalizationProfileResolver.Automatic, TemplatesTexts.ProfileAutomatic(automatic ?? TemplatesTexts.ProfileFallback)),
            new(LocalizationProfileResolver.Fallback, TemplatesTexts.ProfileFallback),
        };
        if (learned is not null)
        {
            items.AddRange(learned.Profiles.Select(p => new LocProfileChoice(p.Path, TemplateWindowTexts.ProfileChoice(p.Path, p.ObjectCount, p.Fields.Count))));
        }

        var selected = _loc.ProfilePath ?? string.Empty;
        if (!items.Any(i => string.Equals(i.Path, selected, StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(new LocProfileChoice(selected, TemplateWindowTexts.ProfileNotLearned(selected)));
        }

        _updatingProfileBox = true;
        try
        {
            ProfileBox.ItemsSource = items;
            ProfileBox.SelectedItem = items.First(i => string.Equals(i.Path, selected, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _updatingProfileBox = false;
        }
    }

    private void ProfileBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingProfileBox || !_ready || _loading || ProfileBox.SelectedItem is not LocProfileChoice choice)
        {
            return;
        }

        _loc.ProfilePath = choice.Path;
        SyncProfileIfChanged(force: true);
    }

    private void ProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (LocProfileWindow.Edit(this, CurrentLocProfile(), _loc))
        {
            SyncProfileIfChanged(force: true);
        }
    }

    // ---- Ergebnis verwenden ---------------------------------------------------------------------------------------------------------------------------

    private string Code() => _model is null ? string.Empty : TemplateEngine.Render(_model, ParseCount());

    private List<LocalizationEntry> Entries()
    {
        SyncLocFromControls();
        return _model is null ? new List<LocalizationEntry>() : LocalizationBuilder.Build(_model, _loc, ParseCount());
    }

    private string Yaml() => LocalizationBuilder.ToYaml(Entries(), _loc.Language);

    private void CodeInsert_Click(object sender, RoutedEventArgs e) => InsertIntoEditor(Code());

    private void CodeNewDocument_Click(object sender, RoutedEventArgs e) => NewDocument(Code());

    private void CodeCopy_Click(object sender, RoutedEventArgs e) => CopyText(Code());

    private void LocInsert_Click(object sender, RoutedEventArgs e) => InsertIntoEditor(Yaml());

    private void LocNewDocument_Click(object sender, RoutedEventArgs e) => NewDocument(Yaml());

    private void LocCopy_Click(object sender, RoutedEventArgs e) => CopyText(Yaml());

    private void LocResetOverrides_Click(object sender, RoutedEventArgs e)
    {
        _loc.Overrides.Clear();
        Regenerate();
    }

    private void InsertIntoEditor(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        _host.InsertAtCaret(text);
        StatusText.Text = TemplateWindowTexts.Inserted;
    }

    private void NewDocument(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        _host.NewDocument(text);
        StatusText.Text = TemplateWindowTexts.OpenedInNewDocument;
    }

    private void CopyText(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(text);
            StatusText.Text = TemplateWindowTexts.Copied;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            StatusText.Text = TemplateWindowTexts.ClipboardBusy;
        }
    }

    private void CodeAppend_Click(object sender, RoutedEventArgs e)
    {
        var code = Code();
        if (code.Length == 0 || PickFile(TemplateWindowTexts.AppendCodeTitle, TemplateWindowTexts.ScriptFileFilter, null) is not { } path)
        {
            return;
        }

        try
        {
            var open = _host.ReadOpenDocument(path);
            if (open is not null)
            {
                _host.ReplaceOpenDocumentText(path, TemplateFileWriter.AppendCodeText(open, code));
                StatusText.Text = TemplateWindowTexts.AppendedInOpenDocument;
            }
            else
            {
                TemplateFileWriter.AppendCodeToFile(path, code, _host.NewFileEncoding?.Invoke(Path.GetExtension(path)));
                StatusText.Text = TemplateWindowTexts.AppendedTo(path);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, TemplateWindowTexts.WriteFailed(ex.Message), TemplateWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LocAppend_Click(object sender, RoutedEventArgs e)
    {
        var entries = Entries();
        if (entries.Count == 0 || PickFile(TemplateWindowTexts.AppendLocTitle, TemplateWindowTexts.LocFileFilter, LocFolderName()) is not { } path)
        {
            return;
        }

        try
        {
            var open = _host.ReadOpenDocument(path);
            var existing = open ?? TemplateFileWriter.ReadFile(path).Text;

            var duplicates = entries.Where(en => TemplateFileWriter.ExistingKeys(existing).Contains(en.Key)).Select(en => en.Key).ToList();
            var skip = true;
            if (duplicates.Count > 0)
            {
                var sample = string.Join(", ", duplicates.Take(5)) + (duplicates.Count > 5 ? ", …" : string.Empty);
                var answer = MessageBox.Show(
                    this,
                    TemplateWindowTexts.DuplicateKeys(duplicates.Count, sample),
                    TemplateWindowTexts.Title,
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
                if (answer == MessageBoxResult.Cancel)
                {
                    return;
                }

                skip = answer == MessageBoxResult.Yes;
            }

            var existingLanguage = TemplateFileWriter.ReadLanguage(existing);
            if (existingLanguage is not null && !string.Equals(existingLanguage, _loc.Language, StringComparison.OrdinalIgnoreCase)
                && MessageBox.Show(this, TemplateWindowTexts.LanguageMismatch(existingLanguage, _loc.Language), TemplateWindowTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            AppendResult result;
            if (open is not null)
            {
                var (text, appended) = TemplateFileWriter.AppendLocalizationText(open, entries, _loc.Language, skip);
                _host.ReplaceOpenDocumentText(path, text);
                result = appended;
            }
            else
            {
                result = TemplateFileWriter.AppendLocalizationToFile(path, entries, _loc.Language, skip);
            }

            StatusText.Text = TemplateWindowTexts.EntriesAppended(result.Added) + (result.Skipped > 0 ? TemplateWindowTexts.EntriesSkipped(result.Skipped) : string.Empty) + (open is not null ? TemplateWindowTexts.InOpenDocumentNotSaved : ".");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, TemplateWindowTexts.WriteFailed(ex.Message), TemplateWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Lokalisierungsordner der Mod als Startordner: <c>localisation</c> bei Spielen mit britischer Schreibweise, sonst <c>localization</c>.</summary>
    private string LocFolderName()
    {
        try
        {
            if (_host.ModRoot() is { Length: > 0 } root
                && LocalizationFolders.Existing(Path.GetFullPath(Environment.ExpandEnvironmentVariables(root))).FirstOrDefault() is { } folder)
            {
                return Path.GetFileName(folder);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or System.Security.SecurityException)
        {
            // Rückfall unten.
        }

        return LocalizationFolders.Names[0];
    }

    private string? PickFile(string title, string filter, string? subfolder)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = false,
            CheckPathExists = false,
        };

        try
        {
            // Der Startordner muss ein vollständiger Windows-Pfad sein; Pfade mit "/" oder Umgebungsvariablen lassen den Dialog sonst abstürzen.
            if (_host.ModRoot() is { Length: > 0 } root)
            {
                var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(root));
                var start = subfolder is not null && Directory.Exists(Path.Combine(full, subfolder)) ? Path.Combine(full, subfolder) : full;
                if (Directory.Exists(start))
                {
                    dialog.InitialDirectory = start;
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or System.Security.SecurityException)
        {
            // Ohne Startordner weitermachen.
        }

        try
        {
            dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("templates.file");
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }
        catch (ArgumentException)
        {
            dialog.InitialDirectory = string.Empty;
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }
    }

    private static Brush Frozen(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Zeichnet Hintergrundflächen hinter die Fundstellen der Zählpunkte im Snippet.</summary>
internal sealed class PointHighlightRenderer : IBackgroundRenderer
{
    public Func<IEnumerable<(int Start, int Length, Brush Brush)>>? Source { get; set; }

    public KnownLayer Layer => KnownLayer.Selection;

    private readonly Dictionary<Brush, Pen?> _pens = new();

    /// <summary>Ein dünner, kräftigerer Rahmen in der Farbe der Fläche, damit die Markierung auch auf dunklem Grund auffällt.</summary>
    private Pen? OutlineFor(Brush brush)
    {
        if (!_pens.TryGetValue(brush, out var pen))
        {
            if (brush is SolidColorBrush solid)
            {
                var line = new SolidColorBrush(Color.FromArgb(0xE0, solid.Color.R, solid.Color.G, solid.Color.B));
                line.Freeze();
                pen = new Pen(line, solid.Color.A is >= 0xB0 or <= 0x10 ? 2 : 1);
                pen.Freeze();
            }

            _pens[brush] = pen;
        }

        return pen;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Source is null || textView.Document is null || !textView.VisualLinesValid)
        {
            return;
        }

        var length = textView.Document.TextLength;
        foreach (var (start, count, brush) in Source())
        {
            if (start < 0 || start >= length)
            {
                continue;
            }

            var segment = new TextSegment { StartOffset = start, Length = Math.Min(count, length - start) };
            var pen = OutlineFor(brush);
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                drawingContext.DrawRectangle(brush, pen, rect);
            }
        }
    }
}
