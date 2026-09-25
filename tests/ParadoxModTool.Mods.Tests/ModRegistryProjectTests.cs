using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModRegistryProjectTests : TempDirTests
{
    private ModRegistry NewRegistry() => ModRegistry.Load(Make("game"));

    private ModInfo AddMod(ModRegistry registry, string name) => registry.Add(name, Make(name));

    [Fact]
    public void NeueMod_IstHauptmodOhneElternteilUndUngesperrt()
    {
        var registry = NewRegistry();
        var mod = AddMod(registry, "A");

        Assert.Equal(ModRole.Main, mod.Role);
        Assert.Null(mod.ParentModId);
        Assert.False(mod.IsLocked);
    }

    [Fact]
    public void SetProject_DevBranch_WirdGespeichertUndGeladen()
    {
        var registry = NewRegistry();
        var main = AddMod(registry, "Haupt");
        var beta = AddMod(registry, "Beta");

        registry.SetProject(beta.Id, main.Id, ModRole.DevBranch);

        var back = ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!).Get(beta.Id)!;
        Assert.Equal(main.Id, back.ParentModId);
        Assert.Equal(ModRole.DevBranch, back.Role);
        Assert.Contains("\"DevBranch\"", File.ReadAllText(registry.FilePath));
    }

    [Fact]
    public void SetProject_OhneElternteil_MachtDieModWiederZumHauptmod()
    {
        var registry = NewRegistry();
        var main = AddMod(registry, "Haupt");
        var beta = AddMod(registry, "Beta");
        registry.SetProject(beta.Id, main.Id, ModRole.Submod);

        registry.SetProject(beta.Id, null, ModRole.Main);

        Assert.Null(beta.ParentModId);
        Assert.Equal(ModRole.Main, beta.Role);
    }

    [Fact]
    public void SetProject_RolleMainMitElternteil_WirftInvalidOperation()
    {
        var registry = NewRegistry();
        var main = AddMod(registry, "Haupt");
        var beta = AddMod(registry, "Beta");

        Assert.Throws<InvalidOperationException>(() => registry.SetProject(beta.Id, main.Id, ModRole.Main));
        Assert.Null(beta.ParentModId);
    }

    [Fact]
    public void SetProject_EigenerElternteil_WirftInvalidOperation()
    {
        var registry = NewRegistry();
        var mod = AddMod(registry, "A");

        Assert.Throws<InvalidOperationException>(() => registry.SetProject(mod.Id, mod.Id, ModRole.DevBranch));
    }

    [Fact]
    public void SetProject_UnbekannterElternteil_WirftInvalidOperation()
    {
        var registry = NewRegistry();
        var mod = AddMod(registry, "A");

        Assert.Throws<InvalidOperationException>(() => registry.SetProject(mod.Id, "gibtsnicht", ModRole.DevBranch));
    }

    [Fact]
    public void SetProject_ElternteilIstSelbstKind_WirftInvalidOperation()
    {
        var registry = NewRegistry();
        var main = AddMod(registry, "Haupt");
        var beta = AddMod(registry, "Beta");
        var gamma = AddMod(registry, "Gamma");
        registry.SetProject(beta.Id, main.Id, ModRole.DevBranch);

        Assert.Throws<InvalidOperationException>(() => registry.SetProject(gamma.Id, beta.Id, ModRole.DevBranch));
    }

    [Fact]
    public void SetProject_ModMitKindern_KannKeinKindWerden()
    {
        var registry = NewRegistry();
        var main = AddMod(registry, "Haupt");
        var beta = AddMod(registry, "Beta");
        var other = AddMod(registry, "Andere");
        registry.SetProject(beta.Id, main.Id, ModRole.DevBranch);

        Assert.Throws<InvalidOperationException>(() => registry.SetProject(main.Id, other.Id, ModRole.DevBranch));
    }

    [Fact]
    public void Remove_Hauptmod_MachtDieKinderZuHauptmods()
    {
        var registry = NewRegistry();
        var main = AddMod(registry, "Haupt");
        var beta = AddMod(registry, "Beta");
        registry.SetProject(beta.Id, main.Id, ModRole.DevBranch);

        registry.Remove(main.Id);

        Assert.Null(beta.ParentModId);
        Assert.Equal(ModRole.Main, beta.Role);
        Assert.Null(ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!).Get(beta.Id)!.ParentModId);
    }

    [Fact]
    public void SetLocked_WirdGespeichert()
    {
        var registry = NewRegistry();
        var mod = AddMod(registry, "A");

        registry.SetLocked(mod.Id, true);

        Assert.True(ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!).Get(mod.Id)!.IsLocked);
        registry.SetLocked(mod.Id, false);
        Assert.False(ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!).Get(mod.Id)!.IsLocked);
    }

    [Fact]
    public void Laden_AlteDateiOhneNeueFelder_ErgibtHauptmods()
    {
        var game = Make("game");
        File.WriteAllText(Path.Combine(game, ModRegistry.FileName),
            "{\"version\":1,\"mods\":[{\"id\":\"a\",\"name\":\"A\",\"paths\":[]}],\"ignoredPaths\":[]}");

        var mod = ModRegistry.Load(game).Get("a")!;

        Assert.Equal(ModRole.Main, mod.Role);
        Assert.Null(mod.ParentModId);
        Assert.False(mod.IsLocked);
    }

    [Fact]
    public void Laden_WiderspruechlicheEintraege_WerdenRepariert()
    {
        var game = Make("game");
        File.WriteAllText(Path.Combine(game, ModRegistry.FileName), """
            {"version":1,"mods":[
              {"id":"haupt","name":"Haupt","paths":[]},
              {"id":"gut","name":"Gut","paths":[],"parentModId":"haupt","role":"DevBranch"},
              {"id":"waise","name":"Waise","paths":[],"parentModId":"weg","role":"DevBranch"},
              {"id":"selbst","name":"Selbst","paths":[],"parentModId":"selbst","role":"Submod"},
              {"id":"enkel","name":"Enkel","paths":[],"parentModId":"gut","role":"Submod"},
              {"id":"z1","name":"Z1","paths":[],"parentModId":"z2","role":"DevBranch"},
              {"id":"z2","name":"Z2","paths":[],"parentModId":"z1","role":"DevBranch"},
              {"id":"ohne","name":"Ohne","paths":[],"role":"Submod"},
              {"id":"rolle","name":"Rolle","paths":[],"parentModId":"haupt"}
            ],"ignoredPaths":[]}
            """);

        var registry = ModRegistry.Load(game);

        Assert.Equal("haupt", registry.Get("gut")!.ParentModId);
        Assert.Equal(ModRole.DevBranch, registry.Get("gut")!.Role);
        foreach (var id in new[] { "haupt", "waise", "selbst", "enkel", "z1", "z2", "ohne", "rolle" })
        {
            Assert.Null(registry.Get(id)!.ParentModId);
            Assert.Equal(ModRole.Main, registry.Get(id)!.Role);
        }
    }
}
