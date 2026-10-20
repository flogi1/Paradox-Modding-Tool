using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public sealed class LocalizationFoldersTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pmt-locfolders-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Names_are_american_then_british() =>
        Assert.Equal(new[] { "localization", "localisation" }, LocalizationFolders.Names);

    [Fact]
    public void Existing_finds_british_spelling()
    {
        Directory.CreateDirectory(Path.Combine(_root, "localisation"));

        Assert.Equal(new[] { Path.Combine(_root, "localisation") }, LocalizationFolders.Existing(_root));
    }

    [Fact]
    public void Existing_finds_both_in_order()
    {
        Directory.CreateDirectory(Path.Combine(_root, "localisation"));
        Directory.CreateDirectory(Path.Combine(_root, "localization"));

        Assert.Equal(new[] { Path.Combine(_root, "localization"), Path.Combine(_root, "localisation") }, LocalizationFolders.Existing(_root));
    }

    [Fact]
    public void Existing_is_empty_for_missing_root() =>
        Assert.Empty(LocalizationFolders.Existing(Path.Combine(_root, "nope")));
}
