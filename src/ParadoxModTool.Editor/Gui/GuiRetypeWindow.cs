using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// "Widget-Schlüssel umstellen" (Plan 3c Ruling 5): Auswahl aus Typen und Engine-Widgets, Freitext möglich. Warnt bei unbekanntem Schlüssel und bei
/// blockoverride-Blöcken, die der neue Schlüssel nicht anbietet; sperrt nur bei ungültigem oder unverändertem Schlüssel.
/// </summary>
public sealed class GuiRetypeWindow : Window
{
    private readonly ComboBox _key = new() { IsEditable = true, IsTextSearchEnabled = true, MinWidth = 320, Margin = new Thickness(0, 2, 0, 8) };
    private readonly TextBlock _warning = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 480, Visibility = Visibility.Collapsed };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 480, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private readonly string _currentKey;

    /// <param name="keys">Typen und Engine-Widgets zur Auswahl.</param>
    /// <param name="warningFor">Warnung zum eingegebenen Schlüssel, null ohne.</param>
    public GuiRetypeWindow(string currentKey, IReadOnlyList<string> keys, Func<string, string?> warningFor)
    {
        _currentKey = currentKey;
        Title = GuiTexts.RetypeTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _key.ItemsSource = keys;
        _key.Text = currentKey;
        _warning.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
        _error.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
        _key.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) =>
        {
            var warning = NewKey.Length == 0 || NewKey == _currentKey ? null : warningFor(NewKey);
            _warning.Text = warning ?? string.Empty;
            _warning.Visibility = warning is null ? Visibility.Collapsed : Visibility.Visible;
        }));

        var apply = new Button { Content = GuiTexts.RetypeApply, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2) };
        apply.Click += (_, _) =>
        {
            if (Validate())
            {
                DialogResult = true;
            }
        };
        var cancel = new Button { Content = GuiTexts.NewTypeCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(apply);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = GuiTexts.RetypeCurrent(currentKey), Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(new TextBlock { Text = GuiTexts.RetypeKey });
        panel.Children.Add(_key);
        panel.Children.Add(_warning);
        panel.Children.Add(_error);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => _key.Focus();
    }

    public string NewKey => (_key.Text ?? string.Empty).Trim();

    private bool Validate()
    {
        var error = !GuiTypeEdits.IsValidName(NewKey) ? GuiTexts.NewTypeInvalidName
            : NewKey == _currentKey ? GuiTexts.RetypeSame
            : null;
        _error.Text = error ?? string.Empty;
        _error.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        return error is null;
    }
}
