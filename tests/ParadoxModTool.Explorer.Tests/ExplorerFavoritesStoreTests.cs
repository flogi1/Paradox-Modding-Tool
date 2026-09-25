using Xunit;

namespace ParadoxModTool.Explorer.Tests;

public class ExplorerFavoritesStoreTests : IDisposable
{
    private readonly string _filePath;

    public ExplorerFavoritesStoreTests()
    {
        _filePath = Path.Combine(Path.GetTempPath(), "pmt-explorer-favorites-" + Guid.NewGuid() + ".json");
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_filePath);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    [Fact]
    public void SaveUndTryLoad_Roundtrip_ErhaeltOriginaleSchreibweise()
    {
        var favorites = new List<string> { @"C:\Mods\MeinMod\common\buildings.txt", @"C:\Mods\MeinMod\gui" };

        ExplorerFavoritesStore.Save(favorites, _filePath);
        var loaded = ExplorerFavoritesStore.TryLoad(_filePath);

        Assert.Equal(favorites, loaded);
    }

    [Fact]
    public void TryLoad_FehlendeDatei_LiefertLeereListe()
    {
        var loaded = ExplorerFavoritesStore.TryLoad(_filePath);

        Assert.Empty(loaded);
    }

    [Fact]
    public void TryLoad_UngueltigesJson_LiefertLeereListeStattAusnahme()
    {
        File.WriteAllText(_filePath, "[ das ist kein gueltiges JSON");

        var loaded = ExplorerFavoritesStore.TryLoad(_filePath);

        Assert.Empty(loaded);
    }
}
