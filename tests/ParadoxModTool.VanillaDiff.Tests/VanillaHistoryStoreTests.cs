using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class VanillaHistoryStoreTests : IDisposable
{
    private readonly string _modRoot;
    private readonly string _vanillaFile;
    private readonly VanillaHistoryStore _sut;

    public VanillaHistoryStoreTests()
    {
        _modRoot = Directory.CreateTempSubdirectory("pmt-mod-").FullName;
        var scratchDir = Directory.CreateTempSubdirectory("pmt-vanilla-src-").FullName;
        _vanillaFile = Path.Combine(scratchDir, "x.txt");
        File.WriteAllText(_vanillaFile, "content-v1");
        _sut = new VanillaHistoryStore(_modRoot);
    }

    public void Dispose()
    {
        TryDelete(_modRoot);
        TryDelete(Path.GetDirectoryName(_vanillaFile)!);
    }

    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    [Fact]
    public void RecordSnapshot_SpeichertUndListetVersion()
    {
        _sut.RecordSnapshot("common/x.txt", "1.0", _vanillaFile);

        Assert.True(_sut.HasSnapshot("common/x.txt", "1.0"));
        Assert.Contains("1.0", _sut.ListRecordedVersions("common/x.txt"));
    }

    [Fact]
    public void ReadSnapshot_LiefertGespeichertenInhalt()
    {
        _sut.RecordSnapshot("common/x.txt", "1.0", _vanillaFile);

        var content = _sut.ReadSnapshot("common/x.txt", "1.0");

        Assert.Equal("content-v1", content);
    }

    [Fact]
    public void ReadSnapshot_FehlendeVersion_WirftFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => _sut.ReadSnapshot("common/x.txt", "2.0"));
    }

    [Fact]
    public void ListRecordedVersions_OhneVorherigenSnapshot_IstLeer()
    {
        Assert.Empty(_sut.ListRecordedVersions("common/nicht-vorhanden.txt"));
    }
}
