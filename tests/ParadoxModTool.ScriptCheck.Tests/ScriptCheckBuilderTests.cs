using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class ScriptCheckBuilderTests : IDisposable
{
    private static readonly string ProfilesDir = ScriptCheckTestRoots.ProfilesDir;

    private readonly ScriptCheckTestRoots _roots = new();
    private readonly string _temp;
    private readonly string _vanilla;
    private readonly string _other;
    private readonly string _mod;
    private readonly ScriptCheckStore _store;

    public ScriptCheckBuilderTests()
    {
        _temp = _roots.Temp;
        _vanilla = _roots.Vanilla;
        _other = _roots.Other;
        _mod = _roots.Mod;
        _store = new ScriptCheckStore(Path.Combine(_temp, "store"));
    }

    public void Dispose() => _roots.Dispose();

    private static ScriptCheckRules Rules() => ScriptCheckTestRoots.Rules();

    private static string Write(string root, string relative, string text) => ScriptCheckTestRoots.Write(root, relative, text);

    private FlagSourceSet Sources(bool withActiveMod = true) => _roots.Sources(withActiveMod: withActiveMod);

    private ScriptCheckBuilder Builder(ScriptCheckRules? rules = null, bool withActiveMod = true) => new(Sources(withActiveMod), rules ?? Rules());

    private void WriteFixture()
    {
        Write(_vanilla, "common/on_action/v.txt", "on_game_start = {\n\tevents = { m.1 }\n}");
        Write(_vanilla, "common/on_action/w.txt", "on_x = {\n\tevents = { m.2 }\n}");
        Write(_other, "events/o.txt", "namespace = o\no.1 = {\n\timmediate = { }\n}");
        Write(_mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = {\n\t\tmy_effect = yes\n\t}\n}");
    }

    private (ScriptCheckIndex Index, Collector Progress) Refresh(ScriptCheckBuilder builder)
    {
        var progress = new Collector();
        var index = builder.Refresh(_store, progress: progress);
        return (index, progress);
    }

    [Fact]
    public void BuildFindsDefinitionsAndRefs()
    {
        WriteFixture();

        var index = Builder().Build();

        Assert.Equal(_mod, index.ModRoot?.Directory);
        Assert.Equal(4, index.FileCount);
        Assert.Empty(index.UnreadFiles);
        var modFile = Assert.Single(index.Files, f => f.Origin == FlagOrigin.ActiveMod);
        Assert.Equal("My Mod", modFile.OriginName);
        Assert.Equal("events/m.txt", modFile.File.RelativePath);
        Assert.Equal(new[] { "m.1" }, modFile.Scan.Definitions.Select(d => d.Name));

        var call = Assert.Single(index.EventCalls, c => c.Ref.Target == "m.1");
        Assert.Equal(_vanilla, call.Location.RootDirectory);
        Assert.Equal("common/on_action/v.txt", call.Location.RelativePath);
        Assert.Equal(2, call.Location.Line);
        Assert.Equal("on_game_start", call.Ref.Owner);

        var mention = Assert.Single(index.Mentions);
        Assert.Equal("m.1", mention.Ref.Target);
        Assert.Equal(CheckKind.Event, mention.Ref.TargetKind);
        Assert.Equal("on_game_start", mention.Ref.Owner);
        Assert.Equal("common/on_action/v.txt", mention.Location.RelativePath);

        var block = Assert.Single(index.ModBlockCalls);
        Assert.Equal("my_effect", block.Ref.Target);
        Assert.Equal(_mod, block.Location.RootDirectory);
        Assert.Equal(4, block.Location.Line);
    }

    [Fact]
    public void RefreshReusesUnchangedFiles()
    {
        WriteFixture();
        var (first, firstProgress) = Refresh(Builder());
        Assert.Equal(4, firstProgress.Last(BuildPhase.Reading).Total);

        var (second, progress) = Refresh(Builder());

        Assert.Equal(new BuildProgress(BuildPhase.Reading, 0, 0), progress.Last(BuildPhase.Reading));
        Assert.Equal(first.Mentions, second.Mentions);
        Assert.Equal(first.EventCalls, second.EventCalls);
        Assert.Equal(first.ModBlockCalls, second.ModBlockCalls);
        Assert.Equal(4, second.FileCount);
    }

    [Fact]
    public void NewModEventRescansMentions()
    {
        WriteFixture();
        var (first, _) = Refresh(Builder());
        Assert.DoesNotContain(first.Mentions, m => m.Ref.Target == "m.2");

        Write(_mod, "events/m2.txt", "namespace = m\nm.2 = {\n\timmediate = { }\n}");
        var (second, progress) = Refresh(Builder());

        Assert.Equal(1, progress.Last(BuildPhase.Reading).Total);
        var mention = Assert.Single(second.Mentions, m => m.Ref.Target == "m.2");
        Assert.Equal("common/on_action/w.txt", mention.Location.RelativePath);
        Assert.Equal("on_x", mention.Ref.Owner);
        Assert.Single(second.Mentions, m => m.Ref.Target == "m.1");
    }

    [Fact]
    public void LocCallsAreMentionsAndRefreshFollowsChanges()
    {
        Write(_mod, "common/script_values/sv.txt", "my_sv = 1");
        var yml = Write(_vanilla, "localization/english/a_l_english.yml", "l_english:\n a:0 \"x [Y.ScriptValue('my_sv')|0]\"");
        Write(_vanilla, "localization/german/a_l_german.yml", "l_german:\n a:0 \"x [Y.ScriptValue('my_sv')]\"");
        var (first, _) = Refresh(Builder());

        var mention = Assert.Single(first.Mentions, m => m.Ref.Target == "my_sv");
        Assert.Equal("localization/english/a_l_english.yml", mention.Location.RelativePath);
        Assert.Equal(2, mention.Location.Line);
        Assert.Equal(CheckKind.Value, mention.Ref.TargetKind);

        File.WriteAllText(yml, "l_english:\n a:0 \"nothing here, my_sv plain\"\n b:0 \"[Y.ScriptValue('my_sv')] [Y.ScriptValue('my_sv')]\"");
        File.SetLastWriteTimeUtc(yml, DateTime.UtcNow.AddMinutes(5));
        var (second, _) = Refresh(Builder());

        Assert.Equal(new[] { 3 }, second.Mentions.Where(m => m.Ref.Target == "my_sv").Select(m => m.Ref.Line).Distinct().ToArray());
    }

    [Fact]
    public void EmptyLocFunctionListReadsNoYml()
    {
        Write(_mod, "common/script_values/sv.txt", "my_sv = 1");
        Write(_vanilla, "localization/english/a_l_english.yml", "l_english:\n a:0 \"[Y.ScriptValue('my_sv')]\"");
        var read = new List<string>();
        var rules = new ScriptCheckRules { ValueFolders = new[] { "common/script_values" } };

        var index = new ScriptCheckBuilder(Sources(), rules, p =>
        {
            lock (read)
            {
                read.Add(p);
            }

            return File.ReadAllText(p);
        }).Build();

        Assert.DoesNotContain(read, p => p.EndsWith(".yml", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(index.Mentions);

        read.Clear();
        new ScriptCheckBuilder(Sources(), Rules(), p =>
        {
            lock (read)
            {
                read.Add(p);
            }

            return File.ReadAllText(p);
        }).Build();
        Assert.Contains(read, p => p.EndsWith("a_l_english.yml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReplacedDefinitionsReadOnlyTxtFiles()
    {
        // Eine Mod-Datei, die eine Vanilla-Datei gleichen Pfads ersetzt, lässt deren Definitionen nur bei .txt zählen; .gui/.gfx haben keine Definitionen und werden nicht gelesen.
        Write(_mod, "interface/w.gui", "window = { }");
        Write(_vanilla, "interface/w.gui", "window = { }");
        Write(_mod, "common/scripted_effects/e.txt", "my_fx = { add_gold = 1 }");
        Write(_vanilla, "common/scripted_effects/e.txt", "my_fx = { add_gold = 2 }");
        var read = new List<string>();

        var index = new ScriptCheckBuilder(Sources(), Rules(), p =>
        {
            lock (read)
            {
                read.Add(p);
            }

            return File.ReadAllText(p);
        }).Build();

        Assert.DoesNotContain(read, p => p.StartsWith(_vanilla, StringComparison.OrdinalIgnoreCase) && p.EndsWith("w.gui", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(index.ReplacedDefinitions, d => d.Name == "my_fx");
    }

    [Fact]
    public void UnchangedNamesSkipMentionScan()
    {
        WriteFixture();
        Refresh(Builder());

        var (_, progress) = Refresh(Builder());
        Assert.Equal(new BuildProgress(BuildPhase.Calls, 0, 0), progress.Last(BuildPhase.Calls));

        // Eine geänderte Datei ohne neue Namen: nur sie wird gelesen und dabei gleich nach Erwähnungen durchsucht, die übrigen Erwähnungen bleiben.
        // Gleiche Länge wie vorher: die Änderungszeit muss sich sicher unterscheiden.
        var path = Write(_vanilla, "common/on_action/w.txt", "on_x = {\n\tevents = { m.1 }\n}");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));
        var (index, changed) = Refresh(Builder());
        Assert.Equal(1, changed.Last(BuildPhase.Reading).Total);
        Assert.Equal(new BuildProgress(BuildPhase.Calls, 0, 0), changed.Last(BuildPhase.Calls));
        Assert.Equal(2, index.Mentions.Count(m => m.Ref.Target == "m.1"));
    }

    private void WriteVanillaEffects()
    {
        Write(_vanilla, "common/scripted_effects/v.txt", "v_a = {\n\tv_b = yes\n}\nv_b = {\n\tadd_gold = 1\n}");
    }

    [Fact]
    public void AllNamesMentionsVanillaEffects()
    {
        WriteVanillaEffects();

        var all = new ScriptCheckBuilder(Sources(), Rules(), ScriptCheckNames.All).Build();
        var mention = Assert.Single(all.Mentions, m => m.Ref.Target == "v_b");
        Assert.Equal("v_a", mention.Ref.Owner);
        Assert.True(mention.Ref.CallForm);

        var mod = new ScriptCheckBuilder(Sources(), Rules(), ScriptCheckNames.Mod).Build();
        Assert.Empty(mod.Mentions);
    }

    [Fact]
    public void AllNamesRefreshReusesMentions()
    {
        WriteVanillaEffects();
        var builder = new ScriptCheckBuilder(Sources(), Rules(), ScriptCheckNames.All);
        var (first, _) = Refresh(builder);
        Assert.Contains(first.Mentions, m => m.Ref.Target == "v_b");

        var (second, progress) = Refresh(new ScriptCheckBuilder(Sources(), Rules(), ScriptCheckNames.All));
        Assert.Equal(new BuildProgress(BuildPhase.Calls, 0, 0), progress.Last(BuildPhase.Calls));
        Assert.Equal(first.Mentions, second.Mentions);
    }

    [Fact]
    public void ModesDoNotShareStore()
    {
        WriteVanillaEffects();
        Refresh(new ScriptCheckBuilder(Sources(), Rules(), ScriptCheckNames.Mod));

        var (all, _) = Refresh(new ScriptCheckBuilder(Sources(), Rules(), ScriptCheckNames.All));
        Assert.Contains(all.Mentions, m => m.Ref.Target == "v_b");

        var (mod, _) = Refresh(new ScriptCheckBuilder(Sources(), Rules(), ScriptCheckNames.Mod));
        Assert.Empty(mod.Mentions);
    }

    [Fact]
    public void RulesChangeForcesFullRead()
    {
        WriteFixture();
        Refresh(Builder());
        var json = File.ReadAllText(Path.Combine(ProfilesDir, "scriptcheck", "ck3.json"));
        var changedPath = Path.Combine(_temp, "ck3-changed.json");
        File.WriteAllText(changedPath, json.Replace("\"notes\": \"", "\"notes\": \"geändert ", StringComparison.Ordinal));
        var changed = ScriptCheckRules.Load(changedPath);
        Assert.NotEqual(Rules().Fingerprint, changed.Fingerprint);

        var (index, progress) = Refresh(Builder(changed));

        Assert.Equal(index.FileCount, progress.Last(BuildPhase.Reading).Total);
        Assert.Equal(4, index.FileCount);
    }

    [Fact]
    public void FirstBuildReadsEachFileOnce()
    {
        WriteFixture();
        Write(_vanilla, "gui/w.gui", "widget = {\n\ttext = \"m.1\"\n}");
        var reads = new System.Collections.Concurrent.ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string Counting(string path)
        {
            reads.AddOrUpdate(path, 1, (_, n) => n + 1);
            return FlagIndexBuilder.ReadText(path);
        }

        var index = new ScriptCheckBuilder(Sources(), Rules(), Counting).Refresh(_store);

        Assert.Equal(5, index.FileCount);
        Assert.Equal(5, reads.Count);
        Assert.All(reads, r => Assert.Equal(1, r.Value));
        Assert.Equal(2, index.Mentions.Count(m => m.Ref.Target == "m.1"));
        Assert.Single(index.ModBlockCalls);
    }

    [Fact]
    public void KindChangeRescansMentions()
    {
        Write(_mod, "common/scripted_effects/x.txt", "my_fx = {\n\tadd_gold = 1\n}");
        Write(_vanilla, "events/v.txt", "namespace = v\nv.1 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}");
        var (first, _) = Refresh(Builder());
        Assert.Equal(CheckKind.Effect, Assert.Single(first.Mentions).Ref.TargetKind);

        File.Delete(Path.Combine(_mod, "common", "scripted_effects", "x.txt"));
        Write(_mod, "common/scripted_triggers/x.txt", "my_fx = {\n\talways = yes\n}");
        var (second, progress) = Refresh(Builder());

        Assert.Equal(CheckKind.Trigger, Assert.Single(second.Mentions).Ref.TargetKind);
        Assert.True(progress.Last(BuildPhase.Calls).Total >= 1);
    }

    [Fact]
    public void VanishedVanillaFileDropsStoredMentions()
    {
        WriteFixture();
        var (first, _) = Refresh(Builder());
        Assert.Single(first.Mentions);

        File.Delete(Path.Combine(_vanilla, "common", "on_action", "v.txt"));
        var (second, _) = Refresh(Builder());

        Assert.Empty(second.Mentions);
        Assert.DoesNotContain(second.EventCalls, c => c.Ref.Target == "m.1");
        Assert.Equal(3, second.FileCount);
    }

    [Fact]
    public void GuiFileMentionsScriptValue()
    {
        Write(_mod, "common/script_values/sv.txt", "my_sv = 1");
        Write(_vanilla, "gui/w.gui", "widget = {\n\ttext = \"[GetPlayer.MakeScope.ScriptValue('my_sv')]\"\n}");

        var index = Builder().Build();

        Assert.Empty(index.UnreadFiles);
        var mention = Assert.Single(index.Mentions);
        Assert.Equal("my_sv", mention.Ref.Target);
        Assert.Equal(CheckKind.Value, mention.Ref.TargetKind);
        Assert.Equal("gui/w.gui", mention.Location.RelativePath);
        Assert.Equal(2, mention.Location.Line);
    }

    [Fact]
    public void NoActiveModHasNoModRoot()
    {
        WriteFixture();

        var index = Builder(withActiveMod: false).Build();

        Assert.Null(index.ModRoot);
        Assert.Empty(index.Mentions);
        Assert.Empty(index.ModBlockCalls);
        Assert.Equal(3, index.FileCount);
        Assert.Contains(index.EventCalls, c => c.Ref.Target == "m.1");
    }

    /// <summary>Sammelt Meldungen synchron (<see cref="Progress{T}"/> meldet asynchron).</summary>
    private sealed class Collector : IProgress<BuildProgress>
    {
        private readonly List<BuildProgress> _reports = new();

        public BuildProgress Last(BuildPhase phase)
        {
            lock (_reports)
            {
                return _reports.Last(r => r.Phase == phase);
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

    [Fact]
    public void IgnoredFoldersAreNotRead()
    {
        var rules = new ScriptCheckRules
        {
            EventFolders = new[] { "events" },
            ScriptContextKeys = new[] { "effect" },
            IgnoredFolders = new[] { "patchnotes" },
        };
        ScriptCheckTestRoots.Write(_roots.Mod, "patchnotes/1.txt", "effect = {\n\tsomething = yes\n}");
        ScriptCheckTestRoots.Write(_roots.Vanilla, "patchnotes/1.txt", "effect = {\n\tsomething = yes\n}");
        ScriptCheckTestRoots.Write(_roots.Mod, "patchnotes_old/1.txt", "effect = {\n\tsomething_else = yes\n}");

        var index = new ScriptCheckBuilder(_roots.Sources(), rules).Build();

        Assert.Equal(new[] { "patchnotes_old/1.txt" }, index.Files.Select(f => f.File.RelativePath));
        Assert.Equal(new[] { "something_else" }, index.ModBlockCalls.Select(c => c.Ref.Target));
    }
}
