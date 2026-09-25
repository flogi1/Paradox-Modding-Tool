using System.Buffers.Binary;
using System.Diagnostics;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiLayoutEngineTests
{
    private readonly ITestOutputHelper _output;

    public GuiLayoutEngineTests(ITestOutputHelper output) => _output = output;

    private static LayoutBox Layout(string gui, LayoutOptions? options = null, Func<string, (int Width, int Height)?>? textures = null, string library = "")
    {
        var (resolver, doc) = GuiResolverTests.Setup(gui, library);
        var element = resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
        return new GuiLayoutEngine(GuiResolverTests.Rules, textures ?? (_ => null)).Layout(element, options);
    }

    [Fact]
    public void UberEngineWidgetsLayOutLikeTheirPlainNames()
    {
        var box = Layout(
            "hbox = { spacing = 5 widget = { size = { 40 20 } } widget = { size = { 40 20 } } }",
            library: "types D { type hbox = uberhbox { } }");

        Assert.Equal("uberhbox", box.Element.BaseWidget);
        AssertRect(box, 0, 0, 85, 20);
        Assert.Equal(45, box.Children[1].X, 3);
    }

    private static void AssertRect(LayoutBox box, double x, double y, double width, double height)
    {
        Assert.Equal(x, box.X, 3);
        Assert.Equal(y, box.Y, 3);
        Assert.Equal(width, box.Width, 3);
        Assert.Equal(height, box.Height, 3);
    }

    [Fact]
    public void CentersWindowOnScreen()
    {
        AssertRect(Layout("window = { size = { 200 100 } parentanchor = center }"), 860, 490, 200, 100);
    }

    [Fact]
    public void ChildPositionIsRelativeToParent()
    {
        var box = Layout("window = { size = { 200 100 } position = { 10 20 } widget = { size = { 50 50 } position = { 5 6 } } }");

        AssertRect(box.Children[0], 15, 26, 50, 50);
    }

    [Fact]
    public void AnchorsAtBottomRight()
    {
        var box = Layout("window = { size = { 200 100 } icon = { size = { 50 20 } parentanchor = bottom|right position = { -5 -5 } } }");

        AssertRect(box.Children[0], 145, 75, 50, 20);
    }

    [Fact]
    public void PercentSizeUsesTheParentSize()
    {
        var box = Layout("window = { size = { 200 100 } widget = { size = { 50% 100% } } }");

        AssertRect(box.Children[0], 0, 0, 100, 100);
    }

    [Fact]
    public void HboxGivesFreeSpaceToExpandAndCentersVertically()
    {
        var box = Layout("hbox = { size = { 300 50 } spacing = 10 widget = { size = { 50 20 } } expand = { } widget = { size = { 50 20 } } }");

        Assert.Equal(3, box.Children.Count);
        AssertRect(box.Children[0], 0, 15, 50, 20);
        Assert.Equal(60, box.Children[1].X, 3);
        Assert.Equal(180, box.Children[1].Width, 3);
        AssertRect(box.Children[2], 250, 15, 50, 20);
    }

    [Fact]
    public void HboxWithoutSizeFitsChildrenSpacingAndMargin()
    {
        var box = Layout("hbox = { spacing = 5 margin = { 10 4 } widget = { size = { 40 20 } } widget = { size = { 40 20 } } }");

        AssertRect(box, 0, 0, 105, 28);
        Assert.Equal(10, box.Children[0].X, 3);
        Assert.Equal(55, box.Children[1].X, 3);
        Assert.Equal(4, box.Children[0].Y, 3);
    }

    [Fact]
    public void VboxStretchesHorizontallyExpandingChild()
    {
        var box = Layout("vbox = { size = { 100 200 } widget = { size = { 10 10 } layoutpolicy_horizontal = expanding } }");

        AssertRect(box.Children[0], 0, 0, 100, 10);
    }

    [Fact]
    public void IconWithoutSizeUsesTextureSize()
    {
        var box = Layout("icon = { texture = \"gfx/a.dds\" }", textures: path => path == "gfx/a.dds" ? (64, 32) : null);

        AssertRect(box, 0, 0, 64, 32);
        Assert.Empty(box.Notes);
    }

    [Fact]
    public void MissingTextureAddsANote()
    {
        var box = Layout("icon = { texture = \"gfx/missing.dds\" }");

        Assert.Contains(box.Notes, n => n.Contains("gfx/missing.dds"));
        Assert.True(box.Uncertain);
    }

    [Fact]
    public void TextSizeIsEstimatedWithANote()
    {
        var box = Layout("text_single = { text = \"Hallo\" }");

        Assert.Equal(35, box.Width, 3);
        Assert.Equal(20, box.Height, 3);
        Assert.Contains("Textgröße geschätzt", box.Notes);
    }

    [Fact]
    public void GridRepeatsTheItemTemplate()
    {
        var box = Layout("fixedgridbox = { addcolumn = 100 addrow = 40 datamodel = \"[List]\" item = { widget = { size = { 100 40 } } } }");

        Assert.Equal(new[] { 0, 1, 2 }, box.Children.Select(c => c.RepeatIndex));
        Assert.Equal(new[] { 0d, 40d, 80d }, box.Children.Select(c => c.Y));
        AssertRect(box, 0, 0, 100, 120);
    }

    [Fact]
    public void VisibilityFollowsTheOptions()
    {
        const string gui = "window = { size = { 10 10 } widget = { visible = no size = { 1 1 } } widget = { visible = \"[IsShown]\" size = { 1 1 } } }";

        Assert.Single(Layout(gui).Children);
        Assert.Equal(2, Layout(gui, new LayoutOptions { ShowHidden = true }).Children.Count);
        Assert.Empty(Layout(gui, new LayoutOptions { ShowConditional = false }).Children);
    }

    [Fact]
    public void MinimumAndMaximumSizeClamp()
    {
        AssertRect(Layout("window = { size = { 10 500 } minimumsize = { 50 50 } maximumsize = { 400 300 } }"), 0, 0, 50, 300);
    }

    [Fact]
    public void TooltipsAreNotLaidOut()
    {
        Assert.Empty(Layout("button = { size = { 10 10 } tooltipwidget = { widget = { size = { 300 300 } } } }").Children);
    }

    [Fact]
    public void UnresolvableSizeStaysFiniteWithNotes()
    {
        var box = Layout("window = { size = { \"[GetWidth]\" 20 } }");

        Assert.Equal(0, box.Width, 3);
        Assert.Equal(20, box.Height, 3);
        Assert.Contains(box.Notes, n => n.StartsWith("Größe nicht auswertbar", StringComparison.Ordinal));
    }

    [Fact]
    public void DdsHeaderReadsWidthAndHeight()
    {
        var path = Path.Combine(Path.GetTempPath(), "pmt-gui-" + Guid.NewGuid().ToString("N") + ".dds");
        try
        {
            var bytes = new byte[128];
            "DDS "u8.CopyTo(bytes);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 32);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 64);
            File.WriteAllBytes(path, bytes);

            Assert.True(DdsHeader.TryReadSize(path, out var width, out var height));
            Assert.Equal((64, 32), (width, height));

            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            Assert.False(DdsHeader.TryReadSize(path, out _, out _));
            Assert.False(DdsHeader.TryReadSize(path + ".missing", out _, out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LayoutOfEveryTopLevelVanillaWidgetHasFiniteNumbers()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var watch = Stopwatch.StartNew();
        var rules = GuiRulesVanillaTests.ProfileRules();
        var resolver = new GuiResolver(GuiRulesVanillaTests.Vanilla, rules);
        var locator = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(VanillaGui.InstallDirectory, Array.Empty<string>(), null));
        var engine = new GuiLayoutEngine(rules, locator.SizeOf);
        Assert.NotNull(locator.SizeOf("gfx/interface/_default_icon.dds"));

        var boxes = 0;
        var uncertain = 0;
        foreach (var file in GuiRulesVanillaTests.Vanilla.Files)
        {
            foreach (var node in GuiRulesVanillaTests.Vanilla.DocumentOf(file).Root.Children.Where(n => n.Kind == GuiNodeKind.KeyedBlock && n.Name.Length > 0))
            {
                var root = engine.Layout(resolver.Resolve(node));
                foreach (var box in root.Descendants().Prepend(root))
                {
                    boxes++;
                    uncertain += box.Uncertain ? 1 : 0;
                    Assert.True(
                        double.IsFinite(box.X) && double.IsFinite(box.Y) && double.IsFinite(box.Width) && double.IsFinite(box.Height),
                        $"{file.RelativePath}:{node.Line} {box.Element.Key}: {box.X} {box.Y} {box.Width} {box.Height}");
                }
            }
        }

        _output.WriteLine($"{boxes} Kästen, davon {uncertain} mit Hinweis, {watch.ElapsedMilliseconds} ms");
    }
}
