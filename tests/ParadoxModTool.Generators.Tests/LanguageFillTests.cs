using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Textregeln für mehrsprachige Felder (Spec Abschnitt 1) und die Schreibweise in .yml (Paradox-Standard: \" und \n).</summary>
public sealed class LanguageFillTests
{
    private static readonly Dictionary<string, string> German = new() { ["german"] = "Reich" };

    [Fact]
    public void Own_translation_wins() =>
        Assert.Equal("Reich", LanguageFill.Resolve("Empire", German, "german", new LanguageDefaults(CopyEnglish: true)));

    [Fact]
    public void English_is_returned_for_english() =>
        Assert.Equal("Empire", LanguageFill.Resolve("Empire", German, "english", new LanguageDefaults()));

    [Fact]
    public void English_for_missing_only_when_element_has_translations()
    {
        var rules = new LanguageDefaults(EnglishForMissing: true);

        Assert.Equal("Empire", LanguageFill.Resolve("Empire", German, "french", rules));
        Assert.Equal(string.Empty, LanguageFill.Resolve("Empire", null, "french", rules));
    }

    [Fact]
    public void Copy_english_fills_empty_languages() =>
        Assert.Equal("Empire", LanguageFill.Resolve("Empire", null, "french", new LanguageDefaults(CopyEnglish: true)));

    [Fact]
    public void Extend_english_also_fills_empty_languages() =>
        Assert.Equal("Empire", LanguageFill.Resolve("Empire", null, "french", new LanguageDefaults(ExtendEnglish: true)));

    [Fact]
    public void Without_rules_missing_language_is_empty() =>
        Assert.Equal(string.Empty, LanguageFill.Resolve("Empire", German, "french", new LanguageDefaults()));

    [Fact]
    public void Extend_english_writes_all_languages()
    {
        var all = new[] { "english", "german", "french" };

        Assert.Equal(all, LanguageFill.LanguagesToWrite(new[] { "english" }, all, new LanguageDefaults(ExtendEnglish: true)));
        Assert.Equal(new[] { "english" }, LanguageFill.LanguagesToWrite(new[] { "english" }, all, new LanguageDefaults()));
    }

    [Fact]
    public void Yaml_value_escapes_quotes_and_line_breaks() =>
        Assert.Equal("He said \\\"yes\\\"\\nok\\nend", LanguageFill.YamlValue("He said \"yes\"\r\nok\nend"));

    [Fact]
    public void Already_escaped_sequences_are_not_doubled() =>
        Assert.Equal("\\\"x\\\" a\\nb", LanguageFill.YamlValue("\\\"x\\\" a\\nb"));
}
