using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class KeyOperatorsTests
{
    [Fact]
    public void Eu5_profile_lists_all_six_operators()
    {
        var profile = GameProfileLoader.LoadFromFile(TempDirTests.RepoFile("profiles/eu5.json"));
        Assert.Equal(
            new[] { "INJECT", "REPLACE", "TRY_INJECT", "TRY_REPLACE", "INJECT_OR_CREATE", "REPLACE_OR_CREATE" }.OrderBy(x => x),
            profile.KeyOperators.OrderBy(x => x));
    }

    [Fact]
    public void Eu4_profile_has_no_operators()
    {
        Assert.Empty(GameProfileLoader.LoadFromFile(TempDirTests.RepoFile("profiles/eu4.json")).KeyOperators);
    }

    [Fact]
    public void ParseFor_ignores_prefixes_the_game_does_not_know()
    {
        var key = OverrideKey.ParseFor("INJECT:farm", Array.Empty<string>());
        Assert.Equal(OverrideOperator.None, key.Operator);
        Assert.Equal("INJECT:farm", key.Key);
    }

    [Fact]
    public void ParseFor_recognises_known_prefixes()
    {
        var key = OverrideKey.ParseFor("TRY_REPLACE:farm", new[] { "TRY_REPLACE" });
        Assert.Equal(OverrideOperator.TryReplace, key.Operator);
        Assert.Equal("farm", key.Key);
    }
}
