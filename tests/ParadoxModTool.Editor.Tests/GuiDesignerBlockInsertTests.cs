using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Blocks;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Bausteine aus der Designer-Palette: nie als <c>using</c> oder <c>Name = { }</c>, sondern nur als fertiger Code.</summary>
public sealed class GuiDesignerBlockInsertTests
{
    private static GuiPaletteEntry BlockEntry() =>
        GuiPalette.BlockEntries(new[] { GuiBlockParser.Parse("# block: Fenster mit Tabs\nwindow = { }\n", GuiBlockLayer.Shipped, @"C:\x\a.guiblock") })[0];

    private static string Apply(string source, GuiDesignerAction action)
    {
        var document = GuiParser.Parse(source);
        var resolver = new GuiResolver(GuiIndex.Empty, GuiRules.Empty, document, null);
        var edit = GuiDesignerController.Compute(action, document, document, resolver, 0);
        Assert.NotNull(edit);
        return TextEdit.Apply(document.Text, edit!.Edits);
    }

    [Fact]
    public void Compute_BlockEntryWithCode_InsertsTheCode()
    {
        var text = Apply("a = 1\n", new GuiDesignerAction(GuiDesignerActionKind.Insert, null) { Entry = BlockEntry(), Code = "button = { }\n" });

        Assert.Contains("button = { }", text);
        Assert.DoesNotContain("Fenster mit Tabs", text);
    }

    [Fact]
    public void Compute_BlockEntryWithoutCode_DoesNothing()
    {
        var document = GuiParser.Parse("template t {\n}\n");
        var resolver = new GuiResolver(GuiIndex.Empty, GuiRules.Empty, document, null);

        var edit = GuiDesignerController.Compute(
            new GuiDesignerAction(GuiDesignerActionKind.Insert, null) { Entry = BlockEntry(), Target = document.Root.Children[0] },
            document,
            document,
            resolver,
            0);

        Assert.NotNull(edit);
        Assert.True(edit!.IsEmpty);
    }

    [Fact]
    public void Compute_TemplateEntry_StillAddsUsing()
    {
        const string source = "template t {\n}\n";
        var document = GuiParser.Parse(source);
        var template = new GuiPaletteEntry("shiny", GuiPaletteKind.Template, "x.gui", GuiLayer.Vanilla, 0, GuiWidgetCategory.Other, null);
        var resolver = new GuiResolver(GuiIndex.Empty, GuiRules.Empty, document, null);

        var edit = GuiDesignerController.Compute(
            new GuiDesignerAction(GuiDesignerActionKind.Insert, null) { Entry = template, Target = document.Root.Children[0] },
            document,
            document,
            resolver,
            0);

        Assert.Contains("using = shiny", TextEdit.Apply(source, edit!.Edits));
    }
}
