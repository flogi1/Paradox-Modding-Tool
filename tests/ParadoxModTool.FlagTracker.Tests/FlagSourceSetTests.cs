using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public sealed class FlagSourceSetTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-flagsrc-" + Guid.NewGuid().ToString("N"));
    private static readonly GameProfile Ck3 = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "ck3.json"));

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

    private string Make(string root, params string[] files)
    {
        var dir = Path.Combine(_temp, root);
        Directory.CreateDirectory(dir);
        foreach (var file in files)
        {
            var path = Path.Combine(dir, file.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "x = 1");
        }

        return dir;
    }

    private static List<string> Names(FlagSourceSet set) =>
        set.EnumerateFiles().Select(f => f.Root.Name + ":" + f.RelativePath).OrderBy(s => s, StringComparer.Ordinal).ToList();

    [Fact]
    public void Playset_ModHidesVanillaSamePath()
    {
        var vanilla = Make("v", "common/a.txt");
        var mod = Make("m", "common/a.txt");
        var set = FlagSourceSet.ForPlayset(Ck3, new[] { new ModSource("M", mod, false, 0) }, mod, new[] { vanilla });

        Assert.Equal(new[] { "M:common/a.txt" }, Names(set));
    }

    [Fact]
    public void Playset_LaterModHidesEarlierSamePath()
    {
        var vanilla = Make("v");
        var m1 = Make("m1", "common/a.txt");
        var m2 = Make("m2", "COMMON/A.txt");
        var set = FlagSourceSet.ForPlayset(Ck3, new[] { new ModSource("M1", m1, false, 0), new ModSource("M2", m2, false, 1) }, m1, new[] { vanilla });

        Assert.Equal(new[] { "M2:COMMON/A.txt" }, Names(set));
    }

    [Fact]
    public void Playset_ReplacePathHidesVanillaFolder()
    {
        var vanilla = Make("v", "common/x/a.txt", "common/x/sub/b.txt", "common/y/c.txt");
        var mod = Make("m", "common/z/d.txt");
        File.WriteAllText(Path.Combine(mod, "descriptor.mod"), "name = \"M\"\nreplace_path = \"common/x\"\n");
        var set = FlagSourceSet.ForPlayset(Ck3, new[] { new ModSource("M", mod, false, 0) }, mod, new[] { vanilla });

        Assert.Equal(new[] { "M:common/z/d.txt", "Vanilla:common/x/sub/b.txt", "Vanilla:common/y/c.txt" }, Names(set));
    }

    [Fact]
    public void AllInstalled_HidesNothing()
    {
        var vanilla = Make("v", "common/x/a.txt", "common/x/sub/b.txt", "common/a.txt");
        var mod = Make("m", "common/z/d.txt", "common/a.txt");
        File.WriteAllText(Path.Combine(mod, "descriptor.mod"), "name = \"M\"\nreplace_path = \"common/x\"\n");
        var set = FlagSourceSet.ForAllInstalled(Ck3, new[] { new ModSource("M", mod, false, 0) }, mod, new[] { vanilla });

        Assert.Equal(5, Names(set).Count);
        Assert.All(set.Roots, r => Assert.Equal(0, r.Rank));
        Assert.False(set.IsPlayset);
    }

    [Fact]
    public void ActiveModNotInPlayset_StillIncluded()
    {
        var vanilla = Make("v", "common/a.txt");
        var other = Make("o", "common/b.txt");
        var active = Make("a", "common/a.txt");
        var set = FlagSourceSet.ForPlayset(Ck3,
            new[] { new ModSource("O", other, false, 0), new ModSource("A", active, false, -1) }, active, new[] { vanilla });

        var root = Assert.Single(set.Roots, r => r.Origin == FlagOrigin.ActiveMod);
        Assert.Equal(ErrorLogSources.Key(active), ErrorLogSources.Key(root.Directory));
        Assert.Equal(set.Roots.Max(r => r.Rank), root.Rank);
        Assert.Equal(3, set.Roots.Count);
        Assert.Equal(new[] { "A:common/a.txt", "O:common/b.txt" }, Names(set));
    }

    [Fact]
    public void ActiveModMissingFromMods_IsRoot()
    {
        var vanilla = Make("v");
        var active = Make("a", "common/a.txt");
        var set = FlagSourceSet.ForPlayset(Ck3, Array.Empty<ModSource>(), active, new[] { vanilla });

        Assert.Equal(FlagOrigin.ActiveMod, set.Roots.Single(r => ErrorLogSources.Key(r.Directory) == ErrorLogSources.Key(active)).Origin);
    }

    [Fact]
    public void ScopeKey_ChangesWithRoots()
    {
        var vanilla = Make("v");
        var m1 = Make("m1");
        var m2 = Make("m2");
        var a = new ModSource("M1", m1, false, 0);
        var b = new ModSource("M2", m2, false, 1);
        var one = FlagSourceSet.ForPlayset(Ck3, new[] { a }, m1, new[] { vanilla });
        var two = FlagSourceSet.ForPlayset(Ck3, new[] { a, b }, m1, new[] { vanilla });
        var twoSwapped = FlagSourceSet.ForPlayset(Ck3, new[] { b, a }, m1, new[] { vanilla });
        var all = FlagSourceSet.ForAllInstalled(Ck3, new[] { a, b }, m1, new[] { vanilla });

        Assert.NotEqual(one.ScopeKey, two.ScopeKey);
        Assert.Equal(two.ScopeKey, twoSwapped.ScopeKey);
        Assert.NotEqual(two.ScopeKey, all.ScopeKey);
        Assert.Equal(16, one.ScopeKey.Length);
    }

    private static FlagScannedFile Scanned(FlagSourceSet set, string rootName, string rel, string def)
    {
        var root = set.Roots.Single(r => r.Name == rootName);
        var scan = new FileScan();
        scan.Definitions.Add(new DefinitionInfo(def, false, 1));
        return new FlagScannedFile(new SiteLocation(root.Directory, rel, 0), root.Origin, root.Name, scan);
    }

    [Fact]
    public void EffectiveDefinition_LastWinsByRank()
    {
        var vanilla = Make("v", "common/scripted_effects/a.txt");
        var mod = Make("m", "common/scripted_effects/b.txt");
        var set = FlagSourceSet.ForPlayset(Ck3, new[] { new ModSource("M", mod, false, 0) }, mod, new[] { vanilla });
        var v = Scanned(set, "Vanilla", "common/scripted_effects/a.txt", "eff");
        var m = Scanned(set, "M", "common/scripted_effects/b.txt", "eff");

        var effective = set.EffectiveDefinitions(new[] { v, m });

        Assert.NotNull(effective);
        Assert.Same(m, effective![new DefinitionKey("eff", false)]);
    }

    [Fact]
    public void EffectiveDefinitions_AllInstalled_IsNull()
    {
        var vanilla = Make("v");
        var set = FlagSourceSet.ForAllInstalled(Ck3, Array.Empty<ModSource>(), vanilla, new[] { vanilla });

        Assert.Null(set.EffectiveDefinitions(Array.Empty<FlagScannedFile>()));
    }

    [Fact]
    public void OwnActiveModOutsidePlayset_RanksAboveAll()
    {
        var vanilla = Make("v", "common/scripted_effects/a.txt", "common/a.txt", "common/x/h.txt");
        var other = Make("o", "common/o.txt");
        var active = Make("a", "common/a.txt", "common/scripted_effects/b.txt");
        File.WriteAllText(Path.Combine(active, "descriptor.mod"), "name = \"A\"\nreplace_path = \"common/x\"\n");
        var set = FlagSourceSet.ForPlayset(Ck3,
            new[] { new ModSource("O", other, false, 0), new ModSource("A", active, true, -1) }, active, new[] { vanilla });

        var root = Assert.Single(set.Roots, r => r.Origin == FlagOrigin.ActiveMod);
        Assert.Equal(2, root.Rank);
        Assert.Equal(new[] { "A:common/a.txt", "A:common/scripted_effects/b.txt", "O:common/o.txt", "Vanilla:common/scripted_effects/a.txt" }, Names(set));
        var v = Scanned(set, "Vanilla", "common/scripted_effects/a.txt", "eff");
        var m = Scanned(set, "A", "common/scripted_effects/b.txt", "eff");
        Assert.Same(m, set.EffectiveDefinitions(new[] { v, m })![new DefinitionKey("eff", false)]);
        Assert.Same(m, set.EffectiveDefinitions(new[] { m, v })![new DefinitionKey("eff", false)]);
    }

    [Fact]
    public void OtherOwnModOutsidePlayset_KeepsRankZero()
    {
        var vanilla = Make("v");
        var own = Make("own");
        var active = Make("a");
        var set = FlagSourceSet.ForPlayset(Ck3, new[] { new ModSource("Own", own, true, -1) }, active, new[] { vanilla });

        Assert.Equal(0, set.Roots.Single(r => r.Name == "Own").Rank);
        Assert.Equal(1, set.Roots.Single(r => r.Origin == FlagOrigin.ActiveMod).Rank);
    }

    [Theory]
    [InlineData("events", true)]
    [InlineData("common/scripted_effects", false)]
    public void EffectiveDefinition_EqualRank_PathOrderBreaksTie(string folder, bool firstWins)
    {
        var v1 = Make("v1", folder + "/a.txt");
        var v2 = Make("v2", folder + "/b.txt");
        var set = FlagSourceSet.ForPlayset(Ck3, Array.Empty<ModSource>(), string.Empty, new[] { v1, v2 });
        var scan = new FileScan();
        scan.Definitions.Add(new DefinitionInfo("eff", false, 1));
        var a = new FlagScannedFile(new SiteLocation(v1, folder + "/a.txt", 0), FlagOrigin.Vanilla, "Vanilla", scan);
        var b = new FlagScannedFile(new SiteLocation(v2, folder + "/b.txt", 0), FlagOrigin.Vanilla, "Vanilla", scan);
        var expected = firstWins ? a : b;

        Assert.Same(expected, set.EffectiveDefinitions(new[] { a, b })![new DefinitionKey("eff", false)]);
        Assert.Same(expected, set.EffectiveDefinitions(new[] { b, a })![new DefinitionKey("eff", false)]);
    }

    [Fact]
    public void EmptyActiveModDirectory_MeansNoActiveMod()
    {
        var vanilla = Make("v");
        var mod = Make("m");
        var playset = FlagSourceSet.ForPlayset(Ck3, new[] { new ModSource("M", mod, false, 0) }, string.Empty, new[] { vanilla });
        var all = FlagSourceSet.ForAllInstalled(Ck3, new[] { new ModSource("M", mod, false, 0) }, string.Empty, new[] { vanilla });

        Assert.DoesNotContain(playset.Roots, r => r.Origin == FlagOrigin.ActiveMod);
        Assert.DoesNotContain(all.Roots, r => r.Origin == FlagOrigin.ActiveMod);
        Assert.Equal(2, playset.Roots.Count);
    }

    [Fact]
    public void LocalizationYml_OnlyUnderLocalizationSegment()
    {
        var vanilla = Make("v", "localization/english/a_l_english.yml", "common/b.yml", "gui/c.gui", "common/d.txt", "common/e.png");
        var vanillaOnly = FlagSourceSet.ForAllInstalled(Ck3, Array.Empty<ModSource>(), Path.Combine(_temp, "none"), new[] { vanilla });

        Assert.Equal(new[] { "Vanilla:common/d.txt", "Vanilla:gui/c.gui", "Vanilla:localization/english/a_l_english.yml" }, Names(vanillaOnly));
    }

    [Fact]
    public void LocalisationEnglishOnly()
    {
        var vanilla = Make("v",
            "localization/english/a_l_english.yml",
            "localisation/b_l_english.yml",
            "localization/german/a_l_german.yml",
            "localisation/c_l_french.yml",
            "sub/LOCALISATION/replace/English/d.yml",
            "english/e_l_english.yml");
        var vanillaOnly = FlagSourceSet.ForAllInstalled(Ck3, Array.Empty<ModSource>(), Path.Combine(_temp, "none"), new[] { vanilla });

        Assert.Equal(
            new[] { "Vanilla:localisation/b_l_english.yml", "Vanilla:localization/english/a_l_english.yml", "Vanilla:sub/LOCALISATION/replace/English/d.yml" },
            Names(vanillaOnly));
    }

    [Fact]
    public void Localisation_RootWithoutEnglish_AllLanguagesCount()
    {
        var vanilla = Make("v", "localization/english/a_l_english.yml", "localization/german/a_l_german.yml");
        var mod = Make("m", "localisation/german/x_l_german.yml", "localization/y_l_german.yml", "common/z.yml", "common/w.txt");
        var set = FlagSourceSet.ForPlayset(Ck3, new[] { new ModSource("M", mod, false, 0) }, mod, new[] { vanilla });

        Assert.Equal(
            new[] { "M:common/w.txt", "M:localisation/german/x_l_german.yml", "M:localization/y_l_german.yml", "Vanilla:localization/english/a_l_english.yml" },
            Names(set));
    }

    [Fact]
    public void Playset_NoOtherRoots_ActiveModRankZero()
    {
        var active = Make("a", "common/a.txt");
        var set = FlagSourceSet.ForPlayset(Ck3, Array.Empty<ModSource>(), active, Array.Empty<string>());

        var root = Assert.Single(set.Roots);
        Assert.Equal(FlagOrigin.ActiveMod, root.Origin);
        Assert.Equal(0, root.Rank);
    }

    [Fact]
    public void EnumerateFilesWithGfx()
    {
        var vanilla = Make("v", "a.txt", "b.gfx", "localization/english/c_l_english.yml");
        var set = FlagSourceSet.ForAllInstalled(Ck3, Array.Empty<ModSource>(), Make("m"), new[] { vanilla });

        var files = set.EnumerateFiles(new[] { ".txt", ".gui", ".gfx" }).Select(f => f.RelativePath).OrderBy(s => s, StringComparer.Ordinal).ToList();

        Assert.Equal(new[] { "a.txt", "b.gfx" }, files);
    }

    [Fact]
    public void WinsFollowsLastWins()
    {
        var vanilla = Make("v", "common/scripted_effects/x.txt");
        var m1 = Make("m1", "common/scripted_effects/x.txt");
        var m2 = Make("m2", "common/scripted_effects/x.txt");
        var set = FlagSourceSet.ForPlayset(Ck3, new[] { new ModSource("M1", m1, false, 0), new ModSource("M2", m2, false, 1) }, m1, new[] { vanilla });
        var low = new SiteLocation(m1, "common/scripted_effects/x.txt", 1);
        var high = new SiteLocation(m2, "common/scripted_effects/x.txt", 1);

        Assert.True(set.Wins(high, low));
        Assert.False(set.Wins(low, high));
    }

    [Fact]
    public void WinsSameFileFirstLine()
    {
        var vanilla = Make("v", "common/scripted_effects/x.txt");
        var set = FlagSourceSet.ForPlayset(Ck3, Array.Empty<ModSource>(), vanilla, new[] { vanilla });
        var early = new SiteLocation(vanilla, "common/scripted_effects/x.txt", 3);
        var late = new SiteLocation(vanilla, "COMMON/scripted_effects/x.txt", 9);

        Assert.True(set.Wins(early, late));
        Assert.False(set.Wins(late, early));
    }
}
