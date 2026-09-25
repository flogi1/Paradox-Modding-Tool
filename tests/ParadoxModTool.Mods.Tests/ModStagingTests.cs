using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModStagingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-stage-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string Put(string relative, string content = "x")
    {
        var path = Path.Combine(_dir, "mod", relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private string Source => Path.Combine(_dir, "mod");

    private void BuildTree()
    {
        Put("a.txt", "12345");
        Put("common/b.txt", "123");
        Put(".metadata/metadata.json", "{}");
        Put(".pmt-versions/dev/y.txt");
        Put("deep/Snapshots/z.txt");
        Put("deep/keep.txt");
        Put("deep/Thumbs.db");
        Put("mod.ZIP");
        Put(".git/config");
        Put("backup.bak");
    }

    [Fact]
    public void Measure_ZaehltNurWasMitgehtUndListetDenRest()
    {
        BuildTree();

        var result = ModStaging.Measure(Source, ModStagingRules.Default);

        Assert.Equal(4, result.FileCount);
        Assert.Equal(5 + 3 + 2 + 1, result.TotalBytes);
        Assert.Contains(".pmt-versions/", result.Excluded);
        Assert.Contains("deep/Snapshots/", result.Excluded);
        Assert.Contains(".git/", result.Excluded);
        Assert.Contains("mod.ZIP", result.Excluded);
        Assert.Contains("deep/Thumbs.db", result.Excluded);
        Assert.Contains("backup.bak", result.Excluded);
        Assert.DoesNotContain(result.Excluded, e => e.StartsWith(".metadata", StringComparison.Ordinal));
    }

    [Fact]
    public void Stage_KopiertNurDieErlaubtenDateien_UndLaesstDasOriginalUnberuehrt()
    {
        BuildTree();
        var target = Path.Combine(_dir, "stage");

        var result = ModStaging.Stage(Source, target, ModStagingRules.Default);

        var copied = Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(target, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(new[] { ".metadata/metadata.json", "a.txt", "common/b.txt", "deep/keep.txt" }, copied);
        Assert.Equal(4, result.FileCount);
        Assert.True(File.Exists(Path.Combine(Source, ".pmt-versions", "dev", "y.txt")));
        Assert.True(File.Exists(Path.Combine(Source, "mod.ZIP")));
    }

    [Fact]
    public void Stage_OrdnerAnBeliebigerTiefeUndOhneGrossKleinschreibung()
    {
        Put("a/b/.GIT/x");
        Put("a/b/SNAPSHOTS/y");
        Put("a/b/ok.txt");

        var result = ModStaging.Measure(Source, ModStagingRules.Default);

        Assert.Equal(1, result.FileCount);
        Assert.Equal(new[] { "a/b/.GIT/", "a/b/SNAPSHOTS/" }, result.Excluded.OrderBy(e => e, StringComparer.Ordinal));
    }

    [Fact]
    public void LeererOrdner_ErgibtNullDateien_UndFehlenderOrdnerWirft()
    {
        Directory.CreateDirectory(Source);

        Assert.Equal(0, ModStaging.Measure(Source, ModStagingRules.Default).FileCount);
        Assert.Throws<DirectoryNotFoundException>(() => ModStaging.Measure(Path.Combine(_dir, "gibtsnicht"), ModStagingRules.Default));
    }

    [Fact]
    public void EigeneRegeln_ErsetzenDieStandardregeln()
    {
        Put("a.txt");
        Put("skip/b.txt");
        Put("c.log");

        var result = ModStaging.Measure(Source, new ModStagingRules(new[] { "skip" }, new[] { "*.log" }));

        Assert.Equal(1, result.FileCount);
        Assert.Equal(2, result.Excluded.Count);
    }
}
