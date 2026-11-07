using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class PerformanceRunStoreTests
{
    private static PerformanceRun Run(string id, DateTime createdUtc, string modKey = "mod-a", string name = "Run") =>
        new(id, name, "eu5", modKey, @"C:\Mods\A", "Playset 1", "autosave", 12, null, null, "-debug_mode", createdUtc, "note");

    private static string DumpSource() => RepoFiles.Data("eu5/iv2_before.csv");

    [Fact]
    public void ImportWritesRunJsonAndDumpCopy()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));
        var run = new PerformanceRun("20261020-101500-ab12", "Before", "eu5", "mod-a", @"C:\Mods\A", "Playset 1", "save_1", 12, 11, "abc123",
            "-debug_mode -run", new DateTime(2026, 10, 20, 10, 15, 0, DateTimeKind.Utc), "first run");

        var imported = store.Import(run, DumpSource());

        Assert.Equal(run, imported);
        Assert.Equal(File.ReadAllBytes(DumpSource()), File.ReadAllBytes(store.DumpPathOf(run)));
        Assert.True(File.Exists(DumpSource()));
        Assert.Equal(Path.Combine(store.FolderOf("mod-a", run.Id), "dump.csv"), store.DumpPathOf(run));
        Assert.Equal(run, Assert.Single(store.List("mod-a")));
        Assert.Contains("\"formatVersion\": 1", File.ReadAllText(Path.Combine(store.FolderOf("mod-a", run.Id), "run.json")));
        Assert.Empty(Directory.GetFiles(store.FolderOf("mod-a", run.Id), "*.tmp"));
    }

    [Fact]
    public void ImportRoundTripsNullableFields()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));
        var run = new PerformanceRun("r1", "N", "eu5", "mod-a", "root", null, null, null, null, null, "", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), "");

        store.Import(run, DumpSource());

        Assert.Equal(run, Assert.Single(store.List("mod-a")));
    }

    [Fact]
    public void ListIsNewestFirstAndSkipsBrokenJson()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));
        store.Import(Run("old", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)), DumpSource());
        store.Import(Run("new", new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)), DumpSource());
        store.Import(Run("mid", new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)), DumpSource());
        store.Import(Run("broken", new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)), DumpSource());
        File.WriteAllText(Path.Combine(store.FolderOf("mod-a", "broken"), "run.json"), "{ not json");

        var runs = store.List("mod-a");

        Assert.Equal(new[] { "new", "mid", "old" }, runs.Select(r => r.Id));
        var warning = Assert.Single(store.Warnings);
        Assert.StartsWith("broken: ", warning);
    }

    [Fact]
    public void ListOfUnknownModIsEmpty()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));

        Assert.Empty(store.List("mod-none"));
        Assert.Empty(store.Warnings);
    }

    [Fact]
    public void ListOnlyReturnsRunsOfTheGivenMod()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));
        store.Import(Run("a1", DateTime.UtcNow, "mod-a"), DumpSource());
        store.Import(Run("b1", DateTime.UtcNow, "mod-b"), DumpSource());

        Assert.Equal("a1", Assert.Single(store.List("mod-a")).Id);
    }

    [Fact]
    public void RenameKeepsIdAndDump()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));
        var run = store.Import(Run("r1", DateTime.UtcNow, name: "Old"), DumpSource());

        var renamed = store.Rename("mod-a", "r1", "New name");

        Assert.Equal("New name", renamed.Name);
        Assert.Equal("r1", renamed.Id);
        Assert.Equal("New name", Assert.Single(store.List("mod-a")).Name);
        Assert.Equal(File.ReadAllBytes(DumpSource()), File.ReadAllBytes(store.DumpPathOf(run)));
    }

    [Fact]
    public void SaveRewritesRunJson()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));
        var run = store.Import(Run("r1", DateTime.UtcNow), DumpSource());

        var saved = store.Save(run with { Note = "changed", CountedMonths = 9, ModFilesHash = "h" });

        Assert.Equal("changed", saved.Note);
        var listed = Assert.Single(store.List("mod-a"));
        Assert.Equal("changed", listed.Note);
        Assert.Equal(9, listed.CountedMonths);
        Assert.Equal("h", listed.ModFilesHash);
        Assert.Equal(File.ReadAllBytes(DumpSource()), File.ReadAllBytes(store.DumpPathOf(run)));
    }

    [Fact]
    public void SaveOfUnknownRunThrows()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));

        Assert.Throws<FileNotFoundException>(() => store.Save(Run("missing", DateTime.UtcNow)));
        Assert.False(Directory.Exists(store.FolderOf("mod-a", "missing")));
    }

    [Fact]
    public void DeleteRemovesFolder()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));
        store.Import(Run("r1", DateTime.UtcNow), DumpSource());

        store.Delete("mod-a", "r1");

        Assert.False(Directory.Exists(store.FolderOf("mod-a", "r1")));
        Assert.Empty(store.List("mod-a"));
        store.Delete("mod-a", "r1");
    }

    [Fact]
    public void ImportRetriesOnceWhenCopyFails()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));
        var calls = 0;

        store.Import(Run("r1", DateTime.UtcNow), DumpSource(), (source, target) =>
        {
            calls++;
            if (calls == 1)
            {
                return false;
            }

            File.Copy(source, target, overwrite: true);
            return true;
        });

        Assert.Equal(2, calls);
        Assert.Single(store.List("mod-a"));

        var failing = 0;
        Assert.Throws<IOException>(() => store.Import(Run("r2", DateTime.UtcNow), DumpSource(), (_, _) =>
        {
            failing++;
            return false;
        }));
        Assert.Equal(2, failing);
        Assert.False(Directory.Exists(store.FolderOf("mod-a", "r2")));
        Assert.Equal("r1", Assert.Single(store.List("mod-a")).Id);
    }

    [Fact]
    public void ImportOfMissingDumpThrowsAndLeavesNothing()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));

        Assert.ThrowsAny<IOException>(() => store.Import(Run("r1", DateTime.UtcNow), tree.PathOf("nope.csv")));
        Assert.False(Directory.Exists(store.FolderOf("mod-a", "r1")));
    }

    [Fact]
    public void FolderOfRejectsPathTraversal()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));

        Assert.Throws<ArgumentException>(() => store.FolderOf("mod-a", ".."));
        Assert.Throws<ArgumentException>(() => store.FolderOf("a/b", "r1"));
    }

    [Fact]
    public void ModKeyForUsesIdOrHashedPath()
    {
        var first = PerformanceRunStore.ModKeyFor(null, @"C:\Mods\Alpha");
        var again = PerformanceRunStore.ModKeyFor("", @"C:\Mods\Alpha\");
        var other = PerformanceRunStore.ModKeyFor(null, @"C:\Mods\Beta");

        Assert.Matches("^mod-[0-9a-f]{8}$", first);
        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
        Assert.Equal("my.mod", PerformanceRunStore.ModKeyFor("my.mod", @"C:\Mods\Alpha"));
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(first, PerformanceRunStore.ModKeyFor(null, @"c:\mods\alpha"));
        }
    }

    [Fact]
    public void SaveAndReadAttachmentRoundTrip()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));
        var run = Run("r1", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        store.Import(run, DumpSource());

        Assert.False(store.HasAttachment(run, PerformanceRunStore.AdviceFileName));
        store.SaveAttachment(run, PerformanceRunStore.AdviceFileName, "first");
        store.SaveAttachment(run, PerformanceRunStore.AdviceFileName, "Zweiter Rat: äöü");

        Assert.True(store.HasAttachment(run, PerformanceRunStore.AdviceFileName));
        Assert.Equal("Zweiter Rat: äöü", store.ReadAttachment(run, PerformanceRunStore.AdviceFileName));
        Assert.Empty(Directory.GetFiles(store.FolderOf("mod-a", "r1"), "*.tmp"));
        Assert.Equal(run, Assert.Single(store.List("mod-a")));
    }

    [Fact]
    public void ReadAttachmentIsNullWhenMissing()
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));
        var run = Run("r1", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        store.Import(run, DumpSource());

        Assert.Null(store.ReadAttachment(run, PerformanceRunStore.AdviceFileName));
    }

    [Theory]
    [InlineData("../x.md")]
    [InlineData("sub/x.md")]
    [InlineData("sub\\x.md")]
    [InlineData("")]
    public void SaveAttachmentRejectsPathSeparators(string fileName)
    {
        using var tree = new TempTree();
        var store = new PerformanceRunStore(tree.PathOf("performance"));
        var run = Run("r1", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        store.Import(run, DumpSource());

        Assert.Throws<ArgumentException>(() => store.SaveAttachment(run, fileName, "x"));
    }
}
