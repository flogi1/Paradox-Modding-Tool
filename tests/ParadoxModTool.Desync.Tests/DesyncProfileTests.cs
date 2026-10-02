using ParadoxModTool.ErrorLog;
using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class DesyncProfileTests
{
    private static string ProfilesDirectory => Path.Combine(AppContext.BaseDirectory, "profiles");

    private static DesyncProfile Shipped() =>
        DesyncProfile.Load(Path.Combine(ProfilesDirectory, "desync", "eu5.json"));

    [Fact]
    public void Path_patterns_match_one_segment_any_segments_ids_and_inner_wildcards()
    {
        Assert.True(PathPattern.Parse("*/database/{id}").Matches("countries/database/1384"));
        Assert.False(PathPattern.Parse("*/database/{id}").Matches("countries/database/tags"));
        Assert.True(PathPattern.Parse("market_manager/**/goods/**").Matches("market_manager/database/65/goods/wine/price"));
        Assert.True(PathPattern.Parse("**/*_cache/**").Matches("countries/database/764/proximity_persistent_cache/x"));
        Assert.True(PathPattern.Parse("metadata/**").Matches("metadata"));
        Assert.True(PathPattern.Parse("played_country/**").Matches("played_country#2/x"));
        Assert.True(PathPattern.Parse("market_manager/**/goods/**").Matches("market_manager/database/65/goods#2/price"));
    }

    [Fact]
    public void Path_patterns_require_whole_segments_and_the_exact_number_of_single_wildcards()
    {
        Assert.False(PathPattern.Parse("*/database/{id}").Matches("countries/database"));
        Assert.False(PathPattern.Parse("*/database/{id}").Matches("a/b/database/1"));
        Assert.False(PathPattern.Parse("*/database/{id}").Matches("countries/database/1/x"));
        Assert.False(PathPattern.Parse("*/database/{id}").Matches("countries/database/12a"));
        Assert.False(PathPattern.Parse("metadata/**").Matches("metadata_other"));
        Assert.False(PathPattern.Parse("**/*_cache").Matches("countries/database/1/cache_x"));
        Assert.True(PathPattern.Parse("**/*_cache").Matches("countries/database/1/proximity_cache"));
        Assert.True(PathPattern.Parse("**").Matches("any/path/at/all"));
        Assert.False(PathPattern.Parse("a/**/b").Matches("a/c"));
        Assert.True(PathPattern.Parse("a/**/b").Matches("a/b"));
    }

    [Fact]
    public void Anonymous_list_and_unit_segments_match_a_single_wildcard_only()
    {
        Assert.True(PathPattern.Parse("a/*/c").Matches("a/[3]/c"));
        Assert.True(PathPattern.Parse("a/*/c").Matches("a/sub_unit=7/c"));
        Assert.True(PathPattern.Parse("a/**/c").Matches("a/[3]/sub_unit=7/c"));
        Assert.False(PathPattern.Parse("a/{id}/c").Matches("a/[3]/c"));
        Assert.False(PathPattern.Parse("a/sub_unit/c").Matches("a/sub_unit=7/c"));
        Assert.False(PathPattern.Parse("a/*_unit=7/c").Matches("a/sub_unit=7/c"));
        Assert.False(PathPattern.Parse("a/sub_*/c").Matches("a/sub_unit=7/c"));
    }

    [Fact]
    public void The_shipped_eu5_profile_loads_with_random_count_explained_in_german_and_english()
    {
        var profile = Shipped();

        Assert.Equal(1, profile.Version);
        Assert.Equal(24, profile.IdSlotBits);
        Assert.Equal(262144, profile.SizeCapBytes);
        Assert.Equal(5, profile.NodeDepth);
        Assert.Equal(0.001, profile.FloatDrift);
        Assert.Equal(new[] { "name", "country_name", "subunit_name_2", "type" }, profile.LabelFields);

        var info = profile.Describe("RANDOM_COUNT");
        Assert.NotNull(info);
        Assert.Equal("Zufallszähler", LocalizedText.Pick(info.Name, "de"));
        Assert.Equal("Random count", LocalizedText.Pick(info.Name, "en"));
        Assert.StartsWith("Der Zufallsgenerator wurde", LocalizedText.Pick(info.Explain, "de"));
        Assert.Contains("random_count", LocalizedText.Pick(info.Explain, "de"));
        Assert.Contains("random", LocalizedText.Pick(info.Explain, "en"), StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(LocalizedText.Pick(info.Explain, "de"), LocalizedText.Pick(info.Explain, "en"));
        Assert.Equal(3, info.Causes.Count);
        Assert.StartsWith("Zufall in Skript", LocalizedText.Pick(info.Causes[0], "de"));
        Assert.Equal("Unterschiedliche Mod-Dateien", LocalizedText.Pick(info.Causes[1], "de"));
        Assert.Equal("Unterschiedliche Spielversionen", LocalizedText.Pick(info.Causes[2], "de"));
        Assert.All(info.Causes, cause => Assert.NotEqual(LocalizedText.Pick(cause, "de"), LocalizedText.Pick(cause, "en")));
    }

    [Fact]
    public void The_shipped_profile_has_rule_patterns_names_and_anonymous_ids()
    {
        var profile = Shipped();

        Assert.Contains(profile.Noise, p => p.Matches("metadata/compatibility/locations"));
        Assert.Contains(profile.Noise, p => p.Matches("countries/database/764/proximity_persistent_cache"));
        Assert.Contains(profile.Noise, p => p.Matches("countries/database/764/proximity_persistent_cache/x"));
        Assert.Contains(profile.Noise, p => p.Matches("played_country#2/x"));
        Assert.DoesNotContain(profile.Noise, p => p.Matches("countries/database/764/treasury"));
        Assert.Contains(profile.Consequence, p => p.Matches("market_manager/database/65/goods/wine/price"));
        Assert.Contains(profile.Consequence, p => p.Matches("countries/database/3/ai_memory/x"));
        Assert.Contains(profile.Consequence, p => p.Matches("countries/database/3/priority"));

        Assert.Equal("sub_unit", profile.AnonymousIdFor("disease_outbreak_manager/database/5/sub_units"));
        Assert.Equal("location", profile.AnonymousIdFor("disease_outbreak_manager/data/[0]/locations"));
        Assert.Null(profile.AnonymousIdFor("countries/database"));

        Assert.Equal("countries/tags", profile.Names["countries/database/{id}"].TagList);
        Assert.Null(profile.Names["countries/database/{id}"].KeyList);
        var locations = profile.Names["locations/locations/{id}"];
        Assert.Equal("metadata/compatibility/locations", locations.KeyList);
        Assert.Equal(1, locations.KeyListBase);
        Assert.Null(locations.TagList);
    }

    [Fact]
    public void The_shipped_profile_keeps_the_known_real_causes_out_of_noise_and_consequence()
    {
        var profile = Shipped();
        var causes = new[]
        {
            "countries/database/1384/government/parliament/parliament_issue_support",
            "construction_manager/database/3003121664",
            "subunit_manager/database/637537109",
        };

        foreach (var path in causes)
        {
            Assert.DoesNotContain(profile.Noise, p => p.Matches(path));
            Assert.DoesNotContain(profile.Consequence, p => p.Matches(path));
        }

        Assert.Contains(profile.Consequence, p => p.Matches("population/database/1/price"));
        Assert.Contains(profile.Consequence, p => p.Matches("trade_manager/database/16777217"));
        Assert.DoesNotContain(profile.Consequence, p => p.Matches("population/database/1"));
    }

    [Fact]
    public void The_shipped_profile_marks_database_and_location_containers_as_object_containers()
    {
        var p = Shipped();

        Assert.True(p.IsObjectContainer("countries/database"));
        Assert.True(p.IsObjectContainer("locations/locations"));
        Assert.False(p.IsObjectContainer("countries/tags"));
        Assert.False(p.IsObjectContainer("countries/database/12"));
    }

    [Fact]
    public void Slot_bits_can_be_overridden_per_container()
    {
        var profile = DesyncProfile.Parse("""
            {
              "version": 1,
              "idSlotBits": 24,
              "idSlotBitsOverride": { "locations/locations": 20, "*/special": 16 },
              "objects": [ "*/database/{id}" ]
            }
            """);

        Assert.Equal(20, profile.SlotBitsFor("locations/locations"));
        Assert.Equal(16, profile.SlotBitsFor("countries/special"));
        Assert.Equal(24, profile.SlotBitsFor("countries/database"));
    }

    [Fact]
    public void Missing_values_fall_back_to_the_defaults()
    {
        var profile = DesyncProfile.Parse("{ }");

        Assert.Equal(24, profile.IdSlotBits);
        Assert.Equal(262144, profile.SizeCapBytes);
        Assert.Equal(4, profile.NodeDepth);
        Assert.Equal(0.001, profile.FloatDrift);
        Assert.Empty(profile.Objects);
        Assert.Empty(profile.Noise);
        Assert.Empty(profile.Names);
        Assert.False(profile.IsObjectContainer("countries/database"));
        Assert.Equal(24, profile.SlotBitsFor("countries/database"));
        Assert.Null(profile.Describe("RANDOM_COUNT"));
    }

    [Fact]
    public void Object_patterns_must_end_in_an_id_segment()
    {
        Assert.Throws<InvalidDataException>(() => DesyncProfile.Parse("""{ "objects": [ "countries/database" ] }"""));
    }

    [Fact]
    public void The_profile_path_comes_from_the_profiles_directory_and_the_game_profile_entry()
    {
        Assert.Equal(Path.Combine(@"C:\p", "desync/eu5.json"), DesyncProfile.PathFor(@"C:\p", "desync/eu5.json"));
        Assert.Null(DesyncProfile.PathFor(@"C:\p", null));
        Assert.Null(DesyncProfile.PathFor(@"C:\p", " "));
    }

    [Fact]
    public void The_eu5_game_profile_points_to_the_desync_rules()
    {
        var profile = GameProfileLoader.LoadFromFile(Path.Combine(ProfilesDirectory, "eu5.json"));

        Assert.Equal("desync/eu5.json", profile.DesyncRules);
        Assert.True(File.Exists(DesyncProfile.PathFor(ProfilesDirectory, profile.DesyncRules)));
    }

    [Fact]
    public void An_unknown_checksum_category_gives_null_so_the_window_shows_the_general_text()
    {
        Assert.Null(Shipped().Describe("SOMETHING_NEW"));
    }
}
