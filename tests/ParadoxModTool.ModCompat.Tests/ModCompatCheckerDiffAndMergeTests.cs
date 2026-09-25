using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class ModCompatCheckerDiffAndMergeTests
{
    private static ScriptStatement Find(ScriptBlock block, string key) =>
        block.Entries.OfType<ScriptStatement>().First(s => s.Key == key);

    private static string ValueText(ScriptStatement stmt) => Assert.IsType<ScriptScalar>(stmt.Value).Text;

    [Fact]
    public void ComputeParallelDiff_ZeigtAbweichungenJedesModsUnabhaengig()
    {
        var vanilla = ScriptDocument.Parse("cost = 100\n");
        var modA = ScriptDocument.Parse("cost = 150\n");
        var modB = ScriptDocument.Parse("cost = 100\nbonus = 5\n");

        var result = ModCompatChecker.ComputeParallelDiff(vanilla, modA, modB);

        var modADiff = Assert.Single(result.ModADiff);
        Assert.Equal(DiffKind.Modified, modADiff.Kind);

        Assert.Equal(2, result.ModBDiff.Count);
        Assert.Equal(DiffKind.Unchanged, result.ModBDiff[0].Kind);
        Assert.Equal(DiffKind.Added, result.ModBDiff[1].Kind);
    }

    [Fact]
    public void AutoMergeAgainstVanilla_NichtUeberlappendeAenderungen_WerdenAutomatischZusammengefuehrt()
    {
        var vanilla = ScriptDocument.Parse("cost = 100\nother = 1\n");
        var modA = ScriptDocument.Parse("cost = 150\nother = 1\n");
        var modB = ScriptDocument.Parse("cost = 100\nother = 1\nbonus = 5\n");

        var result = ModCompatChecker.AutoMergeAgainstVanilla(vanilla, modA, modB);

        Assert.False(result.HasConflicts);
        Assert.Equal("150", ValueText(Find(result.MergedRoot, "cost")));
        Assert.Equal("5", ValueText(Find(result.MergedRoot, "bonus")));
    }

    [Fact]
    public void AutoMergeAgainstVanilla_UeberlappendeAenderung_MeldetKonfliktMitBeidenWerten()
    {
        var vanilla = ScriptDocument.Parse("cost = 100\n");
        var modA = ScriptDocument.Parse("cost = 150\n");
        var modB = ScriptDocument.Parse("cost = 200\n");

        var result = ModCompatChecker.AutoMergeAgainstVanilla(vanilla, modA, modB);

        Assert.True(result.HasConflicts);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("cost", conflict.Path);
        Assert.Equal("150", Assert.IsType<ScriptScalar>(conflict.ModAValue).Text);
        Assert.Equal("200", Assert.IsType<ScriptScalar>(conflict.ModBValue).Text);
        Assert.Contains("Mod A", conflict.Description);
        Assert.Contains("Mod B", conflict.Description);
    }

    [Fact]
    public void AutoMergeAgainstVanilla_VerschachtelterBlockTeilweiseUeberlappend_WirdRekursivZusammengefuehrt()
    {
        var vanilla = ScriptDocument.Parse("idea = {\n a = 1\n b = 2\n}\n");
        var modA = ScriptDocument.Parse("idea = {\n a = 10\n b = 2\n}\n");
        var modB = ScriptDocument.Parse("idea = {\n a = 1\n b = 20\n}\n");

        var result = ModCompatChecker.AutoMergeAgainstVanilla(vanilla, modA, modB);

        Assert.False(result.HasConflicts);
        var ideaBlock = Assert.IsType<ScriptBlock>(Find(result.MergedRoot, "idea").Value);
        Assert.Equal("10", ValueText(Find(ideaBlock, "a")));
        Assert.Equal("20", ValueText(Find(ideaBlock, "b")));
    }
}
