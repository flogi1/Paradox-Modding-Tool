using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public sealed class EditorGameMatcherTests : IDisposable
{
    private readonly string _games = Directory.CreateTempSubdirectory("pmt-games-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_games, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static readonly GameFolders[] Games =
    {
        new("eu4", new[] { @"C:\Docs\Paradox\EU4" }, "236850"),
        new("eu5", new[] { @"C:\Docs\Paradox\EU5", @"D:\Games\Caesar" }, "3450310"),
    };

    [Fact]
    public void LongestRootOrWorkshopWins()
    {
        Assert.Equal("eu5", EditorGameMatcher.GameFor(@"D:\Games\Caesar\game\in_game\gui\topbar.gui", Games));
        Assert.Equal("eu5", EditorGameMatcher.GameFor("c:/docs/paradox/eu5/mod/x/a.gui", Games));
        Assert.Equal("eu4", EditorGameMatcher.GameFor(@"C:\Docs\Paradox\EU4\mod\a.txt", Games));
        Assert.Equal("eu5", EditorGameMatcher.GameFor(@"E:\Steam\steamapps\workshop\content\3450310\123\in_game\gui\a.gui", Games));
        Assert.Null(EditorGameMatcher.GameFor(@"C:\Docs\Paradox\EU5x\a.gui", Games));
        Assert.Null(EditorGameMatcher.GameFor(@"C:\Elsewhere\a.gui", Games));

        var nested = new[] { new GameFolders("outer", new[] { @"C:\Docs" }, null), new GameFolders("inner", new[] { @"C:\Docs\EU5" }, null) };
        Assert.Equal("inner", EditorGameMatcher.GameFor(@"C:\Docs\EU5\mod\a.gui", nested));
    }

    [Fact]
    public void ReaderTakesInstallDocumentsWorkshopAndRegisteredModsWithoutWriting()
    {
        var eu5 = Directory.CreateDirectory(Path.Combine(_games, "eu5")).FullName;
        File.WriteAllText(
            Path.Combine(eu5, "session.json"),
            "{ \"GameExecutablePath\": \"D:\\\\Games\\\\Caesar\\\\binaries\\\\eu5.exe\", \"PlaysetsFilePath\": \"E:\\\\OneDrive\\\\Dokumente\\\\Paradox\\\\EU5\\\\playsets.json\" }");
        File.WriteAllText(Path.Combine(eu5, "mods.json"), "{ \"version\": 1, \"mods\": [ { \"id\": \"a\", \"paths\": [ \"F:\\\\Work\\\\MyMod\" ] } ] }");
        var eu4 = Directory.CreateDirectory(Path.Combine(_games, "eu4")).FullName;
        File.WriteAllText(Path.Combine(eu4, "session.json"), "{ kaputt");
        File.WriteAllText(Path.Combine(eu4, "mods.json"), "nicht json");
        var before = Directory.GetFiles(_games, "*", SearchOption.AllDirectories).Length;

        var games = GameFolderReader.Read(_games, new[]
        {
            new GameProfileFolders("eu5", @"C:\Docs\Paradox\EU5\mod", "3450310"),
            new GameProfileFolders("eu4", @"C:\Docs\Paradox\EU4\mod", "236850"),
        });

        Assert.Equal(before, Directory.GetFiles(_games, "*", SearchOption.AllDirectories).Length);
        var five = games.Single(g => g.GameId == "eu5");
        Assert.Contains(@"C:\Docs\Paradox\EU5", five.Roots);
        Assert.Contains(@"D:\Games\Caesar", five.Roots);
        Assert.Contains(@"F:\Work\MyMod", five.Roots);
        Assert.Contains(@"E:\OneDrive\Dokumente\Paradox\EU5", five.Roots);
        Assert.Equal(new[] { @"C:\Docs\Paradox\EU4" }, games.Single(g => g.GameId == "eu4").Roots);
        Assert.Equal("eu5", EditorGameMatcher.GameFor(@"F:\Work\MyMod\in_game\gui\a.gui", games));
    }

    [Fact]
    public void InstallRootSkipsTheBinariesFolder()
    {
        Assert.Equal(@"D:\Games\Caesar", GameFolderReader.InstallRoot(@"D:\Games\Caesar\binaries\eu5.exe"));
        Assert.Equal(@"D:\Games\EU4", GameFolderReader.InstallRoot(@"D:\Games\EU4\eu4.exe"));
        Assert.Null(GameFolderReader.InstallRoot(null));
    }
}
