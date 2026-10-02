using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.SaveGames.Tests;

public class SaveGameFolderTests
{
    private static readonly SaveGameSettings Settings = new() { Folder = "save games", Extension = ".eu5" };

    [Fact]
    public void FolderIsNextToTheModFolder()
    {
        var folder = SaveGameFolder.For("%USERPROFILE%/Documents/Paradox Interactive/Europa Universalis V/mod", Settings);

        Assert.EndsWith(Path.Combine("Paradox Interactive", "Europa Universalis V", "save games"), folder);
        Assert.DoesNotContain("%", folder);
    }

    [Fact]
    public void NoFolderWithoutModRootTemplate()
    {
        Assert.Null(SaveGameFolder.For(null, Settings));
        Assert.Null(SaveGameFolder.For(" ", Settings));
    }
}
