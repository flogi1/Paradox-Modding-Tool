using System.Text;
using System.Text.Json.Nodes;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Der Paket-Editor bearbeitet package.json als JSON-Baum (Spec Teil 5a, Abschnitt 1).</summary>
public sealed partial class PackageDocumentTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    /// <summary>Kopie des mitgelieferten IV2-Pakets im Temp-Ordner.</summary>
    private string Iv2Copy()
    {
        var source = Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2");
        var target = _temp.PathOf("iv2");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }

        return target;
    }

    private static JsonNode Json(string folder) => JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "package.json")))!;

    private string Minimal(string extra = "", string outputs = "[]")
    {
        _temp.Write("p/package.json", "{ \"id\": \"p\", \"name\": \"P\", \"gameId\": \"eu5\", \"keyField\": \"tag\", " + extra +
            "\"head\": [ { \"id\": \"tag\", \"label\": \"Tag\", \"type\": \"text\" } ], \"outputs\": " + outputs + " }");
        return _temp.PathOf("p");
    }

    [Fact]
    public void Iv2_round_trip_without_edits_is_equal()
    {
        var folder = Iv2Copy();
        var before = Json(folder);

        PackageDocument.Load(folder).Save();

        Assert.True(JsonNode.DeepEquals(before, Json(folder)));
    }

    [Fact]
    public void LocalizedText_single_language_writes_string()
    {
        var text = LocalizedText.From(JsonValue.Create("Tag"));

        Assert.Equal("Tag", text.ToNode()!.GetValue<string>());
        var two = text.With("de", "Kürzel").ToNode()!.AsObject();
        Assert.Equal("Tag", two["en"]!.GetValue<string>());
        Assert.Equal("Kürzel", two["de"]!.GetValue<string>());
        Assert.Equal(2, two.Count);
    }

    [Fact]
    public void LocalizedText_removing_last_language_gives_null()
    {
        var text = LocalizedText.From(JsonNode.Parse("{ \"de\": \"A\" }"));

        Assert.Null(text.With("de", string.Empty).ToNode());
    }

    [Fact]
    public void LocalizedText_of_languages_keeps_plain_text_plain()
    {
        var plain = LocalizedText.Plain("Tag");

        Assert.Same(plain, plain.WithLanguages(new Dictionary<string, string> { ["en"] = "Tag" }));
        var map = plain.WithLanguages(new Dictionary<string, string> { ["en"] = "Tag", ["de"] = "Kürzel" }).ToNode()!.AsObject();
        Assert.Equal(new[] { "en", "de" }, map.Select(p => p.Key));
        Assert.Null(plain.WithLanguages(new Dictionary<string, string>()).ToNode());
    }

    [Fact]
    public void Edit_clearing_then_typing_keeps_other_languages()
    {
        var editor = new LocalizedTextEditor(LocalizedText.From(JsonNode.Parse("{ \"en\": \"Tag\", \"de\": \"Kürzel\" }")), "de");

        editor.Type(string.Empty);
        var value = editor.Type("N");

        Assert.Equal("Tag", value.Values["en"]);
        Assert.Equal("N", value.Values["de"]);
        Assert.Equal(2, value.Values.Count);
    }

    [Fact]
    public void Cell_clearing_then_typing_keeps_other_languages()
    {
        var cells = new LocalizedCellLanguages();
        var row = new object();
        var value = LocalizedText.From(JsonNode.Parse("{ \"en\": \"Tag\", \"de\": \"Kürzel\" }"));

        value = cells.Type(row, "label", value, string.Empty, "de");
        value = cells.Type(row, "label", value, "N", "de");

        Assert.Equal("Tag", value.Values["en"]);
        Assert.Equal("N", value.Values["de"]);
        Assert.Equal(2, value.Values.Count);
    }

    [Fact]
    public void Cell_with_only_one_language_stays_multilingual_after_clearing()
    {
        var cells = new LocalizedCellLanguages();
        var row = new object();
        var cleared = cells.Type(row, "label", LocalizedText.From(JsonNode.Parse("{ \"de\": \"Kürzel\" }")), string.Empty, "de");

        Assert.Null(cleared.ToNode());
        Assert.True(cells.IsMultilingual(row, "label"));
        Assert.False(cells.IsMultilingual(row, "desc"));
        // Das Gitter speichert eine ganz geleerte Zelle als "" (nicht null).
        var typed = cells.Type(row, "label", LocalizedText.From(JsonValue.Create(string.Empty)), "N", "de");
        Assert.Equal("N", typed.Values["de"]);
        Assert.Single(typed.Values);
    }

    [Fact]
    public void Cell_languages_are_per_row_and_column()
    {
        var cells = new LocalizedCellLanguages();
        var first = new object();
        var second = new object();
        var german = LocalizedText.From(JsonNode.Parse("{ \"en\": \"A\", \"de\": \"B\" }"));
        var english = LocalizedText.From(JsonNode.Parse("{ \"en\": \"C\" }"));

        cells.Type(first, "label", german, string.Empty, "de");

        Assert.Equal("D", cells.Type(second, "label", english, "D", "de").Values["en"]);
        Assert.Equal("E", cells.Type(first, "desc", english, "E", "de").Values["en"]);
    }

    [Fact]
    public void Edit_of_plain_text_stays_plain_and_new_text_starts_plain()
    {
        var plain = new LocalizedTextEditor(LocalizedText.Plain("Tag"), "de");
        Assert.Equal("Neu", plain.Type("Neu").ToNode()!.GetValue<string>());

        var empty = new LocalizedTextEditor(LocalizedText.Empty, "de");
        empty.Type(string.Empty);
        Assert.Equal("X", empty.Type("X").ToNode()!.GetValue<string>());
    }

    [Fact]
    public void LocalizedText_resolves_like_the_loader()
    {
        var text = LocalizedText.From(JsonNode.Parse("{ \"fr\": \"F\", \"en\": \"E\" }"));

        Assert.Equal("F", text.Resolve("fr"));
        Assert.Equal("E", text.Resolve("de"));
        Assert.Equal("Tag", LocalizedText.From(JsonValue.Create("Tag")).Resolve("de"));
    }

    [Fact]
    public void Basics_edit_writes_json()
    {
        var folder = Minimal();
        var document = PackageDocument.Load(folder);
        var changed = 0;
        document.Changed += () => changed++;

        document.Name = LocalizedText.From(JsonValue.Create("Neu"));
        document.Targets = new[] { "output" };
        document.Languages = new LanguageDefaults(CopyEnglish: true);
        Assert.True(document.IsDirty);
        document.Save();

        Assert.False(document.IsDirty);
        Assert.Equal(3, changed);
        var again = PackageDocument.Load(folder);
        Assert.Equal("Neu", again.Name.Resolve("de"));
        Assert.Equal(new[] { "output" }, again.Targets);
        Assert.Equal(new LanguageDefaults(CopyEnglish: true), again.Languages);
        Assert.Equal("tag", again.KeyField);
        Assert.Equal("eu5", again.GameId);
        Assert.NotNull(GeneratorPackageLoader.Load(folder));
    }

    [Fact]
    public void Assets_written_in_object_form()
    {
        var folder = Minimal("\"assets\": { \"a\": \"assets/a.zip\" }, ");
        var document = PackageDocument.Load(folder);
        Assert.Equal(new AssetDef("assets/a.zip", AssetOrigin.Game), document.Assets.Single().Asset);

        document.Assets = new[] { ("a", new AssetDef("assets/a.zip", AssetOrigin.Game)), ("b", new AssetDef("assets/b.zip", AssetOrigin.Mod)) };
        document.Save();

        var assets = Json(folder)["assets"]!.AsObject();
        Assert.Equal("game", assets["a"]!["origin"]!.GetValue<string>());
        Assert.Equal("mod", assets["b"]!["origin"]!.GetValue<string>());
        Assert.Equal(AssetOrigin.Mod, GeneratorPackageLoader.Load(folder).AssetDefs["b"].Origin);
    }

    [Fact]
    public void Warnings_and_errors_round_trip()
    {
        var folder = Minimal("\"warnings\": [ { \"when\": \"a\", \"message\": { \"de\": \"A\", \"en\": \"B\" } } ], ");
        var document = PackageDocument.Load(folder);

        Assert.Equal("B", document.Warnings.Single().Message.Resolve("en"));
        document.Errors = new[] { ("x == 0", LocalizedText.From(JsonValue.Create("Leer"))) };
        document.Save();

        var package = GeneratorPackageLoader.Load(folder, uiLanguage: "de");
        Assert.Equal("A", package.Warnings.Single().Message);
        Assert.Equal(new WarningDef("x == 0", "Leer"), package.Errors.Single());
    }

    [Fact]
    public void Document_reports_comments()
    {
        _temp.Write("c/package.json", "{\n  // Kommentar\n  \"id\": \"c\", \"name\": \"C\",\n}");

        var document = PackageDocument.Load(_temp.PathOf("c"));

        Assert.True(document.HadComments);
        Assert.Equal("c", document.Id);
        Assert.False(PackageDocument.Load(Minimal()).HadComments);
    }

    [Fact]
    public void Unknown_keys_survive_edits()
    {
        var folder = Minimal("\"zzz\": { \"a\": 1 }, \"read\": { \"keys\": \"x\" }, \"lookups\": [ { \"id\": \"l\" } ], ");
        var before = Json(folder);
        var document = PackageDocument.Load(folder);

        document.Name = LocalizedText.From(JsonValue.Create("Anders"));
        document.Save();

        var after = Json(folder);
        foreach (var key in new[] { "zzz", "read", "lookups" })
        {
            Assert.True(JsonNode.DeepEquals(before[key], after[key]), key);
        }
    }

    [Fact]
    public void Save_is_atomic_and_lf()
    {
        var folder = Minimal();
        var document = PackageDocument.Load(folder);
        document.Name = LocalizedText.From(JsonValue.Create("Ä"));

        document.Save();

        var bytes = File.ReadAllBytes(Path.Combine(folder, "package.json"));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("\r", text);
        Assert.Contains("\"Ä\"", text);
        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
    }

    [Fact]
    public void Invalid_json_is_a_package_exception()
    {
        _temp.Write("bad/package.json", "{ nope");

        Assert.Throws<GeneratorPackageException>(() => PackageDocument.Load(_temp.PathOf("bad")));
    }
}

/// <summary>Die Schlüssel der dynamischen IV2-Ideen (insertAfter, ifLimit, jsonEntry, autoNumber, externals, afterGenerate) im Paket-Editor.</summary>
public sealed class PackageDocumentNewKeysTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private const string Package = """
        {
          "id": "p", "name": "P", "gameId": "eu5", "keyFields": [ "tag", "n" ],
          "externals": { "tags": { "label": { "de": "Tag-Datei", "en": "Tag file" }, "hint": { "de": "Hinweis", "en": "Hint" } } },
          "afterGenerate": { "de": "Danach starten", "en": "Run afterwards" },
          "head": [ { "id": "tag", "type": "text", "pattern": "^[A-Z]{3}$" }, { "id": "n", "type": "number", "autoNumber": true } ],
          "outputs": [
            { "id": "sm", "kind": "syncMatching", "path": "x.txt", "template": "a.sbn", "match": "m_{{ tag }}", "insertAfter": "^z_{{ tag }}", "ifLimit": "has_flag = {{ tag }}" },
            { "id": "loc", "kind": "localization", "path": "l/{{ lang }}/x_l_{{ lang }}.yml", "template": "a.sbn", "keys": "^k_{{ tag }}", "insertAfter": "^j_{{ tag }}" },
            { "id": "je", "kind": "jsonEntry", "path": "", "external": "tags", "jsonPath": "entries", "entryKey": "{{ tag }}", "template": "a.sbn" }
          ]
        }
        """;

    private string Folder()
    {
        _temp.Write("p/package.json", Package);
        _temp.Write("p/templates/a.sbn", "x = 1\n");
        return _temp.PathOf("p");
    }

    private static JsonNode Json(string folder) => JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "package.json")))!;

    [Fact]
    public void New_keys_survive_load_change_save_load()
    {
        var folder = Folder();
        var before = Json(folder);

        var document = PackageDocument.Load(folder);
        document.Name = LocalizedText.From(JsonValue.Create("Anders"));
        document.Save();
        var again = PackageDocument.Load(folder);

        var after = Json(folder);
        foreach (var key in new[] { "externals", "afterGenerate", "head", "outputs" })
        {
            Assert.True(JsonNode.DeepEquals(before[key], after[key]), key);
        }

        Assert.Equal("Anders", again.Name.Resolve("en"));
        Assert.True(again.Head.Items[1].AutoNumber);
        Assert.False(again.Head.Items[0].AutoNumber);
        var external = Assert.Single(again.Externals);
        Assert.Equal("tags", external.Id);
        Assert.Equal("Tag file", external.Label.Resolve("en"));
        Assert.Equal("Hinweis", external.Hint.Resolve("de"));
        Assert.Equal("Run afterwards", again.AfterGenerate.Resolve("en"));
        Assert.NotNull(GeneratorPackageLoader.Load(folder));
    }

    [Fact]
    public void Edited_new_keys_are_written_and_loadable()
    {
        var folder = Folder();
        var document = PackageDocument.Load(folder);

        document.Externals = new[] { ("tags", LocalizedText.Plain("Tags"), LocalizedText.Empty), ("more", LocalizedText.Plain("Mehr"), LocalizedText.Plain("Ort")) };
        document.AfterGenerate = LocalizedText.Empty;
        document.Head.Items[1].AutoNumber = false;
        document.Outputs.Single(o => o.Id == "sm").Set("ifLimit", null);
        document.Save();

        var json = Json(folder);
        Assert.Null(json["afterGenerate"]);
        Assert.Null(json["head"]![1]!["autoNumber"]);
        Assert.Null(json["outputs"]![0]!["ifLimit"]);
        Assert.Null(json["externals"]!["tags"]!["hint"]);
        Assert.Equal("Ort", json["externals"]!["more"]!["hint"]!.GetValue<string>());
        var package = GeneratorPackageLoader.Load(folder);
        Assert.Equal(2, package.Externals.Count);
        Assert.Null(package.AfterGenerate);
    }

    [Fact]
    public void Removing_all_externals_removes_the_key()
    {
        var document = PackageDocument.Load(Folder());

        document.Externals = Array.Empty<(string, LocalizedText, LocalizedText)>();

        Assert.Null(document.Json["externals"]);
    }

    [Fact]
    public void Renaming_an_external_moves_the_references_of_outputs()
    {
        var document = PackageDocument.Load(Folder());

        document.RenameExternalReferences("tags", "neu");

        Assert.Equal("neu", document.Outputs.Single(o => o.Id == "je").Get("external"));
        Assert.Null(document.Outputs.Single(o => o.Id == "sm").Raw["external"]);
    }

    [Fact]
    public void Kind_switch_drops_jsonEntry_keys()
    {
        var document = PackageDocument.Load(Folder());
        var output = document.Outputs.Single(o => o.Id == "je");

        output.Kind = OutputKind.OwnFile;

        Assert.Null(output.Raw["external"]);
        Assert.Null(output.Raw["jsonPath"]);
        Assert.Null(output.Raw["entryKey"]);
        Assert.Equal("a.sbn", output.Get("template"));
    }

    [Fact]
    public void Kind_switch_to_jsonEntry_keeps_the_template_and_empties_the_path()
    {
        var document = PackageDocument.Load(Folder());
        var output = document.Outputs.Single(o => o.Id == "sm");

        output.Kind = OutputKind.JsonEntry;

        Assert.Equal("a.sbn", output.Get("template"));
        Assert.Equal(string.Empty, output.Raw["path"]!.GetValue<string>());
        Assert.Null(output.Raw["match"]);
        Assert.Null(output.Raw["insertAfter"]);
        Assert.Null(output.Raw["ifLimit"]);
        Assert.Equal(new[] { "external", "jsonPath", "entryKey", "template" }, OutputNode.KeysOf(OutputKind.JsonEntry));
    }

    [Fact]
    public void Kind_switch_to_syncMatching_keeps_forEach() // shipped iv2-idea-groups uses forEach on syncMatching outputs
    {
        var document = PackageDocument.Load(Folder());
        var output = document.AddOutput("je_element", OutputKind.InsertSection);
        output.Set("forEach", "items");

        output.Kind = OutputKind.SyncMatching;

        Assert.Equal("items", output.Get("forEach"));
        Assert.Contains("forEach", OutputNode.KeysOf(OutputKind.SyncMatching));
    }

    [Fact]
    public void Kind_switch_keeps_insertAfter_only_where_the_kind_knows_it()
    {
        var document = PackageDocument.Load(Folder());
        var output = document.Outputs.Single(o => o.Id == "sm");

        output.Kind = OutputKind.Localization;
        Assert.Equal("^z_{{ tag }}", output.Get("insertAfter"));
        Assert.Null(output.Raw["ifLimit"]);

        output.Kind = OutputKind.OwnFile;
        Assert.Null(output.Raw["insertAfter"]);
    }

    [Fact]
    public void Number_field_changed_to_text_drops_autoNumber()
    {
        var document = PackageDocument.Load(Folder());
        var field = document.Head.Items[1];

        field.Type = FieldType.Text;

        Assert.Null(field.Raw["autoNumber"]);
    }

    [Fact]
    public void Idea_groups_copy_with_insertAfter_round_trips_and_still_loads()
    {
        var source = Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-idea-groups");
        var target = _temp.PathOf("copy");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }

        var document = PackageDocument.Load(target);
        document.Outputs.Single(o => o.Id == "ai_ideas").Set("insertAfter", "^iv2_ga_ideagroup_{{ gm }}");
        document.Save();

        var again = PackageDocument.Load(target);
        Assert.Equal("^iv2_ga_ideagroup_{{ gm }}", again.Outputs.Single(o => o.Id == "ai_ideas").Get("insertAfter"));
        var package = GeneratorPackageLoader.Load(target);
        Assert.Equal("^iv2_ga_ideagroup_{{ gm }}", package.Outputs.Single(o => o.Id == "ai_ideas").InsertAfter);
    }

    [Fact]
    public void Check_finds_a_bad_insertAfter_regex_and_a_typo_in_ifLimit()
    {
        var folder = Folder();
        var document = PackageDocument.Load(folder);
        var output = document.Outputs.Single(o => o.Id == "sm");
        output.Set("insertAfter", "^z_({{ tag }}");
        output.Set("ifLimit", "has_flag = {{ tagg }}");

        var issues = PackageChecker.Check(document, new PackageFiles(folder), "en");

        Assert.Contains(issues, i => i.Where == "outputs[sm].insertAfter");
        Assert.Contains(issues, i => i.Where == "outputs[sm].ifLimit");
    }

    [Fact]
    public void Check_finds_a_syntax_error_in_insertAfter_and_ifLimit_templates()
    {
        var folder = Folder();
        var document = PackageDocument.Load(folder);
        var output = document.Outputs.Single(o => o.Id == "sm");
        output.Set("insertAfter", "{{ if }}");
        output.Set("ifLimit", "{{ for }}");

        var issues = PackageChecker.Check(document, new PackageFiles(folder), "en");

        Assert.Contains(issues, i => i.Where == "outputs[sm].insertAfter");
        Assert.Contains(issues, i => i.Where == "outputs[sm].ifLimit");
    }

    [Fact]
    public void Check_accepts_the_valid_package()
    {
        var folder = Folder();

        Assert.Empty(PackageChecker.Check(PackageDocument.Load(folder), new PackageFiles(folder), "en"));
    }

    /// <summary>Kopie des mitgelieferten Pakets iv2-dynamic-ideas (mit <c>candidates</c>) im Temp-Ordner.</summary>
    private string DynamicIdeasCopy(string extraKeyInCandidates = "")
    {
        var source = Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-dynamic-ideas");
        var target = _temp.PathOf("dyn");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }

        if (extraKeyInCandidates.Length > 0)
        {
            var root = (JsonObject)Json(target);
            ((JsonObject)root["candidates"]!)["x"] = 1;
            ((JsonObject)((JsonObject)root["candidates"]!)["create"]!)["y"] = "z";
            ((JsonObject)((JsonArray)((JsonObject)root["candidates"]!)["groups"]!)[0]!)["w"] = true;
            File.WriteAllText(Path.Combine(target, "package.json"), root.ToJsonString());
        }

        return target;
    }

    [Fact]
    public void Candidates_round_trip_keeps_unknown_keys()
    {
        var folder = DynamicIdeasCopy("x");
        var before = Json(folder);
        var document = PackageDocument.Load(folder);

        var candidates = document.Candidates;
        Assert.NotNull(candidates);
        Assert.Equal("country", candidates!.Category);
        Assert.Equal(12, candidates.Groups.Count);
        Assert.Equal("game_data", Assert.Single(candidates.Used).Except);
        Assert.Equal("category, subTab", candidates.Ask);
        Assert.Equal("levels.modifiers", candidates.Modifiers);
        Assert.Equal("Output of single goods", candidates.Groups[0].Label.Resolve("en"));
        Assert.True(candidates.Groups[0].Hidden);
        document.Candidates = candidates;
        document.Save();

        var after = Json(folder);
        Assert.True(JsonNode.DeepEquals(before["candidates"], after["candidates"]));
        Assert.Equal(1, after["candidates"]!["x"]!.GetValue<int>());
        Assert.Equal("z", after["candidates"]!["create"]!["y"]!.GetValue<string>());
        Assert.True(after["candidates"]!["groups"]![0]!["w"]!.GetValue<bool>());
        Assert.NotNull(GeneratorPackageLoader.Load(folder));
    }

    [Fact]
    public void Candidates_group_order_survives_set()
    {
        var folder = DynamicIdeasCopy("x");
        var document = PackageDocument.Load(folder);
        var candidates = document.Candidates!;
        var groups = candidates.Groups.ToList();
        (groups[2], groups[3]) = (groups[3], groups[2]);

        document.Candidates = candidates with { Groups = groups };
        document.Save();

        var array = (JsonArray)Json(folder)["candidates"]!["groups"]!;
        Assert.Equal("ai", array[2]!["id"]!.GetValue<string>());
        Assert.Equal("modOwn", array[3]!["id"]!.GetValue<string>());
        Assert.Equal(12, array.Count);
        Assert.Equal(groups.Select(g => g.Id), PackageDocument.Load(folder).Candidates!.Groups.Select(g => g.Id));
    }

    [Fact]
    public void Candidates_edited_values_are_written_and_loadable()
    {
        var folder = DynamicIdeasCopy();
        var document = PackageDocument.Load(folder);
        var candidates = document.Candidates!;

        document.Candidates = candidates with
        {
            Category = "country",
            Used = new[] { (candidates.Used[0].File, candidates.Used[0].Block, "game_data, other") },
            Groups = candidates.Groups.Take(1).Select(g => (g.Id, LocalizedText.Plain("Neu"), g.Pattern, false)).ToList(),
            Ask = "category",
        };
        document.Save();

        var json = Json(folder)["candidates"]!;
        Assert.Equal(new[] { "game_data", "other" }, json["used"]![0]!["except"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal("Neu", json["groups"]![0]!["label"]!.GetValue<string>());
        Assert.False(json["groups"]![0]!["hidden"]!.GetValue<bool>());
        Assert.Single(json["groups"]!.AsArray());
        Assert.Equal(new[] { "category" }, json["create"]!["ask"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public void Candidates_null_removes_section()
    {
        var folder = DynamicIdeasCopy();
        var document = PackageDocument.Load(folder);

        document.Candidates = null;
        document.Save();

        Assert.Null(document.Candidates);
        Assert.Null(Json(folder)["candidates"]);
        Assert.Null(GeneratorPackageLoader.Load(folder).Candidates);
    }

    [Fact]
    public void Candidates_are_null_without_the_section()
    {
        Assert.Null(PackageDocument.Load(Folder()).Candidates);
    }
}
