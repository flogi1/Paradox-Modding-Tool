using System.Text.Json;
using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

public sealed class WikiScriptDocImporterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pmt-wikidocs-").FullName;
    private static readonly DateTime T0 = new(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* Best-effort Aufräumen. */ }
    }

    private string WriteFile(string json)
    {
        var path = Path.Combine(_dir, "eu4.json");
        File.WriteAllText(path, json);
        return path;
    }

    private const string OneOfEach =
        """
        {
          "fetchedUtc": "2026-09-22T00:00:00Z",
          "entries": [
            { "kind": "Modifier", "name": "army_tradition", "description": "Adds to yearly army tradition.", "scopes": ["Country modifiers"], "targets": [], "extra": { "Effect type": "Additive" } },
            { "kind": "Trigger", "name": "culture", "description": "Returns true if the province culture is X.", "scopes": ["Province"], "targets": [], "extra": { "Type": "Identifier, Scope" } },
            { "kind": "Effect", "name": "add_country_modifier", "description": "Adds an event modifier.", "scopes": ["Country scope"], "targets": [], "extra": {} },
            { "kind": "OnAction", "name": "on_abandon_colony", "description": "Fired right before the colony is abandoned.", "scopes": ["location"], "targets": [], "extra": { "THIS": "country" } }
          ]
        }
        """;

    [Fact]
    public void Import_ReadsAllFourKinds()
    {
        var path = WriteFile(OneOfEach);
        var db = new ScriptDocDatabase();

        var record = WikiScriptDocImporter.Import(db, path, T0);

        Assert.NotNull(record);
        Assert.Equal(4, db.Entries.Count);
        Assert.Contains(db.Entries, e => e is { Kind: ScriptDocKind.Modifier, Name: "army_tradition" });
        Assert.Contains(db.Entries, e => e is { Kind: ScriptDocKind.Trigger, Name: "culture" });
        Assert.Contains(db.Entries, e => e is { Kind: ScriptDocKind.Effect, Name: "add_country_modifier" });
        var onAction = db.Entries.Single(e => e.Kind == ScriptDocKind.OnAction);
        Assert.Equal(new[] { "location" }, onAction.Scopes);
        Assert.Equal("country", onAction.Extra["THIS"]);
    }

    [Fact]
    public void NeedsImport_FalseWithoutFile_TrueAfterFileAppears_FalseAfterImport_TrueAfterChange()
    {
        var db = new ScriptDocDatabase();
        var path = Path.Combine(_dir, "eu4.json");
        Assert.False(WikiScriptDocImporter.NeedsImport(db, path));

        File.WriteAllText(path, OneOfEach);
        Assert.True(WikiScriptDocImporter.NeedsImport(db, path));

        WikiScriptDocImporter.Import(db, path, T0);
        Assert.False(WikiScriptDocImporter.NeedsImport(db, path));

        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(5));
        Assert.True(WikiScriptDocImporter.NeedsImport(db, path));
    }

    [Fact]
    public void Import_MergesDuplicateNames_UnitingScopes()
    {
        var path = WriteFile(
            """
            {
              "fetchedUtc": "2026-09-22T00:00:00Z",
              "entries": [
                { "kind": "Effect", "name": "owner", "description": "Refers to the owning country.", "scopes": ["Country trigger scope"], "targets": ["Country"], "extra": {} },
                { "kind": "Effect", "name": "owner", "description": "", "scopes": ["Province trigger scope"], "targets": ["Country"], "extra": {} }
              ]
            }
            """);
        var db = new ScriptDocDatabase();

        WikiScriptDocImporter.Import(db, path, T0);

        var entry = Assert.Single(db.Entries);
        Assert.Equal(new[] { "Country trigger scope", "Province trigger scope" }, entry.Scopes);
        Assert.Equal("Refers to the owning country.", entry.Description);
    }

    [Fact]
    public void Import_ReturnsNull_OnMissingOrEmptyFile()
    {
        var db = new ScriptDocDatabase();
        Assert.Null(WikiScriptDocImporter.Import(db, Path.Combine(_dir, "fehlt.json"), T0));

        var empty = WriteFile("""{ "fetchedUtc": "2026-09-22T00:00:00Z", "entries": [] }""");
        Assert.Null(WikiScriptDocImporter.Import(db, empty, T0));
    }

    [Fact]
    public void Import_ReturnsNull_OnMalformedJson()
    {
        var db = new ScriptDocDatabase();
        var path = WriteFile("{ kaputt");

        Assert.Null(WikiScriptDocImporter.Import(db, path, T0));
        Assert.Empty(db.Entries);
    }

    [Fact]
    public void ReadFetchedUtc_ReturnsValueFromFile()
    {
        var path = WriteFile(OneOfEach);

        var fetchedUtc = WikiScriptDocImporter.ReadFetchedUtc(path);

        Assert.Equal(new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc), fetchedUtc);
    }

    [Fact]
    public void ReadFetchedUtc_ReturnsNull_OnMissingOrMalformedFile()
    {
        Assert.Null(WikiScriptDocImporter.ReadFetchedUtc(Path.Combine(_dir, "fehlt.json")));

        var path = WriteFile("{ kaputt");
        Assert.Null(WikiScriptDocImporter.ReadFetchedUtc(path));
    }

    [Fact]
    public void SecondImport_UpdatesExisting_AndMarksMissingOutdated()
    {
        var path = WriteFile(OneOfEach);
        var db = new ScriptDocDatabase();
        WikiScriptDocImporter.Import(db, path, T0);

        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(5));
        File.WriteAllText(
            path,
            """
            {
              "fetchedUtc": "2026-09-22T01:00:00Z",
              "entries": [
                { "kind": "Modifier", "name": "army_tradition", "description": "Updated text.", "scopes": ["Country modifiers"], "targets": [], "extra": {} }
              ]
            }
            """);

        WikiScriptDocImporter.Import(db, path, T0.AddHours(1));

        var tradition = db.Entries.Single(e => e.Name == "army_tradition");
        Assert.Equal("Updated text.", tradition.Description);
        Assert.False(db.IsOutdated(tradition));
        var culture = db.Entries.Single(e => e.Name == "culture");
        Assert.True(db.IsOutdated(culture));
    }
}
