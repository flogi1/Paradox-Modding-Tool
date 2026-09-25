using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

/// <summary>
/// Alle Testzeilen sind wortwörtlich aus echten EU5-Error-Logs entnommen
/// (siehe Architektur-Spezifikation, Abschnitt 9) — keine konstruierten Beispiele.
/// </summary>
public class ErrorLogParserTests
{
    [Fact]
    public void ParsingError_ExtrahiertDateiUndZeile()
    {
        var lines = new[]
        {
            "[13:53:23][pdx_persistent_reader.cpp:289]: Error: \"Unexpected token: text, near line: 6\" in file: \"input_profile/z_cmf.profile\" near line: 6"
        };

        var result = ErrorLogParser.Parse(lines);

        var entry = Assert.Single(result);
        Assert.Equal(LogEntryKind.ParsingError, entry.Kind);
        Assert.Equal("input_profile/z_cmf.profile", entry.FilePath);
        Assert.Equal(6, entry.LineNumber);
        Assert.True(entry.HasFileReference);
    }

    [Fact]
    public void GuiFehler_AusZweiQuellen_WirdZuEinemEintragZusammengefuehrt()
    {
        var lines = new[]
        {
            "[12:43:18][pdx_gui_widget_desc.h:18]: gui/Idea Variation 2 Idea Group Types/IV2_IG_types_adm.gui:145 - Duplicate property 'enabled'(773)",
            "[12:43:18][pdx_gui_factory.cpp:290]: failed reading property, at gui/Idea Variation 2 Idea Group Types/IV2_IG_types_adm.gui:145"
        };

        var result = ErrorLogParser.Parse(lines);

        var entry = Assert.Single(result);
        Assert.Equal(LogEntryKind.GuiError, entry.Kind);
        Assert.Equal("gui/Idea Variation 2 Idea Group Types/IV2_IG_types_adm.gui", entry.FilePath);
        Assert.Equal(145, entry.LineNumber);
        Assert.Contains("Duplicate property", entry.Message);
        Assert.Contains("failed reading property", entry.Message);
    }

    [Fact]
    public void SymbolischerFehler_HatKeinenDateibezug()
    {
        var lines = new[]
        {
            "[12:43:21][message_handler.cpp:434]: Failed to find message type: WE_PERFORM_assert_patronato_real_ACTION."
        };

        var result = ErrorLogParser.Parse(lines);

        var entry = Assert.Single(result);
        Assert.Equal(LogEntryKind.SymbolicError, entry.Kind);
        Assert.False(entry.HasFileReference);
        Assert.Contains("WE_PERFORM_assert_patronato_real_ACTION", entry.Message);
    }

    [Fact]
    public void MehrzeiligerLaufzeitfehler_WirdAlsEinBlockGeparst()
    {
        var lines = new[]
        {
            "[12:45:54][jomini_script_system.cpp:256]: Script system error!",
            "  Error: Event target link 'government_type' returned an unset scope",
            "  Script location: common/building_types/01_IV2_idea_buildings.txt:839",
            ""
        };

        var result = ErrorLogParser.Parse(lines);

        var entry = Assert.Single(result);
        Assert.Equal(LogEntryKind.ScriptRuntimeError, entry.Kind);
        Assert.Equal("common/building_types/01_IV2_idea_buildings.txt", entry.FilePath);
        Assert.Equal(839, entry.LineNumber);
        Assert.Contains("unset scope", entry.Message);
    }

    [Fact]
    public void VorschlagsZeilen_WerdenAnVorherigenEintragAngehaengt()
    {
        var lines = new[]
        {
            "[12:43:21][message_handler.cpp:434]: Failed to find message type: WE_PERFORM_assert_patronato_real_ACTION.",
            "[12:43:24][jomini_effect.cpp:1207]:  Possible match: 'iv2_nat_FLO_2'",
            "[12:43:24][jomini_effect.cpp:1207]:  Possible match: 'iv2_nat_FLO_1'"
        };

        var result = ErrorLogParser.Parse(lines);

        var entry = Assert.Single(result);
        Assert.Equal(2, entry.Suggestions.Count);
        Assert.Equal("iv2_nat_FLO_2", entry.Suggestions[0]);
        Assert.Equal("iv2_nat_FLO_1", entry.Suggestions[1]);
    }

    [Fact]
    public void LeereZeilenWerdenUebersprungen()
    {
        var lines = new[]
        {
            "",
            "   ",
            "[13:53:22][dlc_reloadable.cpp:1581]: Could not find item in store backend. Item: 4091680"
        };

        var result = ErrorLogParser.Parse(lines);

        Assert.Single(result);
    }
}
