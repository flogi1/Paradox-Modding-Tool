using ParadoxModTool.GameProfiles;
using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class ModCompatCheckerFileDiscoveryTests : IDisposable
{
    private readonly string _vanillaRoot;
    private readonly string _modARoot;
    private readonly string _modBRoot;

    public ModCompatCheckerFileDiscoveryTests()
    {
        _vanillaRoot = Directory.CreateTempSubdirectory("pmt-vanilla-").FullName;
        _modARoot = Directory.CreateTempSubdirectory("pmt-modA-").FullName;
        _modBRoot = Directory.CreateTempSubdirectory("pmt-modB-").FullName;
    }

    public void Dispose()
    {
        TryDelete(_vanillaRoot);
        TryDelete(_modARoot);
        TryDelete(_modBRoot);
    }

    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    private static void WriteFile(string root, string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    [Fact]
    public void FindCommonlyModifiedFiles_ErkenntNurVonBeidenModsUeberschriebeneDateien()
    {
        WriteFile(_vanillaRoot, "common/ideas/idea1.txt", "vanilla-idea1");
        WriteFile(_vanillaRoot, "common/ideas/idea2.txt", "vanilla-idea2");
        WriteFile(_vanillaRoot, "gui/some.gui", "vanilla-gui");

        WriteFile(_modARoot, "common/ideas/idea1.txt", "mod-a-idea1");
        WriteFile(_modARoot, "gui/some.gui", "mod-a-gui");

        WriteFile(_modBRoot, "common/ideas/idea1.txt", "mod-b-idea1");
        WriteFile(_modBRoot, "common/ideas/idea2.txt", "mod-b-idea2");

        var profile = new GameProfile { Id = "test-game", LoadOrderRule = VanillaFileTracker.AlphabeticalFilenameWinsRule };

        var common = ModCompatChecker.FindCommonlyModifiedFiles(_vanillaRoot, _modARoot, _modBRoot, profile);

        var single = Assert.Single(common);
        Assert.Equal("common/ideas/idea1.txt", single.RelativePath);
        Assert.EndsWith(Path.Combine("common", "ideas", "idea1.txt"), single.ModAFullPath);
        Assert.Contains(_modARoot, single.ModAFullPath);
        Assert.Contains(_modBRoot, single.ModBFullPath);
        Assert.Contains(_vanillaRoot, single.VanillaFullPath);
    }

    [Fact]
    public void FindCommonlyModifiedFiles_KeineUeberschneidung_IstLeer()
    {
        WriteFile(_vanillaRoot, "common/a.txt", "vanilla-a");
        WriteFile(_vanillaRoot, "common/b.txt", "vanilla-b");

        WriteFile(_modARoot, "common/a.txt", "mod-a-a");
        WriteFile(_modBRoot, "common/b.txt", "mod-b-b");

        var profile = new GameProfile { Id = "test-game", LoadOrderRule = VanillaFileTracker.AlphabeticalFilenameWinsRule };

        var common = ModCompatChecker.FindCommonlyModifiedFiles(_vanillaRoot, _modARoot, _modBRoot, profile);

        Assert.Empty(common);
    }
}
