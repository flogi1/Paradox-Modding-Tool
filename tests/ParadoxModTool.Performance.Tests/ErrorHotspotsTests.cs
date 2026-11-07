using Xunit;
using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.Performance.Tests;

public class ErrorHotspotsTests
{
    private static readonly ErrorCatalog Catalog = ErrorCatalog.Load(RepoFiles.PathOf("profiles/errorlog/eu5.json"));

    private static ErrorHotspots LoadFrom(params string[] lines)
    {
        using var tree = new TempTree(("error.log", string.Join("\n", lines) + "\n"));
        return ErrorHotspots.Load(tree.PathOf("error.log"), Catalog);
    }

    [Fact]
    public void LoadMatchesCatalogEntriesByFileAndLine()
    {
        var hotspots = LoadFrom(
            "[19:49:44][jomini_effect.cpp:556]: Unknown effect cmf_show_alert at common/scripted_effects/01_iv2_alert_effects.txt:47");

        Assert.Equal(1, hotspots.Total);
        Assert.Equal(1, hotspots.CountAt("common/scripted_effects/01_iv2_alert_effects.txt", 47));
        Assert.Equal(1, hotspots.CountAt(@"Common\Scripted_Effects\01_iv2_alert_effects.txt", 47));
        Assert.Equal(1, hotspots.CountInFile("common/scripted_effects/01_iv2_alert_effects.txt"));
        Assert.Single(hotspots.At("common/scripted_effects/01_iv2_alert_effects.txt", 47));
    }

    [Fact]
    public void LoadUsesFallbackForUnmatchedEntries()
    {
        var line = "[19:49:44][foo.cpp:1]: Something nobody catalogued happened at common/x.txt:12";
        Assert.Null(Catalog.Match("Something nobody catalogued happened at common/x.txt:12", out _));

        var hotspots = LoadFrom(line);

        Assert.Equal(1, hotspots.CountAt("common/x.txt", 12));
        Assert.Equal(1, hotspots.Total);
    }

    [Fact]
    public void CountIgnoresOtherFilesWithSameLine()
    {
        var hotspots = LoadFrom(
            "[19:49:44][jomini_effect.cpp:556]: Unknown effect a at common/scripted_effects/one.txt:47",
            "[19:49:45][jomini_effect.cpp:556]: Unknown effect b at common/scripted_effects/two.txt:47",
            "[19:49:46][jomini_effect.cpp:556]: Unknown effect c at common/scripted_effects/two.txt:48");

        Assert.Equal(3, hotspots.Total);
        Assert.Equal(1, hotspots.CountAt("common/scripted_effects/one.txt", 47));
        Assert.Equal(1, hotspots.CountAt("common/scripted_effects/two.txt", 47));
        Assert.Equal(2, hotspots.CountInFile("common/scripted_effects/two.txt"));
        Assert.Equal(0, hotspots.CountAt("common/scripted_effects/three.txt", 47));
        Assert.Empty(hotspots.At("common/scripted_effects/three.txt", 47));
    }

    [Fact]
    public void MostCommonMessageAtPicksTheFrequentOne()
    {
        var hotspots = LoadFrom(
            "[19:49:44][jomini_effect.cpp:556]: Unknown effect a at common/scripted_effects/one.txt:47",
            "[19:49:45][jomini_effect.cpp:556]: Unknown effect b at common/scripted_effects/one.txt:47",
            "[19:49:46][jomini_effect.cpp:556]: Unknown effect b at common/scripted_effects/one.txt:47");

        Assert.Equal(
            "Unknown effect b at common/scripted_effects/one.txt:47",
            hotspots.MostCommonMessageAt("common/scripted_effects/one.txt", 47));
        Assert.Null(hotspots.MostCommonMessageAt("common/scripted_effects/one.txt", 99));
    }

    [Fact]
    public void PlacesListsEveryIndexedPlaceWithItsCount()
    {
        var hotspots = LoadFrom(
            "[19:49:44][jomini_effect.cpp:556]: Unknown effect a at common/scripted_effects/one.txt:47",
            "[19:49:45][jomini_effect.cpp:556]: Unknown effect b at common/scripted_effects/one.txt:47",
            "[19:49:46][jomini_effect.cpp:556]: Unknown effect c at common/scripted_effects/two.txt:3");

        Assert.Equal(
            new[] { ("common/scripted_effects/one.txt", 47, 2), ("common/scripted_effects/two.txt", 3, 1) },
            hotspots.Places().OrderBy(p => p.Path, StringComparer.Ordinal));
    }

    [Fact]
    public void FromEntriesMatchesLoad()
    {
        string[] lines =
        {
            "[19:49:44][jomini_effect.cpp:556]: Unknown effect a at common/scripted_effects/one.txt:47",
            "[19:49:45][jomini_effect.cpp:556]: Unknown effect b at common/scripted_effects/one.txt:47",
            "[19:49:46][jomini_effect.cpp:556]: Unknown effect c at common/scripted_effects/two.txt:3",
            "[19:49:47][foo.cpp:1]: Something nobody catalogued happened at common/x.txt:12",
            "[19:49:48][foo.cpp:1]: Nothing to place here",
        };
        var loaded = LoadFrom(lines);

        var fromEntries = ErrorHotspots.FromEntries(ErrorClassifier.Classify(LogReader.Parse(lines), Catalog));

        Assert.Equal(loaded.Total, fromEntries.Total);
        Assert.Equal(4, fromEntries.Total);
        Assert.Equal(
            loaded.Places().OrderBy(p => p.Path, StringComparer.Ordinal).ThenBy(p => p.Line),
            fromEntries.Places().OrderBy(p => p.Path, StringComparer.Ordinal).ThenBy(p => p.Line));
        Assert.Equal(1, fromEntries.CountAt("common/x.txt", 12));
        Assert.Equal(loaded.MostCommonMessageAt("common/scripted_effects/one.txt", 47), fromEntries.MostCommonMessageAt("common/scripted_effects/one.txt", 47));
    }

    [Fact]
    public void ModSideOfSessionCountsUncataloguedModSpamButNotVanilla()
    {
        const string ModLine = "[12:00:00][x.cpp:1]: Something odd happened at common/scripted_effects/iv2_x.txt:7";
        const string VanillaLine = "[12:00:01][x.cpp:1]: Something odd happened at common/scripted_effects/vanilla_only.txt:7";
        const string CataloguedVanilla = "[12:00:02][jomini_effect.cpp:556]: Unknown effect a at common/scripted_effects/vanilla_only.txt:3";
        const string CataloguedMod = "[12:00:03][jomini_effect.cpp:556]: Unknown effect b at common/scripted_effects/iv2_x.txt:2";
        Assert.Null(Catalog.Match("Something odd happened at common/scripted_effects/iv2_x.txt:7", out _));
        using var tree = new TempTree(
            ("mod/common/scripted_effects/iv2_x.txt", "iv2_x = { }\n"),
            ("vanilla/game/in_game/common/scripted_effects/vanilla_only.txt", "vanilla_only = { }\n"));
        var session = new ErrorLogSession(Catalog, new SuppressionSet(), null, "de");
        session.SetSources(new[] { new ModSource("Mod", tree.PathOf("mod"), IsOwn: true, LoadOrder: 0) }, new[] { tree.PathOf("vanilla/game") });
        session.LoadRun("error.log", LogReader.Parse(new[] { ModLine, ModLine, VanillaLine, CataloguedVanilla, CataloguedMod }), DateTime.UtcNow.AddDays(1));

        var hotspots = ErrorHotspots.ModSideOf(session);

        Assert.Equal(2, hotspots.CountAt("common/scripted_effects/iv2_x.txt", 7));
        Assert.Equal(1, hotspots.CountAt("common/scripted_effects/iv2_x.txt", 2));
        Assert.Equal(0, hotspots.CountInFile("common/scripted_effects/vanilla_only.txt"));
        Assert.Equal(3, hotspots.Total);
    }

    [Fact]
    public void TryFallbackPlaceReadsAtFileLine()
    {
        Assert.True(ErrorHotspots.TryFallbackPlace("Something odd happened at common/x.txt:12", out var path, out var line));
        Assert.Equal(("common/x.txt", 12), (path, line));
        Assert.False(ErrorHotspots.TryFallbackPlace("Nothing to place here", out _, out _));
    }

    [Fact]
    public void EmptyHasNothing()
    {
        Assert.Empty(ErrorHotspots.Empty.Places());
        Assert.Equal(0, ErrorHotspots.Empty.Total);
        Assert.Equal(0, ErrorHotspots.Empty.CountAt("a.txt", 1));
        Assert.Null(ErrorHotspots.Empty.MostCommonMessageAt("a.txt", 1));
    }
}
