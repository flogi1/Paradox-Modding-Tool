using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public sealed class FlagTrackerTextsTests
{
    [Fact]
    public void EveryKeyExistsInGermanAndEnglishWithMatchingGermanTextAndPlaceholders()
    {
        var code = SourceCode();
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));
        var english = UiTextTable.Read(RepoFiles.PathOf("locales/en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>flagtracker\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count >= 40, $"nur {matches.Count} Texte gefunden");
        Assert.Equal(matches.Count, matches.Select(m => m.Groups["key"].Value).Distinct().Count());
        foreach (Match match in matches)
        {
            AssertLocalized(match.Groups["key"].Value, Regex.Unescape(match.Groups["text"].Value), german, english);
        }
    }

    [Fact]
    public void EveryFlagTrackerKeyInTheLocaleFilesIsUsedByTheCode()
    {
        var code = SourceCode();
        var used = Regex.Matches(code, "\\b[TF]\\(\"(?<key>flagtracker\\.[^\"]+)\"").Select(m => m.Groups["key"].Value).ToHashSet();
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));

        foreach (var key in german.Keys.Where(k => k.StartsWith("flagtracker.", StringComparison.Ordinal)))
        {
            Assert.Contains(key, used);
        }
    }

    [Fact]
    public void Kind_ScopeFlagShowsFamily()
    {
        Assert.Equal("Flag (character_flag)", FlagTrackerTexts.Kind(FlagKind.ScopeFlag, "character_flag"));
        Assert.Equal("Flag (?)", FlagTrackerTexts.Kind(FlagKind.ScopeFlag, null));
        Assert.Equal("Variable", FlagTrackerTexts.Kind(FlagKind.Variable, "ignored"));
    }

    [Fact]
    public void EveryEnumHasText()
    {
        Assert.All(Enum.GetValues<FlagKind>(), v => Assert.False(string.IsNullOrWhiteSpace(FlagTrackerTexts.Kind(v, "x"))));
        Assert.All(Enum.GetValues<FlagRole>(), v => Assert.False(string.IsNullOrWhiteSpace(FlagTrackerTexts.Role(v))));
        Assert.All(Enum.GetValues<FlagOrigin>(), v => Assert.False(string.IsNullOrWhiteSpace(FlagTrackerTexts.Origin(v))));
        Assert.All(Enum.GetValues<BuildPhase>(), v => Assert.False(string.IsNullOrWhiteSpace(FlagTrackerTexts.Phase(v, 1, 2))));
    }

    [Fact]
    public void TextsFallBackToGermanWithoutLoadedTable()
    {
        Assert.Equal("Flag-Tracker", FlagTrackerTexts.WindowTitle);
        Assert.Equal("Dateien lesen 3/7", FlagTrackerTexts.Phase(BuildPhase.Reading, 3, 7));
        Assert.Equal("Namen auflösen", FlagTrackerTexts.Phase(BuildPhase.Resolving, 1, 1));
        Assert.Equal("über Muster (nicht sicher) (4)", FlagTrackerTexts.PatternGroup(4));
        Assert.Equal("a_b (2)", FlagTrackerTexts.NameGroup("a_b", 2));
        Assert.Equal("über x – im Effekt f.txt:12", FlagTrackerTexts.Via("x", "f.txt", 12));
        Assert.Equal("Aufbau fehlgeschlagen: boom", FlagTrackerTexts.StatusFailed("boom"));
        Assert.Contains("5 Dateien, 1 nicht lesbar", FlagTrackerTexts.Status(new DateTime(2026, 10, 27, 12, 0, 0), 5, 1));
    }

    private static string SourceCode() => File.ReadAllText(RepoFiles.PathOf("src/ParadoxModTool.FlagTracker/FlagTrackerTexts.cs"));

    private static void AssertLocalized(string key, string text, IReadOnlyDictionary<string, string> german, IReadOnlyDictionary<string, string> english)
    {
        Assert.True(german.TryGetValue(key, out var de), $"{key} fehlt in locales/de.json");
        Assert.Equal(text, de);
        Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} fehlt in locales/en.json");
        Assert.False(en!.EndsWith("…") || en.EndsWith("...") || de!.EndsWith("…") || de.EndsWith("..."), $"{key} endet mit Auslassungspunkten");
        Assert.Equal(Placeholders(de!), Placeholders(en));
    }

    private static IEnumerable<string> Placeholders(string text) =>
        Regex.Matches(text, "\\{\\d+[^}]*\\}").Select(m => m.Value).Order();
}
