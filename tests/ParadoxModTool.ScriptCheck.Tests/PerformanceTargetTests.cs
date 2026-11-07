using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class PerformanceTargetTests : IDisposable
{
    private readonly ScriptCheckTestRoots _roots = new();

    public void Dispose() => _roots.Dispose();

    /// <summary>Playset aus der anderen Mod (0) und der gewählten Mod (1); dazu eine eigene Mod außerhalb und eine installierte Mod außerhalb des Playsets.</summary>
    private IReadOnlyList<ModSource> ModSources(string own, string installed) => new List<ModSource>
    {
        new("Other Mod", _roots.Other, false, 0),
        new("My Mod", _roots.Mod, false, 1),
        new("Own Mod", own, true, -1),
        new("Installed Mod", installed, false, -1),
    };

    private static IReadOnlyList<FlagRoot> ModRoots(FlagSourceSet sources) => sources.Roots.Where(r => r.Origin != FlagOrigin.Vanilla).ToList();

    [Fact]
    public void VanillaTargetAlone()
    {
        var jomini = Path.Combine(_roots.Temp, "jomini");
        var target = new PerformanceTarget(PerformanceTargetKind.Vanilla, "Vanilla", _roots.Vanilla + Path.DirectorySeparatorChar);

        var sources = PerformanceTarget.Sources(target, RepoFiles.Profile("ck3"), ModSources(_roots.Temp, _roots.Temp), true, new[] { _roots.Vanilla.ToUpperInvariant(), jomini });

        var active = Assert.Single(sources.Roots, r => r.Origin == FlagOrigin.ActiveMod);
        Assert.Equal(ErrorLogSources.Key(_roots.Vanilla), ErrorLogSources.Key(active.Directory), StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(sources.Roots, r => r.Origin == FlagOrigin.OtherMod);
        var vanilla = Assert.Single(sources.Roots, r => r.Origin == FlagOrigin.Vanilla);
        Assert.Equal(jomini, vanilla.Directory);
    }

    [Fact]
    public void PlaysetModTargetInPlayset()
    {
        var target = new PerformanceTarget(PerformanceTargetKind.PlaysetMod, "My Mod", _roots.Mod);
        var installed = Path.Combine(_roots.Temp, "i");

        var own = Path.Combine(_roots.Temp, "own");
        var sources = PerformanceTarget.Sources(target, RepoFiles.Profile("ck3"), ModSources(own, installed), true, new[] { _roots.Vanilla });

        var mods = ModRoots(sources);
        Assert.DoesNotContain(mods, r => r.Directory == own);
        Assert.Contains(mods, r => r.Directory == _roots.Other && r.Origin == FlagOrigin.OtherMod);
        var active = Assert.Single(mods, r => r.Origin == FlagOrigin.ActiveMod);
        Assert.Equal(_roots.Mod, active.Directory);
        Assert.DoesNotContain(mods, r => r.Directory == installed);
        Assert.Contains(sources.Roots, r => r.Origin == FlagOrigin.Vanilla && r.Directory == _roots.Vanilla);
    }

    [Fact]
    public void InstalledModTargetWithVanillaOnly()
    {
        var installed = Path.Combine(_roots.Temp, "i");
        var target = new PerformanceTarget(PerformanceTargetKind.InstalledMod, "Installed Mod", installed);
        var mods = ModSources(Path.Combine(_roots.Temp, "own"), installed);

        var withPlayset = PerformanceTarget.Sources(target, RepoFiles.Profile("ck3"), mods, true, new[] { _roots.Vanilla });
        var playsetModWithoutPlayset = PerformanceTarget.Sources(
            new PerformanceTarget(PerformanceTargetKind.PlaysetMod, "My Mod", _roots.Mod), RepoFiles.Profile("ck3"), mods, false, new[] { _roots.Vanilla });

        var only = Assert.Single(ModRoots(withPlayset));
        Assert.Equal(installed, only.Directory);
        Assert.Equal(FlagOrigin.ActiveMod, only.Origin);
        Assert.Contains(withPlayset.Roots, r => r.Origin == FlagOrigin.Vanilla && r.Directory == _roots.Vanilla);
        Assert.Equal(_roots.Mod, Assert.Single(ModRoots(playsetModWithoutPlayset)).Directory);
    }

    [Fact]
    public void ChoicesOrderAndDedup()
    {
        var a = Path.Combine(_roots.Temp, "a");
        var b = Path.Combine(_roots.Temp, "b");
        var c = Path.Combine(_roots.Temp, "c");
        var d = Path.Combine(_roots.Temp, "d");

        var choices = PerformanceTarget.Choices(
            _roots.Vanilla,
            new[] { ("Zeta", b), ("Alpha", a), ("Zeta again", b + Path.DirectorySeparatorChar) },
            new[] { ("delta", d), ("Charlie", c), ("Alpha copy", a.ToUpperInvariant()), ("Blank", " ") });

        Assert.Equal(
            new[]
            {
                (PerformanceTargetKind.Vanilla, _roots.Vanilla),
                (PerformanceTargetKind.PlaysetMod, b),
                (PerformanceTargetKind.PlaysetMod, a),
                (PerformanceTargetKind.InstalledMod, c),
                (PerformanceTargetKind.InstalledMod, d),
            },
            choices.Select(t => (t.Kind, t.Directory)).ToArray());
        Assert.Equal(new[] { "Zeta", "Alpha", "Charlie", "delta" }, choices.Skip(1).Select(t => t.Name).ToArray());
        Assert.DoesNotContain(PerformanceTarget.Choices(null, new[] { ("Alpha", a) }, Array.Empty<(string, string)>()), t => t.Kind == PerformanceTargetKind.Vanilla);
    }

    [Fact]
    public void RequestNotes()
    {
        ScriptCheckTestRoots.Write(_roots.Mod, "events/m.txt", "namespace = m");
        var store = new ScriptCheckStore(Path.Combine(_roots.Temp, "store"));
        var mods = ModSources(Path.Combine(_roots.Temp, "own"), Path.Combine(_roots.Temp, "i"));
        var playsetTarget = new PerformanceTarget(PerformanceTargetKind.PlaysetMod, "My Mod", _roots.Mod);
        var vanillaTarget = new PerformanceTarget(PerformanceTargetKind.Vanilla, "Vanilla", _roots.Vanilla);
        ScriptCheckTestRoots.Write(_roots.Vanilla, "events/v.txt", "namespace = v");

        var inPlayset = PerformanceTarget.Request(playsetTarget, RepoFiles.Profile("ck3"), mods, "Set", new[] { _roots.Vanilla }, ScriptCheckTestRoots.Rules(), store);
        var vanillaOnlyRoot = PerformanceTarget.Request(vanillaTarget, RepoFiles.Profile("ck3"), mods, "Set", new[] { _roots.Vanilla }, ScriptCheckTestRoots.Rules(), store);
        var noGame = PerformanceTarget.Request(playsetTarget, RepoFiles.Profile("ck3"), mods, null, Array.Empty<string>(), ScriptCheckTestRoots.Rules(), store);

        Assert.Equal(new[] { FlagTrackerTexts.PlaysetNote("Set") }, inPlayset.Notes);
        Assert.Equal(ScriptCheckNames.All, inPlayset.Names);
        Assert.True(inPlayset.PerformanceOnly);
        Assert.Empty(inPlayset.BuiltIns);
        Assert.Same(store, inPlayset.Store);
        Assert.Empty(vanillaOnlyRoot.Notes);
        Assert.Equal(new[] { FlagTrackerTexts.NoGameFolder }, noGame.Notes);
    }

    [Fact]
    public async Task MissingTargetGivesNote()
    {
        ScriptCheckTestRoots.Write(_roots.Vanilla, "events/v.txt", "namespace = v\nv.1 = {\n\timmediate = { every_living_character = { } }\n}");
        var missing = Path.Combine(_roots.Temp, "missing");
        var empty = Path.Combine(_roots.Temp, "empty");
        Directory.CreateDirectory(Path.Combine(empty, "events"));
        var store = new ScriptCheckStore(Path.Combine(_roots.Temp, "store"));

        var missingRequest = PerformanceTarget.Request(new PerformanceTarget(PerformanceTargetKind.InstalledMod, "Gone", missing), RepoFiles.Profile("ck3"),
            Array.Empty<ModSource>(), null, new[] { _roots.Vanilla }, ScriptCheckTestRoots.Rules(), store);
        var emptyRequest = PerformanceTarget.Request(new PerformanceTarget(PerformanceTargetKind.InstalledMod, "Empty", empty), RepoFiles.Profile("ck3"),
            Array.Empty<ModSource>(), null, new[] { _roots.Vanilla }, ScriptCheckTestRoots.Rules(), store);

        Assert.Contains(ScriptCheckTexts.TargetMissing(missing), missingRequest.Notes);
        Assert.Contains(ScriptCheckTexts.TargetMissing(empty), emptyRequest.Notes);

        var session = new ScriptCheckSession();
        await session.RefreshAsync(() => missingRequest, null, CancellationToken.None);
        Assert.Equal(ScriptCheckState.Ready, session.State);
        Assert.Empty(session.Result!.Findings);
        Assert.Contains(ScriptCheckTexts.TargetMissing(missing), session.StatusText);
    }
}
