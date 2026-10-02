using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class GuiDataTextsTests
{
    [Fact]
    public void EveryKeyExistsInGermanAndEnglishWithMatchingGermanText()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "ParadoxModTool.GuiDesign", "GuiDataTexts.cs"));
        var german = UiTextTable.Read(Path.Combine(root, "locales", "de.json"));
        var english = UiTextTable.Read(Path.Combine(root, "locales", "en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>guiData\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count >= 30, $"nur {matches.Count} Texte gefunden");
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
    public void NoGuiDataTextUsesAnEllipsis()
    {
        var root = FindRepositoryRoot();
        foreach (var language in new[] { "de", "en" })
        {
            var table = UiTextTable.Read(Path.Combine(root, "locales", language + ".json"));
            var bad = table.Where(pair => pair.Key.StartsWith("guiData.", StringComparison.Ordinal) && (pair.Value.Contains('…') || pair.Value.Contains("...")))
                .Select(pair => $"{language}: {pair.Key}")
                .ToList();
            Assert.Empty(bad);
        }
    }

    [Fact]
    public void FormattedTextsFillTheirPlaceholders()
    {
        Assert.Equal("Datentypen vom 17.05.2026", GuiDataTexts.FilesDate("17.05.2026"));
        Assert.Equal("Nicht lesbar: data_types_gui.txt", GuiDataTexts.Unreadable("data_types_gui.txt"));
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
