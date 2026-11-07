using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Start-Sprachen und Sprachregeln der IV2-Pakete (<c>languages.defaultLanguages</c>).</summary>
public sealed class DefaultLanguagesTests
{
    private static readonly string[] Profile = { "english", "french", "german", "spanish" };

    [Theory]
    [InlineData("iv2")]
    [InlineData("iv2-dynamic-ideas")]
    [InlineData("iv2-idea-groups")]
    public void Iv2_packages_start_with_german_and_english_and_no_copy_rules(string id)
    {
        var package = GeneratorPackageLoader.Load(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", id));

        var state = GeneratorState.CreateEmpty(package, Profile);

        Assert.Equal(new[] { "english", "german" }, state.Languages);
        Assert.False(state.LanguageRules.ExtendEnglish);
        Assert.False(state.LanguageRules.CopyEnglish);
    }
}
