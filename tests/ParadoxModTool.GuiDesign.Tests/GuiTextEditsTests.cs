using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiTextEditsTests
{
    private static (GuiDocument Doc, GuiTextEdits Edits) Open(string text)
    {
        var doc = GuiParser.Parse(text);
        return (doc, new GuiTextEdits(doc));
    }

    private static GuiNode Find(GuiDocument doc, string name) => doc.Root.Descendants().First(n => n.Name == name);

    private static string Apply(GuiDocument doc, IReadOnlyList<TextEdit> edits) => TextEdit.Apply(doc.Text, edits);

    [Fact]
    public void SetPropertyReplacesOnlyTheValue()
    {
        var (doc, edits) = Open("widget = {\n\tsize = { 10 20 }\n\talpha = 1 # Deckkraft\n}");

        Assert.Equal("widget = {\n\tsize = { 10 20 }\n\talpha = 0.5 # Deckkraft\n}", Apply(doc, edits.SetProperty(Find(doc, "widget"), "alpha", "0.5")));
        Assert.Equal("widget = {\n\tsize = { 30 40 }\n\talpha = 1 # Deckkraft\n}", Apply(doc, edits.SetProperty(Find(doc, "widget"), "size", "{ 30 40 }")));
    }

    [Fact]
    public void SetPropertyWithTheSameValueChangesNothing()
    {
        var (doc, edits) = Open("widget = {\n\ttext = \"Hallo\"\n\tsize = { 1 2 }\n}");

        Assert.Empty(edits.SetProperty(Find(doc, "widget"), "text", "\"Hallo\""));
        Assert.Empty(edits.SetProperty(Find(doc, "widget"), "size", "{ 1 2 }"));
    }

    [Fact]
    public void SetPropertyInsertsAfterTheLastPropertyBeforeChildWidgets()
    {
        var (doc, edits) = Open("widget = {\n\tsize = { 10 20 }\n\ticon = {\n\t\ttexture = \"a\"\n\t}\n}");

        Assert.Equal(
            "widget = {\n\tsize = { 10 20 }\n\talpha = 0.5\n\ticon = {\n\t\ttexture = \"a\"\n\t}\n}",
            Apply(doc, edits.SetProperty(Find(doc, "widget"), "alpha", "0.5")));
    }

    [Fact]
    public void SetPropertyOnEmptyBlockOpensIt()
    {
        var (doc, edits) = Open("widget = { }");

        Assert.Equal("widget = {\n\tsize = { 1 1 }\n}", Apply(doc, edits.SetProperty(Find(doc, "widget"), "size", "{ 1 1 }")));
    }

    [Fact]
    public void InsertChildAsLastChildReindentsTheSnippet()
    {
        var (doc, edits) = Open("window = {\n\tsize = { 1 1 }\n}\n");

        Assert.Equal(
            "window = {\n\tsize = { 1 1 }\n\ticon = {\n\t\ttexture = \"a.dds\"\n\t}\n}\n",
            Apply(doc, edits.InsertChild(Find(doc, "window"), "icon = {\n\ttexture = \"a.dds\"\n}", InsertPosition.LastChild)));
    }

    [Fact]
    public void InsertUsesSpacesAndCrLfOfTheDocument()
    {
        var (doc, edits) = Open("window = {\r\n    size = { 1 1 }\r\n}\r\n");

        Assert.Equal("    ", edits.IndentUnit);
        Assert.Equal("\r\n", edits.Newline);
        Assert.Equal(
            "window = {\r\n    size = { 1 1 }\r\n    icon = {\r\n        texture = x\r\n    }\r\n}\r\n",
            Apply(doc, edits.InsertChild(Find(doc, "window"), "icon = {\n\ttexture = x\n}", InsertPosition.LastChild)));
    }

    [Fact]
    public void InsertBeforeAndAfterAnAnchor()
    {
        var (doc, edits) = Open("w = {\n\ta = 1\n\tb = 2\n}");
        var w = Find(doc, "w");

        Assert.Equal("w = {\n\tx = 0\n\ta = 1\n\tb = 2\n}", Apply(doc, edits.InsertChild(w, "x = 0", InsertPosition.Before, Find(doc, "a"))));
        Assert.Equal("w = {\n\ta = 1\n\tx = 0\n\tb = 2\n}", Apply(doc, edits.InsertChild(w, "x = 0", InsertPosition.After, Find(doc, "a"))));
        Assert.Equal("w = {\n\tx = 0\n\ta = 1\n\tb = 2\n}", Apply(doc, edits.InsertChild(w, "x = 0", InsertPosition.FirstChild)));
    }

    [Fact]
    public void InsertAfterAnInlineChildMovesTheClosingBraceToItsOwnLine()
    {
        var (doc, edits) = Open("w = { a = 1 }");

        Assert.Equal("w = { a = 1\n\tb = 2\n}", Apply(doc, edits.InsertChild(Find(doc, "w"), "b = 2", InsertPosition.LastChild)));
    }

    [Fact]
    public void InsertIntoTheDocumentAppendsAtTheEnd()
    {
        var (doc, edits) = Open("a = { }");

        Assert.Equal("a = { }\nb = { }", Apply(doc, edits.InsertChild(doc.Root, "b = { }", InsertPosition.LastChild)));
    }

    [Fact]
    public void RemoveNodeRemovesItsWholeLine()
    {
        var (doc, edits) = Open("w = {\n\ta = 1\n\tb = 2\n}");

        Assert.Equal("w = {\n\tb = 2\n}", Apply(doc, edits.RemoveNode(Find(doc, "a"))));
    }

    [Fact]
    public void RemoveInlineNodeRemovesOnlyTheNode()
    {
        var (doc, edits) = Open("w = { a = 1 b = 2 }");

        Assert.Equal("w = { b = 2 }", Apply(doc, edits.RemoveNode(Find(doc, "a"))));
    }

    [Fact]
    public void DuplicateNodeCopiesItsLines()
    {
        var (doc, edits) = Open("w = {\n\ta = { 1 2 }\n}");

        Assert.Equal("w = {\n\ta = { 1 2 }\n\ta = { 1 2 }\n}", Apply(doc, edits.DuplicateNode(Find(doc, "a"))));
    }

    [Fact]
    public void MoveUpAndDownSwapWithTheNeighbour()
    {
        var (doc, edits) = Open("w = {\n\ta = 1\n\t# Kommentar\n\tb = 2\n}");

        Assert.Equal("w = {\n\tb = 2\n\t# Kommentar\n\ta = 1\n}", Apply(doc, edits.MoveUp(Find(doc, "b"))));
        Assert.Equal("w = {\n\tb = 2\n\t# Kommentar\n\ta = 1\n}", Apply(doc, edits.MoveDown(Find(doc, "a"))));
        Assert.Empty(edits.MoveUp(Find(doc, "a")));
        Assert.Empty(edits.MoveDown(Find(doc, "b")));
    }

    [Fact]
    public void WrapInPutsTheNodeIntoANewBox()
    {
        var (doc, edits) = Open("w = {\n\ta = {\n\t\tsize = { 1 2 }\n\t}\n}");

        Assert.Equal(
            "w = {\n\thbox = {\n\t\ta = {\n\t\t\tsize = { 1 2 }\n\t\t}\n\t}\n}",
            Apply(doc, edits.WrapIn(Find(doc, "a"), "hbox")));
    }

    [Fact]
    public void AddBlockOverrideInsertsANamedBlock()
    {
        var (doc, edits) = Open("my_button = {\n}");

        Assert.Equal(
            "my_button = {\n\tblockoverride \"text\" {\n\t\ttext = \"x\"\n\t}\n}",
            Apply(doc, edits.AddBlockOverride(Find(doc, "my_button"), "text", "text = \"x\"")));
    }

    [Fact]
    public void RenameKeyReplacesOnlyTheKey()
    {
        var (doc, edits) = Open("w = {\n\tbutton_standard = { size = { 1 1 } }\n}");

        Assert.Equal("w = {\n\tmy_button = { size = { 1 1 } }\n}", Apply(doc, edits.RenameKey(Find(doc, "button_standard"), "my_button")));
    }

    [Fact]
    public void InvalidCallsThrow()
    {
        var (doc, edits) = Open("w = {\n\ta = 1\n}\nv = { }");

        Assert.Throws<ArgumentException>(() => edits.SetProperty(Find(doc, "a"), "x", "1"));
        Assert.Throws<ArgumentException>(() => edits.InsertChild(Find(doc, "v"), "x = 1", InsertPosition.After, Find(doc, "a")));
    }

    [Fact]
    public void EditsOnVanillaFilesTouchOnlyTheirRange()
    {
        foreach (var path in VanillaGui.Files().Where((_, i) => i % 10 == 0))
        {
            var text = File.ReadAllText(path);
            var doc = GuiParser.Parse(text);
            var edits = new GuiTextEdits(doc);
            var targets = doc.Root.Descendants()
                .Where(n => n.Kind == GuiNodeKind.KeyedBlock && n.CloseBrace >= 0 && n.Children.Any(c => c.Kind == GuiNodeKind.KeyedBlock))
                .Take(20)
                .ToList();

            foreach (var target in targets)
            {
                AssertLocal(text, doc, edits.SetProperty(target, "pmt_test", "1"), "pmt_test", target, path);
                AssertLocal(text, doc, edits.InsertChild(target, "pmt_child = {\n\tsize = { 1 1 }\n}", InsertPosition.LastChild), "pmt_child", target, path);

                var existing = target.Children.FirstOrDefault(c =>
                    c.Kind == GuiNodeKind.Property && c.ValueSpan is not null && ReferenceEquals(target.Children.Last(x => x.Name == c.Name), c));
                if (existing is not null)
                {
                    Assert.Empty(edits.SetProperty(target, existing.Name, doc.TextOf(existing.ValueSpan!.Value)));
                }
            }
        }
    }

    private static void AssertLocal(string original, GuiDocument doc, IReadOnlyList<TextEdit> edits, string addedName, GuiNode target, string path)
    {
        var edit = Assert.Single(edits);
        var result = TextEdit.Apply(original, edits);
        var tail = original.Length - (edit.Offset + edit.Length);

        Assert.Equal(original[..edit.Offset], result[..edit.Offset]);
        Assert.Equal(original[^tail..], result[^tail..]);

        var reparsed = GuiParser.Parse(result);
        Assert.Equal(doc.Diagnostics.Count, reparsed.Diagnostics.Count);
        var added = Assert.Single(reparsed.Root.Descendants(), n => n.Name == addedName);
        Assert.True(added.Parent!.Name == target.Name && added.Parent.Line == target.Line, $"{path}:{target.Line}: {addedName} landet unter {added.Parent.Name}:{added.Parent.Line}");
    }
}
