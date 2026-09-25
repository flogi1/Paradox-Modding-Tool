using ParadoxModTool.VersionManager;
using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class VanillaMergeWorkflowTests : IDisposable
{
    private readonly string _modRoot;
    private readonly string _gameScratchRoot;
    private readonly string _modFilePath;
    private readonly ModVersionManager _versionManager;

    public VanillaMergeWorkflowTests()
    {
        _modRoot = Directory.CreateTempSubdirectory("pmt-mod-").FullName;
        _gameScratchRoot = Directory.CreateTempSubdirectory("pmt-game-").FullName;
        _versionManager = new ModVersionManager(_modRoot);

        _modFilePath = Path.Combine(_modRoot, "common", "ideas", "idea1.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(_modFilePath)!);
    }

    public void Dispose()
    {
        TryDelete(_modRoot);
        TryDelete(_gameScratchRoot);
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

    private string WriteGameFile(string name, string content)
    {
        var path = Path.Combine(_gameScratchRoot, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void MergeFileIntoMod_OhneKonflikt_SchreibtDateiUndErstelltSnapshot()
    {
        var oldVanillaPath = WriteGameFile("old.txt", "cost = 100\nother = 1\n");
        var newVanillaPath = WriteGameFile("new.txt", "cost = 100\nother = 1\nbrand_new = 5\n");
        File.WriteAllText(_modFilePath, "cost = 150\nother = 1\n");

        var result = VanillaMergeWorkflow.MergeFileIntoMod(oldVanillaPath, newVanillaPath, _modFilePath, _versionManager);

        Assert.False(result.HasConflicts);

        var mergedFromDisk = ScriptDocument.Parse(File.ReadAllText(_modFilePath));
        var expected = ScriptDocument.Parse("cost = 150\nother = 1\nbrand_new = 5\n");
        Assert.True(ScriptStructuralComparer.ValueEquals(expected, mergedFromDisk));

        var snapshotsDir = Path.Combine(_modRoot, "snapshots");
        Assert.True(Directory.Exists(snapshotsDir));
        Assert.NotEmpty(Directory.EnumerateFiles(snapshotsDir, "*.zip"));
    }

    [Fact]
    public void MergeFileIntoMod_MitKonflikt_SchreibtStandardmaessigNichtsUndErstelltKeinenSnapshot()
    {
        var oldVanillaPath = WriteGameFile("old.txt", "cost = 100\n");
        var newVanillaPath = WriteGameFile("new.txt", "cost = 200\n");
        const string originalModContent = "cost = 150\n";
        File.WriteAllText(_modFilePath, originalModContent);

        var result = VanillaMergeWorkflow.MergeFileIntoMod(oldVanillaPath, newVanillaPath, _modFilePath, _versionManager);

        Assert.True(result.HasConflicts);
        Assert.Equal(originalModContent, File.ReadAllText(_modFilePath));

        var snapshotsDir = Path.Combine(_modRoot, "snapshots");
        Assert.False(Directory.Exists(snapshotsDir));
    }

    [Fact]
    public void ComputeMerge_LiestDreiDateienUndLiefertMergeResult()
    {
        var oldVanillaPath = WriteGameFile("old.txt", "cost = 100\n");
        var newVanillaPath = WriteGameFile("new.txt", "cost = 100\nbrand_new = 5\n");
        File.WriteAllText(_modFilePath, "cost = 150\n");

        var result = VanillaMergeWorkflow.ComputeMerge(oldVanillaPath, newVanillaPath, _modFilePath);

        Assert.False(result.HasConflicts);
        var mergedFromResult = ScriptStructuralComparer.ValueEquals(
            ScriptDocument.Parse("cost = 150\nbrand_new = 5\n"), result.MergedRoot);
        Assert.True(mergedFromResult);
        // ComputeMerge liest nur, schreibt nichts.
        Assert.Equal("cost = 150\n", File.ReadAllText(_modFilePath));
    }

    [Fact]
    public void WriteMergedResult_SchreibtUndErstelltSnapshot()
    {
        File.WriteAllText(_modFilePath, "cost = 150\n");
        var finalRoot = ScriptDocument.Parse("cost = 150\nbrand_new = 5\n");

        VanillaMergeWorkflow.WriteMergedResult(finalRoot, _modFilePath, _versionManager, preferSourceVerbatim: false);

        var writtenBack = ScriptDocument.Parse(File.ReadAllText(_modFilePath));
        Assert.True(ScriptStructuralComparer.ValueEquals(finalRoot, writtenBack));

        var snapshotsDir = Path.Combine(_modRoot, "snapshots");
        Assert.True(Directory.Exists(snapshotsDir));
        Assert.NotEmpty(Directory.EnumerateFiles(snapshotsDir, "*.zip"));
    }
}
