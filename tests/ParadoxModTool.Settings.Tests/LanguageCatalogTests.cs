using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class LanguageCatalogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-lang-" + Guid.NewGuid().ToString("N"));

    public LanguageCatalogTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void FindsJsonFilesWithGermanFirst()
    {
        File.WriteAllText(Path.Combine(_dir, "en.json"), "{}");
        File.WriteAllText(Path.Combine(_dir, "de.json"), "{}");
        File.WriteAllText(Path.Combine(_dir, "_notes.json"), "{}");
        File.WriteAllText(Path.Combine(_dir, "readme.txt"), "x");

        var languages = LanguageCatalog.Find(_dir);

        Assert.Equal(new[] { new UiLanguageOption("de", "Deutsch"), new UiLanguageOption("en", "English") }, languages);
    }

    [Fact]
    public void MissingFolderStillOffersGerman()
    {
        var languages = LanguageCatalog.Find(Path.Combine(_dir, "fehlt"));

        Assert.Equal(new[] { new UiLanguageOption("de", "Deutsch") }, languages);
    }

    [Fact]
    public void UnknownLanguageUsesItsNativeName()
    {
        Assert.Equal("Français", LanguageCatalog.DisplayName("fr"));
        Assert.Equal("xx-nope", LanguageCatalog.DisplayName("xx-nope"));
    }
}
