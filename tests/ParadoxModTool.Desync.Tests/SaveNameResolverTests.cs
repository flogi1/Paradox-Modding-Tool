using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class SaveNameResolverTests
{
    private const string ProfileJson = """
        {
          "names": {
            "countries/database/{id}": { "tagList": "countries/tags" },
            "locations/locations/{id}": { "keyList": "metadata/compatibility/locations", "keyListBase": 1 }
          }
        }
        """;

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Tables(
        string countries = "1384=BLG", string locations = "2=uppsala") =>
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["countries/database/{id}"] = Parse(countries),
            ["locations/locations/{id}"] = Parse(locations),
        };

    private static IReadOnlyDictionary<string, string> Parse(string pairs) =>
        pairs.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('='))
            .ToDictionary(parts => parts[0], parts => parts[1]);

    private static SaveNameResolver Resolver(SaveNameTables? tables = null) =>
        new(tables ?? new SaveNameTables(Tables()), DesyncProfile.Parse(ProfileJson));

    [Fact]
    public void Country_and_location_ids_are_shown_with_tag_and_key()
    {
        var resolver = Resolver();

        Assert.Equal("countries › 1384 (BLG) › government", resolver.Display("countries/database/1384/government"));
        Assert.Equal("locations › 2 (uppsala) › population", resolver.Display("locations/locations/2/population"));
        Assert.Equal("countries › 1384 (BLG)", resolver.Display("countries/database/1384"));
    }

    [Fact]
    public void Unknown_ids_stay_as_numbers()
    {
        var resolver = Resolver();

        // Objekte, deren Muster das Profil kennt, verlieren auch ohne bekannten Namen das Behältersegment; die Zahl bleibt.
        Assert.Equal("countries › 99999 › government", resolver.Display("countries/database/99999/government"));
        // Ein Pfad ohne Namensmuster behält alle Segmente.
        Assert.Equal("war_manager › database › 5 › x", resolver.Display("war_manager/database/5/x"));
        Assert.Equal("", resolver.Display(""));
    }

    [Fact]
    public void Segments_with_an_occurrence_suffix_still_resolve()
    {
        var resolver = Resolver();

        Assert.Equal("countries › 1384 (BLG) › a#2", resolver.Display("countries/database/1384/a#2"));
    }

    [Fact]
    public void Merged_tables_prefer_the_reference_side_and_keep_names_only_the_other_side_knows()
    {
        var merged = SaveNameTables.Merge(
            Tables(countries: "1384=BLG,5=FRA"),
            Tables(countries: "1384=XXX,7=ENG", locations: "2=uppsala,3=lund"));
        var resolver = Resolver(merged);

        Assert.Equal("countries › 1384 (BLG)", resolver.Display("countries/database/1384"));
        Assert.Equal("countries › 5 (FRA)", resolver.Display("countries/database/5"));
        Assert.Equal("countries › 7 (ENG)", resolver.Display("countries/database/7"));
        Assert.Equal("locations › 3 (lund)", resolver.Display("locations/locations/3"));
    }
}
