using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class SiblingMatcherTests
{
    private const string TwoIfs = "if = { limit = { x = 1 } add = 1 }\nif = { limit = { x = 2 } add = 2 }\n";

    private static List<EntryUnit> Units(string text) => EntryUnits.Build(ScriptDocument.Parse(text).Entries);

    private static AlignedUnits Align(string reference, string other)
    {
        var r = Units(reference);
        var o = Units(other);
        return SiblingMatcher.Align(r, o, SiblingMatcher.RepeatedBaseKeys(r, o));
    }

    [Fact]
    public void Single_keys_keep_their_key()
    {
        Assert.Equal("S:cost", Assert.Single(Align("cost = 1\n", "cost = 2\n").Units).Key);
    }

    [Fact]
    public void Inserted_sibling_in_front_does_not_shift_later_matches()
    {
        var units = Align(TwoIfs, "if = { limit = { x = 0 } add = 0 }\n" + TwoIfs).Units;
        Assert.StartsWith("S:if+", units[0].Key);
        Assert.Equal("S:if", units[1].Key);
        Assert.Equal("S:if#1", units[2].Key);
    }

    [Fact]
    public void Changed_sibling_matches_by_similarity()
    {
        var units = Align(TwoIfs, "if = { limit = { x = 1 } add = 1 }\nif = { limit = { x = 2 } add = 20 }\n").Units;
        Assert.Equal(new[] { "S:if", "S:if#1" }, units.Select(u => u.Key));
    }

    [Fact]
    public void Removed_first_sibling_keeps_identity_of_second()
    {
        var units = Align("modifier = { a = 1 }\nmodifier = { b = 1 }\n", "modifier = { b = 5 }\n").Units;
        Assert.Equal("S:modifier#1", Assert.Single(units).Key);
    }

    private const string Definitions =
        "modifier_definition = { type = privileges key = max_terms trigger = { always = yes } }\n"
        + "modifier_definition = { type = privileges key = num_of_pronoiars trigger = { always = yes } }\n";

    [Fact]
    public void Siblings_with_different_identity_are_never_paired_even_if_similar()
    {
        var units = Align(Definitions, "modifier_definition = { type = privileges key = iv_state_upkeep trigger = { always = yes } }\n").Units;

        var unit = Assert.Single(units);
        Assert.NotEqual("S:modifier_definition", unit.Key);
        Assert.NotEqual("S:modifier_definition#1", unit.Key);
    }

    [Fact]
    public void Siblings_with_the_same_identity_are_paired_even_if_moved_and_changed()
    {
        var units = Align(Definitions, "modifier_definition = { type = loyalty key = num_of_pronoiars trigger = { always = no } }\n").Units;

        Assert.Equal("S:modifier_definition#1", Assert.Single(units).Key);
    }

    [Fact]
    public void New_siblings_with_the_same_identity_get_the_same_key_on_both_sides()
    {
        var added = "modifier_definition = { type = privileges key = iv_state_upkeep trigger = { always = yes } }\n";
        var changed = "modifier_definition = { type = loyalty key = iv_state_upkeep trigger = { always = no } }\n";

        var a = Assert.Single(Align(Definitions, added).Units);
        var b = Assert.Single(Align(Definitions, changed).Units);

        Assert.Equal(a.Key, b.Key);
        Assert.Contains("iv_state_upkeep", a.Display);
    }

    [Fact]
    public void Equally_similar_candidates_are_ambiguous()
    {
        var aligned = Align(
            "if = { limit = { x = 1 } add = 1 }\nif = { limit = { x = 1 } add = 2 }\n",
            "if = { limit = { x = 1 } add = 3 }\n");
        Assert.Contains(Assert.Single(aligned.Units).Key, aligned.AmbiguousKeys);
    }

    [Fact]
    public void Fresh_keys_are_content_based_and_stable()
    {
        var withX = "if = { limit = { x = 9 } add = 9 }\n" + TwoIfs;
        var withYAndX = "if = { limit = { x = 8 } add = 8 }\n" + withX;
        var first = Align(TwoIfs, withX).Units[0].Key;
        Assert.Equal(first, Align(TwoIfs, withX).Units[0].Key);
        Assert.Equal(first, Align(TwoIfs, withYAndX).Units[1].Key);
        Assert.NotEqual(first, Align(TwoIfs, withYAndX).Units[0].Key);
    }

    [Fact]
    public void Large_groups_with_few_changes_are_fast()
    {
        const int N = 3000;
        var reference = string.Concat(Enumerable.Range(0, N).Select(i => $"modifier = {{ v = {i} }}\n"));
        var other = reference.Replace("modifier = { v = 1500 }", "modifier = { v = 99999 }");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var units = Align(reference, other).Units;

        watch.Stop();
        Assert.Equal("S:modifier#1500", units[1500].Key);
        Assert.Equal("S:modifier#2999", units[2999].Key);
        Assert.True(watch.ElapsedMilliseconds < 2000, $"{watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Similarity_weights_child_keys_and_values()
    {
        var a = ScriptDocument.Parse("x = { b = 1 }\n").Entries[0];
        var b = ScriptDocument.Parse("x = { b = 5 }\n").Entries[0];
        var c = ScriptDocument.Parse("x = { a = 1 }\n").Entries[0];
        Assert.Equal(0.5, SiblingMatcher.Similarity(((ScriptStatement)a).Value, ((ScriptStatement)b).Value), 3);
        Assert.Equal(0.0, SiblingMatcher.Similarity(((ScriptStatement)a).Value, ((ScriptStatement)c).Value), 3);
    }
}
