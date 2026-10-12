using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Abschnitt <c>candidates</c> in package.json: Aufbau und Prüfregeln.</summary>
public sealed class CandidatesLoaderTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private const string Section = """
        "candidates": {
          "category": "country",
          "used": [ { "file": "^in_game/common/static_modifiers/.*[.]txt$", "block": "^iv2_.*$", "except": [ "game_data" ] } ],
          "groups": [ { "id": "goodsOutput", "label": { "de": "Warenausstoß", "en": "Goods output" }, "pattern": "(^|_)(output|production)(_|$)", "hidden": true } ],
          "create": { "ask": [ "category", "subTab" ], "name": "name", "modifiers": "levels.modifiers" }
        }
        """;

    private const string Head = """
        [ { "id": "category", "label": "K", "type": "choice", "choices": [ "a", "b" ] },
          { "id": "number", "label": "N", "type": "number", "autoNumber": true },
          { "id": "subTab", "label": "S", "type": "choice", "choices": [ "", "a_1" ] },
          { "id": "name", "label": "Name", "type": "text", "multilingual": true, "required": true } ]
        """;

    private GeneratorPackage Load(string section = Section, string head = Head)
    {
        _temp.Write("pkg/package.json", $$$"""
            { "id": "pk", "name": "PK", "gameId": "eu5", "keyFields": [ "category", "number" ], "head": {{{head}}},
              "root": { "id": "levels", "label": "Stufen", "min": 1, "max": 4, "initial": 4,
                        "fields": [ { "id": "modifiers", "label": "M", "type": "modifiers" }, { "id": "expensive", "label": "E", "type": "bool" } ] },
              "outputs": [ { "id": "o", "kind": "ownFile", "path": "x.txt", "template": "o.sbn" } ],
              {{{section}}} }
            """);
        _temp.Write("pkg/templates/o.sbn", "x");
        return GeneratorPackageLoader.Load(_temp.PathOf("pkg"), uiLanguage: "en");
    }

    [Fact]
    public void Candidates_section_loads()
    {
        var candidates = Load().Candidates!;

        Assert.Equal("country", candidates.Category);
        var used = Assert.Single(candidates.Used);
        Assert.Equal("^in_game/common/static_modifiers/.*[.]txt$", used.File);
        Assert.Equal("^iv2_.*$", used.Block);
        Assert.Equal(new[] { "game_data" }, used.Except);
        var group = Assert.Single(candidates.Groups);
        Assert.Equal("goodsOutput", group.Id);
        Assert.Equal("Goods output", group.Label);
        Assert.True(group.Hidden);
        Assert.Equal(new[] { "category", "subTab" }, candidates.Create.Ask);
        Assert.Equal("name", candidates.Create.Name);
        Assert.Equal("levels.modifiers", candidates.Create.Modifiers);
    }

    [Fact]
    public void Candidates_without_groups_loads_with_empty_list()
    {
        var withoutGroups = Section[..Section.IndexOf("\"groups\"", StringComparison.Ordinal)] + Section[Section.IndexOf("\"create\"", StringComparison.Ordinal)..];

        var candidates = Load(withoutGroups).Candidates!;

        Assert.Empty(candidates.Groups);
        Assert.Equal("name", candidates.Create.Name);
    }

    [Fact]
    public void Candidates_without_autoNumber_does_not_load()
    {
        var head = Head.Replace(", \"autoNumber\": true", string.Empty);

        Assert.Throws<GeneratorPackageException>(() => Load(head: head));
    }

    [Fact]
    public void Candidates_ask_must_cover_other_key_fields()
    {
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("[ \"category\", \"subTab\" ]", "[ \"subTab\" ]")));
    }

    [Fact]
    public void Candidates_ask_must_be_choice_head_fields_and_not_the_autoNumber_field()
    {
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("[ \"category\", \"subTab\" ]", "[ \"category\", \"name\" ]")));
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("[ \"category\", \"subTab\" ]", "[ \"category\", \"number\" ]")));
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("[ \"category\", \"subTab\" ]", "[ \"category\", \"expensive\" ]")));
    }

    [Fact]
    public void Candidates_name_must_be_a_text_head_field()
    {
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("\"name\": \"name\"", "\"name\": \"subTab\"")));
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("\"name\": \"name\"", "\"name\": \"nothing\"")));
    }

    [Fact]
    public void Candidates_modifiers_must_be_a_modifiers_field()
    {
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("levels.modifiers", "levels.expensive")));
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("levels.modifiers", "other.modifiers")));
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("levels.modifiers", "modifiers")));
    }

    [Fact]
    public void Candidates_used_must_not_be_empty()
    {
        var empty = Section[..Section.IndexOf("\"used\"", StringComparison.Ordinal)] + "\"used\": [], " + Section[Section.IndexOf("\"groups\"", StringComparison.Ordinal)..];

        Assert.Throws<GeneratorPackageException>(() => Load(empty));
    }

    [Fact]
    public void Candidates_bad_regex_does_not_load()
    {
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("(^|_)(output|production)(_|$)", "(")));
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("\"^iv2_.*$\"", "\"(\"")));
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("[.]txt$", "(")));
    }

    [Fact]
    public void Candidates_duplicate_or_empty_group_id_does_not_load()
    {
        var group = Section[Section.IndexOf("{ \"id\": \"goodsOutput\"", StringComparison.Ordinal)..(Section.IndexOf("\"hidden\": true }", StringComparison.Ordinal) + "\"hidden\": true }".Length)];

        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace(group, group + ", " + group)));
        Assert.Throws<GeneratorPackageException>(() => Load(Section.Replace("\"id\": \"goodsOutput\"", "\"id\": \"\"")));
    }

    [Fact]
    public void Package_without_candidates_has_null()
    {
        Assert.Null(Load(section: "\"targets\": [ \"mod\" ]").Candidates);
    }
}
