using ParadoxModTool.Patches;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Ausgabeart "jsonEntry": ein Eintrag in einer externen JSON-Datei im Stil von Pythons <c>json.dumps</c> (Lader, Schreiber, Planer, Pfadspeicher).</summary>
public sealed class JsonEntryTests : IDisposable
{
    private const string Tags = "{\n \"version\": 1,\n \"entries\": {\n  \"nat:army_1\": {\n   \"tags\": [\n    \"ai_balanced\"\n   ],\n   \"name\": \"Hinterhalt\"\n  }\n }\n}\n";

    /// <summary><see cref="Tags"/> ohne das Ende des letzten Eintrags; hier hängt ein neuer Eintrag an.</summary>
    private const string TagsBeforeEnd = "{\n \"version\": 1,\n \"entries\": {\n  \"nat:army_1\": {\n   \"tags\": [\n    \"ai_balanced\"\n   ],\n   \"name\": \"Hinterhalt\"\n";

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Same_value_round_trips_byte_equal() =>
        Assert.Equal(Tags, JsonEntryWriter.Set(Tags, "entries", "nat:army_1", "{\"tags\":[\"ai_balanced\"],\"name\":\"Hinterhalt\"}"));

    [Fact]
    public void New_key_is_appended_in_python_style()
    {
        var result = JsonEntryWriter.Set(Tags, "entries", "nat:army_53", "{\"tags\":[],\"name\":\"Ö\\\"x\"}");

        Assert.Equal(TagsBeforeEnd + "  },\n  \"nat:army_53\": {\n   \"tags\": [],\n   \"name\": \"Ö\\\"x\"\n  }\n }\n}\n", result);
    }

    [Fact]
    public void Existing_key_keeps_its_place()
    {
        var two = JsonEntryWriter.Set(Tags, "entries", "nat:army_2", "{}");
        var replaced = JsonEntryWriter.Set(two, "entries", "nat:army_1", "{\"name\":\"neu\"}");

        Assert.Equal("{\n \"version\": 1,\n \"entries\": {\n  \"nat:army_1\": {\n   \"name\": \"neu\"\n  },\n  \"nat:army_2\": {}\n }\n}\n", replaced);
    }

    [Fact]
    public void Line_ending_indent_and_missing_final_newline_follow_the_file()
    {
        var crlf = Tags.Replace("\n", "\r\n");
        Assert.Equal(crlf, JsonEntryWriter.Set(crlf, "entries", "nat:army_1", "{\"tags\":[\"ai_balanced\"],\"name\":\"Hinterhalt\"}"));

        var wide = "{\n    \"entries\": {\n        \"a\": 1\n    }\n}";
        Assert.Equal(
            "{\n    \"entries\": {\n        \"a\": 1,\n        \"b\": [\n            true,\n            null\n        ]\n    }\n}",
            JsonEntryWriter.Set(wide, "entries", "b", "[true,null]"));
    }

    [Fact]
    public void Scalars_and_escapes_are_written_like_python()
    {
        var result = JsonEntryWriter.Set("{}", string.Empty, "k", "{\"n\":1.50,\"e\":1e-05,\"c\":\"a\\u001fb\\t\\\\\\u00e9\\/\",\"x\":{},\"y\":[]}");

        Assert.Equal("{\n \"k\": {\n  \"n\": 1.50,\n  \"e\": 1e-05,\n  \"c\": \"a\\u001fb\\t\\\\é/\",\n  \"x\": {},\n  \"y\": []\n }\n}", result);
    }

    [Fact]
    public void Invalid_json_throws()
    {
        Assert.Throws<GeneratorOutputException>(() => JsonEntryWriter.Set("{ nope", "entries", "k", "{}"));
        Assert.Throws<GeneratorOutputException>(() => JsonEntryWriter.Set(Tags, "entries", "k", "{ nope"));
        Assert.Throws<GeneratorOutputException>(() => JsonEntryWriter.Set(Tags, "entries.gibt.es.nicht", "k", "{}"));
        Assert.Throws<GeneratorOutputException>(() => JsonEntryWriter.Set(Tags, "version", "k", "{}"));
    }

    [Fact]
    public void Duplicate_keys_throw_output_exception_not_argument_exception()
    {
        // im Wert und in der Datei, auch tief verschachtelt und beim Navigieren über jsonPath
        Assert.Throws<GeneratorOutputException>(() => JsonEntryWriter.Set(Tags, "entries", "k", "{\"a\":1,\"a\":2}"));
        Assert.Throws<GeneratorOutputException>(() => JsonEntryWriter.Set(Tags, "entries", "k", "{\"x\":[{\"a\":1,\"a\":2}]}"));
        Assert.Throws<GeneratorOutputException>(() => JsonEntryWriter.Set("{\"entries\":{},\"entries\":{}}", "entries", "k", "{}"));
        Assert.Throws<GeneratorOutputException>(() => JsonEntryWriter.Set("{\"entries\":{\"b\":{\"a\":1,\"a\":2}}}", "entries", "k", "{}"));
    }

    private const string PackageJson = """
        {
          "id": "j", "name": "J", "gameId": "eu5", "keyField": "tag",
          "head": [ { "id": "tag", "label": "Tag", "type": "text" } ],
          "root": { "id": "ideas", "label": "Ideen", "min": 0, "max": 9, "fields": [ { "id": "name", "label": "Name", "type": "text" } ] },
          "externals": { "tags": { "label": "KI-Tags", "hint": "iv2_ai_tags.json" } },
          "outputs": [ OUTPUTS ],
          "targets": [ "mod", "output" ]
        }
        """;

    private const string Output = """{ "id": "t", "kind": "jsonEntry", "path": "", "external": "tags", "jsonPath": "entries", "entryKey": "nat:{{ tag }}", "template": "t.sbn" }""";

    private GeneratorPackage Load(string output = Output)
    {
        _temp.Write("pkg/package.json", PackageJson.Replace("OUTPUTS", output));
        _temp.Write("pkg/templates/t.sbn", "{ \"tags\": [\"ai_balanced\"], \"name\": \"{{ tag }}\" }");
        return GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
    }

    private GeneratorPlan Plan(GeneratorPackage package, string tag, string? externalPath)
    {
        var state = GeneratorState.CreateEmpty(package, new[] { "english" });
        state.Key = tag;
        state.Head.Values["tag"] = tag;
        var paths = externalPath is null ? null : new Dictionary<string, string> { ["tags"] = externalPath };
        return GeneratorPlanner.Plan(package, state, new GeneratorTarget(_temp.PathOf("mod"), null, null, paths));
    }

    [Fact]
    public void Loader_reads_externals_and_the_jsonEntry_output()
    {
        var package = Load();

        Assert.Equal(new ExternalDef("KI-Tags", "iv2_ai_tags.json"), package.Externals["tags"]);
        var output = package.Outputs.Single();
        Assert.Equal(OutputKind.JsonEntry, output.Kind);
        Assert.Equal(string.Empty, output.Path);
        Assert.Equal(("tags", "entries", "nat:{{ tag }}"), (output.External, output.JsonPath, output.EntryKey));
    }

    [Theory]
    [InlineData("""{ "id": "t", "kind": "jsonEntry", "external": "tags", "entryKey": "k" }""", "outputs[t].template")]
    [InlineData("""{ "id": "t", "kind": "jsonEntry", "entryKey": "k", "template": "t.sbn" }""", "outputs[t].external")]
    [InlineData("""{ "id": "t", "kind": "jsonEntry", "external": "tags", "template": "t.sbn" }""", "outputs[t].entryKey")]
    [InlineData("""{ "id": "t", "kind": "jsonEntry", "external": "weg", "entryKey": "k", "template": "t.sbn" }""", "outputs[t].external")]
    [InlineData("""{ "id": "t", "kind": "ownFile", "path": "x.txt", "external": "tags", "template": "t.sbn" }""", "outputs[t].external")]
    [InlineData("""{ "id": "t", "kind": "jsonEntry", "external": "tags", "entryKey": "k", "template": "t.sbn", "match": "x" }""", "outputs[t].match")]
    public void Loader_rejects_incomplete_or_misplaced_json_entry_keys(string output, string expected)
    {
        var error = Assert.Throws<GeneratorPackageException>(() => Load(output));

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void Missing_external_path_skips_with_warning()
    {
        var package = Load();

        var withoutPath = Plan(package, "A", null);
        var missingFile = Plan(package, "A", _temp.PathOf("weg/tags.json"));

        Assert.Empty(withoutPath.Files);
        Assert.Equal(GeneratorsTexts.ExternalNotChosen("KI-Tags"), Assert.Single(withoutPath.Warnings));
        Assert.Empty(missingFile.Files);
        Assert.Equal(GeneratorsTexts.ExternalNotChosen("KI-Tags"), Assert.Single(missingFile.Warnings));
    }

    [Fact]
    public void Plan_adds_the_entry_to_the_external_file_outside_the_mod()
    {
        var path = _temp.Write("ext/tags.json", Tags);

        var plan = Plan(Load(), "A", path);

        var file = Assert.Single(plan.Files);
        Assert.Equal("extern/tags.json", file.RelativePath);
        Assert.Equal(path, file.ExternalPath);
        Assert.Equal(PlannedFileStatus.Changed, file.Status);
        Assert.Equal(Tags, file.OriginalText);
        Assert.Equal(TagsBeforeEnd + "  },\n  \"nat:A\": {\n   \"tags\": [\n    \"ai_balanced\"\n   ],\n   \"name\": \"A\"\n  }\n }\n}\n", file.NewText);
        Assert.False(file.HasBom);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void Plan_is_unchanged_when_the_entry_is_already_there()
    {
        var path = _temp.Write("ext/tags.json", Tags);
        var package = Load();
        var first = Plan(package, "A", path).Files.Single();
        File.WriteAllText(path, first.NewText!);

        Assert.Equal(PlannedFileStatus.Unchanged, Plan(package, "A", path).Files.Single().Status);
    }

    [Fact]
    public void Empty_rendered_value_plans_no_external_file()
    {
        var path = _temp.Write("ext/tags.json", Tags);
        var package = Load();
        // Leerer Wert (nur Leerraum) = kein Eintrag: die Datei wird weder geplant noch geschrieben, ein vorhandener Eintrag bleibt
        _temp.Write("pkg/templates/t.sbn", "{{ if tag != \"A\" }}{ \"tags\": [], \"name\": \"{{ tag }}\" }{{ end }}\n");

        var plan = Plan(package, "A", path);

        Assert.Empty(plan.Files);
        Assert.Empty(plan.Warnings);
        Assert.Single(Plan(package, "B", path).Files);
    }

    [Fact]
    public void Several_entries_for_one_file_build_on_each_other()
    {
        var path = _temp.Write("ext/tags.json", Tags);
        var second = Output.Replace("\"id\": \"t\"", "\"id\": \"u\"").Replace("nat:{{ tag }}", "grp:{{ tag }}");

        var file = Plan(Load(Output + ", " + second), "A", path).Files.Single();

        Assert.Contains("\"nat:A\"", file.NewText);
        Assert.Contains("\"grp:A\"", file.NewText);
    }

    [Fact]
    public void External_file_is_written_as_utf8_without_bom()
    {
        var path = _temp.Write("ext/tags.json", Tags, new System.Text.UTF8Encoding(true));

        var file = Plan(Load(), "A", path).Files.Single();

        Assert.False(file.HasBom);
        Assert.Equal(Tags, file.OriginalText);
        Assert.Equal(PlannedFileStatus.Changed, file.Status);
    }

    [Fact]
    public void Other_outputs_cannot_write_to_the_external_file()
    {
        var path = _temp.Write("ext/tags.json", Tags);
        var own = """{ "id": "o", "kind": "ownFile", "path": "extern/tags.json", "template": "t.sbn" }""";

        Assert.Throws<GeneratorOutputException>(() => Plan(Load(Output + ", " + own), "A", path));
        Assert.Throws<GeneratorOutputException>(() => Plan(Load(own + ", " + Output), "A", path));
    }

    [Fact]
    public void Invalid_external_json_names_the_file()
    {
        var path = _temp.Write("ext/tags.json", "{ kaputt");

        var error = Assert.Throws<GeneratorOutputException>(() => Plan(Load(), "A", path));

        Assert.Contains("extern/tags.json", error.Message);
    }

    [Fact]
    public void Package_check_accepts_empty_path_and_finds_typos_in_the_entry_key()
    {
        Load();
        Assert.Equal(0, Ai.GeneratorCli.Check(_temp.PathOf("pkg")).ExitCode);

        _temp.Write("pkg/package.json", PackageJson.Replace("OUTPUTS", Output.Replace("{{ tag }}", "{{ tagg }}")));
        var result = Ai.GeneratorCli.Check(_temp.PathOf("pkg"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("outputs[t].entryKey", result.Lines[0]);
    }

    [Fact]
    public void External_path_store_remembers_and_forgets_paths()
    {
        var folder = _temp.PathOf("data");
        Assert.Empty(ExternalPathStore.Load(folder));

        ExternalPathStore.Set(folder, "tags", "C:/x/tags.json");
        ExternalPathStore.Set(folder, "other", "C:/x/o.json");
        Assert.Equal("C:/x/tags.json", ExternalPathStore.Load(folder)["tags"]);

        ExternalPathStore.Set(folder, "tags", null);
        Assert.Equal(new[] { "other" }, ExternalPathStore.Load(folder).Keys);

        _temp.Write("data/externals.json", "{ kaputt");
        Assert.Empty(ExternalPathStore.Load(folder));
    }

    [Fact]
    public void External_path_store_load_survives_locked_file_and_set_keeps_other_paths()
    {
        var folder = _temp.PathOf("data");
        ExternalPathStore.Set(folder, "tags", "C:/x/tags.json");
        ExternalPathStore.Set(folder, "other", "C:/x/o.json");

        using (new FileStream(Path.Combine(folder, "externals.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Empty(ExternalPathStore.Load(folder));
            // Gesperrt: Set darf die anderen Pfade nicht mit einer leeren Liste überschreiben, sondern meldet den Fehler
            Assert.ThrowsAny<IOException>(() => ExternalPathStore.Set(folder, "tags", "C:/y/tags.json"));
        }

        Assert.Equal(new[] { "other", "tags" }, ExternalPathStore.Load(folder).Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("C:/x/tags.json", ExternalPathStore.Load(folder)["tags"]);
    }
}
