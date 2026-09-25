using System.Windows;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>Farblegende des Merge-Fensters (Ansichten A, B, C): dieselben Theme-Tokens und Tönungen wie Spalten, Karten und Dokument.</summary>
internal static class CompatchLegend
{
    public static Controls.WrapPanel Build(string modA, string modB)
    {
        var panel = new Controls.WrapPanel { VerticalAlignment = System.Windows.VerticalAlignment.Center, ToolTip = CompatchTexts.LegendTip };
        Add(panel, "CompatchA", CompatchTexts.LegendA(modA));
        Add(panel, "CompatchB", CompatchTexts.LegendB(modB));
        Add(panel, "CompatchReview", CompatchTexts.LegendReview);
        Add(panel, "CompatchConflict", CompatchTexts.LegendConflict);
        Add(panel, "CompatchVanilla", CompatchTexts.LegendGap);
        return panel;
    }

    private static void Add(Controls.Panel panel, string token, string text)
    {
        var item = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0) };
        item.Children.Add(new Controls.Border
        {
            Width = 12,
            Height = 12,
            Background = CompatchWindow.Tint(token),
            BorderBrush = ThemeManager.GetBrush(token),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
        });
        item.Children.Add(new Controls.TextBlock { Text = text, VerticalAlignment = System.Windows.VerticalAlignment.Center });
        panel.Children.Add(item);
    }
}
