using System.Text;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>
/// Der Einleser (Spec Teil 4) mit einem kleinen Paket: Kategorien adm (Basis 0) und dip (Basis 10), Ideen mit Name/Beschreibung, Stufen mit
/// Modifikatoren und "teuer"; Elemente aus <c>m.txt</c>, Lokalisierung aus <c>loc/&lt;sprache&gt;/l_AAA.yml</c>, Flags aus <c>ga.txt</c>.
/// </summary>
public sealed class GeneratorStateReaderTests : IDisposable
{
    private static readonly string[] Languages = { "english", "german", "french" };

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static readonly ReadDef Read = new(
        @"^m_(?<key>[A-Z]{3})\.txt$",
        new ReadElementsDef("m_{{ tag }}.txt", @"m_{{ tag }}_(?<number>\d+)(?<suffix>_\d)?", "levels",
            new Dictionary<string, ReadFieldDef> { ["modifiers"] = new(true, new[] { "game_data" }) }),
        new[] { new ReadLocalizationDef(@"name_{{ tag }}_(?<number>\d+)", "ideas.name"), new ReadLocalizationDef(@"desc_{{ tag }}_(?<number>\d+)", "ideas.desc") },
        new[] { new ReadFlagDef("ga_{{ tag }}.txt", @"ga_{{ tag }}_(?<number>\d+)(?<suffix>_\d)?", "_expensive_", "levels.expensive") },
        new[] { new ReadAssetDef("coa", "coa/{{ tag }}.dds") });

    private GeneratorPackage Package()
    {
        _temp.Write("package/templates/m.sbn",
            "{{ for c in categories }}{{ for i in c.ideas }}{{ for l in i.levels }}m_{{ tag }}_{{ i.number }}{{ l.suffix }} = {\n\tgame_data = { category = country }\n" +
            "{{ if l.modifiers != \"\" }}\t{{ l.modifiers | string.replace \"\\n\" \"\\n\\t\" }}\n{{ end }}}\n{{ end }}{{ end }}{{ end }}");
        _temp.Write("package/templates/loc.sbn", "x");
        var outputs = new[]
        {
            new OutputDef("m", OutputKind.SyncBlocks, "m_{{ tag }}.txt", Template: "m.sbn", KeyPattern: "m_{{ tag }}_*", Keep: new[] { "game_data" }),
            new OutputDef("loc", OutputKind.Localization, "loc/{{ lang }}/l_{{ tag }}_{{ lang }}.yml", Template: "loc.sbn"),
        };
        var package = SamplePackages.Ideas(folder: _temp.PathOf("package"), outputs: outputs, read: Read, ideaInitial: 3, levelInitial: 4);
        return package with
        {
            Head = new[] { package.Head[0], new FieldDef("coa", "Wappen", FieldType.Image, Asset: "coa") },
            Assets = new Dictionary<string, string> { ["coa"] = "assets/coa.zip" },
        };
    }

    private string Mod => _temp.PathOf("mod");

    private void ModFile(string relative, string text, Encoding? encoding = null) => _temp.Write("mod/" + relative, text, encoding);

    private static List<FormNode> Ideas(GeneratorState state, int group) => state.Head.Children["categories"][group].Children["ideas"];

    [Fact]
    public void Keys_finds_keys_and_ignores_foreign_files()
    {
        ModFile("m_AAA.txt", "m_AAA_1 = { }");
        ModFile("m_BBB.txt", "m_BBB_1 = { }");
        ModFile("readme.txt", "x");

        Assert.Equal(new[] { "AAA", "BBB" }, GeneratorStateReader.Keys(Package(), Mod));
    }

    [Fact]
    public void Elements_create_groups_ideas_and_levels()
    {
        ModFile("m_AAA.txt", "m_AAA_1 = { a = 1 }\nm_AAA_1_1 = { b = 2 }\nm_AAA_11 = { c = 3 }\n");

        var state = GeneratorStateReader.Read(Package(), Mod, "AAA", Languages)!.State;

        Assert.Equal("AAA", state.Key);
        Assert.Equal("AAA", state.Head.Values["tag"]);
        var adm = Assert.Single(Ideas(state, 0));
        Assert.Equal(new[] { "a = 1", "b = 2" }, adm.Children["levels"].Select(l => l.Values["modifiers"]));
        Assert.Equal("c = 3", Assert.Single(Assert.Single(Ideas(state, 1)).Children["levels"]).Values["modifiers"]);
    }

    [Fact]
    public void Number_gaps_are_filled()
    {
        ModFile("m_AAA.txt", "m_AAA_1 = { }\nm_AAA_3 = { }\n");

        var ideas = Ideas(GeneratorStateReader.Read(Package(), Mod, "AAA", Languages)!.State, 0);

        Assert.Equal(3, ideas.Count);
        Assert.Single(ideas[1].Children["levels"]);
        Assert.Equal("no", ideas[1].Children["levels"][0].Values["expensive"]);
    }

    [Fact]
    public void Number_outside_every_group_is_skipped_with_note()
    {
        ModFile("m_AAA.txt", "m_AAA_1 = { }\nm_AAA_99 = { }\n");

        var result = GeneratorStateReader.Read(Package(), Mod, "AAA", Languages)!;

        Assert.Single(Ideas(result.State, 0));
        Assert.Contains(GeneratorsTexts.ReadNumberOutside(99), result.Notes);
    }

    [Fact]
    public void Body_keeps_nested_blocks_and_round_trips()
    {
        const string Text = "m_AAA_1 = {\n\tgame_data = { category = country }\n\tpotential = {\n\t\thas_x = yes\n\t}\n\t# Notiz\n\ta = 1\n}\n";
        ModFile("m_AAA.txt", Text);
        var package = Package();

        var state = GeneratorStateReader.Read(package, Mod, "AAA", Languages)!.State;

        Assert.Equal("potential = {\n\thas_x = yes\n}\n# Notiz\na = 1", Ideas(state, 0)[0].Children["levels"][0].Values["modifiers"]);
        var plan = GeneratorPlanner.Plan(package, state, new GeneratorTarget(Mod, null, Languages));
        Assert.Equal(Patches.PlannedFileStatus.Unchanged, plan.Files.Single(f => f.RelativePath == "m_AAA.txt").Status);
    }

    [Fact]
    public void Localization_fills_english_and_translations()
    {
        ModFile("m_AAA.txt", "m_AAA_1 = { }\n");
        ModFile("loc/english/l_AAA_english.yml", "l_english:\n name_AAA_1: \"Iron\"\n desc_AAA_1: \"Strong\"\n");
        ModFile("loc/german/l_AAA_german.yml", "l_german:\n name_AAA_1: \"Eisen\"\n desc_AAA_1: \"Strong\"\n");

        var state = GeneratorStateReader.Read(Package(), Mod, "AAA", Languages)!.State;

        var idea = Ideas(state, 0)[0];
        Assert.Equal("Iron", idea.Values["name"]);
        Assert.Equal("Eisen", idea.Translations["name"]["german"]);
        Assert.False(idea.Translations.TryGetValue("desc", out var desc) && desc.ContainsKey("german"));
        Assert.Equal(new[] { "english", "german" }, state.Languages);
    }

    [Fact]
    public void Blank_other_language_turns_copy_english_off()
    {
        ModFile("m_AAA.txt", "m_AAA_1 = { }\n");
        ModFile("loc/english/l_AAA_english.yml", "l_english:\n name_AAA_1: \"Iron\"\n");
        ModFile("loc/german/l_AAA_german.yml", "l_german:\n name_AAA_1: \"\"\n");

        var state = GeneratorStateReader.Read(Package() with { Languages = new LanguageDefaults(CopyEnglish: true) }, Mod, "AAA", Languages)!.State;

        Assert.False(state.LanguageRules.CopyEnglish);
    }

    [Fact]
    public void Localization_values_unescape_and_ignore_comments()
    {
        ModFile("m_AAA.txt", "m_AAA_1 = { }\n");
        ModFile("loc/english/l_AAA_english.yml", "l_english:\n desc_AAA_1: \"a \\\"b\\\"\\nc\" # Notiz\n");

        Assert.Equal("a \"b\"\nc", Ideas(GeneratorStateReader.Read(Package(), Mod, "AAA", Languages)!.State, 0)[0].Values["desc"]);
    }

    [Fact]
    public void Reads_crlf_and_bom_files()
    {
        var bom = new UTF8Encoding(true);
        ModFile("m_AAA.txt", "m_AAA_1 = {\r\n\ta = 1\r\n}\r\n", bom);
        ModFile("loc/english/l_AAA_english.yml", "l_english:\r\n name_AAA_1: \"Iron\"\r\n", bom);

        var idea = Ideas(GeneratorStateReader.Read(Package(), Mod, "AAA", Languages)!.State, 0)[0];

        Assert.Equal("Iron", idea.Values["name"]);
        Assert.Equal("a = 1", idea.Children["levels"][0].Values["modifiers"]);
    }

    [Fact]
    public void Flags_set_yes_and_no()
    {
        ModFile("m_AAA.txt", "m_AAA_1 = { }\nm_AAA_1_1 = { }\n");
        ModFile("ga_AAA.txt", "ga_AAA_1 = { text = cost_expensive_adm }\nga_AAA_1_1 = { text = cost_adm }\n");

        var levels = Ideas(GeneratorStateReader.Read(Package(), Mod, "AAA", Languages)!.State, 0)[0].Children["levels"];

        Assert.Equal(new[] { "yes", "no" }, levels.Select(l => l.Values["expensive"]));
    }

    [Fact]
    public void Asset_found_by_bytes_or_left_empty()
    {
        ModFile("m_AAA.txt", "m_AAA_1 = { }\n");
        AssetLibraryTests.Zip(_temp, "package/assets/coa.zip", ("coa/A.dds", new byte[] { 1, 2 }), ("coa/B.dds", new byte[] { 3, 4 }));
        Directory.CreateDirectory(_temp.PathOf("mod/coa"));
        File.WriteAllBytes(_temp.PathOf("mod/coa/AAA.dds"), new byte[] { 3, 4 });

        Assert.Equal("coa/B.dds", GeneratorStateReader.Read(Package(), Mod, "AAA", Languages)!.State.Head.Values["coa"]);

        File.WriteAllBytes(_temp.PathOf("mod/coa/AAA.dds"), new byte[] { 9 });
        Assert.False(GeneratorStateReader.Read(Package(), Mod, "AAA", Languages)!.State.Head.Values.TryGetValue("coa", out var none) && none.Length > 0);
    }

    [Fact]
    public void Broken_file_names_the_file()
    {
        ModFile("m_AAA.txt", "m_AAA_1 = { a = \n");

        var error = Assert.Throws<GeneratorOutputException>(() => GeneratorStateReader.Read(Package(), Mod, "AAA", Languages));
        Assert.Contains("m_AAA.txt", error.Message);
    }

    [Fact]
    public void Key_without_files_returns_null() =>
        Assert.Null(GeneratorStateReader.Read(Package(), Mod, "ZZZ", Languages));

    [Fact]
    public void Take_over_sets_target_mod()
    {
        ModFile("m_AAA.txt", "m_AAA_1 = { }\n");

        var result = GeneratorStateReader.TakeOver(Package(), Mod, new[] { "AAA", "ZZZ" }, _temp.PathOf("target"), Languages);

        var taken = Assert.Single(result.States);
        Assert.Equal(_temp.PathOf("target"), taken.ModRoot);
        Assert.Contains(GeneratorsTexts.ReadKeyMissing("ZZZ"), result.Notes);
    }

    [Fact]
    public void Read_skips_flag_file_outside_mod()
    {
        var package = Package() with { Read = Read with { Flags = new[] { new ReadFlagDef("../ga_{{ tag }}.txt", @"ga_{{ tag }}_(?<number>\d+)(?<suffix>_\d)?", "_expensive_", "levels.expensive") } } };
        ModFile("m_AAA.txt", "m_AAA_1 = { x = 1 }");
        _temp.Write("ga_AAA.txt", "ga_AAA_1 = { _expensive_ = yes }");

        var result = GeneratorStateReader.Read(package, Mod, "AAA", Languages)!;

        Assert.NotEqual("yes", Ideas(result.State, 0)[0].Children["levels"][0].Values.GetValueOrDefault("expensive"));
        Assert.Contains(result.Notes, n => n.Contains("../ga_AAA.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_refuses_elements_file_outside_mod()
    {
        var package = Package() with { Read = Read with { Elements = Read.Elements with { File = "../m_{{ tag }}.txt" } } };
        _temp.Write("m_AAA.txt", "m_AAA_1 = { x = 1 }");

        var error = Assert.Throws<GeneratorOutputException>(() => GeneratorStateReader.Read(package, Mod, "AAA", Languages));
        Assert.Contains("../m_AAA.txt", error.Message);
    }
}
