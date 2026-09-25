using Xunit;

namespace ParadoxModTool.SearchReplace.Tests;

public class SearchFavoritesStoreTests : IDisposable
{
    private readonly string _dir;

    public SearchFavoritesStoreTests()
    {
        _dir = Directory.CreateTempSubdirectory("pmt-favorites-").FullName;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    [Fact]
    public void SaveUndTryLoad_Rundtrip()
    {
        var path = Path.Combine(_dir, "favorites.json");
        var folders = new List<string> { @"C:\mod\common", @"C:\mod\gui" };

        SearchFavoritesStore.Save(folders, path);
        var loaded = SearchFavoritesStore.TryLoad(path);

        Assert.Equal(folders, loaded);
    }

    [Fact]
    public void TryLoad_DateiFehltNoch_GibtLeereListeZurueck()
    {
        var path = Path.Combine(_dir, "gibt-es-nicht.json");

        var loaded = SearchFavoritesStore.TryLoad(path);

        Assert.Empty(loaded);
    }

    [Fact]
    public void TryLoad_KaputtesJson_GibtLeereListeStattFehlerZurueck()
    {
        var path = Path.Combine(_dir, "kaputt.json");
        File.WriteAllText(path, "{ das ist kein gueltiges JSON ][");

        var loaded = SearchFavoritesStore.TryLoad(path);

        Assert.Empty(loaded);
    }

    [Fact]
    public void Save_UeberschreibtVorhandeneDatei()
    {
        var path = Path.Combine(_dir, "favorites.json");
        SearchFavoritesStore.Save(new List<string> { "alt" }, path);

        SearchFavoritesStore.Save(new List<string> { "neu1", "neu2" }, path);
        var loaded = SearchFavoritesStore.TryLoad(path);

        Assert.Equal(new List<string> { "neu1", "neu2" }, loaded);
    }
}
