using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Eine Zeile in einem Block sicherstellen (IV2: has_or_had_tag im OR-Block), ohne Markierung.</summary>
public sealed class LineEnsurerTests
{
    private const string Existing = "iv2_trigger = {\n\tOR = {\n\t\thas_or_had_tag = FRA\n\t}\n}\n";

    [Fact]
    public void Adds_missing_line_into_or_block() =>
        Assert.Equal(
            "iv2_trigger = {\n\tOR = {\n\t\thas_or_had_tag = FRA\n\t\thas_or_had_tag = BYZ\n\t}\n}\n",
            LineEnsurer.Apply(Existing, "iv2_trigger.OR", "has_or_had_tag = BYZ", "\n").Text);

    [Fact]
    public void Present_line_is_unchanged()
    {
        Assert.Equal(Existing, LineEnsurer.Apply(Existing, "iv2_trigger.OR", "has_or_had_tag = FRA", "\n").Text);
        Assert.Equal(Existing, LineEnsurer.Apply(Existing, "iv2_trigger.OR", "  has_or_had_tag = FRA  ", "\n").Text);
    }

    private const string Gui =
        "types iv2_historic_national_ideas_setup {\n" +
        "\ttype iv2_historic_shell_adm = widget {\n" +
        "\t\tparentanchor = left\n" +
        "\t}\n" +
        "\ttype iv2_historic_shell_dip = widget {\n" +
        "\t}\n" +
        "}\n";

    [Fact]
    public void Finds_type_inside_types() =>
        Assert.Equal(
            Gui.Replace("\ttype iv2_historic_shell_dip = widget {\n\t}\n", "\ttype iv2_historic_shell_dip = widget {\n\t\tx = {}\n\t}\n"),
            LineEnsurer.Apply(Gui, "types iv2_historic_national_ideas_setup.type iv2_historic_shell_dip", "x = {}", "\n").Text);

    [Fact]
    public void Type_without_types_prefix_is_found_in_any_types_block() =>
        Assert.Equal(
            Gui.Replace("\ttype iv2_historic_shell_dip = widget {\n\t}\n", "\ttype iv2_historic_shell_dip = widget {\n\t\tx = {}\n\t}\n"),
            LineEnsurer.Apply(Gui, "type iv2_historic_shell_dip", "x = {}", "\n").Text);

    [Fact]
    public void Finds_template_and_mixes_with_plain_keys()
    {
        const string Text = "template x_tt {\n\ta = {\n\t\tb = 1\n\t}\n}\n";
        Assert.Equal("template x_tt {\n\ta = {\n\t\tb = 1\n\t\tc = 2\n\t}\n}\n", LineEnsurer.Apply(Text, "template x_tt.a", "c = 2", "\n").Text);
    }

    [Fact]
    public void Missing_gui_header_names_path()
    {
        var error = Assert.Throws<GeneratorOutputException>(() => LineEnsurer.Apply(Gui, "types iv2_historic_national_ideas_setup.type iv2_historic_shell_navy", "x = 1", "\n"));
        Assert.Contains("type iv2_historic_shell_navy", error.Message);
    }

    [Fact]
    public void Missing_block_is_an_error() =>
        Assert.Throws<GeneratorOutputException>(() => LineEnsurer.Apply(Existing, "iv2_trigger.AND", "x = 1", "\n"));
}
