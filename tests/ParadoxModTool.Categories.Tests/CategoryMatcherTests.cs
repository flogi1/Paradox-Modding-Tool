using ParadoxModTool.Categories;
using Xunit;

namespace ParadoxModTool.Categories.Tests;

public class CategoryMatcherTests
{
    private static List<CategoryDefinition> Defaults() => CategoryDefaults.Create();

    [Theory]
    [InlineData(@"C:\mod\common\a.txt", "Text")]
    [InlineData(@"C:\mod\gui\a.GUI", "Gui")]
    [InlineData(@"C:\mod\a.gfx", "Gui")]
    [InlineData(@"C:\mod\loc\a_l_english.yml", "Localization")]
    [InlineData(@"C:\mod\defines\a.lua", "Defines")]
    [InlineData(@"C:\mod\readme.md", "Other")]
    [InlineData(@"C:\mod\noextension", "Other")]
    public void DefaultsMatchByExtension(string path, string expected)
    {
        Assert.Equal(expected, CategoryMatcher.Match(Defaults(), path));
    }

    [Fact]
    public void NullOrEmptyPathIsOther()
    {
        Assert.Equal("Other", CategoryMatcher.Match(Defaults(), null));
        Assert.Equal("Other", CategoryMatcher.Match(Defaults(), ""));
    }

    [Fact]
    public void ImportantAndHelperFileNeverMatchAutomatically()
    {
        foreach (var path in new[] { @"C:\a\x.txt", @"C:\a\x.md", @"C:\a\x.yml" })
        {
            Assert.DoesNotContain(CategoryMatcher.Match(Defaults(), path), new[] { "Important", "HelperFile" });
        }
    }

    [Fact]
    public void FirstCategoryInListOrderWins()
    {
        var list = Defaults();
        var events = new CategoryDefinition { Id = "c-events", Name = "Events", ColorHex = "#FF0000", Patterns = { "events/*" } };
        list.Insert(0, events);

        Assert.Equal("c-events", CategoryMatcher.Match(list, @"C:\mod\events\war.txt"));
        Assert.Equal("Text", CategoryMatcher.Match(list, @"C:\mod\common\war.txt"));

        list.Remove(events);
        list.Insert(list.Count - 1, events);
        Assert.Equal("Text", CategoryMatcher.Match(list, @"C:\mod\events\war.txt"));
    }

    [Theory]
    [InlineData("events/*", "C:/mod/events/a.txt", true)]
    [InlineData("events/*", "C:/mod/events/sub/a.txt", true)]
    [InlineData("events/*", "C:/mod/prevents/a.txt", false)]
    [InlineData("*_decisions.txt", "C:/mod/common/my_decisions.txt", true)]
    [InlineData("*_decisions.txt", "C:/mod/common/my_decisions.yml", false)]
    [InlineData("EVENTS/*", "C:/mod/events/a.txt", true)]
    [InlineData("a?.txt", "C:/mod/ab.txt", true)]
    [InlineData("a?.txt", "C:/mod/abc.txt", false)]
    [InlineData(@"common\ideas\*", "C:/mod/common/ideas/x.txt", true)]
    [InlineData("a.txt", "C:/mod/a.txt", true)]
    [InlineData("a.txt", "C:/mod/ba.txt", false)]
    [InlineData("a.txt", "C:/mod/aXtxt", false)]
    [InlineData("", "C:/mod/a.txt", false)]
    public void PatternMatching(string pattern, string path, bool expected)
    {
        Assert.Equal(expected, CategoryMatcher.PatternMatches(pattern, path));
    }

    [Fact]
    public void ExtensionsAreCaseInsensitiveAndFallbackRulesIgnored()
    {
        var list = Defaults();
        list.First(c => c.Id == "Other").Extensions.Add(".md");
        Assert.Equal("Other", CategoryMatcher.Match(list, @"C:\a\x.md"));

        list.First(c => c.Id == "Text").Extensions.Add(".md");
        Assert.Equal("Text", CategoryMatcher.Match(list, @"C:\a\X.MD"));
    }

    [Fact]
    public void ParseExtensionsNormalizes()
    {
        Assert.Equal(new[] { ".txt", ".gui", ".yml" }, CategoryMatcher.ParseExtensions("txt, .GUI;  yml\n.txt ."));
        Assert.Empty(CategoryMatcher.ParseExtensions("  "));
    }

    [Fact]
    public void ParsePatternsOnePerLine()
    {
        Assert.Equal(new[] { "events/*", "*_x.txt" }, CategoryMatcher.ParsePatterns("events/*\r\n\r\n *_x.txt \nEVENTS/*"));
    }

    [Fact]
    public void NormalizeNullGivesDefaults()
    {
        var result = CategoryMatcher.Normalize(null);
        Assert.Equal(CategoryDefaults.BuiltInIds, result.Select(c => c.Id).ToArray());
    }

    [Fact]
    public void NormalizeCleansEntriesAndKeepsOtherLast()
    {
        var result = CategoryMatcher.Normalize(new[]
        {
            new CategoryDefinition { Id = "Other", Name = "Rest", ColorHex = "#111111" },
            new CategoryDefinition { Id = "c-1", Name = "  ", ColorHex = "rot", Extensions = { "TXT", "txt", "." } },
            new CategoryDefinition { Id = "c-1", Name = "Doppelt" },
            new CategoryDefinition { Id = "", Name = "OhneId" },
            new CategoryDefinition { Id = "c-2", Name = "Zwei", ColorHex = "#ABCDEF", Patterns = { " a/* ", "A/*" } },
        });

        Assert.Equal(new[] { "c-1", "c-2", "Other" }, result.Select(c => c.Id).ToArray());
        Assert.Equal("c-1", result[0].Name);
        Assert.Equal("#808080", result[0].ColorHex);
        Assert.Equal(new[] { ".txt" }, result[0].Extensions);
        Assert.Equal(new[] { "a/*" }, result[1].Patterns);
        Assert.Equal("Rest", result[2].Name);
    }

    [Fact]
    public void NormalizeAddsMissingOther()
    {
        var result = CategoryMatcher.Normalize(new[] { new CategoryDefinition { Id = "c-1", Name = "Eins" } });
        Assert.Equal("Other", result[^1].Id);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void BuiltInIdsAndCustomIds()
    {
        Assert.True(CategoryDefaults.IsBuiltIn("Gui"));
        Assert.False(CategoryDefaults.IsBuiltIn("c-abc"));
        var a = CategoryMatcher.NewCustomId();
        Assert.StartsWith("c-", a);
        Assert.NotEqual(a, CategoryMatcher.NewCustomId());
    }

    [Theory]
    [InlineData("#1E90FF", true)]
    [InlineData("1E90FF", false)]
    [InlineData("#12345", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData(null, false)]
    public void HexValidation(string? hex, bool expected)
    {
        Assert.Equal(expected, CategoryMatcher.IsValidHex(hex));
    }
}
