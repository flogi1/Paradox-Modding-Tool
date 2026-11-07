using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Feldoption <c>suggest</c>: Vorschlagsliste eines Mehrzeilen-Felds aus einer externen JSON-Datei.</summary>
public sealed class FieldSuggestTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private const string Json = """
        {
          "id": "fs", "name": "FS", "gameId": "eu5", "keyFields": [ "number" ],
          "head": [
            { "id": "number", "label": "N", "type": "number" },
            { "id": "aiTags", "label": "T", "type": "multilineText",
              "suggest": { "external": "aiTags", "jsonPath": "tags", "labelKey": "label" } }
          ],
          "root": { "id": "levels", "label": "S", "min": 1, "max": 1, "fields": [ { "id": "modifiers", "label": "M", "type": "modifiers" } ] },
          "externals": { "aiTags": { "label": "KI-Tags" } },
          "outputs": [ { "id": "m", "kind": "ownFile", "path": "m.txt", "template": "n.sbn" } ]
        }
        """;

    private FieldSuggest Suggest()
    {
        _temp.Write("pkg/package.json", Json);
        _temp.Write("pkg/templates/n.sbn", "x\n");
        var package = GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
        return package.Head.Single(f => f.Id == "aiTags").Suggest!;
    }

    private Dictionary<string, string> Paths(string content) => new() { ["aiTags"] = _temp.Write("tags.json", content) };

    [Fact]
    public void Loader_reads_suggest_with_defaults()
    {
        var suggest = Suggest();

        Assert.Equal("aiTags", suggest.External);
        Assert.Equal("tags", suggest.JsonPath);
        Assert.Equal("id", suggest.IdKey);
        Assert.Equal("label", suggest.LabelKey);
    }

    [Fact]
    public void Load_returns_ids_and_labels_without_duplicates_or_idless_entries()
    {
        var paths = Paths("""{ "tags": [ { "id": "ai_a", "label": "A" }, { "label": "no id" }, { "id": "ai_a", "label": "dup" }, { "id": "ai_b" }, "ai_c" ] }""");

        var items = FieldSuggestions.Load(Suggest(), paths);

        Assert.Equal(new[] { ("ai_a", "A"), ("ai_b", ""), ("ai_c", "") }, items);
    }

    [Fact]
    public void Load_is_empty_without_file_or_with_broken_json_or_missing_array()
    {
        var suggest = Suggest();

        Assert.Empty(FieldSuggestions.Load(suggest, new Dictionary<string, string>()));
        Assert.Empty(FieldSuggestions.Load(suggest, new Dictionary<string, string> { ["aiTags"] = _temp.PathOf("missing.json") }));
        Assert.Empty(FieldSuggestions.Load(suggest, Paths("{ not json")));
        Assert.Empty(FieldSuggestions.Load(suggest, Paths("""{ "entries": {} }""")));
    }

    [Fact]
    public void Dynamic_ideas_package_suggests_tags_from_the_tag_file()
    {
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-dynamic-ideas"));
        var suggest = package.Head.Single(f => f.Id == "aiTags").Suggest!;

        var items = FieldSuggestions.Load(suggest, Paths("""{ "tags": [ { "id": "ai_balanced", "label": "Ausgeglichen" } ], "entries": {} }"""));

        Assert.Equal(new[] { ("ai_balanced", "Ausgeglichen") }, items);
    }
}
