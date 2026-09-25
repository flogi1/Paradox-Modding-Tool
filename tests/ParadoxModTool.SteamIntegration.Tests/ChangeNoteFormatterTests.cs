using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class ChangeNoteFormatterTests
{
    private const string Hash = "#############################################################";

    [Fact]
    public void Aufzaehlung_WirdEineListe()
    {
        Assert.Equal("[list]\n[*]A\n[*]B\n[/list]", ChangeNoteFormatter.ToBbCode("- A\n- B"));
    }

    [Fact]
    public void Sternchen_AlsAufzaehlungszeichen()
    {
        Assert.Equal("[list]\n[*]A\n[/list]", ChangeNoteFormatter.ToBbCode("* A"));
    }

    [Fact]
    public void Unterbanner_WirdFettUndDieRautenlinienEntfallen()
    {
        var text = string.Join("\n", Hash, "####### General", Hash, "", "- Fixed X", "", Hash, "####### Ideas", Hash, "", "- Added Y");

        Assert.Equal("[b]General[/b]\n\n[list]\n[*]Fixed X\n[/list]\n\n[b]Ideas[/b]\n\n[list]\n[*]Added Y\n[/list]", ChangeNoteFormatter.ToBbCode(text));
    }

    [Fact]
    public void NormalerTextBleibt_UndSchliesstDieListe()
    {
        Assert.Equal("Intro\n[list]\n[*]A\n[/list]\nSchluss", ChangeNoteFormatter.ToBbCode("Intro\n- A\nSchluss"));
    }

    [Fact]
    public void EingerueckteFolgezeile_GehoertZumPunkt()
    {
        Assert.Equal("[list]\n[*]A weiter\n[/list]", ChangeNoteFormatter.ToBbCode("- A\n  weiter"));
    }

    [Fact]
    public void LeereZeilenWerdenZusammengefasst_UndRaenderEntfallen()
    {
        Assert.Equal("A\n\nB", ChangeNoteFormatter.ToBbCode("\n\nA\n\n\n\nB\n\n"));
    }

    [Fact]
    public void WindowsZeilenenden_UndLeereEingabe()
    {
        Assert.Equal("[list]\n[*]A\n[/list]", ChangeNoteFormatter.ToBbCode("- A\r\n"));
        Assert.Equal("", ChangeNoteFormatter.ToBbCode(""));
    }

    [Fact]
    public void BindestrichOhneLeerzeichen_IstKeinePunkt()
    {
        Assert.Equal("-A", ChangeNoteFormatter.ToBbCode("-A"));
    }

    [Fact]
    public void MaxLength_IstAchttausend()
    {
        Assert.Equal(8000, ChangeNoteFormatter.MaxLength);
        Assert.False(ChangeNoteFormatter.IsTooLong(new string('x', 8000)));
        Assert.True(ChangeNoteFormatter.IsTooLong(new string('x', 8001)));
    }
}
