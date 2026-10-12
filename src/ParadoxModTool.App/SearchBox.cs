using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Settings;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>Suchfeld mit dem Platzhalter "Suchen", wie in den Listen mit Suche (<c>SearchableChoice</c>).</summary>
internal static class SearchBox
{
    /// <summary>Das Feld liegt in einem Grid mit dem Platzhalter; der Platzhalter verschwindet, sobald Text da ist.</summary>
    public static (FrameworkElement Panel, TextBox Box) Create(string? text = null)
    {
        var box = new TextBox { Text = text ?? string.Empty, VerticalContentAlignment = VerticalAlignment.Center, MinHeight = 22 };
        var placeholder = new TextBlock { Text = SettingsTexts.ListSearchPlaceholder, IsHitTestVisible = false, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        placeholder.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        void Update() => placeholder.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        box.TextChanged += (_, _) => Update();
        Update();
        var grid = new Grid();
        grid.Children.Add(box);
        grid.Children.Add(placeholder);
        return (grid, box);
    }
}
