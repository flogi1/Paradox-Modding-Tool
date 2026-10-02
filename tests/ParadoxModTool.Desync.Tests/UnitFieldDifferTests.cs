using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class UnitFieldDifferTests
{
    private static string Block(int count, string value) =>
        "1={ " + string.Join(" ", Enumerable.Range(0, count).Select(i => $"k{i}={value}")) + " }";

    [Fact]
    public void A_changed_float_is_reported_with_both_values_and_its_path()
    {
        var result = UnitFieldDiffer.Diff(
            "1384={\n\tgovernment={\n\t\tparliament_issue_support=0.47488\n\t}\n}",
            "1384={\n\tgovernment={\n\t\tparliament_issue_support=0.2453\n\t}\n}");

        var field = Assert.Single(result.Fields);
        Assert.Equal("government/parliament_issue_support", field.Path);
        Assert.Equal("0.47488", field.ReferenceValue);
        Assert.Equal("0.2453", field.DesyncedValue);
        Assert.Equal(FieldChangeKind.Changed, field.Kind);
        Assert.False(result.Truncated);
        Assert.False(result.LineFallback);
    }

    [Fact]
    public void Texts_that_differ_only_in_formatting_have_no_differences()
    {
        var result = UnitFieldDiffer.Diff("1={ a=1 b={ c=2 } }", "1={\n a=1\n b={ c=2 }\n}");

        Assert.Empty(result.Fields);
        Assert.False(result.LineFallback);
    }

    [Fact]
    public void Repeated_keys_pair_by_occurrence()
    {
        var result = UnitFieldDiffer.Diff("1={ m=a m=b m=c }", "1={ m=a m=x m=c }");

        var field = Assert.Single(result.Fields);
        Assert.Equal("m#2", field.Path);
        Assert.Equal("b", field.ReferenceValue);
        Assert.Equal("x", field.DesyncedValue);
    }

    [Fact]
    public void The_first_occurrence_is_the_plain_key_whether_or_not_the_key_repeats()
    {
        var single = UnitFieldDiffer.Diff("1={ m=a }", "1={ m=b }");
        var repeated = UnitFieldDiffer.Diff("1={ m=a m=z }", "1={ m=b m=z }");

        Assert.Equal("m", Assert.Single(single.Fields).Path);
        Assert.Equal("m", Assert.Single(repeated.Fields).Path);
    }

    [Fact]
    public void Repeated_keys_in_nested_blocks_get_numbered_at_each_level()
    {
        var result = UnitFieldDiffer.Diff(
            "1={ a={ b=1 } a={ b=2 b=3 } }",
            "1={ a={ b=1 } a={ b=2 b=9 } }");

        Assert.Equal("a#2/b#2", Assert.Single(result.Fields).Path);
    }

    [Fact]
    public void Emitted_paths_match_profile_patterns_without_the_occurrence_suffix()
    {
        var result = UnitFieldDiffer.Diff("1={ x={ m=a m=b } }", "1={ x={ m=a m=c } }");

        var path = Assert.Single(result.Fields).Path;
        Assert.Equal("x/m#2", path);
        Assert.True(PathPattern.Parse("x/m").Matches(path));
    }

    [Fact]
    public void A_changed_operator_on_the_unit_itself_is_one_change_at_the_empty_path()
    {
        var result = UnitFieldDiffer.Diff("x=1", "x>1");

        var field = Assert.Single(result.Fields);
        Assert.Equal(string.Empty, field.Path);
        Assert.Equal("=1", field.ReferenceValue);
        Assert.Equal(">1", field.DesyncedValue);
    }

    [Fact]
    public void An_extra_repeated_key_is_reported_for_its_occurrence_only()
    {
        var result = UnitFieldDiffer.Diff("1={ m=a }", "1={ m=a m=b }");

        var field = Assert.Single(result.Fields);
        Assert.Equal("m#2", field.Path);
        Assert.Null(field.ReferenceValue);
        Assert.Equal("b", field.DesyncedValue);
        Assert.Equal(FieldChangeKind.OnlyDesynced, field.Kind);
    }

    [Fact]
    public void None_against_a_block_is_one_change()
    {
        var result = UnitFieldDiffer.Diff("3003121664=none", "3003121664={ type=CIVIL }");

        var field = Assert.Single(result.Fields);
        Assert.Equal(string.Empty, field.Path);
        Assert.Equal("none", field.ReferenceValue);
        Assert.StartsWith("{", field.DesyncedValue);
        Assert.Contains("type=CIVIL", field.DesyncedValue);
        Assert.Equal(FieldChangeKind.Changed, field.Kind);
    }

    [Fact]
    public void Scalar_lists_compare_as_one_value()
    {
        var result = UnitFieldDiffer.Diff("1={ path={ 1 2 3 } }", "1={ path={ 1 2 4 } }");

        var field = Assert.Single(result.Fields);
        Assert.Equal("path", field.Path);
        Assert.Equal("1 2 3", field.ReferenceValue);
        Assert.Equal("1 2 4", field.DesyncedValue);
    }

    [Fact]
    public void A_scalar_list_that_is_the_unit_itself_has_an_empty_path()
    {
        var result = UnitFieldDiffer.Diff("path={ 1 2 3 }", "path={ 1 2 4 }");

        var field = Assert.Single(result.Fields);
        Assert.Equal(string.Empty, field.Path);
        Assert.Equal("1 2 3", field.ReferenceValue);
    }

    [Fact]
    public void Fields_on_one_side_only_are_reported()
    {
        var result = UnitFieldDiffer.Diff("1={ a=1 only_ref={ x=1 } }", "1={ a=1 only_des=7 }");

        Assert.Equal(2, result.Fields.Count);
        var onlyRef = Assert.Single(result.Fields, f => f.Kind == FieldChangeKind.OnlyReference);
        Assert.Equal("only_ref", onlyRef.Path);
        Assert.Equal("{x=1}", onlyRef.ReferenceValue);
        Assert.Null(onlyRef.DesyncedValue);
        var onlyDes = Assert.Single(result.Fields, f => f.Kind == FieldChangeKind.OnlyDesynced);
        Assert.Equal("only_des", onlyDes.Path);
        Assert.Null(onlyDes.ReferenceValue);
        Assert.Equal("7", onlyDes.DesyncedValue);
    }

    [Fact]
    public void Mixed_list_items_pair_by_position()
    {
        var result = UnitFieldDiffer.Diff("1={ list={ { a=1 } { a=2 } } }", "1={ list={ { a=1 } { a=3 } } }");

        var field = Assert.Single(result.Fields);
        Assert.Equal("list/[1]/a", field.Path);
        Assert.Equal("2", field.ReferenceValue);
        Assert.Equal("3", field.DesyncedValue);
    }

    [Fact]
    public void A_changed_operator_is_a_change()
    {
        var result = UnitFieldDiffer.Diff("1={ a=1 }", "1={ a>1 }");

        var field = Assert.Single(result.Fields);
        Assert.Equal("a", field.Path);
        Assert.Equal("=1", field.ReferenceValue);
        Assert.Equal(">1", field.DesyncedValue);
    }

    [Fact]
    public void Unparsable_text_falls_back_to_a_line_diff()
    {
        var result = UnitFieldDiffer.Diff("1={\n a=1\n b=2\n}", "1={\n a=1\n b=3\n");

        Assert.True(result.LineFallback);
        Assert.Equal(2, result.Fields.Count);
        var changed = result.Fields[0];
        Assert.Equal("line 3", changed.Path);
        Assert.Equal("b=2", changed.ReferenceValue);
        Assert.Equal("b=3", changed.DesyncedValue);
        Assert.Equal(FieldChangeKind.Changed, changed.Kind);
        Assert.Equal("line 4", result.Fields[1].Path);
        Assert.Equal(FieldChangeKind.OnlyReference, result.Fields[1].Kind);
    }

    [Fact]
    public void The_line_fallback_reports_the_differing_line_with_its_number()
    {
        var result = UnitFieldDiffer.Diff("{\nx=1\ny=2\n", "{\nx=1\ny=9\n");

        Assert.True(result.LineFallback);
        var field = Assert.Single(result.Fields);
        Assert.Equal("line 3", field.Path);
    }

    [Fact]
    public void More_than_MaxFields_differences_are_truncated()
    {
        var count = UnitFieldDiffer.MaxFields + 50;

        var result = UnitFieldDiffer.Diff(Block(count, "a"), Block(count, "b"));

        Assert.Equal(UnitFieldDiffer.MaxFields, result.Fields.Count);
        Assert.True(result.Truncated);
    }

    [Fact]
    public void Exactly_MaxFields_differences_are_not_truncated()
    {
        var count = UnitFieldDiffer.MaxFields;

        var result = UnitFieldDiffer.Diff(Block(count, "a"), Block(count, "b"));

        Assert.Equal(count, result.Fields.Count);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void Huge_unparsable_texts_stay_bounded_in_the_line_fallback()
    {
        var lines = Enumerable.Range(0, 20000).Select(i => "line" + i).ToList();
        var reference = "{\n" + string.Join("\n", lines);
        lines[10000] = "changed";
        lines.Insert(5, "inserted");
        var desynced = "{\n" + string.Join("\n", lines);

        var result = UnitFieldDiffer.Diff(reference, desynced);

        Assert.True(result.LineFallback);
        Assert.NotEmpty(result.Fields);
        Assert.True(result.Fields.Count <= UnitFieldDiffer.MaxFields);
    }
}
