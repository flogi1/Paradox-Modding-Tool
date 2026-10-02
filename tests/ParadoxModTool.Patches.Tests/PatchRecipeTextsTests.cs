using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>Jeder Text des Fensters Patch-Rezepte und seiner Anleitung steht in allen 8 Sprachdateien; der deutsche Text entspricht dem Rückfall im Code.</summary>
public sealed class PatchRecipeTextsTests
{
    private static readonly string[] Locales = { "de", "en", "es", "fr", "pl", "pt-br", "ru", "zh-hans" };

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "locales", "de.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("locales/de.json");
    }

    private static Dictionary<string, string> Read(string root, string locale)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "locales", locale + ".json")));
        return document.RootElement.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(p => p.Name, p => p.Value.GetString()!);
    }

    private static void AssertTextsComplete(string sourceFile, string prefix, int minimum)
    {
        var root = RepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "ParadoxModTool.App", sourceFile));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>" + Regex.Escape(prefix) + "\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");
        var tables = Locales.ToDictionary(l => l, l => Read(root, l));

        Assert.True(matches.Count >= minimum, $"nur {matches.Count} Texte gefunden");
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;
            Assert.Equal(Regex.Unescape(match.Groups["text"].Value), tables["de"].GetValueOrDefault(key));
            foreach (var locale in Locales)
            {
                Assert.True(tables[locale].TryGetValue(key, out var text) && text.Length > 0, $"{key} fehlt in locales/{locale}.json");
                Assert.False(text!.EndsWith("…") || text.EndsWith("..."), $"{key} endet in {locale} mit Auslassungspunkten");
            }
        }
    }

    [Fact]
    public void Every_patch_window_text_exists_in_all_locales() => AssertTextsComplete("PatchRecipeTexts.cs", "patchUi", 15);

    [Fact]
    public void Every_patch_guide_text_exists_in_all_locales() => AssertTextsComplete("PatchRecipeHelpWindow.cs", "patchhelp", 30);
}
