using Xunit;

namespace ParadoxModTool.Session.Tests;

public class RecoveryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-recovery-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static RecoveryEntry Entry(string id, string text = "x", string? path = "C:\a.txt") =>
        new() { Id = id, FilePath = path, Text = text, DiskWriteTimeUtcTicks = 42 };

    [Fact]
    public void Sync_SchreibtSicherung_LoadAllLiefertSieZurueck()
    {
        new RecoveryStore(_dir).Sync(new[] { Entry("a", "hallo"), Entry("b", "welt", null) });

        var loaded = new RecoveryStore(_dir).LoadAll().OrderBy(e => e.Id).ToList();

        Assert.Equal(2, loaded.Count);
        Assert.Equal("hallo", loaded[0].Text);
        Assert.Equal(42, loaded[0].DiskWriteTimeUtcTicks);
        Assert.Null(loaded[1].FilePath);
    }

    [Fact]
    public void Sync_LoeschtSelbstGeschriebeneSicherungNichtMehrUngespeicherterDokumente()
    {
        var store = new RecoveryStore(_dir);
        store.Sync(new[] { Entry("a"), Entry("b") });

        store.Sync(new[] { Entry("b") });

        Assert.Equal(new[] { "b" }, store.LoadAll().Select(e => e.Id));
    }

    [Fact]
    public void Sync_LoeschtFremdeSicherungenNicht()
    {
        new RecoveryStore(_dir).Sync(new[] { Entry("fremd") });

        var mine = new RecoveryStore(_dir);
        mine.Sync(new[] { Entry("eigen") });
        mine.Sync(Array.Empty<RecoveryEntry>());

        Assert.Equal(new[] { "fremd" }, mine.LoadAll().Select(e => e.Id));
    }

    [Fact]
    public void Delete_EntferntAuchSicherungAusFruehererSitzung()
    {
        new RecoveryStore(_dir).Sync(new[] { Entry("alt") });

        var store = new RecoveryStore(_dir);
        store.Delete("alt");

        Assert.Empty(store.LoadAll());
    }

    [Fact]
    public void LoadAll_UeberspringtBeschaedigteDatei()
    {
        var store = new RecoveryStore(_dir);
        store.Sync(new[] { Entry("ok") });
        File.WriteAllText(Path.Combine(_dir, "kaputt.json"), "{ nicht json");

        Assert.Equal(new[] { "ok" }, store.LoadAll().Select(e => e.Id));
    }

    [Fact]
    public void LoadAll_OhneOrdner_LiefertLeereListe()
    {
        Assert.Empty(new RecoveryStore(_dir).LoadAll());
    }
}
