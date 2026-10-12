using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>Interface-Rework 2: Spielkürzel für "Meine {Kürzel} Mods".</summary>
public sealed class GameProfileShortNameTests
{
    [Fact]
    public void ShortNameFallsBackToDisplayName()
    {
        Assert.Equal("Europa Universalis V", new GameProfile { DisplayName = "Europa Universalis V" }.EffectiveShortName);
        Assert.Equal("Europa Universalis V", new GameProfile { DisplayName = "Europa Universalis V", ShortName = " " }.EffectiveShortName);
        Assert.Equal("EU V", new GameProfile { DisplayName = "Europa Universalis V", ShortName = "EU V" }.EffectiveShortName);
    }

    [Theory]
    [InlineData("ck3.json", "CK III")]
    [InlineData("eu4.json", "EU IV")]
    [InlineData("eu5.json", "EU V")]
    [InlineData("vic3.json", "Vic 3")]
    public void ShippedProfilesCarryShortNames(string file, string expected)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        // Das Testausgabe-Verzeichnis enthält eine Kopie von profiles\eu4.json; die Repository-Wurzel erkennt die Solution-Datei.
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ParadoxModTool.slnx")))
        {
            dir = dir.Parent;
        }

        var profile = GameProfileLoader.LoadFromFile(Path.Combine(dir!.FullName, "profiles", file));

        Assert.Equal(expected, profile.EffectiveShortName);
    }
}
