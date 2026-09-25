using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class GameContentRootTests : TempDirTests
{
    [Fact]
    public void Eu5_layout_exe_in_binaries_content_in_sibling_game_folder()
    {
        var install = Make("Europa Universalis V");
        var exe = Path.Combine(Make("Europa Universalis V", "binaries"), "eu5.exe");
        File.WriteAllText(exe, "x");
        var game = Make("Europa Universalis V", "game");

        Assert.Equal(game, GameContentRoot.Find(exe));
        Assert.Equal(install, GameContentRoot.InstallRoot(exe));
    }

    [Fact]
    public void Eu4_layout_exe_and_content_share_the_install_folder()
    {
        var install = Make("Europa Universalis IV");
        Directory.CreateDirectory(Path.Combine(install, "common"));
        Directory.CreateDirectory(Path.Combine(install, "events"));
        var exe = Path.Combine(install, "eu4.exe");
        File.WriteAllText(exe, "x");

        Assert.Equal(install, GameContentRoot.Find(exe));
        Assert.Equal(install, GameContentRoot.InstallRoot(exe));
    }

    [Fact]
    public void Unknown_layout_and_missing_input_give_null()
    {
        var folder = Make("Irgendwo");
        var exe = Path.Combine(folder, "spiel.exe");
        File.WriteAllText(exe, "x");

        Assert.Null(GameContentRoot.Find(exe));
        Assert.Null(GameContentRoot.Find(null));
        Assert.Null(GameContentRoot.Find("  "));
    }
}
