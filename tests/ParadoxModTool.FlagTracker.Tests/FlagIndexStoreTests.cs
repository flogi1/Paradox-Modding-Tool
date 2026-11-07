using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public sealed class FlagIndexStoreTests : IDisposable
{
    private static readonly GameProfile Ck3 = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "ck3.json"));
    private static readonly FlagCommandTable Table = FlagCommandTable.Ck3(Array.Empty<string>());

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-flagstore-" + Guid.NewGuid().ToString("N"));
    private readonly string _vanilla;
    private readonly string _mod;
    private readonly FlagIndexStore _store;

    public FlagIndexStoreTests()
    {
        _vanilla = Path.Combine(_temp, "v");
        _mod = Path.Combine(_temp, "m");
        Directory.CreateDirectory(_vanilla);
        Directory.CreateDirectory(_mod);
        _store = new FlagIndexStore(Path.Combine(_temp, "store"));
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

    private FlagSourceSet Sources(params string[] extraMods) =>
        FlagSourceSet.ForPlayset(
            Ck3,
            new[] { new ModSource("My Mod", _mod, false, 0) }.Concat(extraMods.Select((m, i) => new ModSource("Mod" + i, m, false, i + 1))).ToList(),
            _mod,
            new[] { _vanilla });

    private FlagIndexBuilder Builder(FlagSourceSet? sources = null) => new(sources ?? Sources(), Table);

    private void WriteFixture()
    {
        Write(_vanilla, "common/scripted_effects/fx.txt", "set_p_effect = {\n\tset_variable = p_$P$\n}");
        Write(_vanilla, "common/scripted_triggers/t.txt", "has_a_trigger = {\n\thas_variable = a\n}");
        Write(_mod, "events/e.txt", "e.1 = {\n\timmediate = {\n\t\tset_variable = a\n\t\tset_p_effect = { P = foo }\n\t}\n}");
        Write(_mod, "events/f.txt", "f.1 = {\n\timmediate = {\n\t\tset_variable = b\n\t}\n}");
    }

    /// <summary>Refresh mit Fortschritt; Last ist die letzte Meldung der Phase Reading (wie viele Dateien neu gelesen wurden).</summary>
    private (FlagIndex Index, BuildProgress Last) Refresh(FlagIndexBuilder builder)
    {
        var progress = new Collector();
        var index = builder.Refresh(_store, progress: progress);
        return (index, progress.Reports.Last(r => r.Phase == BuildPhase.Reading));
    }

    [Fact]
    public void Refresh_FirstTimeBuildsAndSaves()
    {
        WriteFixture();
        var builder = Builder();

        var (index, last) = Refresh(builder);

        Assert.True(File.Exists(_store.FileFor(Sources().ScopeKey)));
        Assert.Equal(4, last.Total);
        Assert.Contains("p_foo", index.Search("p_*").Names.Select(n => n.Name));
        Assert.Equal(4, index.FileCount);
    }

    [Fact]
    public void Refresh_SecondTimeReadsNothing()
    {
        WriteFixture();
        var first = Refresh(Builder()).Index;

        var (second, last) = Refresh(Builder());

        Assert.Equal(new BuildProgress(BuildPhase.Reading, 0, 0), last);
        Assert.Equal(first.Sites, second.Sites);
        Assert.Equal(4, second.FileCount);
    }

    [Fact]
    public void Refresh_NoChange_EachPhaseReportedOnce()
    {
        WriteFixture();
        Refresh(Builder());
        var progress = new Collector();

        Builder().Refresh(_store, progress: progress);

        Assert.Equal(
            new[]
            {
                new BuildProgress(BuildPhase.Reading, 0, 0),
                new BuildProgress(BuildPhase.Calls, 0, 0),
                new BuildProgress(BuildPhase.Resolving, 0, 1),
                new BuildProgress(BuildPhase.Resolving, 1, 1),
            },
            progress.Reports);
    }

    [Fact]
    public void Refresh_ChangedFileReRead()
    {
        WriteFixture();
        Refresh(Builder());
        var path = Write(_mod, "events/f.txt", "f.1 = {\n\timmediate = {\n\t\tset_variable = b\n\t\tset_variable = brand_new\n\t}\n}");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));

        var (index, last) = Refresh(Builder());

        Assert.Equal(new BuildProgress(BuildPhase.Reading, 1, 1), last);
        Assert.Single(index.Search("brand_new").Names);
        Assert.Single(index.Search("a").Names);
        Assert.Contains("p_foo", index.Search("p_*").Names.Select(n => n.Name));
    }

    [Fact]
    public void Refresh_DeletedFileDropped()
    {
        WriteFixture();
        Refresh(Builder());
        File.Delete(Path.Combine(_mod, "events", "f.txt"));

        var (index, last) = Refresh(Builder());

        Assert.Equal(new BuildProgress(BuildPhase.Reading, 0, 0), last);
        Assert.Empty(index.Search("b").Names);
        Assert.Single(index.Search("a").Names);
        Assert.Equal(3, index.FileCount);
    }

    [Fact]
    public void Refresh_NewTemplateCall()
    {
        WriteFixture();
        Refresh(Builder());
        Write(_mod, "events/g.txt", "g.1 = {\n\timmediate = {\n\t\tset_p_effect = { P = bar }\n\t}\n}");

        var (index, last) = Refresh(Builder());

        Assert.Equal(new BuildProgress(BuildPhase.Reading, 1, 1), last);
        var site = Assert.Single(index.Search("p_bar").SitesByName["p_bar"]);
        Assert.Equal(new SiteLocation(_mod, "events/g.txt", 3), site.Location);
        Assert.Contains("p_foo", index.Search("p_*").Names.Select(n => n.Name));
    }

    [Fact]
    public void Refresh_ChangedDefinitionFile_RecomputesCallsOfOtherFiles()
    {
        WriteFixture();
        // Der Aufrufer liegt schon vor dem ersten Refresh vor (Aufrufe leer, weil der Aufgerufene noch unbekannt ist).
        Write(_mod, "events/h.txt", "h.1 = { immediate = { other_effect = { Q = zed } } }");
        Refresh(Builder());
        Assert.Empty(Builder().Build().Search("q_*").Names);
        // Nur die neue Definitionsdatei kommt dazu: Die Aufrufe der unveränderten Datei h.txt müssen jetzt erfasst werden.
        Write(_vanilla, "common/scripted_effects/other.txt", "other_effect = {\n\tset_variable = q_$Q$\n}");

        var (index, last) = Refresh(Builder());

        Assert.Equal(new BuildProgress(BuildPhase.Reading, 1, 1), last);

        Assert.Single(index.Search("q_zed").Names);
        var fresh = Builder().Build();
        Assert.Equal(fresh.Sites, index.Sites);
    }

    [Fact]
    public void Refresh_ScopeChange_NewFile()
    {
        WriteFixture();
        Refresh(Builder());
        var oldFile = _store.FileFor(Sources().ScopeKey);
        var oldWrite = File.GetLastWriteTimeUtc(oldFile);
        var other = Path.Combine(_temp, "m2");
        Write(other, "events/x.txt", "x.1 = { immediate = { set_variable = zz } }");
        var changed = Sources(other);

        var index = Builder(changed).Refresh(_store);

        Assert.NotEqual(Sources().ScopeKey, changed.ScopeKey);
        Assert.True(File.Exists(_store.FileFor(changed.ScopeKey)));
        Assert.True(File.Exists(oldFile));
        Assert.Equal(oldWrite, File.GetLastWriteTimeUtc(oldFile));
        Assert.Single(index.Search("zz").Names);
    }

    [Fact]
    public void Refresh_ChangedCommandTable_RebuildsFully()
    {
        WriteFixture();
        Write(_mod, "events/d.txt", "d.1 = {\n\timmediate = {\n\t\tadd_dynasty_flag = dyn_x\n\t}\n}");
        Assert.Empty(Refresh(Builder()).Index.Search("dyn_x").Names);

        var withDynasty = new FlagIndexBuilder(Sources(), FlagCommandTable.Ck3(new[] { "add_dynasty_flag" }));
        var index = withDynasty.Refresh(_store);

        var site = Assert.Single(index.Search("dyn_x").SitesByName["dyn_x"]);
        Assert.Equal("dynasty_flag", site.Family);
        Assert.Equal(new SiteLocation(_mod, "events/d.txt", 3), site.Location);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        WriteFixture();
        Refresh(Builder());

        var loaded = _store.Load(Sources().ScopeKey);

        Assert.NotNull(loaded);
        Assert.Equal(FlagIndexStore.FormatVersion, loaded!.FormatVersion);
        Assert.Equal(4, loaded.Files.Count);
        Assert.Contains(loaded.Files, f => f.Scan.Templates.Count == 1 && f.Stamp.RelativePath == "common/scripted_effects/fx.txt");
        Assert.Contains(loaded.Files, f => f.Scan.Calls.Any(c => c.Callee == "set_p_effect" && c.Params!["P"] == "foo"));
        Assert.Empty(Directory.GetFiles(Path.Combine(_temp, "store"), "*.tmp"));
    }

    [Fact]
    public void Save_ThenLoad_KeepsCandidateFlag()
    {
        var scan = new FileScan();
        scan.Sites.Add(new RawSite("c", FlagKind.EventTarget, null, FlagRole.Read, 3, IsCandidate: true));
        var stamp = new FileStamp(_mod, "localisation/a_l_english.yml", 10, 20);
        _store.Save("cand", new StoredScans(FlagIndexStore.FormatVersion, DateTime.UtcNow, new[] { (stamp, scan) }));

        var loaded = _store.Load("cand");

        Assert.Equal(scan.Sites[0], Assert.Single(Assert.Single(loaded!.Files).Scan.Sites));
    }

    [Fact]
    public void Load_Missing_Null()
    {
        Assert.Null(_store.Load("nothing"));
    }

    [Fact]
    public void Load_WrongVersion_Null()
    {
        _store.Save("k", new StoredScans(FlagIndexStore.FormatVersion + 1, DateTime.UtcNow, Array.Empty<(FileStamp, FileScan)>()));

        Assert.True(File.Exists(_store.FileFor("k")));
        Assert.Null(_store.Load("k"));
    }

    [Fact]
    public void Load_Corrupt_Null()
    {
        Directory.CreateDirectory(Path.Combine(_temp, "store"));
        File.WriteAllText(_store.FileFor("k"), "this is not gzip");

        Assert.Null(_store.Load("k"));
    }

    [Fact]
    public void Refresh_CorruptStore_BuildsFullAndOverwrites()
    {
        WriteFixture();
        Directory.CreateDirectory(Path.Combine(_temp, "store"));
        File.WriteAllText(_store.FileFor(Sources().ScopeKey), "garbage");

        var (index, last) = Refresh(Builder());

        Assert.Equal(4, last.Total);
        Assert.Contains("p_foo", index.Search("p_*").Names.Select(n => n.Name));
        Assert.NotNull(_store.Load(Sources().ScopeKey));
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
