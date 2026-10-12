using ParadoxModTool.Generators.Candidates;
using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Stapel-Erzeugung aus Kandidaten im Paket iv2-dynamic-ideas: Nummern, Name, Übersetzungen, Stufen und die Prüfung der Fragen.</summary>
public sealed class CandidateBatchTests : IDisposable
{
    private static readonly string[] Languages = { "english", "german" };

    private readonly TempFolder _temp = new();
    private readonly GeneratorPackage _package;

    public CandidateBatchTests()
    {
        _package = GeneratorPackageLoader.Load(
            Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-dynamic-ideas"), _temp.PathOf("data"), shipped: true, uiLanguage: "en");
    }

    public void Dispose() => _temp.Dispose();

    private static IReadOnlyDictionary<string, string> Ask(string category, string subTab = "") =>
        new Dictionary<string, string> { ["category"] = category, ["subTab"] = subTab };

    private static CandidatePick Pick(string modifier, bool boolean = false, string? english = null, string? german = null)
    {
        var names = new Dictionary<string, string>();
        if (english is not null)
        {
            names["english"] = english;
        }

        if (german is not null)
        {
            names["german"] = german;
        }

        return new CandidatePick(modifier, boolean, names);
    }

    private IReadOnlyList<GeneratorState> Create(IReadOnlyList<CandidatePick> picks, IReadOnlyDictionary<string, string> asked, params string[] taken) =>
        CandidateBatch.Create(_package, picks, asked, taken, Languages, "C:/mod");

    [Fact]
    public void Batch_numbers_follow_mod_and_states_without_duplicates()
    {
        var taken = new[] { "army_1", "army_7", "army_9" };
        var picks = new[] { Pick("a"), Pick("b"), Pick("c") };

        var army = Create(picks, Ask("army"), taken);
        var navy = Create(new[] { Pick("d") }, Ask("navy"), taken);

        Assert.Equal(new[] { "army_10", "army_11", "army_12" }, army.Select(s => s.Key));
        Assert.Equal(new[] { "10", "11", "12" }, army.Select(s => s.Head.Values["number"]));
        Assert.Equal("navy_1", Assert.Single(navy).Key);
    }

    [Fact]
    public void Batch_fills_name_modifiers_and_levels()
    {
        var state = Assert.Single(Create(new[] { Pick("land_morale", english: "Morale", german: "Moral") }, Ask("dip", "dip_3")));

        var levels = state.Head.Children["levels"];
        Assert.Equal(4, levels.Count);
        Assert.All(levels, l => Assert.Equal("land_morale = ", l.Values["modifiers"]));
        Assert.Equal("Morale", state.Head.Values["name"]);
        Assert.Equal("Moral", state.Head.Translations["name"]["german"]);
        Assert.Equal("dip_3", state.Head.Values["subTab"]);
        Assert.Equal("dip", state.Head.Values["category"]);
        Assert.Equal("dip_1", state.Key);
        Assert.Equal("C:/mod", state.ModRoot);
        Assert.Equal(_package.Id, state.PackageId);
        Assert.Equal(Languages, state.Languages);
    }

    [Fact]
    public void Batch_boolean_pick_gets_one_level_yes()
    {
        var state = Assert.Single(Create(new[] { Pick("can_conscript", boolean: true, english: "Can conscript") }, Ask("army")));

        var level = Assert.Single(state.Head.Children["levels"]);
        Assert.Equal("can_conscript = yes", level.Values["modifiers"]);
    }

    [Fact]
    public void Batch_names_per_language_without_fallback()
    {
        var onlyEnglish = Assert.Single(Create(new[] { Pick("land_morale", english: "Morale") }, Ask("army")));
        var none = Assert.Single(Create(new[] { Pick("land_morale") }, Ask("army")));
        var emptyGerman = Assert.Single(Create(new[] { Pick("land_morale", english: "Morale", german: "") }, Ask("army")));

        Assert.False(onlyEnglish.Head.Translations.TryGetValue("name", out var german) && german.ContainsKey("german"));
        Assert.Equal("land_morale", none.Head.Values["name"]);
        Assert.False(none.Head.Translations.TryGetValue("name", out german) && german.ContainsKey("german"));
        Assert.False(emptyGerman.Head.Translations.TryGetValue("name", out german) && german.ContainsKey("german"));
    }

    [Fact]
    public void Ask_issues_block_invalid_sub_tab()
    {
        var renderer = new GeneratorRenderer(_package);

        var army = Assert.Single(CandidateBatch.AskIssues(_package, renderer, Ask("army", "dip_1"), Languages));
        Assert.Contains("no sub-tab", army);
        Assert.Empty(CandidateBatch.AskIssues(_package, renderer, Ask("dip", "dip_1"), Languages));
        Assert.Empty(CandidateBatch.AskIssues(_package, renderer, Ask("army"), Languages));
    }
}
