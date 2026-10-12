using System.Text;
using ParadoxModTool.Generators.Lookups;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Katalog aller Quellen eines Felds, JSON-Quelle, Filter und Übersetzungen (Spec 2026-10-05-generator-packages-lookups, Abschnitt 1).</summary>
public sealed class LookupCatalogTests : IDisposable
{
    private const string Database = """
        {"nations":[{"tag":"VIN","label":"Vinland","ideas":[{"key":"k1","name":"N1","desc":"D1"},{"key":"k2","name":"N2"}]},{"label":"NoTag","ideas":[]}]}
        """;

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string[] Roots => new[] { _temp.PathOf("game") };

    private GeneratorPackage Package()
    {
        var json = GeneratorPackageLoaderTests.ValidJson
            .Replace("\"targets\":", """
                "assets": { "db": "assets/db.json" },
                "lookups": [
                  { "id": "nations", "kind": "json", "asset": "db", "items": "nations[].ideas[]",
                    "fields": { "key": "key", "name": "name", "desc": "desc", "tag": "../tag", "nation": "../label" }, "label": "Idee" },
                  { "id": "advances", "kind": "gameKeys", "folder": "in_game/common/advances", "tagFromFile": "^country_([A-Z]{2,5})\\.txt$", "label": "Advance" }
                ],
                "targets":
                """)
            .Replace("""{ "id": "name", "label": "Name", "type": "text", "multilingual": true }""",
                """{ "id": "name", "label": "Name", "type": "text", "multilingual": true, "lookup": { "sources": [ "advances", "nations" ], "fills": { "name": "name" } } }""");
        _temp.Write("p/package.json", json);
        _temp.Write("p/templates/own.sbn", "x");
        return GeneratorPackageLoader.Load(_temp.PathOf("p"), uiLanguage: "de");
    }

    private void Game()
    {
        _temp.Write("game/in_game/common/advances/country_SWE.txt", "swe_adv = { }");
        _temp.Write("game/main_menu/localization/english/a_l_english.yml", "l_english:\n swe_adv: \"Swedish Way\"\n swe_adv_desc: \"Way text\"\n", new UTF8Encoding(true));
        _temp.Write("game/main_menu/localization/german/a_l_german.yml", "l_german:\n swe_adv: \"Schwedisch\"\n", new UTF8Encoding(true));
    }

    private static FieldLookupDef Lookup(GeneratorPackage package) => package.Root!.Child!.Fields.Single(f => f.Id == "name").Lookup!;

    [Fact]
    public void Json_items_with_parent_fields()
    {
        var path = _temp.Write("db.json", Database);
        var def = new LookupSourceDef("nations", LookupKind.Json, "Idee", Asset: "db", Items: "nations[].ideas[]",
            Fields: new Dictionary<string, string> { ["key"] = "key", ["name"] = "name", ["desc"] = "desc", ["tag"] = "../tag", ["nation"] = "../label" });

        var entries = JsonLookupSource.Read(def, path);

        Assert.Equal(2, entries.Count);
        Assert.Equal(new LookupEntry("nations", "k1", "N1", "D1", "VIN", "Vinland"), entries[0]);
        Assert.Equal(new LookupEntry("nations", "k2", "N2", string.Empty, "VIN", "Vinland"), entries[1]);
    }

    [Fact]
    public void Catalog_reads_all_sources_in_lookup_order()
    {
        Game();
        var package = Package();
        _temp.Write("p/assets/db.json", Database);

        var catalog = LookupCatalog.Load(package, Lookup(package), Roots);

        Assert.Empty(catalog.Notes);
        Assert.Equal(new[] { ("advances", "Advance"), ("nations", "Idee") }, catalog.Sources);
        Assert.Equal(new[] { "swe_adv", "k1", "k2" }, catalog.Entries.Select(e => e.Key));
        Assert.Equal("SWE", catalog.Entries[0].Tag);
    }

    [Fact]
    public void Json_invalid_gives_note_not_exception()
    {
        Game();
        var package = Package();
        _temp.Write("p/assets/db.json", "{");

        var catalog = LookupCatalog.Load(package, Lookup(package), Roots);

        Assert.Equal(new[] { "swe_adv" }, catalog.Entries.Select(e => e.Key));
        Assert.Equal("file", Assert.Single(catalog.Notes).Kind);
    }

    [Theory]
    [InlineData("../outside", true)]
    [InlineData("../nothing", false)]
    public void Unsafe_folder_is_reported_before_existence(string folder, bool exists)
    {
        Game();
        if (exists)
        {
            _temp.Write("outside/country_SWE.txt", "x_adv = { }");
        }

        var package = Package();
        _temp.Write("p/package.json", System.IO.File.ReadAllText(_temp.PathOf("p/package.json")).Replace("in_game/common/advances", folder));
        package = GeneratorPackageLoader.Load(_temp.PathOf("p"), uiLanguage: "de");

        var catalog = LookupCatalog.Load(package, new FieldLookupDef(new[] { "advances" }, new Dictionary<string, string> { ["name"] = "name" }), Roots);

        var note = Assert.Single(catalog.Notes);
        Assert.Equal("unsafe", note.Kind);
        Assert.Equal(GeneratorsTexts.LookupUnsafeFolder("Advance", folder), note.Text);
    }

    [Fact]
    public void Missing_game_folder_and_asset_give_notes()
    {
        var package = Package();

        var catalog = LookupCatalog.Load(package, Lookup(package), Array.Empty<string>());

        Assert.Empty(catalog.Entries);
        Assert.Equal(new[] { "gameFolder", "asset" }, catalog.Notes.Select(n => n.Kind));
        Assert.Equal(new[] { "advances", "nations" }, catalog.Notes.Select(n => n.SourceId));
    }

    [Fact]
    public void Translations_from_game_language_folder()
    {
        Game();
        var package = Package();
        _temp.Write("p/assets/db.json", Database);
        var catalog = LookupCatalog.Load(package, Lookup(package), Roots);

        var advance = catalog.Entries.Single(e => e.Key == "swe_adv");
        Assert.Equal(new Dictionary<string, string> { ["name"] = "Schwedisch" }, catalog.Translations(advance, "german", Roots));
        Assert.Empty(catalog.Translations(advance, "french", Roots));
        Assert.Empty(catalog.Translations(catalog.Entries.Single(e => e.Key == "k1"), "german", Roots));
    }

    [Fact]
    public void Filter_words_source_and_tag()
    {
        var all = new[]
        {
            new LookupEntry("advances", "swe_adv", "Swedish Way", "A way", "SWE", string.Empty),
            new LookupEntry("nations", "raid", "Viking Raids", "Raiding", "VIN", "Vinland"),
        };

        Assert.Equal(new[] { "swe_adv" }, LookupFilter.Apply(all, "way swed", null, string.Empty).Select(e => e.Key));
        Assert.Equal(new[] { "raid" }, LookupFilter.Apply(all, string.Empty, "nations", string.Empty).Select(e => e.Key));
        Assert.Equal(new[] { "raid" }, LookupFilter.Apply(all, string.Empty, null, "vi").Select(e => e.Key));
        Assert.Equal(2, LookupFilter.Apply(all, "  ", string.Empty, string.Empty).Count);
        Assert.Equal(new[] { "raid" }, LookupFilter.Apply(all, "vinland", null, string.Empty).Select(e => e.Key));
    }
}
