using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class SteamworksLocatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-swl-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string Library(string name) => Path.Combine(_dir, name);

    private void Manifest(string library, string appId, string installDir)
    {
        var steamapps = Path.Combine(Library(library), "steamapps");
        Directory.CreateDirectory(steamapps);
        File.WriteAllText(
            Path.Combine(steamapps, $"appmanifest_{appId}.acf"),
            $"\"AppState\"\n{{\n\t\"appid\"\t\t\"{appId}\"\n\t\"installdir\"\t\t\"{installDir}\"\n}}\n");
    }

    private string Dll(string library, string installDir, string? sub)
    {
        var folder = Path.Combine(Library(library), "steamapps", "common", installDir);
        if (sub is not null)
        {
            folder = Path.Combine(folder, sub);
        }

        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "steam_api64.dll");
        File.WriteAllText(path, "dll");
        return path;
    }

    [Fact]
    public void FindetDieDllImBinariesOrdner()
    {
        Manifest("lib1", "3450310", "Europa Universalis V");
        var expected = Dll("lib1", "Europa Universalis V", "binaries");

        Assert.Equal(expected, SteamworksLocator.Find(new[] { Library("lib1") }, "3450310"));
    }

    [Fact]
    public void FindetDieDllImSpielordner_WennBinariesFehlt()
    {
        Manifest("lib1", "3450310", "Game");
        var expected = Dll("lib1", "Game", null);

        Assert.Equal(expected, SteamworksLocator.Find(new[] { Library("lib1") }, "3450310"));
    }

    [Fact]
    public void SuchtInMehrerenBibliotheken()
    {
        Directory.CreateDirectory(Path.Combine(Library("lib1"), "steamapps"));
        Manifest("lib2", "3450310", "Game");
        var expected = Dll("lib2", "Game", "binaries");

        Assert.Equal(expected, SteamworksLocator.Find(new[] { Library("lib1"), Library("lib2") }, "3450310"));
    }

    [Fact]
    public void FehlendeDllOderManifest_IstNull()
    {
        Manifest("lib1", "3450310", "Game");

        Assert.Null(SteamworksLocator.Find(new[] { Library("lib1") }, "3450310"));
        Assert.Null(SteamworksLocator.Find(new[] { Library("lib1") }, "999"));
        Assert.Null(SteamworksLocator.Find(Array.Empty<string>(), "3450310"));
    }

    [Fact]
    public void UnsichererInstallDir_WirdIgnoriert()
    {
        Manifest("lib1", "3450310", "..\\..\\evil");

        Assert.Null(SteamworksLocator.Find(new[] { Library("lib1") }, "3450310"));
    }

    [Theory]
    [InlineData("client", true, "client")]
    [InlineData("client", false, "client")]
    [InlineData("steamcmd", true, "steamcmd")]
    [InlineData(null, true, "client")]
    [InlineData(null, false, "steamcmd")]
    [InlineData("unsinn", true, "client")]
    [InlineData("unsinn", false, "steamcmd")]
    public void UploadMethods_Resolve(string? saved, bool clientAvailable, string expected)
    {
        Assert.Equal(expected, UploadMethods.Resolve(saved, clientAvailable));
    }
}
