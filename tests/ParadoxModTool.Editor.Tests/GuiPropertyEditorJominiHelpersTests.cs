using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
using JominiBorderSketch = ParadoxModTool.Editor.Gui.JominiBorderSketch;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Bildleiste unter frame/framesize/frame_grid und Randskizze unter spriteborder im Jomini-Eigenschaftenfeld.</summary>
[Collection("UiLanguage")]
public sealed class GuiPropertyEditorJominiHelpersTests
{
    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var nested in Tree(child))
            {
                yield return nested;
            }
        }
    }

    private static JominiSpriteVisual Sprite(int width, int height, JominiBorder border = default) =>
        new("gfx/interface/x.dds", null, width, height, new LayoutRect(0, 0, width, height), 1, JominiSpriteKind.Stretched, border, JominiFit.Fill,
            false, false, new JominiColor(1, 1, 1, 1), 1, JominiSpritePlaceholder.None, null);

    private static (GuiPropertyEditor Editor, List<GuiDesignerAction> Actions, GuiNode Node, ResolvedElement Element) Show(
        string body, string row, bool editable = true, JominiSpriteVisual? sprite = null, bool noSprite = false,
        Func<GuiNode, ResolvedElement, ResolvedProperty>? shown = null)
    {
        var text = $"widget = {{\n\tname = \"w\"\n{body}\n}}\n";
        var file = new GuiSourceFile(@"C:\mod\gui\a.gui", "gui/a.gui", GuiLayer.Mod, "Mod", 0, 0);
        var index = GuiIndex.Build(new[] { file }, _ => text);
        var node = index.DocumentOf(file).Root.Children[0];
        var element = new GuiResolver(index, GuiRules.Empty).Resolve(node);
        var editor = new GuiPropertyEditor();
        var actions = new List<GuiDesignerAction>();
        editor.ActionRequested += (_, action) => actions.Add(action);
        editor.SetElement(node, element, editable, jominiSprite: noSprite ? null : sprite ?? Sprite(40, 40));
        editor.SetProperty(shown?.Invoke(node, element) ?? element.Property(row) ?? new ResolvedProperty
        {
            Name = row,
            Role = GuiRole.Property,
            Origin = new GuiOrigin(GuiOriginKind.Here, "", null, 0, node),
        });
        return (editor, actions, node, element);
    }

    private static JominiFrameStrip? Strip(GuiPropertyEditor editor) => Tree(editor).OfType<JominiFrameStrip>().SingleOrDefault();

    private static JominiBorderSketch? Sketch(GuiPropertyEditor editor) => Tree(editor).OfType<JominiBorderSketch>().SingleOrDefault();

    private static List<TextBox> Boxes(GuiPropertyEditor editor) => Tree(editor).OfType<TextBox>().ToList();

    [Fact]
    public void FrameStrip_Shown_ForFrameProperties()
    {
        Sta.Run(() =>
        {
            foreach (var row in new[] { "frame", "framesize", "frame_grid" })
            {
                var (editor, _, _, _) = Show("\tframe = 2\n\tframesize = { 20 20 }\n\tframe_grid = { 2 2 }\n\tsize = { 10 10 }", row);
                Assert.NotNull(Strip(editor));
                Assert.Null(Sketch(editor));
            }

            var (sized, _, _, _) = Show("\tframe = 2\n\tsize = { 10 10 }", "size");
            Assert.Null(Strip(sized));
            Assert.Null(Sketch(sized));
        });
    }

    [Fact]
    public void FrameStrip_HighlightsCurrentFrame()
    {
        Sta.Run(() =>
        {
            var (editor, _, _, _) = Show("\tframe = 2\n\tframesize = { 20 20 }", "frame");
            var strip = Strip(editor)!;
            Assert.Equal(4, strip.Layout!.Value.Count);
            Assert.Equal(2, strip.Highlighted);
            Assert.Null(strip.Message);

            // Live: das Feld der Zeile frame bestimmt die Hervorhebung.
            Boxes(editor).Single().Text = "0";
            Assert.Equal(1, strip.Highlighted);
        });
    }

    [Fact]
    public void FrameStripClick_WritesFrame()
    {
        Sta.Run(() =>
        {
            var (editor, actions, node, _) = Show("\tframe = 1\n\tframesize = { 20 20 }", "frame");
            Strip(editor)!.ClickFrame(3);
            var action = actions.Single();
            Assert.Equal(GuiDesignerActionKind.SetValue, action.Kind);
            Assert.Equal("3", action.Value);
            Assert.NotSame(node, action.Node);

            var (without, written, widget, _) = Show("\tframesize = { 20 20 }", "frame");
            Strip(without)!.ClickFrame(3);
            var set = written.Single();
            Assert.Equal(GuiDesignerActionKind.SetProperty, set.Kind);
            Assert.Same(widget, set.Node);
            Assert.Equal("frame", set.Name);
            Assert.Equal("3", set.Value);
        });
    }

    [Fact]
    public void FrameStripClick_FromFrameSizeRow_WritesFrameNode()
    {
        Sta.Run(() =>
        {
            var (editor, actions, _, element) = Show("\tframe = 1\n\tframesize = { 20 20 }", "framesize");
            Strip(editor)!.ClickFrame(4);
            var action = actions.Single();
            Assert.Equal(GuiDesignerActionKind.SetValue, action.Kind);
            Assert.Same(element.Property("frame")!.Origin.Node, action.Node);
            Assert.Equal("4", action.Value);
        });
    }

    [Fact]
    public void FrameStripClick_FromFrameRow_WritesShownRow()
    {
        Sta.Run(() =>
        {
            // Zwei frame-Zeilen: gezeigt wird die, die nicht die wirksame ist. Der Klick schreibt wie Übernehmen an die gezeigte Zeile.
            GuiNode? shownNode = null;
            var (editor, actions, _, element) = Show(
                "\tframe = 1\n\tframe = 2\n\tframesize = { 20 20 }",
                "frame",
                shown: (widget, resolved) =>
                {
                    var effective = resolved.Property("frame")!;
                    shownNode = widget.Children.First(c => c.Name == "frame" && !ReferenceEquals(c, effective.Origin.Node));
                    return new ResolvedProperty
                    {
                        Name = "frame",
                        Value = shownNode.Value ?? string.Empty,
                        Role = GuiRole.Property,
                        Origin = new GuiOrigin(GuiOriginKind.Here, "", null, 0, shownNode),
                    };
                });
            Assert.NotSame(element.Property("frame")!.Origin.Node, shownNode);

            Strip(editor)!.ClickFrame(3);
            var action = actions.Single();
            Assert.Equal(GuiDesignerActionKind.SetValue, action.Kind);
            Assert.Same(shownNode, action.Node);
            Assert.Equal("3", action.Value);
        });
    }

    [Fact]
    public void FrameStripClick_ReadOnly_WritesNothing()
    {
        Sta.Run(() =>
        {
            var (editor, actions, _, _) = Show("\tframe = 1\n\tframesize = { 20 20 }", "frame", editable: false);
            var strip = Strip(editor)!;
            Assert.False(strip.ClicksEnabled);
            strip.ClickFrame(3);
            Assert.Empty(actions);
        });
    }

    [Fact]
    public void FrameStrip_LiveFrameSize()
    {
        Sta.Run(() =>
        {
            var (editor, _, _, _) = Show("\tframesize = { 40 40 }", "framesize");
            var strip = Strip(editor)!;
            Assert.Equal(1, strip.Layout!.Value.Count);

            var boxes = Boxes(editor);
            boxes[0].Text = "20";
            boxes[1].Text = "20";
            Assert.Equal(4, strip.Layout!.Value.Count);

            // Halb getippt: kein Raster, keine Bilder.
            boxes[1].Text = "";
            Assert.Null(strip.Layout);
            Assert.Equal(0, strip.DrawnCount);
        });
    }

    [Fact]
    public void FrameStrip_OutOfRange_HighlightsNone()
    {
        Sta.Run(() =>
        {
            var (editor, _, _, _) = Show("\tframe = 9\n\tframesize = { 20 20 }", "frame");
            var strip = Strip(editor)!;
            Assert.Null(strip.Highlighted);
            Assert.Equal(GuiTexts.FrameStripOutOfRange("9", 4), strip.Message);
            Assert.Equal(4, strip.DrawnCount);

            var (bound, _, _, _) = Show("\tframe = \"[GetFrame]\"\n\tframesize = { 20 20 }", "framesize");
            Assert.Null(Strip(bound)!.Highlighted);
            Assert.Equal(GuiTexts.FrameStripBound, Strip(bound)!.Message);
        });
    }

    [Fact]
    public void FrameStrip_BoundFrame_NoHighlightNoClicks()
    {
        Sta.Run(() =>
        {
            // Aus einer anderen Zeile: die Bindung des Elements gilt, ein Klick ersetzte sie stillschweigend.
            foreach (var row in new[] { "framesize", "frame_grid" })
            {
                var (other, otherActions, _, _) = Show("\tframe = \"[GetFrame]\"\n\tframesize = { 20 20 }\n\tframe_grid = { 2 2 }", row);
                var otherStrip = Strip(other)!;
                Assert.Null(otherStrip.Highlighted);
                Assert.Equal(GuiTexts.FrameStripBound, otherStrip.Message);
                Assert.Equal(4, otherStrip.DrawnCount);
                otherStrip.ClickFrame(2);
                Assert.Empty(otherActions);
            }

            // Zeile frame mit Bindung (Eingabe ist eine editierbare Auswahl): keine Klicks.
            var (boundRow, boundActions, _, _) = Show("\tframe = \"[GetFrame]\"\n\tframesize = { 20 20 }", "frame");
            Assert.Equal(GuiTexts.FrameStripBound, Strip(boundRow)!.Message);
            Assert.True(Strip(boundRow)!.IsBound);
            Strip(boundRow)!.ClickFrame(2);
            Assert.Empty(boundActions);

            // Zeile frame: der getippte Wert entscheidet, live in beide Richtungen.
            var (editor, actions, _, _) = Show("\tframe = 2\n\tframesize = { 20 20 }", "frame");
            var strip = Strip(editor)!;
            var box = Boxes(editor).Single();
            box.Text = "[Other]";
            Assert.Null(strip.Highlighted);
            Assert.Equal(GuiTexts.FrameStripBound, strip.Message);
            strip.ClickFrame(4);
            Assert.Empty(actions);

            box.Text = "2";
            Assert.Equal(2, strip.Highlighted);
            Assert.Null(strip.Message);
            Assert.False(strip.IsBound);
            strip.ClickFrame(3);
            Assert.Equal("3", actions.Single().Value);
        });
    }

    [Fact]
    public void FrameStrip_TooMany_ShowsText()
    {
        Sta.Run(() =>
        {
            // 3000 × 1 Pixel: framesize 1 1 ergibt 3000 Bilder.
            var (editor, _, _, _) = Show("\tframesize = { 1 1 }", "framesize", sprite: Sprite(3000, 1));
            var strip = Strip(editor)!;
            Assert.Equal(3000, strip.Layout!.Value.Count);
            Assert.Equal(GuiTexts.FrameStripTooMany(3000), strip.Message);
            Assert.Equal(0, strip.DrawnCount);
        });
    }

    [Fact]
    public void BorderSketch_Shown_ForBorderProperties()
    {
        Sta.Run(() =>
        {
            foreach (var row in new[] { "spriteborder", "spriteborder_left", "spriteborder_right", "spriteborder_top", "spriteborder_bottom" })
            {
                var (editor, _, _, _) = Show("\tspriteborder = { 10 8 }", row);
                Assert.NotNull(Sketch(editor));
                Assert.Null(Strip(editor));
                Assert.Equal(new JominiBorder(10, 8, 10, 8), Sketch(editor)!.Border);
            }
        });
    }

    [Fact]
    public void BorderSketch_LiveValues()
    {
        Sta.Run(() =>
        {
            var (editor, _, _, _) = Show("\tspriteborder = { 10 8 }", "spriteborder");
            var sketch = Sketch(editor)!;
            var boxes = Boxes(editor);
            boxes[0].Text = "4";
            boxes[1].Text = "4";
            Assert.Equal(new JominiBorder(4, 4, 4, 4), sketch.Border);
            Assert.NotNull(sketch.Geometry);

            var (side, _, _, _) = Show("\tspriteborder = { 10 8 }", "spriteborder_left");
            Boxes(side).Single().Text = "2";
            Assert.Equal(new JominiBorder(2, 8, 10, 8), Sketch(side)!.Border);
        });
    }

    [Fact]
    public void NoSprite_NoHelpers()
    {
        Sta.Run(() =>
        {
            var (frame, _, _, _) = Show("\tframe = 2\n\tframesize = { 20 20 }", "frame", noSprite: true);
            Assert.Null(Strip(frame));
            var (border, _, _, _) = Show("\tspriteborder = { 10 8 }", "spriteborder", noSprite: true);
            Assert.Null(Sketch(border));
        });
    }
}
