using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class MemoryMergerTests
{
    private const string Base = "a = {\n\tcost = 100\n\tx = 1\n}";

    [Fact]
    public void New_unit_is_inserted()
    {
        var outcome = MemoryMerger.Decide("a", Base, null, null, false);
        Assert.Equal(UnitOutcomeKind.Inserted, outcome.Kind);
        Assert.Equal(Base, outcome.Text);
    }

    [Fact]
    public void Unit_deleted_by_hand_is_not_inserted_again()
    {
        var outcome = MemoryMerger.Decide("a", Base, null, Base, false);
        Assert.Equal(UnitOutcomeKind.DeletedByHand, outcome.Kind);
        Assert.Null(outcome.Text);
    }

    [Fact]
    public void Unknown_unit_in_file_is_adopted_and_kept()
    {
        var mine = Base.Replace("100", "1000");
        var outcome = MemoryMerger.Decide("a", Base, mine, null, false);
        Assert.Equal(UnitOutcomeKind.Adopted, outcome.Kind);
        Assert.Equal(mine, outcome.Text);
    }

    [Fact]
    public void Untouched_unit_is_replaced_by_fresh()
    {
        var fresh = Base.Replace("x = 1", "x = 2");
        var outcome = MemoryMerger.Decide("a", fresh, Base, Base, false);
        Assert.Equal(UnitOutcomeKind.Replaced, outcome.Kind);
        Assert.Equal(fresh, outcome.Text);
    }

    [Fact]
    public void Untouched_unit_with_same_fresh_is_unchanged()
    {
        Assert.Equal(UnitOutcomeKind.Unchanged, MemoryMerger.Decide("a", Base, Base.Replace("\n", "\r\n") + "  ", Base, false).Kind);
    }

    [Fact]
    public void Hand_edit_without_vanilla_change_is_unchanged()
    {
        var mine = Base.Replace("100", "1000");
        var outcome = MemoryMerger.Decide("a", Base, mine, Base, false);
        Assert.Equal(UnitOutcomeKind.Unchanged, outcome.Kind);
        Assert.Equal(mine, outcome.Text);
    }

    [Fact]
    public void Hand_edited_modifier_survives_a_vanilla_change_elsewhere()
    {
        var outcome = MemoryMerger.Decide("a", Base.Replace("x = 1", "x = 2"), Base.Replace("100", "1000"), Base, false);

        Assert.Equal(UnitOutcomeKind.Merged, outcome.Kind);
        Assert.Contains("cost = 1000", outcome.Text);
        Assert.Contains("x = 2", outcome.Text);
        Assert.Empty(outcome.Conflicts);
    }

    [Fact]
    public void Both_changed_same_value_is_a_conflict()
    {
        var outcome = MemoryMerger.Decide("a", Base.Replace("100", "200"), Base.Replace("100", "1000"), Base, false);

        Assert.Equal(UnitOutcomeKind.Conflict, outcome.Kind);
        Assert.StartsWith("Baustein „a“: ", Assert.Single(outcome.Conflicts));
        Assert.NotNull(outcome.Text);
    }

    [Fact]
    public void Unparsable_mine_is_a_conflict_and_keeps_mine()
    {
        var mine = "a = {\n\tcost = 1000\n";
        var outcome = MemoryMerger.Decide("a", Base.Replace("x = 1", "x = 2"), mine, Base, false);

        Assert.Equal(UnitOutcomeKind.Conflict, outcome.Kind);
        Assert.Equal(mine, outcome.Text);
        Assert.Equal("Baustein „a“ ist kein gültiges Skript, bitte von Hand prüfen", Assert.Single(outcome.Conflicts));
    }

    [Fact]
    public void Removed_row_with_untouched_unit_is_removed()
    {
        var outcome = MemoryMerger.Decide("a", null, Base, Base, false);
        Assert.Equal(UnitOutcomeKind.Removed, outcome.Kind);
        Assert.Null(outcome.Text);
    }

    [Fact]
    public void Removed_row_with_edited_unit_is_kept()
    {
        var mine = Base.Replace("100", "1000");
        var outcome = MemoryMerger.Decide("a", null, mine, Base, false);
        Assert.Equal(UnitOutcomeKind.KeptEditedRemoved, outcome.Kind);
        Assert.Equal(mine, outcome.Text);
    }

    [Fact]
    public void Foreign_unit_without_row_and_memory_is_unchanged()
    {
        var outcome = MemoryMerger.Decide("a", null, Base, null, false);
        Assert.Equal(UnitOutcomeKind.Unchanged, outcome.Kind);
        Assert.Equal(Base, outcome.Text);
    }

    [Fact]
    public void Localization_merges_per_key()
    {
        const string remembered = " building_a:0 \"A\"\n building_a_desc:0 \"Alt\"";
        const string mine = " building_a:0 \"A von Hand\"\n building_a_desc:0 \"Alt\"";
        const string fresh = " building_a:0 \"A\"\n building_a_desc:0 \"Neu\"\n building_b:0 \"B\"";

        var outcome = MemoryMerger.Decide("a", fresh, mine, remembered, true);

        Assert.Equal(UnitOutcomeKind.Merged, outcome.Kind);
        Assert.Equal(" building_a:0 \"A von Hand\"\n building_a_desc:0 \"Neu\"\n building_b:0 \"B\"", outcome.Text);
    }

    [Fact]
    public void Localization_key_changed_on_both_sides_is_a_conflict_keeping_fresh()
    {
        var outcome = MemoryMerger.Decide("a", " k:0 \"Neu\"", " k:0 \"Hand\"", " k:0 \"Alt\"", true);

        Assert.Equal(UnitOutcomeKind.Conflict, outcome.Kind);
        Assert.Equal(" k:0 \"Neu\"", outcome.Text);
        Assert.Equal("Schlüssel „k“ wurde von Hand und im neu Erzeugten verschieden geändert", Assert.Single(outcome.Conflicts));
    }
}
