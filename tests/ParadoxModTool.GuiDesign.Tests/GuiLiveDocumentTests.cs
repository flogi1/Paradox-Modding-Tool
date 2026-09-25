using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiLiveDocumentTests
{
    [Fact]
    public void NodePathFindsTheSameNodeAfterAnEditElsewhere()
    {
        var before = GuiParser.Parse("window = {\n\thbox = {\n\t\ticon = { }\n\t\ticon = { name = \"b\" }\n\t}\n}");
        var path = GuiNodePath.Of(before.Root.Descendants().Last(n => n.Name == "icon"));

        var after = GuiParser.Parse("# neu\nwindow = {\n\tsize = { 1 1 }\n\thbox = {\n\t\ticon = { }\n\t\ticon = { name = \"b\" }\n\t}\n}");
        var found = path.Find(after)!;

        Assert.Equal("icon", found.Name);
        Assert.Equal("b", found.Children.Single(c => c.Name == "name").Value);
        Assert.True(path.SameAs(GuiNodePath.Of(found)));
    }

    [Fact]
    public void NodePathFallsBackToTheDeepestRemainingAncestor()
    {
        var before = GuiParser.Parse("window = { hbox = { icon = { } } }");
        var path = GuiNodePath.Of(before.Root.Descendants().First(n => n.Name == "icon"));

        Assert.Equal("hbox", path.Find(GuiParser.Parse("window = { hbox = { } }"))!.Name);
        Assert.Null(path.Find(GuiParser.Parse("other = { }")));
        Assert.False(path.SameAs(null));
    }

    [Fact]
    public void LiveDocumentKeepsTheLastUsableTreeWhileTheTextIsBroken()
    {
        var live = new GuiLiveDocument("window = { size = { 1 1 } }");
        Assert.False(live.IsStale);

        Assert.False(live.Update("window = { text = \"offen }"));
        Assert.True(live.IsStale);
        Assert.Equal(GuiDiagnosticKind.UnterminatedString, live.Problem!.Kind);
        Assert.Equal("window = { size = { 1 1 } }", live.Current.Text);

        Assert.True(live.Update("window = { text = \"zu\" }"));
        Assert.False(live.IsStale);
        Assert.Null(live.Problem);
    }

    [Fact]
    public void DiagnosticsPresentWhenOpeningDoNotMakeTheFileStale()
    {
        var live = new GuiLiveDocument("a = { }\n}\n");

        Assert.True(live.Update("a = { b = 1 }\n}\n"));
        Assert.False(live.Update("a = { b = 1 }\n}\n}\n"));
        Assert.Equal(GuiDiagnosticKind.UnexpectedCloseBrace, live.Problem!.Kind);
    }
}
