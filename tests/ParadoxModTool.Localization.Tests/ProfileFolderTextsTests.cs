using System.Text.Json;
using Xunit;

namespace ParadoxModTool.Localization.Tests;

/// <summary>Die Ordnerkategorien der Spielprofile (Label und Beschreibung) laufen über Sprachdateien; der deutsche Text im Profil ist der Rückfall und gleicht de.json.</summary>
public sealed class ProfileFolderTextsTests
{
    private static readonly string Root = FindRepositoryRoot();

    public static IEnumerable<object[]> Profiles() =>
        Directory.EnumerateFiles(Path.Combine(FindRepositoryRoot(), "profiles"), "*.json").Select(path => new object[] { Path.GetFileName(path) });

    [Theory]
    [MemberData(nameof(Profiles))]
    public void EveryFolderCategoryHasLabelAndDescriptionKeysInGermanAndEnglish(string profile)
    {
        var german = UiTextTable.Read(Path.Combine(Root, "locales", "de.json"));
        var english = UiTextTable.Read(Path.Combine(Root, "locales", "en.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "profiles", profile)));

        foreach (var folder in document.RootElement.GetProperty("folderCategories").EnumerateArray())
        {
            var path = folder.GetProperty("path").GetString();
            Check(path, folder, "label", "labelKey", german, english, required: true);
            Check(path, folder, "description", "descriptionKey", german, english, required: false);
        }
    }

    private static void Check(string? path, JsonElement folder, string textField, string keyField, IReadOnlyDictionary<string, string> german, IReadOnlyDictionary<string, string> english, bool required)
    {
        var text = folder.TryGetProperty(textField, out var textElement) ? textElement.GetString() : null;
        if (string.IsNullOrEmpty(text))
        {
            Assert.False(required, $"{path}: {textField} fehlt");
            return;
        }

        var key = folder.TryGetProperty(keyField, out var keyElement) ? keyElement.GetString() : null;
        Assert.False(string.IsNullOrEmpty(key), $"{path}: {keyField} fehlt");
        Assert.True(german.ContainsKey(key!), $"{path}: {key} fehlt in de.json");
        Assert.Equal(text, german[key!]);
        Assert.True(english.ContainsKey(key!), $"{path}: {key} fehlt in en.json");
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

        throw new DirectoryNotFoundException("Repository-Wurzel nicht gefunden.");
    }
}
