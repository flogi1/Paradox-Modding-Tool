using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiTextEditMoveTests
{
    private const string Text =
        "window = {\n\thbox = {\n\t\ticon = {\n\t\t\tsize = { 1 1 }\n\t\t}\n\t}\n\tvbox = {\n\t}\n\tbutton = { }\n}";

    private static GuiNode Find(GuiDocument doc, string name) => doc.Root.Descendants().First(n => n.Name == name);

    [Fact]
    public void EditsAtTheSameOffsetApplyInListOrder()
    {
        var inserts = new[] { new TextEdit(1, 0, "a"), new TextEdit(1, 0, "b") };
        Assert.Equal("XbaY", TextEdit.Apply("XY", inserts));
        Assert.Equal(2, TextEdit.PositionInResult(inserts, 0, 0));
        Assert.Equal(1, TextEdit.PositionInResult(inserts, 1, 0));

        var removeThenInsert = new[] { new TextEdit(1, 2, string.Empty), new TextEdit(1, 0, "Z") };
        Assert.Equal("AZD", TextEdit.Apply("ABCD", removeThenInsert));
        Assert.Equal(1, TextEdit.PositionInResult(removeThenInsert, 1, 0));
        Assert.Equal(new[] { 1, 1 }, TextEdit.InApplyOrder(removeThenInsert).Select(e => e.Offset));
        Assert.Equal(2, TextEdit.InApplyOrder(removeThenInsert)[0].Length);
    }

    [Fact]
    public void MapOffsetFollowsTheText()
    {
        var edits = new[] { new TextEdit(2, 2, "xyz"), new TextEdit(7, 0, "--") };
        var result = TextEdit.Apply("0123456789", edits);

        Assert.Equal("01xyz456--789", result);
        Assert.Equal(1, TextEdit.MapOffset(1, edits));
        Assert.Equal(2, TextEdit.MapOffset(3, edits));
        Assert.Equal(result.IndexOf('4'), TextEdit.MapOffset(4, edits));
        Assert.Equal(result.IndexOf('7'), TextEdit.MapOffset(7, edits));
        Assert.Equal(result.IndexOf('9'), TextEdit.MapOffset(9, edits));
    }

    [Fact]
    public void SetValueChangesExactlyThisLine()
    {
        var doc = GuiParser.Parse("widget = {\n\talpha = 1\n\talpha = 0.5\n\tsize = { 1 1 }\n}");
        var edits = new GuiTextEdits(doc);
        var first = doc.Root.Children[0].Children[0];

        Assert.Equal("widget = {\n\talpha = 0.2\n\talpha = 0.5\n\tsize = { 1 1 }\n}", TextEdit.Apply(doc.Text, edits.SetValue(first, "0.2")));
        Assert.Equal("widget = {\n\talpha = 1\n\talpha = 0.5\n\tsize = { 3 4 }\n}", TextEdit.Apply(doc.Text, edits.SetValue(Find(doc, "size"), "{ 3 4 }")));
        Assert.Empty(edits.SetValue(first, "1"));
    }

    [Fact]
    public void MoveNodeReindentsAndRemovesTheOldLines()
    {
        var doc = GuiParser.Parse(Text);
        var edits = new GuiTextEdits(doc);

        var intoVbox = edits.MoveNode(Find(doc, "icon"), Find(doc, "vbox"), InsertPosition.LastChild);
        Assert.Equal(
            "window = {\n\thbox = {\n\t}\n\tvbox = {\n\t\ticon = {\n\t\t\tsize = { 1 1 }\n\t\t}\n\t}\n\tbutton = { }\n}",
            TextEdit.Apply(doc.Text, intoVbox));
        Assert.Equal(string.Empty, intoVbox[0].Text);

        var buttonFirst = edits.MoveNode(Find(doc, "button"), Find(doc, "window"), InsertPosition.Before, Find(doc, "hbox"));
        Assert.Equal(
            "window = {\n\tbutton = { }\n\thbox = {\n\t\ticon = {\n\t\t\tsize = { 1 1 }\n\t\t}\n\t}\n\tvbox = {\n\t}\n}",
            TextEdit.Apply(doc.Text, buttonFirst));
        Assert.Empty(GuiParser.Parse(TextEdit.Apply(doc.Text, buttonFirst)).Diagnostics);
    }

    [Fact]
    public void MoveNodeRejectsItsOwnPlaceAndDescendants()
    {
        var doc = GuiParser.Parse(Text);
        var edits = new GuiTextEdits(doc);
        var window = Find(doc, "window");

        Assert.Empty(edits.MoveNode(Find(doc, "hbox"), Find(doc, "icon"), InsertPosition.LastChild));
        Assert.Empty(edits.MoveNode(Find(doc, "hbox"), Find(doc, "hbox"), InsertPosition.LastChild));
        Assert.Empty(edits.MoveNode(Find(doc, "vbox"), window, InsertPosition.After, Find(doc, "hbox")));
        Assert.Empty(edits.MoveNode(Find(doc, "vbox"), window, InsertPosition.Before, Find(doc, "button")));
        Assert.Empty(edits.MoveNode(Find(doc, "button"), window, InsertPosition.LastChild));

        var inline = GuiParser.Parse("a = { b = { } c = { } }");
        Assert.Empty(new GuiTextEdits(inline).MoveNode(Find(inline, "b"), Find(inline, "a"), InsertPosition.LastChild));
    }

    [Fact]
    public void FindExactNeedsEveryStep()
    {
        var old = GuiParser.Parse(Text);
        var path = GuiNodePath.Of(Find(old, "icon"));

        var shifted = GuiParser.Parse("# neu\n" + Text);
        var found = path.FindExact(shifted)!;
        Assert.Equal("icon", found.Name);
        Assert.Equal(Find(old, "icon").Span.Start + 6, found.Span.Start);

        var removed = GuiParser.Parse("window = {\n\thbox = {\n\t}\n}");
        Assert.Null(path.FindExact(removed));
        Assert.Equal("hbox", path.Find(removed)!.Name);
    }
}
