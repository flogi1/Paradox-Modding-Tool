using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Entwurf auf Abhängigkeits-Basis (3c-Review): die Kopie der Abhängigkeit ist dieselbe Datei und zählt nicht doppelt.</summary>
public sealed class GuiDraftUsageTests
{
    private const string Text = "types T { type my = button { size = { 1 1 } } }\nmy = { }\nmy = { }\n";

    [Fact]
    public void DraftOnDependencyBase_CountsItsUsagesOnce()
    {
        var vanilla = GuiIndexTests.F("in_game/gui/a.gui");
        var dependency = GuiIndexTests.F("in_game/gui/a.gui", GuiLayer.Dependency);
        var other = GuiIndexTests.F("in_game/gui/b.gui", GuiLayer.Mod);
        var index = GuiIndexTests.Build((vanilla, Text), (dependency, Text), (other, "my = { }\n"));
        var draft = GuiParser.Parse(Text);
        var inType = draft.Root.Descendants().First(n => n.Name == "size");

        Assert.Equal(3, GuiEditScope.Of(inType, index, vanilla.FullPath).Usages);
        Assert.Equal(1, GuiTypeEdits.UsagesElsewhere(index, "my", vanilla.FullPath));
    }
}
