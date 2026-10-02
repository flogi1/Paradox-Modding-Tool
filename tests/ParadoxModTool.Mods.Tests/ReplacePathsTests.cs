using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ReplacePathsTests : TempDirTests
{
    [Fact]
    public void Direct_file_is_hidden() =>
        Assert.True(ReplacePaths.Hides(new[] { "common/religion" }, "common/religion/00_x.txt"));

    [Fact]
    public void Subfolder_file_is_not_hidden() =>
        Assert.False(ReplacePaths.Hides(new[] { "common/religion" }, "common/religion/religion_types/00_x.txt"));

    [Fact]
    public void Case_and_slashes_are_ignored() =>
        Assert.True(ReplacePaths.Hides(new[] { "Common\\Religion\\" }, "common/religion/a.txt"));

    [Fact]
    public void Empty_list_hides_nothing() =>
        Assert.False(ReplacePaths.Hides(Array.Empty<string>(), "common/religion/a.txt"));

    [Fact]
    public void Of_collects_the_paths_of_all_folders()
    {
        var a = Make("a");
        var b = Make("b");
        File.WriteAllText(Path.Combine(Dir, "a.mod"), "replace_path=\"history/titles\"\nreplace_path=\"events\"\n");
        File.WriteAllText(Path.Combine(Dir, "b.mod"), "replace_path=\"events\"\n");

        Assert.Equal(new[] { "events", "history/titles" }, ReplacePaths.Of(new[] { a, b }, ModMetadata.ClassicSiblingMetadataFile));
    }

    private static readonly string[] Eu5Roots = { "in_game", "main_menu", "loading_screen" };

    [Fact]
    public void Root_folder_is_skipped_before_comparing() =>
        Assert.True(ReplacePaths.Hides(new[] { "common/trait_flavor" }, "in_game/common/trait_flavor/00_default.txt", Eu5Roots));

    [Fact]
    public void Path_without_root_hides_in_every_root() =>
        Assert.True(ReplacePaths.Hides(new[] { "gui/pmt_rp1" }, "main_menu/gui/pmt_rp1/a.gui", Eu5Roots));

    [Fact]
    public void Path_with_root_prefix_hides_nothing() =>
        Assert.False(ReplacePaths.Hides(new[] { "in_game/common/trait_flavor" }, "in_game/common/trait_flavor/00_default.txt", Eu5Roots));

    [Fact]
    public void Without_roots_the_first_folder_counts() =>
        Assert.False(ReplacePaths.Hides(new[] { "common/trait_flavor" }, "in_game/common/trait_flavor/00_default.txt"));

    [Fact]
    public void Of_drops_paths_with_root_prefix()
    {
        var a = Make("a", ".metadata");
        File.WriteAllText(Path.Combine(a, "metadata.json"),
            "{ \"name\": \"a\", \"game_custom_data\": { \"replace_paths\": [ \"in_game/common/language_families\", \"common/trait_flavor\", \"gui/pmt_rp1\" ] } }");

        Assert.Equal(new[] { "common/trait_flavor", "gui/pmt_rp1" }, ReplacePaths.Of(new[] { Path.Combine(Dir, "a") }, ".metadata/metadata.json", Eu5Roots));
    }
}
