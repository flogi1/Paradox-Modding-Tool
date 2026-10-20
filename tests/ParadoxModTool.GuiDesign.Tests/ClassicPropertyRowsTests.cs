using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Eigenschaftenzeilen klassischer Elemente: Gruppen und Werte aus passenden <c>if_resolution</c>-Blöcken (EU IV).</summary>
public sealed class ClassicPropertyRowsTests : IDisposable
{
    private readonly ClassicLayoutKit _kit = new();

    public void Dispose() => _kit.Dispose();

    private ResolvedElement Button(string gui = ClassicLayoutEngineTests.AgesView, string name = "close_button") =>
        ClassicLayoutKit.Find(_kit.Layout(gui, game: "eu4"), name).Element;

    private static GuiPropertyRow Row(IReadOnlyList<GuiPropertyRow> rows, string name) =>
        Assert.Single(rows, r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

    // Zwei Blöcke, die sich nur in der Höhe unterscheiden, und ein eigener Basiswert für position.
    private const string BaseAndBlocks =
        "guiTypes = {\n\twindowType = {\n\t\tname = \"w\"\n\t\tguiButtonType = {\n\t\t\tname = \"b\"\n\t\t\tposition = { x = 1 y = 2 }\n"
        + "\t\t\tif_resolution = {\n\t\t\t\tmax_height = 867\n\t\t\t\tposition = { x = 3 y = 4 }\n\t\t\t}\n"
        + "\t\t\tif_resolution = {\n\t\t\t\tmin_height = 868\n\t\t\t\tposition = { x = 5 y = 6 }\n\t\t\t}\n\t\t}\n\t}\n}\n";

    [Theory]
    [InlineData("name", GuiPropertyGroup.Data)]
    [InlineData("Orientation", GuiPropertyGroup.Layout)]
    [InlineData("if_resolution", GuiPropertyGroup.Layout)]
    [InlineData("show_position", GuiPropertyGroup.Layout)]
    [InlineData("hide_position", GuiPropertyGroup.Layout)]
    [InlineData("spriteType", GuiPropertyGroup.Appearance)]
    [InlineData("quadTextureSprite", GuiPropertyGroup.Appearance)]
    [InlineData("buttonFont", GuiPropertyGroup.Text)]
    [InlineData("pdx_tooltip", GuiPropertyGroup.Behavior)]
    [InlineData("hide", GuiPropertyGroup.Behavior)]
    [InlineData("show_tooltip", GuiPropertyGroup.Behavior)]
    [InlineData("fade_time", GuiPropertyGroup.Behavior)]
    [InlineData("animation_speed", GuiPropertyGroup.Behavior)]
    [InlineData("foo", GuiPropertyGroup.Other)]
    public void Groups_follow_spec(string name, GuiPropertyGroup group) =>
        Assert.Equal(group, ClassicPropertyRows.GroupOf(name));

    [Fact]
    public void If_resolution_row_shows_matching_block_value()
    {
        var rows = ClassicPropertyRows.Build(Button(), 1920, 1080);

        var position = Row(rows, "position");
        Assert.Equal("{ x= 214 y = 664 }", position.Value);
        Assert.Equal("min_height = 868", position.Condition);
        Assert.Equal(GuiTexts.ClassicOriginIfResolution("min_height = 868"), position.OriginText);
        Assert.Equal("position", position.Source.Origin.Node.Name);
        var block = position.Source.Origin.Node.Parent!;
        Assert.Equal("if_resolution", block.Name);
        Assert.Equal("868", block.Children.Single(c => c.Name == "min_height").Value);
        Assert.True(position.IsBlock);
        Assert.Equal(GuiPropertyGroup.Layout, position.Group);

        // Schlüssel, die nur der Block setzt, bekommen eine eigene Zeile; die Gruppe richtet sich nach dem Namen.
        var sprite = Row(rows, "quadTextureSprite");
        Assert.Equal("\"button_type_1\"", sprite.Value);
        Assert.Equal("min_height = 868", sprite.Condition);
        Assert.Equal(GuiPropertyGroup.Appearance, sprite.Group);
        Assert.Equal("\"CLOSE\"", Row(rows, "buttonText").Value);

        // Bedingungen sind keine Zeilen; die if_resolution-Blöcke selbst bleiben als Blockzeilen stehen.
        Assert.DoesNotContain(rows, r => r.Name == "min_height");
        var blocks = rows.Where(r => r.Name == "if_resolution").ToList();
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.True(b.IsBlock && b.Condition is null));

        // Was nur das Element setzt, bleibt unverändert.
        var font = Row(rows, "buttonFont");
        Assert.Null(font.Condition);
        Assert.Equal("\"vic_18\"", font.Value);
    }

    [Fact]
    public void No_matching_block_shows_base_value()
    {
        var element = Button(BaseAndBlocks, "b");

        var low = Row(ClassicPropertyRows.Build(element, 1920, 800), "position");
        Assert.Equal("{ x = 3 y = 4 }", low.Value);
        Assert.Equal("max_height = 867", low.Condition);
        Assert.Equal("position", low.Source.Origin.Node.Name);

        var none = Row(ClassicPropertyRows.Build(element, 1920, 867.5), "position");
        Assert.Equal("{ x = 1 y = 2 }", none.Value);
        Assert.Null(none.Condition);
        Assert.Equal(GuiOriginKind.Here, none.Source.Origin.Kind);
        Assert.Equal(GuiTexts.OriginHere, none.OriginText);

        // Ohne Treffer und ohne Basiswert gibt es nur die Blockzeilen von if_resolution.
        var unmatched = ClassicPropertyRows.Build(Button(), 1920, 867.5);
        Assert.DoesNotContain(unmatched, r => r.Name == "position");
        Assert.DoesNotContain(unmatched, r => r.Condition is not null);
    }

    [Fact]
    public void Last_matching_block_wins_per_key_with_its_own_condition()
    {
        const string gui =
            "guiTypes = {\n\twindowType = {\n\t\tname = \"w\"\n\t\tguiButtonType = {\n\t\t\tname = \"b\"\n\t\t\tposition = { x = 1 y = 2 }\n\t\t\tbuttonText = \"BASE\"\n"
            + "\t\t\tif_resolution = {\n\t\t\t\tmin_height = 100\n\t\t\t\tposition = { x = 3 y = 4 }\n\t\t\t\tbuttonText = \"FIRST\"\n\t\t\t}\n"
            + "\t\t\tif_resolution = {\n\t\t\t\tmin_width = 200\n\t\t\t\tposition = { x = 5 y = 6 }\n\t\t\t}\n\t\t}\n\t}\n}\n";

        var rows = ClassicPropertyRows.Build(Button(gui, "b"), 1920, 1080);

        var position = Row(rows, "position");
        Assert.Equal("{ x = 5 y = 6 }", position.Value);
        Assert.Equal("min_width = 200", position.Condition);
        var text = Row(rows, "buttonText");
        Assert.Equal("\"FIRST\"", text.Value);
        Assert.Equal("min_height = 100", text.Condition);
    }

    [Fact]
    public void Rows_keep_group_order_and_text_order()
    {
        var rows = ClassicPropertyRows.Build(Button(BaseAndBlocks, "b"), 1920, 1080);

        Assert.Equal(rows.OrderBy(r => r.Group).Select(r => r.Name), rows.Select(r => r.Name));
        // Der überschriebene Basiswert behält seinen Platz vor den Blockzeilen.
        var names = rows.Select(r => r.Name).ToList();
        Assert.True(names.IndexOf("position") < names.IndexOf("if_resolution"));
    }

    [Fact]
    public void Preview_and_rows_agree()
    {
        var root = _kit.Layout(ClassicLayoutEngineTests.AgesView, new LayoutOptions { ScreenWidth = 1920, ScreenHeight = 1080 }, "eu4");
        var box = ClassicLayoutKit.Find(root, "close_button");

        var withCondition = ClassicPropertyRows.Build(box.Element, 1920, 1080).Where(r => r.Condition is not null).ToList();
        var fromRows = withCondition
            .Select(r => GuiTexts.ClassicFromIfResolution(r.Name, r.Condition!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fromPreview = box.Notes.Where(n => n.Contains("if_resolution", StringComparison.Ordinal)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(withCondition);
        Assert.True(fromPreview.SetEquals(fromRows), $"Vorschau: {string.Join(" | ", fromPreview)}; Zeilen: {string.Join(" | ", fromRows)}");
        AssertValuesAgree(box.Element, 1920, 1080);
    }

    [Fact]
    public void Duplicate_key_inside_block_uses_last_like_preview()
    {
        const string gui =
            "guiTypes = {\n\twindowType = {\n\t\tname = \"w\"\n\t\tguiButtonType = {\n\t\t\tname = \"b\"\n\t\t\tposition = { x = 1 y = 2 }\n"
            + "\t\t\tif_resolution = {\n\t\t\t\tmin_height = 868\n\t\t\t\tposition = { x = 1 y = 1 }\n\t\t\t\tbuttonText = \"A\"\n"
            + "\t\t\t\tposition = { x = 2 y = 2 }\n\t\t\t\tbuttonText = \"B\"\n\t\t\t}\n\t\t}\n\t}\n}\n";
        var element = Button(gui, "b");

        var rows = ClassicPropertyRows.Build(element, 1920, 1080);

        Assert.Equal("{ x = 2 y = 2 }", Row(rows, "position").Value);
        Assert.Equal("\"B\"", Row(rows, "buttonText").Value);
        AssertValuesAgree(element, 1920, 1080);
    }

    [Fact]
    public void Values_agree_with_preview_across_blocks_and_screens()
    {
        var element = Button(BaseAndBlocks, "b");

        foreach (var (w, h) in new[] { (1920.0, 800.0), (1920.0, 1080.0), (1920.0, 867.5) })
        {
            AssertValuesAgree(element, w, h);
        }
    }

    /// <summary>Jede Zeile mit Bedingung zeigt genau den Wert, den die Vorschau (<c>ClassicElementProps</c>) für diesen Bildschirm nimmt.</summary>
    private static void AssertValuesAgree(ResolvedElement element, double width, double height)
    {
        var props = new ClassicElementProps(element, width, height);
        foreach (var row in ClassicPropertyRows.Build(element, width, height).Where(r => r.Condition is not null))
        {
            if (row.IsBlock)
            {
                Assert.Same(props.Block(row.Name), row.Source.Origin.Node);
            }
            else
            {
                Assert.Equal(props.Scalar(row.Name), row.Source.Value);
            }
        }
    }

    [Fact]
    public void Matching_blocks_follow_screen_size_in_text_order()
    {
        var element = Button();

        Assert.Equal("min_height = 868", Assert.Single(ClassicProps.MatchingIfResolution(element, 1920, 1080)).Condition);
        Assert.Equal("max_height = 867", Assert.Single(ClassicProps.MatchingIfResolution(element, 1280, 800)).Condition);
        Assert.Empty(ClassicProps.MatchingIfResolution(element, 1920, 867.5));
    }
}
