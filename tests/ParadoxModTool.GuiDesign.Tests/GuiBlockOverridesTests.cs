using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiBlockOverridesTests
{
    private const string Library =
        "types T {\n\ttype base_button = button {\n\t\tblock \"label\" {\n\t\t\ttext_single = {\n\t\t\t\ttext = \"A\"\n\t\t\t\tsize = { 10 10 }\n\t\t\t}\n\t\t}\n\t\ticon = { }\n\t}\n" +
        "\ttype my_button = base_button {\n\t\tblockoverride \"label\" {\n\t\t\ttext_single = { text = \"B\" }\n\t\t}\n\t}\n}";

    private const string Overlay = "window = {\n\tbase_button = { }\n\tmy_button = { }\n}";

    [Fact]
    public void EffectiveContentFollowsTheTypeChain()
    {
        var (resolver, doc) = GuiResolverTests.Setup(Overlay, Library);
        var window = doc.Root.Children[0];

        var inBase = GuiBlockOverrides.EffectiveContent(window.Children[0], "label", resolver)!;
        var inDerived = GuiBlockOverrides.EffectiveContent(window.Children[1], "label", resolver)!;

        Assert.Equal(GuiNodeKind.Block, inBase.Kind);
        Assert.Equal("text_single = {\n\ttext = \"A\"\n\tsize = { 10 10 }\n}", GuiBlockOverrides.ContentOf(inBase, resolver));
        Assert.Equal(GuiNodeKind.BlockOverride, inDerived.Kind);
        Assert.Equal("text_single = { text = \"B\" }", GuiBlockOverrides.ContentOf(inDerived, resolver));
        Assert.Null(GuiBlockOverrides.EffectiveContent(window.Children[0], "missing", resolver));
        Assert.Equal("a = 1", GuiBlockOverrides.Relative(" a = 1 "));
    }

    [Fact]
    public void InheritedElementsNameTheirBlock()
    {
        var (resolver, doc) = GuiResolverTests.Setup(Overlay, Library);
        var instance = doc.Root.Children[0].Children[0];
        var element = resolver.Resolve(instance);
        var text = element.Children.Single(c => c.Key == "text_single");
        var icon = element.Children.Single(c => c.Key == "icon");

        Assert.True(GuiBlockOverrides.IsInherited(text, instance));
        var source = GuiBlockOverrides.ContainingBlock(text)!;
        Assert.Equal("label", source.BlockName);
        Assert.Equal(GuiNodeKind.Block, source.Owner.Kind);
        Assert.Equal("type base_button = button", source.DefinitionLabel);
        Assert.Null(GuiBlockOverrides.ContainingBlock(icon));
        Assert.Equal("type base_button = button", GuiBlockOverrides.DefinitionLabelOf(icon.Origin.Node));

        var (ownResolver, ownDoc) = GuiResolverTests.Setup("base_button = {\n\tblockoverride \"label\" {\n\t\ttext_single = { }\n\t}\n}", Library);
        var own = ownDoc.Root.Children[0];
        Assert.False(GuiBlockOverrides.IsInherited(ownResolver.Resolve(own).Children.Single(c => c.Key == "text_single"), own));
    }

    [Fact]
    public void OverrideTakesOverTheContentAndPutsTheCursorOnTheElement()
    {
        var (resolver, doc) = GuiResolverTests.Setup(Overlay, Library);
        var instance = doc.Root.Children[0].Children[0];
        var content = GuiBlockOverrides.ContentOf(GuiBlockOverrides.EffectiveContent(instance, "label", resolver)!, resolver);

        var edit = GuiDesignerEdits.OverrideBlock(doc, instance, "label", content, "text_single");

        var text = TextEdit.Apply(doc.Text, edit.Edits);
        Assert.Equal(
            "window = {\n\tbase_button = {\n\t\tblockoverride \"label\" {\n\t\t\ttext_single = {\n\t\t\t\ttext = \"A\"\n\t\t\t\tsize = { 10 10 }\n\t\t\t}\n\t\t}\n\t}\n\tmy_button = { }\n}",
            text);
        Assert.Equal("text_single", GuiParser.Parse(text).FindDeepestAt(edit.Focus!.Value)!.Name);
        Assert.Equal("blockoverride \"x\" {\n}", GuiBlockOverrides.Snippet("x", string.Empty));
        Assert.Equal(
            "window = {\n\tbase_button = {\n\t\tblockoverride \"label\" {\n\t\t}\n\t}\n\tmy_button = { }\n}",
            TextEdit.Apply(doc.Text, GuiDesignerEdits.OverrideBlock(doc, instance, "label").Edits));
    }
}
