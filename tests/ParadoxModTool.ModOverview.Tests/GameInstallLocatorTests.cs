using System.Text.Json;
using Xunit;

namespace ParadoxModTool.ModOverview.Tests;

public class GameInstallLocatorTests : TempDirTests
{
    private string GameDirectoryWithExe(string exePath)
    {
        var gameDirectory = Make("appdata", "games", "g");
        File.WriteAllText(Path.Combine(gameDirectory, "session.json"), JsonSerializer.Serialize(new { GameExecutablePath = exePath }));
        return gameDirectory;
    }

    [Fact]
    public void FromSession_UsesExeFolderAndBinariesParent()
    {
        var eu5 = Make("EU5");
        Make("EU5", "binaries");
        Make("EU5", "game");
        Assert.Equal(eu5, GameInstallLocator.FromSession(GameDirectoryWithExe(Path.Combine(eu5, "binaries", "eu5.exe"))));

        var eu4 = Make("EU4");
        Make("EU4", "common");
        Assert.Equal(eu4, GameInstallLocator.FromSession(GameDirectoryWithExe(Path.Combine(eu4, "eu4.exe"))));
    }

    [Fact]
    public void FromSession_MissingFileOrFolder_Null()
    {
        Assert.Null(GameInstallLocator.FromSession(Make("appdata", "games", "empty")));
        Assert.Null(GameInstallLocator.FromSession(GameDirectoryWithExe(Path.Combine(Dir, "gone", "binaries", "x.exe"))));
    }

    [Fact]
    public void FromSteam_ReadsManifest()
    {
        var library = Make("lib");
        var game = Make("lib", "steamapps", "common", "Europa Universalis IV");
        File.WriteAllText(Path.Combine(library, "steamapps", "appmanifest_236850.acf"), "\"AppState\"\n{\n\t\"appid\"\t\t\"236850\"\n\t\"installdir\"\t\t\"Europa Universalis IV\"\n}\n");

        Assert.Equal(game, GameInstallLocator.FromSteam(new[] { Make("other"), library }, "236850"));
        Assert.Null(GameInstallLocator.FromSteam(new[] { library }, "1158310"));
        Assert.Null(GameInstallLocator.FromSteam(new[] { library }, null));
    }

    [Fact]
    public void Find_PrefersSession()
    {
        var library = Make("lib");
        Make("lib", "steamapps", "common", "EU5 Steam");
        File.WriteAllText(Path.Combine(library, "steamapps", "appmanifest_3450310.acf"), "\"AppState\" { \"installdir\" \"EU5 Steam\" }");
        var own = Make("EU5");
        Make("EU5", "binaries");

        var gameDirectory = GameDirectoryWithExe(Path.Combine(own, "binaries", "eu5.exe"));

        Assert.Equal(own, GameInstallLocator.Find(gameDirectory, new[] { library }, "3450310"));
        Assert.Equal(Path.Combine(library, "steamapps", "common", "EU5 Steam"), GameInstallLocator.Find(Make("appdata", "games", "none"), new[] { library }, "3450310"));
    }
}
