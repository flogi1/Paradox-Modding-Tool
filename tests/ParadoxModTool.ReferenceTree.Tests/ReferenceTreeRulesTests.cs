using ParadoxModTool.FlagTracker;
using ParadoxModTool.ReferenceTree;
using Xunit;

namespace ParadoxModTool.ReferenceTree.Tests;

public class ReferenceTreeRulesTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    public void ContentChanged_OnlyOnRealChange(bool before, bool after, bool expected) =>
        Assert.Equal(expected, ReferenceTreeRules.ContentChanged(before, after));

    [Fact]
    public void KeepsContentAfterReset_HintOnUsedTab_StaysVisible() =>
        Assert.True(ReferenceTreeRules.KeepsContentAfterReset(ReferenceTreeTexts.ReloadHint, showedContent: true));

    [Fact]
    public void KeepsContentAfterReset_EmptyHint_Hides() =>
        Assert.False(ReferenceTreeRules.KeepsContentAfterReset(string.Empty, showedContent: true));

    [Fact]
    public void KeepsContentAfterReset_HintOnUnusedTab_DoesNotShow() =>
        Assert.False(ReferenceTreeRules.KeepsContentAfterReset(ReferenceTreeTexts.NotSupported, showedContent: false));

    [Fact]
    public void OpensOnDoubleClick_DownSiteText_Opens() =>
        Assert.True(ReferenceTreeRules.OpensOnDoubleClick(Node(RefNodeKind.Site, RefDirection.Down, Location()), onExpander: false));

    [Fact]
    public void OpensOnDoubleClick_UpSiteText_Opens() =>
        Assert.True(ReferenceTreeRules.OpensOnDoubleClick(Node(RefNodeKind.Site, RefDirection.Up, Location()), onExpander: false));

    [Theory]
    [InlineData(RefDirection.Up)]
    [InlineData(RefDirection.Down)]
    public void OpensOnDoubleClick_OnExpander_OnlyToggles(RefDirection direction) =>
        Assert.False(ReferenceTreeRules.OpensOnDoubleClick(Node(RefNodeKind.Site, direction, Location()), onExpander: true));

    [Fact]
    public void OpensOnDoubleClick_SiteWithoutLocation_DoesNothing() =>
        Assert.False(ReferenceTreeRules.OpensOnDoubleClick(Node(RefNodeKind.Site, RefDirection.Down, null), onExpander: false));

    [Fact]
    public void OpensOnDoubleClick_Branch_DoesNothing() =>
        Assert.False(ReferenceTreeRules.OpensOnDoubleClick(Node(RefNodeKind.Branch, RefDirection.Down, Location()), onExpander: false));

    [Theory]
    [InlineData(false, false, ReferenceAvailabilityStep.ShowNotSupported)]
    [InlineData(false, true, ReferenceAvailabilityStep.None)]
    [InlineData(true, true, ReferenceAvailabilityStep.ClearNotSupported)]
    [InlineData(true, false, ReferenceAvailabilityStep.None)]
    public void Availability_ResetsOnlyOnChange(bool available, bool showsNotSupported, ReferenceAvailabilityStep expected) =>
        Assert.Equal(expected, ReferenceTreeRules.Availability(available, showsNotSupported));

    [Fact]
    public void Availability_RepeatedWhileUnavailable_ResetsOnce()
    {
        var shows = false;
        var resets = 0;
        for (var i = 0; i < 3; i++)
        {
            if (ReferenceTreeRules.Availability(available: false, shows) == ReferenceAvailabilityStep.ShowNotSupported)
            {
                resets++;
                shows = true;
            }
        }

        Assert.Equal(1, resets);
    }

    // building, empty, failed, reload, firstPass -> erwartet
    [Theory]
    [InlineData(true, false, false, true, true, false)]   // läuft schon: warten, auch mit ⟳
    [InlineData(false, true, false, false, true, true)]   // leer beim ersten Mal: bauen
    [InlineData(false, false, true, false, true, true)]   // fehlgeschlagen beim ersten Mal: bauen
    [InlineData(false, false, false, true, true, true)]   // bereit mit ⟳: bauen
    [InlineData(false, false, false, false, true, false)] // bereit: Index nehmen
    [InlineData(false, true, false, false, false, true)]  // fremder Aufbau abgebrochen ohne Index: eigener Aufbau
    [InlineData(false, false, false, true, false, false)] // fremder Aufbau abgebrochen mit Index (bereit): Index nehmen, kein zweites ⟳
    [InlineData(false, false, true, false, false, false)] // fremder Aufbau fehlgeschlagen: Fehler zeigen
    public void StartsOwnBuild(bool building, bool empty, bool failed, bool reload, bool firstPass, bool expected) =>
        Assert.Equal(expected, ReferenceTreeRules.StartsOwnBuild(building, empty, failed, reload, firstPass));

    private static SiteLocation Location() => new("C:/mod", "common/x.txt", 3);

    private static RefNode Node(RefNodeKind kind, RefDirection direction, SiteLocation? location) =>
        new(kind, "name", null, null, null, location, FlagOrigin.ActiveMod, "Mod", direction, null, null, 0, Expandable: true, Partial: false, Cycle: false,
            Array.Empty<string>());
}
