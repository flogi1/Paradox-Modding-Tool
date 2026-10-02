using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class SourceResolverTests
{
    private static readonly DateTime RunTime = new(2026, 9, 22, 19, 49, 0, DateTimeKind.Utc);

    [Fact]
    public void LaterModInPlaysetWinsAndPhaseFolderIsAdded()
    {
        using var tree = new TempTree(
            ("a/in_game/common/scripted_effects/x.txt", "a"),
            ("b/in_game/common/scripted_effects/x.txt", "b"));
        var a = new ModSource("A", tree.PathOf("a"), IsOwn: false, LoadOrder: 0);
        var b = new ModSource("B", tree.PathOf("b"), IsOwn: true, LoadOrder: 1);
        var resolver = new SourceResolver(new[] { a, b }, Array.Empty<string>(), RunTime, lastWriteUtc: _ => RunTime);

        var source = resolver.Resolve("common/scripted_effects/x.txt")!;

        Assert.Equal(tree.PathOf("b/in_game/common/scripted_effects/x.txt"), source.FullPath);
        Assert.Same(b, source.Mod);
        Assert.True(source.IsOwn);
        Assert.False(source.IsVanilla);
    }

    [Fact]
    public void FindsMainMenuPhaseAndFallsBackToVanilla()
    {
        using var tree = new TempTree(
            ("install/game/main_menu/gui/menu.gui", "m"),
            ("install/jomini/in_game/gui/j.gui", "j"));
        var resolver = new SourceResolver(Array.Empty<ModSource>(), SourceResolver.VanillaRootsFor(tree.PathOf("install")), RunTime, lastWriteUtc: _ => RunTime);

        var menu = resolver.Resolve("gui/menu.gui")!;
        Assert.True(menu.IsVanilla);
        Assert.Equal(tree.PathOf("install/game/main_menu/gui/menu.gui"), menu.FullPath);
        Assert.Equal(tree.PathOf("install/jomini/in_game/gui/j.gui"), resolver.Resolve("gui/j.gui")!.FullPath);
    }

    [Fact]
    public void ModsOutsideThePlaysetAreIgnored()
    {
        using var tree = new TempTree(("cmf/in_game/common/x.txt", "x"));
        var cmf = new ModSource("CMF", tree.PathOf("cmf"), IsOwn: false, LoadOrder: -1);
        var resolver = new SourceResolver(new[] { cmf }, Array.Empty<string>(), RunTime);

        Assert.False(cmf.InPlayset);
        Assert.Null(resolver.Resolve("common/x.txt"));
    }

    [Fact]
    public void ResolvesPathsWithPhaseOrWithoutPhase()
    {
        using var tree = new TempTree(
            ("m/in_game/common/z.txt", "z"),
            ("m/input_profile/z_cmf.profile", "p"));
        var mod = new ModSource("M", tree.PathOf("m"), IsOwn: true, LoadOrder: 0);
        var resolver = new SourceResolver(new[] { mod }, Array.Empty<string>(), RunTime, lastWriteUtc: _ => RunTime);

        Assert.Equal(tree.PathOf("m/in_game/common/z.txt"), resolver.Resolve("in_game/common/z.txt")!.FullPath);
        Assert.Equal(tree.PathOf("m/input_profile/z_cmf.profile"), resolver.Resolve("input_profile/z_cmf.profile")!.FullPath);
        Assert.Equal(tree.PathOf("m/in_game/common/z.txt"), resolver.Resolve(@"common\z.txt")!.FullPath);
    }

    [Fact]
    public void MarksFilesChangedAfterTheRun()
    {
        using var tree = new TempTree(("m/in_game/common/z.txt", "z"));
        var mod = new ModSource("M", tree.PathOf("m"), IsOwn: true, LoadOrder: 0);

        Assert.True(new SourceResolver(new[] { mod }, Array.Empty<string>(), RunTime, lastWriteUtc: _ => RunTime.AddMinutes(5)).Resolve("common/z.txt")!.ChangedSinceRun);
        Assert.False(new SourceResolver(new[] { mod }, Array.Empty<string>(), RunTime, lastWriteUtc: _ => RunTime.AddMinutes(-5)).Resolve("common/z.txt")!.ChangedSinceRun);
    }

    [Fact]
    public void UnknownPathGivesNull()
    {
        var resolver = new SourceResolver(Array.Empty<ModSource>(), Array.Empty<string>(), RunTime, fileExists: _ => false);

        Assert.Null(resolver.Resolve("common/nowhere.txt"));
    }

    [Fact]
    public void FindNearestLinePrefersTheClosestWholeWordMatch()
    {
        var lines = new[] { "a = my_var", "b = my_var_long", "", "", "c = my_var" };

        Assert.Equal(5, SourceResolver.FindNearestLine(lines, 4, "my_var"));
        Assert.Equal(1, SourceResolver.FindNearestLine(lines, 2, "my_var"));
        Assert.Null(SourceResolver.FindNearestLine(lines, 2, "other"));
    }
}
