using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class ScopeFinderTextsTests
{
    [Fact]
    public void EveryKeyExistsInGermanAndEnglishWithMatchingGermanText()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "ParadoxModTool.Settings", "ScopeFinderTexts.cs"));
        var german = UiTextTable.Read(Path.Combine(root, "locales", "de.json"));
        var english = UiTextTable.Read(Path.Combine(root, "locales", "en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>scopeFinder\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count >= 45, $"nur {matches.Count} Texte gefunden");
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;
            var text = match.Groups["text"].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
            Assert.True(german.TryGetValue(key, out var de), $"{key} fehlt in locales/de.json");
            Assert.Equal(text, de);
            Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} fehlt in locales/en.json");
        }
    }

    [Fact]
    public void NoScopeFinderTextUsesAnEllipsis()
    {
        var root = FindRepositoryRoot();
        foreach (var language in new[] { "de", "en" })
        {
            var table = UiTextTable.Read(Path.Combine(root, "locales", language + ".json"));
            var bad = table.Where(pair => pair.Key.StartsWith("scopeFinder.", StringComparison.Ordinal) && (pair.Value.Contains('…') || pair.Value.Contains("...")))
                .Select(pair => $"{language}: {pair.Key}")
                .ToList();
            Assert.Empty(bad);
        }
    }

    [Fact]
    public void FormattedTextsFillTheirPlaceholders()
    {
        Assert.Equal("Hier: country", ScopeFinderTexts.StartHere("country"));
        Assert.Equal("scope:actor: country", ScopeFinderTexts.StartSaved("actor", "country"));
        Assert.Equal("12 von 40 Wegen", ScopeFinderTexts.CountFiltered(12, 40));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ParadoxModTool.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository-Wurzel nicht gefunden");
    }
}
