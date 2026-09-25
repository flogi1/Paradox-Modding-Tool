using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ChangelogParserTests
{
    private const string Hash = "#############################################################";

    /// <summary>Kürzt die echte Datei "The Idea Variation 2" auf alle vorkommenden Banner-Formen.</summary>
    internal static readonly string Sample = string.Join("\n",
        "################################################################################",
        "################################################################################",
        "####### Idea Variation 2 Changelog",
        "################################################################################",
        "################################################################################",
        "",
        Hash,
        "####### Idea Variation 2 Version 2.0.5",
        Hash,
        "",
        Hash,
        "####### General",
        Hash,
        "",
        "- Fixed: The AI having access to buttons they should not have access to ",
        "",
        Hash,
        "####### Idea Variation 2 - The National Ideas update! (Version 2.0.0)",
        Hash,
        "",
        Hash,
        "####### General",
        Hash,
        "",
        "- Added National Ideas",
        "",
        Hash,
        "####### National Ideas",
        Hash,
        "",
        "- Eighty ideas",
        "",
        "",
        Hash,
        "####### Version 1.3.0",
        Hash,
        "",
        "- Ohne Modname",
        "",
        Hash,
        "####### Idea Variation 2 Version 1.0 (Original Release Version)",
        Hash,
        "",
        "####### Main Features",
        "- Ideas",
        "");

    [Fact]
    public void Parse_FindetAlleVersionsBannerInDerReihenfolgeDerDatei()
    {
        var entries = ChangelogParser.Parse(Sample);

        Assert.Equal(new[] { "2.0.5", "2.0.0", "1.3.0", "1.0" }, entries.Select(e => e.Version));
    }

    [Fact]
    public void Parse_UnterabschnitteBleibenImTextDerVersion_KopfWirdIgnoriert()
    {
        var entries = ChangelogParser.Parse(Sample);

        var v2 = entries.Single(e => e.Version == "2.0.0");
        Assert.Contains("- Added National Ideas", v2.Text);
        Assert.Contains("####### National Ideas", v2.Text);
        Assert.Contains("- Eighty ideas", v2.Text);
        Assert.DoesNotContain("Ohne Modname", v2.Text);
        Assert.DoesNotContain("Changelog", entries[0].Text);
    }

    [Fact]
    public void Parse_TitelUndZeilenpositionen()
    {
        var entries = ChangelogParser.Parse(Sample);
        var lines = Sample.Split('\n');

        Assert.Equal("Idea Variation 2 - The National Ideas update! (Version 2.0.0)", entries[1].Title);
        Assert.Equal(Hash, lines[entries[1].BannerLine]);
        Assert.Equal(entries[1].BannerLine + 3, entries[1].BodyStart);
        Assert.Equal(entries[2].BannerLine, entries[1].EndLine);
        Assert.Equal(lines.Length, entries[3].EndLine);
    }

    [Fact]
    public void Parse_TextOhneFuehrendeUndAbschliessendeLeerzeilen()
    {
        var entry = ChangelogParser.Parse(Sample).Single(e => e.Version == "1.3.0");

        Assert.Equal("- Ohne Modname", entry.Text);
    }

    [Fact]
    public void Parse_VersionOhneBanner_UndLeereEingabe()
    {
        Assert.Empty(ChangelogParser.Parse(""));
        Assert.Empty(ChangelogParser.Parse("Version 1.0\n- nichts\n"));
    }

    [Fact]
    public void Parse_KommtMitWindowsZeilenenden()
    {
        var entries = ChangelogParser.Parse(Sample.Replace("\n", "\r\n"));

        Assert.Equal(4, entries.Count);
        Assert.DoesNotContain('\r', entries[0].Text);
    }

    [Fact]
    public void Parse_DieEchteDatei_ImOrdnerDesNutzers()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Paradox Interactive", "Europa Universalis V", "mod", "The Idea Variation 2", "Readme", "Changelog.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var entries = ChangelogParser.Parse(File.ReadAllText(path));

        Assert.Equal(24, entries.Count);
        Assert.Equal("2.0.5", entries[0].Version);
        Assert.Equal("1.0", entries[^1].Version);
        Assert.Contains(entries, e => e.Version == "2.0.0");
        Assert.Contains(entries, e => e.Version == "1.2.0.1");
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Text)));
    }
}
