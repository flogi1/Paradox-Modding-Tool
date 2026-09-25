using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.Notes.Tests;

public class BugTextsTests
{
    [Fact]
    public void JederBugTextKeyStehtInDeutschUndEnglisch_MitGleichemDeutschenText()
    {
        var root = RepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "ParadoxModTool.Notes", "BugTexts.cs"));
        var german = UiTextTable.Read(Path.Combine(root, "locales", "de.json"));
        var english = UiTextTable.Read(Path.Combine(root, "locales", "en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>bugs\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count > 50, $"nur {matches.Count} Texte gefunden");
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;
            var text = Regex.Unescape(match.Groups["text"].Value);
            Assert.True(german.TryGetValue(key, out var de), $"{key} fehlt in locales/de.json");
            Assert.Equal(text, de);
            Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} fehlt in locales/en.json");
            Assert.False(en!.EndsWith("…") || en.EndsWith("...") || de!.EndsWith("…") || de.EndsWith("..."), $"{key} endet mit Auslassungspunkten");
        }
    }

    [Fact]
    public void Texte_FallenOhneTabelleAufDeutschZurueck()
    {
        Assert.Equal("Verworfen", BugTexts.Status(BugStatus.Rejected));
        Assert.Equal("3 neu", BugTexts.NewCount(3));
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "locales", "de.json")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
    }
}
