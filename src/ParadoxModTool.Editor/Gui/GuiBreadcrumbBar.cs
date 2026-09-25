using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Gui;

/// <summary>Brotkrumen-Leiste über dem Code von .gui-Dateien: Pfad zum Element am Cursor, jedes Glied springt zur Stelle, rechts "Inspizieren".</summary>
public sealed class GuiBreadcrumbBar : UserControl
{
    private readonly TextBlock _path = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Margin = new Thickness(6, 2, 6, 2),
    };
    private readonly Button _inspect = new() { Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(0, 1, 2, 1), IsEnabled = false };

    public GuiBreadcrumbBar()
    {
        _inspect.Content = GuiTexts.ContextInspect;
        _inspect.ToolTip = GuiTexts.BreadcrumbInspectTip;
        _inspect.Click += (_, _) => InspectClicked?.Invoke(this, EventArgs.Empty);
        var panel = new DockPanel();
        DockPanel.SetDock(_inspect, Dock.Right);
        panel.Children.Add(_inspect);
        panel.Children.Add(_path);
        var border = new Border { Child = panel, BorderThickness = new Thickness(0, 0, 0, 1) };
        border.SetResourceReference(Border.BackgroundProperty, "Theme.PanelBackground");
        border.SetResourceReference(Border.BorderBrushProperty, "Theme.Separator");
        Content = border;
        SetCrumbs(Array.Empty<GuiCrumb>(), canInspect: false);
    }

    public event EventHandler<GuiNode>? CrumbClicked;

    public event EventHandler? InspectClicked;

    /// <summary>Sperrt oder erlaubt "Inspizieren", ohne den Pfad zu ändern (Code mit Fehlern: Pfad bleibt, Knopf gesperrt; Plan-Ruling 12).</summary>
    public void SetInspectEnabled(bool enabled) => _inspect.IsEnabled = enabled;

    public void SetCrumbs(IReadOnlyList<GuiCrumb> crumbs, bool canInspect)
    {
        _inspect.IsEnabled = canInspect;
        _path.Inlines.Clear();
        if (crumbs.Count == 0)
        {
            _path.Inlines.Add(Muted(GuiTexts.BreadcrumbNone));
            return;
        }

        for (var i = 0; i < crumbs.Count; i++)
        {
            if (i > 0)
            {
                _path.Inlines.Add(Muted(" › "));
            }

            var crumb = crumbs[i];
            var link = new Hyperlink(new Run(crumb.Label)) { TextDecorations = null };
            link.SetResourceReference(TextElement.ForegroundProperty, "Theme.Text");
            link.Click += (_, _) => CrumbClicked?.Invoke(this, crumb.Node);
            _path.Inlines.Add(link);
        }
    }

    private static Run Muted(string text)
    {
        var run = new Run(text);
        run.SetResourceReference(TextElement.ForegroundProperty, "Theme.TextMuted");
        return run;
    }
}
