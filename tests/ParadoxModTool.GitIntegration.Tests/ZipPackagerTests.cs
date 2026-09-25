using System.IO.Compression;
using ParadoxModTool.Mods;
using Xunit;

namespace ParadoxModTool.GitIntegration.Tests;

public class ZipPackagerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-zip-" + Guid.NewGuid().ToString("N"));

    public ZipPackagerTests()
    {
        Put("mod/.metadata/metadata.json");
        Put("mod/common/a.txt");
        Put("mod/gfx/b.png");
        Put("mod/docs/notes.md");
        Put("mod/.git/config");
        Put("mod/old.zip");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string Mod => Path.Combine(_dir, "mod");

    private string Out => Path.Combine(_dir, "out");

    private void Put(string relative)
    {
        var path = Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "inhalt");
    }

    private static GitHubZipVariant Standard() => new() { Name = "Standard" };

    private static string[] Entries(string zip)
    {
        using var archive = ZipFile.OpenRead(zip);
        return archive.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public void Standardvariante_PacktDenModOhneAusschluesse_ImWurzelverzeichnis()
    {
        var zips = ZipPackager.Build(Mod, "Meine Mod", "2.0.5", "v2.0.5", new[] { Standard() }, Out);

        var zip = Assert.Single(zips);
        Assert.Equal("Meine Mod-2.0.5.zip", zip.FileName);
        Assert.Equal(new[] { ".metadata/metadata.json", "common/a.txt", "docs/notes.md", "gfx/b.png" }, Entries(zip.Path));
        Assert.Equal(4, zip.FileCount);
    }

    [Fact]
    public void Varianten_MitOnlyFoldersAusschluessenUndWurzelordner()
    {
        var variants = new[]
        {
            new GitHubZipVariant { Name = "Nur Common", FileNameFormat = "{mod}-{variant}.zip", OnlyFolders = { "common" }, RootFolder = "MeineMod" },
            new GitHubZipVariant { Name = "Ohne Docs", FileNameFormat = "{mod}-ohne-docs.zip", ExcludedFolders = { "docs" }, ExcludedFilePatterns = { "*.png" } },
            new GitHubZipVariant { Name = "Aus", FileNameFormat = "aus.zip", Enabled = false },
        };

        var zips = ZipPackager.Build(Mod, "M", "1.0", "v1.0", variants, Out);

        Assert.Equal(2, zips.Count);
        Assert.Equal(new[] { "MeineMod/common/a.txt" }, Entries(zips[0].Path));
        Assert.Equal(new[] { ".metadata/metadata.json", "common/a.txt" }, Entries(zips[1].Path));
        Assert.False(File.Exists(Path.Combine(Out, "aus.zip")));
    }

    [Fact]
    public void Build_LaesstKeinenStagingOrdnerZurueck_UndUeberschreibtVorhandeneZip()
    {
        ZipPackager.Build(Mod, "M", "1.0", "v1.0", new[] { Standard() }, Out);
        ZipPackager.Build(Mod, "M", "1.0", "v1.0", new[] { Standard() }, Out);

        Assert.Equal(new[] { "M-1.0.zip" }, Directory.GetFileSystemEntries(Out).Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void Validate_ErkenntDoppelteDateinamenUndLeereVarianten()
    {
        var duplicate = new[]
        {
            new GitHubZipVariant { Name = "A", FileNameFormat = "gleich.zip" },
            new GitHubZipVariant { Name = "B", FileNameFormat = "GLEICH.zip" },
        };
        var empty = new[] { new GitHubZipVariant { Name = "Leer", OnlyFolders = { "gibt-es-nicht" } } };

        Assert.Contains(ZipPackager.Validate(Mod, "M", "1", "v1", duplicate), p => p.Contains("denselben Dateinamen"));
        Assert.Contains(ZipPackager.Validate(Mod, "M", "1", "v1", empty), p => p.Contains("keine Datei"));
        Assert.Empty(ZipPackager.Validate(Mod, "M", "1", "v1", new[] { Standard() }));
        Assert.Empty(ZipPackager.Validate(Mod, "M", "1", "v1", Array.Empty<GitHubZipVariant>()));
    }

    [Fact]
    public void Measure_ZaehltDieDateienDerVariante()
    {
        var result = ZipPackager.Measure(Mod, new GitHubZipVariant { Name = "X", OnlyFolders = { "gfx" } });

        Assert.Equal(1, result.FileCount);
    }
}
