using System.IO.Compression;
using ParadoxModTool.Mods;
using Xunit;
using static ParadoxModTool.GitIntegration.Tests.FakeProcessRunner;

namespace ParadoxModTool.GitIntegration.Tests;

public class GitHubPublisherTests : IDisposable
{
    private const string RepoJson = "{\"defaultBranchRef\":{\"name\":\"main\"},\"isPrivate\":false,\"viewerPermission\":\"WRITE\"}";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-pub-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _fake = new();

    public GitHubPublisherTests()
    {
        Put("mod/.metadata/metadata.json");
        Put("mod/common/a.txt");
        _fake.OnContains("repo view", Ok(RepoJson));
        _fake.OnContains("release view", Fail(1, "release not found"));
        _fake.OnContains("release create", Ok("https://github.com/o/r/releases/tag/v2.0.5"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string Mod => Path.Combine(_dir, "mod");

    private string Temp => Path.Combine(_dir, "temp");

    private void Put(string relative)
    {
        var path = Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
    }

    private static GitHubRepoEntry Repo(string name = "o/r", string format = "v{version}") => new() { Repo = name, TagFormat = format };

    private GitHubPublishRequest Request(IReadOnlyList<GitHubRepoEntry>? repos = null, bool update = false, bool tagHead = true, GitState? git = null, IReadOnlyList<GitHubZipVariant>? variants = null) =>
        new("Meine Mod", "2.0.5", Mod, repos ?? new[] { Repo() }, "Meine Mod 2.0.5", "### General\n\n- Fixed: a", false, false, update, variants ?? new[] { new GitHubZipVariant { Name = "Standard" } }, git, tagHead, Temp);

    private Task<GitHubPublishResult> Publish(GitHubPublishRequest request, CancellationToken token = default) =>
        new GitHubPublisher(new GhClient(_fake, "gh")).PublishAsync(request, null, token);

    private static GitState Git(bool pushed = true, string? originRepo = "o/r") =>
        new("C:/mod", "deadbeef00", "s", "main", null, originRepo, "origin/main", 0, 0, pushed, Array.Empty<GitChange>());

    [Fact]
    public async Task NeuesRelease_LegtAnMitZipNotesUndStandardBranch_UndRaeumtAuf()
    {
        string? notes = null;
        string[]? zipEntries = null;
        _fake.Handler = spec =>
        {
            if (FakeProcessRunner.Cmd(spec).Contains("release create"))
            {
                var args = spec.Arguments.ToList();
                notes = File.ReadAllText(args[args.IndexOf("--notes-file") + 1]);
                var zip = args.Single(a => a.EndsWith(".zip", StringComparison.Ordinal));
                using var archive = ZipFile.OpenRead(zip);
                zipEntries = archive.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
                return Ok("https://github.com/o/r/releases/tag/v2.0.5");
            }

            return FakeProcessRunner.Cmd(spec).Contains("repo view") ? Ok(RepoJson) : Fail(1, "release not found");
        };

        var result = await Publish(Request());

        Assert.True(result.AllSucceeded);
        var repo = Assert.Single(result.Repos);
        Assert.Equal(GitHubRepoOutcome.Created, repo.Outcome);
        Assert.Equal("v2.0.5", repo.Tag);
        Assert.Equal("https://github.com/o/r/releases/tag/v2.0.5", repo.Url);
        Assert.Equal("### General\n\n- Fixed: a", notes);
        Assert.Equal(new[] { ".metadata/metadata.json", "common/a.txt" }, zipEntries);
        var create = _fake.Calls.Single(c => Cmd(c).Contains("release create")).Arguments.ToList();
        Assert.Equal("main", create[create.IndexOf("--target") + 1]);
        Assert.Equal("Meine Mod 2.0.5", create[create.IndexOf("--title") + 1]);
        Assert.False(Directory.Exists(Temp) && Directory.EnumerateFileSystemEntries(Temp).Any());
    }

    [Fact]
    public async Task Tag_ZeigtAufGepushtenHead_NurFuerDasOriginRepo_UndNurWennErlaubt()
    {
        await Publish(Request(git: Git()));
        await Publish(Request(git: Git(pushed: false)));
        await Publish(Request(git: Git(originRepo: "x/y")));
        await Publish(Request(git: Git(), tagHead: false));

        var targets = _fake.Calls
            .Where(c => Cmd(c).Contains("release create"))
            .Select(c => c.Arguments.ToList())
            .Select(a => a[a.IndexOf("--target") + 1])
            .ToArray();
        Assert.Equal(new[] { "deadbeef00", "main", "main", "main" }, targets);
    }

    [Fact]
    public async Task VorhandenesRelease_OhneUpdate_WirdUebersprungen_MitUpdate_BearbeitetUndLaedtMitClobberHoch()
    {
        _fake.OnContains("release view", Ok("{\"tagName\":\"v2.0.5\",\"url\":\"https://github.com/o/r/releases/tag/v2.0.5\",\"isDraft\":false}"));

        var skipped = await Publish(Request());
        Assert.Equal(GitHubRepoOutcome.Skipped, skipped.Repos[0].Outcome);
        Assert.DoesNotContain(_fake.Calls, c => Cmd(c).Contains("release create") || Cmd(c).Contains("release edit"));
        Assert.False(skipped.AllSucceeded);

        var updated = await Publish(Request(update: true));

        Assert.Equal(GitHubRepoOutcome.Updated, updated.Repos[0].Outcome);
        Assert.Contains(_fake.Calls, c => Cmd(c).Contains("release edit"));
        Assert.Contains(_fake.Calls, c => Cmd(c).Contains("release upload") && Cmd(c).EndsWith("--clobber", StringComparison.Ordinal));
        Assert.DoesNotContain(_fake.Calls, c => Cmd(c).Contains("release create"));
    }

    [Fact]
    public async Task KeinSchreibrecht_IstFailed_OhneAnlegen()
    {
        _fake.OnContains("repo view", Ok("{\"defaultBranchRef\":{\"name\":\"main\"},\"isPrivate\":false,\"viewerPermission\":\"READ\"}"));

        var result = await Publish(Request());

        Assert.Equal(GitHubRepoOutcome.Failed, result.Repos[0].Outcome);
        Assert.Contains("Schreibrecht", result.Repos[0].Message);
        Assert.DoesNotContain(_fake.Calls, c => Cmd(c).Contains("release create"));
    }

    [Fact]
    public async Task LeeresRepo_IstFailed_MitHinweisAufErstenCommit_OhneAnlegen()
    {
        _fake.OnContains("repo view", Ok("{\"defaultBranchRef\":null,\"isPrivate\":false,\"viewerPermission\":\"ADMIN\"}"));

        var result = await Publish(Request());

        Assert.Equal(GitHubRepoOutcome.Failed, result.Repos[0].Outcome);
        Assert.Contains("leer", result.Repos[0].Message);
        Assert.Contains("Commit", result.Repos[0].Message);
        Assert.DoesNotContain(_fake.Calls, c => Cmd(c).Contains("release create"));
    }

    [Fact]
    public async Task MehrereRepos_EinFehlerStopptDieAnderenNicht_JedesMitEigenemTag()
    {
        _fake.OnContains("repo view x/y", Fail(1, "Could not resolve to a Repository"));
        _fake.OnContains("release create", Ok("https://github.com/o/r/releases/tag/rel-2.0.5"));

        var result = await Publish(Request(new[] { Repo("x/y"), Repo("o/r", "rel-{version}") }));

        Assert.Equal(GitHubRepoOutcome.Failed, result.Repos[0].Outcome);
        Assert.Equal(GitHubRepoOutcome.Created, result.Repos[1].Outcome);
        Assert.Equal("rel-2.0.5", result.Repos[1].Tag);
        Assert.False(result.AllSucceeded);
    }

    [Fact]
    public async Task UngueltigerTag_IstFailed()
    {
        var result = await Publish(Request(new[] { Repo("o/r", "v {version}") }));

        Assert.Equal(GitHubRepoOutcome.Failed, result.Repos[0].Outcome);
        Assert.Contains("ungültig", result.Repos[0].Message);
    }

    [Fact]
    public async Task Abbruch_MarkiertDieRestlichenReposAlsCancelled()
    {
        using var cts = new CancellationTokenSource();
        _fake.Handler = spec =>
        {
            if (Cmd(spec).Contains("release create"))
            {
                cts.Cancel();
                return Ok("https://github.com/o/r/releases/tag/v2.0.5");
            }

            return Cmd(spec).Contains("repo view") ? Ok(RepoJson) : Fail(1, "release not found");
        };

        var result = await Publish(Request(new[] { Repo("o/r"), Repo("a/b") }), cts.Token);

        Assert.Equal(GitHubRepoOutcome.Created, result.Repos[0].Outcome);
        Assert.Equal(GitHubRepoOutcome.Cancelled, result.Repos[1].Outcome);
    }

    [Fact]
    public async Task LeereVariante_ErzeugtEinProblem_UndKeinRepoAufruf()
    {
        var empty = new[] { new GitHubZipVariant { Name = "Leer", OnlyFolders = { "gibt-es-nicht" } } };

        var result = await Publish(Request(variants: empty));

        Assert.NotNull(result.Problem);
        Assert.Empty(result.Repos);
        Assert.Empty(_fake.Calls);
    }

    [Fact]
    public async Task OhneVariante_WirdOhneAnhangVeroeffentlicht()
    {
        var result = await Publish(Request(variants: Array.Empty<GitHubZipVariant>()));

        Assert.True(result.AllSucceeded);
        var create = _fake.Calls.Single(c => Cmd(c).Contains("release create")).Arguments;
        Assert.DoesNotContain(create, a => a.EndsWith(".zip", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EntwurfUndVorabversion_GehenAlsFlagsAnGh()
    {
        var request = Request() with { Draft = true, Prerelease = true };

        await Publish(request);

        var create = _fake.Calls.Single(c => Cmd(c).Contains("release create")).Arguments;
        Assert.Contains("--draft", create);
        Assert.Contains("--prerelease", create);
    }

    [Fact]
    public async Task KeinArgumentEnthaeltEinTokenOderPasswort()
    {
        await Publish(Request());

        Assert.All(_fake.Calls, c => Assert.DoesNotContain(c.Arguments, a => a.Contains("token", StringComparison.OrdinalIgnoreCase) || a.Contains("password", StringComparison.OrdinalIgnoreCase)));
    }
}
