using Xunit;

namespace ParadoxModTool.Explorer.Tests;

public class ExplorerColorStoreTests : IDisposable
{
    private readonly string _filePath;

    public ExplorerColorStoreTests()
    {
        _filePath = Path.Combine(Path.GetTempPath(), "pmt-explorer-colors-" + Guid.NewGuid() + ".json");
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
    public void SaveUndTryLoad_Roundtrip()
    {
        var colors = new Dictionary<string, string>
        {
            [PathNormalization.NormalizeKey(@"C:\Mods\MeinMod\common")] = "#4A90D9",
            [PathNormalization.NormalizeKey(@"C:\Mods\MeinMod\gui")] = "#D98E4A",
        };

        ExplorerColorStore.Save(colors, _filePath);
        var loaded = ExplorerColorStore.TryLoad(_filePath);

        Assert.Equal(2, loaded.Count);
        Assert.Equal("#4A90D9", loaded[PathNormalization.NormalizeKey(@"C:\Mods\MeinMod\common")]);
        Assert.Equal("#D98E4A", loaded[PathNormalization.NormalizeKey(@"C:\Mods\MeinMod\gui")]);
    }

    [Fact]
    public void TryLoad_FehlendeDatei_LiefertLeeresDictionary()
    {
        var loaded = ExplorerColorStore.TryLoad(_filePath);

        Assert.Empty(loaded);
    }

    [Fact]
    public void TryLoad_UngueltigesJson_LiefertLeeresDictionaryStattAusnahme()
    {
        File.WriteAllText(_filePath, "{ das ist kein gueltiges JSON");

        var loaded = ExplorerColorStore.TryLoad(_filePath);

        Assert.Empty(loaded);
    }
}
