using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ChangelogEditorTests
{
    private const string Hash = "#############################################################";

    private static string Sample => ChangelogParserTests.Sample;

    [Fact]
    public void SetText_UnveraenderterText_LaesstDieDateiBytegleich()
    {
        foreach (var entry in ChangelogParser.Parse(Sample))
        {
            Assert.Equal(Sample, ChangelogEditor.SetText(Sample, entry.Version, entry.Text));
        }
    }

    [Fact]
    public void SetText_ErsetztNurDenTextDieserVersion()
    {
        var result = ChangelogEditor.SetText(Sample, "1.3.0", "- Neuer Text\n- Zweite Zeile");

        var entries = ChangelogParser.Parse(result);
        Assert.Equal("- Neuer Text\n- Zweite Zeile", entries.Single(e => e.Version == "1.3.0").Text);
        Assert.Equal(ChangelogParser.Parse(Sample).Single(e => e.Version == "2.0.0").Text, entries.Single(e => e.Version == "2.0.0").Text);
        Assert.Equal(4, entries.Count);
    }

    [Fact]
    public void SetText_LeererText_LaesstDenAbschnittMitBannerStehen()
    {
        var result = ChangelogEditor.SetText(Sample, "1.3.0", "");

        var entries = ChangelogParser.Parse(result);
        Assert.Equal(4, entries.Count);
        Assert.Equal("", entries.Single(e => e.Version == "1.3.0").Text);
        Assert.Equal("1.0", entries[3].Version);
    }

    [Fact]
    public void SetText_LetzterAbschnittOhneNachfolger()
    {
        var result = ChangelogEditor.SetText(Sample, "1.0", "- Ersetzt");

        var last = ChangelogParser.Parse(result)[^1];
        Assert.Equal("1.0", last.Version);
        Assert.Equal("- Ersetzt", last.Text);
        Assert.EndsWith("\n", result);
    }

    [Fact]
    public void SetText_BehaeltWindowsZeilenenden()
    {
        var crlf = Sample.Replace("\n", "\r\n");

        var result = ChangelogEditor.SetText(crlf, "1.3.0", "a\nb");

        Assert.DoesNotContain("\n", result.Replace("\r\n", ""));
        Assert.Contains("a\r\nb", result);
    }

    [Fact]
    public void SetText_UnbekannteVersion_WirftInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() => ChangelogEditor.SetText(Sample, "9.9", "x"));
    }

    [Fact]
    public void InsertVersion_LegtDasBannerUnterDemKopfAnUndLaesstAllesAndereStehen()
    {
        var result = ChangelogEditor.InsertVersion(Sample, "2.0.6", "Idea Variation 2");

        var entries = ChangelogParser.Parse(result);
        Assert.Equal(new[] { "2.0.6", "2.0.5", "2.0.0", "1.3.0", "1.0" }, entries.Select(e => e.Version));
        Assert.Equal("Idea Variation 2 Version 2.0.6", entries[0].Title);
        Assert.Contains("####### General", entries[0].Text);
        Assert.StartsWith(Sample.Split('\n')[0], result);
        Assert.EndsWith(Sample[Sample.IndexOf("####### Idea Variation 2 Version 2.0.5", StringComparison.Ordinal)..], result);
    }

    [Fact]
    public void InsertVersion_OhneVorhandeneVersion_HaengtAnUndBrichtNichtsKaputt()
    {
        var header = "####### Kopf\n\nText\n";

        var result = ChangelogEditor.InsertVersion(header, "1.0.0", "Meine Mod");

        var entries = ChangelogParser.Parse(result);
        Assert.Equal("1.0.0", Assert.Single(entries).Version);
        Assert.StartsWith(header, result);
    }

    [Fact]
    public void InsertVersion_LeereDatei()
    {
        var result = ChangelogEditor.InsertVersion("", "1.0.0", "Meine Mod");

        Assert.Equal("1.0.0", Assert.Single(ChangelogParser.Parse(result)).Version);
    }

    [Fact]
    public void InsertVersion_VorhandeneVersion_WirftInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() => ChangelogEditor.InsertVersion(Sample, "2.0.5", "X"));
        Assert.Throws<InvalidOperationException>(() => ChangelogEditor.InsertVersion(Sample, "1.0.0", "X"));
    }

    [Fact]
    public void InsertVersion_NimmtDieZeilenendenDerDatei()
    {
        var result = ChangelogEditor.InsertVersion(Sample.Replace("\n", "\r\n"), "3.0", "X");

        Assert.DoesNotContain("\n", result.Replace("\r\n", ""));
        Assert.Contains(Hash + "\r\n####### X Version 3.0\r\n", result);
    }

    [Fact]
    public void DieEchteDatei_UnveraendertZurueckgeschriebenBleibtBytegleich_UndEinBannerKommtDazu()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Paradox Interactive", "Europa Universalis V", "mod", "The Idea Variation 2", "Readme", "Changelog.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var text = File.ReadAllText(path);
        var entries = ChangelogParser.Parse(text);
        foreach (var entry in entries)
        {
            Assert.Equal(text, ChangelogEditor.SetText(text, entry.Version, entry.Text));
        }

        var inserted = ChangelogParser.Parse(ChangelogEditor.InsertVersion(text, "2.0.6", "The Idea Variation 2"));
        Assert.Equal(entries.Count + 1, inserted.Count);
        Assert.Equal("2.0.6", inserted[0].Version);
        Assert.Equal(entries.Select(e => e.Text), inserted.Skip(1).Select(e => e.Text));
    }
}
