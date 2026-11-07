using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public sealed class FlagIndexBuilderTests : IDisposable
{
    private static readonly GameProfile Ck3 = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "ck3.json"));
    private static readonly FlagCommandTable Table = FlagCommandTable.Ck3(Array.Empty<string>());

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-flagidx-" + Guid.NewGuid().ToString("N"));
    private readonly string _vanilla;
    private readonly string _mod;

    public FlagIndexBuilderTests()
    {
        _vanilla = Path.Combine(_temp, "v");
        _mod = Path.Combine(_temp, "m");
        Directory.CreateDirectory(_vanilla);
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

    private string Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private FlagIndexBuilder Builder() =>
        new(FlagSourceSet.ForPlayset(Ck3, new[] { new ModSource("My Mod", _mod, false, 0) }, _mod, new[] { _vanilla }), Table);

    private void WriteEndToEndFixture()
    {
        Write(_vanilla, "common/scripted_triggers/t.txt", "has_a_trigger = {\n\thas_variable = a\n}");
        Write(_vanilla, "common/scripted_effects/fx.txt", "set_p_effect = {\n\tset_variable = p_$P$\n}");
        Write(_mod, "events/e.txt", "e.1 = {\n\timmediate = {\n\t\tset_variable = a\n\t\tset_p_effect = { P = foo }\n\t}\n}");
    }

    [Fact]
    public void Build_EndToEnd()
    {
        WriteEndToEndFixture();

        var index = Builder().Build();

        var a = index.Search("a");
        var sites = a.SitesByName["a"];
        var set = Assert.Single(sites, s => s.Role == FlagRole.Set);
        Assert.Equal(FlagOrigin.ActiveMod, set.Origin);
        Assert.Equal("My Mod", set.OriginName);
        Assert.Equal(new SiteLocation(_mod, "events/e.txt", 3), set.Location);
        var read = Assert.Single(sites, s => s.Role == FlagRole.Read);
        Assert.Equal(FlagOrigin.Vanilla, read.Origin);
        Assert.Equal("Vanilla", read.OriginName);
        Assert.Equal(new SiteLocation(_vanilla, "common/scripted_triggers/t.txt", 2), read.Location);

        var resolved = Assert.Single(index.Search("p_foo").SitesByName["p_foo"]);
        Assert.Equal(new SiteLocation(_vanilla, "common/scripted_effects/fx.txt", 2), resolved.Via);
        Assert.Equal("set_p_effect", resolved.ViaName);
        Assert.Equal(new SiteLocation(_mod, "events/e.txt", 4), resolved.Location);
        Assert.Equal(FlagOrigin.ActiveMod, resolved.Origin);
        Assert.Empty(index.UnreadFiles);
        Assert.Equal(3, index.FileCount);
    }

    [Fact]
    public void Build_PassThroughCallerNeverNamesInnerEffect()
    {
        Write(_vanilla, "common/scripted_effects/inner.txt", "inner_eff = {\n\tset_variable = x_$P$\n}");
        Write(_vanilla, "common/scripted_effects/outer.txt", "outer_eff = {\n\tinner_eff = { P = $Q$ }\n}");
        Write(_mod, "events/m.txt", "m.1 = {\n\timmediate = {\n\t\touter_eff = { Q = foo }\n\t}\n}");

        var index = Builder().Build();

        var site = Assert.Single(index.Search("x_foo").SitesByName["x_foo"]);
        Assert.Equal(new SiteLocation(_mod, "events/m.txt", 3), site.Location);
        Assert.Equal("inner_eff", site.ViaName);
    }

    [Fact]
    public void Build_OverriddenOuterDefinition_DoesNotFanOut()
    {
        Write(_vanilla, "common/scripted_effects/inner.txt", "inner_eff = {\n\tset_variable = x_$P$\n}");
        Write(_vanilla, "common/scripted_effects/outer.txt", "outer_eff = {\n\tinner_eff = { P = $Q$ }\n}");
        Write(_mod, "common/scripted_effects/mod_outer.txt", "outer_eff = {\n\tset_variable = unrelated\n}");
        Write(_mod, "events/m.txt", "m.1 = {\n\timmediate = {\n\t\touter_eff = { Q = foo }\n\t}\n}");

        var index = Builder().Build();

        Assert.Empty(index.Search("x_foo").Names);
        Assert.Single(index.Search("x_*").PatternSites);
    }

    [Fact]
    public void Build_GuiAndLocalizationUseDataFunctions()
    {
        Write(_mod, "gui/w.gui", "text = \"[GetVariable('gui_var')]\"");
        Write(_mod, "localization/english/l_english.yml", "l_english:\n key:0 \"[GetGlobalVariable('loc_var')]\"");

        var index = Builder().Build();

        Assert.Equal(FlagRole.Read, Assert.Single(index.Search("gui_var").SitesByName["gui_var"]).Role);
        Assert.Equal(FlagKind.GlobalVariable, Assert.Single(index.Search("loc_var").SitesByName["loc_var"]).Kind);
    }

    [Fact]
    public void Build_BadFileGoesToUnread()
    {
        WriteEndToEndFixture();
        var bad = Write(_mod, "events/bad.txt", "b.1 = { immediate = { set_variable = bad_var } }");

        FlagIndex index;
        using (new FileStream(bad, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            index = Builder().Build();
        }

        var unread = Assert.Single(index.UnreadFiles);
        Assert.Equal(Path.GetFullPath(bad), Path.GetFullPath(unread), StringComparer.OrdinalIgnoreCase);
        Assert.Empty(index.Search("bad_var").Names);
        Assert.Equal(2, index.Search("a").SitesByName["a"].Count);
        Assert.Single(index.Search("p_foo").Names);
    }

    [Fact]
    public void Build_EffectAndTriggerWithSameName_BothResolved()
    {
        Write(_vanilla, "common/scripted_effects/fx.txt", "cd = {\n\tset_variable = x_$k$\n}");
        Write(_vanilla, "common/scripted_triggers/t.txt", "cd = {\n\thas_variable = x_$k$\n}");
        Write(_mod, "events/e.txt", "e.1 = {\n\ttrigger = {\n\t\tcd = { k = foo }\n\t}\n\timmediate = {\n\t\tcd = { k = foo }\n\t}\n}");

        var sites = Builder().Build().Search("x_foo").SitesByName["x_foo"];

        Assert.Contains(sites, s => s.Role == FlagRole.Set && s.Via is { RelativePath: "common/scripted_effects/fx.txt", Line: 2 });
        Assert.Contains(sites, s => s.Role == FlagRole.Read && s.Via is { RelativePath: "common/scripted_triggers/t.txt", Line: 2 });
    }

    [Fact]
    public void Latin1File_IsRead()
    {
        var path = Path.Combine(_mod, "events", "latin.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = new List<byte>();
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("# caf"));
        bytes.Add(0xE9);
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("\nset_variable = x\nset_variable = caf"));
        bytes.Add(0xE9);
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("_v\n"));
        File.WriteAllBytes(path, bytes.ToArray());

        var index = Builder().Build();

        var site = Assert.Single(index.Search("x").SitesByName["x"]);
        Assert.Equal(FlagRole.Set, site.Role);
        Assert.Single(index.Search("café_v").Names);
        Assert.Empty(index.UnreadFiles);
    }

    [Fact]
    public void CandidateKeptOnlyWhenSet()
    {
        var first = new FileScan();
        first.Sites.Add(new RawSite("cand", FlagKind.Variable, null, FlagRole.Read, 1, IsCandidate: true));
        first.Sites.Add(new RawSite("known", FlagKind.Variable, null, FlagRole.Read, 2, IsCandidate: true));
        var second = new FileScan();
        second.Sites.Add(new RawSite("known", FlagKind.Variable, null, FlagRole.Set, 1));
        var files = new[]
        {
            new FlagScannedFile(new SiteLocation(_mod, "localization/english/a_l_english.yml", 0), FlagOrigin.ActiveMod, "My Mod", first),
            new FlagScannedFile(new SiteLocation(_mod, "events/e.txt", 0), FlagOrigin.ActiveMod, "My Mod", second),
        };

        var builder = Builder();
        var index = builder.BuildFrom(files, Array.Empty<string>());

        var known = Assert.Single(index.Search("known").Names);
        Assert.Equal(1, known.ReadCount);
        Assert.Equal(1, known.SetCount);
        Assert.Empty(index.Search("cand").Names);
        Assert.Equal(1, builder.LastDroppedCandidates);
    }

    [Fact]
    public void Build_Cancel()
    {
        WriteEndToEndFixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => Builder().Build(cts.Token));
    }

    [Fact]
    public void Build_ProgressEndsWithTotal()
    {
        for (var i = 0; i < 120; i++)
        {
            Write(_mod, $"events/e{i:D3}.txt", $"e.{i} = {{ immediate = {{ set_variable = v{i} }} }}");
        }

        var progress = new Collector();
        var index = Builder().Build(progress: progress);

        var reports = progress.Reports;
        Assert.Equal(new BuildProgress(BuildPhase.Reading, 0, 120), reports[0]);
        Assert.Contains(new BuildProgress(BuildPhase.Reading, 50, 120), reports);
        Assert.Contains(new BuildProgress(BuildPhase.Reading, 100, 120), reports);
        Assert.Contains(new BuildProgress(BuildPhase.Reading, 120, 120), reports);
        Assert.Contains(new BuildProgress(BuildPhase.Calls, 0, 120), reports);
        Assert.Contains(new BuildProgress(BuildPhase.Calls, 50, 120), reports);
        Assert.Contains(new BuildProgress(BuildPhase.Calls, 120, 120), reports);
        Assert.Contains(new BuildProgress(BuildPhase.Resolving, 0, 1), reports);
        Assert.Equal(new BuildProgress(BuildPhase.Resolving, 1, 1), reports[^1]);
        Assert.Equal(120, index.Search("v*").Names.Count);
        // Phasen in Reihenfolge, Done je Phase nie rückläufig.
        var ordered = reports.OrderBy(r => r.Phase).ThenBy(r => r.Done).ToList();
        Assert.Equal(ordered, reports);
        Assert.Equal(reports.Distinct().Count(), reports.Count);
    }

    private sealed class Collector : IProgress<BuildProgress>
    {
        private readonly List<BuildProgress> _reports = new();

        public List<BuildProgress> Reports
        {
            get
            {
                lock (_reports)
                {
                    return _reports.ToList();
                }
            }
        }

        public void Report(BuildProgress value)
        {
            lock (_reports)
            {
                _reports.Add(value);
            }
        }
    }
}
