using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class PublishReadinessTests
{
    private static PublishReadinessInput Good() => new(
        AppId: "3450310",
        Metadata: new ModMetadata("Mod", "2.0.5", "1.*", @"C:\m"),
        MetadataUnsaved: false,
        WorkshopIdText: "3456789012",
        SteamCmdReady: true,
        UserName: "flogi",
        VersionForChangelog: "2.0.5",
        ChangelogHasVersion: true,
        ChangelogExists: true,
        IsFirstPublish: false,
        PreviewExists: true,
        PublishedOnSteam: false,
        NothingToUpload: false);

    [Fact]
    public void AllesInOrdnung_KeineHinweise_UndVeroeffentlichenErlaubt()
    {
        var readiness = PublishReadiness.Evaluate(Good());

        Assert.Empty(readiness.Hints);
        Assert.True(readiness.CanPublish);
    }

    [Theory]
    [MemberData(nameof(BlockingCases))]
    public void BlockierendeHinweise_SperrenDasVeroeffentlichen(PublishReadinessInput input, string expected)
    {
        var readiness = PublishReadiness.Evaluate(input);

        var hint = Assert.Single(readiness.Hints, h => h.Text.Contains(expected));
        Assert.True(hint.Blocking);
        Assert.False(readiness.CanPublish);
    }

    public static IEnumerable<object[]> BlockingCases()
    {
        yield return new object[] { Good() with { AppId = null }, "AppID" };
        yield return new object[] { Good() with { Metadata = null }, "metadata.json" };
        yield return new object[] { Good() with { Metadata = new ModMetadata("Mod", "abc", "", @"C:\m") }, "gültige Version" };
        yield return new object[] { Good() with { MetadataUnsaved = true }, "ungespeicherte" };
        yield return new object[] { Good() with { WorkshopIdText = "12ab" }, "Workshop-ID" };
        yield return new object[] { Good() with { SteamCmdReady = false }, "SteamCMD" };
        yield return new object[] { Good() with { UserName = " " }, "Benutzername" };
        yield return new object[] { Good() with { IsFirstPublish = true, PreviewExists = false }, "Vorschaubild" };
        yield return new object[] { Good() with { NothingToUpload = true }, "keine Dateien" };
    }

    [Fact]
    public void WorkshopIdGehoertSchonEinerAnderenMod_SperrtMitNamenDerMod()
    {
        var readiness = PublishReadiness.Evaluate(Good() with { WorkshopIdOwner = "Andere Mod" });

        Assert.False(readiness.CanPublish);
        Assert.Contains(readiness.Hints, h => h.Blocking && h.Text.Contains("Andere Mod") && h.Text.Contains("überschreiben"));
    }

    [Fact]
    public void WorkshopIdOhneAndereMod_MachtKeinenHinweis()
    {
        Assert.Empty(PublishReadiness.Evaluate(Good() with { WorkshopIdOwner = null }).Hints);
    }

    [Fact]
    public void SteamClient_BrauchtWederSteamCmdNochBenutzername()
    {
        var readiness = PublishReadiness.Evaluate(Good() with { UseSteamClient = true, SteamCmdReady = false, UserName = " " });

        Assert.True(readiness.CanPublish);
        Assert.DoesNotContain(readiness.Hints, h => h.Text.Contains("SteamCMD") || h.Text.Contains("Benutzername"));
    }

    [Fact]
    public void SteamClient_MitProblem_SperrtMitDemText()
    {
        var readiness = PublishReadiness.Evaluate(Good() with { UseSteamClient = true, ClientProblem = "Steam läuft nicht." });

        Assert.False(readiness.CanPublish);
        Assert.Contains(readiness.Hints, h => h.Blocking && h.Text == "Steam läuft nicht.");
    }

    [Fact]
    public void Hinweise_OhneSperre()
    {
        var missing = PublishReadiness.Evaluate(Good() with { ChangelogHasVersion = false, VersionForChangelog = "2.0.6" });
        var hint = Assert.Single(missing.Hints);
        Assert.False(hint.Blocking);
        Assert.Contains("2.0.6", hint.Text);
        Assert.True(missing.CanPublish);

        var published = PublishReadiness.Evaluate(Good() with { PublishedOnSteam = true });
        Assert.Contains("schon auf Steam", Assert.Single(published.Hints).Text);
        Assert.True(published.CanPublish);
    }

    [Fact]
    public void VorschaubildFehlt_NurBeimErstenVeroeffentlichenEinProblem()
    {
        Assert.Empty(PublishReadiness.Evaluate(Good() with { IsFirstPublish = false, PreviewExists = false }).Hints);
    }

    [Fact]
    public void KeinChangelog_KeinChangelogHinweis()
    {
        Assert.Empty(PublishReadiness.Evaluate(Good() with { ChangelogExists = false, ChangelogHasVersion = false }).Hints);
    }

    [Fact]
    public void WorkshopIdLeerOderNull_IstErlaubt()
    {
        Assert.True(PublishReadiness.Evaluate(Good() with { WorkshopIdText = "", IsFirstPublish = true }).CanPublish);
        Assert.True(PublishReadiness.Evaluate(Good() with { WorkshopIdText = "0", IsFirstPublish = true }).CanPublish);
        Assert.True(PublishReadiness.Evaluate(Good() with { WorkshopIdText = null, IsFirstPublish = true }).CanPublish);
    }
}
