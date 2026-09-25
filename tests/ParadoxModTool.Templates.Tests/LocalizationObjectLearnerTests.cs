using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class LocalizationObjectLearnerTests
{
    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> Names(string folder, IEnumerable<string> names) =>
        new Dictionary<string, IReadOnlyCollection<string>> { [folder] = names.ToList() };

    private static List<string> Ids(string stem, int count) => Enumerable.Range(1, count).Select(i => $"{stem}_{i:00}").ToList();

    [Fact]
    public void Suffixes_get_a_share_and_only_common_ones_are_preselected()
    {
        var names = Ids("build", 10);
        var keys = names.Concat(names.Select(n => n + "_desc")).Concat(names.Take(5).Select(n => n + "_maintenance")).ToList();

        var result = LocalizationObjectLearner.Learn(Names("common/buildings", names), keys);

        var learning = result["common/buildings"];
        Assert.Equal(10, learning.ObjectCount);
        Assert.Equal(new[] { "", "_desc", "_maintenance" }, learning.Patterns.Select(p => p.Suffix));
        Assert.Equal(new[] { 1.0, 1.0, 0.5 }, learning.Patterns.Select(p => p.Share));
        Assert.Equal(new[] { true, true, false }, learning.Patterns.Select(p => p.Preselected));
        Assert.All(learning.Patterns, p => Assert.Equal(string.Empty, p.Prefix));
    }

    [Fact]
    public void A_prefix_is_learned_together_with_its_suffix()
    {
        var names = Ids("realm", 6);
        var keys = names.Select(n => "hint_" + n).Concat(names.Select(n => "hint_" + n + "_hint_text")).ToList();

        var learning = LocalizationObjectLearner.Learn(Names("common/situations", names), keys)["common/situations"];

        Assert.Equal(new[] { ("hint_", ""), ("hint_", "_hint_text") }, learning.Patterns.Select(p => (p.Prefix, p.Suffix)));
        Assert.All(learning.Patterns, p => Assert.True(p.Preselected));
    }

    [Fact]
    public void Objects_without_any_localization_are_not_counted_in_the_denominator()
    {
        var names = Ids("thing", 10);
        var keys = names.Take(6).ToList();

        var learning = LocalizationObjectLearner.Learn(Names("common/things", names), keys)["common/things"];

        Assert.Equal(6, learning.ObjectCount);
        Assert.Equal(1.0, Assert.Single(learning.Patterns).Share);
    }

    [Fact]
    public void A_folder_with_fewer_than_five_objects_with_localization_gets_no_profile()
    {
        var names = Ids("few", 4);

        var result = LocalizationObjectLearner.Learn(Names("common/few", names), names);

        Assert.Empty(result);
    }

    [Fact]
    public void Names_with_up_to_three_characters_are_ignored()
    {
        var names = Ids("long", 5).Append("abc").ToList();

        var learning = LocalizationObjectLearner.Learn(Names("common/mixed", names), names)["common/mixed"];

        Assert.Equal(5, learning.ObjectCount);
    }

    [Fact]
    public void A_suffix_seen_at_fewer_than_five_objects_is_dropped()
    {
        var names = Ids("item", 10);
        var keys = names.Concat(names.Take(4).Select(n => n + "_rare")).ToList();

        var learning = LocalizationObjectLearner.Learn(Names("common/items", names), keys)["common/items"];

        Assert.DoesNotContain(learning.Patterns, p => p.Suffix == "_rare");
    }

    [Fact]
    public void An_affix_longer_than_thirty_characters_is_not_a_pattern()
    {
        var names = Ids("gear", 6);
        var keys = names.Concat(names.Select(n => new string('x', 31) + n)).ToList();

        var learning = LocalizationObjectLearner.Learn(Names("common/gear", names), keys)["common/gear"];

        var pattern = Assert.Single(learning.Patterns);
        Assert.Equal(string.Empty, pattern.Prefix);
        Assert.Equal(string.Empty, pattern.Suffix);
    }

    [Fact]
    public void The_same_names_in_two_folders_are_credited_to_both()
    {
        var names = Ids("same", 5);
        var namesByFolder = new Dictionary<string, IReadOnlyCollection<string>> { ["common/a"] = names, ["common/b"] = names };

        var result = LocalizationObjectLearner.Learn(namesByFolder, names);

        Assert.Contains("common/a", result.Keys);
        Assert.Contains("common/b", result.Keys);
    }

    [Fact]
    public void A_key_that_merely_contains_a_name_in_the_middle_only_counts_within_the_affix_limit()
    {
        var names = Ids("iron", 5);
        var keys = names.Concat(names.Select(n => "the_big_" + n + "_works_of_the_north")).ToList();

        var learning = LocalizationObjectLearner.Learn(Names("common/metals", names), keys)["common/metals"];

        Assert.Contains(learning.Patterns, p => p.Prefix == "the_big_" && p.Suffix == "_works_of_the_north");
    }

    [Fact]
    public void Cancelling_stops_the_search()
    {
        var names = Ids("stop", 5);
        var keys = Enumerable.Range(0, 20000).Select(i => "k" + i);
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => LocalizationObjectLearner.Learn(Names("common/stop", names), keys, source.Token));
    }
}
