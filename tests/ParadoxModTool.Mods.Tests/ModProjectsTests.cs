using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModProjectsTests : TempDirTests
{
    private readonly ModRegistry _registry;
    private readonly ModInfo _main;
    private readonly ModInfo _beta;
    private readonly ModInfo _test;
    private readonly ModInfo _sub;
    private readonly ModInfo _other;

    public ModProjectsTests()
    {
        _registry = ModRegistry.Load(Make("game"));
        _main = _registry.Add("Haupt", Make("haupt"));
        _beta = _registry.Add("Beta", Make("beta"));
        _test = _registry.Add("Alpha", Make("alpha"));
        _sub = _registry.Add("Zusatz", Make("zusatz"));
        _other = _registry.Add("Fremd", Make("fremd"));
        _registry.SetProject(_beta.Id, _main.Id, ModRole.DevBranch);
        _registry.SetProject(_test.Id, _main.Id, ModRole.DevBranch);
        _registry.SetProject(_sub.Id, _main.Id, ModRole.Submod);
    }

    [Fact]
    public void Root_LiefertDenHauptmod()
    {
        Assert.Same(_main, ModProjects.Root(_registry, _main));
        Assert.Same(_main, ModProjects.Root(_registry, _beta));
        Assert.Same(_other, ModProjects.Root(_registry, _other));
    }

    [Fact]
    public void Members_HauptmodZuerstDannKinderNachName()
    {
        var names = ModProjects.Members(_registry, _beta).Select(m => m.Name).ToList();

        Assert.Equal(new[] { "Haupt", "Alpha", "Beta", "Zusatz" }, names);
    }

    [Fact]
    public void Members_ModOhneProjekt_LiefertNurSichSelbst()
    {
        Assert.Equal(new[] { _other }, ModProjects.Members(_registry, _other));
    }

    [Fact]
    public void IsProject_VerlangtMindestensZweiMitglieder()
    {
        Assert.True(ModProjects.IsProject(_registry, _main));
        Assert.True(ModProjects.IsProject(_registry, _sub));
        Assert.False(ModProjects.IsProject(_registry, _other));
    }

    [Fact]
    public void Relation_UnterschiedlicheProjekte_IstUnrelated()
    {
        Assert.Equal(ModRelationKind.Unrelated, ModProjects.Relation(_registry, _main, _other));
        Assert.Equal(ModRelationKind.Unrelated, ModProjects.Relation(_registry, _other, _beta));
    }

    [Fact]
    public void Relation_HauptmodUndDevBranchSowieZweiDevBranches_IstAlternative()
    {
        Assert.Equal(ModRelationKind.Alternative, ModProjects.Relation(_registry, _main, _beta));
        Assert.Equal(ModRelationKind.Alternative, ModProjects.Relation(_registry, _beta, _main));
        Assert.Equal(ModRelationKind.Alternative, ModProjects.Relation(_registry, _beta, _test));
    }

    [Fact]
    public void Relation_MitSubmod_IstAddon()
    {
        Assert.Equal(ModRelationKind.Addon, ModProjects.Relation(_registry, _main, _sub));
        Assert.Equal(ModRelationKind.Addon, ModProjects.Relation(_registry, _sub, _beta));
    }

    [Fact]
    public void Relation_DieselbeMod_WirftArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ModProjects.Relation(_registry, _main, _main));
    }

    [Fact]
    public void RoleText_BeschreibtDieRolleOderIstNullOhneProjekt()
    {
        Assert.Equal("Hauptmod (Projekt, 4 Mods)", ModProjects.RoleText(_registry, _main));
        Assert.Equal("Dev-Branch von Haupt", ModProjects.RoleText(_registry, _beta));
        Assert.Equal("Submod von Haupt", ModProjects.RoleText(_registry, _sub));
        Assert.Null(ModProjects.RoleText(_registry, _other));
    }
}
