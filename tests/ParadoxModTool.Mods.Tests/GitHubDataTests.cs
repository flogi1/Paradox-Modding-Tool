using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class GitHubDataTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-gh-" + Guid.NewGuid().ToString("N"));
    private readonly string _modFolder;

    public GitHubDataTests()
    {
        _modFolder = Path.Combine(_dir, "mod");
        Directory.CreateDirectory(_modFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private (ModRegistry Registry, ModInfo Mod) NewMod()
    {
        var registry = ModRegistry.Load(Path.Combine(_dir, "game"));
        return (registry, registry.Add("Meine Mod", _modFolder));
    }

    [Fact]
    public void Repos_WerdenGespeichertUndGeladen()
    {
        var (registry, mod) = NewMod();

        registry.SetGitHubRepos(mod.Id, new[] { new GitHubRepoEntry { Repo = " flogi/mod ", TagFormat = "", Enabled = false } });

        var back = ModRegistry.Load(Path.Combine(_dir, "game")).Get(mod.Id)!;
        var repo = Assert.Single(back.GitHubRepos);
        Assert.Equal("flogi/mod", repo.Repo);
        Assert.Equal("v{version}", repo.TagFormat);
        Assert.False(repo.Enabled);
    }

    [Fact]
    public void Repos_UngueltigDoppeltOderOhneVersionsplatzhalter_WerfenArgumentException()
    {
        var (registry, mod) = NewMod();

        Assert.Throws<ArgumentException>(() => registry.SetGitHubRepos(mod.Id, new[] { new GitHubRepoEntry { Repo = "kaputt" } }));
        Assert.Throws<ArgumentException>(() => registry.SetGitHubRepos(mod.Id, new[] { new GitHubRepoEntry { Repo = "a/b" }, new GitHubRepoEntry { Repo = "A/B" } }));
        Assert.Throws<ArgumentException>(() => registry.SetGitHubRepos(mod.Id, new[] { new GitHubRepoEntry { Repo = "a/b", TagFormat = "fest" } }));
        Assert.Empty(registry.Get(mod.Id)!.GitHubRepos);
    }

    [Fact]
    public void Varianten_WerdenNormalisiertGespeichert()
    {
        var (registry, mod) = NewMod();
        var variant = new GitHubZipVariant
        {
            Name = " Voll ",
            FileNameFormat = "",
            ExcludedFolders = { " docs ", "" },
            OnlyFolders = { "\\common\\", "gfx/" },
            RootFolder = "  ",
        };

        registry.SetGitHubZipVariants(mod.Id, new[] { variant });

        var saved = ModRegistry.Load(Path.Combine(_dir, "game")).Get(mod.Id)!.GitHubZipVariants.Single();
        Assert.Equal("Voll", saved.Name);
        Assert.Equal("{mod}-{version}.zip", saved.FileNameFormat);
        Assert.Equal(new[] { "docs" }, saved.ExcludedFolders);
        Assert.Equal(new[] { "common", "gfx" }, saved.OnlyFolders);
        Assert.Null(saved.RootFolder);
    }

    [Fact]
    public void Varianten_UngueltigeAngaben_WerfenArgumentException()
    {
        var (registry, mod) = NewMod();

        Assert.Throws<ArgumentException>(() => registry.SetGitHubZipVariants(mod.Id, new[] { new GitHubZipVariant { Name = " " } }));
        Assert.Throws<ArgumentException>(() => registry.SetGitHubZipVariants(mod.Id, new[] { new GitHubZipVariant { Name = "A" }, new GitHubZipVariant { Name = "a" } }));
        Assert.Throws<ArgumentException>(() => registry.SetGitHubZipVariants(mod.Id, new[] { new GitHubZipVariant { Name = "A", OnlyFolders = { "../aussen" } } }));
        Assert.Throws<ArgumentException>(() => registry.SetGitHubZipVariants(mod.Id, new[] { new GitHubZipVariant { Name = "A", RootFolder = "a/b" } }));
    }

    [Fact]
    public void AlteRegisterDatei_OhneFelder_LaedtMitLeerenListen()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "game"));
        File.WriteAllText(
            Path.Combine(_dir, "game", "mods.json"),
            "{ \"version\": 1, \"mods\": [ { \"id\": \"abc\", \"name\": \"M\", \"paths\": [] } ], \"ignoredPaths\": [] }");

        var mod = ModRegistry.Load(Path.Combine(_dir, "game")).Get("abc")!;

        Assert.Empty(mod.GitHubRepos);
        Assert.Empty(mod.GitHubZipVariants);
    }

    [Fact]
    public void Vermerke_ErsetzenGleichesRepoUndTag_UndLassenSichEntfernen()
    {
        var (registry, mod) = NewMod();
        var log = ReleaseLog.Load(registry.DataDirectory(mod.Id));
        log.Add("1.0.0", "2026-09-20");

        log.SetGitHub("1.0.0", new[] { new GitHubPublication { Repo = "a/b", Tag = "v1.0.0", Url = "u1", PublishedAt = "t1" } });
        log.SetGitHub("1.0.0", new[]
        {
            new GitHubPublication { Repo = "A/B", Tag = "v1.0.0", Url = "u2", PublishedAt = "t2" },
            new GitHubPublication { Repo = "c/d", Tag = "v1.0.0", Url = "u3", PublishedAt = "t3" },
        });

        var reloaded = ReleaseLog.Load(registry.DataDirectory(mod.Id)).Find("1.0.0")!;
        Assert.Equal(new[] { "u2", "u3" }, reloaded.GitHub!.Select(p => p.Url).OrderBy(x => x).ToArray());

        Assert.True(log.RemoveGitHub("c/d", "v1.0.0"));
        Assert.False(log.RemoveGitHub("c/d", "v1.0.0"));
        Assert.True(log.RemoveGitHub("a/b", "v1.0.0"));
        Assert.Null(ReleaseLog.Load(registry.DataDirectory(mod.Id)).Find("1.0.0")!.GitHub);
    }

    [Fact]
    public void Vermerk_FuerUnbekannteVersion_WirftInvalidOperation()
    {
        var (registry, mod) = NewMod();
        var log = ReleaseLog.Load(registry.DataDirectory(mod.Id));

        Assert.Throws<InvalidOperationException>(() => log.SetGitHub("9.9", new[] { new GitHubPublication { Repo = "a/b", Tag = "t" } }));
    }
}
