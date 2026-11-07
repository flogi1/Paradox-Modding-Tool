using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiResolverEffectBlockTests
{
    private static readonly GuiRules Rules = new(
        new[] { "modify_texture", "glow", "state" },
        new[] { "tooltipwidget" },
        Array.Empty<string>());

    private static ResolvedElement Resolve(string overlay, string library, string key)
    {
        var index = GuiIndexTests.Build((GuiIndexTests.F("in_game/gui/library.gui"), library));
        var doc = GuiParser.Parse(overlay);
        var resolver = new GuiResolver(index, Rules, doc, @"C:\mod\in_game\gui\edit.gui");
        return resolver.Resolve(GuiResolverTests.Top(doc, key));
    }

    [Fact]
    public void ModifyTexture_UsingTemplate_ResolvesTexture()
    {
        var element = Resolve(
            "icon = { modify_texture = { using = color_x_texture blend_mode = overlay } }",
            "template color_x_texture { texture = \"gfx/a.dds\" }",
            "icon");

        var layer = element.StructuredBlocks("modify_texture").Single().Block!;
        Assert.Equal("gfx/a.dds", layer.Property("texture")!.Value);
        Assert.Equal("overlay", layer.Property("blend_mode")!.Value);
        Assert.Equal(GuiRole.StructuredBlock, layer.Role);
        Assert.Equal("modify_texture", layer.Key);
        Assert.Equal("modify_texture", layer.BaseWidget);
        Assert.NotNull(layer.TemplateLookup);
        Assert.NotNull(layer.FilePathLookup);
    }

    [Fact]
    public void ModifyTexture_BlockOverride_ReplacesDefault()
    {
        var library = "types T { type t = icon { modify_texture = { texture = \"a.dds\" block \"unused\" { visible = no } } } }";

        var plain = Resolve("t = { }", library, "t");
        Assert.Equal("no", plain.StructuredBlocks("modify_texture").Single().Block!.Property("visible")!.Value);

        var overridden = Resolve("t = { blockoverride \"unused\" { } }", library, "t");
        Assert.Null(overridden.StructuredBlocks("modify_texture").Single().Block!.Property("visible"));
    }

    [Fact]
    public void ModifyTexture_Constant_Substituted()
    {
        var element = Resolve(
            "@overlay_alpha = 0.4\nicon = { modify_texture = { texture = \"a.dds\" alpha = @overlay_alpha } }",
            string.Empty,
            "icon");

        Assert.Equal("0.4", element.StructuredBlocks("modify_texture").Single().Block!.Property("alpha")!.Value);
    }

    [Fact]
    public void ModifyTexture_Order_TypeThenInstance()
    {
        var element = Resolve(
            "t = { modify_texture = { texture = \"b.dds\" } }",
            "types T { type t = icon { modify_texture = { texture = \"a.dds\" } } }",
            "t");

        Assert.Equal(
            new[] { "a.dds", "b.dds" },
            element.StructuredBlocks("modify_texture").Select(b => b.Block!.Property("texture")!.Value));
    }

    [Fact]
    public void Glow_Using_Resolved()
    {
        var element = Resolve(
            "icon = { glow = { using = glow_t } }",
            "template glow_t { glow_radius = 3 }",
            "icon");

        Assert.Equal("3", element.StructuredBlocks("glow").Single().Block!.Property("glow_radius")!.Value);
    }

    [Fact]
    public void State_HasNoBlock()
    {
        var element = Resolve("icon = { state = { name = x } }", string.Empty, "icon");

        Assert.Null(element.StructuredBlocks("state").Single().Block);
    }
}
