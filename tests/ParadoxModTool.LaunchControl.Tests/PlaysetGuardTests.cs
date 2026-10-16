using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class PlaysetGuardTests
{
    private static PlaysetData Playset(string name, params (string Path, bool Enabled)[] mods) => new()
    {
        Id = name,
        Name = name,
        Mods = mods.Select(m => new PlaysetModEntry(m.Path, null, m.Enabled, null)).ToList(),
    };

    [Fact]
    public void Clone_is_independent_and_keeps_all_fields()
    {
        var original = Playset("A", ("C:/x", true));
        original.IsActive = true;
        original.IsCloud = true;
        original.IsAutomaticallySorted = true;
        original.Dlcs.Add(new PlaysetDlcEntry("d", false));

        var clone = original.Clone();
        clone.Mods.Clear();
        clone.Dlcs.Clear();

        Assert.True(clone.IsActive && clone.IsCloud && clone.IsAutomaticallySorted);
        Assert.Single(original.Mods);
        Assert.Single(original.Dlcs);
    }

    [Fact]
    public void Game_running_blocks_write()
    {
        var guard = new PlaysetGuard(() => true, () => false);

        Assert.Equal(PlaysetStatus.GameRunning, guard.CheckWritable(usesLauncherDatabase: false).Status);
    }

    [Fact]
    public void Launcher_running_blocks_only_database_games()
    {
        var guard = new PlaysetGuard(() => false, () => true);

        Assert.Equal(PlaysetStatus.LauncherRunning, guard.CheckWritable(usesLauncherDatabase: true).Status);
        Assert.True(guard.CheckWritable(usesLauncherDatabase: false).Success);
    }

    [Fact]
    public void Validate_refuses_duplicate_name_case_insensitive()
    {
        var result = PlaysetGuard.Validate(Playset("Test"), new[] { Playset("test") }, _ => true);

        Assert.Equal(PlaysetStatus.DuplicateName, result.Status);
    }

    [Fact]
    public void Validate_ignores_the_playset_itself_in_others()
    {
        var same = Playset("Test");

        Assert.True(PlaysetGuard.Validate(same, new[] { same }, _ => true).Success);
    }

    [Fact]
    public void Validate_refuses_empty_name()
    {
        Assert.Equal(PlaysetStatus.InvalidName, PlaysetGuard.Validate(Playset("  "), Array.Empty<PlaysetData>(), _ => true).Status);
    }

    [Fact]
    public void Validate_refuses_missing_enabled_mod_folder_and_names_it()
    {
        var result = PlaysetGuard.Validate(Playset("A", ("C:/mods/gone", true), ("C:/mods/ok", true)), Array.Empty<PlaysetData>(), p => p.EndsWith("ok"));

        Assert.Equal(PlaysetStatus.ModMissing, result.Status);
        Assert.Equal("C:/mods/gone", result.Detail);
    }

    [Fact]
    public void Validate_names_an_enabled_mod_without_folder_by_its_display_name()
    {
        var playset = new PlaysetData { Id = "A", Name = "A", Mods = { new PlaysetModEntry(string.Empty, "Unsubscribed Mod", true, "m3") } };

        var result = PlaysetGuard.Validate(playset, Array.Empty<PlaysetData>(), _ => false);

        Assert.Equal(PlaysetStatus.ModMissing, result.Status);
        Assert.Equal("Unsubscribed Mod", result.Detail);
    }

    [Fact]
    public void Validate_accepts_missing_folder_of_disabled_mod()
    {
        var result = PlaysetGuard.Validate(Playset("A", ("C:/mods/gone", false)), Array.Empty<PlaysetData>(), _ => false);

        Assert.True(result.Success);
    }

    [Fact]
    public void Validate_refuses_same_mod_twice()
    {
        var result = PlaysetGuard.Validate(Playset("A", ("C:/mods/x/", true), (@"c:\MODS\x", false)), Array.Empty<PlaysetData>(), _ => true);

        Assert.Equal(PlaysetStatus.DuplicateMod, result.Status);
    }

    [Fact]
    public void Validate_allows_several_disabled_mods_without_folder()
    {
        var result = PlaysetGuard.Validate(Playset("A", ("", false), ("", false)), Array.Empty<PlaysetData>(), _ => false);

        Assert.True(result.Success);
    }

    [Fact]
    public void Validate_accepts_name_with_quote_umlaut_and_space_inside()
    {
        Assert.True(PlaysetGuard.Validate(Playset("Mein \"Test\" Ärger 2"), Array.Empty<PlaysetData>(), _ => true).Success);
    }
}
