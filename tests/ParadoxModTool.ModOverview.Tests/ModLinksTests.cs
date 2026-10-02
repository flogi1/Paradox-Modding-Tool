using ParadoxModTool.Mods;
using Xunit;

namespace ParadoxModTool.ModOverview.Tests;

public class ModLinksTests
{
    [Fact]
    public void WorkshopFromId()
    {
        var links = ModLinks.For(new ModInfo { Id = "m1", SteamWorkshopId = "2656711777" }, null);
        Assert.Equal("https://steamcommunity.com/sharedfiles/filedetails/?id=2656711777", links.Workshop);

        Assert.Null(ModLinks.For(new ModInfo { Id = "m1" }, null).Workshop);
    }

    [Fact]
    public void GitHubPrefersEnabledRepo()
    {
        var entry = new ModLinkEntry { GameId = "eu4", ModId = "m1", GitHubUrl = "https://github.com/other/x", ForumUrl = "https://forum.paradoxplaza.com/f" };
        var mod = new ModInfo
        {
            Id = "m1",
            GitHubRepos = { new GitHubRepoEntry { Repo = "a/old", Enabled = false }, new GitHubRepoEntry { Repo = "flogi/iv", Enabled = true } },
        };

        var links = ModLinks.For(mod, entry);
        Assert.Equal("https://github.com/flogi/iv", links.GitHub);
        Assert.True(links.GitHubFromRepo);
        Assert.Equal("https://forum.paradoxplaza.com/f", links.Forum);

        var withoutRepo = ModLinks.For(new ModInfo { Id = "m1", GitHubRepos = { new GitHubRepoEntry { Repo = "a/old", Enabled = false } } }, entry);
        Assert.Equal("https://github.com/other/x", withoutRepo.GitHub);
        Assert.False(withoutRepo.GitHubFromRepo);
    }
}
