using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

/// <summary>"?=" (gleich, falls vorhanden) und "!=" (ungleich) kommen in CK III und EU V häufig vor.</summary>
public class ScriptOperatorTests
{
    [Theory]
    [InlineData("scope:holder.liege ?= { rite = x }")]
    [InlineData("scope:holder.liege?={ rite = x }")]
    public void Parse_SafeEquals_IsOneStatementWithBlock(string source)
    {
        var root = ScriptDocument.Parse(source);

        var entry = Assert.Single(root.Entries);
        var statement = Assert.IsType<ScriptStatement>(entry);
        Assert.Equal("scope:holder.liege", statement.Key);
        Assert.Equal(ScriptOperator.EqualsIfExists, statement.Operator);
        Assert.IsType<ScriptBlock>(statement.Value);
    }

    [Theory]
    [InlineData("culture != culture:french")]
    [InlineData("culture!=culture:french")]
    public void Parse_NotEquals_IsOneStatement(string source)
    {
        var root = ScriptDocument.Parse(source);

        var statement = Assert.IsType<ScriptStatement>(Assert.Single(root.Entries));
        Assert.Equal("culture", statement.Key);
        Assert.Equal(ScriptOperator.NotEquals, statement.Operator);
        Assert.Equal("culture:french", Assert.IsType<ScriptScalar>(statement.Value).Text);
    }

    [Fact]
    public void Parse_QuestionAndExclamationMarksWithoutEquals_StayPartOfWords()
    {
        var root = ScriptDocument.Parse("a = what? b = wow!\n");

        Assert.Equal("what?", Assert.IsType<ScriptScalar>(Assert.IsType<ScriptStatement>(root.Entries[0]).Value).Text);
        Assert.Equal("wow!", Assert.IsType<ScriptScalar>(Assert.IsType<ScriptStatement>(root.Entries[1]).Value).Text);
    }

    [Fact]
    public void Write_KeepsBothOperators()
    {
        var root = ScriptDocument.Parse("x ?= { a != b }\n");

        var written = ScriptWriter.Write(root);

        Assert.Contains("x ?= {", written);
        Assert.Contains("a != b", written);
        Assert.DoesNotContain("? =", written);
        Assert.DoesNotContain("! =", written);
    }

    [Fact]
    public void Canonical_DistinguishesSafeEqualsFromEquals()
    {
        var safe = Assert.Single(ScriptDocument.Parse("x ?= { a = 1 }").Entries);
        var plain = Assert.Single(ScriptDocument.Parse("x = { a = 1 }").Entries);
        var not = Assert.Single(ScriptDocument.Parse("x != 1").Entries);
        var equal = Assert.Single(ScriptDocument.Parse("x = 1").Entries);

        Assert.NotEqual(SiblingMatcher.Canonical(plain), SiblingMatcher.Canonical(safe));
        Assert.NotEqual(SiblingMatcher.Canonical(equal), SiblingMatcher.Canonical(not));
    }

    [Fact]
    public void Merge_RewrittenBlockWithSafeEquals_StaysValid()
    {
        // Der reale Fall: tower_of_silence_01 kam nach einem Update-Merge als "scope:holder.liege" + "? = {" heraus.
        const string block = "tower = {\n\tis_enabled = {\n\t\tscope:holder.liege ?= {\n\t\t\trite = {0}\n\t\t}\n\t}\n}\n";
        var old = ScriptDocument.Parse(block.Replace("{0}", "a"));
        var newVanilla = ScriptDocument.Parse(block.Replace("{0}", "b"));
        var mod = ScriptDocument.Parse(block.Replace("{0}", "a") + "extra = 1\n");

        var merged = MergeEngine.Merge(old, newVanilla, mod).MergedRoot;
        var written = ScriptWriter.Write(merged);

        Assert.Contains("scope:holder.liege ?= {", written);
        Assert.DoesNotContain("? =", written);
        Assert.Contains("rite = b", written);
    }
}
