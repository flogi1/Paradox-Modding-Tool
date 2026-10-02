using System.Text.Json;
using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class UiTextTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-loc-" + Guid.NewGuid().ToString("N"));

    public UiTextTests()
    {
        Directory.CreateDirectory(_temp);
        File.WriteAllText(Path.Combine(_temp, "de.json"), "{ \"_comment\": \"x\", \"a.key\": \"Hallo\", \"b.key\": \"Nur Deutsch\", \"fmt\": \"{0} Dateien\" }");
        File.WriteAllText(Path.Combine(_temp, "en.json"), "{ \"a.key\": \"Hello\", \"fmt\": \"{0} files\", \"bad\": \"{1\" }");
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void SelectedLanguageThenGermanThenCodeFallback()
    {
        var table = UiTextTable.Load(_temp, "en");

        Assert.Equal("en", table.Language);
        Assert.Equal("Hello", table.Get("a.key", "x"));
        Assert.Equal("Nur Deutsch", table.Get("b.key", "x"));
        Assert.Equal("Rückfall", table.Get("missing", "Rückfall"));
        Assert.Equal("-", table.Get("_comment", "-"));
    }

    [Fact]
    public void FormatUsesTheTranslationAndFallsBackOnBrokenPatterns()
    {
        var table = UiTextTable.Load(_temp, "en");

        Assert.Equal("3 files", table.Format("fmt", "{0} x", 3));
        Assert.Equal("ok 5", table.Format("bad", "ok {0}", 5));
    }

    [Fact]
    public void MissingOrBrokenFilesGiveTheFallbacks()
    {
        Assert.Equal("Hello", UiTextTable.Load(_temp, "fr").Get("a.key", "x"));
        Assert.Equal("x", UiTextTable.Load(Path.Combine(_temp, "missing"), "de").Get("a.key", "x"));

        var broken = Path.Combine(_temp, "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "de.json"), "{ nicht json");
        Assert.Equal("x", UiTextTable.Load(broken, "de").Get("a.key", "x"));
        Assert.Equal("x", UiTextTable.Empty.Get("a.key", "x"));
    }

    [Fact]
    public void EveryGuiTextKeyExistsInGermanAndEnglishWithMatchingGermanText()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "ParadoxModTool.GuiDesign", "GuiTexts.cs"));
        var german = UiTextTable.Read(Path.Combine(root, "locales", "de.json"));
        var english = UiTextTable.Read(Path.Combine(root, "locales", "en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>gui\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count > 40, $"nur {matches.Count} Texte gefunden");
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;
            var text = match.Groups["text"].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
            Assert.True(german.TryGetValue(key, out var de), $"{key} fehlt in locales/de.json");
            Assert.Equal(text, de);
            Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} fehlt in locales/en.json");
        }
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

        throw new InvalidOperationException("Repository-Wurzel (ParadoxModTool.slnx) nicht gefunden.");
    }
}
