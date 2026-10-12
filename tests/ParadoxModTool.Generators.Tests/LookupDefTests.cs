using ParadoxModTool.Generators.Lookups;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Abschnitt <c>lookups</c> und Feld-<c>lookup</c> in package.json (Spec 2026-10-05-generator-packages-lookups, Abschnitt 1).</summary>
public sealed class LookupDefTests : IDisposable
{
    private const string Sources = """
        "assets": { "db": "assets/db.json" },
        "lookups": [
          { "id": "nations", "kind": "json", "asset": "db", "items": "nations[].ideas[]",
            "fields": { "key": "key", "name": "name", "desc": "desc", "tag": "../tag", "nation": "../label" }, "label": { "de": "Idee", "en": "Idea" } },
          { "id": "laws", "kind": "gameKeys", "folder": "in_game/common/laws", "depth": 2, "namedOnly": true,
            "tagFromFile": "^country_([A-Z]{2,5})\\.txt$", "tagInBlock": [ "has_or_had_tag", "tag" ], "label": "Gesetz" }
        ],
        """;

    private const string NameField = """{ "id": "name", "label": "Name", "type": "text", "multilingual": true }""";

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static string Json(string lookup, string sources = Sources) =>
        GeneratorPackageLoaderTests.ValidJson
            .Replace("\"targets\":", sources + "\n  \"targets\":")
            .Replace(NameField, NameField.TrimEnd('}') + ", \"lookup\": " + lookup + " }, { \"id\": \"desc\", \"label\": \"Text\", \"type\": \"multilineText\" }");

    private const string GoodLookup = """{ "sources": [ "nations", "laws" ], "fills": { "name": "name", "desc": "desc" } }""";

    private GeneratorPackage Load(string json)
    {
        _temp.Write("p/package.json", json);
        _temp.Write("p/templates/own.sbn", "x");
        return GeneratorPackageLoader.Load(_temp.PathOf("p"), uiLanguage: "de");
    }

    [Fact]
    public void Lookups_and_field_lookup_are_read()
    {
        var package = Load(Json(GoodLookup));

        var json = package.Lookups![0];
        Assert.Equal(("nations", LookupKind.Json, "Idee"), (json.Id, json.Kind, json.Label));
        Assert.Equal(("db", "nations[].ideas[]"), (json.Asset, json.Items));
        Assert.Equal("../tag", json.Fields!["tag"]);
        Assert.Equal("../label", json.Fields["nation"]);

        var game = package.Lookups[1];
        Assert.Equal(("laws", LookupKind.GameKeys, "Gesetz"), (game.Id, game.Kind, game.Label));
        Assert.Equal("in_game/common/laws", game.Folder);
        Assert.Equal(2, game.Depth);
        Assert.True(game.NamedOnly);
        Assert.Equal("^country_([A-Z]{2,5})\\.txt$", game.TagFromFile);
        Assert.Equal(new[] { "has_or_had_tag", "tag" }, game.TagInBlock);

        var lookup = package.Root!.Child!.Fields.Single(f => f.Id == "name").Lookup!;
        Assert.Equal(new[] { "nations", "laws" }, lookup.Sources);
        Assert.Equal("desc", lookup.Fills["desc"]);
    }

    [Fact]
    public void Unknown_lookup_source_is_a_load_error() =>
        Assert.Contains("nope", Assert.Throws<GeneratorPackageException>(() => Load(Json("""{ "sources": [ "nope" ], "fills": { "name": "name" } }"""))).Message);

    [Fact]
    public void Lookup_fills_must_name_fields_of_the_same_level() =>
        Assert.Contains("tag", Assert.Throws<GeneratorPackageException>(() => Load(Json("""{ "sources": [ "laws" ], "fills": { "name": "name", "desc": "tag" } }"""))).Message);

    [Fact]
    public void Lookup_fills_need_name_or_desc() =>
        Assert.Throws<GeneratorPackageException>(() => Load(Json("""{ "sources": [ "laws" ], "fills": { } }""")));

    [Fact]
    public void Unknown_lookup_kind_is_a_load_error() =>
        Assert.Contains("magic", Assert.Throws<GeneratorPackageException>(() => Load(Json(GoodLookup, Sources.Replace("\"kind\": \"gameKeys\"", "\"kind\": \"magic\"")))).Message);

    [Fact]
    public void Json_lookup_needs_known_asset() =>
        Assert.Contains("db", Assert.Throws<GeneratorPackageException>(() => Load(Json(GoodLookup, Sources.Replace("\"assets\": { \"db\": \"assets/db.json\" },", string.Empty)))).Message);

    [Fact]
    public void Game_lookup_needs_folder() =>
        Assert.Contains("folder", Assert.Throws<GeneratorPackageException>(() => Load(Json(GoodLookup, Sources.Replace("\"folder\": \"in_game/common/laws\", ", string.Empty)))).Message);

    [Fact]
    public void Package_without_lookups_has_none()
    {
        _temp.Write("q/package.json", GeneratorPackageLoaderTests.ValidJson);
        _temp.Write("q/templates/own.sbn", "x");
        var package = GeneratorPackageLoader.Load(_temp.PathOf("q"));

        Assert.Null(package.Lookups);
        Assert.Null(package.Root!.Child!.Fields[0].Lookup);
    }
}
