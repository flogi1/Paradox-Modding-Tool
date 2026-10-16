using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>Playset-Verwaltung: welche Lade-Datei das Spiel beim direkten Start liest.</summary>
public sealed class GameProfilePlaysetLoadFileTests
{
    [Theory]
    [InlineData("ck3.json", "dlc_load.json")]
    [InlineData("eu4.json", "dlc_load.json")]
    [InlineData("vic3.json", "content_load.json")]
    [InlineData("eu5.json", null)]
    public void ShippedProfilesNameTheLoadFile(string file, string? expected)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ParadoxModTool.slnx")))
        {
            dir = dir.Parent;
        }

        var profile = GameProfileLoader.LoadFromFile(Path.Combine(dir!.FullName, "profiles", file));

        Assert.Equal(expected, profile.PlaysetLoadFile);
    }
}
