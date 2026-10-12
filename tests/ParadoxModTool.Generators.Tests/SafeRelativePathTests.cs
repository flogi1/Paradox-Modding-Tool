using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Prüfung relativer Pfade (Spec Teil 5a, Pfadschutz): nichts darf den Zielordner verlassen.</summary>
public sealed class SafeRelativePathTests
{
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("../x")]
    [InlineData("a/../../x")]
    [InlineData("a\\..\\x")]
    [InlineData("C:/x")]
    [InlineData("c:x")]
    [InlineData("/x")]
    [InlineData("\\x")]
    [InlineData("\\\\server\\x")]
    [InlineData("a/./../..")]
    public void IsSafe_refuses_escaping_paths(string path) => Assert.False(SafeRelativePath.IsSafe(path));

    [Theory]
    [InlineData("in_game/common/x_TST.txt")]
    [InlineData("main_menu/localization/english/a_l_english.yml")]
    [InlineData("a..b/c.txt")]
    public void IsSafe_accepts_relative_paths(string path) => Assert.True(SafeRelativePath.IsSafe(path));
}
