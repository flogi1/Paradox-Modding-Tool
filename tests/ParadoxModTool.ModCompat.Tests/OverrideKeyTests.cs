using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class OverrideKeyTests
{
    [Theory]
    [InlineData("INJECT:foo", OverrideOperator.Inject, 4)]
    [InlineData("REPLACE:foo", OverrideOperator.Replace, 5)]
    [InlineData("TRY_INJECT:foo", OverrideOperator.TryInject, 2)]
    [InlineData("TRY_REPLACE:foo", OverrideOperator.TryReplace, 3)]
    [InlineData("INJECT_OR_CREATE:foo", OverrideOperator.InjectOrCreate, 0)]
    [InlineData("REPLACE_OR_CREATE:foo", OverrideOperator.ReplaceOrCreate, 1)]
    public void Parse_reads_prefix_and_order(string raw, OverrideOperator op, int order)
    {
        var key = OverrideKey.Parse(raw);
        Assert.Equal(op, key.Operator);
        Assert.Equal("foo", key.Key);
        Assert.Equal(order, key.Order);
    }

    [Fact]
    public void Parse_without_prefix_is_plain_definition()
    {
        var key = OverrideKey.Parse("building_x");
        Assert.Equal(OverrideOperator.None, key.Operator);
        Assert.Equal("building_x", key.Key);
        Assert.Equal(-1, key.Order);
        Assert.False(key.IsInject);
        Assert.False(key.IsReplace);
    }

    [Fact]
    public void Parse_prefix_without_name_stays_plain()
    {
        var key = OverrideKey.Parse("INJECT:");
        Assert.Equal(OverrideOperator.None, key.Operator);
        Assert.Equal("INJECT:", key.Key);
    }

    [Fact]
    public void Parse_is_case_sensitive()
    {
        Assert.Equal(OverrideOperator.None, OverrideKey.Parse("inject:foo").Operator);
    }

    [Fact]
    public void Inject_and_replace_families()
    {
        Assert.True(OverrideKey.Parse("TRY_INJECT:a").IsInject);
        Assert.True(OverrideKey.Parse("INJECT_OR_CREATE:a").IsInject);
        Assert.True(OverrideKey.Parse("REPLACE_OR_CREATE:a").IsReplace);
        Assert.True(OverrideKey.Parse("TRY_REPLACE:a").IsReplace);
    }

    [Fact]
    public void Script_parser_reads_prefixed_key_as_one_identifier()
    {
        var root = ScriptDocument.Parse("INJECT:building_x = { a = 1 }\nREPLACE:building_y = { b = 2 }\n");
        var keys = root.Entries.OfType<ScriptStatement>().Select(s => s.Key).ToList();
        Assert.Equal(new[] { "INJECT:building_x", "REPLACE:building_y" }, keys);
    }
}
