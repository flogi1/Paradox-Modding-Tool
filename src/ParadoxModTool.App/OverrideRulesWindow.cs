using System.Windows;
using ParadoxModTool.ModCompat;
using Binding = System.Windows.Data.Binding;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using DockPanel = System.Windows.Controls.DockPanel;
using GridView = System.Windows.Controls.GridView;
using GridViewColumn = System.Windows.Controls.GridViewColumn;
using ListView = System.Windows.Controls.ListView;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>
/// Bearbeitet die Spielebene der Überschreib-Regeltabelle. Oben die wirksame Tabelle (Standard aus dem Spielprofil, Spiel-Einträge darüber),
/// unten das Formular für einen Eintrag. Ein Spiel-Eintrag ersetzt den Standard-Eintrag desselben Ordners.
/// </summary>
public sealed class OverrideRulesWindow : Window
{
    private sealed record RuleRow(OverrideRule Rule, bool IsGame, bool OverridesDefault)
    {
        public string Folder => Rule.Folder;

        public string BehaviorText => CompatText.Behavior(Rule.Behavior);

        public string CertainText => Rule.Certain ? "gesichert" : "unsicher";

        public string Layer => IsGame ? (OverridesDefault ? "Spiel (ersetzt Standard)" : "Spiel") : "Standard";
    }

    private sealed record BehaviorChoice(OverrideBehavior Behavior)
    {
        public string Text => CompatText.Behavior(Behavior);
    }

    private readonly IReadOnlyList<OverrideRule> _shipped;
    private readonly string _file;
    private readonly List<OverrideRule> _game;
    private readonly ListView _list = new();
    private readonly TextBox _folder = new();
    private readonly ComboBox _behavior = new();
    private readonly CheckBox _certain = new() { Content = "Laut Wiki gesichert", IsChecked = true, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _remove = new() { Content = "Entfernen", Padding = new Thickness(8, 2, 8, 2), IsEnabled = false };

    public OverrideRulesWindow(IReadOnlyList<OverrideRule> shipped, string rulesFile)
    {
        _shipped = shipped;
        _file = rulesFile;
        _game = OverrideRulesStore.Load(rulesFile);

        Title = "Überschreib-Regeln";
        Width = 760;
        Height = 560;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var root = new DockPanel { Margin = new Thickness(12) };
        var help = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
            Text = "Der Ordner steht relativ zum Wurzelordner der Spielinhalte, zum Beispiel \"*/common/on_action\". Ein Stern steht für genau einen Ordnernamen "
                + "(in_game, main_menu, loading_screen). Der längste passende Ordner gilt. Ein Spiel-Eintrag ersetzt den Standard-Eintrag desselben Ordners; "
                + "wird er entfernt, gilt wieder der Standard. \"Unsicher\" markiert Funde als nicht durch das Wiki gesichert. Änderungen gelten ab der nächsten Prüfung.",
        };
        DockPanel.SetDock(help, System.Windows.Controls.Dock.Top);
        root.Children.Add(help);

        var form = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(form, System.Windows.Controls.Dock.Bottom);
        form.Children.Add(new TextBlock { Text = "Ordner", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
        form.Children.Add(_folder);
        form.Children.Add(new TextBlock { Text = "Verhalten", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 2) });
        _behavior.ItemsSource = Enum.GetValues<OverrideBehavior>().Select(b => new BehaviorChoice(b)).ToList();
        _behavior.DisplayMemberPath = nameof(BehaviorChoice.Text);
        _behavior.SelectedIndex = 1;
        form.Children.Add(_behavior);
        form.Children.Add(_certain);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var apply = new Button { Content = "Übernehmen", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        apply.Click += (_, _) => Apply();
        var clear = new Button { Content = "Neuer Eintrag", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        clear.Click += (_, _) => NewEntry();
        _remove.Margin = new Thickness(0, 0, 6, 0);
        _remove.Click += (_, _) => RemoveSelected();
        var close = new Button { Content = "Schließen", Padding = new Thickness(8, 2, 8, 2), IsCancel = true };
        buttons.Children.Add(apply);
        buttons.Children.Add(clear);
        buttons.Children.Add(_remove);
        buttons.Children.Add(close);
        form.Children.Add(buttons);
        root.Children.Add(form);

        var grid = new GridView();
        grid.Columns.Add(new GridViewColumn { Header = "Ordner", Width = 260, DisplayMemberBinding = new Binding(nameof(RuleRow.Folder)) });
        grid.Columns.Add(new GridViewColumn { Header = "Verhalten", Width = 190, DisplayMemberBinding = new Binding(nameof(RuleRow.BehaviorText)) });
        grid.Columns.Add(new GridViewColumn { Header = "Sicherheit", Width = 80, DisplayMemberBinding = new Binding(nameof(RuleRow.CertainText)) });
        grid.Columns.Add(new GridViewColumn { Header = "Ebene", Width = 150, DisplayMemberBinding = new Binding(nameof(RuleRow.Layer)) });
        _list.View = grid;
        _list.SelectionChanged += (_, _) => LoadSelected();
        root.Children.Add(_list);
        Content = root;

        Refresh();
    }

    private static bool SameFolder(string a, string b) =>
        string.Equals(a.Replace('\\', '/').Trim('/'), b.Replace('\\', '/').Trim('/'), StringComparison.OrdinalIgnoreCase);

    private void Refresh(string? select = null)
    {
        var rows = new List<RuleRow>();
        foreach (var rule in _shipped.Where(s => !_game.Any(g => SameFolder(g.Folder, s.Folder))))
        {
            rows.Add(new RuleRow(rule, false, false));
        }

        foreach (var rule in _game)
        {
            rows.Add(new RuleRow(rule, true, _shipped.Any(s => SameFolder(s.Folder, rule.Folder))));
        }

        rows = rows.OrderBy(r => r.Folder, StringComparer.OrdinalIgnoreCase).ToList();
        _list.ItemsSource = rows;
        _list.SelectedItem = select is null ? null : rows.FirstOrDefault(r => SameFolder(r.Folder, select));
        _remove.IsEnabled = (_list.SelectedItem as RuleRow)?.IsGame == true;
    }

    private void LoadSelected()
    {
        _remove.IsEnabled = (_list.SelectedItem as RuleRow)?.IsGame == true;
        if (_list.SelectedItem is not RuleRow row)
        {
            return;
        }

        _folder.Text = row.Rule.Folder;
        _behavior.SelectedItem = ((IEnumerable<BehaviorChoice>)_behavior.ItemsSource).First(b => b.Behavior == row.Rule.Behavior);
        _certain.IsChecked = row.Rule.Certain;
    }

    private void NewEntry()
    {
        _list.SelectedItem = null;
        _folder.Text = string.Empty;
        _behavior.SelectedIndex = 1;
        _certain.IsChecked = false;
        _folder.Focus();
    }

    private void Apply()
    {
        var folder = _folder.Text.Trim().Replace('\\', '/').Trim('/');
        if (folder.Length == 0 || _behavior.SelectedItem is not BehaviorChoice choice)
        {
            System.Windows.MessageBox.Show(this, "Gib einen Ordner an und wähle ein Verhalten.", "Überschreib-Regeln", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _game.RemoveAll(g => SameFolder(g.Folder, folder));
        _game.Add(new OverrideRule(folder, choice.Behavior, _certain.IsChecked == true));
        OverrideRulesStore.Save(_file, _game);
        Refresh(folder);
    }

    private void RemoveSelected()
    {
        if (_list.SelectedItem is RuleRow { IsGame: true } row)
        {
            _game.RemoveAll(g => SameFolder(g.Folder, row.Folder));
            OverrideRulesStore.Save(_file, _game);
            Refresh();
        }
    }
}
