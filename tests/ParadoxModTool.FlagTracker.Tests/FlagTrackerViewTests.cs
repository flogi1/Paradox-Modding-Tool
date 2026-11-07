using System.Diagnostics;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public sealed class FlagTrackerViewTests
{
    private static readonly FlagKindFamily Var = new(FlagKind.Variable, null);

    private static FlagNameSummary Summary(string name, int set = 1, int read = 1, bool neverSet = false, bool neverRead = false, bool pattern = false, bool inActive = true)
        => new(name, new[] { Var }, set, read, 0, inActive, neverSet, neverRead, pattern);

    private static FlagSite Site(string name, FlagRole role, FlagOrigin origin, string path = "common/a.txt", int line = 1, string originName = "X",
        SiteLocation? via = null, string? viaName = null, bool pattern = false)
        => new(name, FlagKind.Variable, null, role, new SiteLocation("C:/root", path, line), origin, originName, via, viaName, pattern);

    private static FlagSearchResult Result(IReadOnlyList<FlagNameSummary> names, IEnumerable<FlagSite>? sites = null, IReadOnlyList<FlagSite>? patterns = null, bool truncated = false)
    {
        var byName = (sites ?? Array.Empty<FlagSite>()).GroupBy(s => s.Name).ToDictionary(g => g.Key, g => (IReadOnlyList<FlagSite>)g.ToList());
        return new FlagSearchResult("q", names, byName, patterns ?? Array.Empty<FlagSite>(), truncated);
    }

    [Fact]
    public void Overview_HintsFollowSummary()
    {
        var view = FlagTrackerView.From(Result(new[]
        {
            Summary("a", set: 0, neverSet: true),
            Summary("b", set: 0, neverSet: true, pattern: true),
            Summary("c", read: 0, neverRead: true),
            Summary("d"),
        }));

        Assert.Equal(new[] { "a", "b", "c", "d" }, view.Overview.Select(r => r.Name));
        Assert.Equal("nie gesetzt", view.Overview[0].Hint);
        Assert.True(view.Overview[0].HintIsWarning);
        Assert.Equal("evtl. über Muster gesetzt", view.Overview[1].Hint);
        Assert.False(view.Overview[1].HintIsWarning);
        Assert.Equal("nie gelesen", view.Overview[2].Hint);
        Assert.True(view.Overview[2].HintIsWarning);
        Assert.Null(view.Overview[3].Hint);
        Assert.False(view.Overview[3].HintIsWarning);
        Assert.Equal("Variable", view.Overview[3].KindText);
    }

    [Fact]
    public void Overview_BothMissing_JoinsHints()
    {
        var view = FlagTrackerView.From(Result(new[] { Summary("a", 0, 0, neverSet: true, neverRead: true) }));

        Assert.Equal("nie gesetzt; nie gelesen", view.Overview[0].Hint);
    }

    [Fact]
    public void Overview_NoActiveMod_NoHint()
    {
        var view = FlagTrackerView.From(Result(new[] { Summary("a", inActive: false) }));

        Assert.Null(view.Overview[0].Hint);
        Assert.False(view.Overview[0].HintIsWarning);
    }

    [Fact]
    public void Sites_GroupedAndOrdered()
    {
        var sites = new[]
        {
            Site("n", FlagRole.Remove, FlagOrigin.ActiveMod, originName: "Mine"),
            Site("n", FlagRole.Read, FlagOrigin.Vanilla, originName: "Vanilla"),
            Site("n", FlagRole.Read, FlagOrigin.OtherMod, "common/o.txt", 5, "Other Mod"),
            Site("n", FlagRole.Set, FlagOrigin.ActiveMod, originName: "Mine"),
        };

        var view = FlagTrackerView.From(Result(new[] { Summary("n") }, sites));

        Assert.Equal(5, view.Sites.Count);
        Assert.Equal(SiteRowKind.NameHeader, view.Sites[0].Kind);
        Assert.Equal("n (4)", view.Sites[0].Text);
        Assert.Equal(new[] { "Mod", "Anderer Mod", "Vanilla", "Mod" }, view.Sites.Skip(1).Select(r => r.OriginText));
        Assert.Equal(new[] { "gesetzt", "gelesen", "gelesen", "gelöscht" }, view.Sites.Skip(1).Select(r => r.RoleText));
        Assert.Equal("Other Mod / common/o.txt:5", view.Sites[2].FileText);
        Assert.Equal("common/a.txt:1", view.Sites[1].FileText);
        Assert.Null(view.Sites[1].ViaText);
    }

    [Fact]
    public void Sites_OrderedByPathThenLine()
    {
        var sites = new[]
        {
            Site("n", FlagRole.Set, FlagOrigin.Vanilla, "b.txt", 1),
            Site("n", FlagRole.Set, FlagOrigin.Vanilla, "A.txt", 9),
            Site("n", FlagRole.Set, FlagOrigin.Vanilla, "A.txt", 2),
        };

        var view = FlagTrackerView.From(Result(new[] { Summary("n") }, sites));

        Assert.Equal(new[] { "A.txt:2", "A.txt:9", "b.txt:1" }, view.Sites.Skip(1).Select(r => r.FileText));
    }

    [Fact]
    public void Sites_PatternGroupLast()
    {
        var patterns = new[] { Site("x_*", FlagRole.Set, FlagOrigin.ActiveMod, pattern: true) };
        var view = FlagTrackerView.From(Result(new[] { Summary("m"), Summary("n") },
            new[] { Site("m", FlagRole.Set, FlagOrigin.ActiveMod), Site("n", FlagRole.Read, FlagOrigin.ActiveMod) }, patterns));

        Assert.Equal(new[] { SiteRowKind.NameHeader, SiteRowKind.Site, SiteRowKind.NameHeader, SiteRowKind.Site, SiteRowKind.PatternHeader, SiteRowKind.Site },
            view.Sites.Select(r => r.Kind));
        Assert.Equal("über Muster (nicht sicher) (1)", view.Sites[4].Text);
        Assert.Equal("x_*", view.Sites[5].Text);
    }

    [Fact]
    public void Sites_NoPatternHeaderWithoutPatterns()
    {
        var view = FlagTrackerView.From(Result(new[] { Summary("m") }, new[] { Site("m", FlagRole.Set, FlagOrigin.ActiveMod) }));

        Assert.DoesNotContain(view.Sites, r => r.Kind == SiteRowKind.PatternHeader);
    }

    [Fact]
    public void Sites_ViaText()
    {
        var via = new SiteLocation("C:/root", "in_game/common/scripted_effects/country_effects.txt", 131);
        var view = FlagTrackerView.From(Result(new[] { Summary("n") }, new[] { Site("n", FlagRole.Set, FlagOrigin.ActiveMod, via: via, viaName: "unlock_policy_effect") }));

        Assert.Equal("über unlock_policy_effect – im Effekt country_effects.txt:131", view.Sites[1].ViaText);
    }

    [Fact]
    public void IndexOfName_FindsHeader()
    {
        var view = FlagTrackerView.From(Result(new[] { Summary("m"), Summary("n") },
            new[] { Site("m", FlagRole.Set, FlagOrigin.ActiveMod), Site("m", FlagRole.Read, FlagOrigin.ActiveMod), Site("n", FlagRole.Set, FlagOrigin.ActiveMod) }));

        Assert.Equal(0, view.IndexOfName("m"));
        Assert.Equal(3, view.IndexOfName("n"));
        Assert.Equal(-1, view.IndexOfName("zzz"));
        Assert.Equal(-1, FlagTrackerView.Empty.IndexOfName("m"));
    }

    [Fact]
    public void Truncated_IsCarried()
    {
        Assert.True(FlagTrackerView.From(Result(Array.Empty<FlagNameSummary>(), truncated: true)).Truncated);
        Assert.False(FlagTrackerView.Empty.Truncated);
        Assert.Empty(FlagTrackerView.Empty.Overview);
    }

    [Fact]
    public void From_98000Sites_UnderOneSecond()
    {
        FlagTrackerView.From(Result(new[] { Summary("warm") }, new[] { Site("warm", FlagRole.Set, FlagOrigin.ActiveMod) }));
        var names = new List<FlagNameSummary>();
        var byName = new Dictionary<string, IReadOnlyList<FlagSite>>();
        var count = 0;
        for (var i = 0; i < 5000; i++)
        {
            var name = "flag_" + i;
            names.Add(Summary(name));
            var list = new List<FlagSite>();
            var perName = i < 3000 ? 20 : 19;
            for (var j = 0; j < perName; j++)
            {
                list.Add(Site(name, (FlagRole)(j % 3), (FlagOrigin)(j % 3), "common/f" + (j % 7) + ".txt", 100 - j));
            }

            count += perName;
            byName[name] = list;
        }

        Assert.Equal(98000, count);
        var result = new FlagSearchResult("*", names, byName, Array.Empty<FlagSite>(), false);

        var watch = Stopwatch.StartNew();
        var view = FlagTrackerView.From(result);
        watch.Stop();

        Assert.Equal(5000 + 98000, view.Sites.Count);
        Assert.True(watch.ElapsedMilliseconds < 5000, $"{watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void SiteRows_WithEqualContent_AreDistinct()
    {
        var a = new SiteRow(SiteRowKind.Site, "x", null, "o", "r", "k", "f:1", null);
        var b = new SiteRow(SiteRowKind.Site, "x", null, "o", "r", "k", "f:1", null);

        Assert.NotEqual(a, b);
        Assert.Equal(a, a);
        Assert.Equal(2, new HashSet<SiteRow> { a, b }.Count);
    }

    private static FlagHideList Hide(params string[] rules)
    {
        var list = FlagHideList.Load(Path.Combine(Path.GetTempPath(), "pmt-flaghide-view-" + Guid.NewGuid().ToString("N")), "C:/mods/x", false);
        foreach (var rule in rules)
        {
            list.Add(rule);
        }

        return list;
    }

    [Fact]
    public void Hidden_MarksRowsMutesHintAndCounts()
    {
        var view = FlagTrackerView.From(Result(new[]
        {
            Summary("iv2_a_1", read: 0, neverRead: true),
            Summary("iv2_b"),
            Summary("other", read: 0, neverRead: true),
        }), Hide("iv2_*"));

        Assert.Equal(new[] { "iv2_a_1", "iv2_b", "other" }, view.Overview.Select(r => r.Name));
        Assert.True(view.Overview[0].Hidden);
        Assert.Equal("nie gelesen (ausgeblendet)", view.Overview[0].Hint);
        Assert.False(view.Overview[0].HintIsWarning);
        Assert.True(view.Overview[1].Hidden);
        Assert.Equal("ausgeblendet", view.Overview[1].Hint);
        Assert.False(view.Overview[1].HintIsWarning);
        Assert.False(view.Overview[2].Hidden);
        Assert.Equal("nie gelesen", view.Overview[2].Hint);
        Assert.True(view.Overview[2].HintIsWarning);
    }

    [Fact]
    public void Hidden_WithoutList_NothingHidden()
    {
        var view = FlagTrackerView.From(Result(new[] { Summary("a") }));

        Assert.False(view.Overview[0].Hidden);
    }

    [Fact]
    public void HiddenSuffix_Texts()
    {
        Assert.Equal("ausgeblendet", FlagTrackerTexts.HiddenSuffix(null));
        Assert.Equal("nie gesetzt (ausgeblendet)", FlagTrackerTexts.HiddenSuffix("nie gesetzt"));
    }
}
