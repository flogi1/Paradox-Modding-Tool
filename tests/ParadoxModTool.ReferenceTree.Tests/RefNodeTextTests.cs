using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;
using Xunit;

namespace ParadoxModTool.ReferenceTree.Tests;

public class RefNodeTextTests
{
    private static RefNode Node(RefNodeKind kind, string name = "", CheckKind? check = null, FlagKind? flag = null, SiteLocation? location = null,
        RefBranch? branch = null, RefGroup? group = null, int count = 0, bool partial = false, bool cycle = false) =>
        new(kind, name, check, flag, null, location, FlagOrigin.ActiveMod, "My Mod", RefDirection.Up, branch, group, count, false, partial, cycle, []);

    [Fact]
    public void Site_EventInMod()
    {
        var location = new SiteLocation("C:/mods/my", "events/a.txt", 4);
        var (text, tooltip) = RefNodeText.Format(Node(RefNodeKind.Site, "a.1", CheckKind.Event, location: location));

        Assert.Equal("Event a.1 — a.txt:4", text);
        Assert.EndsWith("My Mod", tooltip);
        Assert.Contains(":4\n", tooltip);
    }

    [Fact]
    public void Site_CycleAndPartial()
    {
        var location = new SiteLocation("C:/mods/my", "events/a.txt", 4);
        var (text, _) = RefNodeText.Format(Node(RefNodeKind.Site, "a.1", CheckKind.Event, location: location, partial: true, cycle: true));

        Assert.StartsWith("↻ ", text);
        Assert.EndsWith("nur Event-Aufrufe bekannt", text);
    }

    [Fact]
    public void Site_WithoutNameShowsOnlyPlace()
    {
        var location = new SiteLocation("C:/mods/my", "events/a.txt", 9);
        Assert.Equal("a.txt:9", RefNodeText.Format(Node(RefNodeKind.Site, location: location)).Text);
    }

    [Fact]
    public void Branch_Count() =>
        Assert.Equal("benutzt von (3)", RefNodeText.Format(Node(RefNodeKind.Branch, branch: RefBranch.UsedBy, count: 3)).Text);

    [Fact]
    public void Group_Vanilla() =>
        Assert.Equal("Vanilla (12)", RefNodeText.Format(Node(RefNodeKind.Group, group: RefGroup.Vanilla, count: 12)).Text);

    [Fact]
    public void Root_Flag() =>
        Assert.Equal("f (Flag)", RefNodeText.Format(Node(RefNodeKind.Root, "f", flag: FlagKind.ScopeFlag)).Text);

    [Fact]
    public void Root_VariableAndPartialCheck()
    {
        Assert.Equal("v (Variable)", RefNodeText.Format(Node(RefNodeKind.Root, "v", flag: FlagKind.GlobalVariable)).Text);
        Assert.Equal("a.1 (Event) — nur Event-Aufrufe bekannt", RefNodeText.Format(Node(RefNodeKind.Root, "a.1", CheckKind.Event, partial: true)).Text);
        Assert.Equal("x (Block)", RefNodeText.Format(Node(RefNodeKind.Root, "x", CheckKind.Named)).Text);
    }
}
