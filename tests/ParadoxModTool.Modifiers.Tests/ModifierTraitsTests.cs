using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.Modifiers.Tests;

public sealed class ModifierTraitsTests : IDisposable
{
    private const string Definitions = "a = { color = bad percent = yes category = country }\nb = { boolean = yes category = location }\nc = { decimals = 1 }";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-modifier-traits-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Traits_read_bad_boolean_category()
    {
        var traits = ModifierDefinitionReader.ReadTraits(Definitions);

        Assert.Equal(new ModifierTraits(false, true, "country"), traits["a"]);
        Assert.Equal(new ModifierTraits(true, false, "location"), traits["b"]);
        Assert.Equal(new ModifierTraits(false, false, string.Empty), traits["c"]);
    }

    [Fact]
    public void Traits_read_category_inside_game_data()
    {
        // EU V: category steht in game_data, color und boolean auf oberster Ebene des Eintrags.
        var traits = ModifierDefinitionReader.ReadTraits("call_jihad_cost_modifier={\n\tcolor=bad\n\tpercent=yes\n\tgame_data={\n\t\tcategory=country\n\t}\n}");

        Assert.Equal(new ModifierTraits(false, true, "country"), traits["call_jihad_cost_modifier"]);
    }

    [Fact]
    public void Index_keeps_boolean_modifiers_in_all_names_but_not_in_formats()
    {
        var game = Path.Combine(_root, "game");
        Directory.CreateDirectory(Path.Combine(game, "defs"));
        File.WriteAllText(Path.Combine(game, "defs", "00.txt"), Definitions);

        var index = ModifierFormatIndex.Build(new ModifierFormatSettings { Source = "definitions", Folders = { "defs" } }, new[] { game }, Array.Empty<string>(), Array.Empty<(string, string)>());

        Assert.Contains("b", index.AllNames);
        Assert.Contains("a", index.AllNames);
        Assert.Null(index.Find("b"));
        Assert.True(index.Traits["b"].Boolean);
        Assert.True(index.Traits["a"].Bad);
    }
}
