using Xunit;

namespace ParadoxModTool.GitIntegration.Tests;

public class ReleaseNotesFormatterTests
{
    private static readonly string Hash = new('#', 61);

    [Fact]
    public void Unterbanner_WerdenUeberschriften_HashLinienEntfallen()
    {
        var text = string.Join("\n", "", Hash, "####### General", Hash, "", "- Fixed: a", "- Added: b", "");

        Assert.Equal("### General\n\n- Fixed: a\n- Added: b", ReleaseNotesFormatter.ToMarkdown(text));
    }

    [Fact]
    public void LeerzeilenWerdenZusammengezogen_ZeilenendeLeerzeichenEntfallen()
    {
        var text = "- a   \r\n\r\n\r\n\r\n- b\r\n";

        Assert.Equal("- a\n\n- b", ReleaseNotesFormatter.ToMarkdown(text));
    }

    [Fact]
    public void LeererOderNullText_IstLeer()
    {
        Assert.Equal(string.Empty, ReleaseNotesFormatter.ToMarkdown(null));
        Assert.Equal(string.Empty, ReleaseNotesFormatter.ToMarkdown("  \n\n"));
    }

    [Fact]
    public void ZuLang_ErkenntGrenzeVonGitHub()
    {
        Assert.False(ReleaseNotesFormatter.IsTooLong(new string('a', ReleaseNotesFormatter.MaxLength)));
        Assert.True(ReleaseNotesFormatter.IsTooLong(new string('a', ReleaseNotesFormatter.MaxLength + 1)));
    }
}
