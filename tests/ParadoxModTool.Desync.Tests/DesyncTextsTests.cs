using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

/// <summary>Jeder Text der Desync-Analyse steht in allen 8 Sprachdateien; der deutsche Text entspricht dem Rückfall im Code.</summary>
public sealed class DesyncTextsTests
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

    private static string[] Placeholders(string text) =>
        Regex.Matches(text, "\\{(?<index>\\d+)(?:[,:][^}]*)?\\}").Select(m => m.Groups["index"].Value).Distinct().Order().ToArray();

    [Fact]
    public void Every_desync_text_exists_in_all_locales()
    {
        var root = RepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "ParadoxModTool.Desync", "DesyncTexts.cs"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>desync\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");
        var tables = Locales.ToDictionary(l => l, l => Read(root, l));

        Assert.True(matches.Count > 40, $"nur {matches.Count} Texte gefunden");
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;
            Assert.Equal(Regex.Unescape(match.Groups["text"].Value), tables["de"].GetValueOrDefault(key));
            foreach (var locale in Locales)
            {
                Assert.True(tables[locale].TryGetValue(key, out var text) && text.Length > 0, $"{key} fehlt in locales/{locale}.json");
                Assert.False(text!.EndsWith("…") || text.EndsWith("..."), $"{key} endet in {locale} mit Auslassungspunkten");
                Assert.True(Placeholders(tables["de"][key]).SequenceEqual(Placeholders(text)), $"{key} hat in {locale} andere Platzhalter als in de");
            }
        }

        Assert.Contains("Desync", tables["de"]["desync.menu"]);
    }

    [Fact]
    public void Every_desync_key_in_the_locale_files_is_used_by_the_code()
    {
        var root = RepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "ParadoxModTool.Desync", "DesyncTexts.cs"));
        var used = Regex.Matches(code, "\\b[TF]\\(\"(?<key>desync\\.[^\"]+)\"").Select(m => m.Groups["key"].Value).ToHashSet();

        foreach (var locale in Locales)
        {
            foreach (var key in Read(root, locale).Keys.Where(k => k.StartsWith("desync.", StringComparison.Ordinal)))
            {
                Assert.True(used.Contains(key), $"{key} in locales/{locale}.json wird im Code nicht verwendet");
            }
        }
    }
}
