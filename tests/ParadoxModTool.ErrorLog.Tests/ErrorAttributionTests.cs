using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class ErrorAttributionTests
{
    private static readonly DateTime RunTime = new(2026, 9, 22, 19, 49, 0, DateTimeKind.Utc);

    [Fact]
    public void AttributesByFileByNameOrAsEngineUnassignedAndUnmatched()
    {
        using var tree = new TempTree(
            ("own/in_game/common/scripted_effects/purger.txt", "purger = {\n\tcmf_suppress = { v = iv2_x }\n\tset_variable = { name = iv2_x }\n}\n"),
            ("other/in_game/common/scripted_effects/o.txt", "o = { set_variable = { name = other_var } }\n"),
            ("install/game/in_game/common/governments/g.txt", "themata_bureaucracy = { }\n"));
        var own = new ModSource("Own", tree.PathOf("own"), IsOwn: true, LoadOrder: 1);
        var other = new ModSource("Other", tree.PathOf("other"), IsOwn: false, LoadOrder: 0);
        var resolver = new SourceResolver(new[] { own, other }, SourceResolver.VanillaRootsFor(tree.PathOf("install")), RunTime, lastWriteUtc: _ => RunTime);
        var index = SymbolIndex.Build(new[]
        {
            new SymbolRoot(own.Directory, own, true),
            new SymbolRoot(other.Directory, other, true),
            new SymbolRoot(tree.PathOf("install/game"), null, false),
        });

        var entries = Catalogs.Classify(
            "[19:49:44][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/purger.txt:2",
            "[19:49:53][jomini_effect.cpp:1162]: Variable 'iv2_x' is set but is never used. Note that use in localization doesn't count",
            "[19:49:53][jomini_effect.cpp:1162]: Variable 'other_var' is set but is never used. Note that use in localization doesn't count",
            "[16:59:33][government.cpp:3751]: Removing invalid bureaucracy 'themata_bureaucracy' for 'TRE Trebizond' 40 at game start - might want to look into this",
            "[19:49:53][jomini_effect.cpp:1162]: Variable 'nowhere_var' is set but is never used. Note that use in localization doesn't count",
            "[17:00:05][game_gui_types.cpp:1540]: couldn't convert parameter value to something we recognise",
            "[10:00:00][x.cpp:1]: Error building from string ( TYPE = NCoat )",
            "[19:49:44][jomini_effect.cpp:556]: Unknown effect x at common/scripted_effects/missing.txt:1");

        var attributions = entries.Select(e => ErrorAttributor.Attribute(e, resolver, index)).ToList();

        Assert.Equal(
            new[] { ErrorBucket.OwnMod, ErrorBucket.OwnMod, ErrorBucket.OtherMod, ErrorBucket.Vanilla, ErrorBucket.Unassigned, ErrorBucket.Engine, ErrorBucket.Unmatched, ErrorBucket.Unassigned },
            attributions.Select(a => a.Bucket));
        Assert.Same(own, attributions[0].Mod);
        Assert.Equal(tree.PathOf("own/in_game/common/scripted_effects/purger.txt"), attributions[0].Source!.FullPath);
        Assert.Same(own, attributions[1].Mod);
        Assert.Null(attributions[1].Source);
        Assert.Same(other, attributions[2].Mod);
    }

    [Fact]
    public void GroupsByBucketModCodeAndName()
    {
        var own = new ModSource("Own", @"C:\own", true, 0);
        var entries = Catalogs.Classify(
            "[01:00:00][a.cpp:1]: Unknown effect cmf_suppress at common/a.txt:1",
            "[01:00:00][a.cpp:1]: Unknown effect cmf_suppress at common/a.txt:2",
            "[01:00:00][a.cpp:1]: Unknown effect other at common/a.txt:3",
            "[01:00:00][b.cpp:1]: Error: \"x\" in file: \"common/b.txt\" near line: 4",
            "[01:00:00][b.cpp:1]: Error: \"y\" in file: \"common/b.txt\" near line: 9");

        var groups = ErrorGrouping.Group(entries, _ => new ErrorAttribution(ErrorBucket.OwnMod, own, null));

        Assert.Equal(3, groups.Count);
        var first = groups[0];
        Assert.Equal("EFF-001", first.Code);
        Assert.Equal("cmf_suppress", first.GroupName);
        Assert.Equal(2, first.Entries.Count);
        Assert.Contains(groups, g => g.Code == "PAR-001" && g.GroupName == "common/b.txt" && g.Entries.Count == 2);
    }

    [Fact]
    public void GroupsAreOrderedByBucketThenSize()
    {
        var entries = Catalogs.Classify(
            "[01:00:00][a.cpp:1]: Unknown effect a at common/a.txt:1",
            "[01:00:00][a.cpp:1]: Unknown effect b at common/a.txt:1",
            "[01:00:00][a.cpp:1]: Unknown effect b at common/a.txt:2");

        var groups = ErrorGrouping.Group(entries, e => new ErrorAttribution(e.Name == "a" ? ErrorBucket.OwnMod : ErrorBucket.Vanilla, null, null));

        Assert.Equal(new[] { "a", "b" }, groups.Select(g => g.GroupName));
    }
}
