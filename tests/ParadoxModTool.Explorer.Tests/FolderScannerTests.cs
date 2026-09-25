using Xunit;

namespace ParadoxModTool.Explorer.Tests;

public class FolderScannerTests : IDisposable
{
    private readonly string _root;

    public FolderScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pmt-explorer-scan-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    [Fact]
    public void GetChildren_ListetOrdnerVorDateienUndBeideAlphabetisch()
    {
        Directory.CreateDirectory(Path.Combine(_root, "zzz_ordner"));
        Directory.CreateDirectory(Path.Combine(_root, "aaa_ordner"));
        File.WriteAllText(Path.Combine(_root, "b.txt"), "b");
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");

        var children = FolderScanner.GetChildren(_root);

        Assert.Equal(4, children.Count);
        Assert.True(children[0].IsDirectory);
        Assert.Equal("aaa_ordner", children[0].Name);
        Assert.True(children[1].IsDirectory);
        Assert.Equal("zzz_ordner", children[1].Name);
        Assert.False(children[2].IsDirectory);
        Assert.Equal("a.txt", children[2].Name);
        Assert.False(children[3].IsDirectory);
        Assert.Equal("b.txt", children[3].Name);
    }

    [Fact]
    public void GetChildren_FehlenderOrdner_LiefertLeereListeStattAusnahme()
    {
        var missing = Path.Combine(_root, "gibt-es-nicht");

        var children = FolderScanner.GetChildren(missing);

        Assert.Empty(children);
    }

    [Fact]
    public void GetChildren_LeererOrdner_LiefertLeereListe()
    {
        var children = FolderScanner.GetChildren(_root);

        Assert.Empty(children);
    }
}
