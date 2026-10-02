using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class CodeExcerptTests
{
    [Fact]
    public void ReadsFiveLinesAroundTheTargetAndMarksIt()
    {
        using var tree = new TempTree(("a.txt", string.Join("\n", Enumerable.Range(1, 20).Select(i => "zeile " + i))));

        var lines = CodeExcerpt.Read(tree.PathOf("a.txt"), 10);

        Assert.Equal(Enumerable.Range(5, 11), lines.Select(l => l.Number));
        Assert.Equal("zeile 10", lines.Single(l => l.IsTarget).Text);
    }

    [Fact]
    public void ClampsAtTheStartOfTheFile()
    {
        using var tree = new TempTree(("a.txt", "eins\nzwei\ndrei\n"));

        var lines = CodeExcerpt.Read(tree.PathOf("a.txt"), 1);

        Assert.Equal(new[] { 1, 2, 3 }, lines.Select(l => l.Number));
        Assert.True(lines[0].IsTarget);
    }

    [Fact]
    public void MissingFileOrLineOutsideGivesNothing()
    {
        using var tree = new TempTree(("a.txt", "eins\n"));

        Assert.Empty(CodeExcerpt.Read(tree.PathOf("fehlt.txt"), 1));
        Assert.Empty(CodeExcerpt.Read(tree.PathOf("a.txt"), 7));
        Assert.Empty(CodeExcerpt.Read(tree.PathOf("a.txt"), 0));
    }
}
