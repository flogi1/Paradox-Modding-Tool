using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

/// <summary>Der Fehlerkatalog von Europa Universalis IV (Zeilen ohne Uhrzeit) gegen echte Zeilen aus Data/eu4.</summary>
public class Eu4CatalogTests
{
    private static string Profiles => Path.Combine(AppContext.BaseDirectory, "profiles");

    private static ErrorCatalog Catalog() => ErrorCatalog.Load(Path.Combine(Profiles, "errorlog", "eu4.json"));

    [Fact]
    public void AlreadyDefined_Matches()
    {
        var entries = ErrorClassifier.Classify(LogReader.Parse(new[]
        {
            "[persistent.cpp:88]: Parsing Error. File: \"interface/frontend.gui\", Error: Already Defined. Key: frontend_link_item: alwaystransparent, near line: 28",
        }), Catalog());

        var entry = Assert.Single(entries);
        Assert.Equal("GUI-001", entry.Code);
        Assert.Equal("interface/frontend.gui", entry.FilePath);
        Assert.Equal("frontend_link_item", entry.Name);
        Assert.Equal(28, entry.FileLine);
    }

    [Fact]
    public void Eu4Profile_HasCatalog()
    {
        var profile = GameProfileLoader.LoadFromFile(Path.Combine(Profiles, "eu4.json"));

        Assert.Equal("errorlog/eu4.json", profile.ErrorLogRules);
        var catalog = Catalog();
        Assert.Empty(catalog.Problems);
        var kind = Assert.Single(catalog.Kinds);
        Assert.True(kind.Names.ContainsKey("de") && kind.Names.ContainsKey("en"));
        Assert.True(kind.Explain.ContainsKey("de") && kind.Explain.ContainsKey("en"));
    }

    [Fact]
    public void ToolLists_Eu4AsSupported()
    {
        var profile = GameProfileLoader.LoadFromFile(Path.Combine(Profiles, "eu4.json"));

        Assert.NotNull(ErrorCatalog.PathFor(Profiles, profile.ErrorLogRules));
        Assert.True(File.Exists(ErrorCatalog.PathFor(Profiles, profile.ErrorLogRules)));
    }

    [Fact]
    public void Real_lines_other_than_Already_Defined_stay_unmatched()
    {
        var entries = ErrorClassifier.Classify(LogReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "Data", "eu4", "error.log")), Catalog());

        Assert.Equal(4, entries.Count);
        Assert.Equal(2, entries.Count(e => e.Code == "GUI-001"));
        Assert.Equal(2, entries.Count(e => e.Code == ErrorEntry.UnmatchedCode));
    }
}
