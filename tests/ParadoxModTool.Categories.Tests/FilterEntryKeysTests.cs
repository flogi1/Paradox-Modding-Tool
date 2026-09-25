using ParadoxModTool.Categories;
using Xunit;

namespace ParadoxModTool.Categories.Tests;

public class FilterEntryKeysTests
{
    [Fact]
    public void KeysAreDistinctPerKind()
    {
        Assert.Equal("origin:Vanilla", FilterEntryKeys.ForOrigin(FileOrigin.Vanilla));
        Assert.Equal("category:Text", FilterEntryKeys.ForCategory("Text"));
        Assert.NotEqual(FilterEntryKeys.ForCategory("Vanilla"), FilterEntryKeys.ForOrigin(FileOrigin.Vanilla));
    }

    [Fact]
    public void PruneKeepsValidKeys()
    {
        var result = FilterEntryKeys.Prune(new[] { "origin:External", "category:Text", "category:c-1" }, new[] { "Text", "c-1", "Other" });
        Assert.Equal(new[] { "origin:External", "category:Text", "category:c-1" }, result);
    }

    [Fact]
    public void PruneDropsDeletedCategoriesUnknownOriginsDuplicatesAndGarbage()
    {
        var result = FilterEntryKeys.Prune(
            new[] { "category:gone", "origin:Nowhere", "origin:7", "origin:Mod", "origin:Mod", "", "  ", "text", "category:Text" },
            new[] { "Text" });
        Assert.Equal(new[] { "origin:Mod", "category:Text" }, result);
    }

    [Fact]
    public void PruneNullIsEmpty()
    {
        Assert.Empty(FilterEntryKeys.Prune(null, new[] { "Text" }));
    }
}
