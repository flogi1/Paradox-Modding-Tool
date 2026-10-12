using Xunit;

namespace ParadoxModTool.Mods.Tests;

/// <summary>Feste Wahl der aktiven Mod: gewählte Mod gilt, "Automatisch" nimmt die erkannte, eine verschwundene Wahl fällt auf Automatisch zurück.</summary>
public class ActiveModChoiceTests : TempDirTests
{
    private ModRegistry Registry(out ModInfo idea, out ModInfo probe)
    {
        var registry = ModRegistry.Load(Path.Combine(Dir, "game"));
        idea = registry.Add("Idea Variation", Make("mods", "iv2"));
        probe = registry.Add("Probe", Make("mods", "probe"));
        return registry;
    }

    [Fact]
    public void Chosen_mod_wins_over_the_detected_one()
    {
        var registry = Registry(out var idea, out var probe);

        var result = ActiveModChoice.Resolve(registry, probe.Id, idea);

        Assert.Equal(probe.Id, result.Mod!.Id);
        Assert.False(result.IsAutomatic);
        Assert.False(result.ChoiceLost);
    }

    [Fact]
    public void Automatic_takes_the_detected_mod()
    {
        var registry = Registry(out var idea, out _);

        var result = ActiveModChoice.Resolve(registry, ActiveModChoice.Automatic, idea);

        Assert.Equal(idea.Id, result.Mod!.Id);
        Assert.True(result.IsAutomatic);
        Assert.False(result.ChoiceLost);
        Assert.Null(ActiveModChoice.Resolve(registry, ActiveModChoice.Automatic, null).Mod);
    }

    [Fact]
    public void Removed_mod_falls_back_to_automatic_and_says_so()
    {
        var registry = Registry(out var idea, out var probe);
        registry.Remove(probe.Id);

        var result = ActiveModChoice.Resolve(registry, probe.Id, idea);

        Assert.Equal(idea.Id, result.Mod!.Id);
        Assert.True(result.IsAutomatic);
        Assert.True(result.ChoiceLost);
    }

    [Fact]
    public void Mod_without_existing_folder_falls_back_to_automatic()
    {
        var registry = Registry(out var idea, out var probe);
        Directory.Delete(probe.Paths[0]);

        var result = ActiveModChoice.Resolve(registry, probe.Id, idea);

        Assert.Equal(idea.Id, result.Mod!.Id);
        Assert.True(result.ChoiceLost);
    }

    [Fact]
    public void First_start_fixes_the_detected_mod_or_stays_automatic()
    {
        var registry = Registry(out var idea, out _);

        Assert.Equal(idea.Id, ActiveModChoice.Initial(null, idea));
        Assert.Equal(ActiveModChoice.Automatic, ActiveModChoice.Initial(null, null));
        Assert.Equal("kept", ActiveModChoice.Initial("kept", idea));
    }
}
