using Xunit;

namespace ParadoxModTool.Explorer.Tests;

public class ExplorerSearchTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pmt-search-").FullName;

    public ExplorerSearchTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "in_game", "gui"));
        Directory.CreateDirectory(Path.Combine(_root, "common"));
        File.WriteAllText(Path.Combine(_root, "in_game", "gui", "left_panel.gui"), "x");
        File.WriteAllText(Path.Combine(_root, "in_game", "gui", "right_panel.gui"), "x");
        File.WriteAllText(Path.Combine(_root, "common", "Ideas.txt"), "x");
        File.WriteAllText(Path.Combine(_root, "root.txt"), "x");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private ExplorerSearchResult Search(string query, int limit = 500) =>
        ExplorerSearch.Run(new[] { (_root, "Mod") }, NamePattern.Parse(query)!, limit, CancellationToken.None);

    [Theory]
    [InlineData("panel", "left_panel.gui", true)]
    [InlineData("PANEL", "left_panel.gui", true)]
    [InlineData("*.gui", "left_panel.gui", true)]
    [InlineData("*.gui", "Ideas.txt", false)]
    [InlineData("left*", "left_panel.gui", true)]
    [InlineData("left*", "right_left.gui", false)]
    [InlineData("i?eas.txt", "Ideas.txt", true)]
    [InlineData("i?as.txt", "Ideas.txt", false)]
    public void NamePattern_MatchtPlatzhalterUndTeilstring(string query, string name, bool expected)
    {
        Assert.Equal(expected, NamePattern.Parse(query)!.IsMatch(name));
    }

    [Fact]
    public void NamePattern_LeererText_LiefertNull()
    {
        Assert.Null(NamePattern.Parse("   "));
        Assert.Null(NamePattern.Parse(null));
    }

    [Fact]
    public void NamePattern_RegexSonderzeichenWerdenWoertlichBehandelt()
    {
        Assert.True(NamePattern.Parse("a.b*")!.IsMatch("a.bxyz"));
        Assert.False(NamePattern.Parse("a.b*")!.IsMatch("aXbxyz"));
    }

    [Fact]
    public void Run_FindetDateienUndOrdnerRekursiv()
    {
        var names = Search("gui").Hits.Select(h => h.Name).ToList();

        Assert.Contains("gui", names);            // Ordner
        Assert.Contains("left_panel.gui", names); // Datei
        Assert.Contains("right_panel.gui", names);
    }

    [Fact]
    public void Run_OhneUgcVerweise_FindetSieNicht()
    {
        File.WriteAllText(Path.Combine(_root, "ugc_123.mod"), "x");

        var hidden = ExplorerSearch.Run(new[] { (_root, "Mod") }, NamePattern.Parse("ugc")!, 500, CancellationToken.None, hideUgcReferences: true).Hits;
        var shown = ExplorerSearch.Run(new[] { (_root, "Mod") }, NamePattern.Parse("ugc")!, 500, CancellationToken.None).Hits;

        Assert.Empty(hidden);
        Assert.Single(shown);
    }

    [Fact]
    public void Run_NurOrdner_LiefertKeineDateien()
    {
        var hits = ExplorerSearch.Run(new[] { (_root, "Mod") }, NamePattern.Parse("gui")!, 500, CancellationToken.None, foldersOnly: true).Hits;

        var hit = Assert.Single(hits);
        Assert.Equal("gui", hit.Name);
        Assert.True(hit.IsDirectory);
    }

    [Fact]
    public void Run_MarkiertOrdnerUndLiefertLesbarenSpeicherort()
    {
        var folder = Search("common").Hits.Single();
        var file = Search("left_panel").Hits.Single();

        Assert.True(folder.IsDirectory);
        Assert.Equal("Mod", folder.Location);
        Assert.False(file.IsDirectory);
        Assert.Equal(Path.Combine("Mod", "in_game", "gui"), file.Location);
    }

    [Fact]
    public void Run_LimitErreicht_MeldetAbgeschnitten()
    {
        var result = Search("panel", limit: 1);

        Assert.Single(result.Hits);
        Assert.True(result.Truncated);
    }

    [Fact]
    public void Run_UnterLimit_NichtAbgeschnitten()
    {
        Assert.False(Search("panel").Truncated);
    }

    [Fact]
    public void Run_FlacheTrefferZuerst()
    {
        var hits = ExplorerSearch.Run(new[] { (_root, "Mod") }, NamePattern.Parse("txt")!, 500, CancellationToken.None).Hits;

        Assert.Equal("root.txt", hits[0].Name);
    }

    [Fact]
    public void Run_Abbruch_WirftOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            ExplorerSearch.Run(new[] { (_root, "Mod") }, NamePattern.Parse("x")!, 500, cts.Token));
    }

    [Fact]
    public void Run_FehlendeWurzel_WirdUebersprungen()
    {
        var result = ExplorerSearch.Run(new[] { (Path.Combine(_root, "nope"), "X") }, NamePattern.Parse("a")!, 500, CancellationToken.None);

        Assert.Empty(result.Hits);
    }
}
