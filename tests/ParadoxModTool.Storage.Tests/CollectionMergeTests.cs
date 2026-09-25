using ParadoxModTool.Storage;
using Xunit;

namespace ParadoxModTool.Storage.Tests;

public sealed class CollectionMergeTests
{
    private static Dictionary<string, string> D(params (string, string)[] items) => items.ToDictionary(i => i.Item1, i => i.Item2);

    [Fact]
    public void Dictionary_KeepsEntriesAnotherProcessAdded()
    {
        var baseline = D(("a", "1"));
        var current = D(("a", "1"), ("mine", "x"));
        var disk = D(("a", "1"), ("theirs", "y"));

        var merged = CollectionMerge.MergeDictionary(disk, baseline, current);

        Assert.Equal(D(("a", "1"), ("mine", "x"), ("theirs", "y")), merged);
    }

    [Fact]
    public void Dictionary_OwnRemovalRemovesEntryOnDisk_OtherEntriesStay()
    {
        var baseline = D(("a", "1"), ("b", "2"));
        var current = D(("b", "2"));
        var disk = D(("a", "1"), ("b", "2"), ("theirs", "y"));

        var merged = CollectionMerge.MergeDictionary(disk, baseline, current);

        Assert.Equal(D(("b", "2"), ("theirs", "y")), merged);
    }

    [Fact]
    public void Dictionary_OwnChangedValueWins_UntouchedValueKeepsDiskVersion()
    {
        var baseline = D(("a", "1"), ("b", "2"));
        var current = D(("a", "changed"), ("b", "2"));
        var disk = D(("a", "1"), ("b", "theirs"));

        var merged = CollectionMerge.MergeDictionary(disk, baseline, current);

        Assert.Equal("changed", merged["a"]);
        Assert.Equal("theirs", merged["b"]);
    }

    [Fact]
    public void Dictionary_UsesGivenKeyComparer()
    {
        var baseline = new Dictionary<string, string>();
        var current = D(("Path", "1"));
        var disk = D(("PATH", "2"));

        var merged = CollectionMerge.MergeDictionary(disk, baseline, current, StringComparer.OrdinalIgnoreCase);

        Assert.Single(merged);
        Assert.Equal("1", merged["path"]);
    }

    [Fact]
    public void List_AppendsOwnNewItemsAndKeepsOthers()
    {
        var merged = CollectionMerge.MergeList(
            new[] { "a", "theirs" },
            new[] { "a" },
            new[] { "a", "mine" });

        Assert.Equal(new[] { "a", "theirs", "mine" }, merged);
    }

    [Fact]
    public void List_OwnRemovalRemovesItem()
    {
        var merged = CollectionMerge.MergeList(
            new[] { "a", "b", "theirs" },
            new[] { "a", "b" },
            new[] { "b" });

        Assert.Equal(new[] { "b", "theirs" }, merged);
    }

    [Fact]
    public void List_ItemBothAddedIsNotDuplicated_WithComparer()
    {
        var merged = CollectionMerge.MergeList(
            new[] { "X" },
            Array.Empty<string>(),
            new[] { "x" },
            StringComparer.OrdinalIgnoreCase);

        Assert.Equal(new[] { "X" }, merged);
    }

    [Fact]
    public void List_NothingChangedByThisProcess_ReturnsDiskAsIs()
    {
        var merged = CollectionMerge.MergeList(
            new[] { "a", "theirs" },
            new[] { "a" },
            new[] { "a" });

        Assert.Equal(new[] { "a", "theirs" }, merged);
    }
}
