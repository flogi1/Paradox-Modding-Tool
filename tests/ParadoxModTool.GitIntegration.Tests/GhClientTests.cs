using System.ComponentModel;
using Xunit;
using static ParadoxModTool.GitIntegration.Tests.FakeProcessRunner;

namespace ParadoxModTool.GitIntegration.Tests;

public class GhClientTests
{
    private static GhClient Client(FakeProcessRunner fake) => new(fake, @"C:\gh\gh.exe");

    private static string[] LastArgs(FakeProcessRunner fake) => fake.Calls[^1].Arguments.ToArray();

    [Fact]
    public async Task JederAufruf_LaeuftOhneEingabeUndMitGhUmgebung()
    {
        var fake = new FakeProcessRunner();

        await Client(fake).IsLoggedInAsync(default);

        var call = fake.Calls.Single();
        Assert.Equal(@"C:\gh\gh.exe", call.FileName);
        Assert.Equal("1", call.Environment!["GH_PROMPT_DISABLED"]);
        Assert.Equal("1", call.Environment["NO_COLOR"]);
        Assert.Equal("1", call.Environment["GH_NO_UPDATE_NOTIFIER"]);
    }

    [Fact]
    public async Task IsLoggedIn_NachExitCode_UndFehlendesGhIstNichtAngemeldet()
    {
        var ok = new FakeProcessRunner();
        var notLoggedIn = new FakeProcessRunner();
        notLoggedIn.On("auth status --hostname github.com", Fail(1, "You are not logged into any GitHub hosts"));
        var missing = new FakeProcessRunner { Handler = _ => throw new Win32Exception("nicht gefunden") };

        Assert.True(await Client(ok).IsLoggedInAsync(default));
        Assert.Equal(new[] { "auth", "status", "--hostname", "github.com" }, LastArgs(ok));
        Assert.False(await Client(notLoggedIn).IsLoggedInAsync(default));
        Assert.False(await Client(missing).IsLoggedInAsync(default));
    }

    [Fact]
    public async Task GetRepo_LiestStandardBranchRechteUndSichtbarkeit()
    {
        var fake = new FakeProcessRunner();
        fake.On("--json defaultBranchRef,viewerPermission,isPrivate", Ok("{\"defaultBranchRef\":{\"name\":\"main\"},\"isPrivate\":true,\"viewerPermission\":\"ADMIN\"}"));

        var info = await Client(fake).GetRepoAsync("o/r", default);

        Assert.Equal(new[] { "repo", "view", "o/r", "--json", "defaultBranchRef,viewerPermission,isPrivate" }, LastArgs(fake));
        Assert.Equal("main", info.DefaultBranch);
        Assert.True(info.IsPrivate);
        Assert.True(info.CanWrite);
        Assert.False(new GhRepoInfo("main", "READ", false).CanWrite);
        Assert.True(new GhRepoInfo("main", "MAINTAIN", false).CanWrite);
    }

    [Fact]
    public async Task GetRepo_LeeresRepo_HatKeinenStandardBranchUndIstLeer()
    {
        var fake = new FakeProcessRunner();
        fake.On("--json defaultBranchRef,viewerPermission,isPrivate", Ok("{\"defaultBranchRef\":null,\"isPrivate\":false,\"viewerPermission\":\"ADMIN\"}"));

        var info = await Client(fake).GetRepoAsync("o/r", default);

        Assert.True(info.IsEmpty);
        Assert.Equal(string.Empty, info.DefaultBranch);
        Assert.True(info.CanWrite);
        Assert.False(new GhRepoInfo("main", "ADMIN", false).IsEmpty);
    }

    [Fact]
    public async Task GetRepo_Fehler_WirftMitDerMeldungVonGh()
    {
        var fake = new FakeProcessRunner();
        fake.On("--json defaultBranchRef,viewerPermission,isPrivate", Fail(1, "GraphQL: Could not resolve to a Repository with the name 'o/r'."));

        var ex = await Assert.ThrowsAsync<GhException>(() => Client(fake).GetRepoAsync("o/r", default));

        Assert.Contains("Could not resolve", ex.Message);
    }

    [Fact]
    public async Task FindRelease_Gefunden_NichtGefunden_UndAndereFehler()
    {
        var found = new FakeProcessRunner();
        found.On("--json tagName,url,isDraft", Ok("{\"tagName\":\"v1.0\",\"url\":\"https://github.com/o/r/releases/tag/v1.0\",\"isDraft\":false}"));
        var none = new FakeProcessRunner();
        none.On("--json tagName,url,isDraft", Fail(1, "release not found"));
        var broken = new FakeProcessRunner();
        broken.On("--json tagName,url,isDraft", Fail(1, "HTTP 502"));

        var release = await Client(found).FindReleaseAsync("o/r", "v1.0", default);

        Assert.Equal(new[] { "release", "view", "v1.0", "--repo", "o/r", "--json", "tagName,url,isDraft" }, LastArgs(found));
        Assert.Equal(new GhReleaseInfo("v1.0", "https://github.com/o/r/releases/tag/v1.0", false), release);
        Assert.Null(await Client(none).FindReleaseAsync("o/r", "v1.0", default));
        await Assert.ThrowsAsync<GhException>(() => Client(broken).FindReleaseAsync("o/r", "v1.0", default));
    }

    [Fact]
    public async Task CreateRelease_BautDieArgumenteUndLiefertDieAdresse()
    {
        var fake = new FakeProcessRunner();
        fake.OnContains("release create", Ok("https://github.com/o/r/releases/tag/v1.0"));
        var spec = new GhReleaseSpec("o/r", "v1.0", "Mod 1.0", @"C:\t\notes.md", "abc123", Draft: true, Prerelease: true);

        var url = await Client(fake).CreateReleaseAsync(spec, new[] { @"C:\t\a.zip", @"C:\t\b.zip" }, null, default);

        Assert.Equal("https://github.com/o/r/releases/tag/v1.0", url);
        Assert.Equal(
            new[]
            {
                "release", "create", "v1.0", @"C:\t\a.zip", @"C:\t\b.zip", "--repo", "o/r", "--title", "Mod 1.0", "--notes-file", @"C:\t\notes.md",
                "--target", "abc123", "--draft", "--prerelease",
            },
            LastArgs(fake));
        Assert.True(fake.Calls[^1].Timeout >= TimeSpan.FromMinutes(20));
    }

    [Fact]
    public async Task CreateRelease_OhneZielUndSchalter_LaesstDieFlagsWeg()
    {
        var fake = new FakeProcessRunner();

        var url = await Client(fake).CreateReleaseAsync(new GhReleaseSpec("o/r", "v1", "T", "n.md", null, false, false), Array.Empty<string>(), null, default);

        Assert.Equal(string.Empty, url);
        Assert.Equal(new[] { "release", "create", "v1", "--repo", "o/r", "--title", "T", "--notes-file", "n.md" }, LastArgs(fake));
    }

    [Fact]
    public async Task EditRelease_UndUpload_UndDelete()
    {
        var fake = new FakeProcessRunner();
        var client = Client(fake);

        await client.EditReleaseAsync(new GhReleaseSpec("o/r", "v1", "T", "n.md", null, true, false), default);
        Assert.Equal(new[] { "release", "edit", "v1", "--repo", "o/r", "--title", "T", "--notes-file", "n.md", "--draft=true", "--prerelease=false" }, LastArgs(fake));

        await client.UploadAsync("o/r", "v1", new[] { "a.zip" }, null, default);
        Assert.Equal(new[] { "release", "upload", "v1", "a.zip", "--repo", "o/r", "--clobber" }, LastArgs(fake));

        await client.DeleteReleaseAsync("o/r", "v1", cleanupTag: true, default);
        Assert.Equal(new[] { "release", "delete", "v1", "--repo", "o/r", "--yes", "--cleanup-tag" }, LastArgs(fake));

        await client.DeleteReleaseAsync("o/r", "v1", cleanupTag: false, default);
        Assert.Equal(new[] { "release", "delete", "v1", "--repo", "o/r", "--yes" }, LastArgs(fake));
    }

    [Fact]
    public async Task Fehler_WerfenGhException_MitDenLetztenZeilen()
    {
        var fake = new FakeProcessRunner();
        fake.OnContains("release upload", new ProcessResult(1, Array.Empty<string>(), new[] { "eins", "", "zwei", "drei", "vier" }, false));

        var ex = await Assert.ThrowsAsync<GhException>(() => Client(fake).UploadAsync("o/r", "v1", new[] { "a.zip" }, null, default));

        Assert.Equal("zwei drei vier", ex.Message);
    }

    [Fact]
    public async Task Zeitueberschreitung_WirftGhException()
    {
        var fake = new FakeProcessRunner();
        fake.OnContains("release delete", new ProcessResult(-1, Array.Empty<string>(), Array.Empty<string>(), true));

        var ex = await Assert.ThrowsAsync<GhException>(() => Client(fake).DeleteReleaseAsync("o/r", "v1", false, default));

        Assert.Contains("nicht rechtzeitig", ex.Message);
    }

    [Fact]
    public async Task GhFehlt_WirftGhExceptionMitHinweis()
    {
        var fake = new FakeProcessRunner { Handler = _ => throw new Win32Exception("nicht gefunden") };

        var ex = await Assert.ThrowsAsync<GhException>(() => Client(fake).GetRepoAsync("o/r", default));

        Assert.Contains("gh konnte nicht gestartet werden", ex.Message);
    }

    [Fact]
    public async Task ListReleases_ParstJson()
    {
        var fake = new FakeProcessRunner();
        fake.OnContains("release list", Ok("[{\"tagName\":\"v2\",\"name\":\"Zwei\",\"isDraft\":false,\"isPrerelease\":true,\"publishedAt\":\"2026-09-20T10:00:00Z\"},{\"tagName\":\"v1\",\"name\":\"\",\"isDraft\":true,\"isPrerelease\":false,\"publishedAt\":\"\"}]"));

        var items = await Client(fake).ListReleasesAsync("o/r", default);

        Assert.Equal(new[] { "release", "list", "--repo", "o/r", "--limit", "100", "--json", "tagName,name,isDraft,isPrerelease,publishedAt" }, LastArgs(fake));
        Assert.Equal(2, items.Count);
        Assert.Equal(new GhReleaseListItem("v2", "Zwei", false, true, "2026-09-20T10:00:00Z"), items[0]);
        Assert.True(items[1].IsDraft);
    }

    [Fact]
    public async Task Tags_UndBranches_ListenUndLoeschen()
    {
        var fake = new FakeProcessRunner();
        fake.OnContains("/tags?per_page=100", Ok("v2", "v1"));
        fake.OnContains("/branches?per_page=100", Ok("main\tfalse", "release\ttrue"));
        var client = Client(fake);

        var tags = await client.ListTagsAsync("o/r", default);
        Assert.Equal(new[] { "v2", "v1" }, tags);
        Assert.Equal(new[] { "api", "repos/o/r/tags?per_page=100", "--paginate", "--jq", ".[].name" }, fake.Calls[^1].Arguments.ToArray());

        var branches = await client.ListBranchesAsync("o/r", default);
        Assert.Equal(new[] { new GhBranch("main", false), new GhBranch("release", true) }, branches);
        Assert.Equal(new[] { "api", "repos/o/r/branches?per_page=100", "--paginate", "--jq", ".[] | \"\\(.name)\\t\\(.protected)\"" }, fake.Calls[^1].Arguments.ToArray());

        await client.DeleteTagAsync("o/r", "rel/v 1", default);
        Assert.Equal(new[] { "api", "-X", "DELETE", "repos/o/r/git/refs/tags/rel/v%201" }, LastArgs(fake));

        await client.DeleteBranchAsync("o/r", "feature/x", default);
        Assert.Equal(new[] { "api", "-X", "DELETE", "repos/o/r/git/refs/heads/feature/x" }, LastArgs(fake));
    }
}
