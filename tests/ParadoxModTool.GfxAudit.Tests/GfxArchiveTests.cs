using Xunit;

namespace ParadoxModTool.GfxAudit.Tests;

public sealed class GfxArchiveTests : IDisposable
{
    private static readonly DateTime Clock = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-gfxarchive-" + Guid.NewGuid().ToString("N"));

    private string Mod => Path.Combine(_temp, "mod", "MyMod");

    private string Archive => Path.Combine(_temp, "mod", "MyMod_gfx_archiv");

    public GfxArchiveTests() => Directory.CreateDirectory(Mod);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temp, true);
        }
        catch (IOException)
        {
        }
    }

    private GfxArchive Make(Action<string, string>? move = null) =>
        new(Mod, () => Clock, move ?? ((from, to) => File.Move(from, to)));

    private static string Put(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private static string P(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void ArchiveDirectoryIsSibling()
    {
        Assert.Equal(Archive, new GfxArchive(Mod).ArchiveDirectory);
        Assert.Equal(Archive, new GfxArchive(Mod + Path.DirectorySeparatorChar).ArchiveDirectory);
        Assert.Equal(Archive, new GfxArchive(Mod + "/").ArchiveDirectory);
    }

    [Fact]
    public void MoveOutKeepsSubfolders()
    {
        var source = Put(Mod, "gfx/interface/a.dds", "A");

        var result = Make().MoveOut(new[] { "gfx/interface/a.dds" });

        Assert.Empty(result.Failed);
        var moved = Assert.Single(result.Moved);
        Assert.Equal(new GfxArchiveMove("gfx/interface/a.dds", "gfx/interface/a.dds"), moved);
        Assert.False(File.Exists(source));
        Assert.Equal("A", File.ReadAllText(P(Archive, "gfx/interface/a.dds")));
    }

    [Fact]
    public void MoveOutRemovesEmptiedFoldersOnly()
    {
        Put(Mod, "gfx/interface/a.dds", "A");
        Put(Mod, "gfx/b.dds", "B");

        Make().MoveOut(new[] { "gfx/interface/a.dds" });

        Assert.False(Directory.Exists(P(Mod, "gfx/interface")));
        Assert.True(Directory.Exists(P(Mod, "gfx")));
        Assert.True(File.Exists(P(Mod, "gfx/b.dds")));

        Make().MoveOut(new[] { "gfx/b.dds" });

        Assert.False(Directory.Exists(P(Mod, "gfx")));
        Assert.True(Directory.Exists(Mod));
    }

    [Fact]
    public void CollisionGetsDateSuffix()
    {
        Put(Archive, "gfx/a.dds", "old");
        Put(Mod, "gfx/a.dds", "new1");

        var first = Make().MoveOut(new[] { "gfx/a.dds" });

        Assert.Equal("gfx/a~20261106.dds", Assert.Single(first.Moved).ToRelative);

        Put(Mod, "gfx/a.dds", "new2");
        var second = Make().MoveOut(new[] { "gfx/a.dds" });

        Assert.Equal("gfx/a~20261106-2.dds", Assert.Single(second.Moved).ToRelative);
        Assert.Equal("old", File.ReadAllText(P(Archive, "gfx/a.dds")));
        Assert.Equal("new1", File.ReadAllText(P(Archive, "gfx/a~20261106.dds")));
        Assert.Equal("new2", File.ReadAllText(P(Archive, "gfx/a~20261106-2.dds")));
    }

    [Fact]
    public void MissingSourceReportedNotThrown()
    {
        var result = Make().MoveOut(new[] { "gfx/nope.dds" });

        Assert.Empty(result.Moved);
        Assert.Equal(new GfxArchiveFailure("gfx/nope.dds", GfxArchiveFailureReason.Missing, null), Assert.Single(result.Failed));
    }

    [Fact]
    public void PathOutsideModRefused()
    {
        var other = Put(Path.Combine(_temp, "mod", "Other"), "x.dds", "X");
        var paths = new[] { "../Other/x.dds", "C:/x.dds", "gfx/../../x.dds" };

        var result = Make().MoveOut(paths);

        Assert.Empty(result.Moved);
        Assert.Equal(3, result.Failed.Count);
        Assert.All(result.Failed, f => Assert.Equal(GfxArchiveFailureReason.OutsideFolder, f.Reason));
        Assert.True(File.Exists(other));
        Assert.False(Directory.Exists(Archive));
    }

    [Fact]
    public void NonImageRefused()
    {
        var source = Put(Mod, "common/x.txt", "T");

        var result = Make().MoveOut(new[] { "common/x.txt" });

        Assert.Equal(GfxArchiveFailureReason.NotAnImage, Assert.Single(result.Failed).Reason);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void IoErrorOnOneFileOthersMove()
    {
        Put(Mod, "gfx/a.dds", "A");
        Put(Mod, "gfx/b.dds", "B");
        Put(Mod, "gfx/c.dds", "C");

        var result = Make((from, to) =>
        {
            if (from.EndsWith("b.dds", StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("gesperrt");
            }

            File.Move(from, to);
        }).MoveOut(new[] { "gfx/a.dds", "gfx/b.dds", "gfx/c.dds" });

        Assert.Equal(new[] { "gfx/a.dds", "gfx/c.dds" }, result.Moved.Select(m => m.FromRelative));
        Assert.Equal(new GfxArchiveFailure("gfx/b.dds", GfxArchiveFailureReason.IoError, "gesperrt"), Assert.Single(result.Failed));
        Assert.True(File.Exists(P(Mod, "gfx/b.dds")));
    }

    [Fact]
    public void FailedMoveLeavesNoEmptyFolder()
    {
        Put(Mod, "gfx/new/a.dds", "A");
        Put(Archive, "keep/x.dds", "X");

        var failing = Make((from, to) => throw new IOException("gesperrt"));
        var result = failing.MoveOut(new[] { "gfx/new/a.dds" });

        Assert.Equal(GfxArchiveFailureReason.IoError, Assert.Single(result.Failed).Reason);
        Assert.False(Directory.Exists(P(Archive, "gfx")));
        Assert.True(File.Exists(P(Archive, "keep/x.dds")));
        Assert.True(File.Exists(P(Mod, "gfx/new/a.dds")));

        Put(Archive, "sub/deep/b.dds", "B");
        var restore = failing.Restore(new[] { "sub/deep/b.dds" });

        Assert.Equal(GfxArchiveFailureReason.IoError, Assert.Single(restore.Failed).Reason);
        Assert.False(Directory.Exists(P(Mod, "sub")));
        Assert.True(File.Exists(P(Archive, "sub/deep/b.dds")));
    }

    [Fact]
    public void FailedMoveRemovesNewArchiveFolderItself()
    {
        Put(Mod, "gfx/a.dds", "A");

        Make((from, to) => throw new IOException("gesperrt")).MoveOut(new[] { "gfx/a.dds" });

        Assert.False(Directory.Exists(Archive));
        Assert.True(Directory.Exists(Mod));
    }

    [Fact]
    public void DuplicatesAreMovedOnce()
    {
        Put(Mod, "gfx/a.dds", "A");

        var result = Make().MoveOut(new[] { "gfx/a.dds", "GFX/A.DDS", "gfx\\a.dds" });

        Assert.Single(result.Moved);
        Assert.Empty(result.Failed);
    }

    [Fact]
    public void ListIsEmptyWithoutArchive()
    {
        Assert.Empty(Make().List());
    }

    [Fact]
    public void ListShowsOriginalAndState()
    {
        Put(Archive, "gfx/a.dds", "old");
        Put(Mod, "gfx/a.dds", "new");
        Make().MoveOut(new[] { "gfx/a.dds" });

        var before = Assert.Single(Make().List(), f => f.ArchiveRelative == "gfx/a~20261106.dds");
        Assert.Equal("gfx/a.dds", before.OriginalRelative);
        Assert.False(before.OriginalExists);
        Assert.Equal(3, before.Size);
        Assert.Equal(P(Archive, "gfx/a~20261106.dds"), before.FullPath);

        Put(Mod, "gfx/a.dds", "again");

        var after = Make().List();
        Assert.True(after.Single(f => f.ArchiveRelative == "gfx/a~20261106.dds").OriginalExists);
        Assert.Equal(new[] { "gfx/a.dds", "gfx/a~20261106.dds" }, after.Select(f => f.ArchiveRelative));
    }

    [Fact]
    public void ListSkipsNonImages()
    {
        Put(Archive, "notes.txt", "x");
        Put(Archive, "gfx/a.png", "x");

        Assert.Equal(new[] { "gfx/a.png" }, Make().List().Select(f => f.ArchiveRelative));
    }

    [Fact]
    public void RestoreMovesBackAndCleansArchive()
    {
        Put(Mod, "gfx/interface/a.dds", "A");
        var archive = Make();
        archive.MoveOut(new[] { "gfx/interface/a.dds" });

        var result = archive.Restore(new[] { "gfx/interface/a.dds" });

        Assert.Empty(result.Failed);
        Assert.Equal(new GfxArchiveMove("gfx/interface/a.dds", "gfx/interface/a.dds"), Assert.Single(result.Moved));
        Assert.Equal("A", File.ReadAllText(P(Mod, "gfx/interface/a.dds")));
        Assert.False(Directory.Exists(Archive));
    }

    [Fact]
    public void RestoreUsesOriginalNameOfSuffixedFile()
    {
        Put(Archive, "gfx/a~20261106.dds", "old");

        var result = Make().Restore(new[] { "gfx/a~20261106.dds" });

        Assert.Equal(new GfxArchiveMove("gfx/a~20261106.dds", "gfx/a.dds"), Assert.Single(result.Moved));
        Assert.Equal("old", File.ReadAllText(P(Mod, "gfx/a.dds")));
    }

    [Fact]
    public void RestoreTargetExistsKeepsArchive()
    {
        Put(Mod, "gfx/a.dds", "A");
        var archive = Make();
        archive.MoveOut(new[] { "gfx/a.dds" });
        Put(Mod, "gfx/a.dds", "occupied");

        var result = archive.Restore(new[] { "gfx/a.dds" });

        Assert.Empty(result.Moved);
        Assert.Equal(GfxArchiveFailureReason.TargetExists, Assert.Single(result.Failed).Reason);
        Assert.Equal("occupied", File.ReadAllText(P(Mod, "gfx/a.dds")));
        Assert.Equal("A", File.ReadAllText(P(Archive, "gfx/a.dds")));
    }

    [Fact]
    public void RestoreRefusesPathOutsideArchive()
    {
        Put(Mod, "gfx/a.dds", "A");

        var result = Make().Restore(new[] { "../MyMod/gfx/a.dds" });

        Assert.Equal(GfxArchiveFailureReason.OutsideFolder, Assert.Single(result.Failed).Reason);
        Assert.Equal("A", File.ReadAllText(P(Mod, "gfx/a.dds")));
    }

    [Fact]
    public void OriginalNameStripsOnlyDateSuffix()
    {
        Assert.Equal("a.dds", GfxArchive.OriginalName("a~20261106.dds"));
        Assert.Equal("a.dds", GfxArchive.OriginalName("a~20261106-3.dds"));
        Assert.Equal("gfx/a.dds", GfxArchive.OriginalName("gfx/a~20261106.dds"));
        Assert.Equal("a~b.dds", GfxArchive.OriginalName("a~b.dds"));
        Assert.Equal("a~2026.dds", GfxArchive.OriginalName("a~2026.dds"));
    }

    [Fact]
    public void IsLocalModRules()
    {
        var root = Path.Combine(_temp, "mod");

        Assert.True(GfxArchive.IsLocalMod(Path.Combine(root, "MyMod"), root));
        Assert.True(GfxArchive.IsLocalMod(Path.Combine(root, "MyMod") + Path.DirectorySeparatorChar, root + Path.DirectorySeparatorChar));
        Assert.False(GfxArchive.IsLocalMod(root, root));
        Assert.False(GfxArchive.IsLocalMod(Path.Combine(_temp, "mod2", "x"), root));
        Assert.False(GfxArchive.IsLocalMod(Path.Combine(root, "MyMod"), null));
        Assert.False(GfxArchive.IsLocalMod(Path.Combine(root, "MyMod"), "  "));
        Assert.False(GfxArchive.IsLocalMod(Path.Combine(_temp, "steam", "workshop", "content", "1", "2"), root));
    }

    [Fact]
    public void ArchiveSuffixIsTheSameInLaunchControlAndVanillaDiff()
    {
        Assert.Equal(GfxArchive.ArchiveSuffix, ParadoxModTool.LaunchControl.PlaysetInstalledMods.GfxArchiveSuffix);
        Assert.Equal(GfxArchive.ArchiveSuffix, ParadoxModTool.VanillaDiff.VanillaSnapshotRecorder.GfxArchiveSuffix);
    }

    [Fact]
    public void IsArchiveFolderMatchesSuffixOnly()
    {
        Assert.True(GfxArchive.IsArchiveFolder("MyMod_gfx_archiv"));
        Assert.True(GfxArchive.IsArchiveFolder("MYMOD_GFX_ARCHIV"));
        Assert.False(GfxArchive.IsArchiveFolder("MyMod_gfx_archiv_old"));
        Assert.False(GfxArchive.IsArchiveFolder("MyMod"));
    }
}
