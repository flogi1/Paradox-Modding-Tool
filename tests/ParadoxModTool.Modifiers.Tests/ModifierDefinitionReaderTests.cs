using Xunit;

namespace ParadoxModTool.Modifiers.Tests;

public class ModifierDefinitionReaderTests
{
    [Fact]
    public void ReadsPercentDecimalsAndFormat()
    {
        var result = ModifierDefinitionReader.Read("﻿a={\n\tpercent=yes\n\tdecimals=0\n\tgame_data={\n\t\tcategory=location\n\t\tformat=\"FormatPopCaps\"\n\t}\n}\n");

        var a = result["a"];
        Assert.True(a.Percent);
        Assert.Equal(0, a.Decimals);
        Assert.Equal("FormatPopCaps", a.Format);
    }

    [Fact]
    public void SpacesAroundEqualsDoNotMatter() => Assert.True(ModifierDefinitionReader.Read("b = {\n\tpercent = yes\n}\n")["b"].Percent);

    [Fact]
    public void AlreadyPercentAndNoDifferenceSign()
    {
        var c = ModifierDefinitionReader.Read("c={ already_percent=yes no_difference_sign=yes }")["c"];

        Assert.True(c.AlreadyPercent);
        Assert.True(c.NoDifferenceSign);
        Assert.Null(c.Decimals);
    }

    [Fact]
    public void BooleanModifiersAreSkipped() => Assert.False(ModifierDefinitionReader.Read("c={ boolean=yes }").ContainsKey("c"));

    [Fact]
    public void CommentsAreIgnored()
    {
        var result = ModifierDefinitionReader.Read("# d={ percent=yes }\ne={ percent=yes # decimals=0\n}\n");

        Assert.False(result.ContainsKey("d"));
        Assert.Null(result["e"].Decimals);
    }

    [Fact]
    public void OtherFieldsAreIgnored()
    {
        var f = ModifierDefinitionReader.Read("f={ color=bad prefix=MOD_X hidden=yes game_data={ min=0 max=1 ai=yes } }")["f"];

        Assert.Equal(new ModifierDefinition(false, false, null, null, false), f);
    }

    [Fact]
    public void ABrokenEntryIsSkippedTheRestIsRead()
    {
        var result = ModifierDefinitionReader.Read("g={ decimals=abc } h={ percent=yes }");

        Assert.False(result.ContainsKey("g"));
        Assert.True(result["h"].Percent);
    }

    [Fact]
    public void AnUnclosedLastBlockIsSkipped()
    {
        var result = ModifierDefinitionReader.Read("i={ percent=yes } j={ percent=yes");

        Assert.True(result.ContainsKey("i"));
        Assert.False(result.ContainsKey("j"));
    }

    [Fact]
    public void LaterDuplicateWins()
    {
        var k = ModifierDefinitionReader.Read("k={ percent=yes } k={ decimals=1 }")["k"];

        Assert.False(k.Percent);
        Assert.Equal(1, k.Decimals);
    }

    [Fact]
    public void NameLookupIgnoresCase() => Assert.True(ModifierDefinitionReader.Read("Mod_A={ percent=yes }").ContainsKey("mod_a"));
}
