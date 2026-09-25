using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class FolderComparerTests : TempDirTests
{
    private string Write(string folder, string relative, string content)
    {
        var path = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private IReadOnlyList<FileDifference> Compare(string left, string right) =>
        FolderComparer.Compare(ModFileIndex.Build(left), ModFileIndex.Build(right));

    [Fact]
    public void Compare_ErkenntAlleVierArten()
    {
        var left = Make("links");
        var right = Make("rechts");
        Write(left, "nur-links.txt", "x");
        Write(right, "nur-rechts.txt", "x");
        Write(left, "gleich.txt", "abc");
        Write(right, "gleich.txt", "abc");
        Write(left, "anders.txt", "abc");
        Write(right, "anders.txt", "abcd");

        var result = Compare(left, right).ToDictionary(d => d.RelativePath, d => d.Kind);

        Assert.Equal(DifferenceKind.OnlyLeft, result["nur-links.txt"]);
        Assert.Equal(DifferenceKind.OnlyRight, result["nur-rechts.txt"]);
        Assert.Equal(DifferenceKind.Same, result["gleich.txt"]);
        Assert.Equal(DifferenceKind.Different, result["anders.txt"]);
    }

    [Fact]
    public void Compare_GleicheGroesseAndererInhalt_IstDifferent()
    {
        var left = Make("links");
        var right = Make("rechts");
        Write(left, "a.txt", "aaaa");
        Write(right, "a.txt", "bbbb");

        Assert.Equal(DifferenceKind.Different, Compare(left, right).Single().Kind);
    }

    [Fact]
    public void Compare_GleicherInhaltAnderesDatum_IstSame()
    {
        var left = Make("links");
        var right = Make("rechts");
        Write(left, "a.txt", "inhalt");
        var rightFile = Write(right, "a.txt", "inhalt");
        File.SetLastWriteTimeUtc(rightFile, new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(DifferenceKind.Same, Compare(left, right).Single().Kind);
    }

    [Fact]
    public void Compare_IstNachPfadSortiertUndFuelltDieEintraege()
    {
        var left = Make("links");
        var right = Make("rechts");
        Write(left, "z.txt", "x");
        Write(left, "common/a.txt", "x");
        Write(right, "b.txt", "y");

        var result = Compare(left, right);

        Assert.Equal(new[] { "b.txt", "common/a.txt", "z.txt" }, result.Select(d => d.RelativePath).ToArray());
        Assert.Null(result[0].Left);
        Assert.NotNull(result[0].Right);
        Assert.NotNull(result[1].Left);
        Assert.Null(result[1].Right);
    }

    [Fact]
    public void Compare_AbgebrochenesToken_WirftOperationCanceled()
    {
        var left = Make("links");
        var right = Make("rechts");
        Write(left, "a.txt", "x");
        using var cts = new CancellationTokenSource();
        var indexLeft = ModFileIndex.Build(left);
        var indexRight = ModFileIndex.Build(right);
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => FolderComparer.Compare(indexLeft, indexRight, cts.Token));
    }
}
