using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public sealed class FlagHideListTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-flaghide-" + Guid.NewGuid().ToString("N"));
    private readonly string _dir;
    private readonly string _mod;

    public FlagHideListTests()
    {
        _dir = Path.Combine(_temp, "hidden");
        _mod = Path.Combine(_temp, "mods", "My Mod");
        Directory.CreateDirectory(_mod);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temp, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void KeyFor_IsSixteenLowerHexAndIgnoresCaseAndTrailingSeparators()
    {
        var key = FlagHideList.KeyFor(_mod);

        Assert.Matches("^[0-9a-f]{16}$", key);
        Assert.Equal(key, FlagHideList.KeyFor(_mod.ToUpperInvariant() + Path.DirectorySeparatorChar));
        Assert.Equal(key, FlagHideList.KeyFor(_mod + "/"));
        Assert.NotEqual(key, FlagHideList.KeyFor(Path.Combine(_temp, "mods", "Other")));
    }

    [Fact]
    public void Load_MissingFile_IsEmpty()
    {
        var list = FlagHideList.Load(_dir, _mod, ignoreCase: false);

        Assert.Empty(list.Rules);
        Assert.False(list.IsHidden("a"));
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void Load_CorruptFile_IsEmptyAndDoesNotThrow()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, FlagHideList.KeyFor(_mod) + ".json");
        foreach (var text in new[] { "{ not json", "{\"a\":1}", "", "null" })
        {
            File.WriteAllText(path, text);
            Assert.Empty(FlagHideList.Load(_dir, _mod, false).Rules);
            Assert.False(File.Exists(path));
            Assert.Equal(text, File.ReadAllText(path + ".bad"));
        }
    }

    [Fact]
    public void Load_InvalidModDirectory_IsEmptyAndSaveFails()
    {
        var list = FlagHideList.Load(_dir, "bad\0path", false);

        Assert.Empty(list.Rules);
        Assert.True(list.Add("a"));
        Assert.Throws<IOException>(list.Save);
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void SetIgnoreCase_KeepsUnsavedRulesAndRebuildsMatchers()
    {
        var list = FlagHideList.Load(_dir, _mod, false);
        list.Add("Exact");
        list.Add("Pat_*");
        Assert.False(list.IsHidden("exact"));
        Assert.False(list.IsHidden("pat_1"));
        var version = list.Version;

        list.SetIgnoreCase(true);

        Assert.Equal(new[] { "Exact", "Pat_*" }, list.Rules);
        Assert.True(list.IgnoreCase);
        Assert.True(list.IsHidden("exact"));
        Assert.True(list.IsHidden("pat_1"));
        Assert.NotEqual(version, list.Version);
        Assert.False(File.Exists(Path.Combine(_dir, FlagHideList.KeyFor(_mod) + ".json")));
    }

    [Fact]
    public void ExactAndPatternRules_MatchAndRemoveTogether()
    {
        var list = FlagHideList.Load(_dir, _mod, false);
        for (var i = 0; i < 1000; i++)
        {
            list.Add($"name_{i}");
        }

        list.Add("name_1*");

        Assert.True(list.IsHidden("name_999"));
        Assert.True(list.IsHidden("name_1x"));
        Assert.False(list.IsHidden("name_2000x"));
        Assert.Equal(2, list.RemoveMatching("name_1"));
        Assert.False(list.IsHidden("name_1"));
        Assert.True(list.IsHidden("name_2"));
        Assert.Equal(999, list.Rules.Count);
    }

    [Fact]
    public void Load_SkipsNonStringAndBlankItems()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, FlagHideList.KeyFor(_mod) + ".json"), "[\"a_*\", 3, null, \"  \", \"b\"]");

        Assert.Equal(new[] { "a_*", "b" }, FlagHideList.Load(_dir, _mod, false).Rules);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsPerMod()
    {
        var list = FlagHideList.Load(_dir, _mod, false);
        Assert.True(list.Add("iv2_nat_idea_select_*"));
        Assert.True(list.Add(" exact_name "));
        list.Save();

        var again = FlagHideList.Load(_dir, _mod, false);
        Assert.Equal(new[] { "iv2_nat_idea_select_*", "exact_name" }, again.Rules);
        Assert.True(File.Exists(Path.Combine(_dir, FlagHideList.KeyFor(_mod) + ".json")));
        Assert.Single(Directory.GetFiles(_dir));
        Assert.Empty(FlagHideList.Load(_dir, Path.Combine(_temp, "mods", "Other"), false).Rules);
    }

    [Fact]
    public void Add_IgnoresEmptyAndDuplicates()
    {
        var list = FlagHideList.Load(_dir, _mod, false);

        Assert.False(list.Add("   "));
        Assert.True(list.Add("a"));
        Assert.False(list.Add(" a"));
        Assert.True(list.Add("A"));
        Assert.Equal(new[] { "a", "A" }, list.Rules);

        var folded = FlagHideList.Load(_dir, _mod, true);
        Assert.True(folded.Add("a"));
        Assert.False(folded.Add("A"));
    }

    [Fact]
    public void IsHidden_MatchesLikeSearch()
    {
        var list = FlagHideList.Load(_dir, _mod, false);
        list.Add("iv2_nat_idea_select_*");
        list.Add("exact.name");

        Assert.True(list.IsHidden("iv2_nat_idea_select_adm_105_1"));
        Assert.False(list.IsHidden("x_iv2_nat_idea_select_adm"));
        Assert.True(list.IsHidden("exact.name"));
        Assert.False(list.IsHidden("exactXname"));
        Assert.False(list.IsHidden("exact.name2"));
        Assert.False(list.IsHidden("EXACT.NAME"));

        var folded = FlagHideList.Load(_dir, _mod, true);
        folded.Add("exact.name");
        Assert.True(folded.IsHidden("EXACT.NAME"));
    }

    [Fact]
    public void RemoveMatching_RemovesEveryRuleThatMatchesTheName()
    {
        var list = FlagHideList.Load(_dir, _mod, false);
        list.Add("iv2_*");
        list.Add("iv2_nat_*");
        list.Add("iv2_nat_idea_select_adm_105_1");
        list.Add("other_*");

        Assert.Equal(3, list.RemoveMatching("iv2_nat_idea_select_adm_105_1"));
        Assert.Equal(new[] { "other_*" }, list.Rules);
        Assert.False(list.IsHidden("iv2_nat_idea_select_adm_105_1"));
        Assert.Equal(0, list.RemoveMatching("nothing"));
    }

    [Fact]
    public void Remove_RemovesOneRule()
    {
        var list = FlagHideList.Load(_dir, _mod, false);
        list.Add("a_*");
        list.Add("b");

        Assert.True(list.Remove("a_*"));
        Assert.False(list.Remove("a_*"));
        Assert.False(list.IsHidden("a_x"));
        Assert.Equal(new[] { "b" }, list.Rules);
    }

    [Theory]
    [InlineData("iv2_nat_idea_select_adm_105_1", "iv2_nat_idea_select_adm_*_*")]
    [InlineData("abc", "abc")]
    [InlineData("var12345", "var12345")]
    [InlineData("adm2x_3", "adm2x_*")]
    [InlineData("a.1.2_b", "a.*.*_b")]
    [InlineData("x_1_2", "x_*_*")]
    [InlineData("x__1", "x__*")]
    [InlineData("1_2", "*_*")]
    [InlineData("42", "*")]
    [InlineData("x_1*2", "x_1*2")]
    [InlineData("x_*_1", "x_*_*")]
    public void SuggestPattern_ReplacesDigitRuns(string name, string expected)
    {
        Assert.Equal(expected, FlagHideList.SuggestPattern(name));
    }
}
