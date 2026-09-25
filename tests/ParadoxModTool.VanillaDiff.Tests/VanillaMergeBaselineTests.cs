using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class VanillaMergeBaselineTests : IDisposable
{
    private readonly string _modRoot = Directory.CreateTempSubdirectory("pmt-baseline-mod-").FullName;
    private readonly string _vanillaFile;
    private readonly VanillaHistoryStore _store;

    public VanillaMergeBaselineTests()
    {
        _store = new VanillaHistoryStore(_modRoot);
        _vanillaFile = Path.Combine(_modRoot, "vanilla-scratch.txt");
        File.WriteAllText(_vanillaFile, "cost = 200\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_modRoot, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Resolve_ErsteVerwendung_ZeichnetBaselineAufUndMeldetKeineAeltereVersion()
    {
        var result = VanillaMergeBaseline.Resolve(_store, "common/ideas/idea1.txt", "1.0", _vanillaFile);

        Assert.False(result.HasOlderBaseline);
        Assert.True(_store.HasSnapshot("common/ideas/idea1.txt", "1.0"));
    }

    [Fact]
    public void Resolve_AeltererSnapshotVorhanden_LiefertDessenInhalt()
    {
        _store.RecordSnapshot("common/ideas/idea1.txt", "1.0", _vanillaFile);
        File.WriteAllText(_vanillaFile, "cost = 250\n"); // Spiel wurde aktualisiert

        var result = VanillaMergeBaseline.Resolve(_store, "common/ideas/idea1.txt", "1.1", _vanillaFile);

        Assert.True(result.HasOlderBaseline);
        Assert.Equal("1.0", result.OlderVersion);
        Assert.Equal("cost = 200\n", result.OlderVanillaText);
        Assert.True(_store.HasSnapshot("common/ideas/idea1.txt", "1.1"));
    }

    [Fact]
    public void Resolve_AktuelleVersionBereitsAufgezeichnet_ZeichnetNichtErneutAuf()
    {
        _store.RecordSnapshot("common/ideas/idea1.txt", "1.0", _vanillaFile);
        var recordedVersionsBefore = _store.ListRecordedVersions("common/ideas/idea1.txt");

        var result = VanillaMergeBaseline.Resolve(_store, "common/ideas/idea1.txt", "1.0", _vanillaFile);

        Assert.False(result.HasOlderBaseline);
        Assert.Equal(recordedVersionsBefore, _store.ListRecordedVersions("common/ideas/idea1.txt"));
    }
}
