using System.IO;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// "Scripted GUI anlegen" für ein oberstes Fenster (HOI IV, Plan 3 Task 6): Eintragsname, Anzeigeart (über einem Spielfenster, Entscheidungskategorie, anderer
/// Kontext) und ihre Werte, die Knöpfe, die einen leeren <c>_click</c>-Effekt bekommen. Vorgabe ist, was die Probe im Spiel bestätigt hat
/// (<c>player_context</c> mit <c>top_bar</c>); der Hinweis sagt je Anzeigeart, was getestet ist. Die Probleme des <see cref="ScriptedGuiDraft.Plan"/> stehen
/// live in der Warnfarbe, "Anlegen" bleibt gesperrt, solange es welche gibt. Das Fenster schreibt nichts: Es liefert <see cref="Request"/> und
/// <see cref="Plan"/>, geschrieben wird im Hauptfenster (ein Rückgängig-Schritt).
/// </summary>
public sealed class ScriptedGuiWindow : Window
{
    private readonly string _windowName;
    private readonly string _modRoot;
    private readonly ScriptedGuiIndex _index;
    private readonly string? _gameLanguage;
    private readonly TextBox _entry = new() { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ComboBox _placement = new() { Width = 340, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ComboBox _token = new() { Width = 300, HorizontalAlignment = HorizontalAlignment.Left, IsEditable = true };
    private readonly ComboBox _context = new() { Width = 300, HorizontalAlignment = HorizontalAlignment.Left, IsEditable = true };
    private readonly TextBox _categoryId = new() { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _categoryName = new() { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StackPanel _buttons = new();
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 520, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _problems = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 520, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _create = new() { Content = GuiTexts.ScriptedGuiDoCreate, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2) };
    private readonly List<(TextBlock Label, FrameworkElement Input, ScriptedGuiPlacement Placement)> _placementRows = new();
    private bool _loading;
    private bool _categoryIdEdited;

    /// <param name="windowName">Das oberste Fenster (<c>window_name</c>).</param>
    /// <param name="buttonNames">Die Knöpfe im Fenster; alle sind vorgewählt.</param>
    /// <param name="modRoot">Ordner der aktiven Mod (Basisname für Eintrag und Dateien).</param>
    /// <param name="index">Die Scripted GUIs und Kategorien des aktuellen Stands (Namenskollisionen).</param>
    /// <param name="gameLanguage">Spielsprache aus <c>settings.txt</c> (<c>l_german</c>) oder <see langword="null"/>, wenn sie nicht lesbar ist.</param>
    public ScriptedGuiWindow(string windowName, IReadOnlyList<string> buttonNames, string modRoot, ScriptedGuiIndex index, string? gameLanguage)
    {
        _windowName = windowName;
        _modRoot = modRoot;
        _index = index;
        _gameLanguage = string.IsNullOrWhiteSpace(gameLanguage) ? null : gameLanguage.Trim();
        Title = GuiTexts.ScriptedGuiWindowTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _loading = true;
        _placement.ItemsSource = new[] { GuiTexts.ScriptedGuiPlacementParent, GuiTexts.ScriptedGuiPlacementCategory, GuiTexts.ScriptedGuiPlacementOther };
        _placement.SelectedIndex = (int)ScriptedGuiDraft.DefaultPlacement;
        _token.ItemsSource = ScriptedGuiDraft.ParentTokens;
        _token.Text = ScriptedGuiDraft.DefaultParentToken;
        _context.ItemsSource = ScriptedGuiDraft.OtherContextTypes;
        _context.Text = ScriptedGuiDraft.DefaultOtherContext;
        _entry.Text = ScriptedGuiDraft.DefaultEntryName(modRoot, windowName);
        _categoryId.Text = ScriptedGuiDraft.DefaultCategoryId(_entry.Text);
        _categoryName.Text = windowName;
        foreach (var button in buttonNames)
        {
            var box = new CheckBox { Content = button + "_click", Tag = button, IsChecked = true, Margin = new Thickness(0, 1, 0, 1) };
            box.Checked += (_, _) => Refresh();
            box.Unchecked += (_, _) => Refresh();
            _buttons.Children.Add(box);
        }

        if (buttonNames.Count == 0)
        {
            _buttons.Children.Add(new TextBlock { Text = GuiTexts.ScriptedGuiNoButtons, TextWrapping = TextWrapping.Wrap, MaxWidth = 340 });
        }

        _loading = false;
        _hint.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _problems.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");

        _entry.TextChanged += (_, _) =>
        {
            // Die Kategorie-ID folgt dem Eintragsnamen, bis sie selbst geändert wurde.
            if (!_categoryIdEdited)
            {
                _loading = true;
                _categoryId.Text = ScriptedGuiDraft.DefaultCategoryId(EntryName);
                _loading = false;
            }

            Refresh();
        };
        _categoryId.TextChanged += (_, _) =>
        {
            _categoryIdEdited |= !_loading;
            Refresh();
        };
        _categoryName.TextChanged += (_, _) => Refresh();
        _placement.SelectionChanged += (_, _) => Refresh();
        _token.SelectionChanged += (_, _) => Dispatcher.BeginInvoke(Refresh);
        _token.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => Refresh()));
        _context.SelectionChanged += (_, _) => Dispatcher.BeginInvoke(Refresh);
        _context.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => Refresh()));
        _create.Click += (_, _) =>
        {
            if (TryCreate() && IsVisible)
            {
                DialogResult = true;
            }
        };

        Content = Layout();
        Loaded += (_, _) => _entry.Focus();
        Refresh();
    }

    public string EntryName
    {
        get => _entry.Text.Trim();
        set => _entry.Text = value;
    }

    public ScriptedGuiPlacement Placement
    {
        get => _placement.SelectedIndex < 0 ? ScriptedGuiDraft.DefaultPlacement : (ScriptedGuiPlacement)_placement.SelectedIndex;
        set => _placement.SelectedIndex = (int)value;
    }

    public string ParentToken
    {
        get => _token.Text.Trim();
        set => _token.Text = value;
    }

    public string ContextType
    {
        get => _context.Text.Trim();
        set => _context.Text = value;
    }

    public string CategoryId
    {
        get => _categoryId.Text.Trim();
        set => _categoryId.Text = value;
    }

    public string CategoryName
    {
        get => _categoryName.Text.Trim();
        set => _categoryName.Text = value;
    }

    /// <summary>Der Wunsch nach "Anlegen"; <see langword="null"/>, bis <see cref="TryCreate"/> gelungen ist.</summary>
    public ScriptedGuiRequest? Request { get; private set; }

    /// <summary>Der Plan zu <see cref="Request"/>; <see langword="null"/>, bis <see cref="TryCreate"/> gelungen ist.</summary>
    public ScriptedGuiPlan? Plan { get; private set; }

    /// <summary>Der Hinweis, wie er im Fenster steht.</summary>
    internal string HintText => _hint.Text;

    /// <summary>Die Probleme, wie sie im Fenster stehen.</summary>
    internal IReadOnlyList<string> ShownProblems { get; private set; } = Array.Empty<string>();

    internal Button CreateButton => _create;

    /// <summary>"Anlegen": plant noch einmal und setzt <see cref="Request"/> und <see cref="Plan"/>; <see langword="false"/>, wenn es Probleme gibt.</summary>
    internal bool TryCreate()
    {
        var request = CurrentRequest();
        var plan = PlanFor(request);
        if (!plan.CanApply)
        {
            Refresh();
            return false;
        }

        Request = request;
        Plan = plan;
        return true;
    }

    private ScriptedGuiRequest CurrentRequest()
    {
        var placement = Placement;
        var buttons = _buttons.Children.OfType<CheckBox>().Where(b => b.IsChecked == true).Select(b => (string)b.Tag).ToList();
        return new ScriptedGuiRequest(
            _windowName,
            EntryName,
            placement,
            placement == ScriptedGuiPlacement.ParentToken ? ParentToken : null,
            placement == ScriptedGuiPlacement.OtherContext ? ContextType : null,
            placement == ScriptedGuiPlacement.DecisionCategory ? CategoryId : null,
            placement == ScriptedGuiPlacement.DecisionCategory ? CategoryName : null,
            buttons);
    }

    private ScriptedGuiPlan PlanFor(ScriptedGuiRequest request)
    {
        try
        {
            return ScriptedGuiDraft.Plan(request, _modRoot, _index, ReadExisting, _gameLanguage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new ScriptedGuiPlan(Array.Empty<GuiPlanFile>(), string.Empty, 0, new[] { ex.Message });
        }
    }

    private static string? ReadExisting(string path) => File.Exists(path) ? GuiTextReader.Read(path).Text : null;

    private FrameworkElement Layout()
    {
        var form = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        AddRow(form, GuiTexts.ScriptedGuiEntryLabel, _entry);
        AddRow(form, GuiTexts.ScriptedGuiPlacementLabel, _placement);
        _placementRows.Add((AddRow(form, GuiTexts.ScriptedGuiTokenLabel, _token), _token, ScriptedGuiPlacement.ParentToken));
        _placementRows.Add((AddRow(form, GuiTexts.ScriptedGuiContextLabel, _context), _context, ScriptedGuiPlacement.OtherContext));
        _placementRows.Add((AddRow(form, GuiTexts.ScriptedGuiCategoryIdLabel, _categoryId), _categoryId, ScriptedGuiPlacement.DecisionCategory));
        _placementRows.Add((AddRow(form, GuiTexts.ScriptedGuiCategoryNameLabel, _categoryName), _categoryName, ScriptedGuiPlacement.DecisionCategory));
        var buttonsLabel = AddRow(form, GuiTexts.ScriptedGuiButtonsLabel, _buttons);
        buttonsLabel.VerticalAlignment = VerticalAlignment.Top;

        var cancel = new Button { Content = GuiTexts.ScriptedGuiCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(_create);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = GuiTexts.ScriptedGuiWindowLabel(_windowName), FontWeight = FontWeights.SemiBold });
        panel.Children.Add(form);
        panel.Children.Add(_hint);
        panel.Children.Add(_problems);
        panel.Children.Add(buttons);
        return panel;
    }

    /// <summary>Eine Zeile "Beschriftung | Eingabe" am Ende des Formulars; liefert die Beschriftung.</summary>
    private static TextBlock AddRow(Grid grid, string label, FrameworkElement input)
    {
        var row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var block = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 12, 3) };
        Grid.SetRow(block, row);
        Grid.SetRow(input, row);
        Grid.SetColumn(input, 1);
        input.Margin = new Thickness(0, 3, 0, 3);
        grid.Children.Add(block);
        grid.Children.Add(input);
        return block;
    }

    private void Refresh()
    {
        if (_loading)
        {
            return;
        }

        var placement = Placement;
        foreach (var (label, input, shownFor) in _placementRows)
        {
            label.Visibility = input.Visibility = shownFor == placement ? Visibility.Visible : Visibility.Collapsed;
        }

        var plan = PlanFor(CurrentRequest());
        ShownProblems = plan.Problems;
        _problems.Text = string.Join("\n", plan.Problems);
        _problems.Visibility = plan.Problems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _create.IsEnabled = plan.CanApply;

        var hints = new List<string>();
        switch (placement)
        {
            case ScriptedGuiPlacement.ParentToken:
                hints.Add(GuiTexts.ScriptedGuiHintTopBar);
                if (!string.Equals(ParentToken, ScriptedGuiDraft.DefaultParentToken, StringComparison.OrdinalIgnoreCase))
                {
                    hints.Add(GuiTexts.ScriptedGuiHintToken);
                }

                break;
            case ScriptedGuiPlacement.DecisionCategory:
                hints.Add(GuiTexts.ScriptedGuiHintCategory);
                if (_gameLanguage is null)
                {
                    hints.Add(GuiTexts.ScriptedGuiEnglishOnly);
                }
                else if (!string.Equals(_gameLanguage, "l_english", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(_gameLanguage, "english", StringComparison.OrdinalIgnoreCase))
                {
                    hints.Add(GuiTexts.ScriptedGuiLanguageToo(_gameLanguage));
                }

                break;
            default:
                hints.Add(GuiTexts.ScriptedGuiHintOther);
                break;
        }

        _hint.Text = string.Join("\n\n", hints);
    }
}
