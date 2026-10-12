using System.Text;
using ParadoxModTool.Generators.Lookups;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Einträge und Texte aus dem Spielordner (Spec 2026-10-05-generator-packages-lookups, Abschnitt 1).</summary>
public sealed class GameKeySourceTests : IDisposable
{
    private static readonly UTF8Encoding Bom = new(true);

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string[] Roots => new[] { _temp.PathOf("game") };

    private void Loc(string language, string file, string body) =>
        _temp.Write($"game/main_menu/localization/{language}/{file}", $"l_{language}:\n{body}", Bom);

    private IReadOnlyList<LookupEntry> Read(LookupSourceDef def) => GameKeySource.Read(def, Roots, GameLocalization.Load(Roots, "english"));

    [Fact]
    public void Advances_take_tag_from_file_name_or_block()
    {
        _temp.Write("game/in_game/common/advances/country_SWE.txt", "swe_adv = { }");
        _temp.Write("game/in_game/common/advances/beylik.txt", "# b_old = { }\nb_adv = { potential = { has_or_had_tag = KAR } }");
        _temp.Write("game/in_game/common/advances/0_age.txt", "gen_adv = { }");
        Loc("english", "advances_l_english.yml", " swe_adv:0 \"Swedish \\\"Way\\\"\"\n swe_adv_desc: \"$gen_adv_desc$\"\n gen_adv_desc: \"Generic text\"\n b_adv: \"Beylik\"\n");
        var def = new LookupSourceDef("advances", LookupKind.GameKeys, "Advance", "in_game/common/advances", TagFromFile: "^country_([A-Za-z]{2,5})\\.txt$", TagInBlock: new[] { "has_or_had_tag", "tag" });

        var entries = Read(def).ToDictionary(e => e.Key);

        Assert.Equal(3, entries.Count);
        Assert.Equal(new LookupEntry("advances", "swe_adv", "Swedish \"Way\"", "Generic text", "SWE", string.Empty), entries["swe_adv"]);
        Assert.Equal("KAR", entries["b_adv"].Tag);
        Assert.Equal("Beylik", entries["b_adv"].Name);
        Assert.Equal(new LookupEntry("advances", "gen_adv", "gen_adv", "Generic text", string.Empty, string.Empty), entries["gen_adv"]);
        Assert.Equal(string.Empty, entries["b_adv"].Desc);
    }

    [Fact]
    public void Laws_depth_two_named_only()
    {
        _temp.Write("game/in_game/common/laws/00.txt", "law_a = { potential = { } pol_1 = { country_modifier = { x = 1 } } pol_2 = { } }\nunnamed = { }");
        Loc("english", "laws_l_english.yml", " law_a: \"Law A\"\n pol_1: \"Policy 1\"\n pol_2: \"Policy 2\"\n");
        var def = new LookupSourceDef("laws", LookupKind.GameKeys, "Gesetz", "in_game/common/laws", Depth: 2, NamedOnly: true);

        Assert.Equal(new[] { "law_a", "pol_1", "pol_2" }, Read(def).Select(e => e.Key));
    }

    [Fact]
    public void Depth_one_without_named_only_takes_every_top_block()
    {
        _temp.Write("game/in_game/common/government_reforms/common.txt", "r1 = { allow = { } }\nr2 = { }\nvalue = 3");
        var def = new LookupSourceDef("reforms", LookupKind.GameKeys, "Reform", "in_game/common/government_reforms");

        Assert.Equal(new[] { "r1", "r2" }, Read(def).Select(e => e.Key));
    }

    [Fact]
    public void Localization_reads_versioned_and_plain_keys()
    {
        Loc("english", "a_l_english.yml", " a:0 \"First \\\"quoted\\\"\" # note\n b: \"Second\"\n");

        var loc = GameLocalization.Load(Roots, "english");

        Assert.Equal("First \"quoted\"", loc.Text("a"));
        Assert.Equal("Second", loc.Text("b"));
        Assert.Null(loc.Text("c"));
        Assert.False(loc.IsEmpty);
    }

    [Fact]
    public void Localization_stops_reference_cycles()
    {
        Loc("english", "a_l_english.yml", " a: \"$b$\"\n b: \"$a$\"\n");

        Assert.NotNull(GameLocalization.Load(Roots, "english").Text("a"));
    }

    [Fact]
    public void Desc_falls_back_to_desc_prefix()
    {
        Loc("english", "a_l_english.yml", " desc_k: \"Prefix desc\"\n k2_desc: \"Suffix\"\n desc_k2: \"Not used\"\n");

        var loc = GameLocalization.Load(Roots, "english");

        Assert.Equal("Prefix desc", loc.Desc("k"));
        Assert.Equal("Suffix", loc.Desc("k2"));
        Assert.Null(loc.Desc("k3"));
    }

    [Fact]
    public void Second_language_and_missing_language_folder()
    {
        Loc("german", "sub/advances_l_german.yml", " swe_adv:0 \"Schwedisch\"\n");

        Assert.Equal("Schwedisch", GameLocalization.Load(Roots, "german").Text("swe_adv"));
        Assert.True(GameLocalization.Load(Roots, "french").IsEmpty);
    }

    [Fact]
    public void Unsafe_folder_is_refused()
    {
        _temp.Write("x/outside.txt", "a = { }");
        var def = new LookupSourceDef("s", LookupKind.GameKeys, "S", "../x");

        var error = Assert.Throws<GeneratorOutputException>(() => Read(def));
        Assert.Contains("../x", error.Message);
    }
}
