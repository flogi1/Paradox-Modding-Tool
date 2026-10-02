using Xunit;

namespace ParadoxModTool.Release.Tests;

public sealed class ReleasePathsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pmt-release-paths-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void FindRootWalksUp()
    {
        var tool = Directory.CreateDirectory(Path.Combine(_root, "release-tool")).FullName;
        Assert.Null(ReleasePaths.FindRoot(tool));

        File.WriteAllText(Path.Combine(_root, "ParadoxModTool.slnx"), "<Solution />");

        Assert.Equal(_root, ReleasePaths.FindRoot(tool));
        Assert.Equal(Path.Combine(_root, "Readme", "Changelog.txt"), new ReleasePaths(_root).Changelog);
    }

    [Fact]
    public void ToolSourcesNewerThanIgnoresBinAndObj()
    {
        var t = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        var source = Path.Combine(_root, "tools", "ParadoxModTool.Release", "A.cs");
        var built = Path.Combine(_root, "tools", "ParadoxModTool.Release", "bin", "X.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(built)!);
        File.WriteAllText(source, "class A {}");
        File.WriteAllText(built, "class X {}");
        File.SetLastWriteTimeUtc(source, t);
        File.SetLastWriteTimeUtc(built, t.AddHours(1));
        var paths = new ReleasePaths(_root);

        Assert.False(paths.ToolSourcesNewerThan(t.AddMinutes(1)));
        Assert.True(paths.ToolSourcesNewerThan(t.AddMinutes(-1)));
    }
}
