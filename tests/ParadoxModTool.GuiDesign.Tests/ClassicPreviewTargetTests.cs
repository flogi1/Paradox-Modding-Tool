using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class ClassicPreviewTargetTests
{
    private static readonly GuiLayout Classic = GuiLayout.Default with { Dialect = GuiDialect.Classic };

    [Fact]
    public void Classic_preview_target_is_window_under_guiTypes()
    {
        var doc = GuiParser.Parse(
            "GUITYPES = {\n  containerWindowType = {\n    name = \"w\"\n    iconType = { name = \"i\" position = { x = 1 y = 2 } }\n  }\n}");
        var position = doc.Root.Descendants().First(n => n.Name == "position");

        var target = GuiPreviewBuilder.PreviewTarget(position, Classic);

        Assert.NotNull(target);
        Assert.Equal("containerWindowType", target!.Name);
    }

    [Fact]
    public void Classic_preview_target_null_in_spriteTypes()
    {
        var doc = GuiParser.Parse("spriteTypes = {\n  spriteType = { name = \"GFX_x\" texturefile = \"gfx/x.dds\" }\n}");
        var texture = doc.Root.Descendants().First(n => n.Name == "texturefile");

        Assert.Null(GuiPreviewBuilder.PreviewTarget(texture, Classic));
        Assert.Null(GuiPreviewBuilder.PreviewTarget(doc.Root.Children[0], Classic));
    }

    [Fact]
    public void Jomini_preview_target_unchanged()
    {
        var doc = GuiParser.Parse("types M { type t = widget { icon = { size = { 1 1 } } } }\nwindow = { hbox = { icon = { } } }");
        var size = doc.Root.Descendants().First(n => n.Name == "size");
        var icon = doc.Root.Descendants().Last(n => n.Name == "icon");

        Assert.Equal("t", GuiPreviewBuilder.PreviewTarget(size, GuiLayout.Default)!.Name);
        Assert.Equal("window", GuiPreviewBuilder.PreviewTarget(icon, GuiLayout.Default)!.Name);
        Assert.Null(GuiPreviewBuilder.PreviewTarget(doc.Root.Children[0], GuiLayout.Default));

        // Die alte Überladung ohne Layout bleibt Jomini.
        Assert.Equal("window", GuiPreviewBuilder.PreviewTarget(icon)!.Name);
    }

    [Fact]
    public void Classic_usage_context_is_the_window()
    {
        var doc = GuiParser.Parse("guiTypes = { containerWindowType = { name = \"w\" iconType = { name = \"i\" } } }");
        var icon = doc.Root.Descendants().First(n => n.Name == "iconType");

        Assert.Equal(GuiElementTree.LabelOf(icon.Parent!), GuiInspection.ContextOf(icon, Classic));
        Assert.Equal(GuiElementTree.LabelOf(doc.Root.Children[0]), GuiInspection.ContextOf(icon));
    }

    [Theory]
    [InlineData("containerWindowType", GuiWidgetCategory.Container)]
    [InlineData("windowType", GuiWidgetCategory.Container)]
    [InlineData("WindowType", GuiWidgetCategory.Container)]
    [InlineData("iconType", GuiWidgetCategory.Icon)]
    [InlineData("ICONTYPE", GuiWidgetCategory.Icon)]
    [InlineData("buttonType", GuiWidgetCategory.Button)]
    [InlineData("guiButtonType", GuiWidgetCategory.Button)]
    [InlineData("checkboxType", GuiWidgetCategory.Button)]
    [InlineData("instantTextBoxType", GuiWidgetCategory.Text)]
    [InlineData("textboxType", GuiWidgetCategory.Text)]
    [InlineData("editBoxType", GuiWidgetCategory.Text)]
    [InlineData("gridBoxType", GuiWidgetCategory.List)]
    [InlineData("listboxType", GuiWidgetCategory.List)]
    [InlineData("smoothListboxType", GuiWidgetCategory.List)]
    [InlineData("OverlappingElementsBoxType", GuiWidgetCategory.List)]
    public void Classic_keys_get_categories(string key, GuiWidgetCategory expected) =>
        Assert.Equal(expected, GuiWidgetKinds.CategoryOf(key));

    [Fact]
    public void Classic_workspace_creates_a_new_classic_engine_per_call()
    {
        var install = Path.Combine(Path.GetTempPath(), "pmt-classic-engine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(install, "interface"));
        File.WriteAllText(Path.Combine(install, "interface", "a.gui"), "guiTypes = { containerWindowType = { name = \"w\" } }");
        try
        {
            var rules = new GuiRules(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Classic);
            var classic = GuiWorkspace.Create(new GuiWorkspaceOptions(install, null, Array.Empty<string>(), rules, null));
            var jomini = GuiWorkspace.Create(new GuiWorkspaceOptions(install, null, Array.Empty<string>(), GuiResolverTests.Rules, null));

            var first = classic.CreateLayoutEngine();
            Assert.IsType<Classic.ClassicLayoutEngine>(first);
            Assert.NotSame(first, classic.CreateLayoutEngine());
            Assert.IsType<Layout.GuiLayoutEngine>(jomini.CreateLayoutEngine());
        }
        finally
        {
            Directory.Delete(install, recursive: true);
        }
    }

    [Fact]
    public void Jomini_categories_unchanged()
    {
        Assert.Equal(GuiWidgetCategory.Window, GuiWidgetKinds.CategoryOf("window"));
        Assert.Equal(GuiWidgetCategory.Box, GuiWidgetKinds.CategoryOf("uberhbox"));
        Assert.Equal(GuiWidgetCategory.Button, GuiWidgetKinds.CategoryOf("button_standard"));
        Assert.Equal(GuiWidgetCategory.Text, GuiWidgetKinds.CategoryOf("textbox"));
        Assert.Equal(GuiWidgetCategory.Other, GuiWidgetKinds.CategoryOf("Window"));
    }
}
