using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class UpdateStateStoreTests : IDisposable
{
    private readonly string _game = Directory.CreateTempSubdirectory("pmt-update-state-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_game, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Update_directory_lies_below_the_mod_directory() =>
        Assert.Equal(Path.Combine(CompatchStateStore.DirectoryFor(_game, "mine"), "update"), CompatchStateStore.DirectoryForUpdate(_game, "mine"));

    [Fact]
    public void Saved_lists_compatch_and_update_states_of_the_same_mod()
    {
        new CompatchStateStore(CompatchStateStore.DirectoryFor(_game, "mine")).Save(new CompatchState { Target = PatchTarget.Compatch });
        new CompatchStateStore(CompatchStateStore.DirectoryForUpdate(_game, "mine")).Save(new CompatchState { Target = PatchTarget.UpdateMod, UpdateNewVersion = "1.38" });

        var saved = CompatchStateStore.Saved(_game);

        Assert.Equal(2, saved.Count);
        Assert.Contains(saved, s => s.State.Target == PatchTarget.UpdateMod && s.Store.Directory == CompatchStateStore.DirectoryForUpdate(_game, "mine"));
    }

    [Theory]
    [InlineData("1.38", "1.39", true)]
    [InlineData("1.38", "v1.38", false)]
    [InlineData("1.38", null, false)]
    public void Update_state_is_outdated_after_another_game_update(string stored, string? installed, bool outdated) =>
        Assert.Equal(outdated, CompatchStateStore.IsOutdatedUpdate(new CompatchState { Target = PatchTarget.UpdateMod, UpdateNewVersion = stored }, installed));

    [Fact]
    public void Compatch_state_is_never_an_outdated_update() =>
        Assert.False(CompatchStateStore.IsOutdatedUpdate(new CompatchState { Target = PatchTarget.Compatch }, "1.39"));
}
