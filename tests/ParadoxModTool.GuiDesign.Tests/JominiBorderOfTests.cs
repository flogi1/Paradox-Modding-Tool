using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Rand für die Randskizze: aus den Eigenschaften, die gerade bearbeitete Zeile durch die getippten Werte ersetzt.</summary>
public sealed class JominiBorderOfTests
{
    private static ResolvedElement Element(string body)
    {
        var text = $"widget = {{\n\tname = \"w\"\n{body}\n}}\n";
        var file = new GuiSourceFile(@"C:\mod\gui\a.gui", "gui/a.gui", GuiLayer.Mod, "Mod", 0, 0);
        var index = GuiIndex.Build(new[] { file }, _ => text);
        return new GuiResolver(index, GuiRules.Empty).Resolve(index.DocumentOf(file).Root.Children[0]);
    }

    [Fact]
    public void BorderOf_FromProperties()
    {
        var element = Element("\tspriteborder = { 10 8 }\n\tspriteborder_left = 3");
        Assert.Equal(new JominiBorder(3, 8, 10, 8), JominiBorderSketchGeometry.BorderOf(element, null, null));
    }

    [Fact]
    public void BorderOf_LivePair_KeepsSingleSides()
    {
        var element = Element("\tspriteborder = { 10 8 }\n\tspriteborder_left = 3");
        Assert.Equal(new JominiBorder(3, 4, 4, 4), JominiBorderSketchGeometry.BorderOf(element, "spriteborder", new[] { "4", "4" }));
    }

    [Fact]
    public void BorderOf_LiveSide()
    {
        var element = Element("\tspriteborder = { 10 8 }");
        Assert.Equal(new JominiBorder(10, 8, 10, 6), JominiBorderSketchGeometry.BorderOf(element, "spriteborder_bottom", new[] { "6" }));
    }
}
