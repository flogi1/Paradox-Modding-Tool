using ParadoxModTool.GuiDesign.Blocks;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiBlockSuggesterTests
{
    private const string Window =
        "window = {\n\tname = \"my_window\"\n\tsize = { 600 400 }\n\ttext = \"MY_TITLE\"\n\ticon = { texture = \"gfx/a.dds\" }\n" +
        "\tbutton = { onclick = \"[GetVariableSystem.Set( 'w_tab', 'one' )]\" }\n}\n";

    private static string RenderWithDefaults(GuiBlockSuggestionResult result)
    {
        var text = GuiBlockDraft.Compose("T", string.Empty, string.Empty, result.Parameters, result.Repeats, result.Body);
        var block = GuiBlockParser.Parse(text, GuiBlockLayer.Game, null);
        Assert.Empty(block.Errors);
        return GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block));
    }

    [Fact]
    public void Suggest_FindsNameTextTextureSizeAndVariables()
    {
        var values = GuiBlockSuggester.Suggest(Window).Where(s => s.Kind == GuiBlockSuggestionKind.Value).ToList();

        Assert.Equal(
            new[] { ("name", "my_window"), ("width", "600"), ("height", "400"), ("text_1", "MY_TITLE"), ("texture_1", "gfx/a.dds"), ("var_1", "w_tab"), ("id_1", "one") },
            values.Select(v => (v.Key, v.Default)));
        Assert.Equal("Name", values[0].Label);
        Assert.Equal(GuiTexts.SuggestText(1), values[3].Label);
        Assert.All(values, v => Assert.Equal(v.Default, v.Found));
        Assert.Equal(values.Count, values.Select(v => v.Id).Distinct().Count());
    }

    [Fact]
    public void Suggest_SameValueSharesAKeyWithinACategoryOnly()
    {
        var body = "w = {\n\ttext = \"a\"\n\ttooltip = \"a\"\n\tonclick = \"[GetVariableSystem.Set( 'v', 'a' )]\"\n}\n";

        var values = GuiBlockSuggester.Suggest(body);

        var text = Assert.Single(values, v => v.Key.StartsWith("text_", StringComparison.Ordinal));
        Assert.Equal(2, text.Count);
        Assert.Equal(1, Assert.Single(values, v => v.Key == "id_1").Count);
    }

    [Fact]
    public void Suggest_SkipsValuesWithDollarPipeOrDataExpressions()
    {
        var body = "w = {\n\ttext = \"a$$b\"\n\ttext = \"x|y\"\n\ttext = \"[GetName]\"\n}\n";

        Assert.DoesNotContain(GuiBlockSuggester.Suggest(body), s => s.Key.StartsWith("text_", StringComparison.Ordinal));
    }

    [Fact]
    public void Suggest_AvoidsExistingKeysAndIndex()
    {
        var body = "a = {\n\tname = \"$name$\"\n}\nb = {\n\tname = \"x\"\n}\n";

        var name = Assert.Single(GuiBlockSuggester.Suggest(body));

        Assert.Equal("name_2", name.Key);
        Assert.Equal("x", name.Default);
    }

    [Fact]
    public void Apply_ValuesOnly_RendersTheOriginal()
    {
        var result = GuiBlockSuggester.Apply(Window, GuiBlockSuggester.Suggest(Window));

        Assert.Contains("name = \"$name$\"", result.Body);
        Assert.Contains("size = { $width$ $height$ }", result.Body);
        Assert.Contains("'$var_1$', '$id_1$'", result.Body);
        Assert.Empty(result.Repeats);
        Assert.Equal(Window, RenderWithDefaults(result));
    }

    [Fact]
    public void Apply_SubsetLeavesTheRestUntouched()
    {
        var chosen = GuiBlockSuggester.Suggest(Window).Where(s => s.Key == "text_1").ToList();

        var result = GuiBlockSuggester.Apply(Window, chosen);

        Assert.Contains("text = \"$text_1$\"", result.Body);
        Assert.Contains("name = \"my_window\"", result.Body);
        Assert.Equal(new[] { "text_1" }, result.Parameters.Select(p => p.Key));
        Assert.Equal(Window, RenderWithDefaults(result));
    }
}
