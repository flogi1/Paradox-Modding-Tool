using Xunit;

namespace ParadoxModTool.SaveGames.Tests;

public class SaveComparisonTests
{
    private static string Country(string id, params (string Name, string Type, string Value)[] vars) =>
        $"{id}={{ variables={{ data={{ {string.Concat(vars.Select(v => $"{{ flag={v.Name} data={{ type={v.Type} identity={v.Value} }} }} "))}}} }} }}\n";

    private static SaveSnapshot Countries(params string[] countries) =>
        SaveSnapshotTests.Load("countries={ database={\n" + string.Concat(countries) + "} }\n");

    [Fact]
    public void VariablesAreAddedRemovedChangedOrUnchangedPerOwnerAndName()
    {
        var before = Countries(Country("3", ("a", "value", "1"), ("b", "value", "2"), ("c", "value", "3")));
        var after = Countries(Country("3", ("a", "value", "1"), ("b", "value", "5"), ("d", "value", "4")), Country("4", ("a", "value", "1")));

        var comparison = SaveComparison.Compare(before, after);

        Assert.Equal(SaveChange.Unchanged, comparison.Find("countries/database/3", "a", SaveEntryKind.Variable)!.Change);
        var changed = comparison.Find("countries/database/3", "b", SaveEntryKind.Variable)!;
        Assert.Equal((SaveChange.Changed, "2", "5"), (changed.Change, changed.Before!.RawValue, changed.After!.RawValue));
        Assert.Equal(SaveChange.Removed, comparison.Find("countries/database/3", "c", SaveEntryKind.Variable)!.Change);
        Assert.Equal(SaveChange.Added, comparison.Find("countries/database/3", "d", SaveEntryKind.Variable)!.Change);
        Assert.Equal(SaveChange.Added, comparison.Find("countries/database/4", "a", SaveEntryKind.Variable)!.Change);
        Assert.Null(comparison.Find("countries/database/9", "a", SaveEntryKind.Variable));
        Assert.Equal(5, comparison.Entries.Count);
    }

    [Fact]
    public void TypeChangeCountsAsChanged()
    {
        var comparison = SaveComparison.Compare(Countries(Country("3", ("a", "value", "1"))), Countries(Country("3", ("a", "boolean", "1"))));

        Assert.Equal(SaveChange.Changed, Assert.Single(comparison.Entries).Change);
    }

    [Fact]
    public void ListItemsCompareAsMultisets()
    {
        static string List(params string[] ids) =>
            "3={ variables={ list={ " + string.Concat(ids.Select(id => $"{{ name=\"L\" item={{ type=ctry identity={id} }} }} ")) + "} } }\n";

        var comparison = SaveComparison.Compare(Countries(List("1", "2", "2")), Countries(List("2", "7")));

        Assert.Equal(
            new[] { ("1", SaveChange.Removed), ("2", SaveChange.Unchanged), ("2", SaveChange.Removed), ("7", SaveChange.Added) },
            comparison.Entries.Select(e => ((e.After ?? e.Before)!.RawValue, e.Change)).OrderBy(p => p.RawValue).ThenBy(p => p.Change));
        Assert.All(comparison.Entries, e => Assert.Equal(SaveEntryKind.ListItem, e.Kind));
    }

    [Fact]
    public void NameAndObjectSummaries()
    {
        var before = Countries(Country("1", ("same", "value", "1"), ("gone", "value", "1"), ("mixed", "value", "1")), Country("2", ("mixed", "value", "1")));
        var after = Countries(Country("1", ("same", "value", "1"), ("new", "value", "1"), ("mixed", "value", "1")), Country("3", ("mixed", "value", "1")));

        var comparison = SaveComparison.Compare(before, after);

        Assert.Equal(SaveChange.Unchanged, comparison.ChangeOfName("same"));
        Assert.Equal(SaveChange.Added, comparison.ChangeOfName("new"));
        Assert.Equal(SaveChange.Removed, comparison.ChangeOfName("gone"));
        Assert.Equal(SaveChange.Changed, comparison.ChangeOfName("mixed"));
        Assert.Equal(SaveChange.Unchanged, comparison.ChangeOfName("unknown"));
        Assert.Equal(SaveChange.Changed, comparison.ChangeOfObject("countries/database/1"));
        Assert.Equal(SaveChange.Removed, comparison.ChangeOfObject("countries/database/2"));
        Assert.Equal(SaveChange.Added, comparison.ChangeOfObject("countries/database/3"));
        Assert.Equal(SaveChange.Unchanged, comparison.ChangeOfObject("countries/database/9"));
    }
}
