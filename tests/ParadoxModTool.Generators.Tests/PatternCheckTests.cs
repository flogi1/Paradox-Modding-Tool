using System.Text.Json.Nodes;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Sofort-Prüfung der Muster in den Masken "Einlesen" und "Nachschlagen" (Spec Teil 5b, Abschnitte 1 und 2).</summary>
public sealed class PatternCheckTests
{
    [Fact]
    public void Keys_needs_group_key()
    {
        Assert.Equal(GeneratorsTexts.PatternGroupMissing("key"), PatternCheck.Keys(@"^m_[A-Z]{3}\.txt$"));
        Assert.Null(PatternCheck.Keys(@"^m_(?<key>[A-Z]{3})\.txt$"));
        Assert.NotNull(PatternCheck.Keys("^m_(?<key>[A-Z"));
    }

    [Fact]
    public void Matching_checks_scriban_and_regex_without_group()
    {
        Assert.Null(PatternCheck.Matching("iv2_ig_{{ category }}_{{ group }}(?![0-9])"));
        Assert.NotNull(PatternCheck.Matching("b_({{ group }}"));
        Assert.NotNull(PatternCheck.Matching("b_{{ group"));
        Assert.Equal(GeneratorsTexts.PatternEmpty, PatternCheck.Matching(" "));
    }

    [Fact]
    public void Value_needs_group_value()
    {
        Assert.Equal(GeneratorsTexts.PatternGroupMissing("value"), PatternCheck.Value(@"religion:\w+"));
        Assert.Null(PatternCheck.Value(@"religion = religion:(?<value>\w+)"));
    }

    [Fact]
    public void Keys_block_needs_key_or_all_key_field_groups()
    {
        var fields = new[] { "category", "group" };
        Assert.Null(PatternCheck.KeysBlock(@"m_(?<category>adm|dip)_(?<group>\d+)_1", fields));
        Assert.Null(PatternCheck.KeysBlock(@"m_(?<key>\w+)_1", fields));
        Assert.NotNull(PatternCheck.KeysBlock(@"m_(?<category>adm|dip)_\d+_1", fields));
        Assert.Null(PatternCheck.KeysFile(@"^m_(adm|dip)\.txt$"));
        Assert.NotNull(PatternCheck.KeysFile("^m_("));
    }

    [Fact]
    public void Numbered_needs_group_number()
    {
        Assert.Equal(GeneratorsTexts.PatternGroupMissing("number"), PatternCheck.Numbered("m_{{ tag }}_1"));
        Assert.Null(PatternCheck.Numbered(@"m_{{ tag }}_(?<number>\d+)(?<suffix>_\d)?"));
    }

    [Fact]
    public void Numbered_accepts_suffix_alone_only_when_asked()
    {
        const string SuffixOnly = @"m_{{ tag }}(?<suffix>_\d)?";
        Assert.Equal(GeneratorsTexts.PatternGroupMissing("number"), PatternCheck.Numbered(SuffixOnly));
        Assert.Equal(GeneratorsTexts.PatternGroupMissing("number"), PatternCheck.Numbered(SuffixOnly, suffixAlone: false));
        Assert.Null(PatternCheck.Numbered(SuffixOnly, suffixAlone: true));
        Assert.Equal(GeneratorsTexts.PatternGroupMissing("number"), PatternCheck.Numbered("m_{{ tag }}", suffixAlone: true));
        Assert.NotNull(PatternCheck.Numbered(@"m_{{ tag }}(?<suffix>_\d", suffixAlone: true));
    }

    [Fact]
    public void Unclosed_scriban_is_reported_not_thrown()
    {
        Assert.NotNull(PatternCheck.Numbered(@"x_{{ tag _(?<number>\d+)"));
        Assert.NotNull(PatternCheck.Path("x_{{ tag"));
    }

    [Fact]
    public void Path_outside_is_reported()
    {
        Assert.Equal(GeneratorsTexts.PathUnsafe("../{{ tag }}.txt"), PatternCheck.Path("../{{ tag }}.txt"));
        Assert.Equal(GeneratorsTexts.PatternEmpty, PatternCheck.Path(" "));
        Assert.Null(PatternCheck.Path("main_menu/x_{{ tag }}.txt"));
    }

    [Fact]
    public void TagFromFile_needs_a_group()
    {
        Assert.Equal(GeneratorsTexts.PatternNeedsGroup, PatternCheck.TagFromFile(@"^country_[A-Z]{3}\.txt$"));
        Assert.Null(PatternCheck.TagFromFile(@"^country_([A-Za-z]{2,5})\.txt$"));
    }

    [Fact]
    public void Valid_iv2_patterns_pass()
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2", "package.json")))!;
        var read = json["read"]!;

        Assert.Null(PatternCheck.Keys(read["keys"]!.GetValue<string>()));
        Assert.Null(PatternCheck.Path(read["elements"]!["file"]!.GetValue<string>()));
        Assert.Null(PatternCheck.Numbered(read["elements"]!["block"]!.GetValue<string>()));
        Assert.All(read["localization"]!.AsArray(), l => Assert.Null(PatternCheck.Numbered(l!["key"]!.GetValue<string>())));
        Assert.All(read["flags"]!.AsArray(), f => Assert.Null(PatternCheck.Numbered(f!["block"]!.GetValue<string>())));
        Assert.Null(PatternCheck.TagFromFile(json["lookups"]![0]!["tagFromFile"]!.GetValue<string>()));
    }
}
