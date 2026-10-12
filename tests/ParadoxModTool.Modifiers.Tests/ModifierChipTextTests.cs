using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.Modifiers.Tests;

public sealed class ModifierChipTextTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-modifier-chips-" + Guid.NewGuid().ToString("N"));
    private readonly ModifierFormatIndex _index;
    private readonly ModifierNameIndex _names;

    public ModifierChipTextTests()
    {
        var game = Path.Combine(_root, "game");
        Write(game, "defs/00.txt", "global_population_growth = { percent = yes decimals = 2 }\ncan_x = { boolean = yes }");
        Write(game, "localization/english/m_l_english.yml", "l_english:\n MODIFIER_TYPE_NAME_global_population_growth:0 \"Population Growth\"\n MODIFIER_TYPE_NAME_can_x:0 \"Can X\"\n");
        var settings = new ModifierFormatSettings { Source = "definitions", Folders = { "defs" }, NameLocFolders = { "localization" }, NameKeys = { "MODIFIER_TYPE_NAME_{name}" } };
        _index = ModifierFormatIndex.Build(settings, new[] { game }, Array.Empty<string>(), Array.Empty<(string, string)>());
        _names = ModifierNameIndex.Build(settings, _index.AllNames, new[] { game }, Array.Empty<string>(), new[] { "english" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(true));
    }

    [Fact]
    public void Chip_shows_name_and_formatted_value()
    {
        var expected = "Population Growth " + ModifierValueFormatter.Format(0.001m, _index.Find("global_population_growth")!);

        Assert.Equal(new ModifierChip("global_population_growth", expected, true), ModifierChipText.For("\tglobal_population_growth = 0.001", _index, _names));
    }

    [Fact]
    public void Chip_for_unknown_key_is_raw_and_unknown() =>
        Assert.Equal(new ModifierChip("foo_bar", "foo_bar = 0.1", false), ModifierChipText.For("foo_bar = 0.1", _index, _names));

    [Fact]
    public void Chip_for_non_modifier_line_is_raw()
    {
        Assert.Null(ModifierChipText.For("# note", _index, _names));
        Assert.Null(ModifierChipText.For("   ", _index, _names));
        Assert.Equal(new ModifierChip(string.Empty, "weird line", false), ModifierChipText.For("weird line", _index, _names));
    }

    [Fact]
    public void Chip_for_boolean_modifier_shows_name_and_value() =>
        Assert.Equal(new ModifierChip("can_x", "Can X yes", true), ModifierChipText.For("can_x = yes # c", _index, _names));
}
