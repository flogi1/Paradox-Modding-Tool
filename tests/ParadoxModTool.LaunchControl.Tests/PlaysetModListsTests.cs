using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class PlaysetModListsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-modlists-" + Guid.NewGuid().ToString("N"));

    public PlaysetModListsTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static PlaysetModEntry Mod(string path, string? name = null, bool enabled = true) => new(path, name, enabled, null);

    [Fact]
    public void Empty_query_matches_all()
    {
        Assert.True(PlaysetModFilter.Matches(Mod("C:/x/y"), "  "));
    }

    [Fact]
    public void All_words_must_match_name_or_path()
    {
        var mod = Mod(@"D:\steam\workshop\123", "Flogi's Buildings");

        Assert.True(PlaysetModFilter.Matches(mod, "flogi build"));
        Assert.True(PlaysetModFilter.Matches(mod, "buildings workshop"));
        Assert.False(PlaysetModFilter.Matches(mod, "flogi tech"));
    }

    [Fact]
    public void Case_insensitive_including_umlauts()
    {
        Assert.True(PlaysetModFilter.Matches(Mod("C:/x", "Ärger der Könige"), "äRGER könige"));
    }

    [Fact]
    public void Disabled_mod_still_matches()
    {
        Assert.True(PlaysetModFilter.Matches(Mod("C:/x", "Abc", enabled: false), "abc"));
    }

    [Fact]
    public void Collect_merges_known_mods_and_folders_without_duplicates()
    {
        var folder = Path.Combine(_root, "Local Two");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(_root, "Local Two.mod"), "x");
        var fresh = Path.Combine(_root, "Fresh");
        Directory.CreateDirectory(fresh);
        File.WriteAllText(Path.Combine(_root, "Fresh.mod"), "x");
        var known = new[] { new PlaysetModEntry(folder.ToUpperInvariant() + "\\", "Local Two", true, "m2"), new PlaysetModEntry(@"D:\steam\1", "Workshop One", true, "m1") };

        var result = PlaysetInstalledMods.Collect(known, _root, Array.Empty<PlaysetData>(), requireDescriptor: true);

        Assert.Equal(new[] { "Fresh", "Local Two", "Workshop One" }, result.Select(m => m.DisplayName));
        Assert.Equal("m2", result.Single(m => m.DisplayName == "Local Two").LauncherModId);
    }

    [Fact]
    public void Collect_for_launcher_games_skips_folders_without_mod_file()
    {
        Directory.CreateDirectory(Path.Combine(_root, "No Descriptor"));

        var result = PlaysetInstalledMods.Collect(Array.Empty<PlaysetModEntry>(), _root, Array.Empty<PlaysetData>(), requireDescriptor: true);

        Assert.Empty(result);
    }

    [Fact]
    public void Collect_for_json_games_includes_folders_and_paths_from_all_playsets()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Own Mod"));
        var withMetadata = Path.Combine(_root, "With Meta");
        Directory.CreateDirectory(Path.Combine(withMetadata, ".metadata"));
        File.WriteAllText(Path.Combine(withMetadata, ".metadata", "metadata.json"), "{\"name\":\"Pretty Name\"}");
        var other = new PlaysetData { Id = "o", Name = "o", Mods = { new PlaysetModEntry("C:/steam/workshop/999/", null, true, null) } };

        var result = PlaysetInstalledMods.Collect(Array.Empty<PlaysetModEntry>(), _root, new[] { other }, requireDescriptor: false);

        Assert.Equal(new[] { "999", "Own Mod", "Pretty Name" }, result.Select(m => m.DisplayName));
    }

    [Fact]
    public void Collect_names_fall_back_to_the_folder_name()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Plain"));

        var result = PlaysetInstalledMods.Collect(Array.Empty<PlaysetModEntry>(), _root, Array.Empty<PlaysetData>(), requireDescriptor: false);

        Assert.Equal("Plain", Assert.Single(result).DisplayName);
    }
}
