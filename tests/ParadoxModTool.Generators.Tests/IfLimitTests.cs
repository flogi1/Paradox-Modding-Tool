using System.Text.RegularExpressions;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Abgleich nach Treffer mit <c>ifLimit</c> (Spec 2026-10-10-generator-packages-iv2-dynamic-ideas, "Conditional ideas in the setup effect").</summary>
public sealed class IfLimitTests
{
    private const string Fx = "fx = {\n\tp_1 = 1\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tp_2 = 1\n\t\tp_3 = 1\n\t}\n}\n";

    private static Regex R(string pattern) => new(pattern, RegexOptions.CultureInvariant);

    private static MatchSyncResult Apply(string existing, string match, string rendered, string limit) =>
        MatchSyncer.Apply(existing, "fx", R(match), rendered, "\n", R("^p_"), limit);

    [Fact]
    public void IfLimit_replaces_member_in_place_and_keeps_neighbours()
    {
        var text = Apply(Fx, "p_2(?![0-9])", "\t\tp_2 = 9\n", "c = yes").Text;
        Assert.Equal(Fx.Replace("p_2 = 1", "p_2 = 9"), text);
    }

    [Fact]
    public void IfLimit_matches_condition_written_differently()
    {
        var text = Apply(Fx, "p_3(?![0-9])", "\t\tp_3 = 9\n", "  c   =   yes # same\n").Text;
        Assert.Equal(Fx.Replace("p_3 = 1", "p_3 = 9"), text);
    }

    [Fact]
    public void IfLimit_new_member_joins_existing_block()
    {
        var text = Apply(Fx, "p_4(?![0-9])", "\t\tp_4 = 1\n", "c = yes").Text;
        Assert.Equal(Fx.Replace("\t\tp_3 = 1\n", "\t\tp_3 = 1\n\t\tp_4 = 1\n"), text);
    }

    [Fact]
    public void IfLimit_new_condition_creates_block()
    {
        var text = Apply("fx = {\n\tp_1 = 1\n}\n", "p_2(?![0-9])", "\t\tp_2 = 1\n", "d = yes").Text;
        Assert.Equal("fx = {\n\tp_1 = 1\n\tif = {\n\t\tlimit = {\n\t\t\td = yes\n\t\t}\n\t\tp_2 = 1\n\t}\n}\n", text);
    }

    [Fact]
    public void IfLimit_changed_condition_moves_member_to_top_level()
    {
        var result = Apply(Fx, "p_2(?![0-9])", "\tp_2 = 1\n", string.Empty);
        Assert.Equal("fx = {\n\tp_1 = 1\n\tp_2 = 1\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tp_3 = 1\n\t}\n}\n", result.Text);
        Assert.Empty(result.Removed);
        Assert.Equal(1, result.Replaced);
    }

    [Fact]
    public void IfLimit_moving_last_member_removes_empty_if()
    {
        var one = "fx = {\n\tp_1 = 1\n\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tp_2 = 1\n\t}\n}\n";
        Assert.Equal("fx = {\n\tp_1 = 1\n\tp_2 = 1\n}\n", Apply(one, "p_2(?![0-9])", "\tp_2 = 1\n", string.Empty).Text);
    }

    [Fact]
    public void IfLimit_if_without_limit_stays_a_plain_unit() // e.g. an if = { … } written by hand without limit
    {
        var text = "fx = {\n\tif = {\n\t\tp_9 = 1\n\t}\n}\n";
        Assert.Equal(text, Apply(text, "p_1(?![0-9])", string.Empty, string.Empty).Text);
    }

    [Fact]
    public void IfLimit_moving_into_other_existing_condition()
    {
        var two = Fx.Replace("\t}\n}\n", "\t}\n\tif = {\n\t\tlimit = {\n\t\t\td = yes\n\t\t}\n\t\tp_5 = 1\n\t}\n}\n");
        var result = Apply(two, "p_2(?![0-9])", "\t\tp_2 = 1\n", "d = yes");
        Assert.Equal(two.Replace("\t\tp_2 = 1\n", string.Empty).Replace("\t\tp_5 = 1\n", "\t\tp_5 = 1\n\t\tp_2 = 1\n"), result.Text);
        Assert.Equal(1, result.Replaced);
        Assert.Empty(result.Removed);
    }

    [Fact]
    public void IfLimit_new_block_after_anchor_inside_if_goes_after_whole_if()
    {
        var result = Apply(Fx, "p_4(?![0-9])", "\t\tp_4 = 1\n", "d = yes");
        Assert.Equal(Fx.Replace("\t}\n}\n", "\t}\n\tif = {\n\t\tlimit = {\n\t\t\td = yes\n\t\t}\n\t\tp_4 = 1\n\t}\n}\n"), result.Text);
        Assert.True(result.Appended);
        Assert.Equal(0, result.Replaced);
    }

    [Fact]
    public void IfLimit_multi_line_condition_is_indented_line_by_line()
    {
        var result = MatchSyncer.Apply("fx = {\n}\n", "fx", R("p_1(?![0-9])"), "\t\tp_1 = 1\n", "\n", null, "OR = {\n  a = yes\n  b = yes\n}\n");
        Assert.Equal("fx = {\n\tif = {\n\t\tlimit = {\n\t\t\tOR = {\n\t\t\ta = yes\n\t\t\tb = yes\n\t\t\t}\n\t\t}\n\t\tp_1 = 1\n\t}\n}\n", result.Text);
    }

    [Fact]
    public void IfLimit_new_block_with_space_indent_uses_four_spaces()
    {
        var result = MatchSyncer.Apply("fx = {\n    p_1 = 1\n}\n", "fx", R("p_2(?![0-9])"), "        p_2 = 1\n", "\n", null, "d = yes");
        Assert.Equal("fx = {\n    p_1 = 1\n    if = {\n        limit = {\n            d = yes\n        }\n        p_2 = 1\n    }\n}\n", result.Text);
    }

    [Fact]
    public void IfLimit_new_block_uses_crlf_of_file()
    {
        var result = MatchSyncer.Apply("fx = {\r\n\tp_1 = 1\r\n}\r\n", "fx", R("p_2(?![0-9])"), "\t\tp_2 = 1\r\n", "\r\n", R("^p_"), "d = yes");
        Assert.Equal("fx = {\r\n\tp_1 = 1\r\n\tif = {\r\n\t\tlimit = {\r\n\t\t\td = yes\r\n\t\t}\r\n\t\tp_2 = 1\r\n\t}\r\n}\r\n", result.Text);
    }

    [Fact]
    public void IfLimit_empty_render_removes_member_and_empty_if()
    {
        var one = "fx = {\n\tp_1 = 1\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tp_2 = 1\n\t}\n}\n";
        var result = Apply(one, "p_2(?![0-9])", string.Empty, "c = yes");
        Assert.Equal("fx = {\n\tp_1 = 1\n}\n", result.Text);
        Assert.Equal(new[] { "p_2 = 1" }, result.Removed);
        Assert.Equal(0, result.Replaced);
    }

    [Fact]
    public void IfLimit_comment_inside_if_belongs_to_its_group()
    {
        var commented = Fx.Replace("\t\tp_3 = 1\n", "\t\t#p_2 = 0\n\t\tp_3 = 1\n");
        var result = Apply(commented, "p_2(?![0-9])", "\t\tp_2 = 9\n", "c = yes");
        Assert.Equal(commented.Replace("\t\tp_2 = 1\n\t\t#p_2 = 0\n", "\t\tp_2 = 9\n"), result.Text);
    }

    [Fact]
    public void IfLimit_keeps_hand_written_if_with_only_limit()
    {
        var empty = "fx = {\n\tif = {\n\t\tlimit = {\n\t\t\te = yes\n\t\t}\n\t}\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tp_2 = 1\n\t}\n}\n";
        var result = Apply(empty, "p_2(?![0-9])", "\tp_2 = 1\n", string.Empty);
        Assert.Equal("fx = {\n\tif = {\n\t\tlimit = {\n\t\t\te = yes\n\t\t}\n\t}\n\tp_2 = 1\n}\n", result.Text);
    }

    [Fact]
    public void IfLimit_multi_line_condition_finds_its_block_despite_layout_and_comments()
    {
        var existing = "fx = {\n\tif = {\n\t\tlimit = {\n\t\t\tOR = { # either\n\t\t\t\ta = yes\n\t\t\t\tb = yes\n\t\t\t}\n\t\t}\n\t\tp_1 = 1\n\t}\n}\n";
        var result = Apply(existing, "p_2(?![0-9])", "\t\tp_2 = 1\n", "OR = {\r\n  a = yes\r\n  b = yes\r\n}\r\n");
        Assert.Equal(existing.Replace("\t\tp_1 = 1\n", "\t\tp_1 = 1\n\t\tp_2 = 1\n"), result.Text);
    }

    [Fact]
    public void IfLimit_new_member_does_not_join_if_of_other_category() // insertAfter names the category; the adm if is not the dip if
    {
        var existing = "fx = {\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tadm_1 = 1\n\t}\n\tdip_1 = 1\n}\n";
        var result = MatchSyncer.Apply(existing, "fx", R("dip_2(?![0-9])"), "\t\tdip_2 = 1\n", "\n", R("^dip_"), "c = yes");
        Assert.Equal("fx = {\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tadm_1 = 1\n\t}\n\tdip_1 = 1\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tdip_2 = 1\n\t}\n}\n", result.Text);
        Assert.True(result.Appended);
    }

    [Fact]
    public void IfLimit_moving_member_joins_if_of_its_category_when_several_share_the_limit()
    {
        var existing = "fx = {\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tadm_1 = 1\n\t}\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tdip_1 = 1\n\t}\n\tdip_2 = 1\n}\n";
        var result = MatchSyncer.Apply(existing, "fx", R("dip_2(?![0-9])"), "\t\tdip_2 = 1\n", "\n", R("^dip_"), "c = yes");
        Assert.Equal("fx = {\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tadm_1 = 1\n\t}\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tdip_1 = 1\n\t\tdip_2 = 1\n\t}\n}\n", result.Text);
        Assert.Equal(1, result.Replaced);
    }

    [Fact]
    public void IfLimit_last_member_moving_within_its_category_ends_in_new_if_at_anchor()
    {
        // dip_2 steht doppelt: oben und als einziges Mitglied im dip-if. Ohne die Treffer hält das if keine Einheit mehr, die insertAfter trifft, empfängt also nicht;
        // es fällt leer weg und der Text endet in einem neuen if am Anker
        var existing = "fx = {\n\tdip_2 = 0\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tdip_2 = 1\n\t}\n}\n";
        var result = MatchSyncer.Apply(existing, "fx", R("dip_2(?![0-9])"), "\t\tdip_2 = 9\n", "\n", R("^dip_"), "c = yes");
        Assert.Equal("fx = {\n\tif = {\n\t\tlimit = {\n\t\t\tc = yes\n\t\t}\n\t\tdip_2 = 9\n\t}\n}\n", result.Text);
    }

    [Fact]
    public void IfLimit_condition_matches_despite_spacing_around_braces_and_equals()
    {
        var existing = "fx = {\n\tif = {\n\t\tlimit = {\n\t\t\thas_societal_value = {value=x}\n\t\t}\n\t\tp_1 = 1\n\t}\n}\n";
        var result = Apply(existing, "p_2(?![0-9])", "\t\tp_2 = 1\n", "has_societal_value = { value = x }");
        Assert.Equal(existing.Replace("\t\tp_1 = 1\n", "\t\tp_1 = 1\n\t\tp_2 = 1\n"), result.Text);
    }

    [Fact]
    public void IfLimit_hash_inside_quoted_string_is_not_a_comment()
    {
        var existing = "fx = {\n\tif = {\n\t\tlimit = {\n\t\t\tname = \"a # b\"\n\t\t}\n\t\tp_1 = 1\n\t}\n}\n";
        var result = Apply(existing, "p_2(?![0-9])", "\t\tp_2 = 1\n", "name = \"a # c\"");
        Assert.Equal(existing.Replace("\t}\n}\n", "\t}\n\tif = {\n\t\tlimit = {\n\t\t\tname = \"a # c\"\n\t\t}\n\t\tp_2 = 1\n\t}\n}\n"), result.Text);
    }
}
