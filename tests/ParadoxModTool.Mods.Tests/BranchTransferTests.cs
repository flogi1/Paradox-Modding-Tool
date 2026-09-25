using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class BranchTransferTests : TempDirTests
{
    private readonly ModRegistry _registry;
    private readonly ModInfo _main;
    private readonly ModInfo _beta;
    private readonly ModInfo _sub;
    private readonly ModInfo _other;
    private readonly string _mainFolder;
    private readonly string _betaFolder;

    public BranchTransferTests()
    {
        _registry = ModRegistry.Load(Make("game"));
        _mainFolder = Make("haupt");
        _betaFolder = Make("beta");
        _main = _registry.Add("Haupt", _mainFolder);
        _beta = _registry.Add("Beta", _betaFolder);
        _sub = _registry.Add("Zusatz", Make("zusatz"));
        _other = _registry.Add("Fremd", Make("fremd"));
        _registry.SetProject(_beta.Id, _main.Id, ModRole.DevBranch);
        _registry.SetProject(_sub.Id, _main.Id, ModRole.Submod);
    }

    private static string Write(string folder, string relative, string content)
    {
        var path = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Check_ErlaubtHauptmodUndDevBranchInBeideRichtungen()
    {
        Assert.True(BranchTransfer.Check(_registry, _main, _beta, TransferMode.Copy).Allowed);
        Assert.True(BranchTransfer.Check(_registry, _beta, _main, TransferMode.Move).Allowed);
    }

    [Fact]
    public void Check_LehntAbMitBegruendung()
    {
        Assert.False(BranchTransfer.Check(_registry, _main, _main, TransferMode.Copy).Allowed);
        Assert.False(BranchTransfer.Check(_registry, _main, _other, TransferMode.Copy).Allowed);
        Assert.False(string.IsNullOrEmpty(BranchTransfer.Check(_registry, _main, _other, TransferMode.Copy).Reason));
    }

    [Fact]
    public void Check_ErlaubtUebertragungZwischenHauptmodUndSubmod()
    {
        Assert.True(BranchTransfer.Check(_registry, _main, _sub, TransferMode.Copy).Allowed);
        Assert.True(BranchTransfer.Check(_registry, _main, _sub, TransferMode.Move).Allowed);
        Assert.True(BranchTransfer.Check(_registry, _sub, _main, TransferMode.Move).Allowed);
        Assert.True(BranchTransfer.Check(_registry, _beta, _sub, TransferMode.Copy).Allowed);
    }

    [Fact]
    public void Execute_VerschiebtDateiInLeerenSubmodOrdner()
    {
        Write(_mainFolder, "common/a.txt", "x");

        var result = BranchTransfer.Execute(_registry, _main, _sub, "common/a.txt", TransferMode.Move, overwrite: false);

        Assert.Equal(TransferOutcome.Done, result.Outcome);
        Assert.False(File.Exists(Path.Combine(_mainFolder, "common", "a.txt")));
        Assert.True(File.Exists(Path.Combine(BranchTransfer.WorkingFolder(_sub)!, "common", "a.txt")));
    }

    [Fact]
    public void Check_GesperrteMod_WirdAlsZielUndAlsQuelleAbgelehnt()
    {
        _registry.SetLocked(_beta.Id, true);

        Assert.False(BranchTransfer.Check(_registry, _main, _beta, TransferMode.Copy).Allowed);
        Assert.False(BranchTransfer.Check(_registry, _beta, _main, TransferMode.Copy).Allowed);
        Assert.False(BranchTransfer.Check(_registry, _beta, _main, TransferMode.Move).Allowed);
    }

    [Fact]
    public void Copy_KopiertDieDateiUndLegtOrdnerAn_QuelleBleibt()
    {
        Write(_mainFolder, "common/a.txt", "inhalt");

        var result = BranchTransfer.Execute(_registry, _main, _beta, "common/a.txt", TransferMode.Copy, overwrite: false);

        Assert.Equal(TransferOutcome.Done, result.Outcome);
        Assert.Equal("inhalt", File.ReadAllText(Path.Combine(_betaFolder, "common", "a.txt")));
        Assert.True(File.Exists(Path.Combine(_mainFolder, "common", "a.txt")));
    }

    [Fact]
    public void Move_LoeschtQuelleUndLeereOrdner_ModOrdnerBleibt()
    {
        Write(_mainFolder, "common/tief/a.txt", "inhalt");

        BranchTransfer.Execute(_registry, _main, _beta, "common/tief/a.txt", TransferMode.Move, overwrite: false);

        Assert.True(File.Exists(Path.Combine(_betaFolder, "common", "tief", "a.txt")));
        Assert.False(Directory.Exists(Path.Combine(_mainFolder, "common")));
        Assert.True(Directory.Exists(_mainFolder));
    }

    [Fact]
    public void Move_LaesstNichtLeereOrdnerStehen()
    {
        Write(_mainFolder, "common/a.txt", "eins");
        Write(_mainFolder, "common/b.txt", "zwei");

        BranchTransfer.Execute(_registry, _main, _beta, "common/a.txt", TransferMode.Move, overwrite: false);

        Assert.True(File.Exists(Path.Combine(_mainFolder, "common", "b.txt")));
    }

    [Fact]
    public void VorhandeneZieldateiOhneOverwrite_AendertNichts()
    {
        Write(_mainFolder, "a.txt", "neu");
        Write(_betaFolder, "a.txt", "alt");

        var result = BranchTransfer.Execute(_registry, _main, _beta, "a.txt", TransferMode.Copy, overwrite: false);

        Assert.Equal(TransferOutcome.TargetExists, result.Outcome);
        Assert.Equal("alt", File.ReadAllText(Path.Combine(_betaFolder, "a.txt")));
        Assert.True(BranchTransfer.TargetExists(_beta, "a.txt"));
    }

    [Fact]
    public void Overwrite_SichertDieAlteZieldateiInsBackup()
    {
        Write(_mainFolder, "common/a.txt", "neu");
        Write(_betaFolder, "common/a.txt", "alt");

        var result = BranchTransfer.Execute(_registry, _main, _beta, "common/a.txt", TransferMode.Copy, overwrite: true, backupStamp: "STAMP");

        Assert.Equal(TransferOutcome.Done, result.Outcome);
        Assert.Equal("neu", File.ReadAllText(Path.Combine(_betaFolder, "common", "a.txt")));
        var expected = Path.Combine(_registry.DataDirectory(_beta.Id), "backups", "branch-transfer", "STAMP", "common", "a.txt");
        Assert.Equal(expected, result.BackupPath);
        Assert.Equal("alt", File.ReadAllText(expected));
    }

    [Fact]
    public void FehlendeQuelldatei_ErgibtSourceMissing()
    {
        var result = BranchTransfer.Execute(_registry, _main, _beta, "fehlt.txt", TransferMode.Copy, overwrite: false);

        Assert.Equal(TransferOutcome.SourceMissing, result.Outcome);
    }

    [Theory]
    [InlineData("../aussen.txt")]
    [InlineData("common/../../aussen.txt")]
    [InlineData("C:/Windows/x.txt")]
    [InlineData("/wurzel.txt")]
    [InlineData("")]
    [InlineData("  ")]
    public void UngueltigeRelativePfade_WerfenInvalidOperation(string relative)
    {
        Assert.Throws<InvalidOperationException>(() => BranchTransfer.Execute(_registry, _main, _beta, relative, TransferMode.Copy, overwrite: true));
    }

    [Fact]
    public void GesperrtesZiel_WirftInvalidOperation_NichtsWirdGeschrieben()
    {
        Write(_mainFolder, "a.txt", "x");
        _registry.SetLocked(_beta.Id, true);

        Assert.Throws<InvalidOperationException>(() => BranchTransfer.Execute(_registry, _main, _beta, "a.txt", TransferMode.Copy, overwrite: true));
        Assert.False(File.Exists(Path.Combine(_betaFolder, "a.txt")));
    }

    [Fact]
    public void GesperrteQuellmod_KopierenUndVerschiebenGehenNicht()
    {
        Write(_betaFolder, "a.txt", "x");
        _registry.SetLocked(_beta.Id, true);

        Assert.Throws<InvalidOperationException>(() => BranchTransfer.Execute(_registry, _beta, _main, "a.txt", TransferMode.Copy, overwrite: false));
        Assert.Throws<InvalidOperationException>(() => BranchTransfer.Execute(_registry, _beta, _main, "a.txt", TransferMode.Move, overwrite: true));
        Assert.True(File.Exists(Path.Combine(_betaFolder, "a.txt")));
    }

    [Fact]
    public void SchreibfehlerBeimVerschieben_BehaeltQuelleUndHinterlaesstKeineTempDatei()
    {
        Write(_mainFolder, "a.txt", "inhalt");
        Directory.CreateDirectory(Path.Combine(_betaFolder, "a.txt"));

        Assert.ThrowsAny<Exception>(() => BranchTransfer.Execute(_registry, _main, _beta, "a.txt", TransferMode.Move, overwrite: true));

        Assert.True(File.Exists(Path.Combine(_mainFolder, "a.txt")));
        Assert.Empty(Directory.EnumerateFiles(_betaFolder, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void ModOhneVorhandenenOrdner_WirftDirectoryNotFound()
    {
        Directory.Delete(_betaFolder, recursive: true);
        Write(_mainFolder, "a.txt", "x");

        Assert.Throws<DirectoryNotFoundException>(() => BranchTransfer.Execute(_registry, _main, _beta, "a.txt", TransferMode.Copy, overwrite: false));
    }

    [Fact]
    public void FilesUnder_LiefertNurDateienUnterDemOrdnerOhneAusgeschlossene()
    {
        Write(_mainFolder, "common/a.txt", "x");
        Write(_mainFolder, "common/sub/b.txt", "x");
        Write(_mainFolder, "common/.git/config", "x");
        Write(_mainFolder, "gfx/c.txt", "x");

        var files = BranchTransfer.FilesUnder(_main, "common");

        Assert.Equal(new[] { "common/a.txt", "common/sub/b.txt" }, files.OrderBy(f => f, StringComparer.Ordinal).ToArray());
    }
}
