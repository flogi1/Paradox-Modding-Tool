using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModRegistryMembersTests : TempDirTests
{
    private ModRegistry NewRegistry() => ModRegistry.Load(Make("game"));

    private ModInfo AddMod(ModRegistry registry, string name) => registry.Add(name, Make(name));

    [Fact]
    public void SetProjectMembers_OrdnetZuUndSpeichertEinmal()
    {
        var registry = NewRegistry();
        var main = AddMod(registry, "Haupt");
        var beta = AddMod(registry, "Beta");
        var sub = AddMod(registry, "Zusatz");

        registry.SetProjectMembers(main.Id, new[] { (beta.Id, ModRole.DevBranch), (sub.Id, ModRole.Submod) });

        var back = ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!);
        Assert.Equal(main.Id, back.Get(beta.Id)!.ParentModId);
        Assert.Equal(ModRole.DevBranch, back.Get(beta.Id)!.Role);
        Assert.Equal(ModRole.Submod, back.Get(sub.Id)!.Role);
    }

    [Fact]
    public void SetProjectMembers_FehlendeBisherigeKinder_WerdenEigenstaendig()
    {
        var registry = NewRegistry();
        var main = AddMod(registry, "Haupt");
        var beta = AddMod(registry, "Beta");
        var sub = AddMod(registry, "Zusatz");
        registry.SetProjectMembers(main.Id, new[] { (beta.Id, ModRole.DevBranch), (sub.Id, ModRole.Submod) });

        registry.SetProjectMembers(main.Id, new[] { (sub.Id, ModRole.DevBranch) });

        Assert.Null(beta.ParentModId);
        Assert.Equal(ModRole.Main, beta.Role);
        Assert.Equal(ModRole.DevBranch, sub.Role);
    }

    [Fact]
    public void SetProjectMembers_LeereListe_LoestAlleKinder()
    {
        var registry = NewRegistry();
        var main = AddMod(registry, "Haupt");
        var beta = AddMod(registry, "Beta");
        registry.SetProjectMembers(main.Id, new[] { (beta.Id, ModRole.DevBranch) });

        registry.SetProjectMembers(main.Id, Array.Empty<(string, ModRole)>());

        Assert.Null(beta.ParentModId);
    }

    [Fact]
    public void SetProjectMembers_FehlerAendertNichts()
    {
        var registry = NewRegistry();
        var main = AddMod(registry, "Haupt");
        var other = AddMod(registry, "Andere");
        var beta = AddMod(registry, "Beta");
        var gamma = AddMod(registry, "Gamma");
        registry.SetProjectMembers(other.Id, new[] { (gamma.Id, ModRole.DevBranch) });

        Assert.Throws<InvalidOperationException>(() =>
            registry.SetProjectMembers(main.Id, new[] { (beta.Id, ModRole.DevBranch), (gamma.Id, ModRole.DevBranch) }));

        Assert.Null(beta.ParentModId);
        Assert.Equal(other.Id, gamma.ParentModId);
    }

    [Fact]
    public void SetProjectMembers_UngueltigeFaelle_WerfenInvalidOperation()
    {
        var registry = NewRegistry();
        var main = AddMod(registry, "Haupt");
        var beta = AddMod(registry, "Beta");
        var child = AddMod(registry, "Kind");
        var parentOfChild = AddMod(registry, "Eltern");
        registry.SetProjectMembers(parentOfChild.Id, new[] { (child.Id, ModRole.DevBranch) });

        Assert.Throws<InvalidOperationException>(() => registry.SetProjectMembers(main.Id, new[] { (beta.Id, ModRole.Main) }));
        Assert.Throws<InvalidOperationException>(() => registry.SetProjectMembers(main.Id, new[] { (main.Id, ModRole.DevBranch) }));
        Assert.Throws<InvalidOperationException>(() => registry.SetProjectMembers(main.Id, new[] { ("gibtsnicht", ModRole.DevBranch) }));
        Assert.Throws<InvalidOperationException>(() => registry.SetProjectMembers(main.Id, new[] { (beta.Id, ModRole.DevBranch), (beta.Id, ModRole.Submod) }));
        Assert.Throws<InvalidOperationException>(() => registry.SetProjectMembers(main.Id, new[] { (parentOfChild.Id, ModRole.DevBranch) }));
        Assert.Throws<InvalidOperationException>(() => registry.SetProjectMembers(child.Id, new[] { (beta.Id, ModRole.DevBranch) }));
        Assert.Null(beta.ParentModId);
    }
}
