using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class ModFilesHashTests
{
    private static readonly DateTime Stamp = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Func<string, IEnumerable<(string Relative, long Length, DateTime LastWriteUtc)>> Files(params (string, long)[] files) =>
        _ => files.Select(f => (f.Item1, f.Item2, Stamp));

    [Fact]
    public void ComputeIsStableAndChangesWithContent()
    {
        using var mod = new TempTree();

        var first = ModFilesHash.Compute(mod.Root, Files(("common/a.txt", 10), ("events/b.txt", 20)));
        var second = ModFilesHash.Compute(mod.Root, Files(("events/b.txt", 20), ("common/a.txt", 10)));
        var changed = ModFilesHash.Compute(mod.Root, Files(("common/a.txt", 11), ("events/b.txt", 20)));

        Assert.Equal(16, first.Length);
        Assert.Equal(first, second);
        Assert.NotEqual(first, changed);
        Assert.Equal("", ModFilesHash.Compute(Path.Combine(mod.Root, "missing"), Files(("a", 1))));
    }

    [Fact]
    public void DefaultEnumerationSeesFileChanges()
    {
        using var mod = new TempTree(("common/a.txt", "abc"));

        var before = ModFilesHash.Compute(mod.Root);
        Assert.Equal(before, ModFilesHash.Compute(mod.Root));

        File.WriteAllText(mod.PathOf("common/a.txt"), "abcdef");
        Assert.NotEqual(before, ModFilesHash.Compute(mod.Root));
    }
}
