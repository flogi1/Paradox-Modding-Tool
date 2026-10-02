using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class ScriptContextTests
{
    private static SafetyRules Rules { get; } =
        DesyncProfile.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "desync", "eu5.json")).Safety!;

    private static readonly string[] None = Array.Empty<string>();

    [Theory]
    [InlineData("in_game/common/x.txt", "common/x.txt")]
    [InlineData(@"In_Game\Events\a.txt", "events/a.txt")]
    [InlineData("common/x.txt", "common/x.txt")]
    public void FolderPath_strips_root(string path, string expected)
    {
        Assert.Equal(expected, ScriptContext.FolderPath(Rules, path));
    }

    [Theory]
    [InlineData("in_game/gfx/portraits/a.txt")]
    [InlineData("main_menu/gui/w.gui")]
    public void Local_folders(string path)
    {
        Assert.Equal(SafetyContext.Local, ScriptContext.Classify(Rules, path, None));
    }

    [Theory]
    [InlineData("in_game/events/e.txt")]
    [InlineData("in_game/common/on_action/o.txt")]
    [InlineData("in_game/setup/s.txt")]
    public void Synced_folders(string path)
    {
        Assert.Equal(SafetyContext.Synced, ScriptContext.Classify(Rules, path, None));
    }

    [Theory]
    [InlineData("in_game/map_data/x.txt")]
    [InlineData("readme.txt")]
    public void Unknown_folder(string path)
    {
        Assert.Equal(SafetyContext.Unknown, ScriptContext.Classify(Rules, path, None));
    }

    [Fact]
    public void Local_block_nearest_ancestor()
    {
        const string path = "common/scripted_guis/g.txt";

        Assert.Equal(SafetyContext.Local, ScriptContext.Classify(Rules, path, new[] { "my_gui", "is_shown" }));
        Assert.Equal(SafetyContext.Synced, ScriptContext.Classify(Rules, path, new[] { "my_gui", "effect" }));
        Assert.Equal(SafetyContext.Local, ScriptContext.Classify(Rules, path, new[] { "my_gui", "effect", "is_shown" }));
        Assert.Equal(SafetyContext.Local, ScriptContext.Classify(Rules, path, new[] { "my_gui", "IS_SHOWN", "effect" }));
        Assert.Equal(SafetyContext.Synced, ScriptContext.Classify(Rules, path, None));
    }

    [Fact]
    public void Local_block_folder_matches_whole_segments()
    {
        Assert.Equal(SafetyContext.Synced, ScriptContext.Classify(Rules, "common/scripted_guis_extra/g.txt", new[] { "is_shown" }));
    }

    [Fact]
    public void Local_block_star()
    {
        Assert.Equal(SafetyContext.Local, ScriptContext.Classify(Rules, "in_game/common/customizable_localization/c.txt", None));
        Assert.Equal(SafetyContext.Local, ScriptContext.Classify(Rules, "in_game/common/customizable_localization/c.txt", new[] { "a", "b" }));
    }

    [Theory]
    [InlineData("GUI.CreateWidget", "console_ui")]
    [InlineData("gui.clearwidgets", "console_ui")]
    [InlineData("debug_mode", "console_ui")]
    [InlineData("particleeditor", "console_ui")]
    [InlineData("gui_editor", "console_ui")]
    [InlineData("3dstats", "console_ui")]
    [InlineData("add_gold", "console_other")]
    [InlineData("debug_modex", "console_other")]
    [InlineData("guiCreate", "console_other")]
    public void ConsoleRule_matching(string command, string expectedId)
    {
        Assert.Equal(expectedId, Rules.ConsoleRule(command)?.Id);
    }

    [Fact]
    public void DynamicRule_is_the_dynamic_rule()
    {
        Assert.Equal("console_dynamic", Rules.DynamicRule?.Id);
    }

    [Fact]
    public void ScriptKeyRule_matching()
    {
        Assert.Equal("local_dlc", Rules.ScriptKeyRule("HAS_LOCAL_DLC")?.Id);
        Assert.Null(Rules.ScriptKeyRule("has_dlc"));
    }
}
