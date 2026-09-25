using System.Buffers.Binary;
using System.Collections.Concurrent;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiCoreAdditionsTests
{
    [Theory]
    [InlineData("uberhbox", "hbox")]
    [InlineData("icon", "icon")]
    [InlineData("uber", "uber")]
    public void NormalizeStripsTheUberPrefix(string input, string expected)
    {
        Assert.Equal(expected, GuiWidgetKinds.Normalize(input));
    }

    [Theory]
    [InlineData("ubertextbox", GuiWidgetCategory.Text)]
    [InlineData("uberbutton", GuiWidgetCategory.Button)]
    [InlineData("checkbutton", GuiWidgetCategory.Button)]
    [InlineData("uberwidget", GuiWidgetCategory.Container)]
    [InlineData("container", GuiWidgetCategory.Container)]
    [InlineData("ubervbox", GuiWidgetCategory.Box)]
    [InlineData("fixedgridbox", GuiWidgetCategory.List)]
    [InlineData("uberscrollarea", GuiWidgetCategory.List)]
    [InlineData("window", GuiWidgetCategory.Window)]
    [InlineData("icon", GuiWidgetCategory.Icon)]
    [InlineData("piechart", GuiWidgetCategory.Other)]
    public void CategoryFollowsTheEngineWidget(string baseWidget, GuiWidgetCategory expected)
    {
        Assert.Equal(expected, GuiWidgetKinds.CategoryOf(baseWidget));
    }

    [Fact]
    public void FromDocumentsReusesTheParsedDocuments()
    {
        var index = GuiIndexTests.Build((GuiIndexTests.F("in_game/gui/a.gui"), "types T { type x = icon { } }"));

        var copy = GuiIndex.FromDocuments(index.Files, index.Files.Select(index.DocumentOf).ToList());

        Assert.Same(index.DocumentOf(index.Files[0]), copy.DocumentOf(copy.Files[0]));
        Assert.Equal("icon", copy.Find("x", GuiDefinitionKind.Type)!.BaseName);
        Assert.Throws<ArgumentException>(() => GuiIndex.FromDocuments(index.Files, Array.Empty<GuiDocument>()));
    }

    [Fact]
    public void ResolveTypeResolvesAnEmptyInstanceOfTheType()
    {
        var (resolver, _) = GuiResolverTests.Setup(
            "",
            "types T {\n\ttype my_button = button {\n\t\tsize = { 10 20 }\n\t\ttext_single = { }\n\t}\n}");

        var element = resolver.ResolveType("my_button")!;

        Assert.Equal("button", element.BaseWidget);
        Assert.Equal(new[] { "10", "20" }, element.Property("size")!.Items);
        Assert.Equal(GuiOriginKind.Type, element.Property("size")!.Origin.Kind);
        Assert.Equal("text_single", Assert.Single(element.Children).Key);
        Assert.Equal(2, element.Origin.Line);
        Assert.Null(resolver.ResolveType("unknown"));
    }

    [Fact]
    public void TextureLocatorIsSafeForParallelCallers()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pmt-gui-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(temp, "in_game", "gfx", "a.dds");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bytes = new byte[128];
            "DDS "u8.CopyTo(bytes);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 32);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 64);
            File.WriteAllBytes(path, bytes);

            var locator = new GuiTextureLocator(new[] { Path.Combine(temp, "in_game") });
            var results = new ConcurrentBag<(int Width, int Height)?>();
            Parallel.For(0, 2000, i => results.Add(locator.SizeOf(i % 2 == 0 ? "gfx/a.dds" : $"gfx/missing{i % 50}.dds")));

            Assert.Equal(1000, results.Count(r => r == (64, 32)));
            Assert.Equal(1000, results.Count(r => r is null));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
