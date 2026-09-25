using Xunit;

namespace ParadoxModTool.Explorer.Tests;

public class ExpandedPathSetTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-expanded-" + Guid.NewGuid().ToString("N"));

    public ExpandedPathSetTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    private string MakeDir(string name)
    {
        var path = Path.Combine(_dir, name);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void Contains_IgnoriertGrossKleinschreibungUndAbschliessendenTrenner()
    {
        var set = new ExpandedPathSet();
        var a = MakeDir("Common");

        set.Add(a);

        Assert.True(set.Contains(a.ToUpperInvariant()));
        Assert.True(set.Contains(a + Path.DirectorySeparatorChar));
        Assert.False(set.Contains(Path.Combine(_dir, "Common2")));
    }

    [Fact]
    public void Remove_EntferntNurDiesenPfad()
    {
        var set = new ExpandedPathSet();
        var a = MakeDir("a");
        var b = MakeDir("b");
        set.Add(a);
        set.Add(b);

        set.Remove(a);

        Assert.False(set.Contains(a));
        Assert.True(set.Contains(b));
    }

    [Fact]
    public void Capture_LiefertNurNochVorhandeneOrdner()
    {
        var set = new ExpandedPathSet();
        var a = MakeDir("a");
        var gone = MakeDir("gone");
        set.Add(a);
        set.Add(gone);
        Directory.Delete(gone);

        Assert.Equal(new[] { a }, set.Capture().ToArray());
    }

    [Fact]
    public void Load_ErsetztDenInhalt_UndIgnoriertLeereEintraege()
    {
        var set = new ExpandedPathSet();
        var a = MakeDir("a");
        var b = MakeDir("b");
        set.Add(a);

        set.Load(new[] { b, string.Empty, "  " });

        Assert.False(set.Contains(a));
        Assert.True(set.Contains(b));
    }
}
