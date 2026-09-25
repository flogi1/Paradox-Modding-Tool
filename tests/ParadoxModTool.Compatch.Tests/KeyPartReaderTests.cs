using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class KeyPartReaderTests : CompatchTestBase
{
    private FileParts Read(string relative, string content, bool euV = true)
    {
        PutA(relative, content);
        var rules = euV ? EuVRules() : EuIVRules();
        return KeyPartReader.Read(MergeSide.A, relative, Path.Combine(ADir, relative), rules.Match(relative), euV ? EuVOps : Array.Empty<string>(), new ParseCache());
    }

    [Fact]
    public void Prefixed_keys_are_split_and_constants_go_to_rest()
    {
        var parts = Read("in_game/common/building_types/a.txt", "@c = 5\nINJECT:farm = { x = 1 }\nmine = { y = 1 }\n");
        Assert.Equal(new[] { "farm", "mine" }, parts.Keys.Select(k => k.Address.Key));
        Assert.Equal(OverrideOperator.Inject, parts.Keys[0].Operator);
        Assert.Equal("in_game/common/building_types", parts.Keys[0].Address.Scope);
        Assert.Equal("@c", Assert.IsType<ScriptStatement>(Assert.Single(parts.Rest)).Key);
    }

    [Fact]
    public void Without_operators_the_prefix_stays_part_of_the_key()
    {
        var parts = Read("common/buildings/a.txt", "INJECT:farm = { x = 1 }\n", euV: false);
        Assert.Equal("INJECT:farm", Assert.Single(parts.Keys).Address.Key);
        Assert.Equal(OverrideOperator.None, parts.Keys[0].Operator);
    }

    [Fact]
    public void Gui_types_and_templates_become_keys_and_windows_go_to_rest()
    {
        var parts = Read("in_game/gui/a.gui", "types T {\n type btn = button { size = { 1 2 } }\n}\ntemplate t { x = 1 }\nwindow = { name = w }\n");
        Assert.Equal(new[] { "type btn", "template t" }, parts.Keys.Select(k => k.Address.Key));
        Assert.Equal("window", Assert.IsType<ScriptStatement>(Assert.Single(parts.Rest)).Key);
        Assert.Equal(2, parts.Keys[0].Entries.Count(e => e is ScriptListItem { Value: ScriptScalar } or ScriptStatement { Value: ScriptScalar }));
    }

    [Fact]
    public void Unparseable_file_reports_error()
    {
        var parts = Read("in_game/common/building_types/a.txt", "farm = { x = 1\n");
        Assert.NotNull(parts.Error);
        Assert.True(parts.IsScript);
    }

    [Fact]
    public void Non_script_file_in_keyed_folder_is_whole_file_only()
    {
        var parts = Read("in_game/common/building_types/readme.md", "hello");
        Assert.False(parts.IsScript);
        Assert.Empty(parts.Keys);
    }

    [Fact]
    public void Eu4_localisation_keys_are_read()
    {
        var parts = Read("localisation/mymod_l_english.yml", "l_english:\n foo:0 \"Foo\"\n", euV: false);
        var key = Assert.Single(parts.Keys);
        Assert.Equal("english/foo", key.Address.Key);
        Assert.Equal("localisation", key.Address.Scope);
    }

    [Fact]
    public void Eu5_localization_replace_folder_is_replace_operator()
    {
        var parts = Read("in_game/localization/english/replace/x_l_english.yml", "l_english:\n foo:0 \"Foo\"\n");
        Assert.Equal(OverrideOperator.Replace, Assert.Single(parts.Keys).Operator);
    }

    [Fact]
    public void Addresses_are_lower_case_and_case_insensitive_paths_match()
    {
        Assert.Equal(EntryAddress.ForFileRest("In_Game/Common/X.txt"), EntryAddress.ForFileRest("in_game/common/x.txt"));
        Assert.NotEqual(EntryAddress.ForKey("a", "Farm"), EntryAddress.ForKey("a", "farm"));
    }
}
