using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ParadoxModTool.Templates;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using FontStyle = System.Windows.FontStyle;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Eine Zeile im Regelfenster: eine Regel mit ihrer Ebene, ihrer Wirksamkeit und ihren Treffern im Snippet.</summary>
public sealed class RuleRow
{
    private readonly RuleListRow _row;
    private readonly int _hits;
    private readonly Action<RuleRow, bool> _toggled;

    public RuleRow(RuleListRow row, int hits, Action<RuleRow, bool> toggled)
    {
        _row = row;
        _hits = hits;
        _toggled = toggled;
    }

    public CountingRule Rule => _row.Effective.Rule;

    public RuleLayer Layer => _row.Effective.Layer;

    public bool IsShipped => Layer == RuleLayer.Shipped;

    public bool IsActive
    {
        get => _row.Active;
        set
        {
            if (value != _row.Active)
            {
                _toggled(this, value);
            }
        }
    }

    public string Name => Rule.Name;

    public string LayerText => RuleLayerNames.Of(Layer);

    public string WhereText => RuleSummary.WhereText(Rule);

    public string PatternsText => RuleSummary.PatternsText(Rule);

    public string PatternsFull => string.Join(Environment.NewLine, Rule.Patterns);

    public string ActionText => RuleSummary.ActionText(Rule.Action);

    public string GamesText => RuleSummary.GamesText(Rule, Layer);

    public string HitsText => _row.Active ? _hits.ToString() : string.Empty;

    public FontStyle NameStyle => IsShipped ? FontStyles.Italic : FontStyles.Normal;

    public double Opacity => IsActive ? 1.0 : 0.55;
}

/// <summary>Fenster "Zählregeln": siehe die Beschreibung in <c>RulesWindow.xaml</c>.</summary>
public partial class RulesWindow : Window
{
    private readonly RuleWorkspace _workspace;
    private readonly Func<IReadOnlyList<LayerChoice>> _layerChoices;
    private readonly Func<IReadOnlyDictionary<string, int>> _hits;
    private readonly Func<string> _snippet;
    private readonly Func<IReadOnlyList<(string Id, string Name)>> _games;
    private readonly Action _showHelp;

    public RulesWindow(
        RuleWorkspace workspace,
        Func<IReadOnlyList<LayerChoice>> layerChoices,
        Func<IReadOnlyDictionary<string, int>> hits,
        Func<string> snippet,
        Func<IReadOnlyList<(string Id, string Name)>> games,
        Action showHelp)
    {
        InitializeComponent();
        _workspace = workspace;
        _layerChoices = layerChoices;
        _hits = hits;
        _snippet = snippet;
        _games = games;
        _showHelp = showHelp;
        Refresh();
    }

    private RuleLayer ChosenLayer => (LayerBox.SelectedItem as LayerChoice)?.Layer ?? RuleLayer.Game;

    private RuleRow? Selected => RuleList.SelectedItem as RuleRow;

    /// <summary>Baut die Liste neu auf; die Auswahl bleibt erhalten. Wird nach jeder Änderung und nach jeder neuen Analyse des Snippets aufgerufen.</summary>
    public void Refresh()
    {
        var choices = _layerChoices();
        var keep = (LayerBox.SelectedItem as LayerChoice)?.Layer;
        LayerBox.ItemsSource = choices;
        LayerBox.SelectedItem = choices.FirstOrDefault(c => c.Layer == keep)
            ?? choices.FirstOrDefault(c => c.Layer == RuleLayer.Mod)
            ?? choices.FirstOrDefault(c => c.Layer == RuleLayer.Game)
            ?? choices[0];

        var selectedId = Selected?.Rule.Id;
        var selectedLayer = Selected?.Layer;
        var hits = _hits();
        var rows = _workspace.List().Select(r => new RuleRow(r, hits.GetValueOrDefault(r.Effective.Rule.Id), OnToggled)).ToList();
        RuleList.ItemsSource = rows;
        RuleList.SelectedItem = rows.FirstOrDefault(r => selectedId is not null && r.Rule.Id == selectedId && r.Layer == selectedLayer);

        InfoText.Text = _workspace.Store.Warnings.Count > 0 ? string.Join(" ", _workspace.Store.Warnings) : string.Empty;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var row = Selected;
        var own = row is { IsShipped: false };
        EditButton.IsEnabled = own;
        DeleteButton.IsEnabled = own;
        UpButton.IsEnabled = own;
        DownButton.IsEnabled = own;
        MoveButton.IsEnabled = own;
        CopyButton.IsEnabled = row is not null;
    }

    /// <summary>Führt eine Änderung aus, zeigt Fehler als Meldung und baut die Liste danach neu auf.</summary>
    private void Run(Action change)
    {
        try
        {
            change();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Zählregeln", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        Dispatcher.BeginInvoke(new Action(Refresh));
    }

    private void OnToggled(RuleRow row, bool active)
    {
        var chosen = ChosenLayer;
        Run(() =>
        {
            if (RuleEditing.UsesDisabledList(row.Layer, chosen))
            {
                _workspace.Modify(chosen, f => RuleEditing.SetActive(new RuleFile(), f, row.Rule, row.Layer, chosen, active));
            }
            else
            {
                _workspace.Modify(row.Layer, f => RuleEditing.SetActive(f, f, row.Rule, row.Layer, chosen, active));
            }
        });
    }

    private void RuleList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void RuleList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.CheckBox && Selected is { IsShipped: false })
        {
            Edit_Click(sender, e);
        }
    }

    private void Help_Click(object sender, RoutedEventArgs e) => _showHelp();

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var rule = CountingRule.Create("Neue Regel");
        var edited = RuleEditWindow.Edit(this, rule, ChosenLayer, _layerChoices(), _games(), _snippet, _showHelp, out var layer);
        if (edited is not null)
        {
            Run(() => _workspace.Modify(layer, f => RuleEditing.Add(f, edited)));
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            return;
        }

        if (row.IsShipped)
        {
            MessageBox.Show(this, "Mitgelieferte Regeln lassen sich nicht ändern. Mit „Kopie erstellen“ legst du eine eigene Fassung an.", "Zählregeln", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var edited = RuleEditWindow.Edit(this, row.Rule.Clone(), row.Layer, null, _games(), _snippet, _showHelp, out _);
        if (edited is not null)
        {
            Run(() => _workspace.Modify(row.Layer, f => RuleEditing.Replace(f, edited)));
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            return;
        }

        ShowLayerMenu(CopyButton, "Kopie auf ", null, layer => Run(() => _workspace.Modify(layer, f => RuleEditing.AddCopy(f, row.Rule, row.Layer))));
    }

    private void Move_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { IsShipped: false } row)
        {
            return;
        }

        ShowLayerMenu(MoveButton, "Verschieben nach ", row.Layer, layer => Run(() =>
        {
            var moved = row.Rule.Clone();
            _workspace.Modify(layer, f =>
            {
                if (f.Rules.Any(r => r.Id == moved.Id))
                {
                    throw new InvalidOperationException("Auf der Zielebene gibt es diese Regel schon.");
                }

                RuleEditing.Add(f, moved);
            });
            _workspace.Modify(row.Layer, f => RuleEditing.Delete(f, row.Rule.Id));
        }));
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { IsShipped: false } row)
        {
            return;
        }

        if (MessageBox.Show(this, $"Die Regel „{row.Name}“ ({row.LayerText}) löschen?", "Zählregeln", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            Run(() => _workspace.Modify(row.Layer, f => RuleEditing.Delete(f, row.Rule.Id)));
        }
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { IsShipped: false } row)
        {
            Run(() => _workspace.Modify(row.Layer, f => RuleEditing.MoveUp(f, row.Rule.Id)));
        }
    }

    private void Down_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { IsShipped: false } row)
        {
            Run(() => _workspace.Modify(row.Layer, f => RuleEditing.MoveDown(f, row.Rule.Id)));
        }
    }

    /// <summary>Zeigt unter dem Button ein Menü mit den Ebenen (ohne <paramref name="exclude"/>) und ruft bei der Wahl <paramref name="chosen"/> auf.</summary>
    private void ShowLayerMenu(Button anchor, string prefix, RuleLayer? exclude, Action<RuleLayer> chosen)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
        foreach (var choice in _layerChoices().Where(c => c.Layer != exclude))
        {
            var layer = choice.Layer;
            var item = new MenuItem { Header = prefix + choice.Text };
            item.Click += (_, _) => chosen(layer);
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }
}
