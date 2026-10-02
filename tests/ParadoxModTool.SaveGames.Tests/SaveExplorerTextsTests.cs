using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.SaveGames.Tests;

public class SaveExplorerTextsTests
{
    [Fact]
    public void EveryKeyExistsInGermanAndEnglishWithMatchingGermanTextAndPlaceholders()
    {
        var code = File.ReadAllText(RepoFiles.PathOf("src/ParadoxModTool.SaveGames/SaveExplorerTexts.cs"));
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));
        var english = UiTextTable.Read(RepoFiles.PathOf("locales/en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>saveexplorer\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count >= 5, $"nur {matches.Count} Texte gefunden");
        Assert.Equal(matches.Count, matches.Select(m => m.Groups["key"].Value).Distinct().Count());
        foreach (Match match in matches)
        {
            AssertLocalized(match.Groups["key"].Value, Regex.Unescape(match.Groups["text"].Value), german, english);
        }
    }

    [Fact]
    public void EveryProfileKindLabelIsLocalizedWithTheProfileGermanText()
    {
        var german = UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));
        var english = UiTextTable.Read(RepoFiles.PathOf("locales/en.json"));

        foreach (var kind in Profiles.Eu5.SaveGames!.ObjectKinds)
        {
            AssertLocalized(kind.LabelKey, kind.Label, german, english);
        }
    }

    [Fact]
    public void TextsFallBackToGermanWithoutLoadedTable()
    {
        Assert.Equal("Global", SaveExplorerTexts.Global);
        Assert.Equal("Sonstige", SaveExplorerTexts.Other);
        Assert.Equal("ja", SaveExplorerTexts.Yes);
        Assert.Equal("nein", SaveExplorerTexts.No);
        Assert.Contains("-debug_mode", SaveExplorerTexts.DebugHint);
        Assert.Equal("Flag-Wert", SaveExplorerTexts.NameKind(SaveNameKind.FlagValue));
        Assert.Equal("entfernt", SaveExplorerTexts.Change(SaveChange.Removed));
        Assert.Equal(string.Empty, SaveExplorerTexts.Change(SaveChange.Unchanged));
        Assert.Equal(@"Ordner für Spielstände nicht gefunden: C:\x", SaveExplorerTexts.Notice(SaveNotice.FolderMissing, @"C:\x", null));
        Assert.Equal(SaveExplorerTexts.DebugHint, SaveExplorerTexts.Notice(SaveNotice.NoReadableSave, null, null));
        Assert.Equal("Spielstand kann gerade nicht gelesen werden: gesperrt", SaveExplorerTexts.Notice(SaveNotice.CannotRead, null, "gesperrt"));
        Assert.Equal(string.Empty, SaveExplorerTexts.Notice(SaveNotice.None, null, null));
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
