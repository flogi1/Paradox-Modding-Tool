using System.Text.Json;
using Xunit;

namespace ParadoxModTool.Localization.Tests;

/// <summary>Jede mitgelieferte Sprachdatei in <c>locales</c> muss zur deutschen Referenz passen; Englisch muss vollständig sein.</summary>
public sealed class ShippedLocalesTests
{
    private static readonly string Locales = Path.Combine(FindRepositoryRoot(), "locales");

    [Fact]
    public void EveryShippedLocaleIsValid()
    {
        var reference = UiTextTable.Read(Path.Combine(Locales, "de.json"));
        var english = UiTextTable.Read(Path.Combine(Locales, "en.json"));
        var files = Directory.EnumerateFiles(Locales, "*.json")
            .Where(path => !Path.GetFileName(path).StartsWith('_') && !Path.GetFileName(path).Equals("de.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(files);
        foreach (var path in files)
        {
            var report = LocaleValidator.Check(path, reference, english);
            var name = Path.GetFileName(path);
            Assert.True(report.ParseError is null, $"{name}: {report.ParseError}");
            Assert.True(report.UnknownKeys.Count == 0, $"{name}: unbekannter Key {report.UnknownKeys.FirstOrDefault()}");
            Assert.True(report.PlaceholderMismatches.Count == 0, $"{name}: {report.PlaceholderMismatches.FirstOrDefault()}");
        }
    }

    [Fact]
    public void EnglishIsComplete()
    {
        var report = LocaleValidator.Check(
            Path.Combine(Locales, "en.json"),
            UiTextTable.Read(Path.Combine(Locales, "de.json")),
            UiTextTable.Read(Path.Combine(Locales, "en.json")));

        Assert.True(report.MissingKeys.Count == 0, $"en.json: {report.MissingKeys.Count} fehlen, z. B. {report.MissingKeys.FirstOrDefault()}");
    }

    [Fact]
    public void GlossaryIsValidJsonWhenPresent()
    {
        var glossary = Path.Combine(Locales, "_glossary.json");
        if (File.Exists(glossary))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(glossary));
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
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

        throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
    }
}
