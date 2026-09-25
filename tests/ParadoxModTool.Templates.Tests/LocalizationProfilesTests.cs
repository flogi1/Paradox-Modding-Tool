using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class LocalizationProfilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-locprof-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string File1 => Path.Combine(_dir, "localization", "profiles.json");

    private static LocalizationProfiles Sample() => new()
    {
        Fingerprint = "abc",
        LearnedUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
        FilesScanned = 7,
        Profiles =
        {
            new LocalizationFolderProfile
            {
                Path = "common/building_types",
                ObjectCount = 465,
                Objects = { new LocalizationObjectPattern { Prefix = "", Suffix = "_desc", Share = 1, Preselected = true } },
                Fields = { new LocalizationFieldPattern { Name = "text", Hits = 20, Share = 0.5, Preselected = false } },
            },
            new LocalizationFolderProfile { Path = "common" },
            new LocalizationFolderProfile { Path = "gui" },
        },
    };

    [Fact]
    public void A_profile_file_round_trips_with_readable_names()
    {
        var store = new LocalizationProfileStore(File1);

        store.Save(Sample());
        var back = store.TryLoad()!;

        var json = File.ReadAllText(File1);
        Assert.Contains("\"objects\"", json);
        Assert.Contains("\"preselected\"", json);
        Assert.Equal("abc", back.Fingerprint);
        Assert.Equal(7, back.FilesScanned);
        Assert.Equal(new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc), back.LearnedUtc);
        var building = back.Profiles.Single(p => p.Path == "common/building_types");
        Assert.Equal(465, building.ObjectCount);
        Assert.Equal("_desc", Assert.Single(building.Objects).Suffix);
        Assert.Equal(20, Assert.Single(building.Fields).Hits);
    }

    [Theory]
    [InlineData("common/building_types", "common/building_types")]
    [InlineData("common/building_types/sub/deeper", "common/building_types")]
    [InlineData("common/laws", "common")]
    [InlineData("common/building_types_x", "common")]
    [InlineData("gui", "gui")]
    [InlineData("COMMON\\Building_Types\\x", "common/building_types")]
    public void The_longest_matching_path_wins(string folder, string expected)
    {
        Assert.Equal(expected, Sample().Find(folder)!.Path);
    }

    [Theory]
    [InlineData("events")]
    [InlineData("")]
    [InlineData(null)]
    public void No_matching_path_gives_no_profile(string? folder)
    {
        Assert.Null(Sample().Find(folder));
    }

    [Fact]
    public void A_missing_file_gives_null()
    {
        Assert.Null(new LocalizationProfileStore(File1).TryLoad());
    }

    [Fact]
    public void A_broken_file_is_copied_as_bad_and_gives_null()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File1)!);
        File.WriteAllText(File1, "{ das ist kein json");

        Assert.Null(new LocalizationProfileStore(File1).TryLoad());
        Assert.Equal("{ das ist kein json", File.ReadAllText(File1 + ".bad"));
    }

    [Fact]
    public void A_file_of_another_version_gives_null_without_a_bad_copy()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File1)!);
        File.WriteAllText(File1, "{ \"version\": 0, \"profiles\": [] }");

        Assert.Null(new LocalizationProfileStore(File1).TryLoad());
        Assert.False(File.Exists(File1 + ".bad"));
    }

    [Fact]
    public void Null_lists_in_a_file_are_repaired()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File1)!);
        File.WriteAllText(File1, "{ \"version\": 1, \"profiles\": [ { \"path\": \"gui\", \"objects\": null, \"fields\": null }, null ] }");

        var loaded = new LocalizationProfileStore(File1).TryLoad()!;

        var profile = Assert.Single(loaded.Profiles);
        Assert.Empty(profile.Objects);
        Assert.Empty(profile.Fields);
    }
}
