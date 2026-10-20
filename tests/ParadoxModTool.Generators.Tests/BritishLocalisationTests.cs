using System.Text;
using ParadoxModTool.Generators.Lookups;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Flache Spiele mit britischer Schreibweise (Hearts of Iron IV, EU IV, Stellaris): <c>localisation/&lt;sprache&gt;/x_l_&lt;sprache&gt;.yml</c>.</summary>
public sealed class BritishLocalisationTests : IDisposable
{
    private static readonly UTF8Encoding Bom = new(true);

    // Zwei Schlüssel mit demselben Murmur3-Hash (siehe LocalizationHashTests).
    private const string GameKey = "TWS_THE";
    private const string ClashKey = "WE_PERFORM_iv2_ga_select_national_idea_army_30_3_ACTION_TITLE";

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Game_localization_reads_flat_british_folder()
    {
        _temp.Write("game/localisation/english/ideas_l_english.yml", "l_english:\n GER_x:0 \"German Idea\"\n", Bom);

        var loc = GameLocalization.Load(new[] { _temp.PathOf("game") }, "english");

        Assert.Equal("German Idea", loc.Text("GER_x"));
    }

    [Fact]
    public void Key_index_reads_flat_british_folder()
    {
        _temp.Write("game/localisation/english/a_l_english.yml", $"l_english:\n {GameKey}:0 \"A\"\n", Bom);

        var index = LocalizationKeyIndex.Build(new[] { _temp.PathOf("game") });

        Assert.Equal(new[] { GameKey }, index.Others(ClashKey));
    }

    [Fact]
    public void Mod_languages_come_from_british_folders_too()
    {
        _temp.Write("mod/localisation/german/a_l_german.yml", "l_german:\n", Bom);
        _temp.Write("mod/localisation/english/a_l_english.yml", "l_english:\n", Bom);

        Assert.Equal(new[] { "english", "german" }, ParadoxModTool.Generators.Ai.GeneratorCli.ModLanguages(_temp.PathOf("mod")));
    }
}
