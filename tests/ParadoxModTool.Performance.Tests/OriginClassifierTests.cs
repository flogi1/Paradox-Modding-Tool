using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class OriginClassifierTests
{
    private const string Path = "common/on_action/location_pulses.txt";

    private static OriginClassifier Create(TempTree mod, TempTree? other = null, TempTree? vanilla = null) =>
        new(mod.Root,
            other is null ? Array.Empty<string>() : new[] { other.Root },
            vanilla is null ? Array.Empty<string>() : new[] { vanilla.Root });

    [Fact]
    public void FileOnlyInActiveModIsMod()
    {
        using var mod = new TempTree(("in_game/" + Path, "x"));
        using var vanilla = new TempTree();
        var classifier = Create(mod, vanilla: vanilla);

        Assert.Equal(RowOrigin.Mod, classifier.Classify(Path));
    }

    [Fact]
    public void ModFileUnderMainMenuAndVanillaUnderInGameIsBoth()
    {
        using var mod = new TempTree(("main_menu/" + Path, "x"));
        using var vanilla = new TempTree(("in_game/" + Path, "x"));
        var classifier = Create(mod, vanilla: vanilla);

        Assert.Equal(RowOrigin.Both, classifier.Classify(Path));
    }

    [Fact]
    public void FileOnlyInAnotherModIsOtherMod()
    {
        using var mod = new TempTree();
        using var other = new TempTree((Path, "x"));
        using var vanilla = new TempTree();
        var classifier = Create(mod, other, vanilla);

        Assert.Equal(RowOrigin.OtherMod, classifier.Classify(Path));
    }

    [Fact]
    public void FileOnlyInVanillaIsVanilla()
    {
        using var mod = new TempTree();
        using var vanilla = new TempTree(("loading_screen/" + Path, "x"));
        var classifier = Create(mod, vanilla: vanilla);

        Assert.Equal(RowOrigin.Vanilla, classifier.Classify(Path));
    }

    [Fact]
    public void FileNowhereIsUnknown()
    {
        using var mod = new TempTree();
        using var other = new TempTree();
        using var vanilla = new TempTree();
        var classifier = Create(mod, other, vanilla);

        Assert.Equal(RowOrigin.Unknown, classifier.Classify(Path));
    }

    [Fact]
    public void ProbesEachFileOnlyOnceAcrossRepeatedClassify()
    {
        var calls = new List<string>();
        var classifier = new OriginClassifier(
            "mod",
            new[] { "other" },
            new[] { "vanilla" },
            p =>
            {
                calls.Add(p);
                return false;
            });

        Assert.Equal(RowOrigin.Unknown, classifier.Classify(Path));
        var afterFirst = calls.Count;
        Assert.True(afterFirst > 0);
        Assert.Equal(RowOrigin.Unknown, classifier.Classify(Path));

        Assert.Equal(afterFirst, calls.Count);
        Assert.Equal(calls.Count, calls.Distinct().Count());
    }

    [Fact]
    public void BlankRootsAreNeverProbedAndDoNotMatchTheWorkingDirectory()
    {
        var probed = new List<string>();
        var vanillaRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "van");
        var vanillaFile = System.IO.Path.Combine(vanillaRoot, "in_game", Path.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var classifier = new OriginClassifier(
            string.Empty,
            new[] { string.Empty, "  " },
            new[] { string.Empty, vanillaRoot },
            p =>
            {
                probed.Add(p);
                return p == vanillaFile;
            });

        Assert.Equal(RowOrigin.Vanilla, classifier.Classify(Path));
        Assert.NotEmpty(probed);
        Assert.All(probed, p => Assert.StartsWith(vanillaRoot, p, StringComparison.Ordinal));
        Assert.All(probed, p => Assert.True(System.IO.Path.IsPathRooted(p), $"relative path probed: {p}"));
    }

    [Fact]
    public void WithOnlyBlankRootsEverythingIsUnknownWithoutProbing()
    {
        var probed = new List<string>();
        var classifier = new OriginClassifier(" ", new[] { string.Empty }, new[] { string.Empty }, p =>
        {
            probed.Add(p);
            return true;
        });

        Assert.Equal(RowOrigin.Unknown, classifier.Classify(Path));
        Assert.Empty(probed);
    }

    [Fact]
    public void ModSideIsModAndBothOnly()
    {
        Assert.Equal(new[] { RowOrigin.Mod, RowOrigin.Both }.Order(), OriginClassifier.ModSide.Order());
        foreach (var origin in Enum.GetValues<RowOrigin>())
        {
            Assert.Equal(OriginClassifier.ModSide.Contains(origin), OriginClassifier.IsModSide(origin));
        }
    }
}
