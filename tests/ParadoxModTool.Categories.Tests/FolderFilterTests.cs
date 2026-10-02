using ParadoxModTool.Categories;
using Xunit;

namespace ParadoxModTool.Categories.Tests;

/// <summary>Ordner-Filter: nur Dateien aus einem gewählten Ordnerbaum (z. B. "Idea Variation Hauptmod").</summary>
public class FolderFilterTests
{
    private const string Mods = @"C:\Users\x\Documents\Paradox Interactive\Europa Universalis IV\mod";

    private static FolderFilterDefinition Filter(string id, string folder) => new() { Id = id, Name = id, FolderPath = folder };

    [Theory]
    [InlineData(Mods + @"\Idea Variation\common\ideas\00_ideas.txt", true)]
    [InlineData(Mods + @"\Idea Variation\descriptor.mod", true)]
    [InlineData(Mods + @"\Idea Variation BT Addon\common\ideas\00_ideas.txt", false)]
    [InlineData(Mods + @"\Idea Variation Anbennar Addon\events\a.txt", false)]
    [InlineData(Mods + @"\Idea Variation", false)]
    [InlineData("c:/users/x/documents/paradox interactive/europa universalis iv/mod/idea variation/events/a.txt", true)]
    public void Contains_only_files_below_the_folder(string file, bool expected)
    {
        Assert.Equal(expected, FolderFilterMatcher.Contains(Mods + @"\Idea Variation", file));
    }

    [Fact]
    public void A_trailing_separator_on_the_folder_does_not_matter()
    {
        Assert.True(FolderFilterMatcher.Contains(Mods + @"\Idea Variation\", Mods + @"\Idea Variation\a.txt"));
        Assert.False(FolderFilterMatcher.Contains(Mods + @"\Idea Variation\", Mods + @"\Idea Variation BT Addon\a.txt"));
    }

    [Fact]
    public void No_active_filter_accepts_everything()
    {
        Assert.True(FolderFilterMatcher.Accepts(Array.Empty<FolderFilterDefinition>(), @"C:\anywhere\a.txt"));
        Assert.True(FolderFilterMatcher.Accepts(Array.Empty<FolderFilterDefinition>(), null));
    }

    [Fact]
    public void Several_active_filters_accept_the_union()
    {
        var active = new[] { Filter("main", Mods + @"\Idea Variation"), Filter("bt", Mods + @"\Idea Variation BT Addon") };

        Assert.True(FolderFilterMatcher.Accepts(active, Mods + @"\Idea Variation\a.txt"));
        Assert.True(FolderFilterMatcher.Accepts(active, Mods + @"\Idea Variation BT Addon\a.txt"));
        Assert.False(FolderFilterMatcher.Accepts(active, Mods + @"\Idea Variation Anbennar Addon\a.txt"));
        Assert.False(FolderFilterMatcher.Accepts(active, null));
    }

    [Fact]
    public void Prune_drops_empty_and_duplicate_entries()
    {
        var pruned = FolderFilterMatcher.Prune(new[]
        {
            Filter("a", Mods + @"\Idea Variation"),
            Filter("a", Mods + @"\Other"),
            new FolderFilterDefinition { Id = "b", Name = " ", FolderPath = Mods },
            new FolderFilterDefinition { Id = "c", Name = "C", FolderPath = "" },
            null!,
        });

        Assert.Equal(new[] { "a" }, pruned.Select(f => f.Id));
    }

    [Fact]
    public void Suggested_name_is_the_folder_name()
    {
        Assert.Equal("Idea Variation", FolderFilterMatcher.SuggestName(Mods + @"\Idea Variation\"));
    }
}
