using System.Text.RegularExpressions;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Muster-Vorschlag aus einem Beispiel der Mod (Spec Teil 5b, Abschnitt 1 "Vorschlag").</summary>
public sealed class PatternSuggesterTests
{
    [Fact]
    public void Keys_from_iv2_path()
    {
        var suggestion = PatternSuggester.Keys("main_menu/common/static_modifiers/02_IV2_BYZ.txt", "BYZ", "^[A-Z0-9]{2,5}$");

        Assert.Equal(@"^main_menu/common/static_modifiers/02_IV2_(?<key>[A-Z0-9]{2,5})\.txt$", suggestion.Pattern);
        Assert.Null(suggestion.Failure);
    }

    [Fact]
    public void FileTemplate_from_iv2_path()
    {
        Assert.Equal("in_game/common/generic_actions/iv2_GA_ideas_{{ tag }}.txt",
            PatternSuggester.FileTemplate("in_game/common/generic_actions/iv2_GA_ideas_BYZ.txt", "BYZ", "tag").Pattern);
    }

    [Theory]
    [InlineData("national_idea_modifier_BYZ_3_1", @"national_idea_modifier_{{ tag }}_(?<number>\d+)(?<suffix>_\d+)?")]
    [InlineData("iv2_ga_select_historic_BYZ_3_desc", @"iv2_ga_select_historic_{{ tag }}_(?<number>\d+)_desc")]
    [InlineData("iv2_historic_idea_BYZ_3", @"iv2_historic_idea_{{ tag }}_(?<number>\d+)")]
    public void Numbered_from_iv2_keys(string example, string expected) =>
        Assert.Equal(expected, PatternSuggester.Numbered(example, "BYZ", "tag").Pattern);

    [Fact]
    public void Key_twice_replaced()
    {
        var pattern = PatternSuggester.Numbered("x_BYZ_BYZ_3", "BYZ", "tag").Pattern;

        Assert.Equal(@"x_{{ tag }}_{{ tag }}_(?<number>\d+)", pattern);
    }

    [Fact]
    public void Key_inside_word_not_replaced()
    {
        var suggestion = PatternSuggester.Numbered("BYZANTIUM_3", "BYZ", "tag");

        Assert.Null(suggestion.Pattern);
        Assert.Equal(SuggestFailure.KeyMissing, suggestion.Failure);
    }

    [Fact]
    public void No_number_fails()
    {
        Assert.Equal(SuggestFailure.NumberMissing, PatternSuggester.Numbered("x_BYZ", "BYZ", "tag").Failure);
    }

    [Fact]
    public void KeyClass_fallback()
    {
        Assert.Equal("[A-Za-z0-9_]+", PatternSuggester.KeyClass(null));
        Assert.Equal("[A-Za-z0-9_]+", PatternSuggester.KeyClass("^(a|b)$"));
        Assert.Equal("[A-Z]+", PatternSuggester.KeyClass("[A-Z]+"));
    }

    [Fact]
    public void Literal_parts_are_escaped()
    {
        Assert.Equal(@"^a\.b\+c/(?<key>[A-Za-z0-9_]+)\.txt$", PatternSuggester.Keys("a.b+c/BYZ.txt", "BYZ", null).Pattern);
    }

    [Fact]
    public void Suggestions_match_like_iv2_patterns()
    {
        using var fixture = new Iv2Fixture();
        SlicePackage.Write(fixture.Plan(fixture.State(), fixture.FreshMod));
        var file = "main_menu/common/static_modifiers/02_IV2_TST.txt";
        var text = File.ReadAllText(Path.Combine(fixture.FreshMod, file));
        var example = Regex.Match(text, @"^(national_idea_modifier_TST_\d+_\d+)\s*=", RegexOptions.Multiline).Groups[1].Value;
        Assert.NotEmpty(example);
        var read = fixture.Package.Read!;
        var tag = fixture.Package.Head.Single(f => f.Id == fixture.Package.KeyField);
        var suggested = fixture.Package with
        {
            Read = read with
            {
                Keys = PatternSuggester.Keys(file, "TST", tag.Pattern).Pattern!,
                Elements = read.Elements with { Block = PatternSuggester.Numbered(example, "TST", tag.Id).Pattern! },
            },
        };
        var languages = new[] { "english", "german" };

        Assert.Equal(GeneratorStateReader.Keys(fixture.Package, fixture.FreshMod), GeneratorStateReader.Keys(suggested, fixture.FreshMod));
        var want = GeneratorStateReader.Read(fixture.Package, fixture.FreshMod, "TST", languages)!.State;
        var have = GeneratorStateReader.Read(suggested, fixture.FreshMod, "TST", languages)!.State;
        Assert.Equal(Modifiers(want), Modifiers(have));
    }

    private static IReadOnlyList<string> Modifiers(GeneratorState state) =>
        state.Head.Children["categories"]
            .SelectMany(c => c.Children["ideas"])
            .SelectMany(i => i.Children["levels"])
            .Select(l => l.Values.GetValueOrDefault("modifiers") ?? string.Empty)
            .ToList();
}
