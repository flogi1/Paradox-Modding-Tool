using System.Text.Json;
using ParadoxModTool.Generators.Lookups;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.Generators.Tests.Iv2;

/// <summary>
/// Optionaler Abgleich der Nachschlage-Quellen mit dem Python-Generator (Spec Teil 3, Abschnitt 5): läuft nur mit <c>PMT_EU5_GAME_DIR</c> (Ordner <c>…/game</c>),
/// <c>PMT_IV2_IDEA_DB</c> (iv2_idea_database.json) und <c>PMT_LOOKUP_EXPECTED</c> (Ausgabe von <c>lookup_counts.py</c>), sonst endet er sofort.
/// Spieldaten und Datenbank kommen nicht ins Repo.
/// </summary>
public sealed class Iv2LookupLocalTests
{
    private readonly ITestOutputHelper _output;

    public Iv2LookupLocalTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Lookup_counts_match_python()
    {
        var game = Environment.GetEnvironmentVariable("PMT_EU5_GAME_DIR");
        var database = Environment.GetEnvironmentVariable("PMT_IV2_IDEA_DB");
        var expectedPath = Environment.GetEnvironmentVariable("PMT_LOOKUP_EXPECTED");
        if (string.IsNullOrEmpty(game) || string.IsNullOrEmpty(database) || string.IsNullOrEmpty(expectedPath) || !File.Exists(expectedPath))
        {
            return;
        }

        using var temp = new TempFolder();
        Directory.CreateDirectory(temp.PathOf("assets"));
        File.Copy(database, temp.PathOf("assets/idea_database.json"));
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2"), temp.Root, shipped: true, uiLanguage: "de");
        var lookup = package.Root!.Child!.Fields.Single(f => f.Id == "name").Lookup!;

        var catalog = LookupCatalog.Load(package, lookup, new[] { game });

        using var expected = JsonDocument.Parse(File.ReadAllText(expectedPath));
        var root = expected.RootElement;
        var counts = catalog.Entries.GroupBy(e => e.SourceId).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (source, count) in counts)
        {
            _output.WriteLine($"{source}: {count}");
        }

        // Strukturblöcke der Gesetze dürfen keine Einträge werden (namedOnly).
        var structural = new[] { "potential", "allow", "country_modifier", "estate_preferences", "on_activate", "on_deactivate", "ai_will_do", "modifier" };
        var structuralHits = catalog.Entries.Where(e => e.SourceId is "laws" or "reforms" && structural.Contains(e.Key)).Select(e => e.Key).Distinct().ToList();
        _output.WriteLine("structural: " + string.Join(", ", structuralHits));
        Assert.Empty(structuralHits);
        Assert.Empty(catalog.Notes);
        Assert.Equal(root.GetProperty("advances").GetInt32(), counts["advances"]);
        Assert.Equal(root.GetProperty("nations_ideas").GetInt32(), counts["nations"]);
        foreach (var sample in root.GetProperty("samples").EnumerateObject())
        {
            var entry = catalog.Entries.Single(e => e.SourceId == "advances" && e.Key == sample.Name);
            Assert.Equal(sample.Value.GetString(), entry.Name);
            Assert.Equal(root.GetProperty("sample_tags").GetProperty(sample.Name).GetString(), entry.Tag);
        }
    }
}
