using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Beispiele aus Vanilla-Dateien für die Erklärung im klassischen Designer.</summary>
public sealed class ClassicExampleIndexTests
{
    private static readonly GuiLayout Hoi4 = new(
        Array.Empty<string>(), FirstWins: false, ModsBeforeVanilla: false, Dialect: GuiDialect.Classic, CaseSensitive: true);

    private static GuiIndex Build(params (GuiSourceFile File, string Text)[] files) =>
        GuiIndex.Build(files.Select(f => f.File).ToList(), file => files.First(f => f.File == file).Text, Hoi4);

    // Fenster (Zeile 2) mit orientation, position-Paar und einem iconType (Zeile 7), der orientation in anderer Schreibweise setzt.
    private static (GuiSourceFile, string) File(string path, GuiLayer layer, string orientation, string windowName = "w") =>
        (GuiIndexTests.F(path, layer),
            "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"" + windowName + "\"\n\t\torientation = " + orientation
            + "\n\t\tposition = { x = 10 y = -4 }\n\t\ticonType = {\n\t\t\tname = \"" + windowName + "_icon\"\n\t\t\tOrientation = \"" + orientation + "\"\n\t\t}\n\t}\n}\n");

    [Fact]
    public void Finds_examples_by_value()
    {
        var index = Build(
            File("interface/a.gui", GuiLayer.Vanilla, "LOWER_RIGHT", "a"),
            File("interface/b.gui", GuiLayer.Vanilla, "upper_left", "b"),
            File("interface/c.gui", GuiLayer.Vanilla, "UPPER_LEFT", "c"));

        var examples = ClassicExampleIndex.Build(index);

        var found = examples.For("orientation", "lower_right");
        Assert.Equal(new[] { "a", "a_icon" }, found.Select(e => e.ElementName));
        Assert.All(found, e => Assert.Equal("interface/a.gui", e.File.RelativePath));
        Assert.Equal("containerWindowType", found[0].WidgetKey);
        Assert.Equal(4, found[0].Line);
        Assert.Equal(new[] { "b", "b_icon", "c" }, examples.For("Orientation", "UPPER_LEFT").Select(e => e.ElementName));
    }

    [Fact]
    public void Unknown_value_falls_back_to_property_examples()
    {
        var index = Build(File("interface/a.gui", GuiLayer.Vanilla, "UPPER_LEFT", "a"));

        var examples = ClassicExampleIndex.Build(index);

        Assert.Equal(2, examples.For("orientation", "center").Count);
        Assert.Equal(2, examples.For("orientation", null).Count);
        Assert.Empty(examples.For("nonexistent", null));
    }

    [Fact]
    public void Pair_blocks_are_examples_of_their_raw_values()
    {
        var index = Build(File("interface/a.gui", GuiLayer.Vanilla, "UPPER_LEFT", "a"));

        var examples = ClassicExampleIndex.Build(index);

        var found = Assert.Single(examples.For("position", "10,-4"));
        Assert.Equal("a", found.ElementName);
    }

    [Fact]
    public void Caps_at_three()
    {
        var files = Enumerable.Range(0, 6).Select(i => File($"interface/f{i}.gui", GuiLayer.Vanilla, "UPPER_LEFT", "w" + i)).ToArray();

        var examples = ClassicExampleIndex.Build(Build(files));

        Assert.Equal(3, examples.For("orientation", "upper_left").Count);
        Assert.Equal(3, examples.For("orientation", null).Count);
        Assert.Equal(3, examples.ForElement("containerWindowType").Count);
        Assert.Equal(new[] { "w0", "w0_icon", "w1" }, examples.For("orientation", "upper_left").Select(e => e.ElementName));
    }

    [Fact]
    public void Mod_files_are_not_examples()
    {
        var index = Build(
            File("interface/a.gui", GuiLayer.Vanilla, "UPPER_LEFT", "a"),
            File("interface/m.gui", GuiLayer.Mod, "LOWER_RIGHT", "m"),
            File("interface/d.gui", GuiLayer.Dependency, "LOWER_RIGHT", "d"));

        var examples = ClassicExampleIndex.Build(index);

        Assert.Equal(2, examples.For("orientation", null).Count);
        Assert.All(examples.For("orientation", null), e => Assert.Equal(GuiLayer.Vanilla, e.File.Layer));
        Assert.All(examples.For("orientation", "lower_right"), e => Assert.Equal(GuiLayer.Vanilla, e.File.Layer));
        Assert.All(examples.ForElement("iconType"), e => Assert.Equal(GuiLayer.Vanilla, e.File.Layer));
        Assert.Single(examples.ForElement("iconType"));
    }

    [Fact]
    public void Element_examples_by_key()
    {
        var index = Build(
            File("interface/a.gui", GuiLayer.Vanilla, "UPPER_LEFT", "a"),
            File("interface/b.gui", GuiLayer.Vanilla, "UPPER_LEFT", "b"));

        var examples = ClassicExampleIndex.Build(index);

        Assert.Equal(new[] { "a_icon", "b_icon" }, examples.ForElement("ICONTYPE").Select(e => e.ElementName));
        Assert.Equal("iconType", examples.ForElement("icontype")[0].WidgetKey);
        Assert.Equal(6, examples.ForElement("icontype")[0].Line);
        Assert.Empty(examples.ForElement("buttonType"));
    }

    [Fact]
    public void Blocks_outside_windows_are_no_examples()
    {
        var index = Build((GuiIndexTests.F("interface/a.gfx", GuiLayer.Vanilla),
            "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_x\"\n\t\torientation = CENTER\n\t}\n}\n"));

        var examples = ClassicExampleIndex.Build(index);

        Assert.Empty(examples.For("orientation", null));
        Assert.Empty(examples.ForElement("spriteType"));
    }
}
