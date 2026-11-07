using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public class FlagIndexTests
{
    private const string Root = @"C:\root";

    private static FlagSite Site(
        string name,
        FlagRole role,
        FlagOrigin origin = FlagOrigin.ActiveMod,
        FlagKind kind = FlagKind.Variable,
        int line = 1,
        bool pattern = false) =>
        new(name, kind, kind == FlagKind.ScopeFlag ? "character_flag" : null, role, new SiteLocation(Root, "events/e.txt", line),
            origin, origin == FlagOrigin.Vanilla ? "Vanilla" : "My Mod", IsPattern: pattern);

    private static FlagIndex Index(params FlagSite[] sites) => new(sites, Array.Empty<string>(), DateTime.UtcNow, 1);

    [Fact]
    public void SitesByName_IsReadOnly()
    {
        var sites = Index(Site("a", FlagRole.Set), Site("a", FlagRole.Read, line: 2)).Search("a").SitesByName["a"];

        Assert.True(((ICollection<FlagSite>)sites).IsReadOnly);
        Assert.Throws<NotSupportedException>(() => ((ICollection<FlagSite>)sites).Clear());
        Assert.Equal(2, sites.Count);
    }

    [Fact]
    public void Kinds_AreFlagKindFamilyRecords()
    {
        var summary = Assert.Single(Index(
            Site("f", FlagRole.Set, kind: FlagKind.ScopeFlag),
            Site("f", FlagRole.Read, kind: FlagKind.ScopeFlag, line: 2),
            Site("f", FlagRole.Read, line: 3)).Search("f").Names);

        Assert.Equal(
            new[] { new FlagKindFamily(FlagKind.Variable, null), new FlagKindFamily(FlagKind.ScopeFlag, "character_flag") },
            summary.Kinds);
    }

    [Fact]
    public void Wildcard_FamilyGrouped()
    {
        var index = Index(
            Site("iv2_sel_1_1", FlagRole.Set),
            Site("iv2_sel_1_1", FlagRole.Read, line: 2),
            Site("iv2_sel_1_1", FlagRole.Read, line: 3),
            Site("iv2_sel_1_2", FlagRole.Set),
            Site("iv2_sel_1_2", FlagRole.Remove, line: 4),
            Site("other", FlagRole.Set));

        var result = index.Search("  iv2_sel_*  ");

        Assert.Equal("iv2_sel_*", result.Query);
        Assert.Equal(new[] { "iv2_sel_1_1", "iv2_sel_1_2" }, result.Names.Select(n => n.Name));
        var first = result.Names[0];
        Assert.Equal((1, 2, 0), (first.SetCount, first.ReadCount, first.RemoveCount));
        var second = result.Names[1];
        Assert.Equal((1, 0, 1), (second.SetCount, second.ReadCount, second.RemoveCount));
        Assert.Equal(3, result.SitesByName["iv2_sel_1_1"].Count);
        Assert.Equal(new[] { new FlagKindFamily(FlagKind.Variable, null) }, first.Kinds);
        Assert.False(result.Truncated);
        Assert.Empty(result.PatternSites);
    }

    [Fact]
    public void ExactQuery_NoWildcard()
    {
        var index = Index(Site("iv2_sel_1_1", FlagRole.Set), Site("iv2_sel_1_12", FlagRole.Set), Site("x_iv2_sel_1_1", FlagRole.Set));

        var result = index.Search("iv2_sel_1_1");

        Assert.Equal("iv2_sel_1_1", Assert.Single(result.Names).Name);
    }

    [Fact]
    public void Construction_WithManyPatternSites_IsFast()
    {
        var sites = Enumerable.Range(0, 3000).Select(i => Site($"pat_{i}_*", FlagRole.Set, pattern: true)).ToArray();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var index = Index(sites);
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), watch.Elapsed.ToString());
        Assert.Equal("pat_5_*", Assert.Single(index.Search("pat_5_*").PatternSites).Name);
    }

    [Fact]
    public void ManyStars_FinishFastAndMatchCorrectly()
    {
        var sites = Enumerable.Range(0, 3000)
            .Select(i => Site(string.Concat(Enumerable.Repeat("a_", 40 + i % 5)) + i, FlagRole.Set))
            .Append(Site("a_a_a_a_a_a_a_a_b", FlagRole.Set))
            .ToArray();
        var index = Index(sites);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = index.Search("*a*a*a*a*a*a*a*b");
        watch.Stop();

        Assert.Equal("a_a_a_a_a_a_a_a_b", Assert.Single(result.Names).Name);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), watch.Elapsed.ToString());
        Assert.Equal("xaab", Assert.Single(Index(Site("xaab", FlagRole.Set)).Search("**a**b").Names).Name);
    }

    [Fact]
    public void RegexCharsLiteral()
    {
        var index = Index(Site("axb", FlagRole.Set), Site("a.b", FlagRole.Set));

        Assert.Equal("a.b", Assert.Single(index.Search("a.b").Names).Name);
    }

    [Fact]
    public void CaseSensitive()
    {
        var index = Index(Site("Abc", FlagRole.Set));

        Assert.Empty(index.Search("abc").Names);
    }

    [Fact]
    public void StarAlone_TruncatesAt5000()
    {
        var sites = Enumerable.Range(0, 5001).Select(i => Site("n" + i.ToString("D5"), FlagRole.Set)).ToArray();

        var result = Index(sites).Search("*");

        Assert.Equal(5000, result.Names.Count);
        Assert.True(result.Truncated);
        Assert.Equal("n00000", result.Names[0].Name);
        Assert.Equal("n04999", result.Names[^1].Name);
        Assert.Equal(5000, result.SitesByName.Count);
        Assert.False(result.SitesByName.ContainsKey("n05000"));
    }

    [Fact]
    public void Empty_ReturnsNothing()
    {
        var index = Index(Site("a", FlagRole.Set), Site("a_*", FlagRole.Set, pattern: true));

        foreach (var query in new[] { "", "   " })
        {
            var result = index.Search(query);
            Assert.Empty(result.Names);
            Assert.Empty(result.SitesByName);
            Assert.Empty(result.PatternSites);
            Assert.False(result.Truncated);
        }
    }

    [Fact]
    public void Warnings_ListsOnlyActiveModNamesWithWarning()
    {
        var index = Index(
            Site("ok", FlagRole.Set), Site("ok", FlagRole.Read, line: 2),
            Site("never_read", FlagRole.Set),
            Site("never_set", FlagRole.Read),
            Site("vanilla_only", FlagRole.Set, FlagOrigin.Vanilla),
            Site("symbol", FlagRole.Read, kind: FlagKind.SymbolValue));

        var result = index.Warnings();

        Assert.Equal(new[] { "never_read", "never_set" }, result.Names.Select(n => n.Name));
        Assert.Equal(new[] { "never_read", "never_set" }, result.SitesByName.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Empty(result.PatternSites);
        Assert.False(result.Truncated);
        Assert.Equal(string.Empty, result.Query);
    }

    [Fact]
    public void Warnings_SkipAppliesBeforeTheCap()
    {
        // 5500 Namen mit Warnung (nur gesetzt); die ersten 1000 (w_0000..w_0999) übersprungen: 4500 sichtbar, kein Abschneiden.
        var sites = Enumerable.Range(0, 5500).Select(i => Site($"w_{i:D4}", FlagRole.Set, line: i + 1)).ToArray();
        var index = Index(sites);

        var result = index.Warnings(name => name.StartsWith("w_0", StringComparison.Ordinal));

        Assert.Equal(4500, result.Names.Count);
        Assert.Equal("w_1000", result.Names[0].Name);
        Assert.False(result.Truncated);
        Assert.Equal(1000, result.SkippedCount);
        Assert.True(index.Warnings().Truncated);
        Assert.Equal(0, index.Warnings().SkippedCount);
        Assert.Same(index.Warnings(), index.Warnings(null));
    }

    [Fact]
    public void Warnings_SkipCountsHiddenBeyondTheCap()
    {
        // 6000 Namen, übersprungen die letzten 1000: genau 5000 sichtbar, nicht abgeschnitten; dann nur 200 übersprungen: abgeschnitten, und die 200 hinter der Grenze zählen trotzdem.
        var sites = Enumerable.Range(0, 6000).Select(i => Site($"w_{i:D4}", FlagRole.Set, line: i + 1)).ToArray();

        var result = Index(sites).Warnings(name => string.CompareOrdinal(name, "w_5000") >= 0);

        Assert.Equal(5000, result.Names.Count);
        Assert.False(result.Truncated);
        Assert.Equal(1000, result.SkippedCount);

        var more = Index(sites).Warnings(name => string.CompareOrdinal(name, "w_5800") >= 0);
        Assert.Equal(5000, more.Names.Count);
        Assert.True(more.Truncated);
        Assert.Equal(200, more.SkippedCount);
    }

    [Fact]
    public void Warnings_TruncatesAt5000()
    {
        var sites = Enumerable.Range(0, FlagIndex.MaxNames + 1).Select(i => Site("n" + i.ToString("D5"), FlagRole.Set)).ToArray();

        var result = Index(sites).Warnings();

        Assert.Equal(FlagIndex.MaxNames, result.Names.Count);
        Assert.True(result.Truncated);
    }

    [Fact]
    public void NeverRead_OnlyForActiveMod()
    {
        var index = Index(Site("v_only", FlagRole.Set, FlagOrigin.Vanilla), Site("m_only", FlagRole.Set));

        var vanilla = Assert.Single(index.Search("v_only").Names);
        Assert.False(vanilla.InActiveMod);
        Assert.False(vanilla.NeverRead);
        Assert.False(vanilla.NeverSet);

        var mod = Assert.Single(index.Search("m_only").Names);
        Assert.True(mod.InActiveMod);
        Assert.True(mod.NeverRead);
        Assert.False(mod.NeverSet);
        Assert.False(mod.PatternMayCover);
    }

    [Fact]
    public void SymbolValue_NoWarning()
    {
        var index = Index(Site("sym", FlagRole.Read, kind: FlagKind.SymbolValue));

        var summary = Assert.Single(index.Search("sym").Names);
        Assert.True(summary.InActiveMod);
        Assert.False(summary.NeverSet);
        Assert.False(summary.NeverRead);
    }

    [Fact]
    public void PatternCoversNeverSet()
    {
        var pattern = Site("unlocked_policy_*", FlagRole.Set, FlagOrigin.Vanilla, pattern: true);
        var index = Index(Site("unlocked_policy_x", FlagRole.Read), pattern);

        var result = index.Search("unlocked_policy_x");

        var summary = Assert.Single(result.Names);
        Assert.True(summary.NeverSet);
        Assert.False(summary.NeverRead);
        Assert.True(summary.PatternMayCover);
        Assert.Equal(pattern, Assert.Single(result.PatternSites));
    }

    [Fact]
    public void AllWildcardPattern_NeverCoversAndMatchesOnlyStarQuery()
    {
        var star = Site("*", FlagRole.Set, FlagOrigin.Vanilla, pattern: true);
        var stars = Site("**", FlagRole.Set, FlagOrigin.Vanilla, line: 2, pattern: true);
        var index = Index(Site("abc", FlagRole.Read), star, stars);

        var result = index.Search("abc");

        var summary = Assert.Single(result.Names);
        Assert.True(summary.NeverSet);
        Assert.False(summary.PatternMayCover);
        Assert.Empty(result.PatternSites);
        Assert.Equal(new[] { star, stars }, index.Search("*").PatternSites);
    }

    [Fact]
    public void Kinds_FamilylessScopeFlagDroppedWhenFamilyKnown()
    {
        var locRead = new FlagSite("f", FlagKind.ScopeFlag, null, FlagRole.Read, new SiteLocation(Root, "localisation/english/l_english.yml", 4),
            FlagOrigin.ActiveMod, "My Mod");
        var summary = Assert.Single(Index(Site("f", FlagRole.Set, kind: FlagKind.ScopeFlag), locRead).Search("f").Names);
        Assert.Equal(new[] { new FlagKindFamily(FlagKind.ScopeFlag, "character_flag") }, summary.Kinds);
        Assert.Equal(1, summary.ReadCount);

        var alone = Assert.Single(Index(locRead).Search("f").Names);
        Assert.Equal(new[] { new FlagKindFamily(FlagKind.ScopeFlag, null) }, alone.Kinds);
        Assert.Equal(new[] { new FlagKindFamily(FlagKind.ScopeFlag, null) }, Assert.Single(Index(locRead).Warnings().Names).Kinds);
    }

    [Fact]
    public void PunctuationOnlyPattern_CountsAsAllWildcard()
    {
        var underscore = Site("*_*", FlagRole.Set, FlagOrigin.Vanilla, pattern: true);
        var dot = Site("*.*", FlagRole.Set, FlagOrigin.Vanilla, line: 2, pattern: true);
        var index = Index(Site("a_b", FlagRole.Read), Site("c.d", FlagRole.Read, line: 3), underscore, dot);

        var result = index.Search("a_b");
        var summary = Assert.Single(result.Names);
        Assert.True(summary.NeverSet);
        Assert.False(summary.PatternMayCover);
        Assert.Empty(result.PatternSites);

        Assert.All(index.Warnings().Names, n => Assert.False(n.PatternMayCover));
        Assert.Equal(new[] { underscore, dot }, index.Search("*").PatternSites);
    }

    [Fact]
    public void PatternOfOtherKind_DoesNotCover()
    {
        var index = Index(Site("x_a", FlagRole.Read), Site("x_*", FlagRole.Set, FlagOrigin.Vanilla, FlagKind.ScopeFlag, pattern: true));

        var summary = Assert.Single(index.Search("x_a").Names);
        Assert.True(summary.NeverSet);
        Assert.False(summary.PatternMayCover);
    }

    [Fact]
    public void PatternWithOtherRole_DoesNotCover()
    {
        var index = Index(Site("unlocked_policy_x", FlagRole.Read), Site("unlocked_policy_*", FlagRole.Read, pattern: true));

        var summary = Assert.Single(index.Search("unlocked_policy_x").Names);
        Assert.True(summary.NeverSet);
        Assert.False(summary.PatternMayCover);
    }

    [Fact]
    public void WildcardQuery_MatchesPatternTextLiterally()
    {
        var covered = Site("unlocked_policy_*", FlagRole.Set, pattern: true);
        var index = Index(covered, Site("*_policy_x", FlagRole.Set, pattern: true), Site("other_*", FlagRole.Set, pattern: true));

        var result = index.Search("unlocked_*");

        Assert.Equal(covered, Assert.Single(result.PatternSites));
        Assert.Empty(result.Names);
        Assert.Empty(index.Search("unlocked_policy_\\*").PatternSites);
    }

    [Fact]
    public void DefinitionFlagWithoutReader_NeverRead()
    {
        var index = Index(Site("my_trait_flag", FlagRole.Set, kind: FlagKind.DefinitionFlag));

        var summary = Assert.Single(index.Search("my_trait_flag").Names);
        Assert.True(summary.NeverRead);
        Assert.False(summary.NeverSet);
        Assert.Equal(new[] { new FlagKindFamily(FlagKind.DefinitionFlag, null) }, summary.Kinds);
    }

    [Fact]
    public void Properties_Kept()
    {
        var built = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        var index = new FlagIndex(new[] { Site("a", FlagRole.Set) }, new[] { @"C:\bad.txt" }, built, 7);

        Assert.Equal(new[] { @"C:\bad.txt" }, index.UnreadFiles);
        Assert.Equal(built, index.BuiltUtc);
        Assert.Equal(7, index.FileCount);
    }

    [Fact]
    public void IgnoreCase_GroupsAndShowsFirstSpelling()
    {
        var sites = new[] { Site("ab_FLAG", FlagRole.Read, line: 2), Site("AB_flag", FlagRole.Set) };
        var index = new FlagIndex(sites, Array.Empty<string>(), DateTime.UtcNow, 1, namesIgnoreCase: true);

        var result = index.Search("ab_*");
        var summary = Assert.Single(result.Names);
        Assert.Equal("AB_flag", summary.Name);
        Assert.Equal(1, summary.SetCount);
        Assert.Equal(1, summary.ReadCount);
        Assert.False(summary.NeverSet);
        Assert.False(summary.NeverRead);
        Assert.Empty(index.Warnings().Names);
        Assert.Equal(2, result.SitesByName["AB_flag"].Count);
        Assert.Equal(0, FlagTrackerView.From(result).IndexOfName("AB_flag"));

        var exact = new FlagIndex(sites, Array.Empty<string>(), DateTime.UtcNow, 1);
        Assert.Equal(new[] { "AB_flag", "ab_FLAG" }, exact.Search("*").Names.Select(n => n.Name));
        Assert.DoesNotContain(exact.Search("ab_*").Names, n => n.Name == "AB_flag");
    }

    [Fact]
    public void IgnoreCase_PatternCoversOtherSpelling()
    {
        var sites = new[] { Site("x_Foo", FlagRole.Set), Site("X_*", FlagRole.Read, pattern: true) };
        var index = new FlagIndex(sites, Array.Empty<string>(), DateTime.UtcNow, 1, namesIgnoreCase: true);

        var result = index.Search("x_foo");
        Assert.True(Assert.Single(result.Names).PatternMayCover);
        Assert.Single(result.PatternSites);
    }
}
