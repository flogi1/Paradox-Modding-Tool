using ParadoxModTool.Scopes;
using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class LocalizationScanTests
{
    private static LocalizationFieldTally Tally(string folder, string text, IReadOnlySet<string> keys)
    {
        var tally = new LocalizationFieldTally();
        LocalizationScan.CountFields(folder, ScopeLexer.Tokenize(text), keys, tally);
        return tally;
    }

    private static string Lines(string field, string valuePrefix, int count, bool quoted = true) =>
        string.Join("\n", Enumerable.Range(1, count).Select(i => quoted ? $"{field} = \"{valuePrefix}{i}\"" : $"{field} = {valuePrefix}{i}"));

    private static HashSet<string> Keys(string prefix, int count) => Enumerable.Range(1, count).Select(i => prefix + i).ToHashSet();

    private static readonly IReadOnlySet<string> NoObjects = new HashSet<string>();

    [Fact]
    public void Top_level_names_are_the_keys_of_blocks_on_the_first_level()
    {
        var tokens = ScopeLexer.Tokenize("a_one = { b = { c_two = { } } }\nd_three = 5\ne_four = { }\n\"quoted_five\" = { }");

        Assert.Equal(new[] { "a_one", "e_four", "quoted_five" }, LocalizationScan.TopLevelNames(tokens));
    }

    [Fact]
    public void A_field_with_mostly_localization_values_is_preselected()
    {
        var text = Lines("text", "key_", 20) + "\n" + Lines("text", "free ", 2);

        var field = Assert.Single(Tally("gui", text, Keys("key_", 20)).Select("gui", NoObjects));

        Assert.Equal("text", field.Name);
        Assert.Equal(20, field.Hits);
        Assert.Equal(0.909, field.Share);
        Assert.True(field.Preselected);
    }

    [Fact]
    public void A_field_between_25_and_80_percent_is_only_an_offer()
    {
        var text = Lines("text", "key_", 20) + "\n" + Lines("text", "free ", 40);

        var field = Assert.Single(Tally("gui", text, Keys("key_", 20)).Select("gui", NoObjects));

        Assert.Equal(0.333, field.Share);
        Assert.False(field.Preselected);
    }

    [Fact]
    public void A_field_below_25_percent_or_below_15_hits_is_dropped()
    {
        var lowShare = Lines("text", "key_", 15) + "\n" + Lines("text", "free ", 60);
        var fewHits = Lines("title", "key_", 14);

        Assert.Empty(Tally("gui", lowShare, Keys("key_", 15)).Select("gui", NoObjects));
        Assert.Empty(Tally("gui", fewHits, Keys("key_", 14)).Select("gui", NoObjects));
    }

    [Fact]
    public void Values_that_are_object_names_are_references_and_do_not_count()
    {
        var text = Lines("religion", "faith_", 20, quoted: false);
        var names = Keys("faith_", 20);

        Assert.Empty(Tally("common/x", text, names).Select("common/x", names));
    }

    [Fact]
    public void Field_names_are_grouped_ignoring_case()
    {
        var text = Lines("Text", "key_", 8) + "\n" + string.Join("\n", Enumerable.Range(9, 8).Select(i => $"text = key_{i}"));

        var field = Assert.Single(Tally("gui", text, Keys("key_", 16)).Select("gui", NoObjects));

        Assert.Equal("text", field.Name);
        Assert.Equal(16, field.Hits);
    }

    [Fact]
    public void Folders_are_counted_separately()
    {
        var tally = Tally("gui", Lines("text", "key_", 20), Keys("key_", 20));
        LocalizationScan.CountFields("events", ScopeLexer.Tokenize(Lines("name", "key_", 20)), Keys("key_", 20), tally);

        Assert.Equal(new[] { "text" }, tally.Select("gui", NoObjects).Select(f => f.Name));
        Assert.Equal(new[] { "name" }, tally.Select("events", NoObjects).Select(f => f.Name));
        Assert.Equal(new[] { "events", "gui" }, tally.Folders.OrderBy(f => f, StringComparer.Ordinal));
    }

    [Fact]
    public void Only_assignments_with_a_word_or_string_value_count()
    {
        var compared = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"text > key_{i}"));
        var blocks = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"text = {{ key_{i} }}"));

        Assert.Empty(Tally("gui", compared + "\n" + blocks, Keys("key_", 20)).Select("gui", NoObjects));
    }
}
