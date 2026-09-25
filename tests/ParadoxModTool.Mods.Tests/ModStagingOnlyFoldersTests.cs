using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModStagingOnlyFoldersTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-only-" + Guid.NewGuid().ToString("N"));

    public ModStagingOnlyFoldersTests()
    {
        Put("common/a.txt");
        Put("common/sub/x.txt");
        Put("gfx/b.png");
        Put(".metadata/metadata.json");
        Put("readme.txt");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private void Put(string relative)
    {
        var path = Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
    }

    [Fact]
    public void OnlyFolders_LaesstNurDateienDaruntergelten()
    {
        var rules = ModStagingRules.Default.Extend(null, null, new[] { "common" });

        var result = ModStaging.Measure(_dir, rules);

        Assert.Equal(2, result.FileCount);
        Assert.Contains("gfx/", result.Excluded);
        Assert.Contains(".metadata/", result.Excluded);
        Assert.Contains("readme.txt", result.Excluded);
    }

    [Fact]
    public void OnlyFolders_Unterordner_DurchlaeuftDenWeg()
    {
        var rules = ModStagingRules.Default.Extend(null, null, new[] { "common/sub" });

        var result = ModStaging.Measure(_dir, rules);

        Assert.Equal(1, result.FileCount);
        Assert.Contains("common/a.txt", result.Excluded);
    }

    [Fact]
    public void OnlyFolders_IgnoriertGrossKleinschreibung_UndSchraegstrichArt()
    {
        var rules = ModStagingRules.Default.Extend(null, null, new[] { "\\Common\\" });

        Assert.Equal(2, ModStaging.Measure(_dir, rules).FileCount);
    }

    [Fact]
    public void OhneOnlyFolders_BleibtAllesWieBisher()
    {
        Assert.Equal(5, ModStaging.Measure(_dir, ModStagingRules.Default).FileCount);
    }

    [Fact]
    public void Extend_ErgaenztAusschluesse()
    {
        var rules = ModStagingRules.Default.Extend(new[] { "gfx" }, new[] { "*.txt" }, null);

        var result = ModStaging.Measure(_dir, rules);

        Assert.Equal(1, result.FileCount);
    }

    [Fact]
    public void Stage_KopiertNurDieAuswahl()
    {
        var target = Path.Combine(_dir + "-out");
        try
        {
            ModStaging.Stage(_dir, target, ModStagingRules.Default.Extend(null, null, new[] { "common" }));

            Assert.True(File.Exists(Path.Combine(target, "common", "sub", "x.txt")));
            Assert.False(File.Exists(Path.Combine(target, "readme.txt")));
        }
        finally
        {
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
    }
}
