using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class DesyncRankingTests
{
    private const string ProfileJson = """
        {
          "noise": [ "metadata/**", "**/*_cache/**" ],
          "consequence": [ "trade_path_manager/**/counters/**", "**/priority" ],
          "floatDrift": 0.001,
          "names": { "countries/database/{id}": { "tagList": "countries/tags" } }
        }
        """;

    private static readonly DesyncProfile Profile = DesyncProfile.Parse(ProfileJson);

    private static SaveNameResolver Names() =>
        new(new SaveNameTables(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["countries/database/{id}"] = new Dictionary<string, string> { ["1384"] = "BLG" },
        }), Profile);

    private static FieldDiff Field(string path, string? reference, string? desynced) =>
        new(path, reference, desynced, FieldChangeKind.Changed);

    private static UnitDiffDetail Unit(string path, UnitChange change = UnitChange.Changed, params FieldDiff[] fields) =>
        new(path, change, null, null, null, null, 0, 0, fields, false, false);

    private static RankedDiff RankOne(UnitDiffDetail unit) => Assert.Single(DesyncRanking.Rank(new[] { unit }, Profile, Names()));

    private static DiffWeight WeightOfField(string unitPath, string fieldPath, string? reference, string? desynced) =>
        Assert.Single(RankOne(Unit(unitPath, UnitChange.Changed, Field(fieldPath, reference, desynced))).Fields).Weight;

    [Fact]
    public void A_float_change_within_the_drift_threshold_is_a_consequence()
    {
        // 2.82658 gegen 2.82446: relativ 7.5e-4, höchstens 0.001.
        Assert.Equal(DiffWeight.Consequence, WeightOfField("countries/database/1384", "price", "2.82658", "2.82446"));
    }

    [Fact]
    public void A_float_change_above_the_threshold_is_a_cause()
    {
        Assert.Equal(DiffWeight.Cause, WeightOfField("countries/database/1384", "price", "0.47488", "0.2453"));
    }

    [Fact]
    public void Integer_and_identifier_changes_are_causes()
    {
        Assert.Equal(DiffWeight.Cause, WeightOfField("countries/database/1384", "gold", "100000", "100001"));
        Assert.Equal(DiffWeight.Cause, WeightOfField("countries/database/1384", "government", "monarchy", "republic"));
        Assert.Equal(DiffWeight.Cause, WeightOfField("countries/database/1384", "stance", "1.5", "tall"));
        Assert.Equal(DiffWeight.Cause, WeightOfField("countries/database/1384", "x", null, "1.5"));
    }

    [Fact]
    public void Paths_matching_consequence_patterns_are_consequences_even_for_integers()
    {
        Assert.Equal(DiffWeight.Consequence, WeightOfField("trade_path_manager/database/1", "counters/x", "1", "2"));
        Assert.Equal(DiffWeight.Consequence, WeightOfField("countries/database/1384", "ai/priority", "1", "2"));
    }

    [Fact]
    public void Noise_patterns_win_over_everything()
    {
        Assert.Equal(DiffWeight.Noise, WeightOfField("metadata", "version", "1", "2"));

        // Der Pfad passt auf Rauschen und Folge: Rauschen gewinnt.
        var both = DesyncProfile.Parse("""{ "noise": [ "a/**" ], "consequence": [ "a/**" ] }""");
        var ranked = DesyncRanking.Rank(new[] { Unit("a/b", UnitChange.Changed, Field("x", "1", "2")) }, both, Names());
        Assert.Equal(DiffWeight.Noise, Assert.Single(ranked).Weight);
    }

    [Fact]
    public void Slot_reuse_and_one_sided_units_are_causes_unless_their_path_is_consequence_or_noise()
    {
        Assert.Equal(DiffWeight.Cause, RankOne(Unit("countries/database/1384", UnitChange.SlotReused)).Weight);
        Assert.Equal(DiffWeight.Cause, RankOne(Unit("countries/database/1384", UnitChange.OnlyReference)).Weight);
        Assert.Equal(DiffWeight.Cause, RankOne(Unit("countries/database/1384", UnitChange.OnlyDesynced)).Weight);
        Assert.Equal(DiffWeight.Cause, RankOne(Unit("countries/database/1384", UnitChange.TooLarge)).Weight);
        Assert.Equal(DiffWeight.Consequence, RankOne(Unit("trade_path_manager/database/1/counters/x", UnitChange.OnlyDesynced)).Weight);
        Assert.Equal(DiffWeight.Noise, RankOne(Unit("metadata/x", UnitChange.SlotReused)).Weight);
    }

    [Fact]
    public void A_unit_takes_the_strongest_weight_of_its_fields()
    {
        var unit = Unit(
            "countries/database/1384",
            UnitChange.Changed,
            Field("proximity_cache/a", "1", "2"),
            Field("ai/priority", "1", "2"),
            Field("gold", "1", "2"),
            Field("price", "2.82658", "2.82446"));

        var ranked = RankOne(unit);

        Assert.Equal(DiffWeight.Cause, ranked.Weight);
        Assert.Equal(
            new[] { DiffWeight.Noise, DiffWeight.Consequence, DiffWeight.Cause, DiffWeight.Consequence },
            ranked.Fields.Select(field => field.Weight));

        var withoutCause = Unit("countries/database/1384", UnitChange.OwnScalarsChanged, Field("ai/priority", "1", "2"), Field("proximity_cache/a", "1", "2"));
        Assert.Equal(DiffWeight.Consequence, RankOne(withoutCause).Weight);
    }

    [Fact]
    public void A_field_with_an_empty_path_is_matched_against_the_unit_path_itself()
    {
        Assert.Equal(DiffWeight.Noise, WeightOfField("metadata", "", "a", "b"));
        Assert.Equal(DiffWeight.Cause, WeightOfField("", "random_seed", "1", "2"));
    }

    [Fact]
    public void Ranked_diffs_carry_display_path_and_manager_and_sort_by_weight_manager_and_path()
    {
        var units = new[]
        {
            Unit("metadata", UnitChange.Changed, Field("v", "1", "2")),
            Unit("war_manager/database/3", UnitChange.Changed, Field("x", "1", "2")),
            Unit("countries/database/1384", UnitChange.Changed, Field("gold", "1", "2")),
            Unit("trade_path_manager/database/1", UnitChange.Changed, Field("counters/x", "1", "2")),
            Unit("", UnitChange.OwnScalarsChanged, Field("random_seed", "1", "2")),
        };

        var ranked = DesyncRanking.Rank(units, Profile, Names());

        Assert.Equal(new[] { "", "countries", "war_manager", "trade_path_manager", "metadata" }, ranked.Select(diff => diff.Manager));
        Assert.Equal(
            new[] { DiffWeight.Cause, DiffWeight.Cause, DiffWeight.Cause, DiffWeight.Consequence, DiffWeight.Noise },
            ranked.Select(diff => diff.Weight));
        Assert.Equal("countries › 1384 (BLG)", ranked[1].DisplayPath);
    }

    [Fact]
    public void Managers_with_causes_in_several_pairs_are_recurring()
    {
        RankedDiff Diff(string path, DiffWeight weight) =>
            new(Unit(path), path, path.Split('/')[0], weight, Array.Empty<RankedField>());

        var pairs = new IReadOnlyList<RankedDiff>[]
        {
            new[] { Diff("countries/database/1", DiffWeight.Cause), Diff("countries/database/2", DiffWeight.Cause), Diff("war_manager/x", DiffWeight.Cause) },
            new[] { Diff("countries/database/1", DiffWeight.Cause), Diff("war_manager/x", DiffWeight.Consequence), Diff("market_manager/x", DiffWeight.Cause) },
            new[] { Diff("market_manager/x", DiffWeight.Cause), Diff("countries/database/3", DiffWeight.Cause) },
        };

        var recurring = DesyncRanking.Recurring(pairs);

        Assert.Equal(new[] { ("countries", 3), ("market_manager", 2) }, recurring);
    }
}
