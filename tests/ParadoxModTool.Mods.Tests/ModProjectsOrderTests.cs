using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModProjectsOrderTests : TempDirTests
{
    private ModRegistry NewRegistry() => ModRegistry.Load(Make("game"));

    private ModInfo AddMod(ModRegistry registry, string name) => registry.Add(name, Make(name));

    [Fact]
    public void Ordered_HauptmodDannDevBranchesDannSubmods_JeNachNameEingerueckt()
    {
        var registry = NewRegistry();
        var zeta = AddMod(registry, "Zeta");
        var sub = AddMod(registry, "Aaa Zusatz");
        var devB = AddMod(registry, "Beta B");
        var devA = AddMod(registry, "Beta A");
        var alpha = AddMod(registry, "Alpha");
        registry.SetProjectMembers(zeta.Id, new[] { (sub.Id, ModRole.Submod), (devB.Id, ModRole.DevBranch), (devA.Id, ModRole.DevBranch) });

        var ordered = ModProjects.Ordered(registry);

        Assert.Equal(
            new[] { ("Alpha", 0), ("Zeta", 0), ("Beta A", 1), ("Beta B", 1), ("Aaa Zusatz", 1) },
            ordered.Select(o => (o.Mod.Name, o.Depth)).ToArray());
        Assert.Same(alpha, ordered[0].Mod);
    }

    [Fact]
    public void Ordered_LeeresRegister_LiefertNichts()
    {
        Assert.Empty(ModProjects.Ordered(NewRegistry()));
    }

    [Fact]
    public void Ordered_KindMitFehlendemElternteil_ErscheintAlsHauptmod()
    {
        var registry = NewRegistry();
        var orphan = AddMod(registry, "Waise");
        orphan.ParentModId = "gibtsnicht";
        orphan.Role = ModRole.DevBranch;

        var ordered = ModProjects.Ordered(registry);

        Assert.Equal(new[] { ("Waise", 0) }, ordered.Select(o => (o.Mod.Name, o.Depth)).ToArray());
    }
}
