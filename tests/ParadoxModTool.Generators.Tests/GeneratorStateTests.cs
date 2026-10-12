using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Formulardaten: leerer Stand aus dem Paket, Speichern und Laden der Arbeitsstände je Schlüssel.</summary>
public sealed class GeneratorStateTests : IDisposable
{
    private static readonly string[] Eu5Languages = { "english", "german", "french" };

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Empty_state_has_one_node_per_constants_row_and_min_items()
    {
        var empty = GeneratorState.CreateEmpty(SamplePackages.Ideas(), Eu5Languages);
        var categories = empty.Head.Children["categories"];
        Assert.Equal(2, categories.Count);
        Assert.Empty(categories[0].Children["ideas"]);
        Assert.Equal("mod", empty.Target);
        Assert.Equal(Eu5Languages, empty.Languages);

        var withMin = GeneratorState.CreateEmpty(SamplePackages.Ideas(ideaMin: 1), Eu5Languages);
        var idea = Assert.Single(withMin.Head.Children["categories"][1].Children["ideas"]);
        var level = Assert.Single(idea.Children["levels"]);
        Assert.Equal("no", level.Values["expensive"]);
    }

    [Fact]
    public void Create_uses_initial_count()
    {
        var empty = GeneratorState.CreateEmpty(SamplePackages.Ideas(ideaInitial: 3, levelInitial: 4), Eu5Languages);
        foreach (var category in empty.Head.Children["categories"])
        {
            Assert.Equal(3, category.Children["ideas"].Count);
            Assert.All(category.Children["ideas"], idea => Assert.Equal(4, idea.Children["levels"].Count));
        }

        var withoutInitial = GeneratorState.CreateEmpty(SamplePackages.Ideas(ideaInitial: 2), Eu5Languages);
        Assert.All(withoutInitial.Head.Children["categories"][0].Children["ideas"], idea => Assert.Single(idea.Children["levels"]));
    }

    [Fact]
    public void Empty_profile_language_list_means_english_only() =>
        Assert.Equal(new[] { "english" }, GeneratorState.CreateEmpty(SamplePackages.Ideas(), Array.Empty<string>()).Languages);

    [Fact]
    public void Save_and_load_round_trip_keeps_values_translations_and_children()
    {
        var state = GeneratorState.CreateEmpty(SamplePackages.Ideas(ideaMin: 1), Eu5Languages);
        state.Key = "BYZ";
        state.Head.Values["tag"] = "BYZ";
        var idea = state.Head.Children["categories"][0].Children["ideas"][0];
        idea.Values["name"] = "Imperial \"Bureaucracy\"";
        idea.Translations["name"] = new Dictionary<string, string> { ["german"] = "Reichsverwaltung" };
        idea.Children["levels"][0].Values["modifiers"] = "a = 1\nb = 2";
        state.LanguageRules = new LanguageDefaults(CopyEnglish: true);
        state.ModRoot = @"C:\mods\IV2";
        var folder = GeneratorStateStore.Folder(_temp.Root, "iv2");

        GeneratorStateStore.Save(folder, state);
        var loaded = GeneratorStateStore.Load(folder, "BYZ")!;

        Assert.Equal(GeneratorStateStore.Serialize(state), GeneratorStateStore.Serialize(loaded));
        var loadedIdea = loaded.Head.Children["categories"][0].Children["ideas"][0];
        Assert.Equal("Reichsverwaltung", loadedIdea.Translations["name"]["german"]);
        Assert.Equal("a = 1\nb = 2", loadedIdea.Children["levels"][0].Values["modifiers"]);
        Assert.True(loaded.LanguageRules.CopyEnglish);
        Assert.Equal(@"C:\mods\IV2", loaded.ModRoot);
    }

    [Fact]
    public void Keys_lists_saved_states_sorted()
    {
        var folder = GeneratorStateStore.Folder(_temp.Root, "iv2");
        foreach (var key in new[] { "FRA", "BYZ" })
        {
            var state = GeneratorState.CreateEmpty(SamplePackages.Ideas(), Eu5Languages);
            state.Key = key;
            GeneratorStateStore.Save(folder, state);
        }

        Assert.Equal(new[] { "BYZ", "FRA" }, GeneratorStateStore.Keys(folder));
    }

    [Fact]
    public void Broken_state_file_loads_as_null_and_is_kept_as_broken()
    {
        var folder = GeneratorStateStore.Folder(_temp.Root, "iv2");
        _temp.Write(Path.GetRelativePath(_temp.Root, Path.Combine(folder, "states", "BYZ.json")), "{ kaputt");

        Assert.Null(GeneratorStateStore.Load(folder, "BYZ"));
        Assert.True(File.Exists(Path.Combine(folder, "states", "BYZ.json.broken")));
        Assert.Empty(GeneratorStateStore.Keys(folder));
    }
}
