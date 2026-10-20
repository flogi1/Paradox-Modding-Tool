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

    private static GuiIndex RootIndex() => GuiIndexTests.Build(
        (GuiIndexTests.F("gui/window_county_view.gui"),
            "window = {\n\tsize = { 10 10 }\n\tbutton = { name = \"inner\" }\n}\n" +
            "widget = { name = \"named_root\" }\n" +
            "texticon = { icon = gold }\n" +
            "types T { type big_window = window { } }\n" +
            "template tpl { }\n" +
            "big_window = { }"),
        (GuiIndexTests.F("gui/shared/other.gui"), "container = { }"));

    [Fact]
    public void EntriesOfferTheUnnamedTopLevelWindowsOfAFileUnderItsFileName()
    {
        var index = RootIndex();

        var entries = GuiElementSearch.Entries(index, new GuiResolver(index, GuiResolverTests.Rules));

        var unnamed = entries.Where(e => e.IsUnnamed).OrderBy(e => e.Name == "other.gui").ThenBy(e => e.Definition.Line).ToList();
        Assert.Equal(
            new[] { ("window_county_view.gui", 1, "window"), ("window_county_view.gui", 9, "window"), ("other.gui", 1, "container") },
            unnamed.Select(e => (e.Name, e.Definition.Line, e.BaseWidget)).ToArray());
        Assert.All(unnamed, e => Assert.True(e.IsFileRoot));
        Assert.Equal(GuiWidgetCategory.Window, unnamed[0].Category);

        // Benannte Wurzel einmal (nicht zusätzlich unbenannt), ein inneres Widget ist keine Wurzel, Texticons sind keine Widgets.
        var named = Assert.Single(entries, e => e.Name == "named_root");
        Assert.True(named.IsFileRoot);
        Assert.False(named.IsUnnamed);
        Assert.False(Assert.Single(entries, e => e.Name == "inner").IsFileRoot);
        Assert.DoesNotContain(entries, e => e.Definition.Node.Name == "texticon");

        // Die Auswahl zeigt im Inspektor genau das Fenster aus der Datei.
        var target = GuiInspection.TargetFor(index, unnamed[0].Definition);
        Assert.Same(unnamed[0].Definition.Node, target.Node);
        Assert.Equal(unnamed[0].Definition.File.FullPath, target.FilePath);
    }

    [Fact]
    public void MatchesFiltersByFilePathAndTopLevelWindows()
    {
        var index = RootIndex();
        var entries = GuiElementSearch.Entries(index, new GuiResolver(index, GuiResolverTests.Rules));

        IEnumerable<(string, int)> Found(string text, GuiElementFilter filter, string file) =>
            entries.Where(e => GuiElementSearch.Matches(e, text, filter, file)).Select(e => (e.Name, e.Definition.Line)).OrderBy(t => t.Line);

        Assert.Equal(
            new[] { ("window_county_view.gui", 1), ("named_root", 5), ("window_county_view.gui", 9) },
            Found(string.Empty, GuiElementFilter.FileRoot, "COUNTY_view"));
        Assert.Equal(new[] { ("other.gui", 1) }, Found(string.Empty, GuiElementFilter.FileRoot, @"gui\shared"));
        Assert.Equal(new[] { ("inner", 3) }, Found(string.Empty, GuiElementFilter.Button, "gui/window_county_view.gui"));
        Assert.Empty(Found("inner", GuiElementFilter.All, "other"));

        // Der Name eines unbenannten Fensters ist sein Dateiname: "county" im Namensfeld findet es auch.
        Assert.Equal(new[] { ("window_county_view.gui", 1), ("window_county_view.gui", 9) }, Found("county", GuiElementFilter.All, string.Empty));
    }

    [Fact]
    public void ClassicTopLevelWindowsAreTheWindowsInGuiTypes()
    {
        var classic = new GuiLayout(Array.Empty<string>(), FirstWins: false, ModsBeforeVanilla: false, Dialect: GuiDialect.Classic);
        var files = new[]
        {
            (GuiIndexTests.F("interface/topbar.gui"),
                "guiTypes = {\n\tcontainerWindowType = { name = \"topbar\"\n\t\ticonType = { name = \"bg\" }\n\t}\n\twindowType = { size = { x = 1 y = 1 } }\n}"),
        };
        var index = GuiIndex.Build(files.Select(f => f.Item1).ToList(), _ => files[0].Item2, classic);

        var entries = GuiElementSearch.Entries(index, new GuiResolver(index, GuiResolverTests.Rules));

        Assert.Equal(
            new[] { ("topbar", false), ("topbar.gui", true) },
            entries.Where(e => GuiElementSearch.Matches(e, string.Empty, GuiElementFilter.FileRoot, "topbar")).Select(e => (e.Name, e.IsUnnamed)).OrderBy(t => t.Item2));
        Assert.False(Assert.Single(entries, e => e.Name == "bg").IsFileRoot);
        Assert.DoesNotContain(entries, e => e.Definition.Node.Name == "guiTypes");
    }

    /// <summary>
    /// Crusader Kings III, gui/window_county_view.gui: drei benannte Fenster (holding_view, holding_tracks_view, holding_type_selection_view).
    /// Der Name findet sie schon vorher; neu findet der Dateifilter sie alle, die Art "Oberste Fenster einer Datei" nur diese drei.
    /// </summary>
    [Fact]
    public void Ck3CountyViewWindowsAreFoundByTheirFile()
    {
        var ck3 = VanillaGui.For("ck3");
        var path = Path.Combine(ck3.GameDirectory, "gui", "window_county_view.gui");
        if (!File.Exists(path))
        {
            return;
        }

        var file = new GuiSourceFile(path, "gui/window_county_view.gui", GuiLayer.Vanilla, "game", 0, 0);
        var index = GuiIndex.Build(new[] { file }, _ => File.ReadAllText(path), new GuiLayout(Array.Empty<string>(), FirstWins: true));
        var entries = GuiElementSearch.Entries(index, new GuiResolver(index, GuiResolverTests.Rules));

        var roots = entries.Where(e => GuiElementSearch.Matches(e, string.Empty, GuiElementFilter.FileRoot, "window_county_view")).Select(e => e.Name).ToList();
        _output.WriteLine($"Oberste Fenster: {string.Join(", ", roots)}");

        Assert.Equal(new[] { "holding_tracks_view", "holding_type_selection_view", "holding_view" }, roots);
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

        _output.WriteLine($"Suchliste {entries.Count} Einträge in {entriesMs} ms, davon {entries.Count(e => e.IsTooltip)} Tooltips, "
            + $"{entries.Count(e => e.IsFileRoot)} oberste Fenster ({entries.Count(e => e.IsUnnamed)} unbenannt); Textsuche tooltipwidget: {result.Total} Fundstellen in {findMs} ms");

        Assert.Contains(entries, e => e.IsTooltip);
        Assert.True(result.Total > 0);
    }
}
