using ParadoxModTool.FlagTracker;
using ParadoxModTool.ReferenceTree;
using ParadoxModTool.ScriptCheck;
using Xunit;

namespace ParadoxModTool.ReferenceTree.Tests;

public class ReferenceTreeModelTests
{
    private static ReferenceTreeModel Build(ReferenceTestRoots roots, string game = "ck3") =>
        new(new ReferenceSources(roots.BuildFlags(game), roots.BuildChecks(game)));

    private static RefNode BranchOf(ReferenceTreeModel model, RefNode node, RefBranch branch) =>
        model.Children(node).Single(n => n.NodeKind == RefNodeKind.Branch && n.Branch == branch);

    private static RefNode SiteNamed(ReferenceTreeModel model, RefNode node, string name) =>
        model.Children(node).Single(n => n.NodeKind == RefNodeKind.Site && n.Name == name);

    [Fact]
    public void FlagRootBranches()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt",
            "namespace = a\na.1 = {\n\timmediate = {\n\t\tset_variable = my_var\n\t}\n}\na.2 = {\n\ttrigger = {\n\t\thas_variable = my_var\n\t}\n}");
        var model = Build(roots);
        var root = Assert.Single(model.Roots("my_var"));
        Assert.Equal(RefNodeKind.Root, root.NodeKind);
        Assert.Equal(FlagKind.Variable, root.FlagKind);
        Assert.Null(root.CheckKind);
        var children = model.Children(root);
        Assert.Equal(2, children.Count);
        Assert.Equal(RefBranch.Set, children[0].Branch);
        Assert.Equal(1, children[0].Count);
        Assert.Equal(RefBranch.Read, children[1].Branch);
        Assert.Equal(1, children[1].Count);
        Assert.All(children, c => Assert.Equal(RefDirection.Up, c.Direction));
    }

    [Fact]
    public void FlagSiteExpandsToOwnerCallers()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "common/on_action/x.txt", "yearly_global_pulse = {\n\tevents = { a.1 }\n}");
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt", "namespace = a\na.1 = {\n\timmediate = {\n\t\tset_variable = my_var\n\t}\n}");
        var model = Build(roots);
        var root = Assert.Single(model.Roots("my_var"));
        var site = SiteNamed(model, BranchOf(model, root, RefBranch.Set), "a.1");
        Assert.Equal(CheckKind.Event, site.CheckKind);
        Assert.Equal(FlagRole.Set, site.Role);
        Assert.True(site.Expandable);
        var caller = SiteNamed(model, site, "yearly_global_pulse");
        Assert.Equal(CheckKind.OnAction, caller.CheckKind);
        Assert.Equal(RefDirection.Up, caller.Direction);
    }

    [Fact]
    public void BlockRootUsesIncludesFlags()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tadd_gold = 1\n}");
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt",
            "namespace = a\na.1 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t\tset_variable = my_var\n\t}\n}");
        var model = Build(roots);
        var root = Assert.Single(model.Roots("a.1"));
        Assert.Equal(CheckKind.Event, root.CheckKind);
        Assert.Null(root.FlagKind);
        var uses = BranchOf(model, root, RefBranch.Uses);
        Assert.Equal(RefDirection.Down, uses.Direction);
        Assert.Equal(2, uses.Count);
        var children = model.Children(uses);
        Assert.Equal("my_fx", children[0].Name);
        Assert.Equal(RefNodeKind.Site, children[0].NodeKind);
        var sets = children.Single(n => n.Branch == RefBranch.Sets);
        Assert.Equal(1, sets.Count);
        var flag = Assert.Single(model.Children(sets));
        Assert.Equal("my_var", flag.Name);
        Assert.Equal(FlagRole.Set, flag.Role);
        Assert.False(flag.Expandable);
        Assert.Empty(model.Children(flag));
    }

    [Fact]
    public void UsesSkipsBuiltIns()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tadd_gold = 1\n}");
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt",
            "namespace = a\na.1 = {\n\timmediate = {\n\t\tif = {\n\t\t\tlimit = { is_ai = yes }\n\t\t\tmy_fx = yes\n\t\t}\n\t}\n}");
        var model = Build(roots);
        var root = Assert.Single(model.Roots("a.1"));
        var uses = BranchOf(model, root, RefBranch.Uses);
        Assert.Equal(1, uses.Count);
        var site = Assert.Single(model.Children(uses));
        Assert.Equal("my_fx", site.Name);
    }

    [Fact]
    public void DownChainContinues()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tadd_gold = 1\n}");
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt",
            "namespace = a\na.1 = {\n\timmediate = {\n\t\ttrigger_event = a.2\n\t}\n}\na.2 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}");
        var model = Build(roots);
        var root = Assert.Single(model.Roots("a.1"));
        var a2 = SiteNamed(model, BranchOf(model, root, RefBranch.Uses), "a.2");
        Assert.True(a2.Expandable);
        Assert.Equal(RefDirection.Down, a2.Direction);
        var fx = SiteNamed(model, a2, "my_fx");
        Assert.Equal(RefDirection.Down, fx.Direction);
    }

    [Fact]
    public void GroupsByOrigin()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tadd_gold = 1\n}");
        ReferenceTestRoots.Write(roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}");
        ReferenceTestRoots.Write(roots.Other, "events/o.txt",
            "namespace = o\no.1 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}\no.2 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}");
        ReferenceTestRoots.Write(roots.Vanilla, "events/v.txt",
            "namespace = v\nv.1 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}\nv.2 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}\nv.3 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}");
        var model = Build(roots);
        var root = Assert.Single(model.Roots("my_fx"));
        var usedBy = BranchOf(model, root, RefBranch.UsedBy);
        Assert.Equal(6, usedBy.Count);
        var children = model.Children(usedBy);
        Assert.Equal(3, children.Count);
        Assert.Equal(RefNodeKind.Site, children[0].NodeKind);
        Assert.Equal("m.1", children[0].Name);
        Assert.Equal(FlagOrigin.ActiveMod, children[0].Origin);
        Assert.Equal(RefGroup.OtherMods, children[1].Group);
        Assert.Equal(2, children[1].Count);
        Assert.Equal(RefGroup.Vanilla, children[2].Group);
        Assert.Equal(3, children[2].Count);
        var vanilla = model.Children(children[2]);
        Assert.Equal(new[] { "v.1", "v.2", "v.3" }, vanilla.Select(n => n.Name));
        Assert.All(vanilla, n => Assert.Equal(FlagOrigin.Vanilla, n.Origin));
        var other = model.Children(children[1]);
        Assert.Equal(new[] { "o.1", "o.2" }, other.Select(n => n.Name));
        Assert.All(other, n => Assert.Equal("Other Mod", n.OriginName));
    }

    [Fact]
    public void EmptyGroupsOmitted()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tadd_gold = 1\n}");
        ReferenceTestRoots.Write(roots.Mod, "events/m.txt",
            "namespace = m\nm.2 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}\nm.1 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}");
        var model = Build(roots);
        var root = Assert.Single(model.Roots("my_fx"));
        var children = model.Children(BranchOf(model, root, RefBranch.UsedBy));
        Assert.Equal(2, children.Count);
        Assert.All(children, c => Assert.Equal(RefNodeKind.Site, c.NodeKind));
        // Sortiert nach Pfad, dann Zeile
        Assert.Equal(new[] { "m.2", "m.1" }, children.Select(c => c.Name));
    }

    [Fact]
    public void VanillaEffectIsPartial()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Vanilla, "common/scripted_effects/v.txt", "vanilla_fx = {\n\tadd_gold = 1\n}");
        ReferenceTestRoots.Write(roots.Vanilla, "events/v.txt", "namespace = v\nv.1 = {\n\timmediate = {\n\t\ttrigger_event = a.1\n\t}\n}");
        ReferenceTestRoots.Write(roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tadd_gold = 1\n}");
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt",
            "namespace = a\na.1 = {\n\timmediate = {\n\t\tvanilla_fx = yes\n\t\tmy_fx = yes\n\t}\n}");
        var model = Build(roots);
        var root = Assert.Single(model.Roots("a.1"));
        Assert.False(root.Partial);
        var uses = BranchOf(model, root, RefBranch.Uses);
        Assert.True(SiteNamed(model, uses, "vanilla_fx").Partial);
        Assert.False(SiteNamed(model, uses, "my_fx").Partial);
        var usedBy = BranchOf(model, root, RefBranch.UsedBy);
        var vanillaGroup = Assert.Single(model.Children(usedBy));
        Assert.Equal(RefGroup.Vanilla, vanillaGroup.Group);
        var v1 = Assert.Single(model.Children(vanillaGroup));
        Assert.Equal("v.1", v1.Name);
        Assert.Equal(CheckKind.Event, v1.CheckKind);
        Assert.False(v1.Partial);
        Assert.True(v1.Expandable);
    }

    [Fact]
    public void VanillaEffectRootIsPartial()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Vanilla, "common/scripted_effects/v.txt", "vanilla_fx = {\n\tadd_gold = 1\n}");
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt", "namespace = a\na.1 = {\n\timmediate = {\n\t\tvanilla_fx = yes\n\t}\n}");
        var model = Build(roots);
        var root = Assert.Single(model.Roots("vanilla_fx"));
        Assert.Equal(CheckKind.Effect, root.CheckKind);
        Assert.True(root.Partial);
        Assert.Equal(FlagOrigin.Vanilla, root.Origin);
    }

    [Fact]
    public void CycleMarked()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tmy_fx2 = yes\n}\nmy_fx2 = {\n\tmy_fx = yes\n}");
        var model = Build(roots);
        var root = Assert.Single(model.Roots("my_fx"));
        var fx2 = SiteNamed(model, BranchOf(model, root, RefBranch.Uses), "my_fx2");
        Assert.False(fx2.Cycle);
        Assert.True(fx2.Expandable);
        var back = SiteNamed(model, fx2, "my_fx");
        Assert.True(back.Cycle);
        Assert.False(back.Expandable);
        Assert.Empty(model.Children(back));
    }

    [Fact]
    public void CycleEventOtherSpelling()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt",
            "namespace = a\na.1 = {\n\timmediate = {\n\t\ttrigger_event = a.0002\n\t}\n}\na.2 = {\n\timmediate = {\n\t\ttrigger_event = a.1\n\t}\n}");
        var model = Build(roots);
        var root = Assert.Single(model.Roots("a.1"));
        var a2 = SiteNamed(model, BranchOf(model, root, RefBranch.Uses), "a.0002");
        Assert.False(a2.Cycle);
        var back = SiteNamed(model, a2, "a.1");
        Assert.True(back.Cycle);
        Assert.False(back.Expandable);
    }

    [Fact]
    public void NameInBothIndexes()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "common/scripted_effects/fx.txt", "shared = {\n\tadd_gold = 1\n}");
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt", "namespace = a\na.1 = {\n\timmediate = {\n\t\tset_global_variable = shared\n\t}\n}");
        var model = Build(roots);
        var found = model.Roots("shared");
        Assert.Equal(2, found.Count);
        Assert.Equal(FlagKind.GlobalVariable, found[0].FlagKind);
        Assert.Null(found[0].CheckKind);
        Assert.Equal(CheckKind.Effect, found[1].CheckKind);
        Assert.Null(found[1].FlagKind);
    }

    [Fact]
    public void NotFound()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt", "namespace = a\na.1 = {\n\timmediate = {\n\t\tset_variable = my_var\n\t}\n}");
        var model = Build(roots);
        Assert.Empty(model.Roots("nothing_here"));
        Assert.Empty(model.Roots("my_*"));
    }

    [Fact]
    public void OnlyFlagIndex()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt", "namespace = a\na.1 = {\n\timmediate = {\n\t\tset_variable = my_var\n\t}\n}");
        var model = new ReferenceTreeModel(new ReferenceSources(roots.BuildFlags(), null));
        var root = Assert.Single(model.Roots("my_var"));
        Assert.NotNull(root.FlagKind);
        var site = Assert.Single(model.Children(BranchOf(model, root, RefBranch.Set)));
        Assert.Equal(RefNodeKind.Site, site.NodeKind);
        Assert.False(site.Expandable);
        Assert.Equal(FlagOrigin.ActiveMod, site.Origin);
        Assert.Empty(model.Children(site));
    }

    [Fact]
    public void Hoi4FlagIgnoresCase()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tset_country_flag = MyFlag\n}");
        var model = Build(roots, "hoi4");
        var root = Assert.Single(model.Roots("myflag"));
        Assert.Equal(FlagKind.ScopeFlag, root.FlagKind);
        var site = Assert.Single(model.Children(BranchOf(model, root, RefBranch.Set)));
        Assert.Equal("my_fx", site.Name);
    }

    [Fact]
    public void Eu4EventBlockOwner()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "events/x.txt", "namespace = x\ncountry_event = {\n\tid = x.1\n\toption = {\n\t\tset_country_flag = f\n\t}\n}");
        var model = Build(roots, "eu4");
        var root = Assert.Single(model.Roots("f"));
        var site = Assert.Single(model.Children(BranchOf(model, root, RefBranch.Set)));
        Assert.Equal("x.1", site.Name);
        Assert.Equal(CheckKind.Event, site.CheckKind);
    }
}
