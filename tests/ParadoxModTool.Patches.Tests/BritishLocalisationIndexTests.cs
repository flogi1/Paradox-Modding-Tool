using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>Vanilla-Texte bei Spielen mit britischer Schreibweise (<c>localisation/&lt;sprache&gt;</c>, z. B. Hearts of Iron IV).</summary>
public sealed class BritishLocalisationIndexTests
{
    [Fact]
    public void Vanilla_index_reads_british_folder()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("localisation/english/ideas_l_english.yml", "l_english:\n GER_x:0 \"German Idea\"\n");

        Assert.Equal("German Idea", LocalizationIndex.LoadVanilla(mod.VanillaRoot, "english").Value("GER_x"));
    }
}
