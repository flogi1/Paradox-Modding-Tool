using System.Linq;
using Xunit;

namespace ParadoxModTool.SearchReplace.Tests;

public class FileSearchServiceTests : IDisposable
{
    private readonly string _dir;

    public FileSearchServiceTests()
    {
        _dir = Directory.CreateTempSubdirectory("pmt-search-").FullName;
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
    public void EnumerateSearchableFiles_FindetNurErlaubteEndungen()
    {
        File.WriteAllText(Path.Combine(_dir, "common.txt"), "tax = 5");
        File.WriteAllText(Path.Combine(_dir, "interface.gui"), "tax = 5");
        File.WriteAllText(Path.Combine(_dir, "readme.md"), "tax = 5");
        File.WriteAllText(Path.Combine(_dir, "thumbnail.png"), "keine echten Bilddaten, nur zum Testen");

        var files = FileSearchService.EnumerateSearchableFiles(_dir).ToList();

        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.EndsWith("common.txt"));
        Assert.Contains(files, f => f.EndsWith("interface.gui"));
    }

    [Fact]
    public void EnumerateSearchableFiles_DurchsuchtUnterordnerRekursiv()
    {
        var subDir = Path.Combine(_dir, "common", "idea_groups");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "ideas.txt"), "tax = 5");

        var files = FileSearchService.EnumerateSearchableFiles(_dir).ToList();

        Assert.Single(files);
        Assert.EndsWith("ideas.txt", files[0]);
    }

    [Fact]
    public void Search_FindetTrefferUeberMehrereDateienHinweg()
    {
        File.WriteAllText(Path.Combine(_dir, "a.txt"), "tax = 5\nunrelated = 1");
        File.WriteAllText(Path.Combine(_dir, "b.txt"), "unrelated = 2");
        var subDir = Path.Combine(_dir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "c.txt"), "tax = 10\ntax = 15");

        var results = FileSearchService.Search(_dir, "tax", new SearchOptions());

        Assert.Equal(3, results.Count);
        Assert.Equal(2, results.Count(r => r.FilePath.EndsWith("c.txt")));
        Assert.Equal(1, results.Count(r => r.FilePath.EndsWith("a.txt")));
        Assert.DoesNotContain(results, r => r.FilePath.EndsWith("b.txt"));
    }

    [Fact]
    public void Search_LeererOrdner_LiefertLeereListe()
    {
        var results = FileSearchService.Search(_dir, "tax", new SearchOptions());

        Assert.Empty(results);
    }
}
