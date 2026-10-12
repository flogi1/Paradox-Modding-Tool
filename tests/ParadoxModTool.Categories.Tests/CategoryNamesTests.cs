using Xunit;
using ParadoxModTool.Localization;

namespace ParadoxModTool.Categories.Tests;

/// <summary>Die eingebauten Dateikategorien zeigen ihren Namen in der Oberflächensprache, solange er nicht umbenannt wurde; Umbenanntes bleibt.</summary>
public sealed class CategoryNamesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pmt-catnames-" + Guid.NewGuid().ToString("N"));

    public CategoryNamesTests()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "de.json"), "{\"category.builtin.text\":\"Textdateien\",\"category.builtin.other\":\"Sonstige\"}");
        File.WriteAllText(Path.Combine(_folder, "en.json"), "{\"category.builtin.text\":\"Text files\",\"category.builtin.other\":\"Other\"}");
    }

    public void Dispose()
    {
        UiText.Load(_folder, "de");
        Directory.Delete(_folder, recursive: true);
    }

    private static CategoryDefinition Category(string id) => CategoryDefaults.Create().Single(c => c.Id == id);

    [Fact]
    public void BuiltInNameFollowsTheInterfaceLanguage()
    {
        UiText.Load(_folder, "en");
        Assert.Equal("Text files", CategoryDefaults.DisplayName(Category(CategoryDefaults.TextId)));
        Assert.Equal("Other", CategoryDefaults.DisplayName(Category(CategoryDefaults.OtherId)));

        UiText.Load(_folder, "de");
        Assert.Equal("Textdateien", CategoryDefaults.DisplayName(Category(CategoryDefaults.TextId)));
    }

    [Fact]
    public void RenamedBuiltInKeepsItsName()
    {
        UiText.Load(_folder, "en");
        var renamed = Category(CategoryDefaults.TextId);
        renamed.Name = "Meine Skripte";

        Assert.Equal("Meine Skripte", CategoryDefaults.DisplayName(renamed));
    }

    [Fact]
    public void CustomCategoryKeepsItsName()
    {
        UiText.Load(_folder, "en");

        Assert.Equal("Events", CategoryDefaults.DisplayName(new CategoryDefinition { Id = "custom1", Name = "Events" }));
    }

    [Fact]
    public void BuiltInWithoutTextInTheLanguageFilesFallsBackToTheGermanName()
    {
        UiText.Load(_folder, "en");

        Assert.Equal("GUI-Dateien", CategoryDefaults.DisplayName(Category(CategoryDefaults.GuiId)));
    }

    [Fact]
    public void TypingTheShownNameStoresTheGermanDefaultAgain()
    {
        UiText.Load(_folder, "en");

        Assert.Equal("Textdateien", CategoryDefaults.NameToStore(CategoryDefaults.TextId, "Text files"));
        Assert.Equal("Meine Skripte", CategoryDefaults.NameToStore(CategoryDefaults.TextId, "Meine Skripte"));
        Assert.Equal("Text files", CategoryDefaults.NameToStore("custom1", "Text files"));
    }
}
