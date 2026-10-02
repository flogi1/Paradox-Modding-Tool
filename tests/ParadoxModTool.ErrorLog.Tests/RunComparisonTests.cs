using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class RunComparisonTests
{
    [Fact]
    public void LineShiftsCountAsUnchangedAndMissingGroupsAsFixed()
    {
        var previous = Catalogs.Classify(
            "[10:00:00][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/a.txt:680",
            "[10:00:00][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/a.txt:681",
            "[10:00:00][jomini_effect.cpp:1160]: Variable 'iv2_nat_adm_87' is set but is never used.");
        var current = Catalogs.Classify(
            "[11:00:00][jomini_effect.cpp:557]: Unknown effect cmf_suppress at common/a.txt:700",
            "[11:00:00][jomini_effect.cpp:1162]: Variable 'iv2_nat_adm_88' is set but is never used.");

        var diff = RunComparison.Compare(current, previous);

        Assert.Equal(RunStatus.Unchanged, diff.StatusOf(current[0]));
        Assert.Equal(RunStatus.New, diff.StatusOf(current[1]));
        var gone = Assert.Single(diff.Fixed);
        Assert.Equal("iv2_nat_adm_87", gone.Sample.Name);
        Assert.Equal(1, diff.NewCount);
        Assert.Equal(1, diff.FixedCount);
        var unchanged = diff.ByKey[RunComparison.KeyOf(current[0])];
        Assert.Equal(1, unchanged.CurrentCount);
        Assert.Equal(2, unchanged.PreviousCount);
    }

    [Fact]
    public void KeyIgnoresLineNumbersInNamelessMessagesButKeepsDigitsInNames()
    {
        var a = Catalogs.Classify("[01:00:00][b.cpp:1]: Error: \"bad, near line: 4\" in file: \"common/b.txt\" near line: 4")[0];
        var b = Catalogs.Classify("[02:00:00][b.cpp:2]: Error: \"bad, near line: 9\" in file: \"common/b.txt\" near line: 9")[0];
        var c = Catalogs.Classify("[01:00:00][e.cpp:1]: Variable 'v_1' is set but is never used.")[0];
        var d = Catalogs.Classify("[01:00:00][e.cpp:1]: Variable 'v_2' is set but is never used.")[0];

        Assert.Equal(RunComparison.KeyOf(a), RunComparison.KeyOf(b));
        Assert.NotEqual(RunComparison.KeyOf(c), RunComparison.KeyOf(d));
    }

    [Fact]
    public void EmptyPreviousRunMakesEverythingNew()
    {
        var current = Catalogs.Classify("[01:00:00][a.cpp:1]: Unknown effect a at common/a.txt:1");

        var diff = RunComparison.Compare(current, Array.Empty<ErrorEntry>());

        Assert.Equal(RunStatus.New, diff.StatusOf(current[0]));
        Assert.Empty(diff.Fixed);
    }
}
