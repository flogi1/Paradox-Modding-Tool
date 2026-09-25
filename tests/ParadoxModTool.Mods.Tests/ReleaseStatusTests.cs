using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ReleaseStatusTests
{
    private static ModMetadata Metadata(string version) => new("Mod", version, "1.*", @"C:\m");

    private static IReadOnlyList<ChangelogEntry> Changelog(params string[] versions) =>
        versions.Select((v, i) => new ChangelogEntry(v, "Mod Version " + v, i * 10, i * 10 + 3, i * 10 + 10, "text")).ToList();

    private static IReadOnlyList<ReleaseEntry> Releases(params string[] versions) =>
        versions.Select(v => new ReleaseEntry { Version = v }).ToList();

    [Fact]
    public void Alles_stimmt_ueberein_ergibt_keinen_Hinweis()
    {
        var status = ReleaseStatus.Evaluate(Metadata("2.0.5"), Changelog("2.0.5", "2.0.4"), Releases("2.0.5", "2.0.4"));

        Assert.Empty(status.Hints);
        Assert.Equal("2.0.5", status.MetadataVersion);
        Assert.Equal("2.0.5", status.ChangelogTop);
        Assert.Equal("2.0.5", status.LatestRelease);
    }

    [Fact]
    public void Metadaten_und_Changelog_Spitze_weichen_ab_obwohl_die_Version_im_Changelog_steht()
    {
        var status = ReleaseStatus.Evaluate(Metadata("2.0.4"), Changelog("2.0.5", "2.0.4"), Releases("2.0.4"));

        Assert.Equal(new[] { "metadata.json steht auf 2.0.4, der Changelog beginnt mit 2.0.5." }, status.Hints);
    }

    [Fact]
    public void Metadaten_Version_fehlt_im_Changelog()
    {
        var status = ReleaseStatus.Evaluate(Metadata("2.0.6"), Changelog("2.0.5"), Releases("2.0.6"));

        Assert.Equal(new[] { "Im Changelog fehlt ein Abschnitt für Version 2.0.6." }, status.Hints);
    }

    [Fact]
    public void Version_ist_noch_keine_erfasste_Release()
    {
        var status = ReleaseStatus.Evaluate(Metadata("2.0.5"), Changelog("2.0.5"), Releases("2.0.4"));

        Assert.Equal(new[] { "Version 2.0.5 ist noch nicht als Release erfasst." }, status.Hints);
        Assert.Equal("2.0.4", status.LatestRelease);
    }

    [Fact]
    public void Gleiche_Version_in_anderer_Schreibweise_gilt_als_gleich()
    {
        var status = ReleaseStatus.Evaluate(Metadata("1.0"), Changelog("1.0.0"), Releases("1.0.0"));

        Assert.Empty(status.Hints);
    }

    [Fact]
    public void Ohne_Metadaten_oder_ohne_gueltige_Version()
    {
        Assert.Equal(new[] { "Keine lesbare metadata.json gefunden." }, ReleaseStatus.Evaluate(null, Changelog("1.0"), Releases()).Hints);
        Assert.Equal(new[] { "metadata.json enthält keine gültige Version." }, ReleaseStatus.Evaluate(Metadata("abc"), Changelog("1.0"), Releases()).Hints);
    }

    [Fact]
    public void Ohne_Changelog_gibt_es_keinen_Changelog_Hinweis_und_ein_Changelog_ohne_Banner_wird_gemeldet()
    {
        Assert.Empty(ReleaseStatus.Evaluate(Metadata("1.0"), null, Releases("1.0")).Hints);
        Assert.Equal(
            new[] { "Der Changelog enthält keine Versions-Banner." },
            ReleaseStatus.Evaluate(Metadata("1.0"), Changelog(), Releases("1.0")).Hints);
    }

    [Fact]
    public void Neueste_Release_nach_Zahlenwert()
    {
        var status = ReleaseStatus.Evaluate(Metadata("1.0"), null, Releases("2.0.9", "2.0.10", "1.5"));

        Assert.Equal("2.0.10", status.LatestRelease);
        Assert.Null(ReleaseStatus.Evaluate(Metadata("1.0"), null, Releases()).LatestRelease);
    }
}
