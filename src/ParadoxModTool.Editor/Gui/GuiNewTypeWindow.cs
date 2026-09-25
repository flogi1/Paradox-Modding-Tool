using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;

namespace ParadoxModTool.Editor.Gui;

public enum GuiNewTypeMode
{
    Derive,
    Extract,
    Copy,
}

/// <summary>
/// Name für einen neuen Typ (Plan 3c Ruling 1, 3, 4, 7): Vorschlag, Prüfung auf gültigen und freien Namen. Bei "Neuer Typ" dazu die weiteren
/// Stellen derselben Datei (nicht angehakt) und die Anzahl der Stellen in anderen Dateien, die unverändert bleiben.
/// </summary>
public sealed class GuiNewTypeWindow : Window
{
    private readonly TextBox _name = new() { MinWidth = 320, Margin = new Thickness(0, 2, 0, 8) };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 480, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private readonly List<CheckBox> _spots = new();
    private readonly Func<string, bool> _taken;

    /// <param name="baseLabel">Element bzw. Schlüssel, von dem der Typ ausgeht.</param>
    /// <param name="spots">Weitere Stellen derselben Datei als Anzeigetexte (nur <see cref="GuiNewTypeMode.Derive"/>).</param>
    /// <param name="elsewhere">Verwendungen in anderen Dateien, die unverändert bleiben.</param>
    /// <param name="where">Hinweis, wohin der Typ kommt.</param>
    /// <param name="taken">Name ist schon vergeben.</param>
    public GuiNewTypeWindow(GuiNewTypeMode mode, string baseLabel, string suggestedName, IReadOnlyList<string> spots, int elsewhere, string where, Func<string, bool> taken)
    {
        _taken = taken;
        Title = mode switch
        {
            GuiNewTypeMode.Derive => GuiTexts.NewTypeTitle,
            GuiNewTypeMode.Extract => GuiTexts.NewTypeExtractTitle,
            _ => GuiTexts.NewTypeCopyTitle,
        };
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _name.Text = suggestedName;
        _error.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");

        TextBlock Muted(string text)
        {
            var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 480, Margin = new Thickness(0, 0, 0, 6) };
            block.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            return block;
        }

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = GuiTexts.NewTypeBase(baseLabel), Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(new TextBlock { Text = GuiTexts.NewTypeName });
        panel.Children.Add(_name);
        if (spots.Count > 0)
        {
            panel.Children.Add(new TextBlock { Text = GuiTexts.NewTypeSpots, Margin = new Thickness(0, 0, 0, 2) });
            var list = new StackPanel();
            foreach (var spot in spots)
            {
                var box = new CheckBox { Content = new TextBlock { Text = spot }, Margin = new Thickness(0, 2, 0, 2) };
                _spots.Add(box);
                list.Children.Add(box);
            }

            panel.Children.Add(new ScrollViewer { Content = list, MaxHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 8) });
        }

        if (elsewhere > 0)
        {
            panel.Children.Add(Muted(GuiTexts.NewTypeElsewhere(elsewhere)));
        }

        if (mode == GuiNewTypeMode.Extract)
        {
            panel.Children.Add(Muted(GuiTexts.NewTypeExtractHint));
        }

        panel.Children.Add(Muted(where));
        panel.Children.Add(_error);

        var create = new Button { Content = GuiTexts.NewTypeCreate, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2) };
        create.Click += (_, _) =>
        {
            if (Validate())
            {
                DialogResult = true;
            }
        };
        var cancel = new Button { Content = GuiTexts.NewTypeCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(create);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) =>
        {
            _name.Focus();
            _name.SelectAll();
        };
    }

    public string TypeName => _name.Text.Trim();

    /// <summary>Indizes der angehakten weiteren Stellen, in der Reihenfolge der übergebenen Liste.</summary>
    public IReadOnlyList<int> SelectedSpots => _spots.Select((box, i) => (box, i)).Where(p => p.box.IsChecked == true).Select(p => p.i).ToList();

    private bool Validate()
    {
        var name = TypeName;
        var error = !GuiTypeEdits.IsValidName(name) ? GuiTexts.NewTypeInvalidName
            : _taken(name) ? GuiTexts.NewTypeNameTaken(name)
            : null;
        _error.Text = error ?? string.Empty;
        _error.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        return error is null;
    }
}
