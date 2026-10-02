using ParadoxModTool.Localization;
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
    public void CombinesProgramAndUserFolders()
    {
        var program = Directory.CreateDirectory(Path.Combine(_dir, "program")).FullName;
        var user = Directory.CreateDirectory(Path.Combine(_dir, "user")).FullName;
        File.WriteAllText(Path.Combine(program, "de.json"), "{}");
        File.WriteAllText(Path.Combine(program, "en.json"), "{}");
        File.WriteAllText(Path.Combine(user, "fr.json"), "{}");
        File.WriteAllText(Path.Combine(user, "en.json"), "{}");

        var languages = LanguageCatalog.Find(new LocaleSources(program, user));

        Assert.Equal(
            new[]
            {
                new UiLanguageOption("de", "Deutsch", LocaleOrigin.Program),
                new UiLanguageOption("en", "English", LocaleOrigin.User),
                new UiLanguageOption("fr", "Français", LocaleOrigin.User),
            },
            languages);
    }

    [Fact]
    public void MixedCaseFileNameIsOneLanguage()
    {
        var program = Directory.CreateDirectory(Path.Combine(_dir, "program")).FullName;
        var user = Directory.CreateDirectory(Path.Combine(_dir, "user")).FullName;
        File.WriteAllText(Path.Combine(program, "pt-BR.json"), "{}");
        File.WriteAllText(Path.Combine(user, "pt-br.json"), "{}");

        var languages = LanguageCatalog.Find(new LocaleSources(program, user));

        var portuguese = Assert.Single(languages, language => language.Code.StartsWith("pt", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("pt-br", portuguese.Code);
        Assert.Equal(LocaleOrigin.User, portuguese.Origin);
    }

    [Fact]
    public void MissingUserFolderIsIgnored()
    {
        File.WriteAllText(Path.Combine(_dir, "en.json"), "{}");

        var languages = LanguageCatalog.Find(new LocaleSources(_dir, Path.Combine(_dir, "fehlt")));

        Assert.Equal(new[] { new UiLanguageOption("de", "Deutsch"), new UiLanguageOption("en", "English") }, languages);
    }

    [Fact]
    public void UnknownLanguageUsesItsNativeName()
    {
        Assert.Equal("Français", LanguageCatalog.DisplayName("fr"));
        Assert.Equal("xx-nope", LanguageCatalog.DisplayName("xx-nope"));
    }
}
