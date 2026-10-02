using ParadoxModTool.Mods;
using ParadoxModTool.SteamIntegration;
using Xunit;

namespace ParadoxModTool.ModOverview.Tests;

public class OverviewAlertsTests
{
    private static readonly EffectiveLinks NoLinks = new(null, null, null, null, false);

    private static ModMetadata Meta(string name = "M") => new(name, "1.0", "1.37.*", "C:\\mods\\" + name);

    private static OverviewMod Mod(
        string name,
        string game = "eu4",
        ModMetadata? metadata = null,
        bool noMetadata = false,
        IReadOnlyList<string>? missing = null,
        VersionSupport support = VersionSupport.Supported,
        ReleaseStatus? status = null,
        string? statusProblem = null,
        SteamCacheEntry? steam = null,
        string? workshopId = null,
        ModRole role = ModRole.Main)
    {
        var mod = new OverviewMod
        {
            GameId = game,
            Mod = new ModInfo { Id = name, Name = name, SteamWorkshopId = workshopId, Role = role },
            Metadata = noMetadata ? null : metadata ?? Meta(name),
            WorkshopId = workshopId,
            MissingPaths = missing ?? Array.Empty<string>(),
            VersionSupport = support,
            ReleaseStatus = status ?? new ReleaseStatus("1.0", null, "1.0", Array.Empty<string>()),
            ReleaseStatusProblem = statusProblem,
            Steam = steam,
            Links = NoLinks,
        };
        mod.ProjectRoot = mod;
        return mod;
    }

    private static OverviewSnapshot Snapshot(params OverviewMod[] mods) => new(
        new[] { new OverviewGame("eu4", "Europa Universalis IV", "v1.37.5.0"), new OverviewGame("eu5", "Europa Universalis V", null) },
        mods,
        Array.Empty<OverviewGameError>(),
        null);

    private static IReadOnlyList<OverviewAlert> Alerts(OverviewAlertKind kind, params OverviewMod[] mods) =>
        OverviewAlerts.Compute(Snapshot(mods)).Where(a => a.Kind == kind).ToList();

    [Fact]
    public void MissingFolder_OnePerMissingPath()
    {
        var alerts = Alerts(OverviewAlertKind.MissingFolder, Mod("A", missing: new[] { "C:\\x", "C:\\y" }), Mod("B"));

        Assert.Equal(2, alerts.Count);
        Assert.All(alerts, a => Assert.Equal("A", a.ModId));
        Assert.Contains("C:\\x", alerts[0].Text);
    }

    [Fact]
    public void MissingDescriptor_WhenMetadataNull()
    {
        var alert = Assert.Single(Alerts(OverviewAlertKind.MissingDescriptor, Mod("A", noMetadata: true), Mod("B")));
        Assert.Equal("A", alert.ModId);
    }

    [Fact]
    public void Outdated_OnlyForOutdated_NotUnknown()
    {
        var alert = Assert.Single(Alerts(OverviewAlertKind.OutdatedGameVersion,
            Mod("A", support: VersionSupport.Outdated, metadata: Meta("A") with { SupportedGameVersion = "1.36.*" }),
            Mod("B", support: VersionSupport.Unknown),
            Mod("C")));

        Assert.Equal("A", alert.ModId);
        Assert.Contains("1.36.*", alert.Text);
        Assert.Contains("v1.37.5.0", alert.Text);
    }

    [Fact]
    public void ReleaseStatus_WhenHintsOrProblem()
    {
        var alerts = Alerts(OverviewAlertKind.ReleaseStatus,
            Mod("A", status: new ReleaseStatus("1.1", null, "1.0", new[] { "h1", "h2" })),
            Mod("B", statusProblem: "locked"),
            Mod("C"));

        Assert.Equal(new[] { "A", "B" }, alerts.Select(a => a.ModId));
        Assert.Contains("h1; h2", alerts[0].Text);
        Assert.Contains("locked", alerts[1].Text);
    }

    [Fact]
    public void SteamPage_HiddenAndNotFound_OnlyWithWorkshopId()
    {
        var alerts = Alerts(OverviewAlertKind.SteamPage,
            Mod("A", workshopId: "1", steam: new SteamCacheEntry { Id = "1", State = WorkshopPageState.Hidden }),
            Mod("B", workshopId: "2", steam: new SteamCacheEntry { Id = "2", State = WorkshopPageState.NotFound }),
            Mod("C", workshopId: "3", steam: new SteamCacheEntry { Id = "3", State = WorkshopPageState.Ok }),
            Mod("D", steam: new SteamCacheEntry { Id = "4", State = WorkshopPageState.Hidden }));

        Assert.Equal(new[] { "A", "B" }, alerts.Select(a => a.ModId));
        Assert.NotEqual(alerts[0].Text, alerts[1].Text);
    }

    [Fact]
    public void SubmodWithoutDependency()
    {
        var main = Mod("Main");
        var lonely = Mod("Lonely", role: ModRole.Submod);
        lonely.ProjectRoot = main;
        var good = Mod("Good", role: ModRole.Submod);
        good.ProjectRoot = main;
        good.ResolvedDependencies = new[] { (new ModDependency(null, "Main"), (OverviewMod?)main) };
        var dev = Mod("Dev", role: ModRole.DevBranch);
        dev.ProjectRoot = main;

        var alert = Assert.Single(Alerts(OverviewAlertKind.SubmodWithoutDependency, main, lonely, good, dev));

        Assert.Equal("Lonely", alert.ModId);
        Assert.Contains("Main", alert.Text);
    }

    [Fact]
    public void DependencyNotInProject()
    {
        var main = Mod("Main");
        var other = Mod("Other");
        var addon = Mod("Addon", role: ModRole.Submod);
        addon.ProjectRoot = main;
        addon.ResolvedDependencies = new[]
        {
            (new ModDependency(null, "Main"), (OverviewMod?)main),
            (new ModDependency(null, "Other"), (OverviewMod?)other),
            (new ModDependency(null, "Beyond Typus"), (OverviewMod?)null),
        };

        var alert = Assert.Single(Alerts(OverviewAlertKind.DependencyNotInProject, main, other, addon));

        Assert.Equal("Addon", alert.ModId);
        Assert.Contains("Other", alert.Text);
    }

    [Fact]
    public void ForSourceErrors_CarriesDetail()
    {
        var root = Mod("Main");

        var alert = Assert.Single(OverviewAlerts.ForSourceErrors(new[] { new OverviewBugSourceError(root, "flogi/iv", "gh not found") }));

        Assert.Equal(OverviewAlertKind.BugSourceFailed, alert.Kind);
        Assert.Equal("Main", alert.ModId);
        Assert.Equal("eu4", alert.GameId);
        Assert.Contains("flogi/iv", alert.Text);
        Assert.Equal("gh not found", alert.Detail);
    }

    [Fact]
    public void Order_GameThenNameThenKind()
    {
        var alerts = OverviewAlerts.Compute(Snapshot(
            Mod("Zeta", game: "eu5", noMetadata: true),
            Mod("Beta", noMetadata: true, missing: new[] { "C:\\b" }),
            Mod("Alpha", support: VersionSupport.Outdated)));

        Assert.Equal(
            new[] { ("Alpha", OverviewAlertKind.OutdatedGameVersion), ("Beta", OverviewAlertKind.MissingFolder), ("Beta", OverviewAlertKind.MissingDescriptor), ("Zeta", OverviewAlertKind.MissingDescriptor) },
            alerts.Select(a => (a.ModId, a.Kind)));
        Assert.Equal("eu5", alerts[^1].GameId);
    }
}
