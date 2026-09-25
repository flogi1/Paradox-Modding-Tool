using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.Scopes;

namespace ParadoxModTool.App;

/// <summary>
/// Dialog "Pfade vorschlagen": sucht Wege von einem Start-Scope zu einem Ziel-Scope (siehe <see cref="ScopePathFinder"/>) und lässt einen auswählen. Das Einfügen
/// übernimmt der Aufrufer anhand von <see cref="ChosenPath"/>, <see cref="PreferredForm"/> und <see cref="ChainOnly"/>.
/// </summary>
public partial class ScopePathWindow : Window
{
    /// <summary>Ein möglicher Start des Weges.</summary>
    /// <param name="Label">Text in der Auswahl (<c>Hier: country</c>, <c>scope:actor (country)</c> …).</param>
    /// <param name="Scope">Der Scope am Start.</param>
    /// <param name="Prefix">Womit der Weg im Skript beginnt: leer (aktueller Scope), <c>root</c> oder <c>scope:name</c>.</param>
    public sealed record StartOption(string Label, string Scope, string Prefix);

    private sealed record PathItem(ScopePath Path, string Text, string Detail);

    private readonly ScopePathFinder _finder;
    private bool _ready;

    public ScopePathWindow(ScopePathFinder finder, IReadOnlyList<StartOption> starts, bool triggerContext)
    {
        InitializeComponent();
        _finder = finder;

        StartBox.ItemsSource = starts;
        StartBox.SelectedIndex = 0;
        GoalBox.ItemsSource = finder.Scopes;
        FormBox.SelectedIndex = triggerContext ? 0 : 1;

        Loaded += (_, _) => GoalBox.Focus();
        _ready = true;
        Refresh();
    }

    /// <summary>Der gewählte Weg, wenn der Dialog mit <c>true</c> endet.</summary>
    public ScopePath? ChosenPath { get; private set; }

    /// <summary>Die Vorsilbe für Iteratoren: <c>any</c>, <c>every</c>, <c>random</c> oder <c>ordered</c>.</summary>
    public string PreferredForm => (FormBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "every";

    /// <summary>Nur den Ausdruck (<c>owner.capital</c>) einfügen statt der Blöcke.</summary>
    public bool ChainOnly { get; private set; }

    private int MaxSteps => int.TryParse((StepsBox.SelectedItem as ComboBoxItem)?.Content as string, out var steps) ? steps : 2;

    private void Options_Changed(object sender, RoutedEventArgs e) => Refresh();

    private void Filter_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (!_ready)
        {
            return;
        }

        PathsList.ItemsSource = null;
        InsertButton.IsEnabled = ChainButton.IsEnabled = false;

        if (StartBox.SelectedItem is not StartOption start || GoalBox.SelectedItem is not string goal)
        {
            CountText.Text = string.Empty;
            HintText.Text = "Wähle den Ziel-Scope (\"Nach\").";
            return;
        }

        if (start.Scope == goal)
        {
            CountText.Text = string.Empty;
            HintText.Text = "Start und Ziel sind derselbe Scope: hier ist kein Weg nötig.";
            return;
        }

        var form = PreferredForm;
        var paths = _finder.Find(start.Scope, goal, MaxSteps, GlobalLinksBox.IsChecked == true, start.Prefix);
        var filter = FilterBox.Text.Trim();
        var items = paths
            .Select(p => new PathItem(p, p.Describe(form), Detail(p)))
            .Where(i => filter.Length == 0 || i.Text.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        PathsList.ItemsSource = items;
        CountText.Text = items.Count == paths.Count ? $"{items.Count} Wege" : $"{items.Count} von {paths.Count} Wegen";
        HintText.Text = paths.Count == 0
            ? $"Kein Weg von {start.Scope} nach {goal} in {MaxSteps} Schritt(en) gefunden. Mehr Schritte oder globale Verweise versuchen."
            : "Doppelklick fügt den Weg als Blöcke an der Cursorstelle ein.";
    }

    /// <summary>Die Scope-Folge und die Art der Schritte: <c>country → location · Event-Target</c>.</summary>
    private static string Detail(ScopePath path)
    {
        var scopes = string.Join(" → ", new[] { path.StartScope }.Concat(path.Steps.Select(s => s.To)));
        var kinds = string.Join(" › ", path.Steps.Select(s => s.Kind switch
        {
            PathStepKind.Iterator => "Iterator",
            PathStepKind.GlobalLink => "globaler Verweis (KEY ersetzen)",
            _ => s.AnyInput ? "Event-Target (allgemein)" : "Event-Target",
        }));
        return $"{scopes}  ·  {kinds}";
    }

    private void PathsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = PathsList.SelectedItem as PathItem;
        InsertButton.IsEnabled = item is not null;
        ChainButton.IsEnabled = item?.Path.IsChain == true;
    }

    private void PathsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PathsList.SelectedItem is PathItem)
        {
            Insert_Click(sender, e);
        }
    }

    private void Insert_Click(object sender, RoutedEventArgs e) => Finish(chainOnly: false);

    private void Chain_Click(object sender, RoutedEventArgs e) => Finish(chainOnly: true);

    private void Finish(bool chainOnly)
    {
        if (PathsList.SelectedItem is not PathItem item)
        {
            return;
        }

        ChosenPath = item.Path;
        ChainOnly = chainOnly;
        DialogResult = true;
    }
}
