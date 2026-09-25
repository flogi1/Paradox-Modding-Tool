using Xunit;

namespace ParadoxModTool.SearchReplace.Tests;

public class FileReplaceServiceTests : IDisposable
{
    private readonly string _dir;

    public FileReplaceServiceTests()
    {
        _dir = Directory.CreateTempSubdirectory("pmt-replace-").FullName;
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
    public void ComputeReplacements_LiefertNurPlaeneFuerDateienMitTreffern_UndSchreibtNichts()
    {
        var pathA = Path.Combine(_dir, "a.txt");
        var pathB = Path.Combine(_dir, "b.txt");
        File.WriteAllText(pathA, "tax = 5\ntax = 10");
        File.WriteAllText(pathB, "unrelated = 1");

        var plans = FileReplaceService.ComputeReplacements(
            new[] { pathA, pathB }, "tax", "income", new SearchOptions());

        Assert.Single(plans);
        Assert.Equal(pathA, plans[0].FilePath);
        Assert.Equal(2, plans[0].ReplacementCount);
        Assert.Equal("income = 5\nincome = 10", plans[0].NewContent);

        // Rein berechnend - die Originaldatei darf noch nicht verändert worden sein, das
        // eigentliche Schreiben übernimmt das App-Modul über den Aktionsverlauf.
        Assert.Equal("tax = 5\ntax = 10", File.ReadAllText(pathA));
    }

    [Fact]
    public void ComputeReplacements_NichtLesbareDatei_BrichtBerechnungDerUebrigenNichtAb()
    {
        var pathA = Path.Combine(_dir, "a.txt");
        var missingPath = Path.Combine(_dir, "gibt-es-nicht.txt");
        File.WriteAllText(pathA, "tax = 5");

        var plans = FileReplaceService.ComputeReplacements(
            new[] { missingPath, pathA }, "tax", "income", new SearchOptions());

        Assert.Single(plans);
        Assert.Equal(pathA, plans[0].FilePath);
    }

    [Fact]
    public void ComputeReplacements_OhneTreffer_LiefertLeereListe()
    {
        var pathA = Path.Combine(_dir, "a.txt");
        File.WriteAllText(pathA, "unrelated = 1");

        var plans = FileReplaceService.ComputeReplacements(
            new[] { pathA }, "tax", "income", new SearchOptions());

        Assert.Empty(plans);
    }
}
