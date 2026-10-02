using ParadoxModTool.Mods;
using ParadoxModTool.SteamIntegration;
using Xunit;

namespace ParadoxModTool.ModOverview.Tests;

public class ThumbnailCacheTests : TempDirTests
{
    private static SteamCacheEntry Entry(DateTimeOffset? updated) =>
        new() { Id = "42", State = WorkshopPageState.Ok, PreviewUrl = "https://x.test/ugc/1/ABC/", TimeUpdated = updated };

    [Fact]
    public void CachedFile_ExtensionFromUrl()
    {
        var thumbs = Make("thumbs");

        Assert.Equal(Path.Combine(thumbs, "42.png"), ThumbnailCache.CachedFile(thumbs, "42", "https://x.test/a/b.png?x=1"));
        Assert.Equal(Path.Combine(thumbs, "42.jpg"), ThumbnailCache.CachedFile(thumbs, "42", "https://x.test/ugc/1/ABC/"));
    }

    [Fact]
    public void LocalPictureWins()
    {
        var modFolder = Make("mod");
        var picture = WriteFile(Path.Combine("mod", "thumbnail.png"), "png");
        var thumbs = Make("thumbs");
        WriteFile(Path.Combine("thumbs", "42.jpg"), "jpg");
        var metadata = new ModMetadata("M", "1", "1.*", modFolder) { Picture = "thumbnail.png" };

        Assert.Equal(picture, ThumbnailCache.Resolve(metadata, thumbs, Entry(null)));
        Assert.Equal(Path.Combine(thumbs, "42.jpg"), ThumbnailCache.Resolve(metadata with { Picture = "" }, thumbs, Entry(null)));
        Assert.Null(ThumbnailCache.Resolve(null, thumbs, null));
    }

    [Fact]
    public void NeedsDownload_WhenMissingOrUpdated()
    {
        var thumbs = Make("thumbs");
        var file = Path.Combine(thumbs, "42.jpg");
        var old = DateTimeOffset.FromUnixTimeSeconds(1000);

        Assert.True(ThumbnailCache.NeedsDownload(file, Entry(old), old));

        File.WriteAllText(file, "jpg");
        Assert.False(ThumbnailCache.NeedsDownload(file, Entry(old), old));
        Assert.True(ThumbnailCache.NeedsDownload(file, Entry(old.AddDays(1)), old));
    }
}
