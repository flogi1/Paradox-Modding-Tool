using System.IO;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Blocks;
using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Dialog "Neue GUI-Datei" (Plan 3a Ruling 10): Dateiname, Kontext (Plan 3b Ruling 2: die Datei entsteht in <c>&lt;Mod&gt;/&lt;Kontext&gt;/gui</c>),
/// Baustein (docs/superpowers/specs/2026-09-27-gui-building-blocks-design.md), Zielordner und Hinweis auf das nötige Skript.
/// </summary>
public sealed class GuiNewFileWindow : Window
{
    private readonly string _modRoot;
    private readonly ComboBox _context = new() { SelectedIndex = 0, MinWidth = 160, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 8) };
    private readonly TextBox _name = new() { MinWidth = 280, Margin = new Thickness(0, 2, 0, 8) };
    private readonly TextBox _search = new() { Width = 280, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2) };

    // Feste Größe und Texteinträge statt ComboBoxItems mit TextBlock: Deren Auswahlanzeige (VisualBrush) ließ das Fenster mit SizeToContent endlos wachsen.
    private readonly ListBox _list = new() { Width = 280, Height = 150, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2) };
    private readonly TextBlock _description = new() { TextWrapping = TextWrapping.Wrap, Width = 280, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private readonly IReadOnlyList<GuiBlock> _blocks;
    private readonly GuiLayout _layout;
    private List<GuiBlock> _offered = new();

    /// <param name="blocks">Wirksame Bausteine der Bibliothek; angeboten werden die fehlerfreien, vorgewählt "Fenster", sonst "Leere Datei".</param>
    /// <param name="layout">Kontextordner des Spiels; im flachen Layout (Crusader Kings III) gibt es keine Kontextauswahl, die Datei kommt nach gui/.</param>
    /// <param name="layout">Kontextordner des Spiels; im flachen Layout (Crusader Kings III) gibt es keine Kontextauswahl, die Datei kommt nach gui/.</param>
    public GuiNewFileWindow(string modRoot, IReadOnlyList<GuiBlock> blocks, GuiLayout? layout = null)
    {
        _modRoot = modRoot;
        _layout = layout ?? GuiLayout.Default;
        _context.ItemsSource = _layout.Contexts;
        _context.SelectedIndex = _layout.IsFlat ? -1 : 0;
        _blocks = blocks.Where(b => b.IsValid).ToList();
        Title = GuiTexts.NewFileTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _search.ToolTip = GuiTexts.BlocksSearchTip;
        _search.TextChanged += (_, _) => Filter();
        _list.SelectionChanged += (_, _) => _description.Text = Block?.Description ?? string.Empty;
        _description.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _error.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
        Filter(_blocks.FirstOrDefault(b => b.Name == "Fenster") ?? _blocks.FirstOrDefault(b => b.IsBuiltIn));

        var target = new TextBlock { Text = GuiTexts.NewFileTarget(Folder), TextWrapping = TextWrapping.Wrap, MaxWidth = 460, Margin = new Thickness(0, 0, 0, 6) };
        _context.ToolTip = GuiTexts.NewFileContextTip;
        _context.SelectionChanged += (_, _) => target.Text = GuiTexts.NewFileTarget(Folder);
        target.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        var hint = new TextBlock { Text = GuiTexts.NewFileHint, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");

        var create = new Button { Content = GuiTexts.NewFileCreate, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2) };
        create.Click += (_, _) =>
        {
            if (Validate())
            {
                DialogResult = true;
            }
        };
        var cancel = new Button { Content = GuiTexts.NewFileCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(create);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = GuiTexts.NewFileName });
        panel.Children.Add(_name);
        if (!_layout.IsFlat)
        {
            panel.Children.Add(new TextBlock { Text = GuiTexts.NewFileContext });
            panel.Children.Add(_context);
        }

        panel.Children.Add(new TextBlock { Text = GuiTexts.NewFileBlock });
        panel.Children.Add(_search);
        panel.Children.Add(_list);
        panel.Children.Add(_description);
        panel.Children.Add(target);
        panel.Children.Add(hint);
        panel.Children.Add(_error);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => _name.Focus();
    }

    /// <summary>Dateiname mit Endung .gui.</summary>
    public string FileName
    {
        get
        {
            var name = _name.Text.Trim();
            return name.EndsWith(".gui", StringComparison.OrdinalIgnoreCase) ? name : name + ".gui";
        }
    }

    /// <summary>Gewählter Baustein, aus dem die Datei entsteht; <c>null</c>, wenn die Suche nichts findet.</summary>
    public GuiBlock? Block => _list.SelectedIndex >= 0 && _list.SelectedIndex < _offered.Count ? _offered[_list.SelectedIndex] : null;

    /// <summary>Die nach der Suche angebotenen Bausteine in Listenreihenfolge.</summary>
    public IReadOnlyList<GuiBlock> OfferedBlocks => _offered;

    /// <summary>Suchtext (filtert nach Name, Gruppe und Beschreibung).</summary>
    public string SearchText
    {
        get => _search.Text;
        set => _search.Text = value;
    }

    /// <summary>Füllt die Liste neu; die Auswahl bleibt, solange der Baustein noch passt, sonst der erste.</summary>
    private void Filter(GuiBlock? select = null)
    {
        select ??= Block;
        var filter = _search.Text.Trim();
        _offered = _blocks.Where(b => filter.Length == 0
            || b.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || b.Group.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || b.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        _list.ItemsSource = _offered.Select(Label).ToList();
        var index = select is null ? -1 : _offered.IndexOf(select);
        _list.SelectedIndex = index >= 0 ? index : _offered.Count > 0 ? 0 : -1;
        _list.ScrollIntoView(_list.SelectedItem);
    }

    private static string Label(GuiBlock block) => block.Layer switch
    {
        GuiBlockLayer.Game => $"{block.Name} {GuiTexts.BlocksLayerGame}",
        GuiBlockLayer.Mod => $"{block.Name} {GuiTexts.BlocksLayerMod}",
        _ => block.Name,
    };

    /// <summary>Ordner der neuen Datei: <c>&lt;Mod&gt;/&lt;Kontext&gt;/gui</c>.</summary>
    public string Folder => GuiContexts.GuiFolder(_modRoot, _context.SelectedItem as string ?? GuiContexts.DefaultOf(_layout));

    private bool Validate()
    {
        var name = _name.Text.Trim();
        var stem = name.EndsWith(".gui", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        string? error = null;
        if (stem.Trim().Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = GuiTexts.NewFileInvalidName;
        }
        else if (File.Exists(Path.Combine(Folder, FileName)))
        {
            error = GuiTexts.NewFileExists(FileName);
        }
        else if (Block is null)
        {
            error = GuiTexts.NewFileNoBlock;
        }

        _error.Text = error ?? string.Empty;
        _error.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        return error is null;
    }
}
