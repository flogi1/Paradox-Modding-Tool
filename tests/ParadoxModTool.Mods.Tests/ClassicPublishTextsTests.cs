using Xunit;

namespace ParadoxModTool.Mods.Tests;

/// <summary>Spiele mit klassischer <c>.mod</c>-Datei (EU IV, CK III) bekommen Hinweise, die die <c>.mod</c>-Datei nennen und nicht die <c>metadata.json</c>.</summary>
public class ClassicPublishTextsTests
{
    private static PublishReadinessInput Good() => new(
        AppId: "236850",
        Metadata: new ModMetadata("Mod", "1.0", "v1.37.*.*", @"C:\m"),
        MetadataUnsaved: false,
        WorkshopIdText: "1703319446",
        SteamCmdReady: true,
        UserName: "flogi",
        VersionForChangelog: "1.0",
        ChangelogHasVersion: true,
        ChangelogExists: true,
        IsFirstPublish: false,
        PreviewExists: true,
        PublishedOnSteam: false,
        NothingToUpload: false,
        ClassicMetadata: true);

    [Fact]
    public void PublishReadiness_klassisch_nennt_die_mod_Datei()
    {
        var texts = new[]
        {
            Good() with { Metadata = null },
            Good() with { Metadata = new ModMetadata("Mod", "", "", @"C:\m") },
            Good() with { MetadataUnsaved = true },
            Good() with { IsFirstPublish = true, PreviewExists = false },
        }.Select(input => Assert.Single(PublishReadiness.Evaluate(input).Hints).Text).ToList();

        Assert.Equal(
            new[]
            {
                "Keine lesbare .mod-Datei gefunden.",
                "Die .mod-Datei enthält keine gültige Version.",
                "Die .mod-Datei hat ungespeicherte Änderungen. Speichere sie vor dem Veröffentlichen.",
                "Das Vorschaubild fehlt. Beim ersten Veröffentlichen braucht die Workshop-Seite eines (Feld picture in der .mod-Datei).",
            },
            texts);
        Assert.DoesNotContain(texts, t => t.Contains("metadata.json"));
    }

    [Fact]
    public void PublishReadiness_ohne_Angabe_bleibt_bei_metadata_json()
    {
        var hint = Assert.Single(PublishReadiness.Evaluate(Good() with { ClassicMetadata = false, Metadata = null }).Hints);

        Assert.Equal("Keine lesbare metadata.json gefunden.", hint.Text);
    }

    private const string DescriptorMissing = "Im Mod-Ordner fehlt descriptor.mod. Steam lädt nur den Ordner hoch; ohne descriptor.mod erkennt der Launcher die Mod nicht.";

    [Fact]
    public void PublishReadiness_klassisch_ohne_descriptor_im_Ordner_sperrt()
    {
        var readiness = PublishReadiness.Evaluate(Good() with { ClassicDescriptorMissing = true });

        var hint = Assert.Single(readiness.Hints);
        Assert.Equal(DescriptorMissing, hint.Text);
        Assert.True(hint.Blocking);
        Assert.False(readiness.CanPublish);
    }

    [Fact]
    public void PublishReadiness_descriptor_Hinweis_nur_klassisch_und_nur_mit_lesbaren_Metadaten()
    {
        Assert.Empty(PublishReadiness.Evaluate(Good() with { ClassicMetadata = false, ClassicDescriptorMissing = true }).Hints);
        Assert.Equal(
            new[] { "Keine lesbare .mod-Datei gefunden." },
            PublishReadiness.Evaluate(Good() with { Metadata = null, ClassicDescriptorMissing = true }).Hints.Select(h => h.Text));
    }

    [Fact]
    public void ReleaseStatus_klassisch_ohne_descriptor_im_Ordner_meldet_es()
    {
        var releases = new[] { new ReleaseEntry { Version = "1.0" } };
        var metadata = new ModMetadata("Mod", "1.0", "", @"C:\m");

        Assert.Equal(new[] { DescriptorMissing }, ReleaseStatus.Evaluate(metadata, null, releases, classic: true, descriptorMissing: true).Hints);
        Assert.Empty(ReleaseStatus.Evaluate(metadata, null, releases, classic: false, descriptorMissing: true).Hints);
        Assert.Empty(ReleaseStatus.Evaluate(metadata, null, releases, classic: true).Hints);
    }

    [Fact]
    public void ReleaseStatus_klassisch_nennt_die_mod_Datei()
    {
        var changelog = new[] { new ChangelogEntry("1.1", "Mod Version 1.1", 0, 3, 10, "t"), new ChangelogEntry("1.0", "Mod Version 1.0", 10, 13, 20, "t") };
        var releases = new[] { new ReleaseEntry { Version = "1.0" } };

        Assert.Equal(new[] { "Keine lesbare .mod-Datei gefunden." }, ReleaseStatus.Evaluate(null, null, releases, classic: true).Hints);
        Assert.Equal(new[] { "Die .mod-Datei enthält keine gültige Version." }, ReleaseStatus.Evaluate(new ModMetadata("Mod", "", "", @"C:\m"), null, releases, classic: true).Hints);
        Assert.Equal(
            new[] { "Die .mod-Datei steht auf 1.0, der Changelog beginnt mit 1.1." },
            ReleaseStatus.Evaluate(new ModMetadata("Mod", "1.0", "", @"C:\m"), changelog, releases, classic: true).Hints);
    }
}
