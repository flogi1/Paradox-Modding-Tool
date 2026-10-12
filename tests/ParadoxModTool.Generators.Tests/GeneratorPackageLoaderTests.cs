using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>package.json lesen und prüfen; kaputte Pakete werden mit ihrem Fehler gelistet.</summary>
public sealed class GeneratorPackageLoaderTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    internal const string ValidJson = """
        {
          "id": "iv2",
          "name": "IV2 National Ideas",
          "gameId": "eu5",
          "keyField": "tag",
          "head": [
            { "id": "tag", "label": "Tag", "type": "text", "required": true, "pattern": "^[A-Z0-9]{2,5}$" }
          ],
          "root": {
            "id": "categories", "label": "Kategorien", "constantsTable": "categories",
            "child": {
              "id": "ideas", "label": "Ideen", "min": 0, "max": 9, "numberBaseField": "base",
              "fields": [ { "id": "name", "label": "Name", "type": "text", "multilingual": true } ],
              "child": { "id": "levels", "label": "Stufen", "min": 1, "max": 4, "fields": [ { "id": "modifiers", "label": "Modifikatoren", "type": "modifiers" } ] }
            }
          },
          "constants": { "categories": [ { "key": "adm", "base": "0" }, { "key": "dip", "base": "10" } ] },
          "outputs": [ { "id": "own", "kind": "ownFile", "path": "common/x_{{tag}}.txt", "template": "own.sbn" } ],
          "targets": [ "mod", "output" ]
        }
        """;

    private string Package(string folder, string json, params string[] templates)
    {
        _temp.Write(folder + "/package.json", json);
        foreach (var template in templates)
        {
            _temp.Write(folder + "/templates/" + template, "x");
        }

        return _temp.PathOf(folder);
    }

    [Fact]
    public void Valid_package_loads_levels_fields_and_outputs()
    {
        var package = GeneratorPackageLoader.Load(Package("iv2", ValidJson, "own.sbn"));

        Assert.Equal("iv2", package.Id);
        Assert.Equal("tag", package.KeyField);
        Assert.Equal("^[A-Z0-9]{2,5}$", package.Head[0].Pattern);
        Assert.Equal("categories", package.Root!.ConstantsTable);
        Assert.Equal(9, package.Root.Child!.Max);
        Assert.Equal("base", package.Root.Child.NumberBaseField);
        Assert.Equal(4, package.Root.Child.Child!.Max);
        Assert.Equal(FieldType.Modifiers, package.Root.Child.Child.Fields[0].Type);
        Assert.True(package.Root.Child.Fields[0].Multilingual);
        Assert.Equal(OutputKind.OwnFile, package.Outputs[0].Kind);
        Assert.Equal(new[] { "mod", "output" }, package.Targets);
        Assert.Equal("10", package.Constants["categories"][1]["base"]);
    }

    private static string WithAssets(string assets) => ValidJson.Replace("\"targets\": [", "\"assets\": " + assets + ",\n  \"targets\": [");

    [Fact]
    public void Asset_object_form_reads_origin()
    {
        var package = GeneratorPackageLoader.Load(Package("a", WithAssets("""{ "a": { "path": "assets/a.zip", "origin": "mod" } }"""), "own.sbn"));

        Assert.Equal(new AssetDef("assets/a.zip", AssetOrigin.Mod), package.AssetDefs["a"]);
        Assert.Equal("assets/a.zip", package.Assets["a"]);
    }

    [Fact]
    public void Asset_string_form_counts_as_game()
    {
        var package = GeneratorPackageLoader.Load(Package("a", WithAssets("""{ "a": "assets/a.zip" }"""), "own.sbn"));

        Assert.Equal(new AssetDef("assets/a.zip", AssetOrigin.Game), package.AssetDefs["a"]);
    }

    [Fact]
    public void Asset_unknown_origin_is_error()
    {
        var folder = Package("a", WithAssets("""{ "a": { "path": "assets/a.zip", "origin": "steam" } }"""), "own.sbn");

        var error = Assert.Throws<GeneratorPackageException>(() => GeneratorPackageLoader.Load(folder));
        Assert.Contains("steam", error.Message);
    }

    [Fact]
    public void Asset_path_with_parent_is_error()
    {
        var folder = Package("a", WithAssets("""{ "a": "../x.zip" }"""), "own.sbn");

        var error = Assert.Throws<GeneratorPackageException>(() => GeneratorPackageLoader.Load(folder));
        Assert.Contains("../x.zip", error.Message);
    }

    [Fact]
    public void Constant_cell_null_is_empty_text()
    {
        var json = ValidJson.Replace("{ \"key\": \"adm\", \"base\": \"0\" }", "{ \"key\": \"adm\", \"base\": null }");
        Assert.Contains("\"base\": null", json);

        var package = GeneratorPackageLoader.Load(Package("c", json, "own.sbn"));

        Assert.Equal(string.Empty, package.Constants["categories"][0]["base"]);
    }

    [Fact]
    public void Marker_id_is_read()
    {
        var package = GeneratorPackageLoader.Load(Package("m", ValidJson.Replace("\"targets\": [", "\"markerId\": \"orig\",\n  \"targets\": ["), "own.sbn"));

        Assert.Equal("orig", package.MarkerId);
        Assert.Equal("iv2", package.Id);
    }

    [Fact]
    public void Marker_id_with_slash_is_error()
    {
        var folder = Package("m", ValidJson.Replace("\"targets\": [", "\"markerId\": \"a/b\",\n  \"targets\": ["), "own.sbn");

        var error = Assert.Throws<GeneratorPackageException>(() => GeneratorPackageLoader.Load(folder));
        Assert.Contains("a/b", error.Message);
    }

    [Fact]
    public void Template_name_with_parent_is_error()
    {
        _temp.Write("secret.sbn", "geheim");
        var folder = Package("t", ValidJson.Replace("\"template\": \"own.sbn\"", "\"template\": \"../../secret.sbn\""), "own.sbn");

        var error = Assert.Throws<GeneratorPackageException>(() => GeneratorPackageLoader.Load(folder));
        Assert.Contains("../../secret.sbn", error.Message);
    }

    [Fact]
    public void Skeleton_name_in_subfolder_is_error()
    {
        var folder = Package("t", ValidJson.Replace("\"template\": \"own.sbn\"", "\"template\": \"own.sbn\", \"skeleton\": \"sub/own.sbn\""), "own.sbn");

        Assert.Throws<GeneratorPackageException>(() => GeneratorPackageLoader.Load(folder));
    }

    [Fact]
    public void Unknown_output_kind_is_an_error_naming_it()
    {
        var folder = Package("bad", ValidJson.Replace("\"ownFile\"", "\"foo\""), "own.sbn");

        var error = Assert.Throws<GeneratorPackageException>(() => GeneratorPackageLoader.Load(folder));
        Assert.Contains("foo", error.Message);
    }

    [Fact]
    public void Missing_template_file_is_an_error_naming_it()
    {
        var folder = Package("bad", ValidJson);

        var error = Assert.Throws<GeneratorPackageException>(() => GeneratorPackageLoader.Load(folder));
        Assert.Contains("own.sbn", error.Message);
    }

    [Fact]
    public void KeyField_must_be_a_head_field()
    {
        var folder = Package("bad", ValidJson.Replace("\"keyField\": \"tag\"", "\"keyField\": \"nope\""), "own.sbn");

        var error = Assert.Throws<GeneratorPackageException>(() => GeneratorPackageLoader.Load(folder));
        Assert.Contains("nope", error.Message);
    }

    [Fact]
    public void List_reports_broken_packages_without_throwing()
    {
        Package("generator-packages/good", ValidJson, "own.sbn");
        Package("generator-packages/broken", "{ not json");

        var list = GeneratorPackageLoader.List(_temp.PathOf("generator-packages"));

        Assert.Equal(2, list.Count);
        Assert.NotNull(list.Single(p => p.Folder.EndsWith("good")).Package);
        Assert.NotNull(list.Single(p => p.Folder.EndsWith("broken")).Error);
    }

    internal const string ExtendedJson = """
        {
          "id": "ext",
          "name": { "de": "N-de", "en": "N-en" },
          "gameId": "eu5",
          "keyField": "tag",
          "head": [ { "id": "tag", "label": { "en": "Tag-en", "fr": "Tag-fr" }, "type": "text" } ],
          "root": {
            "id": "categories", "label": "Kategorien", "constantsTable": "categories",
            "child": { "id": "ideas", "label": { "de": "Ideen", "en": "Ideas" }, "min": 0, "max": 9, "initial": 4 }
          },
          "constants": { "categories": [ { "key": "adm", "label": { "de": "Verwaltung", "en": "Administrative" }, "base": 0 } ] },
          "outputs": [
            { "id": "sec", "kind": "insertSection", "path": "x.txt", "template": "own.sbn", "legacyPatterns": [ "a_{{ tag }}_\\d+" ] },
            { "id": "mods", "kind": "syncBlocks", "path": "y.txt", "template": "own.sbn", "keyPattern": "m_*", "keep": [ "game_data" ] }
          ],
          "errors": [ { "when": "x", "message": { "de": "F-de", "en": "E" } } ],
          "warnings": [ { "when": "y", "message": { "en": "W" } } ]
        }
        """;

    [Fact]
    public void Reads_initial_errors_patterns_keep()
    {
        var package = GeneratorPackageLoader.Load(Package("ext", ExtendedJson, "own.sbn"), uiLanguage: "en");

        Assert.Equal(4, package.Root!.Child!.Initial);
        Assert.Null(package.Root.Initial);
        Assert.Equal("E", package.Errors.Single().Message);
        Assert.Equal("x", package.Errors.Single().When);
        Assert.Equal("W", package.Warnings.Single().Message);
        Assert.Equal(new[] { "a_{{ tag }}_\\d+" }, package.Outputs[0].LegacyPatterns);
        Assert.Null(package.Outputs[0].Keep);
        Assert.Equal(new[] { "game_data" }, package.Outputs[1].Keep);
    }

    [Fact]
    public void Labels_and_name_can_be_language_maps()
    {
        var german = GeneratorPackageLoader.Load(Package("ext", ExtendedJson, "own.sbn"), uiLanguage: "de");
        var english = GeneratorPackageLoader.Load(Package("ext", ExtendedJson, "own.sbn"), uiLanguage: "en");

        Assert.Equal("N-de", german.Name);
        Assert.Equal("N-en", english.Name);
        Assert.Equal("Tag-en", german.Head[0].Label);
        Assert.Equal("Ideen", german.Root!.Child!.Label);
        Assert.Equal("Verwaltung", german.Constants["categories"][0]["label"]);
        Assert.Equal("Administrative", english.Constants["categories"][0]["label"]);
        Assert.Equal("0", german.Constants["categories"][0]["base"]);
        Assert.Equal("F-de", german.Errors[0].Message);
    }

    [Fact]
    public void AfterGenerate_is_localized()
    {
        var json = ValidJson.Replace("\"targets\"", "\"afterGenerate\": { \"de\": \"Skript laufen lassen\", \"en\": \"Run the script\" },\n  \"targets\"");
        var folder = Package("after", json, "own.sbn");

        Assert.Equal("Run the script", GeneratorPackageLoader.Load(folder, uiLanguage: "en").AfterGenerate);
        Assert.Equal("Skript laufen lassen", GeneratorPackageLoader.Load(folder, uiLanguage: "de").AfterGenerate);
        Assert.Null(GeneratorPackageLoader.Load(Package("plain", ValidJson, "own.sbn")).AfterGenerate);
    }

    [Fact]
    public void Data_folder_defaults_to_package_folder()
    {
        var folder = Package("iv2", ValidJson, "own.sbn");
        var package = GeneratorPackageLoader.Load(folder);

        Assert.Equal(folder, package.DataFolder);
        Assert.False(package.Shipped);
        Assert.Empty(package.Errors);
    }

    [Fact]
    public void Asset_path_uses_data_folder()
    {
        var json = ValidJson.Replace("\"targets\"", "\"assets\": { \"coa\": \"assets/coa.zip\" },\n  \"targets\"");
        var data = _temp.PathOf("data/iv2");
        var package = GeneratorPackageLoader.Load(Package("shipped/iv2", json, "own.sbn"), data, shipped: true);

        Assert.Equal(data, package.DataFolder);
        Assert.True(package.Shipped);
        Assert.Equal(Path.Combine(data, "assets", "coa.zip"), package.AssetPath("coa"));
    }

    [Fact]
    public void Shipped_wins_and_broken_shipped_is_listed()
    {
        Package("shipped/iv2", ValidJson, "own.sbn");
        _temp.Write("shipped/broken/readme.txt", "x");
        Package("data/iv2", ValidJson.Replace("IV2 National Ideas", "Eigene Kopie"), "own.sbn");
        Package("data/own", ValidJson.Replace("\"id\": \"iv2\"", "\"id\": \"own\""), "own.sbn");

        var list = GeneratorPackageLoader.ListAll(_temp.PathOf("shipped"), _temp.PathOf("data"));

        Assert.Equal(3, list.Count);
        var iv2 = list.Single(e => e.Package?.Id == "iv2");
        Assert.True(iv2.Shipped);
        Assert.Equal("IV2 National Ideas", iv2.Package!.Name);
        Assert.Equal(_temp.PathOf("data/iv2"), iv2.Package.DataFolder);
        var broken = list.Single(e => e.Package is null);
        Assert.True(broken.Shipped);
        Assert.NotNull(broken.Error);
        var own = list.Single(e => e.Package?.Id == "own");
        Assert.False(own.Shipped);
        Assert.Equal(_temp.PathOf("data/own"), own.Package!.DataFolder);
    }

    [Fact]
    public void Shipped_packages_of_another_game_are_hidden()
    {
        Package("shipped/iv2", ValidJson, "own.sbn");
        Package("shipped/vic", ValidJson.Replace("\"id\": \"iv2\"", "\"id\": \"vic\"").Replace("\"gameId\": \"eu5\"", "\"gameId\": \"vic3\""), "own.sbn");
        Package("data/iv2", ValidJson.Replace("IV2 National Ideas", "Eigene Kopie"), "own.sbn");

        var forVic3 = GeneratorPackageLoader.ListAll(_temp.PathOf("shipped"), _temp.PathOf("data"), "vic3");
        var all = GeneratorPackageLoader.ListAll(_temp.PathOf("shipped"), _temp.PathOf("data"));

        // Die eigene Kopie mit der Kennung eines mitgelieferten Pakets bleibt verdeckt, auch wenn das mitgelieferte zu einem anderen Spiel gehört.
        Assert.Equal("vic", Assert.Single(forVic3).Package!.Id);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void ListAll_without_shipped_folder_lists_data_packages()
    {
        Package("data/own", ValidJson, "own.sbn");

        var entry = Assert.Single(GeneratorPackageLoader.ListAll(_temp.PathOf("nothing"), _temp.PathOf("data")));
        Assert.False(entry.Shipped);
    }

    [Fact]
    public void States_live_in_data_folder()
    {
        var package = GeneratorPackageLoader.Load(Package("shipped/iv2", ValidJson, "own.sbn"), _temp.PathOf("data/iv2"), shipped: true);
        var state = GeneratorState.CreateEmpty(package, new[] { "english" });
        state.Key = "BYZ";

        GeneratorStateStore.Save(package.DataFolder, state);

        Assert.True(File.Exists(_temp.PathOf("data/iv2/states/BYZ.json")));
        Assert.False(Directory.Exists(_temp.PathOf("shipped/iv2/states")));
    }

    internal const string ReadSection = """
        "read": {
          "keys": "^main_menu/common/static_modifiers/[^/]*IV2_(?<key>[A-Z0-9]{2,5})\\.txt$",
          "elements": {
            "file": "x_{{ tag }}.txt",
            "block": "m_{{ tag }}_(?<number>\\d+)(?<suffix>_\\d)?",
            "level": "levels",
            "fields": { "modifiers": { "body": true, "except": [ "game_data" ] } }
          },
          "localization": [
            { "key": "name_{{ tag }}_(?<number>\\d+)", "field": "ideas.name" },
            { "key": "desc_{{ tag }}_(?<number>\\d+)", "field": "ideas.desc" }
          ],
          "flags": [ { "file": "ga_{{ tag }}.txt", "block": "ga_{{ tag }}_(?<number>\\d+)(?<suffix>_\\d)?", "contains": "_expensive_", "field": "levels.expensive" } ],
          "assets": [ { "field": "coa", "file": "coa/{{ tag }}.dds" } ]
        },
        """;

    [Fact]
    public void Reads_read_section()
    {
        var package = GeneratorPackageLoader.Load(Package("iv2", ValidJson.Replace("\"targets\"", ReadSection + "\n  \"targets\""), "own.sbn"));

        var read = package.Read!;
        Assert.Equal("^main_menu/common/static_modifiers/[^/]*IV2_(?<key>[A-Z0-9]{2,5})\\.txt$", read.Keys);
        Assert.Equal("levels", read.Elements.Level);
        Assert.Equal("x_{{ tag }}.txt", read.Elements.File);
        Assert.True(read.Elements.Fields["modifiers"].Body);
        Assert.Equal(new[] { "game_data" }, read.Elements.Fields["modifiers"].Except);
        Assert.Equal(2, read.Localization.Count);
        Assert.Equal("ideas.desc", read.Localization[1].Field);
        Assert.Equal("_expensive_", Assert.Single(read.Flags).Contains);
        Assert.Equal("coa", Assert.Single(read.Assets).Field);
    }

    [Fact]
    public void Package_without_read_has_null() =>
        Assert.Null(GeneratorPackageLoader.Load(Package("iv2", ValidJson, "own.sbn")).Read);

    [Fact]
    public void Reads_check_unreferenced()
    {
        var json = ValidJson.Replace(
            "\"outputs\": [ { \"id\": \"own\", \"kind\": \"ownFile\", \"path\": \"common/x_{{tag}}.txt\", \"template\": \"own.sbn\" } ]",
            "\"outputs\": [ { \"id\": \"own\", \"kind\": \"ownFile\", \"path\": \"common/x_{{tag}}.txt\", \"template\": \"own.sbn\" }, { \"id\": \"m\", \"kind\": \"syncBlocks\", \"path\": \"m.txt\", \"template\": \"own.sbn\", \"keyPattern\": \"m_*\", \"checkUnreferenced\": true } ]");
        var package = GeneratorPackageLoader.Load(Package("iv2", json, "own.sbn"));

        Assert.False(package.Outputs[0].CheckUnreferenced);
        Assert.True(package.Outputs[1].CheckUnreferenced);
    }

    [Fact]
    public void Read_without_elements_is_an_error_naming_it()
    {
        var json = ValidJson.Replace("\"targets\"", "\"read\": { \"keys\": \"x\" },\n  \"targets\"");

        var error = Assert.Throws<GeneratorPackageException>(() => GeneratorPackageLoader.Load(Package("iv2", json, "own.sbn")));
        Assert.Contains("read.elements", error.Message);
    }

    [Fact]
    public void PackagesRoot_is_below_the_game_directory() =>
        Assert.Equal(Path.Combine("C:\\data\\games\\eu5", "generator-packages"), GeneratorPackageLoader.PackagesRoot("C:\\data\\games\\eu5"));
}
