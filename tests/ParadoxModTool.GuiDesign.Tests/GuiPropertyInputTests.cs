using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiPropertyInputTests
{
    private static GuiPropertyCatalog Catalog()
    {
        var catalog = new GuiPropertyCatalog();
        var button = new GuiWidgetStats { Name = "button", Count = 10 };
        void Add(string name, GuiValueForm form, int count, params (string Word, int Count)[] words)
        {
            var stats = new GuiPropertyStats { Name = name, Count = count };
            stats.Forms[form] = count;
            foreach (var (word, seen) in words)
            {
                stats.Words[word] = seen;
            }

            button.Properties[name] = stats;
        }

        Add("parentanchor", GuiValueForm.Word, 10, ("top", 4), ("center", 6));
        Add("size", GuiValueForm.Pair, 9);
        Add("text", GuiValueForm.Text, 8);
        Add("alpha", GuiValueForm.Number, 7);
        Add("texture", GuiValueForm.TexturePath, 6);
        Add("color", GuiValueForm.Color, 5);
        Add("visible", GuiValueForm.Binding, 4);
        catalog.Widgets["button"] = button;
        return catalog;
    }

    private static ResolvedProperty Property(string name, GuiRole role, string value, params string[] items)
    {
        var node = GuiParser.Parse("x = 1").Root.Children[0];
        return new ResolvedProperty
        {
            Name = name,
            Role = role,
            Value = value,
            Items = items,
            Origin = new GuiOrigin(GuiOriginKind.Here, string.Empty, null, 1, node),
        };
    }

    [Fact]
    public void EditorFollowsTheCatalogAndTheCurrentValue()
    {
        var catalog = Catalog();
        GuiValueEditor Editor(string name, ResolvedProperty? current = null) => GuiPropertyInput.EditorFor(name, "button", catalog, current);

        Assert.Equal(GuiValueEditor.Enum, Editor("parentanchor"));
        Assert.Equal(GuiValueEditor.Pair, Editor("size"));
        Assert.Equal(GuiValueEditor.Text, Editor("text"));
        Assert.Equal(GuiValueEditor.Number, Editor("alpha"));
        Assert.Equal(GuiValueEditor.Texture, Editor("texture"));
        Assert.Equal(GuiValueEditor.Color, Editor("color"));
        Assert.Equal(GuiValueEditor.Binding, Editor("visible"));
        Assert.Equal(GuiValueEditor.Text, Editor("unknown"));
        Assert.Equal(GuiValueEditor.Pair, Editor("unknown", Property("unknown", GuiRole.ValueBlock, "1 2", "1", "2")));
        Assert.Equal(GuiValueEditor.Block, Editor("size", Property("size", GuiRole.StructuredBlock, "a = b")));
        Assert.Equal(new[] { "center", "top" }, GuiPropertyInput.EnumOptions("parentanchor", "button", catalog));
    }

    [Theory]
    [InlineData(GuiValueEditor.Text, "Hallo", false, "\"Hallo\"")]
    [InlineData(GuiValueEditor.Text, "\"Hallo\"", false, "\"Hallo\"")]
    [InlineData(GuiValueEditor.Text, "a\"b", false, null)]
    [InlineData(GuiValueEditor.Number, "0.5", false, "0.5")]
    [InlineData(GuiValueEditor.Number, "", false, null)]
    [InlineData(GuiValueEditor.Number, "a b", false, null)]
    [InlineData(GuiValueEditor.Enum, "top", false, "top")]
    [InlineData(GuiValueEditor.Enum, "top", true, "\"top\"")]
    [InlineData(GuiValueEditor.Pair, "10 20", false, "{ 10 20 }")]
    [InlineData(GuiValueEditor.Pair, "{ 50% @w }", false, "{ 50% @w }")]
    [InlineData(GuiValueEditor.Pair, "10", false, null)]
    [InlineData(GuiValueEditor.Pair, "a b", false, null)]
    [InlineData(GuiValueEditor.Color, "1 0.5 0", false, "{ 1 0.5 0 }")]
    [InlineData(GuiValueEditor.Color, "[Country.GetColor]", false, "\"[Country.GetColor]\"")]
    [InlineData(GuiValueEditor.Color, "1 0", false, null)]
    [InlineData(GuiValueEditor.Block, "a = b", false, null)]
    [InlineData(GuiValueEditor.Bool, "[GetVariableSystem.HasValue( 'iv2_var_system_tabs', 'iv2_tabs_ideagroups' )]", false, "\"[GetVariableSystem.HasValue( 'iv2_var_system_tabs', 'iv2_tabs_ideagroups' )]\"")]
    [InlineData(GuiValueEditor.Enum, "[Country.IsPlayer]", false, "\"[Country.IsPlayer]\"")]
    [InlineData(GuiValueEditor.Number, "[Country.GetTax]", false, "\"[Country.GetTax]\"")]
    public void ValueTextWritesValidCode(GuiValueEditor editor, string input, bool wasQuoted, string? expected)
    {
        Assert.Equal(expected, GuiPropertyInput.ValueText(editor, input, wasQuoted));
    }

    [Fact]
    public void DataExpressionsAreOfferedWhereVanillaUsesThemOrTheValueIsOne()
    {
        var catalog = Catalog();
        catalog.Widgets["button"].Properties["enabled"] = new GuiPropertyStats { Name = "enabled", Count = 12, Forms = { [GuiValueForm.Bool] = 9, [GuiValueForm.Binding] = 3 } };

        Assert.True(GuiPropertyInput.AcceptsDataExpression("visible", "button", catalog, "yes"));
        Assert.True(GuiPropertyInput.AcceptsDataExpression("enabled", "button", catalog, "no"));
        Assert.True(GuiPropertyInput.AcceptsDataExpression("alpha", "button", catalog, "[Country.GetAlpha]"));
        Assert.False(GuiPropertyInput.AcceptsDataExpression("alpha", "button", catalog, "0.5"));
        Assert.False(GuiPropertyInput.AcceptsDataExpression("unknown", "button", catalog, string.Empty));
    }

    [Fact]
    public void PairsAndColors()
    {
        Assert.Equal(("10", "20"), GuiPropertyInput.SplitPair("{ 10 20 }"));
        Assert.Equal(("@w", string.Empty), GuiPropertyInput.SplitPair("@w"));
        Assert.Equal("50%", GuiPropertyInput.TogglePercent("50"));
        Assert.Equal("50", GuiPropertyInput.TogglePercent("50%"));
        Assert.Equal("@w", GuiPropertyInput.TogglePercent("@w"));

        Assert.True(GuiPropertyInput.TryParseColor("{ 1 0.5 0 }", out var color));
        Assert.Equal(new GuiColor(1, 0.5, 0, 1), color);
        Assert.False(GuiPropertyInput.TryParseColor("{ 255 0 0 1 }", out _));
        Assert.Equal("{ 1 0.502 0 1 }", GuiPropertyInput.FormatColor(GuiPropertyInput.FromBytes(255, 128, 0, 1)));
        Assert.Equal(((byte)255, (byte)128, (byte)0), GuiPropertyInput.ToBytes(new GuiColor(1, 0.5, 0, 1)));
    }

    [Fact]
    public void TargetIsTheLineInThisFileOrTheSelectedElement()
    {
        var (resolver, doc) = GuiResolverTests.Setup(
            "window = {\n\tdatacontext = \"[Country]\"\n\tmy_button = {\n\t\talpha = 1\n\t\tblockoverride \"label\" {\n\t\t\tsize = { @w 1 }\n\t\t}\n\t}\n}",
            "types T {\n\ttype my_button = button {\n\t\ttooltip = \"x\"\n\t\tblock \"label\" { }\n\t}\n}");
        var instance = doc.Root.Descendants().First(n => n.Name == "my_button");
        var element = resolver.Resolve(instance);

        var alpha = GuiPropertyInput.TargetFor(element.Property("alpha")!, instance);
        var size = GuiPropertyInput.TargetFor(element.Property("size")!, instance);
        var tooltip = GuiPropertyInput.TargetFor(element.Property("tooltip")!, instance);

        Assert.True(alpha.InPlace);
        Assert.Equal("alpha", alpha.Node.Name);
        Assert.True(size.InPlace);
        Assert.Equal("size", size.Node.Name);
        Assert.False(tooltip.InPlace);
        Assert.Same(instance, tooltip.Node);
        Assert.Equal("@w 1", GuiPropertyInput.InputOf(element.Property("size")));
        Assert.Equal("x", GuiPropertyInput.InputOf(element.Property("tooltip")));
        Assert.Equal("[Country]", GuiPropertyInput.DataContextOf(instance));
    }

    [Fact]
    public void AddablePropertiesSkipWhatIsSetHere()
    {
        var (resolver, doc) = GuiResolverTests.Setup(
            "my_button = {\n\talpha = 1\n}",
            "types T {\n\ttype my_button = button {\n\t\ttext = \"x\"\n\t}\n}");

        var addable = GuiPropertyInput.AddableProperties(resolver.Resolve(doc.Root.Children[0]), Catalog());

        Assert.Equal(new[] { "parentanchor", "size", "text", "texture", "color", "visible" }, addable);
    }

    [Fact]
    public void TexturePathIsRelativeToAContextRoot()
    {
        var roots = new[] { @"C:\mod\in_game", @"C:\game\game\in_game" };

        Assert.Equal("gfx/interface/a.dds", GuiPropertyInput.TexturePathFor(@"C:\mod\in_game\gfx\interface\a.dds", roots));
        Assert.Equal("gfx/b.dds", GuiPropertyInput.TexturePathFor(@"C:\game\game\in_game\gfx\b.dds", roots));
        Assert.Null(GuiPropertyInput.TexturePathFor(@"C:\other\gfx\c.dds", roots));
    }
}
