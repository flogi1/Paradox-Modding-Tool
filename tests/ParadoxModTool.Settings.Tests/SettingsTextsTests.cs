using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class SettingsTextsTests
{
    [Fact]
    public void EverySettingsTextKeyExistsInGermanAndEnglishWithMatchingGermanText()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "ParadoxModTool.Settings", "SettingsTexts.cs"));
        var german = UiTextTable.Read(Path.Combine(root, "locales", "de.json"));
        var english = UiTextTable.Read(Path.Combine(root, "locales", "en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>settings\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count >= 120, $"nur {matches.Count} Texte gefunden");
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;
            var text = match.Groups["text"].Value.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");
            Assert.True(german.TryGetValue(key, out var de), $"{key} fehlt in locales/de.json");
            Assert.Equal(text, de);
            Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} fehlt in locales/en.json");
        }
    }

    [Fact]
    public void ErrorLogSettingsTextsHaveGermanFallbacks()
    {
        Assert.Equal("Fehlerprotokoll", SettingsTexts.IconLabel("ErrorLog"));
        Assert.Equal("Fehlerprotokoll als eigenes Fenster", SettingsTexts.ErrorLogStandalone);
        Assert.Equal("Spielstand-Explorer als eigenes Fenster", SettingsTexts.SaveExplorerStandalone);
        Assert.Equal("Spielstand-Explorer", SettingsTexts.CategoryLabel(SettingsCategory.SaveExplorer));
    }

    [Fact]
    public void ThemeCopyPromptNamesTheTheme()
    {
        Assert.Contains("Dunkel", SettingsTexts.ThemeReadOnlyCopyPrompt("Dunkel"));
    }

    [Fact]
    public void NoSettingsTextUsesAnEllipsis()
    {
        var root = FindRepositoryRoot();
        foreach (var language in new[] { "de", "en" })
        {
            var table = UiTextTable.Read(Path.Combine(root, "locales", language + ".json"));
            var bad = table.Where(pair => pair.Key.StartsWith("settings.", StringComparison.Ordinal) && (pair.Value.Contains('…') || pair.Value.Contains("...")))
                .Select(pair => $"{language}: {pair.Key}")
                .ToList();
            Assert.Empty(bad);
        }
    }

    [Fact]
    public void EveryCategoryToolbarItemAndIconHasALabel()
    {
        Assert.All(SettingsCategories.All, category => Assert.False(string.IsNullOrWhiteSpace(SettingsTexts.CategoryLabel(category))));
        Assert.Equal(SettingsCategories.All.Count, SettingsCategories.All.Select(SettingsTexts.CategoryLabel).Distinct().Count());
        // Unbekannte Kennungen liefern die Kennung selbst; bekannte haben je einen eigenen Text (daher: alle verschieden, Stichproben).
        Assert.Equal(ToolbarCatalog.All.Count, ToolbarCatalog.All.Select(item => SettingsTexts.ToolbarItemLabel(item.Id)).Distinct().Count());
        Assert.Equal("Suchen & Ersetzen", SettingsTexts.ToolbarItemLabel(ToolbarItemIds.Search));
        Assert.All(Enum.GetValues<ToolbarGroup>(), group => Assert.False(string.IsNullOrWhiteSpace(SettingsTexts.ToolbarGroupLabel(group))));
        Assert.Equal(6, IconIds.All.Append("Lock").Select(SettingsTexts.IconLabel).Distinct().Count());
        Assert.Equal("Sperre", SettingsTexts.IconLabel("Lock"));
        Assert.Equal("Vanilla-Datei", SettingsTexts.IconLabel(IconIds.Vanilla));
    }

    [Theory]
    [InlineData(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "P", "Strg+Umschalt+P")]
    [InlineData(HotkeyModifiers.None, "Escape", "Esc")]
    [InlineData(HotkeyModifiers.Ctrl, "Space", "Strg+Leertaste")]
    [InlineData(HotkeyModifiers.Alt, "F", "Alt+F")]
    [InlineData(HotkeyModifiers.None, "F3", "F3")]
    [InlineData(HotkeyModifiers.None, "Delete", "Entf")]
    [InlineData(HotkeyModifiers.Alt, "Up", "Alt+Pfeil nach oben")]
    public void GestureUsesGermanKeyNames(HotkeyModifiers modifiers, string key, string expected) =>
        Assert.Equal(expected, SettingsTexts.Gesture(modifiers, key));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ParadoxModTool.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository-Wurzel (ParadoxModTool.slnx) nicht gefunden.");
    }
}
