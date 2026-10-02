using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModFileIndexTests : TempDirTests
{
    private string Write(string folder, string relative, string content)
    {
        var path = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void EnumerateFiles_LiefertRelativePfadeMitSchraegstrich()
    {
        var mod = Make("mod");
        Write(mod, "common/a.txt", "x");
        Write(mod, "b.txt", "y");

        var files = ModStaging.EnumerateFiles(mod, ModStagingRules.Default);

        Assert.Equal(new[] { "b.txt", "common/a.txt" }, files.OrderBy(f => f, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Default_rules_skip_the_patch_recipe_folder()
    {
        var mod = Make("mod");
        Write(mod, ".pmt-patches/x.json", "{}");
        Write(mod, "common/a.txt", "x");

        var files = ModStaging.EnumerateFiles(mod, ModStagingRules.Default);

        Assert.Equal(new[] { "common/a.txt" }, files);
    }

    [Fact]
    public void EnumerateFiles_BeachtetOnlyFolders()
    {
        var mod = Make("mod");
        Write(mod, "common/a.txt", "x");
        Write(mod, "gfx/b.txt", "y");

        var files = ModStaging.EnumerateFiles(mod, ModStagingRules.Default.Extend(null, null, new[] { "common" }));

        Assert.Equal(new[] { "common/a.txt" }, files);
    }

    [Fact]
    public void Build_WendetDieStagingRegelnAn()
    {
        var mod = Make("mod");
        Write(mod, "common/a.txt", "x");
        Write(mod, ".git/config", "x");
        Write(mod, ".pmt-versions/dev/f.txt", "x");
        Write(mod, "snapshots/s.zip", "x");
        Write(mod, "backup.zip", "x");

        var index = ModFileIndex.Build(mod);

        Assert.Equal(new[] { "common/a.txt" }, index.Entries.Keys.ToArray());
    }

    [Fact]
    public void Build_PfadeSindUnabhaengigVonDerSchreibweise()
    {
        var mod = Make("mod");
        Write(mod, "Common/A.txt", "x");

        var index = ModFileIndex.Build(mod);

        Assert.True(index.Entries.ContainsKey("common/a.TXT"));
    }

    [Fact]
    public void Build_MerktGroesseUndZeit()
    {
        var mod = Make("mod");
        var path = Write(mod, "a.txt", "12345");

        var entry = ModFileIndex.Build(mod).Entries["a.txt"];

        Assert.Equal(5, entry.Size);
        Assert.Equal(File.GetLastWriteTimeUtc(path), entry.LastWriteUtc);
    }

    [Fact]
    public void Build_LeererOrdner_ErgibtLeereListe()
    {
        Assert.Empty(ModFileIndex.Build(Make("leer")).Entries);
    }

    [Fact]
    public void Build_FehlenderOrdner_WirftDirectoryNotFound()
    {
        Assert.Throws<DirectoryNotFoundException>(() => ModFileIndex.Build(Path.Combine(Dir, "gibtsnicht")));
    }

    [Fact]
    public void Build_AbgebrochenesToken_WirftOperationCanceled()
    {
        var mod = Make("mod");
        Write(mod, "a.txt", "x");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => ModFileIndex.Build(mod, null, cts.Token));
    }

    [Fact]
    public void HashOf_GleicherInhaltGleicherHash_AndererInhaltAndererHash()
    {
        var one = Make("eins");
        var two = Make("zwei");
        Write(one, "a.txt", "gleich");
        Write(two, "a.txt", "gleich");
        Write(two, "b.txt", "anders");

        var indexOne = ModFileIndex.Build(one);
        var indexTwo = ModFileIndex.Build(two);

        Assert.Equal(indexOne.HashOf("a.txt"), indexTwo.HashOf("a.txt"));
        Assert.NotEqual(indexTwo.HashOf("a.txt"), indexTwo.HashOf("b.txt"));
    }
}
