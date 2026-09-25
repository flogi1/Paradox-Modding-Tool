using System.IO.Compression;
using Xunit;

namespace ParadoxModTool.VersionManager.Tests;

public class ModVersionManagerTests : IDisposable
{
    private readonly string _modRoot;
    private readonly ModVersionManager _sut;

    public ModVersionManagerTests()
    {
        _modRoot = Directory.CreateTempSubdirectory("pmt-tests-").FullName;
        _sut = new ModVersionManager(_modRoot);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_modRoot, recursive: true);
        }
        catch
        {
            // Best-effort Aufräumen — schlägt das Löschen fehl, soll der Test trotzdem nicht rot werden.
        }
    }

    [Fact]
    public void CreateSnapshot_ErzeugtZipUndSchliesstVersionsordnerAus()
    {
        File.WriteAllText(Path.Combine(_modRoot, "common_file.txt"), "inhalt");
        Directory.CreateDirectory(Path.Combine(_modRoot, ".pmt-versions", "dev"));
        File.WriteAllText(Path.Combine(_modRoot, ".pmt-versions", "dev", "dev_only.txt"), "sollte nicht im snapshot sein");

        var zipPath = _sut.CreateSnapshot("erster-test");

        Assert.True(File.Exists(zipPath));
        Assert.Contains("erster-test", Path.GetFileName(zipPath));

        using var archive = ZipFile.OpenRead(zipPath);
        var entryNames = archive.Entries.Select(e => e.FullName).ToList();

        Assert.Contains("common_file.txt", entryNames);
        Assert.DoesNotContain(entryNames, n => n.StartsWith(".pmt-versions", StringComparison.Ordinal));
    }
}
