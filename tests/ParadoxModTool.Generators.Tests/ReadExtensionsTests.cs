using ParadoxModTool.Generators.Editing.Package;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Einlesen: Schlüssel aus Blöcken, Kopf-Felder aus der Lokalisierung, gesammelte Werte (Spec 2026-10-06-generator-packages-idea-groups, Abschnitt 4).</summary>
public sealed class ReadExtensionsTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private const string Json = """
        {
          "id": "rx", "name": "RX", "gameId": "eu5", "keyFields": [ "category", "group" ],
          "head": [
            { "id": "category", "label": "K", "type": "choice", "choices": [ "adm", "dip" ] },
            { "id": "group", "label": "N", "type": "number" },
            { "id": "title", "label": "T", "type": "text", "multilingual": true },
            { "id": "religions", "label": "R", "type": "multilineText" }
          ],
          "root": { "id": "ideas", "label": "I", "min": 0, "max": 7, "fields": [ { "id": "modifiers", "label": "M", "type": "modifiers" } ] },
          "outputs": [
            { "id": "names", "kind": "localization", "path": "loc/{{ lang }}/n_l_{{ lang }}.yml", "template": "n.sbn", "keys": "^t_{{ category }}_{{ group }}$" },
            { "id": "m", "kind": "ownFile", "path": "m_{{ category }}.txt", "template": "n.sbn" }
          ],
          "read": {
            "keys": { "file": "^m_(adm|dip)\\.txt$", "block": "m_(?<category>adm|dip)_(?<group>\\d+)_1" },
            "elements": { "file": "m_{{ category }}.txt", "block": "m_{{ category }}_{{ group }}_(?<number>\\d+)", "level": "ideas",
                          "fields": { "modifiers": { "body": true } } },
            "localization": [ { "key": "t_{{ category }}_{{ group }}", "field": "title" } ],
            "values": [ { "file": "ga_{{ category }}.txt", "block": "ga_{{ category }}_{{ group }}", "pattern": "religion = religion:(?<value>\\w+)", "field": "religions" } ]
          }
        }
        """;

    private GeneratorPackage Load()
    {
        _temp.Write("pkg/package.json", Json);
        _temp.Write("pkg/templates/n.sbn", "x\n");
        return GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
    }

    private string Mod => _temp.PathOf("mod");

    [Fact]
    public void Keys_from_block_keys_with_named_groups()
    {
        _temp.Write("mod/m_adm.txt", "m_adm_1_1 = {}\nm_adm_1_2 = {}\nm_adm_2_1 = {}\n");
        _temp.Write("mod/m_dip.txt", "m_dip_1_1 = {}\n");
        _temp.Write("mod/other.txt", "m_adm_9_1 = {}\n");

        Assert.Equal(new[] { "adm_1", "adm_2", "dip_1" }, GeneratorStateReader.Keys(Load(), Mod));
    }

    [Fact]
    public void Composite_key_sets_head_fields_and_elements()
    {
        _temp.Write("mod/m_adm.txt", "m_adm_2_1 = {\n\ta = 1\n}\nm_adm_2_2 = {\n\tb = 2\n}\n");

        var state = GeneratorStateReader.Read(Load(), Mod, "adm_2", new[] { "english" })!.State;

        Assert.Equal("adm", state.Head.Values["category"]);
        Assert.Equal("2", state.Head.Values["group"]);
        Assert.Equal(new[] { "a = 1", "b = 2" }, state.Head.Children["ideas"].Select(i => i.Values["modifiers"]));
    }

    [Fact]
    public void Head_field_read_from_localization()
    {
        _temp.Write("mod/m_adm.txt", "m_adm_1_1 = {}\n");
        _temp.Write("mod/loc/english/n_l_english.yml", "l_english:\n t_adm_1: \"Title\"\n t_adm_10: \"No\"\n");
        _temp.Write("mod/loc/german/n_l_german.yml", "l_german:\n t_adm_1: \"Titel\"\n");

        var state = GeneratorStateReader.Read(Load(), Mod, "adm_1", new[] { "english", "german" })!.State;

        Assert.Equal("Title", state.Head.Values["title"]);
        Assert.Equal("Titel", state.Head.Translations["title"]["german"]);
    }

    [Fact]
    public void Values_collects_hits_in_order_without_duplicates()
    {
        _temp.Write("mod/m_adm.txt", "m_adm_1_1 = {}\n");
        _temp.Write("mod/ga_adm.txt", "ga_adm_1 = {\n\tOR = {\n\t\treligion = religion:a\n\t\treligion = religion:b\n\t\treligion = religion:a\n\t}\n}\nga_adm_10 = { religion = religion:z }\n");

        var state = GeneratorStateReader.Read(Load(), Mod, "adm_1", new[] { "english" })!.State;

        Assert.Equal("a\nb", state.Head.Values["religions"]);
    }

    [Fact]
    public void Values_with_single_hit()
    {
        _temp.Write("mod/m_adm.txt", "m_adm_1_1 = {}\n");
        _temp.Write("mod/ga_adm.txt", "ga_adm_1 = { potential = { religion = religion:catholic } }\n");

        var state = GeneratorStateReader.Read(Load(), Mod, "adm_1", new[] { "english" })!.State;

        Assert.Equal("catholic", state.Head.Values["religions"]);
    }

    [Fact]
    public void Values_body_reads_a_sub_block_dedented()
    {
        _temp.Write("pkg/package.json", Json.Replace(
            "\"values\": [ ",
            "\"values\": [ { \"file\": \"ga_{{ category }}.txt\", \"block\": \"ga_{{ category }}_{{ group }}\", \"body\": \"allow\", \"field\": \"title\" }, "));
        _temp.Write("pkg/templates/n.sbn", "x\n");
        var package = GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
        _temp.Write("mod/m_adm.txt", "m_adm_1_1 = {}\n");
        _temp.Write("mod/ga_adm.txt", "ga_adm_1 = {\n\tallow = {\n\t\tscope:actor = {\n\t\t\tx = yes\n\t\t}\n\t}\n\teffect = { }\n}\n");

        var state = GeneratorStateReader.Read(package, Mod, "adm_1", new[] { "english" })!.State;

        Assert.Equal("scope:actor = {\n\tx = yes\n}", state.Head.Values["title"]);
    }

    [Fact]
    public void Flag_can_fill_a_head_field()
    {
        _temp.Write("pkg/package.json", Json.Replace(
            "\"values\": [ ",
            "\"flags\": [ { \"file\": \"ga_{{ category }}.txt\", \"block\": \"ga_{{ category }}_{{ group }}\", \"contains\": \"reli_var\", \"field\": \"title\" } ], \"values\": [ "));
        _temp.Write("pkg/templates/n.sbn", "x\n");
        var package = GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
        _temp.Write("mod/m_adm.txt", "m_adm_1_1 = {}\n");
        _temp.Write("mod/ga_adm.txt", "ga_adm_1 = { set_variable = reli_var }\n");

        var state = GeneratorStateReader.Read(package, Mod, "adm_1", new[] { "english" })!.State;

        Assert.Equal("yes", state.Head.Values["title"]);
    }

    [Fact]
    public void Flag_pattern_respects_number_boundaries()
    {
        _temp.Write("pkg/package.json", Json.Replace(
            "\"values\": [ ",
            "\"flags\": [ { \"file\": \"ai.txt\", \"block\": \"list_{{ category }}\", \"pattern\": \"pick_{{ category }}_{{ group }}(?![0-9])\", \"field\": \"title\" } ], \"values\": [ "));
        _temp.Write("pkg/templates/n.sbn", "x\n");
        var package = GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
        _temp.Write("mod/m_adm.txt", "m_adm_1_1 = {}\nm_adm_2_1 = {}\n");
        _temp.Write("mod/ai.txt", "list_adm = {\n\tpick_adm_10\n\tpick_adm_2\n}\n");

        Assert.Equal("no", GeneratorStateReader.Read(package, Mod, "adm_1", new[] { "english" })!.State.Head.Values["title"]);
        Assert.Equal("yes", GeneratorStateReader.Read(package, Mod, "adm_2", new[] { "english" })!.State.Head.Values["title"]);
    }

    [Fact]
    public void Flags_and_values_see_gui_header_blocks()
    {
        // GUI-Kopf "types shells { }" hat kein "=": der Block heißt nach seiner Kopfzeile
        _temp.Write("pkg/package.json", Json.Replace(
            "\"values\": [ { \"file\": \"ga_{{ category }}.txt\"",
            "\"flags\": [ { \"file\": \"s.gui\", \"block\": \"types shells\", \"pattern\": \"(?m)^\\\\s*#\\\\s*pick_{{ category }}_{{ group }} = \\\\{\\\\}\", \"field\": \"title\" } ], " +
            "\"values\": [ { \"file\": \"s.gui\", \"block\": \"types shells\", \"pattern\": \"#\\\\s*pick_{{ category }}_{{ group }} = \\\\{\\\\} # (?<value>[^\\\\n]*)\", \"field\": \"religions\" }, { \"file\": \"ga_{{ category }}.txt\""));
        _temp.Write("pkg/templates/n.sbn", "x\n");
        var package = GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
        _temp.Write("mod/m_adm.txt", "m_adm_1_1 = {}\nm_adm_2_1 = {}\n");
        _temp.Write("mod/s.gui", "types shells {\n\ttype shell_adm = widget {\n\t\tpick_adm_1 = {}\n\t\t#pick_adm_2 = {} # off\n\t}\n}\n");

        var first = GeneratorStateReader.Read(package, Mod, "adm_1", new[] { "english" })!.State;
        var second = GeneratorStateReader.Read(package, Mod, "adm_2", new[] { "english" })!.State;

        Assert.Equal("no", first.Head.Values["title"]);
        Assert.Equal("yes", second.Head.Values["title"]);
        Assert.Equal("off", second.Head.Values["religions"]);
    }

    [Fact]
    public void Document_round_trips_keys_block_and_values()
    {
        Load();
        var document = PackageDocument.Load(_temp.PathOf("pkg"));
        var read = document.Read!;

        Assert.Equal("^m_(adm|dip)\\.txt$", read.Keys);
        Assert.Equal("m_(?<category>adm|dip)_(?<group>\\d+)_1", read.KeysBlock);
        Assert.Equal("religion = religion:(?<value>\\w+)", read.Values.Items.Single().Get("pattern"));

        read.Keys = "^x\\.txt$";
        Assert.Equal("m_(?<category>adm|dip)_(?<group>\\d+)_1", read.KeysBlock);
        read.KeysBlock = string.Empty;
        Assert.Equal("^x\\.txt$", document.Json["read"]!["keys"]!.GetValue<string>());
    }

    /// <summary>Paket mit einer zweiten Lokalisierungs-Ausgabe "extra" und einem Eintrag, der daraus liest.</summary>
    private GeneratorPackage LoadWithSecondLocalization(string output = "extra")
    {
        _temp.Write("pkg/package.json", Json
            .Replace("{ \"id\": \"religions\"", "{ \"id\": \"note\", \"label\": \"X\", \"type\": \"text\", \"multilingual\": true },\n    { \"id\": \"religions\"")
            .Replace("\"outputs\": [", "\"outputs\": [\n    { \"id\": \"extra\", \"kind\": \"localization\", \"path\": \"loc/{{ lang }}/x_l_{{ lang }}.yml\", \"template\": \"n.sbn\", \"keys\": \"^x_\" },")
            .Replace("\"localization\": [ { \"key\": \"t_{{ category }}_{{ group }}\", \"field\": \"title\" } ]",
                "\"localizationOutput\": \"names\", \"localization\": [ { \"key\": \"t_{{ category }}_{{ group }}\", \"field\": \"title\" }, { \"key\": \"x_{{ category }}_{{ group }}\", \"field\": \"note\", \"output\": \"" + output + "\" } ]"));
        _temp.Write("pkg/templates/n.sbn", "x\n");
        return GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
    }

    [Fact]
    public void Localization_entry_reads_from_its_own_output()
    {
        var package = LoadWithSecondLocalization();
        _temp.Write("mod/m_adm.txt", "m_adm_1_1 = {}\n");
        _temp.Write("mod/loc/english/n_l_english.yml", "l_english:\n t_adm_1: \"T\"\n");
        _temp.Write("mod/loc/english/x_l_english.yml", "l_english:\n x_adm_1: \"X\"\n");

        var head = GeneratorStateReader.Read(package, Mod, "adm_1", new[] { "english" })!.State.Head;

        Assert.Equal("T", head.Values["title"]);
        Assert.Equal("X", head.Values["note"]);
    }

    [Fact]
    public void Localization_output_must_be_a_localization_output()
    {
        var error = Assert.Throws<GeneratorPackageException>(() => LoadWithSecondLocalization("m"));

        Assert.Contains("read.localization", error.Message);
    }

    [Fact]
    public void Checker_reports_unknown_localization_output()
    {
        LoadWithSecondLocalization();
        var document = PackageDocument.Load(_temp.PathOf("pkg"));
        document.Read!.Localization.Items[1].Set("output", "nope");

        var issues = PackageChecker.Check(document, new PackageFiles(_temp.PathOf("pkg")), "en");

        Assert.Contains(issues, i => i.Where == "read.localization[2].output");
    }
}
