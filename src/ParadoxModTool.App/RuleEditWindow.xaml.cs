using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using ParadoxModTool.Templates;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using ComboBox = System.Windows.Controls.ComboBox;
using MessageBox = System.Windows.MessageBox;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using TextBlock = System.Windows.Controls.TextBlock;

namespace ParadoxModTool.App;

/// <summary>Eine wählbare Ebene im Dialog und im Regelfenster (mit Anzeigetext, z. B. "Mod: Idea Variation").</summary>
public sealed record LayerChoice(RuleLayer Layer, string Text)
{
    public override string ToString() => Text;
}

/// <summary>Dialog "Regel bearbeiten": siehe die Beschreibung in <c>RuleEditWindow.xaml</c>.</summary>
public partial class RuleEditWindow : Window
{
    private static readonly Brush CountsBrush = new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47));
    private static readonly Brush NotCountsBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));

    private readonly string _ruleId;
    private readonly bool _enabled;
    private readonly RuleLayer _fixedLayer;
    private readonly bool _layerEditable;
    private readonly IReadOnlyList<(string Id, string Name)> _games;
    private readonly Func<string> _snippet;
    private readonly Action _showHelp;
    private readonly DispatcherTimer _timer;
    private readonly List<CheckBox> _gameChecks = new();
    private readonly bool _ready;

    private RuleEditWindow(
        CountingRule rule,
        RuleLayer layer,
        IReadOnlyList<LayerChoice>? layerChoices,
        IReadOnlyList<(string Id, string Name)> games,
        Func<string> snippet,
        Action showHelp)
    {
        InitializeComponent();
        _ruleId = rule.Id;
        _enabled = rule.Enabled;
        _fixedLayer = layer;
        _layerEditable = layerChoices is not null;
        _games = games;
        _snippet = snippet;
        _showHelp = showHelp;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            RefreshPreview();
        };

        if (layerChoices is not null)
        {
            LayerBox.ItemsSource = layerChoices;
            LayerBox.SelectedItem = layerChoices.FirstOrDefault(c => c.Layer == layer) ?? layerChoices[0];
        }
        else
        {
            LayerPanel.Visibility = Visibility.Collapsed;
        }

        foreach (var (id, name) in games)
        {
            var check = new CheckBox { Content = name, Tag = id, Margin = new Thickness(0, 0, 14, 4) };
            check.Click += (_, _) => Changed();
            _gameChecks.Add(check);
            GamesPanel.Children.Add(check);
        }

        foreach (var action in Enum.GetValues<RuleAction>())
        {
            ActionBox.Items.Add(new ComboBoxItem { Content = RuleSummary.ActionText(action), Tag = action });
        }

        NameBox.Text = rule.Name;
        WhereBox.SelectedIndex = (int)rule.Where;
        KeyBox.Text = rule.WhereKey;
        PatternsBox.Text = string.Join(Environment.NewLine, rule.Patterns);
        CaseBox.IsChecked = rule.CaseSensitive;
        ActionBox.SelectedIndex = (int)rule.Action;
        var allGames = rule.Games.Count == 0 || rule.Games.Contains("*");
        AllGamesCheck.IsChecked = allGames;
        foreach (var check in _gameChecks)
        {
            check.IsChecked = rule.Games.Contains((string)check.Tag, StringComparer.OrdinalIgnoreCase);
        }

        _ready = true;
        UpdateVisibility();
        RefreshPreview();
        Loaded += (_, _) => NameBox.Focus();
    }

    /// <summary>
    /// Öffnet den Dialog und gibt die bearbeitete Regel zurück; <see langword="null"/> bei Abbruch. Ohne <paramref name="layerChoices"/> ist die Ebene fest
    /// (<paramref name="layer"/>) und wird nicht angeboten; sonst wählt der Nutzer sie und <paramref name="resultLayer"/> nennt die Wahl.
    /// </summary>
    public static CountingRule? Edit(
        Window owner,
        CountingRule rule,
        RuleLayer layer,
        IReadOnlyList<LayerChoice>? layerChoices,
        IReadOnlyList<(string Id, string Name)> games,
        Func<string> snippet,
        Action showHelp,
        out RuleLayer resultLayer)
    {
        var window = new RuleEditWindow(rule, layer, layerChoices, games, snippet, showHelp) { Owner = owner };
        if (window.ShowDialog() != true)
        {
            resultLayer = layer;
            return null;
        }

        resultLayer = window.CurrentLayer;
        return window.BuildRule();
    }

    private RuleLayer CurrentLayer => _layerEditable && LayerBox.SelectedItem is LayerChoice choice ? choice.Layer : _fixedLayer;

    private CountingRule BuildRule() => new()
    {
        Id = _ruleId,
        Name = NameBox.Text.Trim(),
        Enabled = _enabled,
        Games = AllGamesCheck.IsChecked == true
            ? new List<string> { "*" }
            : _gameChecks.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList(),
        Where = WhereBox.SelectedIndex == 1 ? RuleWhere.InsideKey : RuleWhere.Anywhere,
        WhereKey = KeyBox.Text.Trim(),
        Patterns = PatternsBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        CaseSensitive = CaseBox.IsChecked == true,
        Action = ActionBox.SelectedItem is ComboBoxItem { Tag: RuleAction action } ? action : RuleAction.OnlyMarkedCounts,
    };

    private void UpdateVisibility()
    {
        GamesHost.Visibility = CurrentLayer == RuleLayer.AllGames ? Visibility.Visible : Visibility.Collapsed;
        GamesPanel.IsEnabled = AllGamesCheck.IsChecked != true;
        KeyBox.IsEnabled = WhereBox.SelectedIndex == 1;
        ActionHelpText.Text = ActionBox.SelectedItem is ComboBoxItem { Tag: RuleAction action } ? RuleSummary.ActionHelp(action) : string.Empty;
    }

    private void Changed()
    {
        if (!_ready)
        {
            return;
        }

        UpdateVisibility();
        _timer.Stop();
        _timer.Start();
    }

    private void Changed_TextChanged(object sender, TextChangedEventArgs e) => Changed();

    private void Changed_SelectionChanged(object sender, SelectionChangedEventArgs e) => Changed();

    private void Changed_Click(object sender, RoutedEventArgs e) => Changed();

    private void LayerBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => Changed();

    private void ActionBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => Changed();

    private void AllGamesCheck_Click(object sender, RoutedEventArgs e) => Changed();

    private void Help_Click(object sender, RoutedEventArgs e) => _showHelp();

    private void RefreshPreview()
    {
        var lines = RulePreview.Evaluate(_snippet(), BuildRule());
        PreviewCount.Text = lines.Count == 0
            ? "Diese Regel trifft im aktuellen Snippet keinen Namen."
            : $"Diese Regel trifft {lines.Count} Namen im aktuellen Snippet:";
        PreviewList.ItemsSource = lines.Select(BuildLine).ToList();
    }

    /// <summary>Ein Name aus dem Snippet mit farbig markierten Zahlen.</summary>
    private static TextBlock BuildLine(RulePreviewLine line)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.NoWrap };
        var at = 0;
        foreach (var run in line.Runs.OrderBy(r => r.Start))
        {
            if (run.Start > at)
            {
                block.Inlines.Add(new Run(line.Text[at..run.Start]));
            }

            var number = new Run(line.Text.Substring(run.Start, run.Length));
            switch (run.Verdict)
            {
                case RuleVerdict.Counts:
                    number.Foreground = CountsBrush;
                    number.FontWeight = FontWeights.Bold;
                    break;
                case RuleVerdict.NotCounts:
                    number.Foreground = NotCountsBrush;
                    number.TextDecorations = TextDecorations.Strikethrough;
                    break;
                default:
                    number.Foreground = Brushes.Gray;
                    break;
            }

            block.Inlines.Add(number);
            at = run.Start + run.Length;
        }

        if (at < line.Text.Length)
        {
            block.Inlines.Add(new Run(line.Text[at..]));
        }

        return block;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var rule = BuildRule();
        var errors = RuleValidator.Validate(rule).ToList();
        if (CurrentLayer == RuleLayer.AllGames && rule.Games.Count == 0)
        {
            errors.Add("Wähle mindestens ein Spiel oder „alle Spiele“.");
        }

        if (errors.Count > 0)
        {
            ErrorText.Text = string.Join(Environment.NewLine, errors);
            return;
        }

        DialogResult = true;
    }
}
