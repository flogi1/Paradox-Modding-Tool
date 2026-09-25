using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class CompatchTextsTests
{
    [Fact]
    public void Every_compatch_text_key_exists_in_german_and_english_with_matching_german_text()
    {
        var root = Path.GetDirectoryName(Path.GetDirectoryName(CompatchTestBase.RepoFile("locales/de.json")))!;
        var code = File.ReadAllText(Path.Combine(root, "src", "ParadoxModTool.Compatch", "CompatchTexts.cs"));
        var german = UiTextTable.Read(Path.Combine(root, "locales", "de.json"));
        var english = UiTextTable.Read(Path.Combine(root, "locales", "en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>compatch\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count > 20, $"nur {matches.Count} Texte gefunden");
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;
            var text = Regex.Unescape(match.Groups["text"].Value);
            Assert.True(german.TryGetValue(key, out var de), $"{key} fehlt in locales/de.json");
            Assert.Equal(text, de);
            Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} fehlt in locales/en.json");
            Assert.False(en!.EndsWith("…") || en.EndsWith("...") || de!.EndsWith("…"), $"{key} endet mit Auslassungspunkten");
        }
    }

    [Fact]
    public void Texts_fall_back_to_german_without_loaded_table()
    {
        Assert.Equal("Konflikt", CompatchTexts.Stage(MergeStage.Conflict));
        Assert.Equal("Kompatch für A + B", CompatchTexts.RoleText("A + B"));
    }
}
