using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModPathsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "pmt-paths");

    [Fact]
    public void Normalize_removes_trailing_separator_and_dot_segments()
    {
        var expected = Path.Combine(Root, "mod");

        Assert.Equal(expected, ModPaths.Normalize(expected + Path.DirectorySeparatorChar));
        Assert.Equal(expected, ModPaths.Normalize(Path.Combine(Root, "a", "..", "mod")));
    }

    [Fact]
    public void AreEqual_ignores_case_and_slash_style()
    {
        var forward = Root.Replace('\\', '/') + "/MOD/";

        Assert.True(ModPaths.AreEqual(Path.Combine(Root, "mod"), forward));
        Assert.False(ModPaths.AreEqual(Path.Combine(Root, "mod"), Path.Combine(Root, "mod2")));
    }

    [Fact]
    public void IsUnder_accepts_the_folder_itself_and_children_but_not_a_sibling_with_the_same_prefix()
    {
        var mod = Path.Combine(Root, "mod");

        Assert.True(ModPaths.IsUnder(mod, mod));
        Assert.True(ModPaths.IsUnder(Path.Combine(mod, "common", "a.txt"), mod));
        Assert.True(ModPaths.IsUnder(Path.Combine(Root, "MOD", "x"), mod));
        Assert.False(ModPaths.IsUnder(Path.Combine(Root, "mod2", "x"), mod));
        Assert.False(ModPaths.IsUnder(Root, mod));
    }

    [Fact]
    public void IsUnder_works_for_a_drive_root()
    {
        var driveRoot = Path.GetPathRoot(Root)!;

        Assert.True(ModPaths.IsUnder(Root, driveRoot));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\0b")]
    public void TryNormalize_is_false_for_empty_or_invalid_paths(string? path)
    {
        Assert.False(ModPaths.TryNormalize(path, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }
}
