using ParadoxModTool.Mods;
using Xunit;
using static ParadoxModTool.ModOverview.Tests.TestMods;

namespace ParadoxModTool.ModOverview.Tests;

public class OverviewViewTests
{
    private static OverviewViewOptions Options(
        bool byGame = true,
        OverviewSortKey sort = OverviewSortKey.Name,
        bool descending = false,
        bool onlyPublished = false,
        bool onlyOpenBugs = false,
        bool onlyWarnings = false,
        string search = "",
        IReadOnlyList<string>? order = null,
        IReadOnlyCollection<string>? hidden = null) =>
        new(byGame, sort, descending, onlyPublished, onlyOpenBugs, onlyWarnings, search, order ?? Array.Empty<string>(), hidden ?? Array.Empty<string>());

    private static IReadOnlyList<OverviewSection> Build(
        OverviewViewOptions options,
        IReadOnlyList<OverviewAlert>? alerts = null,
        IReadOnlyDictionary<OverviewMod, BugCount>? bugs = null,
        params OverviewMod[] mods) =>
        OverviewView.Build(Snapshot(mods), alerts ?? Array.Empty<OverviewAlert>(), bugs ?? new Dictionary<OverviewMod, BugCount>(), options);

    private static IEnumerable<string> Names(IReadOnlyList<OverviewSection> sections) =>
        sections.SelectMany(s => s.Groups).Select(g => g.Entries[0].Mod.Mod.Name);

    [Fact]
    public void Build_ProjectFrameWithSubmodAndCompatch()
    {
        var main = Mod("Main");
        var addon = Mod("Addon", role: ModRole.Submod, root: main);
        var dev = Mod("Dev", role: ModRole.DevBranch, root: main);
        var patch = Mod("Patch", compatch: true, compatchTargets: new[] { "Main" });

        var group = Assert.Single(Assert.Single(Build(Options(), null, null, main, addon, dev, patch)).Groups);

        Assert.Same(main, group.ProjectRoot);
        Assert.Equal(new[] { "Main", "Addon", "Patch" }, group.Entries.Select(e => e.Mod.Mod.Name));
        Assert.Equal(new[] { dev }, group.Entries[0].DevBranches);
    }

    [Fact]
    public void Build_StandAloneModHasNoFrame()
    {
        var group = Assert.Single(Assert.Single(Build(Options(), null, null, Mod("Single"))).Groups);

        Assert.Null(group.ProjectRoot);
        Assert.Single(group.Entries);
    }

    [Fact]
    public void Build_GroupByGame_UsesGameOrderThenRest()
    {
        var sections = Build(Options(order: new[] { "ck3" }), null, null, Mod("A", "eu4"), Mod("B", "ck3"), Mod("C", "eu5"));

        Assert.Equal(new[] { "ck3", "eu4", "eu5" }, sections.Select(s => s.GameId));
        Assert.Equal("Crusader Kings III", sections[0].Title);
    }

    [Fact]
    public void Build_HiddenGamesLeftOut_BothModes()
    {
        var mods = new[] { Mod("A", "eu4"), Mod("B", "ck3") };

        Assert.Equal(new[] { "eu4" }, Build(Options(hidden: new[] { "ck3" }), null, null, mods).Select(s => s.GameId));
        Assert.Equal(new[] { "A" }, Names(Build(Options(byGame: false, hidden: new[] { "ck3" }), null, null, mods)));
    }

    [Fact]
    public void Build_AllGames_SingleSection()
    {
        var section = Assert.Single(Build(Options(byGame: false), null, null, Mod("B", "eu4"), Mod("A", "ck3")));

        Assert.Null(section.GameId);
        Assert.Equal(string.Empty, section.Title);
        Assert.Equal(new[] { "A", "B" }, section.Groups.Select(g => g.Entries[0].Mod.Mod.Name));
    }

    [Theory]
    [InlineData(OverviewSortKey.Name, false, "Alpha,Beta,Gamma")]
    [InlineData(OverviewSortKey.Name, true, "Gamma,Beta,Alpha")]
    [InlineData(OverviewSortKey.Subscribers, false, "Beta,Gamma,Alpha")]
    [InlineData(OverviewSortKey.Subscribers, true, "Alpha,Gamma,Beta")]
    [InlineData(OverviewSortKey.OpenBugs, false, "Beta,Gamma,Alpha")]
    [InlineData(OverviewSortKey.OpenBugs, true, "Alpha,Gamma,Beta")]
    [InlineData(OverviewSortKey.LastRelease, false, "Gamma,Beta,Alpha")]
    [InlineData(OverviewSortKey.LastRelease, true, "Alpha,Beta,Gamma")]
    public void Build_SortKeys(OverviewSortKey key, bool descending, string expected)
    {
        var alpha = Mod("Alpha", subscribers: 300, releaseDate: "2026-09-01");
        var beta = Mod("Beta", releaseDate: "2026-01-01");
        var gamma = Mod("Gamma", subscribers: 10);
        var bugs = new Dictionary<OverviewMod, BugCount> { [alpha] = new(5, 0), [gamma] = new(1, 0) };

        var sections = Build(Options(sort: key, descending: descending), null, bugs, gamma, alpha, beta);

        Assert.Equal(expected.Split(','), Names(sections));
    }

    [Fact]
    public void Build_Filters_KeepFrameWhenAnyMemberPasses()
    {
        var main = Mod("Main");
        var addon = Mod("Addon", role: ModRole.Submod, root: main, published: true);
        var other = Mod("Other");

        var group = Assert.Single(Assert.Single(Build(Options(onlyPublished: true), null, null, main, addon, other)).Groups);

        Assert.Same(main, group.ProjectRoot);
        Assert.Equal(new[] { addon }, group.Entries.Select(e => e.Mod));
    }

    [Fact]
    public void Build_OnlyOpenBugs_UsesProjectRootCount()
    {
        var main = Mod("Main");
        var addon = Mod("Addon", role: ModRole.Submod, root: main);
        var other = Mod("Other");
        var bugs = new Dictionary<OverviewMod, BugCount> { [main] = new(1, 1) };

        var sections = Build(Options(onlyOpenBugs: true), null, bugs, main, addon, other);

        var group = Assert.Single(Assert.Single(sections).Groups);
        Assert.Equal(new[] { "Main", "Addon" }, group.Entries.Select(e => e.Mod.Mod.Name));
        Assert.Equal(new BugCount(1, 1), group.Entries[0].Bugs);
        Assert.Equal(default, group.Entries[1].Bugs);
    }

    [Fact]
    public void Search_PlainSubstringIgnoringCase()
    {
        var mods = new[] { Mod("IV", descriptorName: "The Idea (Variation)"), Mod("Other") };

        Assert.Equal(new[] { "IV" }, Names(Build(Options(search: "idea (v"), null, null, mods)));
        Assert.Equal(new[] { "IV" }, Names(Build(Options(search: "IDEA"), null, null, mods)));
        Assert.Equal(new[] { "IV" }, Names(Build(Options(search: "iv"), null, null, mods)));
    }

    [Fact]
    public void Build_OnlyWarnings_UsesAlerts()
    {
        var warned = Mod("Warned");
        var alerts = new[] { new OverviewAlert(OverviewAlertKind.MissingDescriptor, "eu4", "Warned", "x") };

        var group = Assert.Single(Assert.Single(Build(Options(onlyWarnings: true), alerts, null, warned, Mod("Fine"))).Groups);

        Assert.Equal("Warned", group.Entries[0].Mod.Mod.Name);
        Assert.Single(group.Entries[0].Alerts);
    }
}
