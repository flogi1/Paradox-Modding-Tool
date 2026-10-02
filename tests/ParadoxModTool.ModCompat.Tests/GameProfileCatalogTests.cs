using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>Spielauswahl und Profilliste zeigen die Spiele in der Reihenfolge ihres Erscheinens.</summary>
public class GameProfileCatalogTests : TempDirTests
{
    private void Profile(string id, string name, string? releaseDate)
    {
        var date = releaseDate is null ? string.Empty : $", \"releaseDate\": \"{releaseDate}\"";
        File.WriteAllText(Path.Combine(Dir, id + ".json"), $"{{ \"id\": \"{id}\", \"displayName\": \"{name}\"{date} }}");
    }

    [Fact]
    public void Profiles_are_ordered_by_release_date()
    {
        Profile("eu5", "Europa Universalis V", "2025-11-04");
        Profile("ck3", "Crusader Kings III", "2020-09-01");
        Profile("eu4", "Europa Universalis IV", "2013-08-13");

        var ids = GameProfileCatalog.Discover(Dir).Select(e => e.Profile.Id).ToList();

        Assert.Equal(new[] { "eu4", "ck3", "eu5" }, ids);
    }

    [Fact]
    public void Profiles_without_release_date_come_last_by_name()
    {
        Profile("zzz", "Zeta", null);
        Profile("aaa", "Alpha", null);
        Profile("eu4", "Europa Universalis IV", "2013-08-13");

        var ids = GameProfileCatalog.Discover(Dir).Select(e => e.Profile.Id).ToList();

        Assert.Equal(new[] { "eu4", "aaa", "zzz" }, ids);
    }

    [Fact]
    public void Shipped_profiles_all_carry_a_release_date()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "profiles", "eu4.json")))
        {
            dir = dir.Parent;
        }

        var entries = GameProfileCatalog.Discover(Path.Combine(dir!.FullName, "profiles"));

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.NotNull(e.Profile.ReleaseDate));
    }
}
