using ParadoxModTool.Patches;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Schlüsselfeld mit <c>autoNumber</c>: nächste freie Nummer und Weiterzählen bei Hash-Kollision.</summary>
public sealed class AutoNumberTests : IDisposable
{
    private const string GameKey = "TWS_THE";
    private const string ClashKey = "WE_PERFORM_iv2_ga_select_national_idea_army_30_3_ACTION_TITLE";
    private const string LocPath = "in_game/localization/english/iv2_l_english.yml";

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private const string Head = """
        [ { "id": "category", "label": "K", "type": "choice", "choices": [ "adm", "dip", "army" ] },
          { "id": "group", "label": "N", "type": "number", "autoNumber": true } ]
        """;

    private GeneratorPackage Load(string head = Head, string keys = "\"keyFields\": [ \"category\", \"group\" ]")
    {
        _temp.Write("pkg/package.json", $$$"""
            { "id": "pk", "name": "PK", "gameId": "eu5", {{{keys}}}, "head": {{{head}}},
              "outputs": [ { "id": "o", "kind": "ownFile", "path": "x.txt", "template": "o.sbn" } ] }
            """);
        _temp.Write("pkg/templates/o.sbn", "x");
        return GeneratorPackageLoader.Load(_temp.PathOf("pkg"));
    }

    [Fact]
    public void Next_skips_gaps_and_uses_max_plus_one()
    {
        var package = Load();

        var next = AutoNumber.Next(package, new[] { "adm_1", "adm_2", "adm_4", "dip_9" }, new Dictionary<string, string> { ["category"] = "adm" });

        Assert.Equal(5, next);
    }

    [Fact]
    public void Next_is_one_without_taken_keys()
    {
        var package = Load();

        Assert.Equal(1, AutoNumber.Next(package, Array.Empty<string>(), new Dictionary<string, string> { ["category"] = "adm" }));
        Assert.Equal(1, AutoNumber.Next(package, new[] { "dip_9", "adm_x" }, new Dictionary<string, string> { ["category"] = "adm" }));
    }

    [Fact]
    public void Next_works_with_a_single_key_field()
    {
        var package = Load("""[ { "id": "n", "label": "N", "type": "number", "autoNumber": true } ]""", "\"keyField\": \"n\"");

        Assert.Equal(8, AutoNumber.Next(package, new[] { "3", "7" }, new Dictionary<string, string>()));
    }

    [Fact]
    public void Resolve_moves_on_until_no_collision()
    {
        using var game = new TempFolder();
        game.Write(LocPath, $"l_english:\n {GameKey}: \"x\"\n");
        var index = LocalizationKeyIndex.Build(new[] { game.Root });

        var result = AutoNumber.Resolve(5, _ => false, PlanFor, index);

        Assert.Equal(6, result.Number);
        Assert.Equal(new[] { (5, new HashCollision(ClashKey, GameKey, 243924145u)) }, result.Skipped);
        Assert.Equal("k6", result.Plan.Key);
    }

    [Fact]
    public void Resolve_skips_taken_numbers()
    {
        var index = LocalizationKeyIndex.Build(Array.Empty<string>());

        var result = AutoNumber.Resolve(5, n => n is 5 or 6, PlanFor, index);

        Assert.Equal(7, result.Number);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Resolve_throws_after_max_tries()
    {
        using var game = new TempFolder();
        game.Write(LocPath, $"l_english:\n {GameKey}: \"x\"\n");
        var index = LocalizationKeyIndex.Build(new[] { game.Root });

        Assert.Throws<GeneratorOutputException>(() => AutoNumber.Resolve(1, _ => false, _ => ClashPlan(), index, maxTries: 3));
    }

    [Fact]
    public void AutoNumber_only_on_last_number_key_field()
    {
        var onChoice = """
            [ { "id": "category", "label": "K", "type": "choice", "choices": [ "adm" ], "autoNumber": true },
              { "id": "group", "label": "N", "type": "number" } ]
            """;
        var notLast = """
            [ { "id": "group", "label": "N", "type": "number", "pattern": "^[0-9]+$", "autoNumber": true },
              { "id": "category", "label": "K", "type": "text" } ]
            """;
        var notKey = """
            [ { "id": "category", "label": "K", "type": "choice", "choices": [ "adm" ] },
              { "id": "group", "label": "N", "type": "number" },
              { "id": "extra", "label": "E", "type": "number", "autoNumber": true } ]
            """;

        var a = Assert.Throws<GeneratorPackageException>(() => Load(onChoice));
        Assert.Contains("category", a.Message);
        var b = Assert.Throws<GeneratorPackageException>(() => Load(notLast, "\"keyFields\": [ \"group\", \"category\" ]"));
        Assert.Contains("group", b.Message);
        var c = Assert.Throws<GeneratorPackageException>(() => Load(notKey));
        Assert.Contains("extra", c.Message);
    }

    [Fact]
    public void AutoNumber_flag_is_read_on_the_last_number_key_field()
    {
        var package = Load();

        Assert.True(package.Head.Single(f => f.Id == "group").AutoNumber);
        Assert.False(package.Head.Single(f => f.Id == "category").AutoNumber);
    }

    private static ReadDef AnyRead() => new(
        "^x$",
        new ReadElementsDef("x.txt", "x", "levels", new Dictionary<string, ReadFieldDef>()),
        Array.Empty<ReadLocalizationDef>(), Array.Empty<ReadFlagDef>(), Array.Empty<ReadAssetDef>());

    [Fact]
    public void IsNew_needs_a_read_section_and_an_unknown_key()
    {
        var package = Load();
        var withRead = package with { Read = AnyRead() };

        Assert.False(AutoNumber.IsNew(package, "adm_5", Array.Empty<string>()));
        Assert.False(AutoNumber.IsNew(withRead, "adm_5", new[] { "ADM_5" }));
        Assert.True(AutoNumber.IsNew(withRead, "adm_5", new[] { "adm_4" }));
    }

    [Fact]
    public void KeyFor_replaces_the_number_part()
    {
        Assert.Equal("dip_12", AutoNumber.KeyFor(Load(), "dip_3", 12));
    }

    [Fact]
    public void Rekey_moves_the_state_file_when_it_belongs_to_the_state()
    {
        var package = Load();
        var state = StateWithKey(package, "adm_30");
        GeneratorStateStore.Save(package.DataFolder, state);

        AutoNumber.Rekey(package, state, "adm_30", "adm_31");

        Assert.Equal("adm_31", state.Key);
        Assert.Equal("31", state.Head.Values["group"]);
        Assert.Equal(new[] { "adm_31" }, GeneratorStateStore.Keys(package.DataFolder));
    }

    [Fact]
    public void Rekey_keeps_the_stored_file_after_an_unsaved_key_edit()
    {
        var package = Load();
        var state = StateWithKey(package, "adm_30");
        GeneratorStateStore.Save(package.DataFolder, state);
        PackageKey.Apply(package, state, "adm_30");
        state.Head.Values["group"] = "40"; // Formular sagt 40, Datei und state.Key sagen noch 30

        AutoNumber.Rekey(package, state, "adm_40", "adm_41");

        Assert.Equal("adm_41", state.Key);
        Assert.Equal(new[] { "adm_30", "adm_41" }, GeneratorStateStore.Keys(package.DataFolder));
    }

    private static GeneratorState StateWithKey(GeneratorPackage package, string key)
    {
        var state = GeneratorState.CreateEmpty(package, Array.Empty<string>());
        PackageKey.Apply(package, state, key);
        return state;
    }

    private static GeneratorPlan PlanFor(int number) => PlanWith($"k{number}", number == 5 ? ClashKey : $"fresh_key_{number}");

    private static GeneratorPlan ClashPlan() => PlanWith("k", ClashKey);

    private static GeneratorPlan PlanWith(string key, string locKey) => new()
    {
        PackageId = "p",
        Key = key,
        WriteRoot = "w",
        Files = new[]
        {
            new GeneratorPlannedFile(LocPath, PlannedFileStatus.New, null, $"l_english:\n {locKey}: \"t\"\n", null, null, true, "utf-8", Array.Empty<string>(), Array.Empty<string>(), 0, null),
        },
        Warnings = Array.Empty<string>(),
    };
}
