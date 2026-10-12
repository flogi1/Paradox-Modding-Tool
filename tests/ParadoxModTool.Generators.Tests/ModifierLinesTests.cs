using ParadoxModTool.Generators.Editing;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

public class ModifierLinesTests
{
    [Fact]
    public void Insert_into_empty_and_filled_field()
    {
        Assert.Equal("a = 0.1", ModifierLines.Insert("  \n", "a", "0.1"));
        Assert.Equal("x = 1\na = 0.1", ModifierLines.Insert("x = 1", "a", "0.1"));
        Assert.Equal("x = 1\na = 0.1", ModifierLines.Insert("x = 1\n", "a", "0.1"));
        Assert.Equal("x = 1\na = 0.1", ModifierLines.Insert("x = 1\r\n", "a", "0.1"));
    }

    [Fact]
    public void Insert_skips_present_key()
    {
        Assert.Equal("\tA = 2 # c", ModifierLines.Insert("\tA = 2 # c", "a", "0.1"));
        Assert.True(ModifierLines.HasKey("x = 1\n\tA = 2 # c", "a"));
        Assert.False(ModifierLines.HasKey("# a = 1\nab = 2", "a"));
    }

    [Fact]
    public void Start_values()
    {
        Assert.Equal("yes", ModifierLines.StartValue(true, true));
        Assert.Equal("-0.1", ModifierLines.StartValue(false, true));
        Assert.Equal("0.1", ModifierLines.StartValue(false, false));
    }

    [Fact]
    public void Scale_from_level_one_and_two()
    {
        Assert.Equal(new[] { "0.1", "0.15", "0.2" }, new[] { 2, 3, 4 }.Select(m => ModifierLines.Scale("0.05", 1, m)));
        Assert.Equal("0.3", ModifierLines.Scale("0.2", 2, 3));
        Assert.Equal("yes", ModifierLines.Scale("yes", 1, 3));
    }

    [Fact]
    public void Scale_keeps_sign_and_has_no_float_noise()
    {
        Assert.Equal("0.004", ModifierLines.Scale("0.001", 1, 4));
        Assert.Equal("-0.3", ModifierLines.Scale("-0.1", 1, 3));
        Assert.Equal("0.3", ModifierLines.Scale("0.1", 1, 3));
    }

    [Fact]
    public void Copy_to_higher_levels_scales_and_keeps_present_keys()
    {
        var result = ModifierLines.CopyToHigherLevels(new[] { "a = 0.05", "", "a = 9", "" }, 1, "a", "0.05", scale: true);
        Assert.Equal(new[] { "a = 0.05", "a = 0.1", "a = 9", "a = 0.2" }, result);

        // Die eigene Stufe fügt der Aufrufer selbst ein; CopyToHigherLevels ändert nur höhere Stufen.
        Assert.Equal(new[] { "", "a = 0.05", "a = 0.05" }, ModifierLines.CopyToHigherLevels(new[] { "", "", "" }, 1, "a", "0.05", scale: false));
        Assert.Equal(new[] { "", "", "a = 0.15", "a = 0.2" }, ModifierLines.CopyToHigherLevels(new[] { "", "", "", "" }, 2, "a", "0.1", scale: true));
    }

    [Fact]
    public void Remove_keeps_comments_and_other_lines() =>
        Assert.Equal("# head\nb = 1\n\nc = 2", ModifierLines.Remove("# head\na = 1\nb = 1\n\n\tA = 3 # x\nc = 2", "a"));

    [Fact]
    public void Remove_from_all_levels() =>
        Assert.Equal(new[] { "b = 1", "", "c = 1" }, ModifierLines.RemoveFromAll(new[] { "a = 1\nb = 1", "a = 2", "c = 1" }, "a"));
}
