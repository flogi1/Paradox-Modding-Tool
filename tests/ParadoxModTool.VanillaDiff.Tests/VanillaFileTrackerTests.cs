using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class VanillaFileTrackerTests : IDisposable
{
    private readonly string _vanillaRoot;
    private readonly string _modRoot;

    public VanillaFileTrackerTests()
    {
        _vanillaRoot = Directory.CreateTempSubdirectory("pmt-vanilla-").FullName;
        _modRoot = Directory.CreateTempSubdirectory("pmt-mod-").FullName;
    }

    public void Dispose()
    {
        TryDelete(_vanillaRoot);
        TryDelete(_modRoot);
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

    private void WriteFile(string root, string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    [Fact]
    public void FindOverriddenFiles_ErkenntNurGleichnamigeDateien()
    {
        WriteFile(_vanillaRoot, "common/ideas/idea1.txt", "vanilla-idea1");
        WriteFile(_vanillaRoot, "common/ideas/idea2.txt", "vanilla-idea2");
        WriteFile(_vanillaRoot, "gui/some.gui", "vanilla-gui");

        WriteFile(_modRoot, "common/ideas/idea1.txt", "mod-idea1");
        WriteFile(_modRoot, "common/ideas/new_idea.txt", "mod-only-idea");

        var profile = new GameProfile { Id = "test-game", LoadOrderRule = VanillaFileTracker.AlphabeticalFilenameWinsRule };

        var overridden = VanillaFileTracker.FindOverriddenFiles(_vanillaRoot, _modRoot, profile);

        var single = Assert.Single(overridden);
        Assert.Equal("common/ideas/idea1.txt", single.RelativePath);
    }

    [Fact]
    public void FindOverriddenFiles_IgnoriertInterneToolOrdner()
    {
        WriteFile(_vanillaRoot, "manifest.json", "vanilla-manifest");
        WriteFile(_modRoot, ".pmt-versions/dev/manifest.json", "sollte-nicht-zaehlen");

        var profile = new GameProfile { Id = "test-game", LoadOrderRule = VanillaFileTracker.AlphabeticalFilenameWinsRule };

        var overridden = VanillaFileTracker.FindOverriddenFiles(_vanillaRoot, _modRoot, profile);

        Assert.Empty(overridden);
    }

    [Fact]
    public void FindOverriddenFiles_UnbekannteLadeRegel_WirftNotSupportedException()
    {
        var profile = new GameProfile { Id = "test-game", LoadOrderRule = "unbekannte-regel" };

        Assert.Throws<NotSupportedException>(() => VanillaFileTracker.FindOverriddenFiles(_vanillaRoot, _modRoot, profile));
    }
}
