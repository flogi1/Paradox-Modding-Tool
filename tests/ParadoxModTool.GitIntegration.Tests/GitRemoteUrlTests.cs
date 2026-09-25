using Xunit;

namespace ParadoxModTool.GitIntegration.Tests;

public class GitRemoteUrlTests
{
    [Theory]
    [InlineData("https://github.com/flogi/mod.git", "flogi/mod")]
    [InlineData("https://github.com/flogi/mod", "flogi/mod")]
    [InlineData("https://github.com/flogi/mod/", "flogi/mod")]
    [InlineData("https://user:geheim@github.com/flogi/mod.git", "flogi/mod")]
    [InlineData("git@github.com:flogi/mod.git", "flogi/mod")]
    [InlineData("ssh://git@github.com/flogi/mod.git", "flogi/mod")]
    [InlineData("ssh://git@github.com:22/flogi/mod", "flogi/mod")]
    [InlineData("HTTPS://GitHub.com/Flogi/Mod.Git", "Flogi/Mod")]
    public void ToRepo_ErkenntGitHubAdressen(string url, string expected) => Assert.Equal(expected, GitRemoteUrl.ToRepo(url));

    [Theory]
    [InlineData("https://gitlab.com/flogi/mod.git")]
    [InlineData("https://github.com/flogi")]
    [InlineData("kein url")]
    [InlineData("")]
    [InlineData(null)]
    public void ToRepo_ImmerNullBeiAnderemHostOderMuell(string? url) => Assert.Null(GitRemoteUrl.ToRepo(url));
}
