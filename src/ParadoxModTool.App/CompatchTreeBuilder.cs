using System.Windows;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>Kopfzeilen des Ordnerbaums (Ansicht A, B, C): Name mit Zählung, Farbpunkt je Stelle.</summary>
internal static class CompatchTreeBuilder
{
    /// <summary>"Name (12 · 3 Konflikte)" für Ordner und Dateien.</summary>
    public static string Header(ActionTreeNode node) =>
        $"{node.Name} ({node.Count}" + (node.OpenConflicts > 0 ? " · " + CompatchTexts.TreeConflicts(node.OpenConflicts) : string.Empty) + ")";

    /// <summary>Kopf einer Stelle: Farbpunkt (Plan 5, Ruling 6) und Text.</summary>
    public static Controls.StackPanel ActionHeader(string token, string text)
    {
        var panel = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        panel.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = ThemeManager.GetBrush(token),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
        });
        panel.Children.Add(new Controls.TextBlock { Text = text });
        return panel;
    }
}
