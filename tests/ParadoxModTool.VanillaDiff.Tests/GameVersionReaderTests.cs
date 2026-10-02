using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class GameVersionReaderTests : IDisposable
{
    private readonly string _gameRoot = Directory.CreateTempSubdirectory("pmt-gameroot-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_gameRoot, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void TryReadVersion_KeineDatei_LiefertNull()
    {
        Assert.Null(GameVersionReader.TryReadVersion(_gameRoot));
    }

    [Fact]
    public void TryReadVersion_RawVersionImSpielordner_WirdGelesen()
    {
        File.WriteAllText(Path.Combine(_gameRoot, "launcher-settings.json"), "{ \"rawVersion\": \"1.2.3\" }");
        Assert.Equal("1.2.3", GameVersionReader.TryReadVersion(_gameRoot));
    }

    [Fact]
    public void TryReadVersion_VersionImBinariesUnterordner_WirdAlsFallbackGelesen()
    {
        var binariesDir = Path.Combine(_gameRoot, "binaries");
        Directory.CreateDirectory(binariesDir);
        File.WriteAllText(Path.Combine(binariesDir, "launcher-settings.json"), "{ \"version\": \"4.5.6\" }");

        Assert.Equal("4.5.6", GameVersionReader.TryReadVersion(_gameRoot));
    }

    [Fact]
    public void TryReadVersion_LauncherSubfolder()
    {
        var launcherDir = Path.Combine(_gameRoot, "launcher");
        Directory.CreateDirectory(launcherDir);
        File.WriteAllText(Path.Combine(launcherDir, "launcher-settings.json"), "{ \"version\": \"1.19.0.6 (Scribe)\", \"rawVersion\": \"1.19.0.6\" }");

        Assert.Equal("1.19.0.6", GameVersionReader.TryReadVersion(_gameRoot));
    }

    [Fact]
    public void TryReadVersion_UngueltigesJson_LiefertNull()
    {
        File.WriteAllText(Path.Combine(_gameRoot, "launcher-settings.json"), "{ das ist kein json");
        Assert.Null(GameVersionReader.TryReadVersion(_gameRoot));
    }

    [Fact]
    public void TryReadVersion_EuV_ReadsReleaseBranchFromTheExeNextToTheGameFolder()
    {
        // EU V: kein launcher-settings.json; die Version steht als Zweigname "…/release/1.3.11" in binaries\eu5.exe neben game\.
        var game = Path.Combine(_gameRoot, "game");
        var binaries = Path.Combine(_gameRoot, "binaries");
        Directory.CreateDirectory(game);
        Directory.CreateDirectory(binaries);
        var filler = new byte[5 * 1024 * 1024];
        var marker = System.Text.Encoding.ASCII.GetBytes("\0caesar/u26q2/release/1.3.11\0\0Pavia\0");
        File.WriteAllBytes(Path.Combine(binaries, "eu5.exe"), filler.Concat(marker).Concat(filler).ToArray());

        Assert.Equal("1.3.11", GameVersionReader.TryReadVersion(game));
    }

    [Fact]
    public void TryReadVersion_ExeWithoutReleaseBranch_GivesNull()
    {
        var game = Path.Combine(_gameRoot, "game");
        var binaries = Path.Combine(_gameRoot, "binaries");
        Directory.CreateDirectory(game);
        Directory.CreateDirectory(binaries);
        File.WriteAllBytes(Path.Combine(binaries, "eu5.exe"), System.Text.Encoding.ASCII.GetBytes("release/ x 1.3.11"));

        Assert.Null(GameVersionReader.TryReadVersion(game));
    }

    [Fact]
    public void TryReadVersion_RealEuVInstall_WhenPresent()
    {
        const string Game = @"C:\Program Files (x86)\Steam\steamapps\common\Europa Universalis V\game";
        if (!Directory.Exists(Game))
        {
            return;
        }

        // Release-Builds tragen die Version als Zweigname "…/release/1.3.11" in der Exe. Beta-Zweige heißen "…/release/next" ohne Nummer: Dann ist
        // null richtig (die Oberfläche fragt nach der Version). Den Fund selbst prüfen die Tests mit der nachgebauten Exe.
        var version = GameVersionReader.TryReadVersion(Game);
        Assert.True(version is null || System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+(\.\d+)+$"), $"Keine Versionsnummer: \"{version}\"");
    }

    [Fact]
    public void TryReadVersion_Ck3_ReadsLauncherFolderNextToTheGameFolder()
    {
        // CK III: game\ und launcher\launcher-settings.json liegen nebeneinander im Installationsordner.
        var game = Path.Combine(_gameRoot, "game");
        var launcher = Path.Combine(_gameRoot, "launcher");
        Directory.CreateDirectory(game);
        Directory.CreateDirectory(launcher);
        File.WriteAllText(Path.Combine(launcher, "launcher-settings.json"), "{ \"rawVersion\": \"1.20.0.2\" }");

        Assert.Equal("1.20.0.2", GameVersionReader.TryReadVersion(game));
    }
}
