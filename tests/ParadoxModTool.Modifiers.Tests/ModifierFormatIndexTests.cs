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

    [Theory]
    [InlineData("ck3", "MOD_{name}")]
    [InlineData("eu5", "MODIFIER_TYPE_NAME_{name}")]
    [InlineData("eu4", "MODIFIER_{name}")]
    public void RealProfilesHaveNameLocalization(string game, string expectedKey)
    {
        var settings = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", game + ".json")).ModifierFormats!;

        Assert.NotEmpty(settings.NameLocFolders);
        Assert.Contains(expectedKey, settings.NameKeys);
    }
}
