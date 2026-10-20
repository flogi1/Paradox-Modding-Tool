using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Eingabefelder für klassische Eigenschaften: welches Feld passt, was geschrieben wird, Paar-Schlüssel und Prozent-Umschalter.</summary>
public sealed class ClassicPropertyInputTests
{
    private const string Widget = "icontype";

    private static GuiPropertyStats Stats(string name, int count, GuiValueForm form, int quoted = 0, string? spelling = null)
    {
        var stats = new GuiPropertyStats { Name = name, Count = count, Quoted = quoted };
        stats.Forms[form] = count;
        stats.Spellings[spelling ?? name] = count;
        return stats;
    }

    private static GuiPropertyCatalog Catalog(params GuiPropertyStats[] properties)
    {
        var catalog = new GuiPropertyCatalog { Classic = true };
        var widget = new GuiWidgetStats { Name = Widget, Count = 10 };
        foreach (var property in properties)
        {
            widget.Properties[property.Name] = property;
        }

        catalog.Widgets[Widget] = widget;
        return catalog;
    }

    private static ResolvedProperty Resolved(string gui, GuiRole role)
    {
        var node = GuiParser.Parse(gui).Root.Children[0];
        return new ResolvedProperty
        {
            Name = node.Name,
            Role = role,
            Value = node.Value ?? string.Empty,
            IsQuoted = node.IsQuoted,
            Origin = new GuiOrigin(GuiOriginKind.Here, string.Empty, null, 1, node),
        };
    }

    private static ClassicInput InputFor(string name, ResolvedProperty? current, GuiPropertyCatalog? catalog = null) =>
        ClassicPropertyInput.InputFor(name, current, catalog ?? Catalog(), Widget);

    [Fact]
    public void Catalog_lookup_folds_case_only_when_classic()
    {
        var classic = Catalog(Stats("orientation", 3, GuiValueForm.Word));
        var jomini = new GuiPropertyCatalog();
        jomini.Widgets["button"] = new GuiWidgetStats { Name = "button" };
        jomini.Widgets["button"].Properties["Size"] = Stats("Size", 1, GuiValueForm.Pair);

        Assert.NotNull(classic.Lookup("iconType", "Orientation"));
        Assert.NotNull(classic.LookupWidget("ICONTYPE"));
        Assert.Single(classic.PropertiesOf("iconType"));
        Assert.NotNull(jomini.Lookup("button", "Size"));
        Assert.Null(jomini.Lookup("button", "size"));
        Assert.Null(jomini.LookupWidget("Button"));
    }

    [Fact]
    public void Orientation_gets_anchor_input_even_when_quoted()
    {
        var current = Resolved("Orientation = \"UPPER_LEFT\"", GuiRole.Property);

        Assert.Equal(ClassicInput.Anchor, InputFor("Orientation", current));
        Assert.Equal(ClassicInput.Anchor, InputFor("origo", null));
    }

    [Fact]
    public void Junk_anchor_is_invalid_but_kept()
    {
        Assert.False(ClassicPropertyInput.IsValidAnchor("UPPERL_LEFT"));
        Assert.False(ClassicPropertyInput.IsValidAnchor("left"));
        Assert.True(ClassicPropertyInput.IsValidAnchor("\"center_up\""));
        Assert.Equal(9, ClassicPropertyInput.AnchorOptions.Count);
        Assert.All(ClassicPropertyInput.AnchorOptions, o => Assert.True(ClassicPropertyInput.IsValidAnchor(o)));
        Assert.All(ClassicPropertyInput.AnchorOptions, o => Assert.Equal(o.ToUpperInvariant(), o));
    }

    [Fact]
    public void Format_options_from_text_rules()
    {
        var options = ClassicPropertyInput.FormatOptions(Catalog(), Widget);

        Assert.Contains("left", options);
        Assert.Contains("centre", options);
        Assert.Contains("right", options);
        Assert.DoesNotContain("UP;", options);
        Assert.All(options, o => Assert.True(ClassicPropertyInput.IsValidFormat(o)));
        Assert.False(ClassicPropertyInput.IsValidFormat("UP;x"));
        Assert.True(ClassicPropertyInput.IsValidFormat("\"Left\""));
    }

    [Fact]
    public void Format_options_put_frequent_catalog_values_first()
    {
        var format = Stats("format", 20, GuiValueForm.Word);
        format.Words["right"] = 12;
        format.Words["UP;"] = 4;
        format.Words["center"] = 3;
        format.Words["LEFT"] = 5;
        format.Words["UPPERL_LEFT"] = 30;

        var options = ClassicPropertyInput.FormatOptions(Catalog(format), Widget);

        // "UP;" liest die Vorschau als "up" (Semikolon fällt weg); Tippfehler werden nicht angeboten.
        Assert.Equal(new[] { "right", "left", "up", "center" }, options.Take(4));
        Assert.DoesNotContain(options, o => o.Contains("upperl", StringComparison.Ordinal));
        Assert.DoesNotContain(options, o => o.Contains(';'));
        Assert.Equal(options.Count, options.Distinct().Count());
    }

    [Theory]
    [InlineData("100%%", true)]
    [InlineData("-20", true)]
    [InlineData("50%", true)]
    [InlineData("@w", true)]
    [InlineData("abc", false)]
    [InlineData("@", false)]
    [InlineData("%%", false)]
    [InlineData("NaN", false)]
    public void Pair_component_accepts_double_percent(string value, bool expected) =>
        Assert.Equal(expected, ClassicPropertyInput.IsPairComponent(value));

    [Theory]
    [InlineData("100", "100%")]
    [InlineData("100%", "100%%")]
    [InlineData("100%%", "100")]
    [InlineData("-5", "-5%")]
    [InlineData("@w", "@w")]
    public void Cycle_percent_three_states(string input, string expected) =>
        Assert.Equal(expected, ClassicPropertyInput.CyclePercent(input));

    [Fact]
    public void New_size_uses_dominant_pair_keys()
    {
        var size = Stats("size", 7, GuiValueForm.Block);
        size.PairKeys["width,height"] = 5;
        size.PairKeys["x,y"] = 2;
        var slotSize = Stats("slotsize", 3, GuiValueForm.Block);
        slotSize.PairKeys["x,y"] = 3;
        var catalog = Catalog(size, slotSize);

        Assert.Equal(new ClassicPairKeys("width", "height"), ClassicPropertyInput.PairKeysFor("Size", null, catalog, Widget));
        Assert.Equal(new ClassicPairKeys("x", "y"), ClassicPropertyInput.PairKeysFor("slotSize", null, catalog, Widget));
        Assert.Equal(new ClassicPairKeys("x", "y"), ClassicPropertyInput.PairKeysFor("position", null, catalog, Widget));
        Assert.Equal(new ClassicPairKeys("x", "y"), ClassicPropertyInput.PairKeysFor("size", null, Catalog(), Widget));
    }

    [Fact]
    public void Existing_pair_block_keeps_its_keys()
    {
        var block = GuiParser.Parse("size = { x = 1 y = 2 }").Root.Children[0];
        var size = Stats("size", 7, GuiValueForm.Block);
        size.PairKeys["width,height"] = 5;

        Assert.Equal(new ClassicPairKeys("x", "y"), ClassicPropertyInput.PairKeysFor("size", block, Catalog(size), Widget));
        Assert.Equal(("1", "2"), ClassicPropertyInput.ReadPair(block));
    }

    [Fact]
    public void Read_pair_keeps_constants_and_tolerates_missing_values()
    {
        Assert.Equal(("@w", "-22%%"), ClassicPropertyInput.ReadPair(GuiParser.Parse("position = { x = @w y = -22%% }").Root.Children[0]));
        Assert.Equal(("5", string.Empty), ClassicPropertyInput.ReadPair(GuiParser.Parse("position = { x = 5 }").Root.Children[0]));
    }

    [Fact]
    public void New_orientation_written_quoted_when_vanilla_quotes()
    {
        var quoted = Catalog(Stats("orientation", 10, GuiValueForm.Text, quoted: 8, spelling: "Orientation"));
        var plain = Catalog(Stats("orientation", 10, GuiValueForm.Word, quoted: 5));

        Assert.Equal("\"UPPER_LEFT\"", ClassicPropertyInput.ScalarText("Orientation", "UPPER_LEFT", null, quoted, Widget));
        Assert.Equal("UPPER_LEFT", ClassicPropertyInput.ScalarText("Orientation", "UPPER_LEFT", null, plain, Widget));
        Assert.Equal("UPPER_LEFT", ClassicPropertyInput.ScalarText("Orientation", "UPPER_LEFT", null, Catalog(), Widget));
    }

    [Fact]
    public void Scalar_text_keeps_old_quoting_and_rejects_junk()
    {
        var catalog = Catalog(Stats("orientation", 10, GuiValueForm.Text, quoted: 10));

        Assert.Equal("CENTER", ClassicPropertyInput.ScalarText("orientation", "CENTER", false, catalog, Widget));
        Assert.Equal("\"CENTER\"", ClassicPropertyInput.ScalarText("orientation", "CENTER", true, catalog, Widget));
        Assert.Equal("\"CENTER\"", ClassicPropertyInput.ScalarText("orientation", " \"CENTER\" ", false, catalog, Widget));
        Assert.Equal("\"two words\"", ClassicPropertyInput.ScalarText("text", "two words", true, catalog, Widget));
        Assert.Null(ClassicPropertyInput.ScalarText("text", "two words", false, catalog, Widget));
        Assert.Null(ClassicPropertyInput.ScalarText("text", "a\"b", true, catalog, Widget));
        Assert.Null(ClassicPropertyInput.ScalarText("text", "  ", true, catalog, Widget));
        Assert.Null(ClassicPropertyInput.ScalarText("text", "{ x }", false, catalog, Widget));
    }

    [Theory]
    [InlineData("Orientation", ClassicInput.Anchor)]
    [InlineData("origo", ClassicInput.Anchor)]
    [InlineData("Format", ClassicInput.Format)]
    [InlineData("spriteType", ClassicInput.Sprite)]
    [InlineData("quadTextureSprite", ClassicInput.Sprite)]
    [InlineData("font", ClassicInput.Font)]
    [InlineData("buttonFont", ClassicInput.Font)]
    [InlineData("buttonText", ClassicInput.LocalisedText)]
    [InlineData("pdx_tooltip", ClassicInput.LocalisedText)]
    [InlineData("delayedTooltipText", ClassicInput.LocalisedText)]
    [InlineData("Frame", ClassicInput.Frame)]
    [InlineData("margin", ClassicInput.Block)]
    [InlineData("if_resolution", ClassicInput.Block)]
    [InlineData("position", ClassicInput.Pair)]
    [InlineData("max_slots", ClassicInput.Pair)]
    public void New_property_input_by_name(string name, ClassicInput expected) =>
        Assert.Equal(expected, InputFor(name, null));

    [Fact]
    public void Pair_names_are_pairs_only_for_two_scalar_children()
    {
        Assert.Equal(ClassicInput.Pair, InputFor("position", Resolved("position = { x = 1 y = 2 }", GuiRole.StructuredBlock)));
        Assert.Equal(ClassicInput.Pair, InputFor("size", Resolved("size = { x = 1 # Breite\n y = 2 }", GuiRole.StructuredBlock)));
        Assert.Equal(ClassicInput.Block, InputFor("size", Resolved("size = { min = { x = 1 y = 2 } max = { x = 3 y = 4 } }", GuiRole.StructuredBlock)));
        Assert.Equal(ClassicInput.Block, InputFor("position", Resolved("position = { x = 1 y = 2 z = 3 }", GuiRole.StructuredBlock)));
    }

    [Fact]
    public void Rest_follows_catalog_form_and_bool_values()
    {
        var catalog = Catalog(
            Stats("maxwidth", 10, GuiValueForm.Number),
            Stats("alwaystransparent", 10, GuiValueForm.Bool),
            Stats("name", 10, GuiValueForm.Text, quoted: 10));

        Assert.Equal(ClassicInput.Number, InputFor("maxWidth", null, catalog));
        Assert.Equal(ClassicInput.Bool, InputFor("alwaysTransparent", null, catalog));
        Assert.Equal(ClassicInput.Text, InputFor("name", null, catalog));
        Assert.Equal(ClassicInput.Text, InputFor("unknown", null, catalog));
        Assert.Equal(ClassicInput.Bool, InputFor("clipping", Resolved("clipping = yes", GuiRole.Property), catalog));
        Assert.Equal(ClassicInput.Bool, InputFor("clipping", Resolved("clipping = 0", GuiRole.Property), catalog));
        Assert.Equal(ClassicInput.Number, InputFor("maxwidth", Resolved("maxwidth = 1", GuiRole.Property), catalog));
    }

    [Fact]
    public void Spelling_follows_catalog_else_name()
    {
        var catalog = Catalog(Stats("orientation", 10, GuiValueForm.Word, spelling: "Orientation"));

        Assert.Equal("Orientation", ClassicPropertyInput.SpellingFor("orientation", catalog, Widget));
        Assert.Equal("foo_Bar", ClassicPropertyInput.SpellingFor("foo_Bar", catalog, Widget));
    }

    [Fact]
    public void Addable_properties_ignore_case_of_present_keys()
    {
        var catalog = Catalog(
            Stats("orientation", 10, GuiValueForm.Word, spelling: "Orientation"),
            Stats("position", 9, GuiValueForm.Block),
            Stats("buttonfont", 5, GuiValueForm.Text, spelling: "buttonFont"));
        var element = new ResolvedElement
        {
            Key = "iconType",
            Role = GuiRole.EngineWidget,
            BaseWidget = "iconType",
            TypeChain = new[] { "iconType" },
            Origin = new GuiOrigin(GuiOriginKind.Here, string.Empty, null, 1, GuiParser.Parse("iconType = { }").Root.Children[0]),
        };
        var node = GuiParser.Parse("orientation = center").Root.Children[0];
        element.Properties.Add(new ResolvedProperty { Name = "orientation", Role = GuiRole.Property, Origin = new GuiOrigin(GuiOriginKind.Here, string.Empty, null, 1, node) });

        var addable = ClassicPropertyInput.AddableProperties(element, catalog);

        Assert.Equal(new[] { "position", "buttonFont" }, addable);
    }
}
