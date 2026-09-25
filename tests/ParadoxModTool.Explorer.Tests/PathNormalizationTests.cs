using Xunit;

namespace ParadoxModTool.Explorer.Tests;

public class PathNormalizationTests
{
    [Fact]
    public void NormalizeKey_UnterschiedlicheGrossSchreibung_ErgibtDenselbenSchluessel()
    {
        var a = PathNormalization.NormalizeKey(@"C:\Mods\Common\file.txt");
        var b = PathNormalization.NormalizeKey(@"c:\mods\common\FILE.TXT");

        Assert.Equal(a, b);
    }

    [Fact]
    public void NormalizeKey_AbschliessenderBackslash_ErgibtDenselbenSchluesselWieOhne()
    {
        var a = PathNormalization.NormalizeKey(@"C:\Mods\MeinMod\");
        var b = PathNormalization.NormalizeKey(@"C:\Mods\MeinMod");

        Assert.Equal(a, b);
    }

    [Fact]
    public void NormalizeKey_UnterschiedlichePfade_ErgebenUnterschiedlicheSchluessel()
    {
        var a = PathNormalization.NormalizeKey(@"C:\Mods\MeinMod\common");
        var b = PathNormalization.NormalizeKey(@"C:\Mods\MeinMod\gui");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void IsSameOrUnder_ErkenntOrdnerUnterhalbUndDenOrdnerSelbst()
    {
        Assert.True(PathNormalization.IsSameOrUnder(@"C:\Mods\A", @"C:\Mods\A"));
        Assert.True(PathNormalization.IsSameOrUnder(@"C:\Mods\A\", @"c:\mods\a\gui\x.gui"));
        Assert.False(PathNormalization.IsSameOrUnder(@"C:\Mods\A", @"C:\Mods\AB\x.gui"));
    }
}
