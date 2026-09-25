using ParadoxModTool.Categories;
using Xunit;

namespace ParadoxModTool.Categories.Tests;

public class FileOriginClassifierTests
{
    private static readonly string[] Vanilla = { @"C:\Games\EU5\game" };
    private static readonly string[] Mods = { @"C:\Users\me\Documents\Paradox Interactive\EU5\mod" };

    [Theory]
    [InlineData(@"C:\Games\EU5\game\common\a.txt", FileOrigin.Vanilla)]
    [InlineData(@"C:\Users\me\Documents\Paradox Interactive\EU5\mod\my_mod\common\a.txt", FileOrigin.Mod)]
    [InlineData(@"C:\Temp\notes.txt", FileOrigin.External)]
    [InlineData(@"C:\Games\EU5\binaries\eu5.exe", FileOrigin.External)]
    public void ClassifiesByRoot(string path, FileOrigin expected)
    {
        Assert.Equal(expected, FileOriginClassifier.Classify(path, Vanilla, Mods));
    }

    [Fact]
    public void IgnoresCaseAndSlashStyle()
    {
        Assert.Equal(FileOrigin.Vanilla, FileOriginClassifier.Classify("c:/games/eu5/GAME/common/a.txt", Vanilla, Mods));
    }

    [Fact]
    public void RootWithTrailingSeparatorWorks()
    {
        Assert.Equal(FileOrigin.Vanilla, FileOriginClassifier.Classify(@"C:\Games\EU5\game\a.txt", new[] { @"C:\Games\EU5\game\" }, Mods));
    }

    [Fact]
    public void SiblingFolderWithSamePrefixDoesNotMatch()
    {
        Assert.Equal(FileOrigin.External, FileOriginClassifier.Classify(@"C:\Games\EU5\game_backup\a.txt", Vanilla, Mods));
    }

    [Fact]
    public void RootItselfIsNotAFile()
    {
        Assert.Equal(FileOrigin.External, FileOriginClassifier.Classify(@"C:\Games\EU5\game", Vanilla, Mods));
    }

    [Fact]
    public void DeeperRootWins()
    {
        var vanilla = new[] { @"C:\Games\EU5" };
        var mods = new[] { @"C:\Games\EU5\game\mod" };
        Assert.Equal(FileOrigin.Mod, FileOriginClassifier.Classify(@"C:\Games\EU5\game\mod\x\a.txt", vanilla, mods));
        Assert.Equal(FileOrigin.Vanilla, FileOriginClassifier.Classify(@"C:\Games\EU5\game\common\a.txt", vanilla, mods));
    }

    [Fact]
    public void SameRootInBothListsPrefersMod()
    {
        var both = new[] { @"C:\X" };
        Assert.Equal(FileOrigin.Mod, FileOriginClassifier.Classify(@"C:\X\a.txt", both, both));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnsavedOrEmptyPathIsExternal(string? path)
    {
        Assert.Equal(FileOrigin.External, FileOriginClassifier.Classify(path, Vanilla, Mods));
    }

    [Fact]
    public void EmptyOrInvalidRootsAreIgnored()
    {
        Assert.Equal(FileOrigin.External, FileOriginClassifier.Classify(@"C:\a\b.txt", new[] { "", "  ", "\0bad" }, Array.Empty<string>()));
    }
}
