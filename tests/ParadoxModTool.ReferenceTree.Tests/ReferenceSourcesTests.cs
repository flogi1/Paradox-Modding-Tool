using ParadoxModTool.FlagTracker;
using ParadoxModTool.ReferenceTree;
using ParadoxModTool.ScriptCheck;
using Xunit;

namespace ParadoxModTool.ReferenceTree.Tests;

public class ReferenceSourcesTests
{
    private static ReferenceSources Build(ReferenceTestRoots roots) => new(roots.BuildFlags(), roots.BuildChecks());

    [Fact]
    public void OwnerOfFlagSiteInEvent()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt", "namespace = a\na.1 = {\n\toption = {\n\t\tset_variable = my_var\n\t}\n}");
        var flags = roots.BuildFlags();
        var site = flags.Search("my_var").SitesByName["my_var"].Single();
        var sources = new ReferenceSources(flags, roots.BuildChecks());
        Assert.Equal(new RefOwner("a.1", CheckKind.Event), sources.OwnerAt(site.Location));
    }

    [Fact]
    public void OwnerOfFlagSiteInScriptedEffect()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tset_global_variable = g\n}");
        var flags = roots.BuildFlags();
        var site = flags.Search("g").SitesByName["g"].Single();
        var sources = new ReferenceSources(flags, roots.BuildChecks());
        Assert.Equal(new RefOwner("my_fx", CheckKind.Effect), sources.OwnerAt(site.Location));
    }

    [Fact]
    public void NoOwnerOutsideDefinitions()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "common/decisions/d.txt", "my_decision = {\n\teffect = {\n\t\tset_variable = dv\n\t}\n}");
        var flags = roots.BuildFlags();
        var site = flags.Search("dv").SitesByName["dv"].Single();
        var sources = new ReferenceSources(flags, roots.BuildChecks());
        Assert.Null(sources.OwnerAt(site.Location));
    }

    [Fact]
    public void FlagSitesInEvent()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt",
            "namespace = a\na.1 = {\n\timmediate = {\n\t\tset_variable = my_var\n\t\tif = {\n\t\t\tlimit = { has_variable = other_var }\n\t\t}\n\t}\n}\na.2 = {\n\timmediate = {\n\t\tset_variable = third\n\t}\n}");
        var sources = Build(roots);
        var sites = sources.FlagSitesIn("a.1");
        Assert.Equal(2, sites.Count);
        Assert.Contains(sites, s => s.Name == "my_var" && s.Role == FlagRole.Set);
        Assert.Contains(sites, s => s.Name == "other_var" && s.Role == FlagRole.Read);
    }

    [Fact]
    public void FlagSitesInEventOtherSpelling()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Mod, "events/a.txt", "namespace = a\na.1 = {\n\timmediate = {\n\t\tset_variable = my_var\n\t}\n}");
        var sources = Build(roots);
        Assert.NotEmpty(sources.FlagSitesIn("a.1"));
        Assert.Equal(sources.FlagSitesIn("a.1"), sources.FlagSitesIn("a.0001"));
    }

    [Fact]
    public void OriginOfVanillaAndOtherMod()
    {
        using var roots = new ReferenceTestRoots();
        ReferenceTestRoots.Write(roots.Vanilla, "common/scripted_effects/v.txt", "v_fx = {\n\tset_variable = vv\n}");
        ReferenceTestRoots.Write(roots.Other, "common/scripted_effects/o.txt", "o_fx = {\n\tset_variable = ov\n}");
        ReferenceTestRoots.Write(roots.Mod, "common/scripted_effects/m.txt", "m_fx = {\n\tset_variable = mv\n}");
        var sources = Build(roots);
        Assert.Equal((FlagOrigin.Vanilla, "Vanilla"), sources.OriginOf(new SiteLocation(roots.Vanilla, "common/scripted_effects/v.txt", 1)));
        Assert.Equal((FlagOrigin.OtherMod, "Other Mod"), sources.OriginOf(new SiteLocation(roots.Other, "common/scripted_effects/o.txt", 1)));
        Assert.Equal(FlagOrigin.ActiveMod, sources.OriginOf(new SiteLocation(roots.Mod, "common/scripted_effects/m.txt", 1)).Origin);
        Assert.True(sources.DefinedInMod("m_fx"));
        Assert.False(sources.DefinedInMod("o_fx"));
    }

    [Fact]
    public void NullIndexes()
    {
        var sources = new ReferenceSources(null, null);
        var location = new SiteLocation("x", "events/a.txt", 1);
        Assert.Null(sources.OwnerAt(location));
        Assert.Empty(sources.FlagSitesIn("a.1"));
        Assert.False(sources.DefinedInMod("a.1"));
        Assert.Equal(FlagOrigin.Vanilla, sources.OriginOf(location).Origin);
    }
}
