using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModRegistryChangelogTests : TempDirTests
{
    [Fact]
    public void SetChangelogFile_WirdGespeichertUndGeladen_UndLeerEntfernt()
    {
        var registry = ModRegistry.Load(Dir);
        var mod = registry.Add("A", Make("a"));
        Assert.Null(mod.ChangelogFile);

        registry.SetChangelogFile(mod.Id, "Readme/Changelog.txt");
        Assert.Equal("Readme/Changelog.txt", ModRegistry.Load(Dir).Get(mod.Id)!.ChangelogFile);

        registry.SetChangelogFile(mod.Id, "  ");
        Assert.Null(ModRegistry.Load(Dir).Get(mod.Id)!.ChangelogFile);
    }

    [Fact]
    public void SetChangelogFile_UnbekannteMod_WirftInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() => ModRegistry.Load(Dir).SetChangelogFile("gibtsnicht", "x.txt"));
    }

    [Fact]
    public void AlteMods_json_ohne_Feld_laedt_unveraendert()
    {
        File.WriteAllText(Path.Combine(Dir, ModRegistry.FileName), "{\"version\":1,\"mods\":[{\"id\":\"i\",\"name\":\"N\",\"paths\":[]}],\"ignoredPaths\":[]}");

        var mod = Assert.Single(ModRegistry.Load(Dir).Mods);

        Assert.Equal("N", mod.Name);
        Assert.Null(mod.ChangelogFile);
    }
}
