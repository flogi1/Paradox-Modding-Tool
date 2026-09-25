using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class BranchTransferLockTests : TempDirTests
{
    private readonly ModRegistry _registry;
    private readonly ModInfo _main;
    private readonly ModInfo _beta;
    private readonly string _mainFolder;
    private readonly string _betaFolder;

    public BranchTransferLockTests()
    {
        _registry = ModRegistry.Load(Make("game"));
        _mainFolder = Make("haupt");
        _betaFolder = Make("beta");
        _main = _registry.Add("Haupt", _mainFolder);
        _beta = _registry.Add("Beta", _betaFolder);
        _registry.SetProject(_beta.Id, _main.Id, ModRole.DevBranch);
    }

    private static string Write(string folder, string relative, string content)
    {
        var path = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void GesperrteZieldatei_WirdNichtUeberschrieben()
    {
        Write(_mainFolder, "a.txt", "neu");
        Write(_betaFolder, "a.txt", "alt");
        _registry.SetPathLocked(_beta.Id, "a.txt", true);

        var result = BranchTransfer.Execute(_registry, _main, _beta, "a.txt", TransferMode.Copy, overwrite: true);

        Assert.Equal(TransferOutcome.TargetLocked, result.Outcome);
        Assert.Equal("alt", File.ReadAllText(Path.Combine(_betaFolder, "a.txt")));
    }

    [Fact]
    public void ZieldateiUnterGesperrtemOrdner_IstGesperrt_AuchWennSieFehlt()
    {
        Write(_mainFolder, "common/a.txt", "x");
        _registry.SetPathLocked(_beta.Id, "common", true);

        var result = BranchTransfer.Execute(_registry, _main, _beta, "common/a.txt", TransferMode.Copy, overwrite: false);

        Assert.Equal(TransferOutcome.TargetLocked, result.Outcome);
        Assert.False(File.Exists(Path.Combine(_betaFolder, "common", "a.txt")));
    }

    [Fact]
    public void GesperrteQuelldatei_KopierenUndVerschiebenGehenNicht()
    {
        Write(_betaFolder, "a.txt", "x");
        _registry.SetPathLocked(_beta.Id, "a.txt", true);

        var copy = BranchTransfer.Execute(_registry, _beta, _main, "a.txt", TransferMode.Copy, overwrite: false);
        var move = BranchTransfer.Execute(_registry, _beta, _main, "a.txt", TransferMode.Move, overwrite: true);

        Assert.Equal(TransferOutcome.SourceLocked, copy.Outcome);
        Assert.Equal(TransferOutcome.SourceLocked, move.Outcome);
        Assert.True(File.Exists(Path.Combine(_betaFolder, "a.txt")));
        Assert.False(File.Exists(Path.Combine(_mainFolder, "a.txt")));
    }

    [Fact]
    public void FehlendeQuelle_GehtVorDerSperrePruefung()
    {
        _registry.SetPathLocked(_beta.Id, "fehlt.txt", true);

        var result = BranchTransfer.Execute(_registry, _main, _beta, "fehlt.txt", TransferMode.Copy, overwrite: false);

        Assert.Equal(TransferOutcome.SourceMissing, result.Outcome);
    }

    [Fact]
    public void Ordneruebertrag_UebertraegtUebrigeDateien_UndLaesstGesperrteUnberuehrt()
    {
        Write(_mainFolder, "common/a.txt", "neuA");
        Write(_mainFolder, "common/b.txt", "neuB");
        Write(_betaFolder, "common/a.txt", "altA");
        _registry.SetPathLocked(_beta.Id, "common/a.txt", true);

        var outcomes = BranchTransfer.FilesUnder(_main, "common")
            .Select(path => BranchTransfer.Execute(_registry, _main, _beta, path, TransferMode.Copy, overwrite: true).Outcome)
            .ToList();

        Assert.Contains(TransferOutcome.TargetLocked, outcomes);
        Assert.Contains(TransferOutcome.Done, outcomes);
        Assert.Equal("altA", File.ReadAllText(Path.Combine(_betaFolder, "common", "a.txt")));
        Assert.Equal("neuB", File.ReadAllText(Path.Combine(_betaFolder, "common", "b.txt")));
    }
}
