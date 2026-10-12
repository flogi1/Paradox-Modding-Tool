using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Abschnitt zwischen Markierungen einfügen oder ersetzen; alte Python-Markierungen werden erkannt und bleiben stehen.</summary>
public sealed class SectionInserterTests
{
    private const string Begin = "# PMT-Generator iv2/select/adm begin";
    private const string End = "# PMT-Generator iv2/select/adm end";
    private const string LegacyBegin = "# === IV2Generator:BYZ:START ===";
    private const string LegacyEnd = "# === IV2Generator:BYZ:END ===";

    private static SectionResult Apply(string? existing, string? block, string content, string? skeleton = null) =>
        SectionInserter.Apply(existing, block, Begin, End, LegacyBegin, LegacyEnd, content, skeleton, "\n");

    [Fact]
    public void Appends_section_inside_named_block()
    {
        const string Existing = "types t {\n}\niv2_historic_shell_adm = {\n\tother = {}\n}\nafter = 1\n";

        var result = Apply(Existing, "iv2_historic_shell_adm", "a = {}\nb = {}\n");

        Assert.Equal("types t {\n}\niv2_historic_shell_adm = {\n\tother = {}\n\t" + Begin + "\n\ta = {}\n\tb = {}\n\t" + End + "\n}\nafter = 1\n", result.Text);
        Assert.Equal(result.Text, Apply(result.Text, "iv2_historic_shell_adm", "a = {}\nb = {}\n").Text);
    }

    [Fact]
    public void Empty_one_line_block_is_opened()
    {
        var result = Apply("shell = { }\n", "shell", "a = {}\n");

        Assert.Equal("shell = {\n\t" + Begin + "\n\ta = {}\n\t" + End + "\n}\n", result.Text);
    }

    [Fact]
    public void Nested_block_path_is_followed()
    {
        var result = Apply("outer = {\n\tinner = {\n\t}\n}\n", "outer.inner", "x = 1\n");

        Assert.Equal("outer = {\n\tinner = {\n\t\t" + Begin + "\n\t\tx = 1\n\t\t" + End + "\n\t}\n}\n", result.Text);
    }

    [Fact]
    public void Section_without_block_goes_to_the_end_of_the_file()
    {
        Assert.Equal("a = 1\n" + Begin + "\nb = 2\n" + End + "\n", Apply("a = 1\n", null, "b = 2\n").Text);
        Assert.Equal("a = 1\n" + Begin + "\nb = 2\n" + End + "\n", Apply("a = 1", null, "b = 2\n").Text);
    }

    [Fact]
    public void Replaces_content_between_legacy_markers_in_place()
    {
        const string Existing =
            "iv2_historic_shell_adm = {\n" +
            "    # === IV2Generator:BYZ:START ===\n" +
            "    iv2_historic_idea_select_BYZ_1 = {}\n" +
            "    # === IV2Generator:BYZ:END ===\n" +
            "    # === IV2Generator:FRA:START ===\n" +
            "    iv2_historic_idea_select_FRA_1 = {}\n" +
            "    # === IV2Generator:FRA:END ===\n" +
            "}\n";

        var result = Apply(Existing, "iv2_historic_shell_adm", "iv2_historic_idea_select_BYZ_1 = {}\niv2_historic_idea_select_BYZ_2 = {}\n");

        Assert.Equal(Existing.Replace(
            "    iv2_historic_idea_select_BYZ_1 = {}\n",
            "    iv2_historic_idea_select_BYZ_1 = {}\n    iv2_historic_idea_select_BYZ_2 = {}\n"), result.Text);
    }

    [Fact]
    public void Pmt_markers_win_over_legacy_markers() =>
        Assert.Contains("\tnew = 1\n", Apply("s = {\n\t" + Begin + "\n\told = 1\n\t" + End + "\n}\n", "s", "new = 1\n").Text);

    [Fact]
    public void Start_without_end_is_an_error() =>
        Assert.Throws<GeneratorOutputException>(() => Apply("s = {\n\t" + LegacyBegin + "\n\tx = 1\n}\n", "s", "y = 1\n"));

    [Fact]
    public void Missing_file_uses_skeleton() =>
        Assert.Equal("s = {\n\t" + Begin + "\n\ty = 1\n\t" + End + "\n}\n", Apply(null, "s", "y = 1\n", "s = {\n}\n").Text);

    [Fact]
    public void Missing_file_without_skeleton_is_an_error() =>
        Assert.Throws<GeneratorOutputException>(() => Apply(null, "s", "y = 1\n"));

    [Fact]
    public void Missing_block_is_an_error_naming_the_block()
    {
        var error = Assert.Throws<GeneratorOutputException>(() => Apply("a = {\n}\n", "iv2_historic_shell_navy", "y = 1\n"));
        Assert.Contains("iv2_historic_shell_navy", error.Message);
    }

    [Fact]
    public void Empty_content_without_markers_changes_nothing_and_is_stable()
    {
        var first = Apply("s = {\n}\n", "s", string.Empty);

        Assert.Equal("s = {\n}\n", first.Text);
        Assert.Equal(first.Text, Apply(first.Text, "s", string.Empty).Text);
    }

    [Fact]
    public void Crlf_files_keep_crlf()
    {
        var result = SectionInserter.Apply("s = {\r\n}\r\n", "s", Begin, End, null, null, "y = 1\n", null, "\r\n");

        Assert.Equal("s = {\r\n\t" + Begin + "\r\n\ty = 1\r\n\t" + End + "\r\n}\r\n", result.Text);
    }
}
