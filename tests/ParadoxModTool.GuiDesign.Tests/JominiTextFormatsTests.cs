using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class JominiTextFormatsTests
{
    private static readonly JominiTextStyle Base = new(null, JominiFontStyle.Regular, 15, JominiColor.White, false, false, false);

    private static JominiTextFormats Formats(params (GuiSourceFile File, string Text)[] files) =>
        JominiTextFormats.From(GuiIndexTests.Build(files));

    private static string Format(string name, string spec) => $"\tformat = {{\n\t\tname = {name}\n\t\tformat = \"{spec}\"\n\t}}\n";

    [Fact]
    public void Format_chain_applies_bold_size_and_color()
    {
        var formats = Formats((GuiIndexTests.F("gui/textformatting.gui"),
            "textformatting = {\n" + Format("medium", "color:{0.61,0.6,0.56}") + Format("header", "medium;bold;size:18") + "}"));

        var style = formats.Apply(Base, "header");

        Assert.Equal(JominiFontStyle.Bold, style.Style);
        Assert.Equal(18, style.Size);
        Assert.Equal(new JominiColor(0.61, 0.6, 0.56, 1), style.Color);
    }

    [Fact]
    public void Direct_spec_and_big_color_values_work()
    {
        var style = JominiTextFormats.Empty.Apply(Base, "Color:{255, 0, 51, 255};Italic;UNDERLINE;shadow;glow_color:{0,0,0,1};glow_offset:{1,1}");

        Assert.Equal(new JominiColor(1, 0, 0.2, 1), style.Color);
        Assert.Equal(JominiFontStyle.Italic, style.Style);
        Assert.True(style.Underline);
        Assert.True(style.Shadow);
        Assert.True(style.Glow);
    }

    [Fact]
    public void Color_scale_is_decided_per_tuple()
    {
        // Wie fontcolor: ist eine Komponente über 1, ist das ganze Tupel 0–255 (auch die Werte 1 und 0.5).
        var big = JominiTextFormats.Empty.Apply(Base, "color:{255, 1, 0.5}");
        var small = JominiTextFormats.Empty.Apply(Base, "color:{1, 0.5, 0, 0.25}");

        Assert.Equal(new JominiColor(1, 1 / 255.0, 0.5 / 255, 1), big.Color);
        Assert.Equal(new JominiColor(1, 0.5, 0, 0.25), small.Color);
    }

    [Theory]
    [InlineData("size:-10")]
    [InlineData("size:0")]
    [InlineData("size:NaN")]
    [InlineData("size:Infinity")]
    [InlineData("size:-Infinity")]
    public void Unusable_size_changes_nothing(string spec)
    {
        Assert.Equal(15, JominiTextFormats.Empty.Apply(Base, spec).Size);
    }

    [Fact]
    public void Upper_case_size_and_font_keys_work()
    {
        var style = JominiTextFormats.Empty.Apply(Base, "SIZE:30;FONT:MapFont");

        Assert.Equal(30, style.Size);
        Assert.Equal("MapFont", style.Font);
    }

    [Fact]
    public void Mod_format_overrides_vanilla()
    {
        var formats = Formats(
            (GuiIndexTests.F("gui/textformatting.gui", GuiLayer.Mod), "textformatting = {\n" + Format("medium", "size:40") + "}"),
            (GuiIndexTests.F("gui/textformatting.gui", GuiLayer.Vanilla), "textformatting = {\n" + Format("medium", "size:20") + Format("other", "bold") + "}"));

        Assert.Equal(40, formats.Apply(Base, "medium").Size);
        Assert.Equal(JominiFontStyle.Bold, formats.Apply(Base, "other").Style);
    }

    [Fact]
    public void Cyclic_and_unknown_formats_change_nothing()
    {
        var formats = Formats((GuiIndexTests.F("gui/textformatting.gui"),
            "textformatting = {\n" + Format("a", "b") + Format("b", "a") + Format("self", "self") + "}"));

        Assert.Equal(Base, formats.Apply(Base, "a"));
        Assert.Equal(Base, formats.Apply(Base, "self"));
        Assert.Equal(Base, formats.Apply(Base, "does_not_exist"));
    }

    [Fact]
    public void Self_referencing_format_with_many_references_finishes_fast_and_changes_nothing()
    {
        var formats = Formats((GuiIndexTests.F("gui/textformatting.gui"),
            "textformatting = {\n" + Format("a", "bold;a;a;a;a;a;a;a;a;a;a") + "}"));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var style = formats.Apply(Base, "a");
        watch.Stop();

        Assert.Equal(Base, style);
        Assert.True(watch.ElapsedMilliseconds < 1000, $"dauerte {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Icon_picks_iconsize_by_fontsize()
    {
        var formats = Formats((GuiIndexTests.F("gui/texticons.gui"),
            "texticon = {\n\ticon = warn\n" +
            "\ticonsize = { texture = \"a14.dds\" size = { 14 14 } offset = { 0 2 } fontsize = 14 }\n" +
            "\ticonsize = { texture = \"a18.dds\" size = { 18 18 } offset = { 1 3 } fontsize = 18 }\n}\n" +
            "texticon = {\n\ticon = plain\n\ticonsize = { texture = \"p.dds\" size = { 25 20 } }\n}"));

        var icon = formats.Icon("warn", 15)!;
        Assert.Equal(new JominiTextIcon("warn", "a14.dds", 14, 14, 0, 2, 14), icon);
        Assert.Equal("a18.dds", formats.Icon("warn", 20)!.Texture);
        Assert.Equal("a14.dds", formats.Icon("warn", 10)!.Texture);
        Assert.Equal((25, 20), (formats.Icon("plain", 15)!.Width, formats.Icon("plain", 15)!.Height));
        Assert.Null(formats.Icon("plain", 15)!.FontSize);
        Assert.Null(formats.Icon("missing", 15));
    }

    [Fact]
    public void Icon_sizes_resolve_file_constants_and_expressions()
    {
        // Wie EU V font_icons.gui: size = { @font_icon_default_size … }, @[font_icon_default_size * 0.85], fontsize = @font_icon_default_fontsize.
        var formats = Formats((GuiIndexTests.F("gui/texticons.gui"),
            "@s = 20\n@f = 16\n@big = @s\n" +
            "texticon = {\n\ticon = constant\n\ticonsize = { texture = \"c.dds\" size = { @s @big } offset = { 0 @[s / 10] } fontsize = @f }\n}\n" +
            "texticon = {\n\ticon = expression\n\ticonsize = { texture = \"e.dds\" size = { @[s * 0.85] @[s * 0.85] } offset = { 0 2 } fontsize = @f }\n}\n" +
            "texticon = {\n\ticon = choose\n" +
            "\ticonsize = { texture = \"small.dds\" size = { @s @s } fontsize = @f }\n" +
            "\ticonsize = { texture = \"large.dds\" size = { 30 30 } fontsize = @[f + 8] }\n}\n" +
            "texticon = {\n\ticon = unknown\n\ticonsize = { texture = \"u.dds\" size = { @nope @nope } fontsize = @nope }\n}"));

        Assert.Equal(new JominiTextIcon("constant", "c.dds", 20, 20, 0, 2, 16), formats.Icon("constant", 15));
        Assert.Equal(new JominiTextIcon("expression", "e.dds", 17, 17, 0, 2, 16), formats.Icon("expression", 15));
        // Mit aufgelöster fontsize lässt sich zwischen den Blöcken wählen.
        Assert.Equal("small.dds", formats.Icon("choose", 20)!.Texture);
        Assert.Equal("large.dds", formats.Icon("choose", 24)!.Texture);
        // Fehlende Konstanten bleiben unauswertbar: Größe 0, keine fontsize.
        var unknown = formats.Icon("unknown", 15)!;
        Assert.Equal((0.0, 0.0, (double?)null), (unknown.Width, unknown.Height, unknown.FontSize));
    }
}
