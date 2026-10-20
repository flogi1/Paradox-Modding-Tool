using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.Modifiers.Tests;

public sealed class ModifierFormatIndexTests : IDisposable
{
    private const string Folder = "x/defs";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-modifier-index-" + Guid.NewGuid().ToString("N"));

    private string Game => Path.Combine(_root, "game");

    private string Mod => Path.Combine(_root, "mod");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static ModifierFormatSettings Definitions() => new() { Source = "definitions", Folders = { Folder } };

    private static void Write(string root, string file, string text)
    {
        var dir = Path.Combine(root, "x", "defs");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, file), text);
    }

    private ModifierFormatIndex Build(ModifierFormatSettings? settings) =>
        ModifierFormatIndex.Build(settings, new[] { Game }, new[] { Mod }, Array.Empty<(string, string)>());

    [Fact]
    public void DefinitionsFromGameAndModTheModWins()
    {
        Write(Game, "00.txt", "m={ percent=yes }");
        Write(Mod, "zz.txt", "m={ decimals=0 }");

        var m = Build(Definitions()).Find("m")!;

        Assert.False(m.Percent);
        Assert.Equal(0, m.Decimals);
    }

    [Fact]
    public void FilesAreReadInAlphabeticalOrder()
    {
        Write(Game, "01.txt", "m={ percent=yes }");
        Write(Game, "00.txt", "m={ decimals=1 }");

        Assert.True(Build(Definitions()).Find("m")!.Percent);
    }

    [Fact]
    public void FormatFactorAndDefaults()
    {
        Write(Game, "00.txt", "a={ game_data={ format=\"FormatManPower\" } } b={ game_data={ format=\"FormatUnknown\" } }");
        var settings = Definitions();
        settings.DefaultDecimals = 2;
        settings.Formats["FormatManPower"] = new ModifierFormatFactor { Factor = 1000 };

        var index = Build(settings);

        Assert.Equal(1000, index.Find("a")!.Factor);
        Assert.Equal(2, index.Find("a")!.Decimals);
        Assert.False(index.Find("a")!.Estimated);
        Assert.Equal(1, index.Find("b")!.Factor);
    }

    [Fact]
    public void NameLookupIgnoresCase()
    {
        Write(Game, "00.txt", "m={ percent=yes }");

        Assert.NotNull(Build(Definitions()).Find("M"));
    }

    [Fact]
    public void Names_ListsAllModifiers()
    {
        var wiki = new[] { ("global_tax", "Multiplicative"), ("army_morale", "Additive") };

        var index = ModifierFormatIndex.Build(new ModifierFormatSettings { Source = "wiki" }, Array.Empty<string>(), Array.Empty<string>(), wiki);

        Assert.Equal(new[] { "army_morale", "global_tax" }, index.Names.OrderBy(n => n, StringComparer.Ordinal));
        Assert.Empty(ModifierFormatIndex.Empty.Names);
    }

    [Fact]
    public void WikiMultiplicativeIsPercentAdditiveIsPlain()
    {
        var wiki = new[] { ("a", "Multiplicative"), ("b", "Additive"), ("c", "Constant"), ("d", "Additive / Multiplicative") };

        var index = ModifierFormatIndex.Build(new ModifierFormatSettings { Source = "wiki" }, Array.Empty<string>(), Array.Empty<string>(), wiki);

        Assert.True(index.Find("a")!.Percent);
        Assert.True(index.Find("a")!.Estimated);
        Assert.False(index.Find("b")!.Percent);
        Assert.True(index.Find("b")!.Estimated);
        Assert.Null(index.Find("c"));
        Assert.Null(index.Find("d"));
    }

    [Fact]
    public void NoneMissingSettingsOrMissingFolderGiveAnEmptyIndex()
    {
        Assert.Equal(0, Build(null).Count);
        Assert.Equal(0, Build(new ModifierFormatSettings { Source = "none" }).Count);
        Assert.Equal(0, Build(Definitions()).Count);
    }

    [Fact]
    public void RealProfilesHaveModifierFormats()
    {
        var eu5 = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "eu5.json")).ModifierFormats!;
        var eu4 = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "eu4.json")).ModifierFormats!;

        Assert.Equal("definitions", eu5.Source);
        Assert.Contains("main_menu/common/modifier_type_definitions", eu5.Folders);
        Assert.Equal(1000, eu5.Formats["FormatPopCaps"].Factor);
        Assert.Equal("wiki", eu4.Source);
    }

    [Fact]
    public void Ck3_profile_reads_modifier_definition_formats()
    {
        var settings = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "ck3.json")).ModifierFormats!;
        var dir = Path.Combine(Game, "common", "modifier_definition_formats");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "00_definitions.txt"),
            "diplomacy = {\n\tdecimals = 0\n\tprefix = MOD_DIPLOMACY_PREFIX\n}\nmonthly_prestige_gain_mult = {\n\tpercent = yes\n\tcolor = good\n}\n");

        var index = Build(settings);

        Assert.Equal("definitions", settings.Source);
        Assert.Equal(0, index.Find("diplomacy")!.Decimals);
        Assert.True(index.Find("monthly_prestige_gain_mult")!.Percent);
    }

    [Fact]
    public void Vic3_profile_reads_modifier_type_definitions()
    {
        var settings = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "vic3.json")).ModifierFormats!;
        var dir = Path.Combine(Game, "common", "modifier_type_definitions");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "00_modifier_types.txt"),
            "battle_casualties_mult={\n\tdecimals=0\n\tcolor=bad\n\tpercent=yes\n\tgame_data={\n\t\tai_value=0\n\t}\n}\n");

        var format = Build(settings).Find("battle_casualties_mult")!;

        Assert.Equal("definitions", settings.Source);
        Assert.True(format.Percent);
        Assert.Equal(0, format.Decimals);
    }

    [Theory]
    [InlineData("ck3", "MOD_{name}")]
    [InlineData("eu5", "MODIFIER_TYPE_NAME_{name}")]
    [InlineData("eu4", "MODIFIER_{name}")]
    [InlineData("vic3", "{name}")]
    [InlineData("hoi4", "MODIFIER_{name}")]
    public void RealProfilesHaveNameLocalization(string game, string expectedKey)
    {
        var settings = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", game + ".json")).ModifierFormats!;

        Assert.NotEmpty(settings.NameLocFolders);
        Assert.Contains(expectedKey, settings.NameKeys);
    }

    [Fact]
    public void Suffix_source_estimates_percent_and_plain_numbers_from_the_name()
    {
        var settings = new ModifierFormatSettings
        {
            Source = "suffix",
            Suffixes = new Dictionary<string, string> { ["_mult"] = "percent", ["_add"] = "number" },
        };
        var names = new[] { ("ship_hull_mult", ""), ("ship_hull_add", ""), ("pop_happiness", "") };

        var index = ModifierFormatIndex.Build(settings, Array.Empty<string>(), Array.Empty<string>(), names);

        Assert.True(index.Find("ship_hull_mult")!.Percent);
        Assert.True(index.Find("ship_hull_mult")!.Estimated);
        Assert.False(index.Find("ship_hull_add")!.Percent);
        Assert.True(index.Find("ship_hull_add")!.Estimated);
        Assert.Null(index.Find("pop_happiness"));
        Assert.Equal(2, index.Count);
    }

    [Fact]
    public void Suffix_uses_doc_decimals()
    {
        var settings = new ModifierFormatSettings { Source = "suffix", Suffixes = new Dictionary<string, string> { ["_factor"] = "percent" } };
        var decimals = new Dictionary<string, int> { ["army_org_factor"] = 1 };

        var index = ModifierFormatIndex.Build(
            settings, Array.Empty<string>(), Array.Empty<string>(), new[] { ("army_org_factor", "army"), ("research_speed_factor", "") }, decimals);

        Assert.True(index.Find("army_org_factor")!.Percent);
        Assert.Equal(1, index.Find("army_org_factor")!.Decimals);
        Assert.True(index.Find("army_org_factor")!.Estimated);
        Assert.Equal(2, index.Find("research_speed_factor")!.Decimals);
    }

    [Fact]
    public void Suffix_then_definitions_override()
    {
        Write(Game, "lar.txt", "operation_outcome = {\n\tcolor_type = good\n\tvalue_type = percentage\n\tprecision = 0\n\tcategory = intelligence_agency\n}\n");
        var settings = new ModifierFormatSettings
        {
            Source = "suffix",
            Suffixes = new Dictionary<string, string> { ["_factor"] = "percent" },
            Folders = { Folder },
        };

        var index = ModifierFormatIndex.Build(settings, new[] { Game }, new[] { Mod }, new[] { ("operation_outcome", "intelligence_agency") });

        var format = index.Find("operation_outcome")!;
        Assert.True(format.Percent);
        Assert.Equal(0, format.Decimals);
        Assert.False(format.Estimated);
    }

    [Fact]
    public void Suffix_source_without_folders_reads_no_definitions()
    {
        Write(Game, "a.txt", "ship_x = { percent = yes decimals = 3 }\n");
        var settings = new ModifierFormatSettings { Source = "suffix", Suffixes = new Dictionary<string, string> { ["_mult"] = "percent" } };

        var index = ModifierFormatIndex.Build(settings, new[] { Game }, new[] { Mod }, Array.Empty<(string, string)>());

        Assert.Null(index.Find("ship_x"));
    }

    [Fact]
    public void Suffix_source_prefers_the_longest_matching_suffix()
    {
        var settings = new ModifierFormatSettings
        {
            Source = "suffix",
            Suffixes = new Dictionary<string, string> { ["_add"] = "number", ["_mult_add"] = "percent" },
        };

        var index = ModifierFormatIndex.Build(settings, Array.Empty<string>(), Array.Empty<string>(), new[] { ("x_mult_add", "") });

        Assert.True(index.Find("x_mult_add")!.Percent);
    }

    [Fact]
    public void Suffix_source_lists_every_modifier_with_its_category_even_without_a_format()
    {
        var settings = new ModifierFormatSettings { Source = "suffix", Suffixes = new Dictionary<string, string> { ["_mult"] = "percent" } };
        var names = new[] { ("ship_hull_mult", "Military Ships"), ("pop_happiness", "Pops") };

        var index = ModifierFormatIndex.Build(settings, Array.Empty<string>(), Array.Empty<string>(), names);

        Assert.Equal(new[] { "pop_happiness", "ship_hull_mult" }, index.AllNames.OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("Pops", index.Traits["pop_happiness"].Category);
        Assert.False(index.Traits["pop_happiness"].Boolean);
        Assert.Equal("Military Ships", index.Traits["ship_hull_mult"].Category);
    }
}
