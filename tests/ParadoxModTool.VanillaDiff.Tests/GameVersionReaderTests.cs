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
    public void TryReadVersion_UngueltigesJson_LiefertNull()
    {
        File.WriteAllText(Path.Combine(_gameRoot, "launcher-settings.json"), "{ das ist kein json");
        Assert.Null(GameVersionReader.TryReadVersion(_gameRoot));
    }
}
