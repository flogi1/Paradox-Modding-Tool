using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public class NameShapesTests
{
    [Theory]
    [InlineData("political_platform_@THIS", "political_platform_*", true)]
    [InlineData("infested_star@root.owner", "infested_star*", true)]
    [InlineData("x@event_target:y", "x*", true)]
    [InlineData("x@var:y", "x*", true)]
    [InlineData("x@$P$", "x*", true)]
    [InlineData("x@prev.prev", "x*", true)]
    [InlineData("CHI_holding_@ROOT", "CHI_holding_*", true)]
    [InlineData("a_@FROM.FROM", "a_*", true)]
    [InlineData("num_equipment@infantry_equipment", "num_equipment@infantry_equipment", false)]
    [InlineData("x@rooted", "x@rooted", false)]
    [InlineData("plain_name", "plain_name", false)]
    [InlineData("@CONST", "@CONST", false)]
    public void AtSuffix(string name, string expected, bool isPattern)
    {
        Assert.Equal((expected, isPattern), NameShapes.AtSuffix(name));
    }

    [Theory]
    [InlineData("global.CZE_selling_country", "CZE_selling_country")]
    [InlineData("GLOBAL.NORDIC_conflict_scale", "NORDIC_conflict_scale")]
    [InlineData("PREV.PREV.PRC_arr", "PRC_arr")]
    [InlineData("101.DEN_cost", "DEN_cost")]
    [InlineData("var:SWI:SWI_req", "SWI_req")]
    [InlineData("var:global.RAJ_x", "RAJ_x")]
    [InlineData("SWI.num_owned_states", "num_owned_states")]
    [InlineData("GER_mefo?0", "GER_mefo")]
    [InlineData("x?global.y", "x")]
    [InlineData("arr^num", "arr")]
    [InlineData("global.arr^0", "arr")]
    [InlineData("x|Y0", "x")]
    [InlineData("ROOT.party_popularity@ruling_party", "party_popularity@ruling_party")]
    [InlineData("plain", "plain")]
    public void Hoi4VariableName(string raw, string expected)
    {
        Assert.Equal(expected, NameShapes.Hoi4VariableName(raw));
    }

    [Theory]
    [InlineData("5", true)]
    [InlineData("-0.5", true)]
    [InlineData("@CONST", true)]
    [InlineData("token:x", true)]
    [InlineData("constant:a.b", true)]
    [InlineData("value:sv", true)]
    [InlineData("trigger:t", true)]
    [InlineData("modifier:m", true)]
    [InlineData("GER", true)]
    [InlineData("D01", true)]
    [InlineData("ROOT", true)]
    [InlineData("root", true)]
    [InlineData("FROM", true)]
    [InlineData("prev", true)]
    [InlineData("THIS", true)]
    [InlineData("OWNER", true)]
    [InlineData("yes", true)]
    [InlineData("no", true)]
    [InlineData("", true)]
    [InlineData("my_var", false)]
    [InlineData("Ger", false)]
    [InlineData("ENG_cost?50", false)]
    public void IsNonNameValue(string token, bool expected)
    {
        Assert.Equal(expected, NameShapes.IsNonNameValue(token));
    }
}
