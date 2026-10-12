using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>
/// Einleser-Ergänzungen für das Paket "iv2-dynamic-ideas": <c>read.json</c> (Werte aus einer externen JSON-Datei), Adressierung von Elementen der
/// zählbaren obersten Ebene allein über die Endung, <c>body: "."</c> für <c>read.values</c>.
/// </summary>
public sealed class ReadJsonTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private const string Json = """
        {
          "id": "rj", "name": "RJ", "gameId": "eu5", "keyFields": [ "category", "number" ],
          "head": [
            { "id": "category", "label": "K", "type": "choice", "choices": [ "army", "navy" ] },
            { "id": "number", "label": "N", "type": "number" },
            { "id": "aiTags", "label": "T", "type": "multilineText" }
          ],
          "root": { "id": "levels", "label": "S", "min": 1, "max": 4, "fields": [ { "id": "modifiers", "label": "M", "type": "modifiers" } ] },
          "externals": { "aiTags": { "label": "KI-Tags" } },
          "outputs": [ { "id": "m", "kind": "ownFile", "path": "m_{{ category }}.txt", "template": "n.sbn" } ],
          "read": {
            "keys": { "file": "^m_(army|navy)\\.txt$", "block": "m_(?<category>army|navy)_(?<number>\\d+)" },
            "elements": { "file": "m_{{ category }}.txt", "block": "m_{{ category }}_{{ number }}(?<suffix>_\\d)?", "level": "levels",
                          "fields": { "modifiers": { "body": true } } },
            JSON_ENTRIES
          }
        }
        """;

    private const string JsonEntries = """
        "json": [ { "external": "aiTags", "jsonPath": "entries", "entryKey": "nat:{{ category }}_{{ number }}", "property": "tags", "field": "aiTags", "join": "\n" } ]
        """;

    private GeneratorPackage Load(string entries = JsonEntries)
    {
        _temp.Write("pkg/package.json", Json.Replace("JSON_ENTRIES", entries));
        _temp.Write("pkg/templates/n.sbn", "x\n");
        return GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
    }

    private string Mod => _temp.PathOf("mod");

    private static readonly string[] Languages = { "english" };

    private Dictionary<string, string> Paths(string tagFile) => new() { ["aiTags"] = _temp.Write("tags/iv2_ai_tags.json", tagFile) };

    [Fact]
    public void Read_json_joins_array_into_field()
    {
        _temp.Write("mod/m_army.txt", "m_army_1 = { a = 1 }\n");
        var paths = Paths("""{ "entries": { "nat:army_1": { "tags": ["ai_balanced","ai_defensive"], "name": "X" } } }""");

        var result = GeneratorStateReader.Read(Load(), Mod, "army_1", Languages, paths)!;

        Assert.Equal("ai_balanced\nai_defensive", result.State.Head.Values["aiTags"]);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Read_json_takes_a_string_as_it_is()
    {
        _temp.Write("mod/m_army.txt", "m_army_1 = { a = 1 }\n");
        var paths = Paths("""{ "entries": { "nat:army_1": { "tags": "ai_balanced" } } }""");

        var result = GeneratorStateReader.Read(Load(), Mod, "army_1", Languages, paths)!;

        Assert.Equal("ai_balanced", result.State.Head.Values["aiTags"]);
    }

    [Fact]
    public void Read_json_is_passed_through_by_take_over()
    {
        _temp.Write("mod/m_army.txt", "m_army_1 = { a = 1 }\n");
        var paths = Paths("""{ "entries": { "nat:army_1": { "tags": ["ai_balanced"] } } }""");

        var result = GeneratorStateReader.TakeOver(Load(), Mod, new[] { "army_1" }, null, Languages, paths);

        Assert.Equal("ai_balanced", Assert.Single(result.States).Head.Values["aiTags"]);
    }

    [Fact]
    public void Read_json_missing_path_or_entry_keeps_field_and_notes()
    {
        _temp.Write("mod/m_army.txt", "m_army_1 = { a = 1 }\n");
        var package = Load();

        // kein Pfad gewählt
        var none = GeneratorStateReader.Read(package, Mod, "army_1", Languages)!;
        Assert.Contains(GeneratorsTexts.ReadJsonNoFile("KI-Tags"), none.Notes);

        // Datei fehlt
        var gone = GeneratorStateReader.Read(package, Mod, "army_1", Languages, new Dictionary<string, string> { ["aiTags"] = _temp.PathOf("tags/weg.json") })!;
        Assert.Contains(GeneratorsTexts.ReadJsonNoFile("KI-Tags"), gone.Notes);

        // Eintrag fehlt
        var noEntry = GeneratorStateReader.Read(package, Mod, "army_1", Languages, Paths("""{ "entries": { "nat:army_9": { "tags": ["x"] } } }"""))!;
        Assert.Contains(GeneratorsTexts.ReadJsonNoEntry("KI-Tags", "nat:army_1"), noEntry.Notes);

        // Eigenschaft fehlt
        var noProperty = GeneratorStateReader.Read(package, Mod, "army_1", Languages, Paths("""{ "entries": { "nat:army_1": { "name": "X" } } }"""))!;
        Assert.Contains(GeneratorsTexts.ReadJsonNoProperty("KI-Tags", "nat:army_1", "tags"), noProperty.Notes);

        // Pfad fehlt
        var noPath = GeneratorStateReader.Read(package, Mod, "army_1", Languages, Paths("""{ "other": {} }"""))!;
        Assert.Contains(GeneratorsTexts.ReadJsonNoEntry("KI-Tags", "nat:army_1"), noPath.Notes);

        foreach (var result in new[] { none, gone, noEntry, noProperty, noPath })
        {
            Assert.True(string.IsNullOrEmpty(result.State.Head.Values.GetValueOrDefault("aiTags")));
        }
    }

    [Fact]
    public void Read_json_invalid_json_adds_note_without_throwing()
    {
        _temp.Write("mod/m_army.txt", "m_army_1 = { a = 1 }\n");

        var result = GeneratorStateReader.Read(Load(), Mod, "army_1", Languages, Paths("{ nope"))!;

        Assert.Contains(result.Notes, n => n.StartsWith(GeneratorsTexts.ReadJsonInvalid("KI-Tags", string.Empty), StringComparison.Ordinal));
    }

    [Fact]
    public void Loader_reads_read_json_entries()
    {
        var json = Assert.Single(Load().Read!.Json!);

        Assert.Equal(new ReadJsonDef("aiTags", "entries", "nat:{{ category }}_{{ number }}", "tags", "aiTags", "\n"), json);
    }

    [Fact]
    public void Loader_requires_a_package_external_for_read_json()
    {
        var error = Assert.Throws<GeneratorPackageException>(() => Load(JsonEntries.Replace("\"external\": \"aiTags\"", "\"external\": \"weg\"")));

        Assert.Contains("read.json.external", error.Message, StringComparison.Ordinal);
    }

    private const string CountableJson = """
        {
          "id": "sx", "name": "SX", "gameId": "eu5", "keyField": "tag",
          "head": [ { "id": "tag", "label": "Tag", "type": "text" } ],
          "root": { "id": "levels", "label": "S", "min": 1, "max": 4, "fields": [
            { "id": "modifiers", "label": "M", "type": "modifiers" }, { "id": "expensive", "label": "E", "type": "bool" }, { "id": "extra", "label": "X", "type": "text" } ] },
          "outputs": [ { "id": "m", "kind": "ownFile", "path": "m_{{ tag }}.txt", "template": "n.sbn" } ],
          "read": {
            "keys": "^m_(?<key>[A-Z]+)\\.txt$",
            "elements": { "file": "m_{{ tag }}.txt", "block": "m_{{ tag }}(?<suffix>_\\d)?", "level": "levels", "fields": { "modifiers": { "body": true } } },
            "flags": [ { "file": "ga_{{ tag }}.txt", "block": "ga_{{ tag }}(?<suffix>_\\d)?", "contains": "_expensive_", "field": "levels.expensive" } ],
            "values": [ { "file": "ga_{{ tag }}.txt", "block": "ga_{{ tag }}(?<suffix>_\\d)?", VALUE_SOURCE, "field": "levels.extra" } ]
          }
        }
        """;

    private GeneratorPackage LoadCountable(string valueSource = "\"pattern\": \"x = (?<value>\\\\w+)\"")
    {
        _temp.Write("pkg/package.json", CountableJson.Replace("VALUE_SOURCE", valueSource));
        _temp.Write("pkg/templates/n.sbn", "x\n");
        return GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
    }

    [Fact]
    public void Suffix_only_addresses_countable_root_element()
    {
        _temp.Write("mod/m_X.txt", "m_X = { a = 1 }\nm_X_1 = { b = 2 }\nm_X_3 = { d = 4 }\n");

        var state = GeneratorStateReader.Read(LoadCountable(), Mod, "X", Languages)!.State;

        var levels = state.Head.Children["levels"];
        Assert.Equal(4, levels.Count);
        Assert.Equal(new[] { "a = 1", "b = 2", string.Empty, "d = 4" }, levels.Select(l => l.Values.GetValueOrDefault("modifiers") ?? string.Empty));
    }

    [Fact]
    public void Suffix_only_is_passed_through_for_flags_and_values_with_level_field()
    {
        _temp.Write("mod/m_X.txt", "m_X = { a = 1 }\nm_X_1 = { b = 2 }\n");
        _temp.Write("mod/ga_X.txt", "ga_X = { y = 1 }\nga_X_1 = { _expensive_ x = foo }\n");

        var state = GeneratorStateReader.Read(LoadCountable(), Mod, "X", Languages)!.State;

        var levels = state.Head.Children["levels"];
        Assert.Equal(new[] { "no", "yes" }, levels.Select(l => l.Values.GetValueOrDefault("expensive") ?? "no"));
        Assert.Equal("foo", levels[1].Values["extra"]);
    }

    [Fact]
    public void Without_number_and_suffix_groups_the_element_stays_unaddressed()
    {
        _temp.Write("mod/m_X.txt", "m_X = { a = 1 }\n");
        var package = LoadCountable();
        package = package with { Read = package.Read! with { Elements = package.Read!.Elements with { Block = "m_{{ tag }}" } } };

        var result = GeneratorStateReader.Read(package, Mod, "X", Languages)!;

        Assert.Contains(GeneratorsTexts.ReadNumberOutside(-1), result.Notes);
    }

    [Fact]
    public void Values_body_dot_takes_the_whole_block_content_dedented()
    {
        _temp.Write("mod/m_X.txt", "m_X = { a = 1 }\n");
        _temp.Write("mod/ga_X.txt", "ga_X = {\n\tfoo = {\n\t\tbar = 1\n\t}\n\tbaz = 2\n}\n");

        var state = GeneratorStateReader.Read(LoadCountable("\"body\": \".\""), Mod, "X", Languages)!.State;

        Assert.Equal("foo = {\n\tbar = 1\n}\nbaz = 2", state.Head.Children["levels"][0].Values["extra"]);
    }
}
