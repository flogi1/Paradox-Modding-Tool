using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Gemeinsame Befundliste von Designer und Inspektor (<see cref="GuiFindingsPanel"/>): Gruppen, Klick, Marken je Element.</summary>
public sealed class GuiFindingsPanelTests
{
    private static (GuiNode Window, GuiNode Inner) Nodes()
    {
        var document = GuiParser.Parse("window = { name = \"main\" widget = { name = \"inner\" } }");
        var window = document.Root.Children[0];
        return (window, window.Children.First(c => c.Name == "widget"));
    }

    private static GuiFinding Finding(GuiFindingSeverity severity, string message, GuiNode? node) =>
        new(severity == GuiFindingSeverity.Hint ? GuiFindingKind.PreviewNote : GuiFindingKind.TextureMissing, severity, message, node, "a.gui", node?.Line ?? 0);

    /// <summary>Texte der Listeneinträge in Reihenfolge: Überschriften als Text, Befundzeilen als ihre Meldung.</summary>
    private static List<string> Entries(GuiFindingsPanel panel) =>
        ((ListBox)panel.Content).Items.Cast<object>()
            .Select(item => item is ListBoxItem { Tag: GuiFinding finding } ? finding.Message : ((TextBlock)item).Text)
            .ToList();

    [Fact]
    public void GroupsErrorsBeforeHints()
    {
        Sta.Run(() =>
        {
            var (window, inner) = Nodes();
            var panel = new GuiFindingsPanel { JominiHeadings = true };

            panel.SetFindings(new[]
            {
                Finding(GuiFindingSeverity.Hint, "note", inner),
                Finding(GuiFindingSeverity.Warning, "warning", window),
                Finding(GuiFindingSeverity.Error, "error", inner),
            });

            Assert.Equal(Visibility.Visible, panel.Visibility);
            Assert.Equal(GuiTexts.FindingsHeader(3), panel.HeaderText);
            Assert.Equal(new[] { GuiTexts.FindingsGroupProblems, "error", "warning", GuiTexts.FindingsGroupPreviewNotes, "note" }, Entries(panel));
            Assert.Equal(new[] { "error", "warning", "note" }, panel.Rows.Select(r => ((GuiFinding)r.Tag).Message));

            // Klassisch heißen die Hinweise "Hinweise"; ohne Hinweise fehlt die Überschrift.
            panel.JominiHeadings = false;
            Assert.Equal(new[] { GuiTexts.FindingsGroupProblems, "error", "warning", GuiTexts.FindingsGroupNotes, "note" }, Entries(panel));
            panel.SetFindings(new[] { Finding(GuiFindingSeverity.Error, "error", inner) });
            Assert.Equal(new[] { GuiTexts.FindingsGroupProblems, "error" }, Entries(panel));
            panel.SetFindings(Array.Empty<GuiFinding>());
            Assert.Equal(new[] { GuiTexts.FindingsNone }, Entries(panel));
            Assert.Equal(GuiTexts.FindingsHeader(0), panel.HeaderText);
        });
    }

    [Fact]
    public void Activate_RaisesFindingActivated()
    {
        Sta.Run(() =>
        {
            var (_, inner) = Nodes();
            var panel = new GuiFindingsPanel();
            var finding = Finding(GuiFindingSeverity.Error, "error", inner);
            panel.SetFindings(new[] { finding });
            GuiFinding? activated = null;
            panel.FindingActivated += (_, f) => activated = f;

            panel.Rows[0].RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });

            Assert.Same(finding, activated);
        });
    }

    [Fact]
    public void Marks_WorstSeverityPerNode()
    {
        var (window, inner) = Nodes();
        var hintOnly = GuiParser.Parse("widget = { }").Root.Children[0];

        var marks = GuiFindingsPanel.Marks(new[]
        {
            Finding(GuiFindingSeverity.Warning, "w", inner),
            Finding(GuiFindingSeverity.Error, "e", inner),
            Finding(GuiFindingSeverity.Hint, "h", inner),
            Finding(GuiFindingSeverity.Warning, "w2", window),
            Finding(GuiFindingSeverity.Hint, "h2", hintOnly),
            Finding(GuiFindingSeverity.Error, "no node", null),
        });

        Assert.Equal(2, marks.Count);
        Assert.Equal(GuiFindingSeverity.Error, marks[inner]);
        Assert.Equal(GuiFindingSeverity.Warning, marks[window]);
        Assert.False(marks.ContainsKey(hintOnly));
    }
}
