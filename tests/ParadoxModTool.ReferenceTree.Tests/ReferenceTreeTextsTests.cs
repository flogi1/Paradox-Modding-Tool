using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.ReferenceTree.Tests;

public sealed class ReferenceTreeTextsTests
{
    [Fact]
    public void EveryKeyExistsInGermanAndEnglishWithMatchingGermanTextAndPlaceholders()
    {
        var code = SourceCode();
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));
        var english = UiTextTable.Read(RepoFiles.PathOf("locales/en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>referencetree\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count >= 30, $"nur {matches.Count} Texte gefunden");
        Assert.Equal(matches.Count, matches.Select(m => m.Groups["key"].Value).Distinct().Count());
        foreach (Match match in matches)
        {
            AssertLocalized(match.Groups["key"].Value, Regex.Unescape(match.Groups["text"].Value), german, english);
        }
    }

    [Fact]
    public void EveryReferenceTreeKeyInTheLocaleFilesIsUsedByTheCode()
    {
        var code = SourceCode();
        var used = Regex.Matches(code, "\\b[TF]\\(\"(?<key>referencetree\\.[^\"]+)\"").Select(m => m.Groups["key"].Value).ToHashSet();
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));
        var english = UiTextTable.Read(RepoFiles.PathOf("locales/en.json"));

        foreach (var key in german.Keys.Concat(english.Keys).Where(k => k.StartsWith("referencetree.", StringComparison.Ordinal)))
        {
            Assert.Contains(key, used);
        }
    }

    private static string SourceCode() => File.ReadAllText(RepoFiles.PathOf("src/ParadoxModTool.ReferenceTree/ReferenceTreeTexts.cs"));

    private static void AssertLocalized(string key, string text, IReadOnlyDictionary<string, string> german, IReadOnlyDictionary<string, string> english)
    {
        Assert.True(german.TryGetValue(key, out var de), $"{key} fehlt in locales/de.json");
        Assert.Equal(text, de);
        Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} fehlt in locales/en.json");
        Assert.Equal(Placeholders(de!), Placeholders(en));
    }

    private static IEnumerable<string> Placeholders(string text) =>
        Regex.Matches(text, "\\{\\d+[^}]*\\}").Select(m => m.Value).Order();
}
