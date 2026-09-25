using System.ComponentModel;
using Xunit;
using static ParadoxModTool.GitIntegration.Tests.FakeProcessRunner;

namespace ParadoxModTool.GitIntegration.Tests;

public class GitClientTests
{
    private const string Folder = @"C:\mod";

    private static FakeProcessRunner Repo()
    {
        var fake = new FakeProcessRunner();
        fake.On("rev-parse --show-toplevel", Ok("C:/mod"));
        fake.On("rev-parse HEAD", Ok("abcdef1234567890abcdef1234567890abcdef12"));
        fake.On("remote get-url origin", Ok("https://github.com/o/r.git"));
        fake.On("rev-parse --abbrev-ref HEAD", Ok("main"));
        fake.On("log -1 --format=%s", Ok("Letzter Commit"));
        fake.On("rev-parse --abbrev-ref @{u}", Ok("origin/main"));
        fake.On("rev-list --left-right --count HEAD...@{u}", Ok("0\t0"));
        fake.On("branch -r --contains HEAD", Ok("  origin/main"));
        fake.On("status --porcelain", Ok());
        return fake;
    }

    private static GitClient Client(FakeProcessRunner fake) => new(fake);

    [Fact]
    public async Task Inspect_Sauber_LiestAllesUndRuftNurLesendAuf()
    {
        var fake = Repo();

        var state = await Client(fake).InspectAsync(Folder, default);

        Assert.NotNull(state);
        Assert.Equal("C:/mod", state!.TopLevel);
        Assert.Equal("main", state.Branch);
        Assert.Equal("origin/main", state.Upstream);
        Assert.Equal("o/r", state.OriginRepo);
        Assert.Equal("Letzter Commit", state.HeadSubject);
        Assert.Equal("abcdef12", state.ShortHead);
        Assert.True(state.HeadPushed);
        Assert.True(state.IsClean);
        Assert.Equal((0, 0), (state.Ahead, state.Behind));
        Assert.All(fake.Calls, c =>
        {
            Assert.Equal("git", c.FileName);
            Assert.Equal(new[] { "-C", Folder }, c.Arguments.Take(2));
            Assert.Equal("0", c.Environment!["GIT_TERMINAL_PROMPT"]);
            Assert.DoesNotContain(c.Arguments, a => a is "add" or "commit" or "push" or "checkout" or "reset");
        });
    }

    [Fact]
    public async Task Inspect_Geaendert_ZaehltVorUndZurueck_UndLiestAenderungen()
    {
        var fake = Repo();
        fake.On("status --porcelain", Ok(" M common/a.txt", "?? neu datei.txt", "R  alt.txt -> neu.txt", "?? \"ue.txt\""));
        fake.On("rev-list --left-right --count HEAD...@{u}", Ok("2\t3"));
        fake.On("branch -r --contains HEAD", Ok());

        var state = await Client(fake).InspectAsync(Folder, default);

        Assert.Equal(4, state!.Changes.Count);
        Assert.Equal(new GitChange("M", "common/a.txt"), state.Changes[0]);
        Assert.Equal(new GitChange("??", "neu datei.txt"), state.Changes[1]);
        Assert.Equal("ue.txt", state.Changes[3].Path);
        Assert.False(state.IsClean);
        Assert.Equal((2, 3), (state.Ahead, state.Behind));
        Assert.False(state.HeadPushed);
    }

    [Fact]
    public async Task Inspect_OhneUpstream_UndLosgeloest()
    {
        var fake = Repo();
        fake.On("rev-parse --abbrev-ref @{u}", Fail(128, "fatal: no upstream configured"));
        fake.On("rev-parse --abbrev-ref HEAD", Ok("HEAD"));

        var state = await Client(fake).InspectAsync(Folder, default);

        Assert.Null(state!.Upstream);
        Assert.Null(state.Branch);
        Assert.Equal((0, 0), (state.Ahead, state.Behind));
    }

    [Fact]
    public async Task Inspect_KeinGitOrdner_KeinCommit_UndGitFehlt_SindNull()
    {
        var notGit = new FakeProcessRunner();
        notGit.On("rev-parse --show-toplevel", Fail(128, "fatal: not a git repository"));
        Assert.Null(await Client(notGit).InspectAsync(Folder, default));

        var noCommit = Repo();
        noCommit.On("rev-parse HEAD", Fail(128, "fatal: ambiguous argument 'HEAD'"));
        Assert.Null(await Client(noCommit).InspectAsync(Folder, default));

        var missing = new FakeProcessRunner { Handler = _ => throw new Win32Exception("nicht gefunden") };
        Assert.Null(await Client(missing).InspectAsync(Folder, default));
    }

    [Fact]
    public async Task Inspect_OhneOrigin_HatKeinRepo()
    {
        var fake = Repo();
        fake.On("remote get-url origin", Fail(2, "error: No such remote 'origin'"));

        var state = await Client(fake).InspectAsync(Folder, default);

        Assert.Null(state!.Origin);
        Assert.Null(state.OriginRepo);
    }

    [Fact]
    public async Task Commit_FuegtAllesHinzuUndCommittetMitNachricht()
    {
        var fake = new FakeProcessRunner();

        var result = await Client(fake).CommitAllAsync(Folder, "Release 2.0.5", default);

        Assert.True(result.Success);
        Assert.Equal(new[] { "-C " + Folder + " add -A", "-C " + Folder + " commit -m Release 2.0.5" }, fake.Calls.Select(Cmd));
        Assert.Equal(new[] { "-C", Folder, "commit", "-m", "Release 2.0.5" }, fake.Calls[1].Arguments);
    }

    [Fact]
    public async Task Commit_OhneNachricht_RuftGitNichtAuf()
    {
        var fake = new FakeProcessRunner();

        var result = await Client(fake).CommitAllAsync(Folder, "  ", default);

        Assert.False(result.Success);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task Commit_Fehler_ZeigtDieAusgabeVonGit()
    {
        var fake = new FakeProcessRunner();
        fake.On("commit -m x", Fail(1, "Author identity unknown"));

        var result = await Client(fake).CommitAllAsync(Folder, "x", default);

        Assert.False(result.Success);
        Assert.Contains("Author identity unknown", result.Message);
    }

    private static GitState State(string? branch = "main", string? upstream = "origin/main", int behind = 0) =>
        new("C:/mod", "abc", "s", branch, "https://github.com/o/r.git", "o/r", upstream, 1, behind, false, Array.Empty<GitChange>());

    [Fact]
    public async Task Push_MitUpstream_UndOhne_NieMitForce()
    {
        var fake = new FakeProcessRunner();

        var withUpstream = await Client(fake).PushAsync(Folder, State(), default);
        var withoutUpstream = await Client(fake).PushAsync(Folder, State(upstream: null), default);

        Assert.True(withUpstream.Success);
        Assert.True(withoutUpstream.Success);
        Assert.Equal(new[] { "-C", Folder, "push" }, fake.Calls[0].Arguments);
        Assert.Equal(new[] { "-C", Folder, "push", "-u", "origin", "main" }, fake.Calls[1].Arguments);
        Assert.All(fake.Calls, c => Assert.DoesNotContain(c.Arguments, a => a is "--force" or "-f" or "--force-with-lease"));
    }

    [Fact]
    public async Task Push_HintenLosgeloest_RuftGitNichtAuf()
    {
        var fake = new FakeProcessRunner();

        var behind = await Client(fake).PushAsync(Folder, State(behind: 2), default);
        var detached = await Client(fake).PushAsync(Folder, State(branch: null), default);

        Assert.False(behind.Success);
        Assert.Contains("hinter", behind.Message);
        Assert.False(detached.Success);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task Push_AnmeldungNoetig_GibtHinweisOhneToken()
    {
        var fake = new FakeProcessRunner();
        fake.On("push", Fail(128, "fatal: could not read Username for 'https://github.com': terminal prompts disabled"));

        var result = await Client(fake).PushAsync(Folder, State(), default);

        Assert.False(result.Success);
        Assert.Contains("Anmeldung", result.Message);
    }

    [Fact]
    public void Warnungen_ZeigenAlleProbleme()
    {
        Assert.Empty(GitWarnings.From(null));
        Assert.Empty(GitWarnings.From(State() with { HeadPushed = true }));

        var warnings = GitWarnings.From(State(branch: null, behind: 2) with { Changes = new[] { new GitChange("M", "a") }, HeadPushed = false });

        Assert.Equal(4, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("ungespeicherte"));
        Assert.Contains(warnings, w => w.Contains("nicht gepusht"));
        Assert.Contains(warnings, w => w.Contains("hinter"));
        Assert.Contains(warnings, w => w.Contains("losgelöst"));
    }
}
