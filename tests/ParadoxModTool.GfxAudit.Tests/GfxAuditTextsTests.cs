using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using ParadoxModTool.ScriptCheck;
using Xunit;

namespace ParadoxModTool.GfxAudit.Tests;

public sealed class GfxAuditTextsTests
{
    [Fact]
    public void GermanFallbackMatchesDeJson()
    {
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));
        var english = UiTextTable.Read(RepoFiles.PathOf("locales/en.json"));
        var matches = Regex.Matches(SourceCode(), "\\b[TF]\\(\"(?<key>gfxaudit\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count >= 25, $"nur {matches.Count} Texte gefunden");
        Assert.Equal(matches.Count, matches.Select(m => m.Groups["key"].Value).Distinct().Count());
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;
            var text = Regex.Unescape(match.Groups["text"].Value);
            Assert.True(german.TryGetValue(key, out var de), $"{key} fehlt in locales/de.json");
            Assert.Equal(text, de);
            Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} fehlt in locales/en.json");
            Assert.Equal(Placeholders(de!), Placeholders(en!));
        }
    }

    [Fact]
    public void EveryGfxAuditKeyInTheLocaleFilesIsUsedByTheCode()
    {
        var used = Regex.Matches(SourceCode(), "\\b[TF]\\(\"(?<key>gfxaudit\\.[^\"]+)\"").Select(m => m.Groups["key"].Value).ToHashSet();
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));
        var english = UiTextTable.Read(RepoFiles.PathOf("locales/en.json"));

        foreach (var key in german.Keys.Concat(english.Keys).Where(k => k.StartsWith("gfxaudit.", StringComparison.Ordinal)))
        {
            Assert.Contains(key, used);
        }
    }

    [Fact]
    public void EveryEnumHasText()
    {
        Assert.All(Enum.GetValues<GfxFindingKind>(), v => Assert.False(string.IsNullOrWhiteSpace(GfxAuditTexts.Kind(v))));
        Assert.All(Enum.GetValues<FindingSeverity>(), v => Assert.False(string.IsNullOrWhiteSpace(GfxAuditTexts.Severity(v))));
    }

    [Fact]
    public void TextsFallBackToGermanWithoutLoadedTable()
    {
        Assert.Equal("GFX-Prüfung", GfxAuditTexts.Menu);
        Assert.Equal("Ungenutztes Bild", GfxAuditTexts.Kind(GfxFindingKind.UnusedImage));
        Assert.Equal("Doppelter Sprite-Name", GfxAuditTexts.Kind(GfxFindingKind.DuplicateSprite));
        Assert.Equal("Benutzt von / zeigt auf", GfxAuditTexts.ColRelated);
        Assert.Equal("Prüfung fehlgeschlagen: boom", GfxAuditTexts.Failed("boom"));
        Assert.Equal("2 Dateien nicht lesbar", GfxAuditTexts.Unread(2));
        Assert.Contains("5 Bilder, 7 Dateien", GfxAuditTexts.Status(5, 7, new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc)));
    }

    private static string SourceCode() => File.ReadAllText(RepoFiles.PathOf("src/ParadoxModTool.GfxAudit/GfxAuditTexts.cs"));

    private static IEnumerable<string> Placeholders(string text) =>
        Regex.Matches(text, "\\{\\d+[^}]*\\}").Select(m => m.Value).Order();
}
