using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class CompatchFileFilterTests
{
    [Theory]
    [InlineData("in_game/gfx/x.dds", "Grafik")]
    [InlineData("in_game/sound/x.OGG", "Audio")]
    [InlineData("in_game/gfx/models/x.mesh", "Modelle")]
    public void Default_hides_graphics_audio_models(string path, string kind)
    {
        var skipped = CompatchFileFilter.Default.Check(path);
        Assert.NotNull(skipped);
        Assert.Equal(SkipReason.HiddenKind, skipped!.Reason);
        Assert.Equal(kind, skipped.Kind);
    }

    [Fact]
    public void Scripts_pass()
    {
        Assert.Null(CompatchFileFilter.Default.Check("in_game/common/x.txt"));
    }

    [Theory]
    [InlineData(".metadata/metadata.json")]
    [InlineData("descriptor.mod")]
    [InlineData("thumbnail.png")]
    public void Metadata_is_always_skipped(string path)
    {
        Assert.Equal(SkipReason.Metadata, CompatchFileFilter.Default.Check(path)!.Reason);
    }

    [Fact]
    public void Locked_paths_are_skipped_only_when_asked()
    {
        var on = new CompatchFileFilter(CompatchFileFilter.DefaultHiddenExtensions, new[] { "in_game/common/Buildings" }, skipLocked: true);
        var off = new CompatchFileFilter(CompatchFileFilter.DefaultHiddenExtensions, new[] { "in_game/common/Buildings" }, skipLocked: false);
        Assert.Equal(SkipReason.Locked, on.Check("in_game/common/buildings/a.txt")!.Reason);
        Assert.Null(off.Check("in_game/common/buildings/a.txt"));
        Assert.True(off.IsLocked("in_game/common/buildings/a.txt"));
        Assert.False(off.IsLocked("in_game/common/buildings_extra/a.txt"));
    }

    [Fact]
    public void Empty_locked_path_locks_the_whole_mod()
    {
        var filter = new CompatchFileFilter(CompatchFileFilter.DefaultHiddenExtensions, new[] { string.Empty }, skipLocked: true);
        Assert.Equal(SkipReason.Locked, filter.Check("in_game/common/a.txt")!.Reason);
    }
}
