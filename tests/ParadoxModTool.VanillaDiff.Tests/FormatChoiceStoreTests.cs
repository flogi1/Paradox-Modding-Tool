using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class FormatChoiceStoreTests : IDisposable
{
    private readonly string _modRoot = Directory.CreateTempSubdirectory("pmt-formatchoice-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_modRoot, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void TryLoad_FehlendeDatei_LiefertLeereStandardeinstellungen()
    {
        var settings = FormatChoiceStore.TryLoad(FormatChoiceStore.GetDefaultFilePath(_modRoot));
        Assert.Empty(settings.PreferModFormatting);
    }

    [Fact]
    public void SaveUndTryLoad_Roundtrip()
    {
        var path = FormatChoiceStore.GetDefaultFilePath(_modRoot);
        var settings = new FormatChoiceSettings();
        settings.PreferModFormatting["common/ideas/idea1.txt"] = true;

        FormatChoiceStore.Save(settings, path);
        var loaded = FormatChoiceStore.TryLoad(path);

        Assert.True(loaded.PreferModFormatting["common/ideas/idea1.txt"]);
    }

    [Fact]
    public void TryLoad_BeschaedigteDatei_LiefertLeereStandardeinstellungen()
    {
        var path = FormatChoiceStore.GetDefaultFilePath(_modRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ das ist kein gueltiges JSON");

        var settings = FormatChoiceStore.TryLoad(path);
        Assert.Empty(settings.PreferModFormatting);
    }
}
