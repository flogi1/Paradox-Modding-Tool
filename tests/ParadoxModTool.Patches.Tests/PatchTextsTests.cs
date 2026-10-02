using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>Jeder Text der Patch-Rezepte steht in allen 8 Sprachdateien; der deutsche Text entspricht dem Rückfall im Code.</summary>
public sealed class PatchTextsTests
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

    [Fact]
    public void Every_patch_text_exists_in_all_locales()
    {
        var root = RepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "ParadoxModTool.Patches", "PatchTexts.cs"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>patch\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");
        var tables = Locales.ToDictionary(l => l, l => Read(root, l));

        Assert.True(matches.Count >= 27, $"nur {matches.Count} Texte gefunden");
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
}
