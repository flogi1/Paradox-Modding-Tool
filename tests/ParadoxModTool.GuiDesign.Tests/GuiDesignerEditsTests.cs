using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiDesignerEditsTests
{
    private const string Library =
        "types T {\n\ttype my_button = button {\n\t\tblock \"label\" { }\n\t}\n}\ntemplate shiny { }";

    private const string Overlay =
        "window = {\n\tsize = { 10 10 }\n\tmodify_texture = { }\n\tmy_button = { }\n\ttooltipwidget = { }\n}";

    private static GuiNode N(GuiDocument doc, string name) => doc.Root.Descendants().First(n => n.Name == name);

    private static string Apply(GuiDocument doc, GuiDesignEdit edit) => TextEdit.Apply(doc.Text, edit.Edits);

    /// <summary>Name des Knotens, auf den der Cursor nach der Aktion zeigt (im neu geparsten Text).</summary>
    private static string FocusName(GuiDocument doc, GuiDesignEdit edit) =>
        GuiParser.Parse(Apply(doc, edit)).FindDeepestAt(edit.Focus!.Value)!.Name;

    [Fact]
    public void InsertionRulesFollowTheKindOfTheTarget()
    {
        var (resolver, doc) = GuiResolverTests.Setup(Overlay, Library);
        var window = N(doc, "window");
        GuiInsertion? At(GuiNode? target, GuiPlacement placement, double y = 0) => GuiDesignerEdits.InsertionFor(target, placement, y, doc, resolver);

        Assert.Equal(new GuiInsertion(window, InsertPosition.LastChild, null), At(window, GuiPlacement.AtSelection));
        Assert.Equal(new GuiInsertion(window, InsertPosition.After, N(doc, "size")), At(N(doc, "size"), GuiPlacement.AtSelection));
        Assert.Equal(new GuiInsertion(window, InsertPosition.After, N(doc, "modify_texture")), At(N(doc, "modify_texture"), GuiPlacement.AtSelection));
        Assert.Equal(new GuiInsertion(doc.Root, InsertPosition.LastChild, null), At(null, GuiPlacement.AtSelection));
        Assert.Null(At(N(doc, "size"), GuiPlacement.Child));
        Assert.Equal(new GuiInsertion(window, InsertPosition.Before, N(doc, "my_button")), At(N(doc, "my_button"), GuiPlacement.Before));

        Assert.True(GuiDesignerEdits.AcceptsWidgets(N(doc, "tooltipwidget"), resolver));
        Assert.False(GuiDesignerEdits.AcceptsWidgets(N(doc, "size"), resolver));
        Assert.True(GuiDesignerEdits.AcceptsUsing(window, resolver));
        Assert.False(GuiDesignerEdits.AcceptsUsing(doc.Root, resolver));
        Assert.False(GuiDesignerEdits.AcceptsUsing(N(doc, "tooltipwidget"), resolver));
        Assert.True(GuiDesignerEdits.IsElement(N(doc, "my_button"), resolver));
        Assert.False(GuiDesignerEdits.IsElement(N(doc, "modify_texture"), resolver));
    }

    /// <summary>
    /// Klassisch (HOI IV, EU IV) nehmen nur <c>guiTypes</c> und Container-Elemente (containerWindowType, windowType, eu3dialogtype; jede
    /// Schreibweise) Kinder auf; Icons, Knöpfe, Texte, Listen und <c>background</c> nicht, auch wenn sie Eigenschaften haben.
    /// </summary>
    [Theory]
    [InlineData("hoi4")]
    [InlineData("eu4")]
    public void AcceptsWidgets_ClassicContainers(string game)
    {
        const string Text =
            "guiTypes = {\n"
            + "\tcontainerWindowType = {\n\t\tname = \"a\"\n\t\tbackground = { name = \"Background\" quadTextureSprite = \"x\" }\n"
            + "\t\tWindowType = { name = \"b\" }\n"
            + "\t\ticonType = { name = \"c\" }\n"
            + "\t\tbuttonType = { name = \"d\" }\n"
            + "\t\tguiButtonType = { name = \"e\" }\n"
            + "\t\tinstantTextBoxType = { name = \"f\" }\n"
            + "\t\tgridBoxType = { name = \"g\" }\n"
            + "\t\tposition = { x = 0 y = 0 }\n"
            + "\t}\n"
            + "\teu3dialogtype = { name = \"h\" }\n"
            + "}\n";
        var rules = ClassicLayoutKit.Rules(game);
        var doc = GuiParser.Parse(Text);
        var index = Index.GuiIndex.FromDocuments(Array.Empty<Index.GuiSourceFile>(), Array.Empty<GuiDocument>(), rules.Layout);
        var resolver = new Resolve.GuiResolver(index, rules, doc, "interface/edit.gui");
        bool Accepts(string name) => GuiDesignerEdits.AcceptsWidgets(N(doc, name), resolver);

        Assert.True(Accepts("guiTypes"));
        Assert.True(Accepts("containerWindowType"));
        Assert.True(Accepts("WindowType"));
        Assert.True(Accepts("eu3dialogtype"));
        Assert.False(GuiDesignerEdits.AcceptsWidgets(doc.Root, resolver));
        foreach (var name in new[] { "background", "iconType", "buttonType", "guiButtonType", "instantTextBoxType", "gridBoxType", "position" })
        {
            Assert.False(Accepts(name), name);
        }

        // Ohne Auswahl kommt das Element ans Ende von guiTypes, nicht ans Dateiende; neben guiTypes (Dateiebene) gibt es keine Einfügestelle.
        var guiTypes = N(doc, "guiTypes");
        Assert.Equal(new GuiInsertion(guiTypes, InsertPosition.LastChild, null), GuiDesignerEdits.InsertionFor(null, GuiPlacement.AtSelection, 0, doc, resolver));
        Assert.Equal(new GuiInsertion(guiTypes, InsertPosition.LastChild, null), GuiDesignerEdits.InsertionFor(doc.Root, GuiPlacement.AtSelection, 0, doc, resolver));
        Assert.Null(GuiDesignerEdits.InsertionFor(guiTypes, GuiPlacement.After, 0, doc, resolver));

        // Klassische Datei ohne guiTypes (leer): ans Dateiende wie bisher.
        var empty = GuiParser.Parse(string.Empty);
        Assert.Equal(new GuiInsertion(empty.Root, InsertPosition.LastChild, null), GuiDesignerEdits.InsertionFor(null, GuiPlacement.AtSelection, 0, empty, resolver));
    }

    [Fact]
    public void DropRulesAndMoves()
    {
        var (resolver, doc) = GuiResolverTests.Setup(Overlay, Library);
        var window = N(doc, "window");
        var button = N(doc, "my_button");
        GuiInsertion? Drop(GuiNode target, double y) => GuiDesignerEdits.InsertionFor(target, GuiPlacement.Drop, y, doc, resolver);

        Assert.Equal(new GuiInsertion(window, InsertPosition.Before, button), Drop(button, 0.1));
        Assert.Equal(new GuiInsertion(window, InsertPosition.After, button), Drop(button, 0.9));
        Assert.Equal(new GuiInsertion(button, InsertPosition.LastChild, null), Drop(button, 0.5));
        Assert.Equal(new GuiInsertion(window, InsertPosition.After, N(doc, "size")), Drop(N(doc, "size"), 0.5));

        var (moveResolver, moveDoc) = GuiResolverTests.Setup("window = {\n\thbox = {\n\t}\n\ticon = { }\n}");
        var hbox = N(moveDoc, "hbox");
        var move = GuiDesignerEdits.Move(moveDoc, N(moveDoc, "icon"), GuiDesignerEdits.InsertionFor(hbox, GuiPlacement.Drop, 0.5, moveDoc, moveResolver)!);
        Assert.Equal("window = {\n\thbox = {\n\t\ticon = { }\n\t}\n}", Apply(moveDoc, move));
        Assert.Equal("icon", FocusName(moveDoc, move));
        Assert.True(GuiDesignerEdits.Move(moveDoc, hbox, new GuiInsertion(hbox, InsertPosition.LastChild, null)).IsEmpty);
        Assert.True(GuiDesignerEdits.Move(moveDoc, N(moveDoc, "window"), new GuiInsertion(hbox, InsertPosition.LastChild, null)).IsEmpty);
    }

    [Fact]
    public void InsertIntoAnEmptyFileGoesToTheEnd()
    {
        var (resolver, doc) = GuiResolverTests.Setup(string.Empty, Library);

        var edit = GuiDesignerEdits.Insert(doc, GuiDesignerEdits.InsertionFor(null, GuiPlacement.AtSelection, 0, doc, resolver)!, "hbox = { }");

        Assert.Equal("hbox = { }\n", Apply(doc, edit));
        Assert.Equal(0, edit.Focus);
    }

    [Fact]
    public void InsertPutsTheCursorOnTheNewElement()
    {
        var (resolver, doc) = GuiResolverTests.Setup(Overlay, Library);

        var edit = GuiDesignerEdits.Insert(doc, GuiDesignerEdits.InsertionFor(N(doc, "window"), GuiPlacement.AtSelection, 0, doc, resolver)!, "icon = { }");

        Assert.Equal(
            "window = {\n\tsize = { 10 10 }\n\tmodify_texture = { }\n\tmy_button = { }\n\ttooltipwidget = { }\n\ticon = { }\n}",
            Apply(doc, edit));
        Assert.Equal("icon", FocusName(doc, edit));
    }

    [Fact]
    public void DeleteDuplicateAndMoveKeepTheCursorOnTheElement()
    {
        var (_, doc) = GuiResolverTests.Setup("window = {\n\ta = { }\n\tb = { }\n}");
        var a = N(doc, "a");
        var b = N(doc, "b");

        var delete = GuiDesignerEdits.Delete(doc, b);
        Assert.Equal("window = {\n\ta = { }\n}", Apply(doc, delete));
        Assert.Equal("a", FocusName(doc, delete));

        var duplicate = GuiDesignerEdits.Duplicate(doc, a);
        var duplicated = GuiParser.Parse(Apply(doc, duplicate));
        Assert.Equal("window = {\n\ta = { }\n\ta = { }\n\tb = { }\n}", duplicated.Text);
        Assert.Same(duplicated.Root.Children[0].Children[1], duplicated.FindDeepestAt(duplicate.Focus!.Value));

        var down = GuiDesignerEdits.MoveDown(doc, a);
        var movedDown = GuiParser.Parse(Apply(doc, down));
        Assert.Equal("window = {\n\tb = { }\n\ta = { }\n}", movedDown.Text);
        Assert.Same(movedDown.Root.Children[0].Children[1], movedDown.FindDeepestAt(down.Focus!.Value));

        var up = GuiDesignerEdits.MoveUp(doc, b);
        var movedUp = GuiParser.Parse(Apply(doc, up));
        Assert.Equal("window = {\n\tb = { }\n\ta = { }\n}", movedUp.Text);
        Assert.Same(movedUp.Root.Children[0].Children[0], movedUp.FindDeepestAt(up.Focus!.Value));

        Assert.True(GuiDesignerEdits.MoveUp(doc, a).IsEmpty);
    }

    [Fact]
    public void RebindFindsTheNodeAfterTheTextChanged()
    {
        var old = GuiParser.Parse("window = {\n\ta = { }\n\tb = { }\n}");
        var staleB = old.Root.Children[0].Children[1];
        var fresh = GuiParser.Parse("# neu\nwindow = {\n\ta = { }\n\tb = { }\n}");

        var rebound = GuiDesignerEdits.Rebind(staleB, old, fresh)!;

        Assert.Equal(staleB.Span.Start + 6, rebound.Span.Start);
        Assert.Equal("# neu\nwindow = {\n\ta = { }\n}", TextEdit.Apply(fresh.Text, GuiDesignerEdits.Delete(fresh, rebound).Edits));
        Assert.Null(GuiDesignerEdits.Rebind(staleB, old, GuiParser.Parse("window = {\n\ta = { }\n}")));
        Assert.Same(fresh.Root, GuiDesignerEdits.Rebind(old.Root, old, fresh));
    }

    [Fact]
    public void RebindRefusesOlderParsesAndLookalikes()
    {
        var old = GuiParser.Parse("window = {\n\ta = { }\n\tb = { }\n}");
        var staleB = old.Root.Children[0].Children[1];
        var shown = GuiParser.Parse(old.Text);

        // Der Knoten stammt aus einem älteren Parse als dem angezeigten (Baum noch nicht neu aufgebaut).
        Assert.Null(GuiDesignerEdits.Rebind(staleB, shown, shown));

        // Vor dem Ziel steht jetzt ein gleichnamiges anderes Element: der Pfad zeigt darauf, der Text passt nicht.
        var lookalike = GuiParser.Parse("window = {\n\ta = { }\n\tb = { x = 1 }\n\tb = { }\n}");
        Assert.Null(GuiDesignerEdits.Rebind(staleB, old, lookalike));
    }

    [Fact]
    public void PropertiesBlocksAndTemplates()
    {
        var (_, doc) = GuiResolverTests.Setup("window = {\n\tsize = { 1 1 }\n\tmy_button = { }\n}", Library);
        var window = N(doc, "window");

        var set = GuiDesignerEdits.SetProperty(doc, window, "alpha", "0.5");
        Assert.Equal("window = {\n\tsize = { 1 1 }\n\talpha = 0.5\n\tmy_button = { }\n}", Apply(doc, set));
        Assert.Equal("window", FocusName(doc, set));
        Assert.Equal("window = {\n\tsize = { 2 2 }\n\tmy_button = { }\n}", Apply(doc, GuiDesignerEdits.SetValue(doc, N(doc, "size"), "{ 2 2 }")));
        Assert.Equal("window = {\n\tmy_button = { }\n}", Apply(doc, GuiDesignerEdits.RemoveProperty(doc, N(doc, "size"))));
        Assert.Equal(
            "window = {\n\tsize = { 1 1 }\n\tmy_button = {\n\t\tblockoverride \"label\" {\n\t\t}\n\t}\n}",
            Apply(doc, GuiDesignerEdits.OverrideBlock(doc, N(doc, "my_button"), "label")));
        Assert.Equal("window = {\n\tsize = { 1 1 }\n\tusing = shiny\n\tmy_button = { }\n}", Apply(doc, GuiDesignerEdits.AddUsing(doc, window, "shiny")));

        var (_, withUsing) = GuiResolverTests.Setup("window = {\n\tusing = shiny\n}", Library);
        Assert.True(GuiDesignerEdits.AddUsing(withUsing, withUsing.Root.Children[0], "shiny").IsEmpty);
    }

    [Fact]
    public void WrapPutsTheCursorOnTheNewBox()
    {
        var (_, doc) = GuiResolverTests.Setup("window = {\n\ticon = { }\n}");

        var wrap = GuiDesignerEdits.WrapIn(doc, N(doc, "icon"), "hbox");

        Assert.Equal("window = {\n\thbox = {\n\t\ticon = { }\n\t}\n}", Apply(doc, wrap));
        Assert.Equal("hbox", FocusName(doc, wrap));
    }
}
