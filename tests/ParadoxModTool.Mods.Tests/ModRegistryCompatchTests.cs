using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModRegistryCompatchTests : TempDirTests
{
    private ModRegistry NewRegistry() => ModRegistry.Load(Make("game"));

    private ModInfo AddMod(ModRegistry registry, string name) => registry.Add(name, Make(name));

    [Fact]
    public void SetCompatch_SpeichertZieleAllerDreiArten()
    {
        var registry = NewRegistry();
        var a = AddMod(registry, "A");
        var patch = AddMod(registry, "Patch");
        var folder = Make("Fremd");

        registry.SetCompatch(patch.Id, new[]
        {
            new CompatchTarget { ModId = a.Id, DisplayName = "A" },
            new CompatchTarget { WorkshopId = " 3599735023 ", DisplayName = "Idea Variation 2" },
            new CompatchTarget { Folder = folder + "\\", DisplayName = "Fremd" },
        });

        var back = ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!).Get(patch.Id)!;
        Assert.True(back.IsCompatch);
        Assert.Equal(3, back.CompatchTargets.Count);
        Assert.Equal(a.Id, back.CompatchTargets[0].ModId);
        Assert.Equal("3599735023", back.CompatchTargets[1].WorkshopId);
        Assert.Equal(ModPaths.Normalize(folder), back.CompatchTargets[2].Folder);
        Assert.Equal(ModRole.Main, back.Role);
    }

    [Fact]
    public void ClearCompatch_MachtDieModWiederNormal()
    {
        var registry = NewRegistry();
        var a = AddMod(registry, "A");
        var patch = AddMod(registry, "Patch");
        registry.SetCompatch(patch.Id, new[] { new CompatchTarget { ModId = a.Id, DisplayName = "A" } });

        registry.ClearCompatch(patch.Id);

        var back = ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!).Get(patch.Id)!;
        Assert.False(back.IsCompatch);
        Assert.Empty(back.CompatchTargets);
    }

    [Theory]
    [InlineData("leer")]
    [InlineData("zwei")]
    [InlineData("keine")]
    [InlineData("selbst")]
    [InlineData("workshop")]
    [InlineData("doppelt")]
    public void SetCompatch_LehntUngueltigeZieleAb(string fall)
    {
        var registry = NewRegistry();
        var a = AddMod(registry, "A");
        var patch = AddMod(registry, "Patch");
        CompatchTarget[] targets = fall switch
        {
            "leer" => Array.Empty<CompatchTarget>(),
            "zwei" => new[] { new CompatchTarget { ModId = a.Id, WorkshopId = "1", DisplayName = "x" } },
            "keine" => new[] { new CompatchTarget { DisplayName = "x" } },
            "selbst" => new[] { new CompatchTarget { ModId = patch.Id, DisplayName = "x" } },
            "workshop" => new[] { new CompatchTarget { WorkshopId = "12a", DisplayName = "x" } },
            _ => new[] { new CompatchTarget { ModId = a.Id, DisplayName = "A" }, new CompatchTarget { ModId = a.Id, DisplayName = "A" } },
        };

        Assert.Throws<InvalidOperationException>(() => registry.SetCompatch(patch.Id, targets));
        Assert.False(registry.Get(patch.Id)!.IsCompatch);
    }

    [Fact]
    public void Kompatch_UndProjekt_SchliessenSichAus()
    {
        var registry = NewRegistry();
        var main = AddMod(registry, "Haupt");
        var beta = AddMod(registry, "Beta");
        var patch = AddMod(registry, "Patch");
        registry.SetProject(beta.Id, main.Id, ModRole.DevBranch);
        registry.SetCompatch(patch.Id, new[] { new CompatchTarget { ModId = main.Id, DisplayName = "Haupt" } });

        Assert.Throws<InvalidOperationException>(() => registry.SetCompatch(beta.Id, new[] { new CompatchTarget { ModId = patch.Id, DisplayName = "P" } }));
        Assert.Throws<InvalidOperationException>(() => registry.SetCompatch(main.Id, new[] { new CompatchTarget { ModId = patch.Id, DisplayName = "P" } }));
        Assert.Throws<InvalidOperationException>(() => registry.SetProject(patch.Id, main.Id, ModRole.Submod));
        Assert.Throws<InvalidOperationException>(() => registry.SetProjectMembers(main.Id, new[] { (beta.Id, ModRole.DevBranch), (patch.Id, ModRole.Submod) }));
    }

    [Fact]
    public void Laden_RepariertKaputteZiellisten()
    {
        var registry = NewRegistry();
        var patch = AddMod(registry, "Patch");
        var json = File.ReadAllText(registry.FilePath).Replace("\"isCompatch\": false", "\"isCompatch\": true").Replace("\"compatchTargets\": []", "\"compatchTargets\": null");
        File.WriteAllText(registry.FilePath, json);

        var back = ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!).Get(patch.Id)!;

        Assert.NotNull(back.CompatchTargets);
        Assert.True(back.IsCompatch);
    }
}
