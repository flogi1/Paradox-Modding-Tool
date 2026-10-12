using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie 2 (Layout): Fensterlayout (Kacheln/Klassisch), Position der kleinen Werkzeugleiste (Leistenposition), Reiter einfärben und Symbolfarben für das aktuelle Theme (Farbfeld öffnet die Farbwahl, "Zurücksetzen" = Standard).</summary>
public sealed class LayoutPage : SettingsPage
{
    public LayoutPage(ISettingsHost host)
        : base(host)
    {
        AddHeading(SettingsTexts.WindowLayoutHeading);
        AddRadio("windowLayout", SettingsTexts.WindowLayoutTiles, () => Host.Settings.WindowLayout == "Tiles", () => Host.Settings.WindowLayout = "Tiles");
        AddRadio("windowLayout", SettingsTexts.WindowLayoutClassic, () => Host.Settings.WindowLayout == "Classic", () => Host.Settings.WindowLayout = "Classic");
        AddHint(SettingsTexts.WindowLayoutHint);

        // Die Position gilt nur für die kleine Werkzeugleiste des neuen Layouts; im klassischen Layout ist das Feld ausgeblendet.
        var positionPanel = new StackPanel();
        AddHeading(SettingsTexts.ToolbarPositionHeading, positionPanel);
        AddPositionRadio("Top", SettingsTexts.ToolbarPositionTop, positionPanel);
        AddPositionRadio("Left", SettingsTexts.ToolbarPositionLeft, positionPanel);
        AddPositionRadio("Right", SettingsTexts.ToolbarPositionRight, positionPanel);
        AddPositionRadio("Bottom", SettingsTexts.ToolbarPositionBottom, positionPanel);
        AddHint(SettingsTexts.ToolbarPositionHint, positionPanel);
        Root.Children.Add(positionPanel);
        OnRefresh(() => positionPanel.Visibility = Host.Settings.WindowLayout == "Tiles" ? Visibility.Visible : Visibility.Collapsed);

        AddCheckBox(SettingsTexts.ColorizeWholeTab, SettingsTexts.ColorizeWholeTabTip, () => Host.Settings.ColorizeWholeTab, value => Host.Settings.ColorizeWholeTab = value);

        var heading = AddHeading(string.Empty);
        OnRefresh(() => heading.Text = SettingsTexts.IconColorsHeading(Host.CurrentThemeName));
        AddHint(SettingsTexts.IconColorsHint);
        foreach (var iconId in IconIds.All)
        {
            AddIconRow(iconId);
        }

        RefreshOnSettings();
        RefreshOn(handler => Host.ThemeChanged += handler, handler => Host.ThemeChanged -= handler);
    }

    internal static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private void AddPositionRadio(string position, string label, Panel parent) =>
        AddRadio("toolbarPosition", label, () => Host.Settings.ToolbarPosition == position, () => Host.Settings.ToolbarPosition = position, parent);

    private void AddIconRow(string iconId)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
        row.Children.Add(new TextBlock { Text = SettingsTexts.IconLabel(iconId), Width = 140, VerticalAlignment = VerticalAlignment.Center });

        var chip = new Border { Width = 36, Height = 14, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
        var swatch = new Button { Content = chip, Tag = iconId, ToolTip = SettingsTexts.PickColorTip, Padding = new Thickness(4, 2, 4, 2) };
        swatch.Click += (_, _) =>
        {
            if (Host.PickColor(Host.IconColor(iconId)) is { } color)
            {
                Host.Settings.SetIconColor(Host.CurrentThemeId, iconId, Hex(color));
            }
        };

        var reset = new Button { Content = Label(SettingsTexts.Reset), Tag = iconId, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(8, 0, 0, 0) };
        reset.Click += (_, _) => Host.Settings.SetIconColor(Host.CurrentThemeId, iconId, null);

        OnRefresh(() =>
        {
            chip.Background = new SolidColorBrush(Host.IconColor(iconId));
            reset.IsEnabled = Host.Settings.GetIconColor(Host.CurrentThemeId, iconId) is not null;
        });

        row.Children.Add(swatch);
        row.Children.Add(reset);
        Root.Children.Add(row);
    }
}
