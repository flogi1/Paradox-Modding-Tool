using System.Globalization;
using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class PerformanceTextsTests
{
    [Fact]
    public void EveryKeyExistsInGermanAndEnglishWithMatchingGermanTextAndPlaceholders()
    {
        var code = File.ReadAllText(RepoFiles.PathOf("src/ParadoxModTool.Performance/PerformanceTexts.cs"));
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));
        var english = UiTextTable.Read(RepoFiles.PathOf("locales/en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>performance\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count >= 20, $"nur {matches.Count} Texte gefunden");
        Assert.Equal(matches.Count, matches.Select(m => m.Groups["key"].Value).Distinct().Count());
        foreach (Match match in matches)
        {
            AssertLocalized(match.Groups["key"].Value, Regex.Unescape(match.Groups["text"].Value), german, english);
        }
    }

    [Fact]
    public void EveryPerformanceKeyInTheLocaleFilesIsUsedByTheCode()
    {
        var code = File.ReadAllText(RepoFiles.PathOf("src/ParadoxModTool.Performance/PerformanceTexts.cs"));
        var used = Regex.Matches(code, "\\b[TF]\\(\"(?<key>performance\\.[^\"]+)\"").Select(m => m.Groups["key"].Value).ToHashSet();
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));

        foreach (var key in german.Keys.Where(k => k.StartsWith("performance.", StringComparison.Ordinal)))
        {
            Assert.Contains(key, used);
        }
    }

    [Fact]
    public void TextsFallBackToGermanWithoutLoadedTable()
    {
        Assert.Equal("s", PerformanceTexts.TimeUnit);
        Assert.Equal("Mod", PerformanceTexts.Origin(RowOrigin.Mod));
        Assert.Equal("Mod und Vanilla", PerformanceTexts.Origin(RowOrigin.Both));
        Assert.Equal("Andere Mod", PerformanceTexts.Origin(RowOrigin.OtherMod));
        Assert.Equal("Vanilla", PerformanceTexts.Origin(RowOrigin.Vanilla));
        Assert.Equal("Unbekannt", PerformanceTexts.Origin(RowOrigin.Unknown));
        Assert.Equal("besser", PerformanceTexts.VerdictName(Verdict.Better));
        Assert.Equal("schlechter", PerformanceTexts.VerdictName(Verdict.Worse));
        Assert.Equal("unverändert", PerformanceTexts.VerdictName(Verdict.Unchanged));
        Assert.Equal("neu in B", PerformanceTexts.VerdictName(Verdict.NewInB));
        Assert.Equal("fehlt in B", PerformanceTexts.VerdictName(Verdict.MissingInB));
        Assert.Equal("Verhalten geändert", PerformanceTexts.VerdictName(Verdict.BehaviorChanged));
        Assert.Equal("Profiler-Lauf vorher: 12 Monate", PerformanceTexts.ReportHeader("vorher", 12));
        Assert.Equal("Profiler-Lauf vorher: Monate unbekannt", PerformanceTexts.ReportHeader("vorher", null));
        Assert.Equal("Vergleich der Profiler-Läufe: A = a, B = b", PerformanceTexts.ComparisonHeader("a", "b"));
        Assert.Contains("A: 12, B: 24", PerformanceTexts.MonthsDiffer(12, 24));
        Assert.Contains("A: ?, B: 24", PerformanceTexts.MonthsDiffer(null, 24));
        Assert.Contains("unbekannt", PerformanceTexts.MonthsUnknown);
    }

    [Fact]
    public void EveryOriginAndVerdictHasADistinctNonEmptyName()
    {
        var origins = Enum.GetValues<RowOrigin>().Select(PerformanceTexts.Origin).ToList();
        var verdicts = Enum.GetValues<Verdict>().Select(PerformanceTexts.VerdictName).ToList();

        Assert.All(origins.Concat(verdicts), name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(origins.Count, origins.Distinct().Count());
        Assert.Equal(verdicts.Count, verdicts.Distinct().Count());
    }

    [Fact]
    public void NoiseNoteShowsAllThreeThresholdsInTheCurrentCulture()
    {
        var note = PerformanceTexts.NoiseNote(new NoiseSettings(0.10, 0.05, 0.25));

        Assert.Contains(0.10.ToString("P0", CultureInfo.CurrentCulture), note);
        Assert.Contains(0.05.ToString("N3", CultureInfo.CurrentCulture) + " " + PerformanceTexts.TimeUnit, note);
        Assert.Contains(0.25.ToString("P0", CultureInfo.CurrentCulture), note);
    }

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
