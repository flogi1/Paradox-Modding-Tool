using Xunit;

namespace ParadoxModTool.SaveGames.Tests;

public class SaveExplorerQueryTests
{
    private static string Country(string id, string body) => $"{id}={{ variables={{ {body} }} }}\n";

    private static string Vars(params (string Name, string Value)[] vars) =>
        "data={ " + string.Concat(vars.Select(v => $"{{ flag={v.Name} data={{ type=value identity={v.Value} }} }} ")) + "}";

    private static SaveSnapshot Countries(params string[] countries) =>
        SaveSnapshotTests.Load("countries={ tags={ 1=AAA 2=BBB } database={\n" + string.Concat(countries) + "} }\n");

    private static SaveView Compare(SaveSnapshot before, SaveSnapshot after) => new(after, before, SaveComparison.Compare(before, after));

    [Fact]
    public void NamesWithoutComparisonHaveNoChange()
    {
        var view = new SaveView(Countries(Country("1", Vars(("a", "1"), ("b", "1"))), Country("2", Vars(("a", "1")))), null, null);

        var names = SaveExplorerQuery.Names(view, SaveFilter.None);

        Assert.Equal(new[] { new SaveNameRow("a", SaveNameKind.Variable, 2, null), new SaveNameRow("b", SaveNameKind.Variable, 1, null) }, names);
        Assert.All(SaveExplorerQuery.ValuesForName(view, "a", SaveNameKind.Variable, false), row => Assert.Null(row.Before));
    }

    [Fact]
    public void SearchIsAPlainCaseInsensitiveSubstring()
    {
        var view = new SaveView(Countries(Country("1", Vars(("iv2_A.b", "1"), ("iv2_x*", "1"), ("other[1]", "1")))), null, null);

        string[] Find(string search) => SaveExplorerQuery.Names(view, new SaveFilter(search, null, false)).Select(n => n.Name).ToArray();

        Assert.Equal(new[] { "iv2_A.b" }, Find("a.B"));
        Assert.Equal(new[] { "iv2_x*" }, Find("X*"));
        Assert.Equal(new[] { "other[1]" }, Find("[1]"));
        Assert.Equal(new[] { "iv2_A.b", "iv2_x*" }, Find("IV2"));
        Assert.Equal(3, Find(string.Empty).Length);
    }

    [Fact]
    public void OwnNameFilterKeepsOnlyOwnNamesAndObjectsWithOwnNames()
    {
        var view = new SaveView(Countries(Country("1", Vars(("iv2_a", "1"), ("vanilla", "1"))), Country("2", Vars(("vanilla", "1")))), null, null);
        var filter = new SaveFilter(string.Empty, name => name.StartsWith("iv2_", StringComparison.Ordinal), false);

        Assert.Equal(new[] { "iv2_a" }, SaveExplorerQuery.Names(view, filter).Select(n => n.Name));
        Assert.Equal(new[] { "AAA" }, SaveExplorerQuery.Objects(view, filter).Select(o => o.Object.DisplayName));
        Assert.Equal(new[] { "iv2_a" }, SaveExplorerQuery.ValuesForObject(view, "countries/database/1", filter).Select(r => r.Name));
    }

    [Fact]
    public void ObjectSearchMatchesDisplayNameOrId()
    {
        var view = new SaveView(Countries(Country("1", Vars(("a", "1"))), Country("2", Vars(("a", "1")))), null, null);

        Assert.Equal(new[] { "BBB" }, SaveExplorerQuery.Objects(view, new SaveFilter("bb", null, false)).Select(o => o.Object.DisplayName));
        Assert.Equal(new[] { "AAA" }, SaveExplorerQuery.Objects(view, new SaveFilter("1", null, false)).Select(o => o.Object.DisplayName));
    }

    [Fact]
    public void CountryTagFilterKeepsObjectsOfThatCountryAndObjectsWithoutTag()
    {
        var snapshot = SaveSnapshotTests.Load(
            "countries={ tags={ 1=AAA 2=BBB } database={\n" + Country("1", Vars(("a", "1"))) + Country("2", Vars(("a", "1"))) + "} }\n" +
            "character_db={ database={ 5={ country=1 variables={ " + Vars(("c", "1")) + " } } 6={ country=2 variables={ " + Vars(("c", "1")) + " } } } }\n" +
            "provinces={ database={ 7={ variables={ " + Vars(("p", "1")) + " } } } }\n");
        var view = new SaveView(snapshot, null, null);

        var kept = SaveExplorerQuery.Objects(view, SaveFilter.None with { Tag = "aaa" }).Select(o => o.Object.OwnerPath);

        Assert.Equal(new[] { "countries/database/1", "character_db/database/5", "provinces/database/7" }, kept);
        Assert.Equal(new[] { "AAA", "BBB" }, SaveExplorerQuery.Tags(view));
        Assert.Equal(5, SaveExplorerQuery.Objects(view, SaveFilter.None).Count);
    }

    [Fact]
    public void ComparisonAddsRemovedNamesAndObjectsAndMarksChanges()
    {
        var before = Countries(Country("1", Vars(("a", "1"), ("gone", "1"))), Country("2", Vars(("a", "1"))));
        var after = Countries(Country("1", Vars(("a", "2"), ("new", "1"))));
        var view = Compare(before, after);

        var names = SaveExplorerQuery.Names(view, SaveFilter.None);
        Assert.Contains(new SaveNameRow("a", SaveNameKind.Variable, 1, SaveChange.Changed), names);
        Assert.Contains(new SaveNameRow("gone", SaveNameKind.Variable, 0, SaveChange.Removed), names);
        Assert.Contains(new SaveNameRow("new", SaveNameKind.Variable, 1, SaveChange.Added), names);

        var objects = SaveExplorerQuery.Objects(view, SaveFilter.None);
        Assert.Equal(SaveChange.Removed, objects.Single(o => o.Object.DisplayName == "BBB").Change);
        Assert.Equal(SaveChange.Changed, objects.Single(o => o.Object.DisplayName == "AAA").Change);

        var values = SaveExplorerQuery.ValuesForName(view, "a", SaveNameKind.Variable, false);
        Assert.Equal(
            new[] { ("AAA", "0.00002", "0.00001", SaveChange.Changed), ("BBB", string.Empty, "0.00001", SaveChange.Removed) },
            values.Select(r => (r.Object.DisplayName, r.Value, r.Before!, r.Change!.Value)));

        var objectValues = SaveExplorerQuery.ValuesForObject(view, "countries/database/1", SaveFilter.None);
        Assert.Equal(new[] { ("a", SaveChange.Changed), ("gone", SaveChange.Removed), ("new", SaveChange.Added) }, objectValues.Select(r => (r.Name, r.Change!.Value)));

        var onlyChanges = new SaveView(Countries(Country("1", Vars(("same", "1"), ("x", "1")))), null, null);
        var sameView = Compare(onlyChanges.Current, Countries(Country("1", Vars(("same", "1"), ("x", "2")))));
        Assert.Equal(new[] { "x" }, SaveExplorerQuery.Names(sameView, new SaveFilter(string.Empty, null, true)).Select(n => n.Name));
        Assert.Equal(new[] { "x" }, SaveExplorerQuery.ValuesForObject(sameView, "countries/database/1", new SaveFilter(string.Empty, null, true)).Select(r => r.Name));
        Assert.Equal("0.00001", SaveExplorerQuery.ValuesForName(sameView, "same", SaveNameKind.Variable, false).Single().Before);
        Assert.Empty(SaveExplorerQuery.ValuesForName(sameView, "same", SaveNameKind.Variable, true));
    }

    [Fact]
    public void FlagValueRowsFollowTheVariablesHoldingTheFlag()
    {
        const string flag = "data={ { flag=army data={ type=flag flag=\"iv2_army_12\" } } }";
        var before = Countries(Country("1", flag));
        var after = Countries(Country("1", flag), Country("2", flag));
        var view = Compare(before, after);

        Assert.Contains(new SaveNameRow("iv2_army_12", SaveNameKind.FlagValue, 2, SaveChange.Changed), SaveExplorerQuery.Names(view, SaveFilter.None));
        Assert.Equal(
            new[] { ("AAA", SaveChange.Unchanged), ("BBB", SaveChange.Added) },
            SaveExplorerQuery.ValuesForName(view, "iv2_army_12", SaveNameKind.FlagValue, false).Select(r => (r.Object.DisplayName, r.Change!.Value)));
    }

    [Fact]
    public void ListItemsShowAddedAndRemovedItems()
    {
        static string List(string id) => $"list={{ {{ name=\"L\" item={{ type=ctry identity={id} }} }} }}";

        var view = Compare(Countries(Country("1", List("1"))), Countries(Country("1", List("2"))));

        var rows = SaveExplorerQuery.ValuesForName(view, "L", SaveNameKind.List, false);
        Assert.Equal(2, rows.Count);
        var removed = rows.Single(r => r.Change == SaveChange.Removed);
        var added = rows.Single(r => r.Change == SaveChange.Added);
        Assert.Equal(("AAA", string.Empty), (removed.Before, removed.Value));
        Assert.Equal("BBB", added.Value);
    }
}
