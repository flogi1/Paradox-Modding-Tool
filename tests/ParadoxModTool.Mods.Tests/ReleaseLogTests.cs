using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ReleaseLogTests : TempDirTests
{
    [Fact]
    public void Load_OhneDatei_IstLeer_UndLegtNichtsAn()
    {
        var log = ReleaseLog.Load(Path.Combine(Dir, "mod"));

        Assert.Empty(log.Releases);
        Assert.Null(log.LoadWarning);
        Assert.False(Directory.Exists(Path.Combine(Dir, "mod")));
    }

    [Fact]
    public void Add_SpeichertSofort_UndSortiertNachZahlenwertNeuesteZuerst()
    {
        var folder = Path.Combine(Dir, "mod");
        var log = ReleaseLog.Load(folder);
        log.Add("2.0.9", "2026-09-01");
        log.Add("2.0.10", "");
        log.Add("1.0", "2025-01-01");

        var reloaded = ReleaseLog.Load(folder);

        Assert.Equal(new[] { "2.0.10", "2.0.9", "1.0" }, reloaded.Releases.Select(r => r.Version));
        Assert.Equal("2026-09-01", reloaded.Find("2.0.9")!.Date);
        Assert.Equal("", reloaded.Find("2.0.10")!.Date);
    }

    [Fact]
    public void Add_DoppelteVersionNachZahlenwert_WirftInvalidOperation()
    {
        var log = ReleaseLog.Load(Path.Combine(Dir, "mod"));
        log.Add("1.0", "");

        Assert.Throws<InvalidOperationException>(() => log.Add("1.0.0", ""));
    }

    [Theory]
    [InlineData("abc", "")]
    [InlineData("", "")]
    [InlineData("1.0", "gestern")]
    [InlineData("1.0", "2026-13-40")]
    public void Add_UngueltigeEingaben_WerfenArgumentException(string version, string date)
    {
        var log = ReleaseLog.Load(Path.Combine(Dir, "mod"));

        Assert.Throws<ArgumentException>(() => log.Add(version, date));
        Assert.Empty(log.Releases);
    }

    [Fact]
    public void SetDate_UndRemove_AendernUndSpeichern()
    {
        var folder = Path.Combine(Dir, "mod");
        var log = ReleaseLog.Load(folder);
        log.Add("1.0", "");
        log.Add("1.1", "");

        log.SetDate("1.0", "2026-01-02");
        log.Remove("1.1");

        var reloaded = ReleaseLog.Load(folder);
        Assert.Equal("2026-01-02", Assert.Single(reloaded.Releases).Date);
        Assert.Throws<InvalidOperationException>(() => reloaded.SetDate("9.9", ""));
    }

    [Fact]
    public void Load_KaputteDatei_LiefertLeeresLog_SichertKopie_UndWarnt()
    {
        var folder = Make("mod");
        var file = Path.Combine(folder, ReleaseLog.FileName);
        File.WriteAllText(file, "{ kaputt");

        var log = ReleaseLog.Load(folder);

        Assert.Empty(log.Releases);
        Assert.NotNull(log.LoadWarning);
        Assert.Equal("{ kaputt", File.ReadAllText(file + ".bad"));
    }

    [Fact]
    public void Load_UeberspringtEintraegeOhneGueltigeVersion()
    {
        var folder = Make("mod");
        File.WriteAllText(
            Path.Combine(folder, ReleaseLog.FileName),
            "{\"version\":1,\"releases\":[{\"version\":\"1.0\",\"date\":\"2026-01-01\"},{\"version\":\"x\",\"date\":\"\"},null,{\"date\":\"\"}]}");

        var log = ReleaseLog.Load(folder);

        Assert.Equal("1.0", Assert.Single(log.Releases).Version);
    }
}
