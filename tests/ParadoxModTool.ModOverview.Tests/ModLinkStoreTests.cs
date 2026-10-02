using Xunit;

namespace ParadoxModTool.ModOverview.Tests;

public class ModLinkStoreTests : TempDirTests
{
    private string LinksFile => new OverviewPaths(Dir).LinksFile;

    [Fact]
    public void RoundTrip()
    {
        ModLinkStore.Set(LinksFile, "eu4", "m1", "https://forum.paradoxplaza.com/x", "http://discord.gg/abc", "https://github.com/a/b");

        var entry = ModLinkStore.Find(ModLinkStore.Load(LinksFile), "eu4", "m1");

        Assert.NotNull(entry);
        Assert.Equal("https://forum.paradoxplaza.com/x", entry!.ForumUrl);
        Assert.Equal("http://discord.gg/abc", entry.DiscordUrl);
        Assert.Equal("https://github.com/a/b", entry.GitHubUrl);
        Assert.Null(ModLinkStore.Find(ModLinkStore.Load(LinksFile), "eu5", "m1"));
    }

    [Fact]
    public void EmptyValueRemovesField()
    {
        ModLinkStore.Set(LinksFile, "eu4", "m1", "https://forum.paradoxplaza.com/x", "http://discord.gg/abc", null);
        ModLinkStore.Set(LinksFile, "eu4", "m1", "", "http://discord.gg/abc", null);

        var entry = ModLinkStore.Find(ModLinkStore.Load(LinksFile), "eu4", "m1");
        Assert.Null(entry!.ForumUrl);
        Assert.Equal("http://discord.gg/abc", entry.DiscordUrl);

        ModLinkStore.Set(LinksFile, "eu4", "m1", "", " ", null);
        Assert.Empty(ModLinkStore.Load(LinksFile));
    }

    [Fact]
    public void RejectsNonHttp()
    {
        Assert.False(ModLinkStore.IsValidUrl("ftp://x"));
        Assert.False(ModLinkStore.IsValidUrl("forum.paradoxplaza.com"));
        Assert.False(ModLinkStore.IsValidUrl("javascript:alert(1)"));
        Assert.True(ModLinkStore.IsValidUrl("https://forum.paradoxplaza.com/x"));
        Assert.True(ModLinkStore.IsValidUrl("http://discord.gg/abc"));

        ModLinkStore.Set(LinksFile, "eu4", "m1", "https://forum.paradoxplaza.com/x", null, null);
        var before = File.ReadAllText(LinksFile);
        Assert.Throws<ArgumentException>(() => ModLinkStore.Set(LinksFile, "eu4", "m1", "https://forum.paradoxplaza.com/x", "javascript:alert(1)", null));
        Assert.Equal(before, File.ReadAllText(LinksFile));
    }

    [Fact]
    public void WorkshopIdSurvivesLinkEdits()
    {
        ModLinkStore.SetWorkshopId(LinksFile, "eu5", "m1", "3599737490");
        ModLinkStore.Set(LinksFile, "eu5", "m1", "https://forum.paradoxplaza.com/x", null, null);
        ModLinkStore.Set(LinksFile, "eu5", "m1", null, null, null);

        var entry = ModLinkStore.Find(ModLinkStore.Load(LinksFile), "eu5", "m1");
        Assert.Equal("3599737490", entry!.WorkshopId);
        Assert.Null(entry.ForumUrl);

        Assert.Throws<ArgumentException>(() => ModLinkStore.SetWorkshopId(LinksFile, "eu5", "m1", "12ab"));
        ModLinkStore.SetWorkshopId(LinksFile, "eu5", "m1", null);
        Assert.Empty(ModLinkStore.Load(LinksFile));
    }

    [Fact]
    public void CorruptFileIsEmpty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LinksFile)!);
        File.WriteAllText(LinksFile, "{nope");

        Assert.Empty(ModLinkStore.Load(LinksFile));

        ModLinkStore.Set(LinksFile, "eu4", "m1", "https://forum.paradoxplaza.com/x", null, null);
        Assert.Single(ModLinkStore.Load(LinksFile));
    }
}
