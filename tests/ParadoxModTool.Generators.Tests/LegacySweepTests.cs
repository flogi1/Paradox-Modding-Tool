using System.Text.RegularExpressions;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Unmarkierte Alt-Einträge eines Schlüssels (IV2: Tag) vor dem Einfügen entfernen, wie der Python-Generator.</summary>
public sealed class LegacySweepTests
{
    private const string Begin = "# PMT-Generator iv2/see/TST begin";
    private const string End = "# PMT-Generator iv2/see/TST end";
    private const string LegacyBegin = "# === IV2Generator:TST:START ===";
    private const string LegacyEnd = "# === IV2Generator:TST:END ===";

    private static Regex[] Patterns(params string[] patterns) => patterns.Select(p => new Regex(p, RegexOptions.CultureInvariant)).ToArray();

    private static SectionResult Apply(string existing, string? block, string content, string newLine, params string[] patterns) =>
        SectionInserter.Apply(existing, block, Begin, End, LegacyBegin, LegacyEnd, content, null, newLine, Patterns(patterns));

    [Fact]
    public void Removes_single_line_entries_of_tag_only()
    {
        const string Text = "shell = {\n\tiv2_historic_idea_see_TST_1 = {}\n\tiv2_historic_idea_see_TS_1 = {}\n\tiv2_historic_idea_see_TST_12 = {}\n}\n";

        var (result, removed) = LegacySweep.Apply(Text, 0, Text.Length, Patterns(@"iv2_historic_idea_see_TST_\d+\s*=\s*\{\}"), Array.Empty<(int, int)>());

        Assert.Equal("shell = {\n\tiv2_historic_idea_see_TS_1 = {}\n}\n", result);
        Assert.Equal(2, removed);
    }

    [Fact]
    public void Removes_multi_line_block_by_brace_count()
    {
        const string Tst = "\tif = {\n\t\tlimit = { has_or_had_tag = TST }\n\t\tset_variable = {\n\t\t\tname = x\n\t\t}\n\t}\n";
        const string Ts = "\tif = {\n\t\tlimit = { has_or_had_tag = TS }\n\t\tset_variable = {\n\t\t\tname = y\n\t\t}\n\t}\n";
        var text = "effect = {\n" + Tst + Ts + "}\n";

        var (result, removed) = LegacySweep.Apply(text, 0, text.Length, Patterns(@"if\s*=\s*\{\s*\n\s*limit\s*=\s*\{\s*has_or_had_tag\s*=\s*TST\s*\}"), Array.Empty<(int, int)>());

        Assert.Equal("effect = {\n" + Ts + "}\n", result);
        Assert.Equal(1, removed);
    }

    [Fact]
    public void Removes_messagetype_blocks_without_spaces()
    {
        const string Text = "WE_PERFORM_iv2_ga_select_historic_TST_1_ACTION={\nlog=yes\n}\nWE_PERFORM_iv2_ga_select_historic_TS_1_ACTION={\nlog=yes\n}\n";

        var (result, _) = LegacySweep.Apply(Text, 0, Text.Length, Patterns(@"WE_PERFORM_iv2_ga_(?:select|deselect)_historic_TST_\d+\w*_ACTION\s*=\s*\{"), Array.Empty<(int, int)>());

        Assert.Equal("WE_PERFORM_iv2_ga_select_historic_TS_1_ACTION={\nlog=yes\n}\n", result);
    }

    [Fact]
    public void Sweep_skips_own_marker_section()
    {
        var text = "shell = {\n\t" + Begin + "\n\tiv2_historic_idea_see_TST_1 = {}\n\t" + End + "\n\tiv2_historic_idea_see_TST_2 = {}\n}\n";

        var result = Apply(text, "shell", "iv2_historic_idea_see_TST_1 = {}\niv2_historic_idea_see_TST_3 = {}\n", "\n", @"iv2_historic_idea_see_TST_\d+\s*=\s*\{\}");

        Assert.Equal("shell = {\n\t" + Begin + "\n\tiv2_historic_idea_see_TST_1 = {}\n\tiv2_historic_idea_see_TST_3 = {}\n\t" + End + "\n}\n", result.Text);
    }

    [Fact]
    public void Sweep_keeps_crlf_and_other_tags()
    {
        var text = "shell = {\r\n" +
            "\t# === IV2Generator:ARA:START ===\r\n\tiv2_historic_idea_see_ARA_1 = {}\r\n\t# === IV2Generator:ARA:END ===\r\n" +
            "\t# PMT-Generator iv2/see/FRA begin\r\n\tiv2_historic_idea_see_FRA_1 = {}\r\n\t# PMT-Generator iv2/see/FRA end\r\n" +
            "\tiv2_historic_idea_see_TST_1 = {}\r\n" +
            "}\r\n";

        var result = Apply(text, "shell", "iv2_historic_idea_see_TST_1 = {}\n", "\r\n", @"iv2_historic_idea_see_TST_\d+\s*=\s*\{\}");

        var expected = text.Replace("\tiv2_historic_idea_see_TST_1 = {}\r\n", "\t" + Begin + "\r\n\tiv2_historic_idea_see_TST_1 = {}\r\n\t" + End + "\r\n");
        Assert.Equal(expected, result.Text);
        Assert.DoesNotMatch("[^\r]\n", result.Text);
    }

    [Fact]
    public void Legacy_marker_section_content_is_replaced_not_swept_away()
    {
        var text = "shell = {\n\t" + LegacyBegin + "\n\tiv2_historic_idea_see_TST_1 = {}\n\t" + LegacyEnd + "\n}\n";

        var result = Apply(text, "shell", "iv2_historic_idea_see_TST_2 = {}\n", "\n", @"iv2_historic_idea_see_TST_\d+\s*=\s*\{\}");

        Assert.Equal("shell = {\n\t" + LegacyBegin + "\n\tiv2_historic_idea_see_TST_2 = {}\n\t" + LegacyEnd + "\n}\n", result.Text);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Empty_content_without_marker_inserts_nothing()
    {
        const string Text = "shell = {\n\tiv2_historic_idea_see_TST_1 = {}\n\tother = {}\n}\n";

        var result = Apply(Text, "shell", "\n", "\n", @"iv2_historic_idea_see_TST_\d+\s*=\s*\{\}");

        Assert.Equal("shell = {\n\tother = {}\n}\n", result.Text);
    }

    [Fact]
    public void Empty_content_with_marker_empties_section()
    {
        var text = "shell = {\n\t" + Begin + "\n\tiv2_historic_idea_see_TST_1 = {}\n\t" + End + "\n}\n";

        var result = Apply(text, "shell", string.Empty, "\n");

        Assert.Equal("shell = {\n\t" + Begin + "\n\t" + End + "\n}\n", result.Text);
    }

    [Fact]
    public void Legacy_note_counts_removed_entries()
    {
        const string Text = "shell = {\n\tiv2_historic_idea_see_TST_1 = {}\n\tiv2_historic_idea_see_TST_2 = {}\n}\n";

        var result = Apply(Text, "shell", "iv2_historic_idea_see_TST_1 = {}\n", "\n", @"iv2_historic_idea_see_TST_\d+\s*=\s*\{\}");

        Assert.Equal(new[] { GeneratorsTexts.LegacyRemoved(2) }, result.Notes);
        Assert.Contains("2", result.Notes[0]);
    }
}
