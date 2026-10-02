using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ParadoxModTool.Scopes;
using ParadoxModTool.Settings;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace ParadoxModTool.App;

/// <summary>
/// Dialog "Pfade vorschlagen" mit Kettenbaukasten: sucht Wege von einem Start-Scope zu einem Ziel-Scope (siehe <see cref="ScopePathFinder"/>), übernimmt einen
/// davon als bearbeitbare <see cref="ScopeChain"/> und lässt Glieder anhängen, abschneiden und mit Bedingung oder limit versehen. Das Einfügen übernimmt der
/// Aufrufer anhand von <see cref="ChosenChain"/>, <see cref="PreferredForm"/> und <see cref="ChainOnly"/>.
/// </summary>
public partial class ScopePathWindow : Window
{
    /// <summary>Ein möglicher Start des Weges.</summary>
    /// <param name="Label">Text in der Auswahl (<c>Hier: country</c>, <c>scope:actor: country</c> …).</param>
    /// <param name="Scope">Der Scope am Start.</param>
    /// <param name="Prefix">Womit der Weg im Skript beginnt: leer (aktueller Scope), <c>root</c> oder <c>scope:name</c>.</param>
    public sealed record StartOption(string Label, string Scope, string Prefix);

    private sealed record PathItem(ScopePath Path, string Text, string Detail);

    private sealed record StepItem(PathStep Step, string Text);

    /// <summary>Eine Zeile der Gliederliste.</summary>
    private sealed record LinkRow(int Index, string Number, string Text, string Scope, bool Condition, bool Limit, Visibility LimitVisibility, bool LimitEnabled, string LimitTip);

    private readonly ScopePathFinder _finder;
    private readonly IReadOnlyList<StartOption> _starts;
    private readonly IReadOnlyList<string> _modScriptValues;
    private readonly IReadOnlyList<string> _vanillaScriptValues;
    private readonly bool _ready;
    private bool _filteringLists;
    private IReadOnlyList<StepItem> _appendItems = Array.Empty<StepItem>();
    private IReadOnlyList<string> _valueItems = Array.Empty<string>();
    private ScopeChain _chain;
    private bool _updatingChain;

    /// <param name="modScriptValues">Script-Values der Mod der offenen Datei (Wert-Vorschläge nach den eingebauten Werten).</param>
    /// <param name="vanillaScriptValues">Script-Values aus Vanilla (Wert-Vorschläge nach denen der Mod).</param>
    public ScopePathWindow(
        ScopePathFinder finder,
        IReadOnlyList<StartOption> starts,
        bool triggerContext,
        IReadOnlyList<string> modScriptValues,
        IReadOnlyList<string> vanillaScriptValues)
    {
        InitializeComponent();
        _finder = finder;
        _starts = starts;
        _modScriptValues = modScriptValues;
        _vanillaScriptValues = vanillaScriptValues;
        _chain = ScopeChain.Empty(starts.FirstOrDefault()?.Scope ?? string.Empty, starts.FirstOrDefault()?.Prefix ?? string.Empty);

        StartBox.ItemsSource = starts;
        StartBox.SelectedIndex = 0;
        GoalBox.ItemsSource = finder.Scopes;
        FormBox.SelectedIndex = triggerContext ? 0 : 1;
        ValueBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => UpdatePreview()));

        Loaded += (_, _) => AppendSearchBox.Focus();
        _ready = true;
        ResetChain();
        Refresh();
    }

    /// <summary>Die gewählte Kette (mit Wert am Ende, falls eingetragen), wenn der Dialog mit <c>true</c> endet.</summary>
    public ScopeChain? ChosenChain { get; private set; }

    /// <summary>Die Vorsilbe für Iteratoren: <c>any</c>, <c>every</c>, <c>random</c> oder <c>ordered</c>.</summary>
    public string PreferredForm => (FormBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "every";

    /// <summary>Nur den Ausdruck (<c>owner.capital</c>) einfügen statt der Blöcke.</summary>
    public bool ChainOnly { get; private set; }

    private int MaxSteps => int.TryParse((StepsBox.SelectedItem as ComboBoxItem)?.Content as string, out var steps) ? steps : 2;

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        Refresh();
        UpdateChain();
    }

    private void Filter_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void StartOrGoal_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _filteringLists)
        {
            return;
        }

        ResetChain();
        Refresh();
    }

    private void StartSearch_TextChanged(object sender, TextChangedEventArgs e) =>
        FilterList(StartBox, _starts, StartSearchBox.Text, start => start.Label);

    private void GoalSearch_TextChanged(object sender, TextChangedEventArgs e) =>
        FilterList(GoalBox, _finder.Scopes, GoalSearchBox.Text, scope => scope);

    /// <summary>
    /// Zeigt in <paramref name="box"/> nur die Einträge, die den Suchtext enthalten. Die bisherige Auswahl bleibt, wenn sie passt; sonst wird der erste Treffer
    /// gewählt. Nur ein echter Wechsel der Auswahl leert die Kette und sucht neu.
    /// </summary>
    private void FilterList<T>(ComboBox box, IReadOnlyList<T> all, string search, Func<T, string> text)
        where T : class
    {
        if (!_ready)
        {
            return;
        }

        var filter = search.Trim();
        var items = filter.Length == 0 ? all.ToList() : all.Where(item => text(item).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        var before = box.SelectedItem as T;

        _filteringLists = true;
        box.ItemsSource = items;
        box.SelectedItem = before is not null && items.Contains(before) ? before : items.FirstOrDefault();
        _filteringLists = false;

        if (!Equals(before, box.SelectedItem))
        {
            ResetChain();
            Refresh();
        }
    }

    private void Form_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        Refresh();
        UpdateChain();
    }

    private void Refresh()
    {
        if (!_ready)
        {
            return;
        }

        var selected = (PathsList.SelectedItem as PathItem)?.Path;
        PathsList.ItemsSource = null;

        if (StartBox.SelectedItem is not StartOption start || GoalBox.SelectedItem is not string goal)
        {
            CountText.Text = string.Empty;
            HintText.Text = ScopeFinderTexts.HintChooseGoal;
            return;
        }

        if (start.Scope == goal)
        {
            CountText.Text = string.Empty;
            HintText.Text = ScopeFinderTexts.HintSameScope;
            return;
        }

        var form = PreferredForm;
        var includeGlobal = GlobalLinksBox.IsChecked == true;
        var all = _finder.Search(start.Scope, goal, MaxSteps, includeGlobal, start.Prefix, string.Empty, form);
        var result = _finder.Search(start.Scope, goal, MaxSteps, includeGlobal, start.Prefix, FilterBox.Text.Trim(), form);
        var items = result.Paths.Select(p => new PathItem(p, p.Describe(form), Detail(p))).ToList();

        // Die Auswahl neu setzen, ohne die (vielleicht schon bearbeitete) Kette zu ersetzen.
        _updatingChain = true;
        PathsList.ItemsSource = items;
        PathsList.SelectedItem = items.FirstOrDefault(i => ReferenceEquals(i.Path, selected));
        _updatingChain = false;

        CountText.Text = result.Total == all.Total ? ScopeFinderTexts.Count(result.Total) : ScopeFinderTexts.CountFiltered(result.Total, all.Total);
        HintText.Text = all.Total == 0
            ? ScopeFinderTexts.HintNoPath(start.Scope, goal, MaxSteps)
            : result.Truncated ? ScopeFinderTexts.HintTruncated : ScopeFinderTexts.HintPick;
    }

    /// <summary>Die Scope-Folge und die Art der Schritte: <c>country → location · Event-Target</c>.</summary>
    private static string Detail(ScopePath path)
    {
        var scopes = string.Join(" → ", new[] { path.StartScope }.Concat(path.Steps.Select(s => s.To)));
        var kinds = string.Join(" › ", path.Steps.Select(s => s.Kind switch
        {
            PathStepKind.Iterator => ScopeFinderTexts.KindIterator,
            PathStepKind.GlobalLink => ScopeFinderTexts.KindGlobal,
            PathStepKind.DataLink => ScopeFinderTexts.KindDataLink,
            _ => s.AnyInput ? ScopeFinderTexts.KindTargetAny : ScopeFinderTexts.KindTarget,
        }));
        return $"{scopes}  ·  {kinds}";
    }

    private void PathsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingChain || PathsList.SelectedItem is not PathItem item)
        {
            return;
        }

        _chain = ScopeChain.From(item.Path);
        UpdateChain();
    }


    /// <summary>Leert die Kette: sie beginnt dann am gewählten Start.</summary>
    private void ResetChain()
    {
        var start = StartBox.SelectedItem as StartOption;
        _chain = ScopeChain.Empty(start?.Scope ?? string.Empty, start?.Prefix ?? string.Empty);
        UpdateChain();
    }

    /// <summary>Zeigt die Kette neu an: Start, Glieder, Folgeglieder, Wert-Vorschläge, Vorschau und Knöpfe.</summary>
    private void UpdateChain()
    {
        var form = PreferredForm;
        StartText.Text = _chain.StartPrefix.Length > 0 ? $"{_chain.StartPrefix} ({_chain.StartScope})" : _chain.StartScope;
        StartConditionBox.IsChecked = _chain.StartCondition;

        LinksList.ItemsSource = _chain.Links.Select((link, index) =>
        {
            var iterator = link.Step.Kind == PathStepKind.Iterator;
            return new LinkRow(
                index,
                $"{index + 1}.",
                link.Step.Text(form),
                "→ " + link.Step.To,
                link.Condition,
                link.Limit,
                iterator ? Visibility.Visible : Visibility.Collapsed,
                form != "any",
                form == "any" ? ScopeFinderTexts.LimitAnyTip : ScopeFinderTexts.LimitTip);
        }).ToList();

        _appendItems = _chain.StartScope.Length == 0
            ? Array.Empty<StepItem>()
            : _finder.NextSteps(_chain.EndScope, first: _chain.Links.Count == 0, GlobalLinksBox.IsChecked == true)
                .Select(s => new StepItem(s, $"{s.Text(form)}  → {s.To}"))
                .ToList();
        ApplyAppendFilter();

        _valueItems = _finder.ValueSuggestions(_chain.EndScope, _modScriptValues, _vanillaScriptValues);
        ApplyValueFilter();
        // Ein Wert geht auch direkt am Start (root.country_tax_base, im aktuellen Scope nur country_tax_base).
        var pure = _chain.CanBeChain && _chain.StartScope.Length > 0;
        ValueBox.IsEnabled = pure;
        ValueSearchBox.IsEnabled = pure;
        ValueBox.ToolTip = pure ? ScopeFinderTexts.ValueTip : ScopeFinderTexts.ValueDisabledTip;

        InsertButton.IsEnabled = _chain.Links.Count > 0;
        UpdatePreview();
    }

    /// <summary>Zeigt unter "Glied anhängen" nur die Folgeglieder, die den Suchtext enthalten; ohne Auswahl, angehängt wird erst auf Wahl.</summary>
    private void ApplyAppendFilter()
    {
        var filter = AppendSearchBox.Text.Trim();
        _updatingChain = true;
        AppendBox.ItemsSource = filter.Length == 0
            ? _appendItems
            : _appendItems.Where(i => i.Text.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        AppendBox.SelectedIndex = -1;
        _updatingChain = false;
    }

    /// <summary>Zeigt unter "Wert am Ende" nur die Vorschläge, die den Suchtext enthalten; der eingetragene Wert bleibt.</summary>
    private void ApplyValueFilter()
    {
        var filter = ValueSearchBox.Text.Trim();
        var valueText = ValueBox.Text;
        ValueBox.ItemsSource = filter.Length == 0
            ? _valueItems
            : _valueItems.Where(v => v.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        ValueBox.Text = valueText;
    }

    private void AppendSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready)
        {
            ApplyAppendFilter();
        }
    }

    private void ValueSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready)
        {
            ApplyValueFilter();
        }
    }

    // Enter im Suchfeld übernimmt den ersten Treffer: bei "Glied anhängen" wird er angehängt, bei "Wert am Ende" als Wert eingetragen. Enter in einem
    // Suchfeld löst nie "Einfügen" aus (der Standardknopf des Dialogs).
    private void AppendSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (AppendBox.Items.Count > 0 && AppendBox.Items[0] is StepItem first)
        {
            Append(first.Step);
            AppendSearchBox.Focus();
        }
    }

    private void ValueSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (ValueBox.Items.Count > 0 && ValueBox.Items[0] is string first)
        {
            ValueBox.Text = first;
        }
    }

    private void ListSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
        }
    }

    /// <summary>Vorschau: die Blöcke für "Einfügen" (nur mit Gliedern) und die Kette für "Nur Kette" (auch ein Wert direkt am Start); "Nur Kette" nur, wenn es eine gibt.</summary>
    private void UpdatePreview()
    {
        var parts = new List<string>();
        if (_chain.Links.Count > 0)
        {
            parts.Add(_chain.ToBlock(PreferredForm, string.Empty, Environment.NewLine, out _));
        }

        var chain = _chain.WithValue(ValueBox.Text).ChainText();
        if (chain is not null)
        {
            parts.Add(chain);
        }

        PreviewBox.Text = string.Join(Environment.NewLine + Environment.NewLine, parts);
        ChainButton.IsEnabled = chain is not null;
    }

    private void StartCondition_Click(object sender, RoutedEventArgs e)
    {
        _chain = _chain.WithCondition(-1, StartConditionBox.IsChecked == true);
        UpdateChain();
    }

    private void LinkCondition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: int index } box)
        {
            _chain = _chain.WithCondition(index, box.IsChecked == true);
            UpdateChain();
        }
    }

    private void LinkLimit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: int index } box)
        {
            _chain = _chain.WithLimit(index, box.IsChecked == true);
            UpdateChain();
        }
    }

    private void RemoveFrom_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int index })
        {
            _chain = _chain.TruncateAt(index);
            UpdateChain();
        }
    }

    // Angehängt wird erst beim Schließen der Liste oder mit Enter: Blättern und Tippen bei geschlossener Liste ändern nur die Auswahl.
    private void AppendBox_DropDownClosed(object sender, EventArgs e) => AppendSelected();

    private void AppendBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && AppendBox.SelectedItem is StepItem)
        {
            AppendSelected();
            e.Handled = true;
        }
    }

    private void AppendSelected()
    {
        if (_updatingChain || AppendBox.SelectedItem is not StepItem item)
        {
            return;
        }

        Append(item.Step);
        AppendBox.Focus();
    }

    /// <summary>Hängt ein Glied an; die Suche bei "Glied anhängen" gilt dem nächsten Glied und wird darum geleert.</summary>
    private void Append(PathStep step)
    {
        _chain = _chain.Append(step);
        _updatingChain = true;
        AppendSearchBox.Text = string.Empty;
        _updatingChain = false;
        UpdateChain();
    }

    private void Insert_Click(object sender, RoutedEventArgs e) => Finish(chainOnly: false);

    private void Chain_Click(object sender, RoutedEventArgs e) => Finish(chainOnly: true);

    private void Finish(bool chainOnly)
    {
        var chosen = _chain.WithValue(ValueBox.Text);
        if (chainOnly ? chosen.ChainText() is null : chosen.Links.Count == 0)
        {
            return;
        }

        ChosenChain = chosen;
        ChainOnly = chainOnly;
        DialogResult = true;
    }
}
