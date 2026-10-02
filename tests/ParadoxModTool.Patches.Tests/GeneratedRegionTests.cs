using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class GeneratedRegionTests
{
    private static RegionUnit[] Units(params (string Key, string Text)[] units) => units.Select(u => new RegionUnit(u.Key, u.Text)).ToArray();

    [Fact]
    public void Write_appends_a_new_region_with_unit_markers()
    {
        var text = GeneratedRegion.Write("a = 1\n", "r", "defs", null, Units(("x_01", "x_01 = { }")), "\n");

        Assert.Equal("a = 1\n# PMT-Patch r/defs begin\n# PMT-Unit x_01\nx_01 = { }\n# PMT-Patch r/defs end\n", text);
    }

    [Fact]
    public void Write_adds_missing_final_newline_before_the_region()
    {
        Assert.StartsWith("a = 1\r\n# PMT-Patch", GeneratedRegion.Write("a = 1", "r", "defs", null, Units(), "\r\n"));
    }

    [Fact]
    public void Read_returns_units_in_file_order_with_their_text()
    {
        var text = "a = 1\n# PMT-Patch r/defs begin\n# PMT-Unit b_01\nb_01 = {\n\tx = 1\n}\n\n# PMT-Unit a_01\na_01 = { }\n# PMT-Patch r/defs end\nz = 2\n";

        var region = GeneratedRegion.Read(text, "r", "defs");

        Assert.True(region.Found);
        Assert.False(region.Broken);
        Assert.Equal(Units(("b_01", "b_01 = {\n\tx = 1\n}"), ("a_01", "a_01 = { }")), region.Units);
    }

    [Fact]
    public void Read_without_region_is_not_found()
    {
        var region = GeneratedRegion.Read("a = 1\n# PMT-Patch r/other begin\n# PMT-Patch r/other end\n", "r", "defs");

        Assert.False(region.Found);
        Assert.False(region.Broken);
        Assert.Empty(region.Units);
    }

    [Fact]
    public void Write_inside_block_places_region_before_closing_brace_indented()
    {
        var text = GeneratedRegion.Write("eff = {\n\thidden_effect = {\n\t\tif = { }\n\t}\n}\n", "r", "sort", "eff/hidden_effect", Units(("a_01", "if = {\n\tx = a_01\n}")), "\n");

        Assert.Equal(
            "eff = {\n\thidden_effect = {\n\t\tif = { }\n\t\t# PMT-Patch r/sort begin\n\t\t# PMT-Unit a_01\n\t\tif = {\n\t\t\tx = a_01\n\t\t}\n\t\t# PMT-Patch r/sort end\n\t}\n}\n",
            text);
        Assert.Equal(Units(("a_01", "if = {\n\tx = a_01\n}")), GeneratedRegion.Read(text, "r", "sort").Units);
    }

    [Fact]
    public void Write_inside_block_with_brace_on_the_same_line_leaves_no_trailing_space()
    {
        var text = GeneratedRegion.Write("\teff = { a = 1 }\n", "r", "sort", "eff", Units(("a_01", "x = a_01")), "\n");

        Assert.Equal("\teff = { a = 1\n\t\t# PMT-Patch r/sort begin\n\t\t# PMT-Unit a_01\n\t\tx = a_01\n\t\t# PMT-Patch r/sort end\n\t}\n", text);
    }

    [Fact]
    public void Write_replaces_existing_region_and_keeps_surrounding_text()
    {
        var first = GeneratedRegion.Write("a = 1\n", "r", "defs", null, Units(("x_01", "x_01 = { }")), "\n") + "z = 2\n";

        var second = GeneratedRegion.Write(first, "r", "defs", null, Units(("y_01", "y_01 = { }")), "\n");

        Assert.Equal("a = 1\n# PMT-Patch r/defs begin\n# PMT-Unit y_01\ny_01 = { }\n# PMT-Patch r/defs end\nz = 2\n", second);
    }

    [Fact]
    public void Write_twice_with_same_units_is_stable()
    {
        var once = GeneratedRegion.Write("a = 1\n", "r", "defs", null, Units(("x_01", "x_01 = {\n\ty = 1\n}")), "\n");

        Assert.Equal(once, GeneratedRegion.Write(once, "r", "defs", null, GeneratedRegion.Read(once, "r", "defs").Units, "\n"));
    }

    [Fact]
    public void Missing_end_marker_is_broken()
    {
        var region = GeneratedRegion.Read("# PMT-Patch r/defs begin\n# PMT-Unit a\na = { }\n", "r", "defs");

        Assert.True(region.Broken);
        Assert.False(string.IsNullOrEmpty(region.Problem));
    }

    [Fact]
    public void Double_begin_marker_is_broken()
    {
        Assert.True(GeneratedRegion.Read("# PMT-Patch r/defs begin\n# PMT-Patch r/defs end\n# PMT-Patch r/defs begin\n# PMT-Patch r/defs end\n", "r", "defs").Broken);
    }

    [Fact]
    public void Repeated_unit_key_is_broken()
    {
        Assert.True(GeneratedRegion.Read("# PMT-Patch r/defs begin\n# PMT-Unit a_01\na_01 = { }\n# PMT-Unit a_01\nmy_variant_01 = { x = 5 }\n# PMT-Patch r/defs end\n", "r", "defs").Broken);
    }

    [Fact]
    public void Text_before_the_first_unit_marker_is_broken()
    {
        Assert.True(GeneratedRegion.Read("# PMT-Patch r/defs begin\nstray = 1\n# PMT-Unit a\na = { }\n# PMT-Patch r/defs end\n", "r", "defs").Broken);
    }

    [Fact]
    public void Missing_inside_block_is_reported()
    {
        var ex = Assert.Throws<PatchRegionException>(() => GeneratedRegion.Write("eff = { }\n", "r", "sort", "eff/hidden_effect", Units(), "\n"));
        Assert.Contains("eff/hidden_effect", ex.Message);
    }
}
