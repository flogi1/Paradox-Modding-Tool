using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.Modifiers.Tests;

public sealed class ModifierNameIndexTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-modifier-names-" + Guid.NewGuid().ToString("N"));

    private string Game => Path.Combine(_root, "game");

    private string Mod => Path.Combine(_root, "mod");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static ModifierFormatSettings Settings(params string[] keys) => new() { NameLocFolders = { "localization" }, NameKeys = keys.ToList() };

    private static void Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(true));
    }

    private ModifierNameIndex Build(ModifierFormatSettings? settings, string[] names, params string[] languages) =>
        ModifierNameIndex.Build(settings, names, new[] { Game }, new[] { Mod }, languages);

    [Fact]
    public void KeyPatternIgnoresCaseAndReadsSubfolders()
    {
        Write(Game, "localization/english/sub/core_l_english.yml", "l_english:\n MOD_LEVY_SIZE:0 \"[levy|E] Size\"\n");

        var index = Build(Settings("MOD_{name}"), new[] { "levy_size" }, "english");

        Assert.Equal("Levy Size", index.Find("levy_size"));
    }

    [Fact]
    public void UiLanguageComesFirstThenEnglish()
    {
        Write(Game, "localization/english/a_l_english.yml", "l_english:\n k_a:0 \"Tax\"\n k_b:0 \"Only English\"\n");
        Write(Game, "localization/german/a_l_german.yml", "l_german:\n k_a:0 \"Steuer\"\n");

        var index = Build(Settings("k_{name}"), new[] { "a", "b" }, "german", "english");

        Assert.Equal("Steuer", index.Find("a"));
        Assert.Equal("Only English", index.Find("b"));
    }

    [Fact]
    public void FirstMatchingPatternWins()
    {
        Write(Game, "localization/english/a_l_english.yml", "l_english:\n MODIFIER_x:0 \"Second\"\n x:0 \"First\"\n");

        var index = Build(Settings("{name}", "MODIFIER_{name}"), new[] { "x" }, "english");

        Assert.Equal("First", index.Find("x"));
    }

    [Fact]
    public void ModOverridesGame()
    {
        Write(Game, "localization/english/a_l_english.yml", "l_english:\n x:0 \"Vanilla\"\n");
        Write(Mod, "localization/english/b_l_english.yml", "l_english:\n x:0 \"Mod\"\n");

        Assert.Equal("Mod", Build(Settings("{name}"), new[] { "x" }, "english").Find("x"));
    }

    [Fact]
    public void FirstGameDefinitionWins()
    {
        Write(Game, "localization/english/a_l_english.yml", "l_english:\n x:0 \"One\"\n");
        Write(Game, "localization/english/b_l_english.yml", "l_english:\n x:0 \"Two\"\n");

        Assert.Equal("One", Build(Settings("{name}"), new[] { "x" }, "english").Find("x"));
    }

    [Fact]
    public void UnresolvableTextGivesNoName()
    {
        Write(Game, "localization/english/a_l_english.yml", "l_english:\n x:0 \"[GetScheme('a').GetName] Speed\"\n");

        Assert.Null(Build(Settings("{name}"), new[] { "x" }, "english").Find("x"));
    }

    [Fact]
    public void OtherLanguagesFilesAreIgnored()
    {
        Write(Game, "localization/german/a_l_german.yml", "l_german:\n x:0 \"Deutsch\"\n");

        Assert.Null(Build(Settings("{name}"), new[] { "x" }, "english").Find("x"));
    }

    [Fact]
    public void NoSettingsOrNoFoldersGiveEmptyIndex()
    {
        Write(Game, "localization/english/a_l_english.yml", "l_english:\n x:0 \"Name\"\n");

        Assert.Equal(0, Build(null, new[] { "x" }, "english").Count);
        Assert.Equal(0, Build(new ModifierFormatSettings { NameKeys = { "{name}" } }, new[] { "x" }, "english").Count);
        Assert.Equal(0, Build(Settings(), new[] { "x" }, "english").Count);
    }

    [Fact]
    public void ConceptsAndReferencesAreTranslatedLikeTheName()
    {
        Write(Game, "localization/german/a_l_german.yml", "l_german:\n MOD_LEVY_SIZE:0 \"[levy|E]-Größe\"\n MOD_TAX:0 \"[tax|E]\"\n game_concept_levy:0 \"Aufgebot\"\n game_concept_tax:0 \"Steuer\"\n");
        Write(Game, "localization/english/a_l_english.yml", "l_english:\n MOD_LEVY_SIZE:0 \"[levy|E] Size\"\n game_concept_levy:0 \"Levy\"\n");
        var settings = Settings("MOD_{name}");
        settings.NameRefKeys = new List<string> { "game_concept_{id}" };

        var index = Build(settings, new[] { "levy_size", "tax" }, "german", "english");

        Assert.Equal("Aufgebot-Größe", index.Find("levy_size"));
        Assert.Equal("Steuer", index.Find("tax"));
    }

    [Fact]
    public void ReferenceToAnotherKeyIsFollowedAcrossLevels()
    {
        Write(Game, "localization/german/a_l_german.yml",
            "l_german:\n MOD_DAMAGE_ADD: \"[men_at_arms|E]-[damage|E] ([stationed|E])\"\n MOD_DAMAGE_MULT: \"$MOD_DAMAGE_ADD$\"\n MOD_DAMAGE_PER_LEVEL: \"$MOD_DAMAGE_MULT$ pro Stufe\"\n"
            + " game_concept_men_at_arms: \"Schwertkämpfer\"\n game_concept_damage: \"Schaden\"\n game_concept_stationed: \"Stationiert\"\n");
        var settings = Settings("MOD_{name}");
        settings.NameRefKeys = new List<string> { "game_concept_{id}" };

        var index = Build(settings, new[] { "damage_mult", "damage_per_level" }, "german");

        Assert.Equal("Schwertkämpfer-Schaden (Stationiert)", index.Find("damage_mult"));
        Assert.Equal("Schwertkämpfer-Schaden (Stationiert) pro Stufe", index.Find("damage_per_level"));
    }

    [Fact]
    public void UnresolvedKeyReferenceGivesNoNameInsteadOfTheKey()
    {
        Write(Game, "localization/english/a_l_english.yml", "l_english:\n MOD_X: \"$MOD_MISSING_ADD$\"\n");

        Assert.Null(Build(Settings("MOD_{name}"), new[] { "x" }, "english").Find("x"));
    }

    [Fact]
    public void CircularKeyReferencesDoNotHang()
    {
        Write(Game, "localization/english/a_l_english.yml", "l_english:\n MOD_A: \"$MOD_B$\"\n MOD_B: \"$MOD_A$\"\n");

        Assert.Null(Build(Settings("MOD_{name}"), new[] { "a" }, "english").Find("a"));
    }

    [Fact]
    public void ReferenceWithoutLocalizationFallsBackToTheHumanizedName()
    {
        Write(Game, "localization/english/a_l_english.yml", "l_english:\n x:0 \"$nobles$ Growth\"\n");

        Assert.Equal("Nobles Growth", Build(Settings("{name}"), new[] { "x" }, "english").Find("x"));
    }

    [Fact]
    public void ConceptCallShowsItsSecondText() =>
        Assert.Equal("Aufgebots-Größe", ModifierNameText.Clean("[Concept('levy', 'Aufgebots')|E]-Größe"));

    [Fact]
    public void ConceptCallWithoutTextIsResolved() =>
        Assert.Equal("Aufgebot Size", ModifierNameText.Clean("[Concept('levy')|E] Size", id => id == "levy" ? "Aufgebot" : null));

    [Fact]
    public void CleanStripsCodesAndExpandsConcepts()
    {
        Assert.Equal("Levy Size", ModifierNameText.Clean("[levy|E] Size"));
        Assert.Equal("Nobles Growth", ModifierNameText.Clean("$nobles$ Growth"));
        Assert.Equal("National Tax", ModifierNameText.Clean("§YNational§! Tax"));
        Assert.Equal("Bold Name", ModifierNameText.Clean("#bold Bold#! Name"));
        Assert.Equal("Auswirkung der Ruhmstufe", ModifierNameText.Clean("@prestige_icon!Auswirkung der Ruhmstufe"));
        Assert.Equal("Eins Zwei", ModifierNameText.Clean("Eins\\nZwei"));
        Assert.Null(ModifierNameText.Clean("[ShowEstateTypeName('nobles_estate')] Unrest"));
        Assert.Null(ModifierNameText.Clean("   "));
    }

    [Theory]
    [InlineData("de", "german")]
    [InlineData("pt-br", "braz_por")]
    [InlineData("zh-hans", "simp_chinese")]
    [InlineData("DE", "german")]
    public void GameLanguageFollowsTheUiLanguageThenEnglish(string ui, string game) =>
        Assert.Equal(new[] { game, "english" }, ModifierNameIndex.GameLanguages(ui));

    [Theory]
    [InlineData("en")]
    [InlineData("xx")]
    [InlineData(null)]
    public void EnglishAndUnknownLanguagesOnlyGetEnglish(string? ui) => Assert.Equal(new[] { "english" }, ModifierNameIndex.GameLanguages(ui));
}
