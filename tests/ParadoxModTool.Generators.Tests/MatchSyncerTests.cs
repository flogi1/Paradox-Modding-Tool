using System.Text.RegularExpressions;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Abgleich nach Treffer (Spec 2026-10-06-generator-packages-idea-groups, Abschnitt 1): passende Einheiten an Ort und Stelle ersetzen.</summary>
public sealed class MatchSyncerTests
{
    private static Regex R(string pattern) => new(pattern, RegexOptions.CultureInvariant);

    [Fact]
    public void Replaces_matching_units_in_place()
    {
        var result = MatchSyncer.Apply("a_1 = { x = 1 }\nb_18 = { y = 1 }\nb_18_1 = { z }\nc_2 = {}\n", null, R("b_18(?![0-9])"), "b_18 = { y = 2 }\n", "\n");

        Assert.Equal("a_1 = { x = 1 }\nb_18 = { y = 2 }\nc_2 = {}\n", result.Text);
        Assert.Equal(1, result.Replaced);
        Assert.False(result.Appended);
    }

    [Fact]
    public void Match_boundary_keeps_other_groups()
    {
        var result = MatchSyncer.Apply("b_1 = {}\nb_18 = {}\n", null, R("b_1(?![0-9])"), "b_1 = { new }\n", "\n");

        Assert.Equal("b_1 = { new }\nb_18 = {}\n", result.Text);
    }

    [Fact]
    public void Second_run_is_removed_without_double_blank_lines()
    {
        var result = MatchSyncer.Apply("g_1 = {}\n\nx = {}\n\ng_1_2 = {}\n\ny = {}\n", null, R("g_1(?![0-9])"), "g_1 = { new }\n", "\n");

        Assert.Equal("g_1 = { new }\n\nx = {}\n\ny = {}\n", result.Text);
    }

    [Fact]
    public void No_match_appends_before_block_close()
    {
        var result = MatchSyncer.Apply("list = {\n\tactions = {\n\t\ta_1\n\t}\n}\n", "list.actions", R("a_2(?![0-9])"), "\t\ta_2\n", "\n");

        Assert.Equal("list = {\n\tactions = {\n\t\ta_1\n\t\ta_2\n\t}\n}\n", result.Text);
        Assert.True(result.Appended);
    }

    [Fact]
    public void No_match_appends_at_file_end()
    {
        var result = MatchSyncer.Apply("a = {}", null, R("b_1(?![0-9])"), "b_1 = {}\n", "\n");

        Assert.Equal("a = {}\nb_1 = {}\n", result.Text);
        Assert.True(result.Appended);
    }

    [Fact]
    public void Empty_render_removes_runs()
    {
        var result = MatchSyncer.Apply("a = {}\nb_1 = {\n\tx = 1\n}\nc = {}\n", null, R("b_1(?![0-9])"), string.Empty, "\n");

        Assert.Equal("a = {}\nc = {}\n", result.Text);
        Assert.Equal(new[] { "b_1 = {" }, result.Removed);
    }

    [Fact]
    public void Empty_render_without_match_changes_nothing()
    {
        var result = MatchSyncer.Apply("a = {}\n", null, R("b_1(?![0-9])"), string.Empty, "\n");

        Assert.Equal("a = {}\n", result.Text);
        Assert.False(result.Appended);
    }

    [Fact]
    public void Unnamed_if_blocks_inside_effect_are_units()
    {
        const string text = "eff = {\n\tif = {\n\t\tlimit = { has_variable = s_18_1 }\n\t}\n\tif = {\n\t\tlimit = { has_variable = s_19_1 }\n\t}\n}\n";

        var result = MatchSyncer.Apply(text, "eff", R("s_18_"), "\tif = {\n\t\tlimit = { has_variable = s_18_1 }\n\t\tx = yes\n\t}\n", "\n");

        Assert.Equal(
            "eff = {\n\tif = {\n\t\tlimit = { has_variable = s_18_1 }\n\t\tx = yes\n\t}\n\tif = {\n\t\tlimit = { has_variable = s_19_1 }\n\t}\n}\n",
            result.Text);
    }

    [Fact]
    public void Gui_template_at_top_level_is_a_unit()
    {
        var result = MatchSyncer.Apply("template tt_adm_18 {\n\tx = 1\n}\ntemplate tt_adm_19 {\n}\n", null, R("tt_adm_18(?![0-9])"), "template tt_adm_18 {\n\tx = 2\n}\n", "\n");

        Assert.Equal("template tt_adm_18 {\n\tx = 2\n}\ntemplate tt_adm_19 {\n}\n", result.Text);
    }

    [Fact]
    public void Gui_type_inside_types_is_a_unit()
    {
        const string text = "types all {\n\ttype ig_adm_17 = container {\n\t}\n\ttype ig_adm_18 = container {\n\t\ta = 1\n\t}\n}\n";

        var result = MatchSyncer.Apply(text, "types all", R("ig_adm_18(?![0-9])"), "\ttype ig_adm_18 = container {\n\t\ta = 2\n\t}\n", "\n");

        Assert.Equal("types all {\n\ttype ig_adm_17 = container {\n\t}\n\ttype ig_adm_18 = container {\n\t\ta = 2\n\t}\n}\n", result.Text);
    }

    [Fact]
    public void List_value_is_a_unit()
    {
        var result = MatchSyncer.Apply("list = {\n\tactions = {\n\t\ta_1\n\t\ta_2\n\t}\n}\n", "list.actions", R("a_1(?![0-9])"), "\t\ta_1\n\t\ta_1_b\n", "\n");

        Assert.Equal("list = {\n\tactions = {\n\t\ta_1\n\t\ta_1_b\n\t\ta_2\n\t}\n}\n", result.Text);
    }

    [Fact]
    public void Comment_line_is_its_own_unit()
    {
        const string text = "#### Idea Group adm_18 X\nb_18 = {}\n#### Idea Group adm_19 Y\nb_19 = {}\n";

        var result = MatchSyncer.Apply(text, null, R("adm_18(?![0-9])|b_18(?![0-9])"), "#### Idea Group adm_18 Z\nb_18 = { new }\n", "\n");

        Assert.Equal("#### Idea Group adm_18 Z\nb_18 = { new }\n#### Idea Group adm_19 Y\nb_19 = {}\n", result.Text);
    }

    [Fact]
    public void Run_sharing_a_line_is_replaced_by_span()
    {
        var result = MatchSyncer.Apply("x = { b_18 = {} b_19 = {} }\n", "x", R("b_18(?![0-9])"), "b_18 = { n }", "\n");

        Assert.Equal("x = { b_18 = { n } b_19 = {} }\n", result.Text);
    }

    [Fact]
    public void Unchanged_group_gives_identical_text()
    {
        const string text = "a = {}\r\n#### adm_18\r\nb_18 = {\r\n\tx = 1\t\r\n}\r\n\r\nb_18_1 = {}\r\nc = {}\r\n";

        var result = MatchSyncer.Apply(text, null, R("(adm|b)_18(?![0-9])"), "#### adm_18\r\nb_18 = {\r\n\tx = 1\t\r\n}\r\n\r\nb_18_1 = {}\r\n", "\r\n");

        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void Comments_between_units_join_the_run()
    {
        const string text = "x = {}\n#### Select adm_18\n####\n\nsel_18 = {}\n\n# note\n####\nb_18 = {}\ny = {}\n";

        var result = MatchSyncer.Apply(text, null, R("_18(?![0-9])"), "NEW = {}\n", "\n");

        Assert.Equal("x = {}\nNEW = {}\ny = {}\n", result.Text);
    }

    [Fact]
    public void Decoration_lines_at_run_edges_belong_to_the_run()
    {
        const string text = "a_17 = {}\n#####\n#####\n\n#####\n#### Select adm_18\nb_18 = {}\n#### adm_18 End\n#####\n#####\n\n#####\na_19 = {}\n";

        var result = MatchSyncer.Apply(text, null, R("adm_18(?![0-9])|b_18(?![0-9])"), "NEW\n", "\n");

        Assert.Equal("a_17 = {}\n#####\n#####\n\nNEW\n\n#####\na_19 = {}\n", result.Text);
    }

    [Fact]
    public void Path_tries_same_named_siblings_until_the_rest_fits()
    {
        const string text = "window = {\n\twidget = {\n\t\tx = 1\n\t}\n\twidget = {\n\t\tscrollbox = {\n\t\t\tblockoverride \"scrollbox_content\"\n\t\t\t{\n\t\t\t\tvbox = {\n\t\t\t\t\tc_1 = {}\n\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n";

        var result = MatchSyncer.Apply(text, "window.widget.scrollbox.blockoverride scrollbox_content.vbox", R("c_2(?![0-9])"), "\t\t\t\t\tc_2 = {}\n", "\n");

        Assert.Contains("\t\t\t\t\tc_1 = {}\n\t\t\t\t\tc_2 = {}\n\t\t\t\t}", result.Text);
    }

    [Fact]
    public void Path_index_picks_the_nth_same_named_sibling()
    {
        const string text = "eff = {\n\tif = {\n\t\ts = {\n\t\t\ta = 1\n\t\t}\n\t}\n\tif = {\n\t\ts = {\n\t\t\ta = 2\n\t\t}\n\t}\n}\n";

        var result = MatchSyncer.Apply(text, "eff.if#2.s", R("b_1(?![0-9])"), "\t\t\tb_1 = yes\n", "\n");

        Assert.Equal("eff = {\n\tif = {\n\t\ts = {\n\t\t\ta = 1\n\t\t}\n\t}\n\tif = {\n\t\ts = {\n\t\t\ta = 2\n\t\t\tb_1 = yes\n\t\t}\n\t}\n}\n", result.Text);
    }

    [Fact]
    public void Path_filter_picks_the_sibling_containing_the_text()
    {
        const string text = "eff = {\n\tif = { limit = { a = 1 } }\n\tif = {\n\t\tlimit = { v.x = 2 }\n\t\ts = {\n\t\t}\n\t}\n\tif = {\n\t\tlimit = { v.x = 3 }\n\t\ts = {\n\t\t}\n\t}\n}\n";

        var result = MatchSyncer.Apply(text, "eff.if[v.x = 3].s", R("b_1(?![0-9])"), "\t\t\tb_1 = yes\n", "\n");

        Assert.Equal("eff = {\n\tif = { limit = { a = 1 } }\n\tif = {\n\t\tlimit = { v.x = 2 }\n\t\ts = {\n\t\t}\n\t}\n\tif = {\n\t\tlimit = { v.x = 3 }\n\t\ts = {\n\t\t\tb_1 = yes\n\t\t}\n\t}\n}\n", result.Text);
    }

    [Fact]
    public void Path_filters_chain_and_negate()
    {
        const string text = "eff = {\n\ts = {\n\t\ttrigger = religion.group\n\t}\n\ts = {\n\t\ttrigger = religion\n\t}\n}\n";

        var result = MatchSyncer.Apply(text, "eff.s[trigger = religion][!religion.group]", R("c_1(?![0-9])"), "\t\tc_1 = yes\n", "\n");

        Assert.Equal("eff = {\n\ts = {\n\t\ttrigger = religion.group\n\t}\n\ts = {\n\t\ttrigger = religion\n\t\tc_1 = yes\n\t}\n}\n", result.Text);
    }

    [Fact]
    public void Appended_section_head_gets_a_blank_line_before_it()
    {
        var result = MatchSyncer.Apply("#### a_1\na_1 = {}\n####\n", null, R("a_2(?![0-9])"), "####\n#### a_2\na_2 = {}\n", "\n");

        Assert.Equal("#### a_1\na_1 = {}\n####\n\n####\n#### a_2\na_2 = {}\n", result.Text);
    }

    [Fact]
    public void Appended_list_value_gets_no_blank_line() =>
        Assert.Equal("l = {\n\ta_1\n\ta_2\n}\n", MatchSyncer.Apply("l = {\n\ta_1\n}\n", "l", R("a_2(?![0-9])"), "\ta_2\n", "\n").Text);

    [Fact]
    public void Missing_block_throws() =>
        Assert.Throws<GeneratorOutputException>(() => MatchSyncer.Apply("a = {}\n", "missing", R("x"), "x\n", "\n"));

    [Fact]
    public void InsertAfter_puts_new_unit_after_last_unit_of_its_category()
    {
        var existing = "list = {\n\ta_1 = 1\n\ta_2 = 1\n\tb_1 = 1\n}\n";
        var result = MatchSyncer.Apply(existing, "list", R("a_3(?![0-9])"), "\ta_3 = 1\n", "\n", insertAfter: R("^a_[0-9]+"));
        Assert.Equal("list = {\n\ta_1 = 1\n\ta_2 = 1\n\ta_3 = 1\n\tb_1 = 1\n}\n", result.Text);
        Assert.True(result.Appended);
    }

    [Fact]
    public void InsertAfter_without_anchor_appends_as_before()
    {
        var result = MatchSyncer.Apply("list = {\n\tb_1 = 1\n}\n", "list", R("a_1(?![0-9])"), "\ta_1 = 1\n", "\n", insertAfter: R("^a_[0-9]+"));
        Assert.Equal("list = {\n\tb_1 = 1\n\ta_1 = 1\n}\n", result.Text);
    }

    [Fact]
    public void InsertAfter_is_ignored_when_unit_exists()
    {
        var result = MatchSyncer.Apply("a_1 = 1\nb_1 = 1\n", null, R("b_1(?![0-9])"), "b_1 = 2\n", "\n", insertAfter: R("^a_[0-9]+"));
        Assert.Equal("a_1 = 1\nb_1 = 2\n", result.Text);
    }

    [Fact]
    public void InsertAfter_on_last_line_without_line_end_adds_one()
    {
        var result = MatchSyncer.Apply("a_1 = 1", null, R("a_2(?![0-9])"), "a_2 = 1\n", "\n", insertAfter: R("^a_[0-9]+"));
        Assert.Equal("a_1 = 1\na_2 = 1\n", result.Text);
    }

    [Fact]
    public void InsertAfter_after_multi_line_block_unit()
    {
        var existing = "list = {\n\ta_1 = {\n\t\tx = 1\n\t}\n\tb_1 = 1\n}\n";
        var result = MatchSyncer.Apply(existing, "list", R("a_2(?![0-9])"), "\ta_2 = 1\n", "\n", insertAfter: R("^a_[0-9]+"));
        Assert.Equal("list = {\n\ta_1 = {\n\t\tx = 1\n\t}\n\ta_2 = 1\n\tb_1 = 1\n}\n", result.Text);
    }

    [Theory]
    // Abstand 1 zwischen den Einheiten: eine Leerzeile vor der neuen; die Leerzeile vor "}" bleibt hinter ihr
    [InlineData("list = {\n\ta_1 = 1\n\n\ta_2 = 1\n\n}\n", "list = {\n\ta_1 = 1\n\n\ta_2 = 1\n\n\ta_3 = 1\n\n}\n")]
    // Abstand 0: keine Leerzeile
    [InlineData("list = {\n\ta_1 = 1\n\ta_2 = 1\n\n\tb_1 = 1\n}\n", "list = {\n\ta_1 = 1\n\ta_2 = 1\n\ta_3 = 1\n\n\tb_1 = 1\n}\n")]
    // Zwei Leerzeilen (mit Leerraum) werden zu zwei leeren Zeilen
    [InlineData("list = {\n\ta_1 = 1\n\t\n\n\ta_2 = 1\n}\n", "list = {\n\ta_1 = 1\n\t\n\n\ta_2 = 1\n\n\n\ta_3 = 1\n}\n")]
    // Der Anker ist die erste Einheit: die Leerzeile hinter "{" ist kein Abstand zwischen Einheiten
    [InlineData("list = {\n\n\ta_2 = 1\n\tb_1 = 1\n}\n", "list = {\n\n\ta_2 = 1\n\ta_3 = 1\n\tb_1 = 1\n}\n")]
    public void InsertAfter_copies_the_gap_above_the_anchor(string existing, string expected)
    {
        var result = MatchSyncer.Apply(existing, "list", R("a_3(?![0-9])"), "\ta_3 = 1\n", "\n", insertAfter: R("^a_[0-9]+"));
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void InsertAfter_copies_the_gap_on_file_level_with_crlf_and_comment_header()
    {
        // Datei-Ebene mit CRLF; ein Text mit Kommentarkopf bekommt nur den kopierten Abstand, keine zweite Leerzeile
        var existing = "# a_1\r\na_1 = 1\r\n\r\n# a_2\r\na_2 = 1\r\nb_1 = 1\r\n";
        var result = MatchSyncer.Apply(existing, null, R("a_3(?![0-9])"), "# a_3\r\na_3 = 1\r\n", "\r\n", insertAfter: R("a_[0-9]+"));
        Assert.Equal("# a_1\r\na_1 = 1\r\n\r\n# a_2\r\na_2 = 1\r\n\r\n# a_3\r\na_3 = 1\r\nb_1 = 1\r\n", result.Text);
    }

    [Fact]
    public void InsertAfter_with_block_closing_on_same_line_goes_before_close()
    {
        var result = MatchSyncer.Apply("list = { a_1 = 1 }\n", "list", R("a_2(?![0-9])"), "\ta_2 = 1\n", "\n", insertAfter: R("^a_[0-9]+"));
        Assert.Equal("list = { a_1 = 1\n\ta_2 = 1\n}\n", result.Text);
    }
}
