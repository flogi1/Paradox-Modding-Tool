using ParadoxModTool.Mods;
using Xunit;

namespace ParadoxModTool.GitIntegration.Tests;

public class GitHubReadinessTests
{
    private static GitHubReadinessInput Good() => new(
        GhFound: true,
        LoggedIn: true,
        VersionValid: true,
        ChangelogExists: true,
        ChangelogHasVersion: true,
        Title: "Mod 1.0",
        Notes: "- a",
        SelectedRepoCount: 1,
        Checks: new[] { new GitHubRepoCheck("o/r", "v1.0", null, true, false) },
        UpdateExisting: false,
        ZipProblems: Array.Empty<string>(),
        GitWarnings: Array.Empty<string>());

    [Fact]
    public void AllesInOrdnung_KeineHinweise_UndVeroeffentlichenErlaubt()
    {
        var readiness = GitHubReadiness.Evaluate(Good());

        Assert.Empty(readiness.Hints);
        Assert.True(readiness.CanPublish);
    }

    [Fact]
    public void NichtGeprueftePunkte_SperrenNicht()
    {
        var readiness = GitHubReadiness.Evaluate(Good() with { Checks = new[] { new GitHubRepoCheck("o/r", "v1.0", null, null, null) } });

        Assert.True(readiness.CanPublish);
    }

    public static IEnumerable<object[]> BlockingCases()
    {
        yield return new object[] { Good() with { GhFound = false }, "gh" };
        yield return new object[] { Good() with { LoggedIn = false }, "Anmeldung" };
        yield return new object[] { Good() with { VersionValid = false }, "Version" };
        yield return new object[] { Good() with { SelectedRepoCount = 0, Checks = Array.Empty<GitHubRepoCheck>() }, "Repository angehakt" };
        yield return new object[] { Good() with { Title = " " }, "Titel" };
        yield return new object[] { Good() with { Notes = new string('a', ReleaseNotesFormatter.MaxLength + 1) }, "zu lang" };
        yield return new object[] { Good() with { Checks = new[] { new GitHubRepoCheck("o/r", "v1.0", "Nicht gefunden.", null, null) } }, "o/r: Nicht gefunden." };
        yield return new object[] { Good() with { Checks = new[] { new GitHubRepoCheck("o/r", "v1.0", null, false, false) } }, "Schreibrecht" };
        yield return new object[] { Good() with { Checks = new[] { new GitHubRepoCheck("o/r", "v1.0", null, true, true) } }, "existiert schon" };
        yield return new object[] { Good() with { ZipProblems = new[] { "Die ZIP-Variante „X“ enthält keine Datei." } }, "enthält keine Datei" };
    }

    [Theory]
    [MemberData(nameof(BlockingCases))]
    public void BlockierendeHinweise_SperrenDasVeroeffentlichen(GitHubReadinessInput input, string expected)
    {
        var readiness = GitHubReadiness.Evaluate(input);

        var hint = Assert.Single(readiness.Hints, h => h.Text.Contains(expected));
        Assert.True(hint.Blocking);
        Assert.False(readiness.CanPublish);
    }

    [Fact]
    public void VorhandenerTag_MitUpdate_IstNurEinHinweis()
    {
        var readiness = GitHubReadiness.Evaluate(Good() with { UpdateExisting = true, Checks = new[] { new GitHubRepoCheck("o/r", "v1.0", null, true, true) } });

        var hint = Assert.Single(readiness.Hints);
        Assert.False(hint.Blocking);
        Assert.Contains("aktualisiert", hint.Text);
        Assert.True(readiness.CanPublish);
    }

    [Fact]
    public void ChangelogOhneVersion_UndGitWarnungen_SindNurHinweise()
    {
        var readiness = GitHubReadiness.Evaluate(Good() with { ChangelogHasVersion = false, GitWarnings = new[] { "Im Mod-Ordner gibt es 2 ungespeicherte Änderung(en)." } });

        Assert.Equal(2, readiness.Hints.Count);
        Assert.All(readiness.Hints, h => Assert.False(h.Blocking));
        Assert.True(readiness.CanPublish);
    }
}
