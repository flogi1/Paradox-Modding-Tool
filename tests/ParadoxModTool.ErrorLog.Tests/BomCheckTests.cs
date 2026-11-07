using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class BomCheckTests
{
    private static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };
    private static readonly byte[] Body = "namespace = test\r\nx = { a = \"ä\" }\n"u8.ToArray();

    private static GameProfile Profile(string game) => GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", game + ".json"));

    private static void Write(TempTree tree, string relative, bool bom)
    {
        var path = tree.PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bom ? Bom.Concat(Body).ToArray() : Body);
    }

    private static TempTree MixedMod()
    {
        var tree = new TempTree();
        Write(tree, "common/scripted_effects/bom_effects.txt", bom: true);
        Write(tree, "common/scripted_effects/plain_effects.txt", bom: false);
        Write(tree, "interface/bom.gui", bom: true);
        Write(tree, "interface/bom.gfx", bom: true);
        Write(tree, "interface/plain.gfx", bom: false);
        Write(tree, "localisation/english/bom_l_english.yml", bom: true);
        Write(tree, "gfx/readme.md", bom: true);
        Write(tree, ".git/config.txt", bom: true);
        Write(tree, ".pmt-patches/x.txt", bom: true);
        Write(tree, "snapshots/2026-10-28/common/x.txt", bom: true);
        return tree;
    }

    [Fact]
    public void FindsScriptGuiAndGfxFilesWithBomInStellaris()
    {
        using var tree = MixedMod();

        var found = BomCheck.Find(Profile("stellaris"), tree.Root, new[] { tree.PathOf("snapshots") });

        Assert.Equal(
            new[] { "common/scripted_effects/bom_effects.txt", "interface/bom.gfx", "interface/bom.gui" },
            found);
    }

    [Fact]
    public void IgnoresLocalisationFilesWithoutBomAndHiddenFolders()
    {
        using var tree = MixedMod();

        var found = BomCheck.Find(Profile("stellaris"), tree.Root);

        Assert.DoesNotContain(found, p => p.EndsWith(".yml", StringComparison.Ordinal));
        Assert.DoesNotContain(found, p => p.Contains("plain", StringComparison.Ordinal));
        Assert.DoesNotContain(found, p => p.StartsWith(".", StringComparison.Ordinal));
        Assert.DoesNotContain(found, p => p.EndsWith(".md", StringComparison.Ordinal));
        Assert.Contains("snapshots/2026-10-28/common/x.txt", found);
    }

    [Fact]
    public void OtherGameProfilesWithBomEncodingFindNothing()
    {
        using var tree = MixedMod();

        Assert.Empty(BomCheck.Find(Profile("ck3"), tree.Root));
        Assert.Empty(BomCheck.Find(Profile("eu5"), tree.Root));
        Assert.Empty(BomCheck.Find(null, tree.Root));
    }

    [Fact]
    public void FilesDirectlyInTheModFolderAreNotScript()
    {
        using var tree = new TempTree();
        Write(tree, "readme.txt", bom: true);
        Write(tree, "changelog.txt", bom: true);
        Write(tree, "events/a.txt", bom: true);

        Assert.Equal(new[] { "events/a.txt" }, BomCheck.Find(Profile("stellaris"), tree.Root));
    }

    [Fact]
    public void LinkedFoldersAreNotFollowed()
    {
        using var tree = new TempTree();
        using var target = new TempTree();
        Write(tree, "events/a.txt", bom: true);
        Write(target, "events/b.txt", bom: true);
        if (!TryLink(tree.PathOf("linked"), target.Root))
        {
            return; // Weder symbolische Verknüpfung noch Junction anlegbar: nichts zu prüfen.
        }

        try
        {
            Assert.Equal(new[] { "events/a.txt" }, BomCheck.Find(Profile("stellaris"), tree.Root));
        }
        finally
        {
            // Nur die Verknüpfung löschen, nicht das Ziel (rekursives Löschen verweigert Windows bei einer Junction).
            Directory.Delete(tree.PathOf("linked"));
        }
    }

    private static bool TryLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        process!.WaitForExit(10000);
        return Directory.Exists(link) && new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    [Fact]
    public void FindInModsScansEveryOwnModAndNamesTheMod()
    {
        using var tree = new TempTree();
        Write(tree, "galaxy/common/a.txt", bom: true);
        Write(tree, "galaxy/snapshots/x/common/s.txt", bom: true);
        Write(tree, "slaves/interface/b.gui", bom: true);
        Write(tree, "slaves/interface/c.gui", bom: false);

        var found = BomCheck.FindInMods(Profile("stellaris"), new[]
        {
            new BomScanRoot("Galaxy", tree.PathOf("galaxy"), new[] { tree.PathOf("galaxy/snapshots") }),
            new BomScanRoot("Slaves", tree.PathOf("slaves"), Array.Empty<string>()),
            new BomScanRoot("Missing", tree.PathOf("missing"), Array.Empty<string>()),
        });

        Assert.Equal(
            new[] { ("Galaxy", "common/a.txt", tree.PathOf("galaxy/common/a.txt")), ("Slaves", "interface/b.gui", tree.PathOf("slaves/interface/b.gui")) },
            found.Select(f => (f.ModName, f.RelativePath, f.FullPath)));
        Assert.Equal(tree.PathOf("galaxy"), found[0].ModRoot);
    }

    [Fact]
    public void FindInModsSkipsAnInvalidModPathButScansTheOthers()
    {
        using var tree = new TempTree();
        Write(tree, "galaxy/common/a.txt", bom: true);

        var found = BomCheck.FindInMods(Profile("stellaris"), new[]
        {
            new BomScanRoot("Broken", "bad\0path", Array.Empty<string>()),
            new BomScanRoot("Galaxy", tree.PathOf("galaxy"), Array.Empty<string>()),
        });

        Assert.Equal(new[] { ("Galaxy", "common/a.txt") }, found.Select(f => (f.ModName, f.RelativePath)));
    }

    [Fact]
    public void FindInModsListsAFileOfNestedOrRepeatedModFoldersOnce()
    {
        using var tree = new TempTree();
        Write(tree, "outer/common/a.txt", bom: true);
        Write(tree, "outer/inner/common/b.txt", bom: true);

        var found = BomCheck.FindInMods(Profile("stellaris"), new[]
        {
            new BomScanRoot("Outer", tree.PathOf("outer"), Array.Empty<string>()),
            new BomScanRoot("Inner", tree.PathOf("outer/inner"), Array.Empty<string>()),
            new BomScanRoot("Outer again", tree.PathOf("outer") + Path.DirectorySeparatorChar, Array.Empty<string>()),
        });

        Assert.Equal(
            new[] { ("Inner", "common/b.txt"), ("Outer", "common/a.txt") },
            found.Select(f => (f.ModName, f.RelativePath)));
    }

    [Fact]
    public void MissingModFolderFindsNothing()
    {
        Assert.Empty(BomCheck.Find(Profile("stellaris"), Path.Combine(Path.GetTempPath(), "pmt-bom-missing-" + Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void WithoutBomKeepsEveryOtherByte()
    {
        var invalidUtf8 = new byte[] { 0xEF, 0xBB, 0xBF, 0x61, 0x0D, 0x0A, 0xE4, 0xFF, 0x0A, 0xEF, 0xBB, 0xBF };

        Assert.Equal(Body, BomCheck.WithoutBom(Bom.Concat(Body).ToArray()));
        Assert.Equal(invalidUtf8[3..], BomCheck.WithoutBom(invalidUtf8));
        Assert.Equal(Array.Empty<byte>(), BomCheck.WithoutBom(Bom));
    }

    [Fact]
    public void ReadWithoutBomReturnsTheFileBodyAndLeavesFilesWithoutBomUntouched()
    {
        using var tree = new TempTree();
        Write(tree, "common/a.txt", bom: true);
        Write(tree, "common/b.txt", bom: false);
        var plain = tree.PathOf("common/b.txt");
        var before = File.GetLastWriteTimeUtc(plain);

        Assert.Equal(Body, BomCheck.ReadWithoutBom(tree.PathOf("common/a.txt")));
        Assert.Null(BomCheck.ReadWithoutBom(plain));
        Assert.Equal(Body, File.ReadAllBytes(plain));
        Assert.Equal(before, File.GetLastWriteTimeUtc(plain));
        Assert.Equal(Bom.Concat(Body).ToArray(), File.ReadAllBytes(tree.PathOf("common/a.txt")));
    }

    [Fact]
    public void StampFromReadStaysUnchangedUntilTheFileIsSavedAgain()
    {
        using var tree = new TempTree();
        Write(tree, "common/a.txt", bom: true);
        var path = tree.PathOf("common/a.txt");

        Assert.Equal(Body, BomCheck.ReadWithoutBom(path, out var stamp));
        Assert.True(BomCheck.Unchanged(path, stamp));

        // Gespeichert nach dem Lesen: andere Länge.
        File.WriteAllBytes(path, Bom.Concat(Body).Concat("y = 1\n"u8.ToArray()).ToArray());
        Assert.False(BomCheck.Unchanged(path, stamp));
    }

    [Fact]
    public void StampSeesSameLengthSaveByWriteTimeAndMissingFileAsChanged()
    {
        using var tree = new TempTree();
        Write(tree, "common/a.txt", bom: true);
        var path = tree.PathOf("common/a.txt");
        BomCheck.ReadWithoutBom(path, out var stamp);

        // Gleiche Länge, neue Änderungszeit.
        File.WriteAllBytes(path, Bom.Concat(Body).ToArray());
        File.SetLastWriteTimeUtc(path, stamp.LastWriteUtc.AddSeconds(5));
        Assert.False(BomCheck.Unchanged(path, stamp));

        File.Delete(path);
        Assert.False(BomCheck.Unchanged(path, stamp));
    }

    [Fact]
    public void WithoutBomLeavesFilesWithoutBomAlone()
    {
        Assert.Null(BomCheck.WithoutBom(Body));
        Assert.Null(BomCheck.WithoutBom(new byte[] { 0xEF, 0xBB }));
        Assert.Null(BomCheck.WithoutBom(Array.Empty<byte>()));
    }
}
