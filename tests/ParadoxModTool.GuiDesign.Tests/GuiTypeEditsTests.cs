using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiTypeEditsTests
{
    private const string Types =
        "types T {\n\ttype button_standard = button {\n\t\tblock \"text\" { }\n\t}\n\ttype button_round = button {\n\t}\n}\ntemplate tpl {\n\talpha = 1\n}\n";

    private static readonly GuiRules Rules = new(new[] { "size", "background" }, new[] { "tooltipwidget" }, Array.Empty<string>());

    private static GuiIndex Index() => GuiIndexTests.Build((GuiIndexTests.F("in_game/gui/types.gui"), Types));

    private static (GuiDocument Doc, GuiResolver Resolver) Open(string text)
    {
        var doc = GuiParser.Parse(text);
        return (doc, new GuiResolver(Index(), Rules, doc));
    }

    private static GuiNode Nth(GuiDocument doc, string name, int n = 0) =>
        doc.Root.Descendants().Where(x => x.Kind == GuiNodeKind.KeyedBlock && x.Name == name).ElementAt(n);

    private static string Apply(GuiDocument doc, GuiDesignEdit edit) => TextEdit.Apply(doc.Text, edit.Edits);

    [Fact]
    public void NamesAreIdentifiers()
    {
        Assert.True(GuiTypeEdits.IsValidName("mymod_button_2"));
        Assert.True(GuiTypeEdits.IsValidName("_x"));
        Assert.False(GuiTypeEdits.IsValidName("2x"));
        Assert.False(GuiTypeEdits.IsValidName("my-button"));
        Assert.False(GuiTypeEdits.IsValidName(string.Empty));
    }

    [Fact]
    public void NameIsTakenByIndexUnsavedTextAndEngineWidgets()
    {
        var unsaved = GuiParser.Parse("types L {\n\ttype local_new = button {\n\t}\n}\n");
        bool Engine(string name) => name == "hbox";

        Assert.True(GuiTypeEdits.IsTaken("button_standard", GuiDefinitionKind.Type, Index(), null, Engine));
        Assert.True(GuiTypeEdits.IsTaken("local_new", GuiDefinitionKind.Type, Index(), unsaved, Engine));
        Assert.True(GuiTypeEdits.IsTaken("hbox", GuiDefinitionKind.Type, Index(), null, Engine));
        Assert.True(GuiTypeEdits.IsTaken("tpl", GuiDefinitionKind.Template, Index(), null, Engine));
        Assert.False(GuiTypeEdits.IsTaken("tpl", GuiDefinitionKind.Type, Index(), null, Engine));
        Assert.False(GuiTypeEdits.IsTaken("local_new", GuiDefinitionKind.Type, Index(), null, Engine));
    }

    [Fact]
    public void SuggestionStartsWithTheModNameAndCountsUp()
    {
        Assert.Equal("my_mod", GuiTypeEdits.ModBase(@"C:\mods\My Mod"));
        Assert.Equal("MyModTypes", GuiTypeEdits.TypesContainerFor(@"C:\mods\My Mod"));
        Assert.Equal("mymod_button_standard", GuiTypeEdits.SuggestName("mymod", "button_standard", _ => false));
        Assert.Equal("mymod_button_standard_2", GuiTypeEdits.SuggestName("mymod", "button_standard", n => n == "mymod_button_standard"));
        Assert.Equal("mymod_button_2", GuiTypeEdits.SuggestName("mymod", "mymod_button", n => n == "mymod_button"));
    }

    [Fact]
    public void OnlyWidgetsCanBeRetyped()
    {
        var (doc, resolver) = Open("window = {\n\tbutton_standard = { size = { 1 1 } }\n\thbox = { }\n\ttooltipwidget = { }\n}\n");

        Assert.True(GuiTypeEdits.CanRetype(Nth(doc, "button_standard"), resolver));
        Assert.True(GuiTypeEdits.CanRetype(Nth(doc, "hbox"), resolver));
        Assert.False(GuiTypeEdits.CanRetype(Nth(doc, "size"), resolver));
        Assert.False(GuiTypeEdits.CanRetype(Nth(doc, "tooltipwidget"), resolver));
        Assert.Throws<ArgumentException>(() => GuiTypeEdits.Retype(doc, doc.Root.Descendants().First(n => n.Kind == GuiNodeKind.ListItem), "x"));
    }

    [Fact]
    public void DeriveAddsTheTypeToTheLastContainerAndRetypesTheChosenSpots()
    {
        var (doc, resolver) = Open("types T {\n\ttype panel = widget {\n\t\tbutton_standard = { }\n\t\tbutton_standard = { size = { 1 1 } }\n\t}\n}\n");
        var first = Nth(doc, "button_standard");
        var spots = GuiTypeEdits.SameKeySpots(doc, first, resolver);

        var edit = GuiTypeEdits.Derive(doc, first, "mymod_button_standard", spots, "MyModTypes");

        var expected = "types T {\n\ttype panel = widget {\n\t\tmymod_button_standard = { }\n\t\tmymod_button_standard = { size = { 1 1 } }\n\t}\n" +
            "\ttype mymod_button_standard = button_standard {\n\t}\n}\n";
        Assert.Equal(expected, Apply(doc, edit));
        Assert.Equal(expected.IndexOf("type mymod_button_standard", StringComparison.Ordinal), edit.Focus);
        Assert.Single(spots);
    }

    [Fact]
    public void DeriveCreatesAContainerAtTheEndAndKeepsCrlf()
    {
        var (doc, _) = Open("window = {\r\n\tbutton_standard = { }\r\n}\r\n");

        var edit = GuiTypeEdits.Derive(doc, Nth(doc, "button_standard"), "mymod_b", Array.Empty<GuiNode>(), "MyModTypes");

        Assert.Equal(
            "window = {\r\n\tmymod_b = { }\r\n}\r\ntypes MyModTypes {\r\n\ttype mymod_b = button_standard {\r\n\t}\r\n}\r\n",
            Apply(doc, edit));
    }

    [Fact]
    public void ExtractMovesEverythingButTheName()
    {
        var (doc, _) = Open(
            "types T {\n\ttype panel = widget {\n\t\tbutton_standard = {\n\t\t\tname = \"ok\"\n\t\t\tsize = { 10 20 }\n" +
            "\t\t\tblockoverride \"text\" {\n\t\t\t\ttext = \"A\"\n\t\t\t}\n\t\t}\n\t}\n}\n");

        var edit = GuiTypeEdits.Extract(doc, Nth(doc, "button_standard"), "mymod_ok_button", "MyModTypes");

        var expected = "types T {\n\ttype panel = widget {\n\t\tmymod_ok_button = { name = \"ok\" }\n\t}\n" +
            "\ttype mymod_ok_button = button_standard {\n\t\tsize = { 10 20 }\n\t\tblockoverride \"text\" {\n\t\t\ttext = \"A\"\n\t\t}\n\t}\n}\n";
        Assert.Equal(expected, Apply(doc, edit));
        Assert.Equal(expected.IndexOf("mymod_ok_button = { name", StringComparison.Ordinal), edit.Focus);
    }

    [Fact]
    public void ExtractWorksOnAnInlineWidget()
    {
        var (doc, _) = Open("window = { hbox = { button_standard = { size = { 1 1 } } } }\n");

        var edit = GuiTypeEdits.Extract(doc, Nth(doc, "button_standard"), "mymod_b", "MyModTypes");

        Assert.Equal(
            "window = { hbox = { mymod_b = { } } }\ntypes MyModTypes {\n\ttype mymod_b = button_standard {\n\t\tsize = { 1 1 }\n\t}\n}\n",
            Apply(doc, edit));
    }

    [Fact]
    public void ExtractAtTheEndOfAFileWithoutNewline()
    {
        var (doc, _) = Open("window = { button_standard = { size = { 1 1 } } }");

        var result = Apply(doc, GuiTypeEdits.Extract(doc, Nth(doc, "button_standard"), "mymod_b", "MyModTypes"));

        Assert.Equal("window = { mymod_b = { } }\ntypes MyModTypes {\n\ttype mymod_b = button_standard {\n\t\tsize = { 1 1 }\n\t}\n}", result);
        Assert.Empty(GuiParser.Parse(result).Diagnostics);
    }

    [Fact]
    public void RetypeChangesOnlyTheKey()
    {
        var (doc, _) = Open("w = {\n\tbutton_standard = { }\n}");

        Assert.Equal("w = {\n\tbutton_round = { }\n}", Apply(doc, GuiTypeEdits.Retype(doc, Nth(doc, "button_standard"), "button_round")));
        Assert.True(GuiTypeEdits.Retype(doc, Nth(doc, "button_standard"), "button_standard").IsEmpty);
    }

    [Fact]
    public void LostBlockOverridesAreThoseTheNewKeyDoesNotOffer()
    {
        var (doc, resolver) = Open("w = {\n\tbutton_standard = {\n\t\tblockoverride \"text\" { }\n\t\tblockoverride \"icon\" { }\n\t}\n}");
        var instance = Nth(doc, "button_standard");

        Assert.Equal(new[] { "text", "icon" }, GuiTypeEdits.LostBlockOverrides(instance, "button_round", resolver));
        Assert.Equal(new[] { "icon" }, GuiTypeEdits.LostBlockOverrides(instance, "button_standard", resolver));
    }

    [Fact]
    public void UsagesElsewhereLeaveOutThisFile()
    {
        var here = GuiIndexTests.F("in_game/gui/here.gui");
        var index = GuiIndexTests.Build(
            (GuiIndexTests.F("in_game/gui/types.gui"), Types),
            (here, "w = { button_standard = { } }"),
            (GuiIndexTests.F("in_game/gui/other.gui"), "v = { button_standard = { } button_standard = { } }"));

        Assert.Equal(2, GuiTypeEdits.UsagesElsewhere(index, "button_standard", here.FullPath));
        Assert.Equal(3, GuiTypeEdits.UsagesElsewhere(index, "button_standard", null));
    }

    [Fact]
    public void ScopeCountsUnsavedUsesInThisFileAndSavedUsesElsewhere()
    {
        var here = GuiIndexTests.F("in_game/gui/here.gui");
        var index = GuiIndexTests.Build((here, "w = { }"), (GuiIndexTests.F("in_game/gui/other.gui"), "v = { mymod_b = { } }"));
        var live = GuiParser.Parse("types T {\n\ttype mymod_b = button {\n\t}\n}\nw = {\n\tmymod_b = { }\n\tmymod_b = { }\n}\n");
        var definition = live.Root.Descendants().First(n => n.Kind == GuiNodeKind.TypeDefinition);

        Assert.Equal(3, GuiEditScope.Of(definition, index, here.FullPath).Usages);
        Assert.Equal(1, GuiEditScope.Of(definition, index).Usages);
    }

    [Theory]
    [InlineData("types Panels {\n\ttype panel = widget {\n\t\tbutton_a = { }\n\t}\n}\n", "panel")]
    [InlineData("template tpl_panel {\n\tbutton_a = { }\n}\n", "tpl_panel")]
    public void NewTypeInADraftIsANewDefinitionWithoutFileCopy(string vanilla, string enclosing)
    {
        var source = GuiOverridePlannerTests.V("in_game/gui/panel.gui");
        var index = GuiIndexTests.Build((source, vanilla), (GuiIndexTests.F("in_game/gui/buttons.gui"), "types B {\n\ttype button_a = button {\n\t}\n}\n"));
        var document = GuiParser.Parse(vanilla);
        var edit = GuiTypeEdits.Derive(document, Nth(document, "button_a"), "mymod_button_a", Array.Empty<GuiNode>(), "MyModTypes");
        var draft = TextEdit.Apply(vanilla, edit.Edits);

        var plan = GuiOverridePlanner.Plan(new GuiOverrideRequest(source, vanilla, draft, index, GuiOverridePlannerTests.Targets, _ => null));

        Assert.Null(plan.CopyReason);
        Assert.Empty(plan.Blockers);
        Assert.Contains(plan.Entries, e => e.Kind == GuiPlanEntryKind.NewDefinition && e.Name == "mymod_button_a");
        Assert.Contains(plan.Entries, e => e.Kind == GuiPlanEntryKind.Override && e.Name == enclosing);
        Assert.Contains("type mymod_button_a = button_a {", plan.Files.Single(f => f.FullPath == GuiOverridePlannerTests.TypesPath).NewText);
    }

    [Fact]
    public void ExtractTakesConstantsFromEnclosingBlocksAlong()
    {
        var (doc, _) = Open("types T {\n\ttype panel = widget {\n\t\t@w = 5\n\t\tbutton_standard = {\n\t\t\tsize = { @w @w }\n\t\t}\n\t}\n}\n");

        var result = Apply(doc, GuiTypeEdits.Extract(doc, Nth(doc, "button_standard"), "mymod_b", "MyModTypes"));

        Assert.Contains("\ttype mymod_b = button_standard {\n\t\t@w = 5\n\t\tsize = { @w @w }\n\t}", result);
        Assert.Empty(GuiParser.Parse(result).Diagnostics);
    }
}
