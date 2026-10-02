using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

/// <summary>Die Testzeilen des früheren ErrorLogParser, wortwörtlich aus echten EU-V-Logs, jetzt gegen LogReader + Katalog.</summary>
public class ErrorLogRegressionTests
{
    [Fact]
    public void ParsingError_ExtrahiertDateiUndZeile()
    {
        var entry = Assert.Single(Catalogs.Classify(
            "[13:53:23][pdx_persistent_reader.cpp:289]: Error: \"Unexpected token: text, near line: 6\" in file: \"input_profile/z_cmf.profile\" near line: 6"));

        Assert.Equal("PAR-001", entry.Code);
        Assert.Equal("input_profile/z_cmf.profile", entry.FilePath);
        Assert.Equal(6, entry.FileLine);
        Assert.True(entry.HasFileReference);
    }

    [Fact]
    public void GuiFehler_AusZweiQuellen_WirdZuEinemEintragZusammengefuehrt()
    {
        var entry = Assert.Single(Catalogs.Classify(
            "[12:43:18][pdx_gui_widget_desc.h:18]: gui/Idea Variation 2 Idea Group Types/IV2_IG_types_adm.gui:145 - Duplicate property 'enabled'(773)",
            "[12:43:18][pdx_gui_factory.cpp:290]: failed reading property, at gui/Idea Variation 2 Idea Group Types/IV2_IG_types_adm.gui:145"));

        Assert.Equal("GUI-002", entry.Code);
        Assert.Equal("gui/Idea Variation 2 Idea Group Types/IV2_IG_types_adm.gui", entry.FilePath);
        Assert.Equal(145, entry.FileLine);
        Assert.Contains("Duplicate property", entry.Message);
        Assert.Contains("failed reading property", entry.Message);
        Assert.Equal(2, entry.Lines.Count);
    }

    [Fact]
    public void SymbolischerFehler_HatKeinenDateibezug()
    {
        var entry = Assert.Single(Catalogs.Classify(
            "[12:43:21][message_handler.cpp:434]: Failed to find message type: WE_PERFORM_assert_patronato_real_ACTION."));

        Assert.Equal("MSG-001", entry.Code);
        Assert.False(entry.HasFileReference);
        Assert.Equal("WE_PERFORM_assert_patronato_real_ACTION", entry.Name);
    }

    [Fact]
    public void MehrzeiligerLaufzeitfehler_WirdAlsEinBlockGeparst()
    {
        var entry = Assert.Single(Catalogs.Classify(
            "[12:45:54][jomini_script_system.cpp:256]: Script system error!",
            "  Error: Event target link 'government_type' returned an unset scope",
            "  Script location: common/building_types/01_IV2_idea_buildings.txt:839",
            ""));

        Assert.Equal("RUN-001", entry.Code);
        Assert.Equal("common/building_types/01_IV2_idea_buildings.txt", entry.FilePath);
        Assert.Equal(839, entry.FileLine);
        Assert.Contains("unset scope", entry.Detail);
    }

    [Fact]
    public void VorschlagsZeilen_WerdenAnVorherigenEintragAngehaengt()
    {
        var entry = Assert.Single(Catalogs.Classify(
            "[12:43:21][message_handler.cpp:434]: Failed to find message type: WE_PERFORM_assert_patronato_real_ACTION.",
            "[12:43:24][jomini_effect.cpp:1207]:  Possible match: 'iv2_nat_FLO_2'",
            "[12:43:24][jomini_effect.cpp:1207]:  Possible match: 'iv2_nat_FLO_1'"));

        Assert.Equal(new[] { "iv2_nat_FLO_2", "iv2_nat_FLO_1" }, entry.Candidates);
    }

    [Fact]
    public void LeereZeilenWerdenUebersprungen()
    {
        var entry = Assert.Single(Catalogs.Classify(
            "",
            "   ",
            "[13:53:22][dlc_reloadable.cpp:1581]: Could not find item in store backend. Item: 4091680"));

        Assert.Equal("DLC-001", entry.Code);
    }
}
