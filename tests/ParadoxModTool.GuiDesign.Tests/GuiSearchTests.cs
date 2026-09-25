using System.Diagnostics;
using System.Text;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiSearchTests
{
    private readonly ITestOutputHelper _output;

    public GuiSearchTests(ITestOutputHelper output) => _output = output;

    private static GuiIndex SearchIndex() => GuiIndexTests.Build(
        (GuiIndexTests.F("in_game/gui/a.gui"),
            "types T {\n\ttype my_button = button { }\n\ttype my_tooltip = widget { }\n\ttype plain_widget = widget { }\n\ttype info_box = vbox { }\n}\n" +
            "template shiny { }\n" +
            "window = {\n\tname = \"main_window\"\n\ttooltipwidget = { plain_widget = { } }\n\tmy_button = { name = \"ok\" }\n\ttext_single = { name = \"title\" }\n}"),
        (GuiIndexTests.F("in_game/gui/b.gui"), "container = { name = \"title\" }"),
        (GuiIndexTests.F("in_game/gui/c.gui", GuiLayer.Mod), "types M { type my_button = button { size = { 2 2 } } }"));

    [Fact]
    public void EntriesListEffectiveTypesAndEveryNamedWidget()
    {
        var index = SearchIndex();

        var entries = GuiElementSearch.Entries(index, new GuiResolver(index, GuiResolverTests.Rules));
        GuiElementEntry One(string name) => entries.Single(e => e.Name == name);

        Assert.Equal(GuiLayer.Mod, One("my_button").Definition.File.Layer);
        Assert.Equal(2, entries.Count(e => e.Name == "title"));
        Assert.Equal(GuiWidgetCategory.Button, One("my_button").Category);
        Assert.Equal(GuiWidgetCategory.Box, One("info_box").Category);
        Assert.Equal(GuiWidgetCategory.Window, One("main_window").Category);
        Assert.Equal(GuiWidgetCategory.Button, One("ok").Category);
        Assert.True(One("plain_widget").IsTooltip);
        Assert.True(One("my_tooltip").IsTooltip);
        Assert.False(One("info_box").IsTooltip);
        Assert.Equal(GuiDefinitionKind.Template, One("shiny").Kind);
    }

    [Fact]
    public void MatchesFiltersByNameAndKind()
    {
        var index = SearchIndex();
        var entries = GuiElementSearch.Entries(index, new GuiResolver(index, GuiResolverTests.Rules));

        IEnumerable<string> Names(string text, GuiElementFilter filter) =>
            entries.Where(e => GuiElementSearch.Matches(e, text, filter)).Select(e => e.Name);

        Assert.Equal(new[] { "shiny" }, Names(string.Empty, GuiElementFilter.Template));
        Assert.Equal(new[] { "my_button", "ok" }, Names(string.Empty, GuiElementFilter.Button));
        Assert.Equal(new[] { "my_button" }, Names("BUTTON", GuiElementFilter.All));
        Assert.Contains("info_box", Names(string.Empty, GuiElementFilter.Container));
        Assert.Equal(new[] { "my_tooltip", "plain_widget" }, Names(string.Empty, GuiElementFilter.Tooltip));
    }

    [Theory]
    [InlineData("\ttexture = \"gfx/interface/icons/my_icon.dds\"", 20, "gfx/interface/icons/my_icon.dds")]
    [InlineData(" my_key:0 \"Text\"", 3, "my_key")]
    [InlineData("scripted_gui = my_gui.", 17, "my_gui")]
    [InlineData("scripted_gui = my_gui", 21, "my_gui")]
    [InlineData("value = 100", 9, null)]
    [InlineData("a = b", 2, null)]
    public void WordAtTakesPathsAndKeys(string line, int column, string? expected)
    {
        Assert.Equal(expected, GuiReferenceSearch.WordAt(line, column));
    }

    [Fact]
    public void FindsWholeNamesOutsideCommentsWithTheirElement()
    {
        var index = GuiIndexTests.Build(
            (GuiIndexTests.F("in_game/gui/a.gui"),
                "window = {\n\tname = \"w\"\n\t# my_gui im Kommentar\n\tbutton = {\n\t\tonclick = \"[GetScriptedGui('my_gui').Execute( GuiScope.End )]\"\n\t\ttext = \"my_gui_title\"\n\t}\n}"),
            (GuiIndexTests.F("in_game/gui/b.gui", GuiLayer.Mod),
                "icon = { texture = \"gfx/my_gui/x.dds\" }\nwidget = { text = \"my_gui\" }"));
        var resolver = new GuiResolver(index, GuiResolverTests.Rules);

        var result = GuiReferenceSearch.Find(index, resolver, "my_gui");

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.FileCount);
        Assert.Equal(("b.gui", 2, "widget"), (Path.GetFileName(result.Hits[0].File.RelativePath), result.Hits[0].Line, result.Hits[0].Element!.Name));
        Assert.Equal(("a.gui", 5, "button"), (Path.GetFileName(result.Hits[1].File.RelativePath), result.Hits[1].Line, result.Hits[1].Element!.Name));
        Assert.StartsWith("onclick = ", result.Hits[1].LineText);
        Assert.Equal("icon", Assert.Single(GuiReferenceSearch.Find(index, resolver, "gfx/my_gui/x.dds").Hits).Element!.Name);
        Assert.Equal(0, GuiReferenceSearch.Find(index, resolver, "my").Total);
    }

    [Fact]
    public void FindCapsTheHitsAndCountsAll()
    {
        var text = new StringBuilder();
        for (var i = 0; i < 1200; i++)
        {
            text.Append("icon = { texture = \"gfx/x.dds\" }\n");
        }

        var index = GuiIndexTests.Build((GuiIndexTests.F("in_game/gui/many.gui"), text.ToString()));

        var result = GuiReferenceSearch.Find(index, new GuiResolver(index, GuiResolverTests.Rules), "gfx/x.dds");

        Assert.Equal(1200, result.Total);
        Assert.Equal(GuiReferenceSearch.MaxHits, result.Hits.Count);
        Assert.Equal(1, result.Hits[0].Line);
    }

    [Fact]
    public void VanillaSearchesReportTheirDuration()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var index = GuiRulesVanillaTests.Vanilla;
        var resolver = new GuiResolver(index, GuiRulesVanillaTests.ProfileRules());
        var watch = Stopwatch.StartNew();
        var entries = GuiElementSearch.Entries(index, resolver);
        var entriesMs = watch.ElapsedMilliseconds;
        watch.Restart();
        var result = GuiReferenceSearch.Find(index, resolver, "tooltipwidget");
        var findMs = watch.ElapsedMilliseconds;

        _output.WriteLine($"Suchliste {entries.Count} Einträge in {entriesMs} ms, davon {entries.Count(e => e.IsTooltip)} Tooltips; Textsuche tooltipwidget: {result.Total} Fundstellen in {findMs} ms");

        Assert.Contains(entries, e => e.IsTooltip);
        Assert.True(result.Total > 0);
    }
}
