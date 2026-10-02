using Xunit;

namespace ParadoxModTool.ModOverview.Tests;

public class OverviewAlertFilterTests
{
    private static OverviewAlert Alert(OverviewAlertKind kind, string mod = "m") => new(kind, "eu4", mod, "x");

    [Fact]
    public void CategoriesCoverEveryAlertKindPlusBugsAndWorkshop()
    {
        var expected = Enum.GetNames<OverviewAlertKind>().Append(OverviewAlertFilter.NewBugs).Append(OverviewAlertFilter.WorkshopId);

        Assert.Equal(expected.OrderBy(n => n), OverviewAlertFilter.Categories.OrderBy(n => n));
        Assert.Equal(OverviewAlertFilter.NewBugs, OverviewAlertFilter.Categories[0]);
    }

    [Fact]
    public void Visible_DropsHiddenKinds()
    {
        var alerts = new[] { Alert(OverviewAlertKind.ReleaseStatus), Alert(OverviewAlertKind.MissingFolder), Alert(OverviewAlertKind.ReleaseStatus, "n") };

        var visible = OverviewAlertFilter.Visible(alerts, new[] { nameof(OverviewAlertKind.ReleaseStatus) });

        Assert.Equal(new[] { OverviewAlertKind.MissingFolder }, visible.Select(a => a.Kind));
        Assert.Equal(3, OverviewAlertFilter.Visible(alerts, Array.Empty<string>()).Count);
    }

    [Fact]
    public void Counts_PerCategory()
    {
        var alerts = new[] { Alert(OverviewAlertKind.ReleaseStatus), Alert(OverviewAlertKind.ReleaseStatus, "n"), Alert(OverviewAlertKind.SteamPage) };

        var counts = OverviewAlertFilter.Counts(alerts, newBugs: 5, workshopId: 2);

        Assert.Equal(2, counts[nameof(OverviewAlertKind.ReleaseStatus)]);
        Assert.Equal(1, counts[nameof(OverviewAlertKind.SteamPage)]);
        Assert.Equal(0, counts[nameof(OverviewAlertKind.MissingFolder)]);
        Assert.Equal(5, counts[OverviewAlertFilter.NewBugs]);
        Assert.Equal(2, counts[OverviewAlertFilter.WorkshopId]);
        Assert.Equal(OverviewAlertFilter.Categories.Count, counts.Count);
    }
}
