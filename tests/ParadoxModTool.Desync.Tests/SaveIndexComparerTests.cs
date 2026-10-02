using System.Text;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class SaveIndexComparerTests
{
    private const string DatabaseProfile = """{ "objects": [ "*/database/{id}", "database/{id}" ], "nodeDepth": 4, "sizeCapBytes": 100 }""";

    private static DesyncProfile Profile() => DesyncProfile.Parse(DatabaseProfile);

    private static SaveIndex Index(string text) =>
        SaveIndexer.Read(new MemoryStream(Encoding.UTF8.GetBytes(text)), Profile(), null, CancellationToken.None);

    private static IReadOnlyList<UnitDiff> Compare(string reference, string desynced) =>
        SaveIndexComparer.Compare(Index(reference), Index(desynced), Profile());

    [Fact]
    public void Equal_saves_give_no_differences()
    {
        const string text = """
            random_seed=1
            countries={ database={ 1384={ gold=5 } 1385={ gold=6 } } }
            war_manager={ x=1 }
            """;

        Assert.Empty(Compare(text, text));
    }

    [Fact]
    public void A_changed_unit_is_one_changed_entry_not_its_ancestors()
    {
        const string template = """
            random_seed=1
            countries={ database={ 1384={ gold=VALUE } 1385={ gold=6 } } }
            war_manager={ x=1 }
            """;

        var diffs = Compare(template.Replace("VALUE", "5"), template.Replace("VALUE", "7"));

        var diff = Assert.Single(diffs);
        Assert.Equal("countries/database/1384", diff.Path);
        Assert.Equal(UnitChange.Changed, diff.Change);
        Assert.NotNull(diff.Reference);
        Assert.NotNull(diff.Desynced);
        Assert.Equal(diff.Reference!.Slot, diff.Desynced!.Slot);
    }

    [Fact]
    public void A_database_with_one_slot_reuse_gives_one_entry_and_no_own_scalar_change_for_the_container()
    {
        var diffs = Compare(
            "subunit_manager={ database={ 637537109={ a=1 } 5={ b=1 } } }",
            "subunit_manager={ database={ 671091541={ a=1 } 5={ b=1 } } }");

        var diff = Assert.Single(diffs);
        Assert.Equal(UnitChange.SlotReused, diff.Change);
    }

    [Fact]
    public void None_against_a_block_in_an_object_container_is_one_changed_unit()
    {
        var diffs = Compare(
            "construction_manager={ database={ 3003121664=none 1={ x=1 } } }",
            "construction_manager={ database={ 3003121664={ type=CIVIL } 1={ x=1 } } }");

        var diff = Assert.Single(diffs);
        Assert.Equal("construction_manager/database/3003121664", diff.Path);
        Assert.Equal(UnitChange.Changed, diff.Change);
        Assert.Equal(NodeKind.ScalarUnit, diff.Reference!.Kind);
        Assert.Equal(NodeKind.Unit, diff.Desynced!.Kind);
    }

    [Fact]
    public void Same_slot_with_other_generation_is_slot_reuse_not_a_change()
    {
        var diffs = Compare(
            "subunit_manager={ database={ 637537109={ a=1 } } }",
            "subunit_manager={ database={ 671091541={ a=2 } } }");

        var diff = Assert.Single(diffs);
        Assert.Equal(UnitChange.SlotReused, diff.Change);
        Assert.Equal("subunit_manager/database/637537109", diff.Path);
        Assert.Equal(0xb55, diff.Reference!.Slot);
        Assert.Equal(0x26, diff.Reference.Generation);
        Assert.Equal(0xb55, diff.Desynced!.Slot);
        Assert.Equal(0x28, diff.Desynced.Generation);
    }

    [Fact]
    public void A_container_with_a_duplicate_slot_on_the_reference_side_is_paired_by_full_id()
    {
        // 637537109 und 671091541 teilen den Slot 0xb55; der Slot ist im Behälter nicht eindeutig.
        var diffs = Compare(
            "subunit_manager={ database={ 637537109={ a=1 } 671091541={ a=2 } 7={ b=1 } } }",
            "subunit_manager={ database={ 671091541={ a=2 } 7={ b=2 } } }");

        Assert.Equal(2, diffs.Count);
        var onlyReference = Assert.Single(diffs, diff => diff.Change == UnitChange.OnlyReference);
        Assert.Equal("subunit_manager/database/637537109", onlyReference.Path);
        var changed = Assert.Single(diffs, diff => diff.Change == UnitChange.Changed);
        Assert.Equal("subunit_manager/database/7", changed.Path);
    }

    [Fact]
    public void A_container_with_a_duplicate_slot_on_the_desynced_side_is_paired_by_full_id()
    {
        var diffs = Compare(
            "subunit_manager={ database={ 637537109={ a=1 } } }",
            "subunit_manager={ database={ 671091541={ a=2 } 637537109={ a=1 } } }");

        var diff = Assert.Single(diffs);
        Assert.Equal(UnitChange.OnlyDesynced, diff.Change);
        Assert.Equal("subunit_manager/database/671091541", diff.Path);
    }

    [Fact]
    public void Units_on_one_side_only_are_reported_per_side()
    {
        var diffs = Compare(
            "countries={ database={ 1={ a=1 } 2={ a=1 } } }",
            "countries={ database={ 2={ a=1 } 3={ a=1 } } }");

        Assert.Equal(2, diffs.Count);

        var onlyReference = Assert.Single(diffs, diff => diff.Change == UnitChange.OnlyReference);
        Assert.Equal("countries/database/1", onlyReference.Path);
        Assert.NotNull(onlyReference.Reference);
        Assert.Null(onlyReference.Desynced);

        var onlyDesynced = Assert.Single(diffs, diff => diff.Change == UnitChange.OnlyDesynced);
        Assert.Equal("countries/database/3", onlyDesynced.Path);
        Assert.Null(onlyDesynced.Reference);
        Assert.NotNull(onlyDesynced.Desynced);
    }

    [Fact]
    public void A_missing_section_is_reported_as_one_entry_for_the_section()
    {
        var diffs = Compare("a={ x=1 }\nb={ database={ 1={ y=1 } } }", "a={ x=1 }");

        var diff = Assert.Single(diffs);
        Assert.Equal(UnitChange.OnlyReference, diff.Change);
        Assert.Equal("b", diff.Path);
    }

    [Fact]
    public void A_unit_on_one_side_only_in_a_line_formatted_save_does_not_change_the_containers_own_scalars()
    {
        var diffs = Compare(
            "countries={\n\tdatabase={\n\t\t1={\n\t\t\ta=1\n\t\t}\n\t\t2={\n\t\t\ta=1\n\t\t}\n\t}\n}\nafter=1\n",
            "countries={\n\tdatabase={\n\t\t1={\n\t\t\ta=1\n\t\t}\n\t}\n}\nafter=1\n");

        var diff = Assert.Single(diffs);
        Assert.Equal(UnitChange.OnlyReference, diff.Change);
        Assert.Equal("countries/database/2", diff.Path);
    }

    [Fact]
    public void A_section_missing_on_one_side_in_a_line_formatted_save_does_not_change_the_roots_own_scalars()
    {
        var diffs = Compare(
            "random_seed=1\ncountries={\n\tdatabase={\n\t\t1={\n\t\t\ta=1\n\t\t}\n\t}\n}\nwar_manager={\n\tx=1\n}\n",
            "random_seed=1\ncountries={\n\tdatabase={\n\t\t1={\n\t\t\ta=1\n\t\t}\n\t}\n}\n");

        var diff = Assert.Single(diffs);
        Assert.Equal(UnitChange.OnlyReference, diff.Change);
        Assert.Equal("war_manager", diff.Path);
    }

    [Fact]
    public void A_changed_block_with_child_nodes_is_descended_and_its_own_scalars_reported_separately()
    {
        var diffs = Compare(
            "section={ own=1 child={ value=1 } other=2 }",
            "section={ own=3 child={ value=1 } other=2 }");

        var diff = Assert.Single(diffs);
        Assert.Equal("section", diff.Path);
        Assert.Equal(UnitChange.OwnScalarsChanged, diff.Change);
    }

    [Fact]
    public void A_changed_child_of_a_section_with_equal_own_scalars_reports_only_the_child()
    {
        var diffs = Compare(
            "section={ own=1 child={ value=1 } other=2 }",
            "section={ own=1 child={ value=9 } other=2 }");

        var diff = Assert.Single(diffs);
        Assert.Equal("section/child", diff.Path);
        Assert.Equal(UnitChange.Changed, diff.Change);
    }

    [Fact]
    public void A_changed_child_and_changed_own_scalars_are_both_reported()
    {
        var diffs = Compare(
            "section={ own=1 child={ value=1 } }",
            "section={ own=2 child={ value=9 } }");

        Assert.Equal(2, diffs.Count);
        Assert.Contains(diffs, diff => diff.Path == "section" && diff.Change == UnitChange.OwnScalarsChanged);
        Assert.Contains(diffs, diff => diff.Path == "section/child" && diff.Change == UnitChange.Changed);
    }

    [Fact]
    public void Changed_top_level_scalars_are_own_scalars_of_the_root()
    {
        var diffs = Compare("random_seed=1\na={ x=1 }", "random_seed=2\na={ x=1 }");

        var diff = Assert.Single(diffs);
        Assert.Equal(UnitChange.OwnScalarsChanged, diff.Change);
        Assert.Equal(string.Empty, diff.Path);
        Assert.Equal(0, diff.Reference!.Id);
        Assert.Equal(NodeKind.Root, diff.Desynced!.Kind);
    }

    [Fact]
    public void A_changed_leaf_block_is_changed_and_one_over_the_size_cap_is_too_large()
    {
        var small = Compare("leaf={ x=1 }", "leaf={ x=2 }");
        Assert.Equal(UnitChange.Changed, Assert.Single(small).Change);

        var filler = string.Join(' ', Enumerable.Range(0, 40).Select(i => "k" + i + "=1"));
        var large = Compare("leaf={ x=1 " + filler + " }", "leaf={ x=2 " + filler + " }");
        var diff = Assert.Single(large);
        Assert.Equal(UnitChange.TooLarge, diff.Change);
        Assert.Equal("leaf", diff.Path);
        Assert.True(diff.Reference!.End - diff.Reference.Start > 100);
    }

    [Fact]
    public void A_leaf_block_is_too_large_when_only_the_desynced_side_exceeds_the_cap()
    {
        var filler = string.Join(' ', Enumerable.Range(0, 40).Select(i => "k" + i + "=1"));

        var diffs = Compare("leaf={ x=1 }", "leaf={ x=2 " + filler + " }");

        Assert.Equal(UnitChange.TooLarge, Assert.Single(diffs).Change);
    }

    [Fact]
    public void A_unit_over_the_size_cap_is_still_changed()
    {
        var filler = string.Join(' ', Enumerable.Range(0, 40).Select(i => "k" + i + "=1"));

        var diffs = Compare(
            "countries={ database={ 1={ x=1 " + filler + " } } }",
            "countries={ database={ 1={ x=2 " + filler + " } } }");

        Assert.Equal(UnitChange.Changed, Assert.Single(diffs).Change);
    }

    [Fact]
    public void Repeated_top_level_keys_are_paired_by_occurrence()
    {
        var diffs = Compare(
            "played_country={ name=a }\nplayed_country={ name=b }\nplayed_country={ name=c }",
            "played_country={ name=a }\nplayed_country={ name=X }\nplayed_country={ name=c }");

        var diff = Assert.Single(diffs);
        Assert.Equal("played_country#2", diff.Path);
        Assert.Equal(UnitChange.Changed, diff.Change);
    }

    [Fact]
    public void Differences_are_reported_in_file_order_with_one_sided_desynced_entries_last()
    {
        var diffs = Compare(
            "a={ x=1 }\nb={ x=1 }\nc={ x=1 }",
            "a={ x=2 }\nc={ x=2 }\nd={ x=1 }");

        Assert.Equal(
            new[] { "a", "b", "c", "d" },
            diffs.Select(diff => diff.Path));
        Assert.Equal(
            new[] { UnitChange.Changed, UnitChange.OnlyReference, UnitChange.Changed, UnitChange.OnlyDesynced },
            diffs.Select(diff => diff.Change));
    }

    [Fact]
    public void A_large_pair_of_mostly_equal_databases_gives_only_the_one_difference()
    {
        var reference = new StringBuilder("countries={ database={\n");
        var desynced = new StringBuilder("countries={ database={\n");
        for (var i = 0; i < 100_000; i++)
        {
            reference.Append(i).Append("={ gold=").Append(i).Append(" }\n");
            desynced.Append(i).Append("={ gold=").Append(i == 777 ? -1 : i).Append(" }\n");
        }

        reference.Append("} }");
        desynced.Append("} }");

        var diffs = Compare(reference.ToString(), desynced.ToString());

        var diff = Assert.Single(diffs);
        Assert.Equal("countries/database/777", diff.Path);
    }
}
