using System.Text;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Minimale Textänderungen für klassische Paar-Blöcke und Grundwerte.</summary>
public sealed class ClassicEditsTests
{
    private static readonly ClassicPairKeys Xy = new("x", "y");
    private static readonly ClassicPairKeys WidthHeight = new("width", "height");

    private static GuiNode Find(GuiDocument document, string name) => document.Root.Descendants().First(n => n.Name == name);

    private static string Apply(GuiDocument document, GuiDesignEdit edit) => TextEdit.Apply(document.Text, edit.Edits);

    [Fact]
    public void SetPair_keeps_spacing()
    {
        var document = GuiParser.Parse("iconType = {\n\tposition = { x= 188 y = -22 }\n}\n");

        var edit = ClassicEdits.SetPair(document, Find(document, "position"), "position", Xy, "190", "-22");

        Assert.Equal("iconType = {\n\tposition = { x= 190 y = -22 }\n}\n", Apply(document, edit));
    }

    [Fact]
    public void SetPair_changes_both_values_and_ignores_key_case()
    {
        var document = GuiParser.Parse("iconType = {\n\tSize = { Width=1 Height =2 }\n}\n");

        var edit = ClassicEdits.SetPair(document, Find(document, "Size"), "size", WidthHeight, "100%%", "@h");

        Assert.Equal("iconType = {\n\tSize = { Width=100%% Height =@h }\n}\n", Apply(document, edit));
    }

    [Fact]
    public void SetPair_without_change_is_empty()
    {
        var document = GuiParser.Parse("iconType = {\n\tposition = { x = 1 y = 2 }\n}\n");

        Assert.True(ClassicEdits.SetPair(document, Find(document, "position"), "position", Xy, "1", "2").IsEmpty);
    }

    [Fact]
    public void SetPair_inserts_missing_inner_key()
    {
        var inline = GuiParser.Parse("iconType = {\n\tposition = { x = 1 }\n}\n");
        var multi = GuiParser.Parse("iconType = {\n\tposition = {\n\t\ty = 2\n\t}\n}\n");

        Assert.Equal("iconType = {\n\tposition = { x = 5 y = 6 }\n}\n", Apply(inline, ClassicEdits.SetPair(inline, Find(inline, "position"), "position", Xy, "5", "6")));
        Assert.Equal("iconType = {\n\tposition = {\n\t\tx = 5\n\t\ty = 6\n\t}\n}\n", Apply(multi, ClassicEdits.SetPair(multi, Find(multi, "position"), "position", Xy, "5", "6")));
    }

    [Fact]
    public void SetPair_inserts_block_in_file_form()
    {
        var document = GuiParser.Parse("iconType = {\n\tname = \"a\"\n}\n");

        var edit = ClassicEdits.SetPair(document, Find(document, "iconType"), "size", WidthHeight, "100%%", "50");

        Assert.Equal("iconType = {\n\tname = \"a\"\n\tsize = { width = 100%% height = 50 }\n}\n", Apply(document, edit));
    }

    [Fact]
    public void SetPair_on_the_element_edits_an_existing_block_in_place()
    {
        var document = GuiParser.Parse("iconType = {\n\tPosition = { x= 1 y = 2 }\n}\n");

        var edit = ClassicEdits.SetPair(document, Find(document, "iconType"), "position", Xy, "3", "2");

        Assert.Equal("iconType = {\n\tPosition = { x= 3 y = 2 }\n}\n", Apply(document, edit));
    }

    [Fact]
    public void SetPair_inside_if_resolution_edits_that_block()
    {
        var text = "iconType = {\n\tposition = { x = 1 y = 2 }\n\tif_resolution = {\n\t\tmin_height = 868\n\t\tposition = { x = 3 y = 4 }\n\t}\n}\n";
        var document = GuiParser.Parse(text);
        var inBlock = Find(document, "if_resolution").Children.Single(c => c.Name == "position");

        var result = Apply(document, ClassicEdits.SetPair(document, inBlock, "position", Xy, "30", "4"));

        Assert.Equal(text.Replace("x = 3 y = 4", "x = 30 y = 4"), result);
        Assert.Contains("\tposition = { x = 1 y = 2 }\n", result);
    }

    [Fact]
    public void Base_value_edit_skips_if_resolution()
    {
        var text = "iconType = {\n\tSpriteType = \"a\"\n\tif_resolution = {\n\t\tmin_height = 868\n\t\tspritetype = \"b\"\n\t}\n}\n";
        var document = GuiParser.Parse(text);

        var edit = ClassicEdits.SetBaseValue(document, Find(document, "iconType"), "spriteType", "\"c\"");

        Assert.Equal(text.Replace("SpriteType = \"a\"", "SpriteType = \"c\""), Apply(document, edit));
    }

    [Fact]
    public void Base_value_edit_inserts_in_the_element_when_only_the_block_sets_it()
    {
        var text = "iconType = {\n\tname = \"x\"\n\tif_resolution = {\n\t\tmin_height = 868\n\t\tspritetype = \"b\"\n\t}\n}\n";
        var document = GuiParser.Parse(text);

        var result = Apply(document, ClassicEdits.SetBaseValue(document, Find(document, "iconType"), "spriteType", "\"c\""));

        Assert.Equal(text.Replace("\tname = \"x\"\n", "\tname = \"x\"\n\tspriteType = \"c\"\n"), result);
    }

    [Fact]
    public void Windows1252_text_survives_pair_edit()
    {
        var original = "# Kommentar mit é\r\niconType = {\r\n\tposition = { x = 1 y = 2 } # fenêtre\r\n}\r\n";
        var document = GuiParser.Parse(original);

        var edit = ClassicEdits.SetPair(document, Find(document, "position"), "position", Xy, "9", "2");
        var result = Apply(document, edit);

        Assert.Equal(original.Replace("x = 1", "x = 9"), result);
        Assert.Contains("é", result);
        Assert.Contains("fenêtre", result);
    }
}
