using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class ScriptCheckTextsTests
{
    [Fact]
    public void EveryKeyExistsInGermanAndEnglishWithMatchingGermanTextAndPlaceholders()
    {
        var code = SourceCode();
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));
        var english = UiTextTable.Read(RepoFiles.PathOf("locales/en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>scriptcheck\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count >= 15, $"nur {matches.Count} Texte gefunden");
        Assert.Equal(matches.Count, matches.Select(m => m.Groups["key"].Value).Distinct().Count());
        foreach (Match match in matches)
        {
            AssertLocalized(match.Groups["key"].Value, Regex.Unescape(match.Groups["text"].Value), german, english);
        }
    }

    [Fact]
    public void EveryScriptCheckKeyInTheLocaleFilesIsUsedByTheCode()
    {
        var used = Regex.Matches(SourceCode(), "\\b[TF]\\(\"(?<key>scriptcheck\\.[^\"]+)\"").Select(m => m.Groups["key"].Value).ToHashSet();
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));

        foreach (var key in german.Keys.Where(k => k.StartsWith("scriptcheck.", StringComparison.Ordinal)))
        {
            Assert.Contains(key, used);
        }
    }

    [Fact]
    public void EveryEnumHasText()
    {
        Assert.All(Enum.GetValues<FindingKind>(), v => Assert.False(string.IsNullOrWhiteSpace(ScriptCheckTexts.Kind(v))));
        Assert.All(Enum.GetValues<FindingSeverity>(), v => Assert.False(string.IsNullOrWhiteSpace(ScriptCheckTexts.Severity(v))));
        Assert.All(Enum.GetValues<PerformanceTargetKind>(), v => Assert.False(string.IsNullOrWhiteSpace(ScriptCheckTexts.PerfTargetKind(v))));
    }

    [Fact]
    public void PerformanceTabTextsFallBackToGerman()
    {
        Assert.Equal("Leistung anderer Ziele", ScriptCheckTexts.TabPerformance);
        Assert.Equal("Ziel:", ScriptCheckTexts.TargetLabel);
        Assert.Equal("Vanilla", ScriptCheckTexts.PerfTargetKind(PerformanceTargetKind.Vanilla));
        Assert.Equal("Playset", ScriptCheckTexts.PerfTargetKind(PerformanceTargetKind.PlaysetMod));
        Assert.Equal("Installiert", ScriptCheckTexts.PerfTargetKind(PerformanceTargetKind.InstalledMod));
        Assert.Equal("Leistung prüfen", ScriptCheckTexts.CheckPerformance);
        Assert.Equal("Als Bericht kopieren", ScriptCheckTexts.CopyReport);
        Assert.Equal("Weg", ScriptCheckTexts.ColPath);
        Assert.StartsWith("Für dieses Ziel gibt es noch kein Ergebnis", ScriptCheckTexts.PerfNoResult);
        Assert.Equal("Kein Ziel zur Auswahl.", ScriptCheckTexts.NoTargets);
    }

    [Fact]
    public void TextsFallBackToGermanWithoutLoadedTable()
    {
        Assert.Equal("Skript-Prüfung", ScriptCheckTexts.Menu);
        Assert.Equal("Verweis ins Leere", ScriptCheckTexts.Kind(FindingKind.BrokenReference));
        Assert.Equal("Event-Zirkel", ScriptCheckTexts.Kind(FindingKind.EventCycle));
        Assert.Equal("Teurer Iterator", ScriptCheckTexts.Kind(FindingKind.ExpensiveIterator));
        Assert.Equal("Prüfung fehlgeschlagen: boom", ScriptCheckTexts.StatusFailed("boom"));
        Assert.Contains("5 Dateien, 1 nicht lesbar, 3 Funde", ScriptCheckTexts.Status(new DateTime(2026, 10, 27, 12, 0, 0), 5, 1, 3));
        Assert.StartsWith("Script-Doku fehlt", ScriptCheckTexts.NoBuiltIns);
    }

    private static string SourceCode() => File.ReadAllText(RepoFiles.PathOf("src/ParadoxModTool.ScriptCheck/ScriptCheckTexts.cs"));

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
