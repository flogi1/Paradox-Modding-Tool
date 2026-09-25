using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModGuiSettingsTests : TempDirTests
{
    private ModRegistry NewRegistry() => ModRegistry.Load(Make("game"));

    [Fact]
    public void DefaultsComeFromTheMainModAndCompatchTargets()
    {
        var registry = NewRegistry();
        var main = registry.Add("Main", Make("Main"));
        var sub = registry.Add("Sub", Make("Sub"));
        registry.SetProject(sub.Id, main.Id, ModRole.Submod);
        var target = registry.Add("Target", Make("Target"));
        var patch = registry.Add("Patch", Make("Patch"));
        var foreign = Make("Foreign");
        registry.SetCompatch(patch.Id, new[]
        {
            new CompatchTarget { ModId = target.Id, DisplayName = "Target" },
            new CompatchTarget { Folder = foreign, DisplayName = "Foreign" },
            new CompatchTarget { WorkshopId = "123", DisplayName = "Workshop" },
        });

        Assert.Equal(new[] { main.Paths[0] }, ModGuiDependencies.Defaults(registry, registry.Get(sub.Id)!));
        Assert.Equal(new[] { target.Paths[0], ModPaths.Normalize(foreign) }, ModGuiDependencies.Defaults(registry, registry.Get(patch.Id)!));
        Assert.Empty(ModGuiDependencies.Defaults(registry, main));
    }

    [Fact]
    public void SettingsAreStoredAndNullRestoresTheDefaults()
    {
        var registry = NewRegistry();
        var mod = registry.Add("Mod", Make("Mod"));
        var cmf = Make("CMF");

        registry.SetGuiSettings(mod.Id, " mine.gui ", "types.gui", new[] { cmf + "\\", cmf });

        var back = ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!).Get(mod.Id)!;
        Assert.Equal("mine.gui", back.GuiOverrideFile);
        Assert.Equal("types.gui", back.GuiTypesFile);
        Assert.Equal(new[] { ModPaths.Normalize(cmf) }, ModGuiDependencies.For(registry, back));

        registry.SetGuiSettings(mod.Id, " ", null, null);
        var reset = ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!).Get(mod.Id)!;
        Assert.Null(reset.GuiOverrideFile);
        Assert.Null(reset.GuiTypesFile);
        Assert.Null(reset.GuiDependencies);
    }
}
