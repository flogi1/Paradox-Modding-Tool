using System.IO;
using ParadoxModTool.Categories;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public sealed class TabSortingTests
{
    private static OpenDocument Doc(string? path) =>
        path is null ? OpenDocument.NewEmpty(null) : OpenDocument.LoadFromFile(TempFiles.Write(path), null);

    [Fact]
    public void ManualKeepsTheOrder() => Sta.Run(() =>
    {
        var docs = new[] { Doc("b.txt"), Doc("a.txt") };
        Assert.Equal(docs, TabSorting.Sorted(docs, TabSorting.Manual));
        Assert.Equal(2, TabSorting.InsertIndex(docs, Doc("0.txt"), TabSorting.Manual));
    });

    [Fact]
    public void NameSortsCaseInsensitiveAndStable() => Sta.Run(() =>
    {
        var first = Doc("x/Same.txt");
        var second = Doc("y/same.txt");
        var docs = new[] { Doc("b.txt"), second, Doc(null), first, Doc("A.txt") };

        var sorted = TabSorting.Sorted(docs, TabSorting.Name);

        Assert.Equal(new[] { "A.txt", "b.txt", "same.txt", "Same.txt", EditorTexts.Untitled }, sorted.Select(d => d.DisplayName));
        Assert.Same(second, sorted[2]);
        Assert.Equal(1, TabSorting.InsertIndex(sorted, Doc("ab.txt"), TabSorting.Name));
        Assert.Equal(4, TabSorting.InsertIndex(sorted, Doc("z/same.txt"), TabSorting.Name));
    });

    [Fact]
    public void OriginRanksVanillaBeforeModBeforeExternal() => Sta.Run(() =>
    {
        // FileOriginTracker (TabIconSettings.cs) ist prozessweit: Wurzeln setzen, nach dem Test leeren.
        FileOriginTracker.SetRoots(new[] { Path.Combine(TempFiles.Root, "game") }, new[] { Path.Combine(TempFiles.Root, "mod") });
        try
        {
            var external = Doc("a_external.txt");
            var mod = Doc("mod/b_mod.txt");
            var vanilla = Doc("game/c_vanilla.txt");
            Assert.Equal(FileOrigin.Vanilla, vanilla.Origin);
            var docs = new[] { external, mod, vanilla };

            var sorted = TabSorting.Sorted(docs, TabSorting.Origin);

            Assert.Equal(new[] { vanilla, mod, external }, sorted);
            Assert.Equal(1, TabSorting.InsertIndex(sorted, Doc("mod/a_mod.txt"), TabSorting.Origin));
        }
        finally
        {
            FileOriginTracker.SetRoots(Array.Empty<string>(), Array.Empty<string>());
        }
    });

    [Fact]
    public void CategorySortsByCategoryNameThenFileName() => Sta.Run(() =>
    {
        var yml = Doc("b.yml");
        var txtB = Doc("b.txt");
        var txtA = Doc("a.txt");

        var sorted = TabSorting.Sorted(new[] { yml, txtB, txtA }, TabSorting.Category);

        Assert.Equal(new[] { txtA, txtB }, sorted.Where(d => d.Category == txtA.Category));
        Assert.Equal(
            sorted.Select(d => d.CategoryDisplayName).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase),
            sorted.Select(d => d.CategoryDisplayName));
    });

    [Theory]
    [InlineData(null, "Manual")]
    [InlineData("", "Manual")]
    [InlineData("name", "Manual")]
    [InlineData("Category", "Category")]
    public void NormalizeAcceptsOnlyKnownValues(string? stored, string expected) => Assert.Equal(expected, TabSorting.Normalize(stored));
}
