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

    /// <summary>Kopf eines Ordners oder einer Datei (Ansicht A): Zählung wie <see cref="Header"/>, grüner Haken, wenn alles darunter erledigt ist.</summary>
    public static Controls.StackPanel NodeHeader(ActionTreeNode node)
    {
        var panel = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        panel.Children.Add(new Controls.TextBlock { Text = Header(node) });
        if (node.Count > 0 && node.Done == node.Count)
        {
            panel.Children.Add(Check());
        }

        return panel;
    }

    private static Controls.TextBlock Check()
    {
        var check = new Controls.TextBlock { Text = "✔", FontWeight = FontWeights.Bold, Margin = new Thickness(6, 0, 0, 0), ToolTip = CompatchTexts.DoneTip };
        check.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.DiffAdded");
        return check;
    }

    /// <summary>Kopf einer Stelle: Farbpunkt (Plan 5, Ruling 6), Text und bei erledigten Stellen bzw. Blöcken ein grüner Haken (Nutzerwunsch).</summary>
    public static Controls.StackPanel ActionHeader(string token, string text, bool done = false, bool seen = false)
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
        if (done)
        {
            panel.Children.Add(Check());
        }

        // "Gesehen" (Nutzerwunsch): hellblaues Auge statt eines Hakens, damit es sich klar vom grünen "erledigt" unterscheidet.
        if (seen)
        {
            var eye = new Controls.TextBlock { Text = "👁", FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"), Margin = new Thickness(6, 0, 0, 0), ToolTip = CompatchTexts.Seen };
            eye.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.CompatchSeen");
            panel.Children.Add(eye);
        }

        return panel;
    }
}
