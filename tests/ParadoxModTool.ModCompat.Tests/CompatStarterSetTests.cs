using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class CompatStarterSetTests : TempDirTests
{
    [Fact]
    public void SeedIfFirstUse_creates_starter_set_when_sets_folder_is_missing()
    {
        var setsDir = Path.Combine(Dir, "compat", "sets");
        var store = new CompatSetStore(setsDir);

        var seeded = CompatStarterSet.SeedIfFirstUse(setsDir, store);

        Assert.True(seeded);
        Assert.Equal(new[] { CompatStarterSet.Name }, store.List());
        var loaded = store.Load(CompatStarterSet.Name)!;
        Assert.Equal(CompatStarterSet.Description, loaded.Description);
        Assert.Empty(loaded.Members);
    }

    [Fact]
    public void SeedIfFirstUse_does_nothing_when_sets_folder_already_exists()
    {
        var setsDir = Make("compat", "sets");
        var store = new CompatSetStore(setsDir);

        var seeded = CompatStarterSet.SeedIfFirstUse(setsDir, store);

        Assert.False(seeded);
        Assert.Empty(store.List());
    }
}
