using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public class ScriptReaderTests
{
    private static FileScan Read(string text, string path = "in_game/common/events/x.txt") =>
        new ScriptReader(FlagCommandTable.Eu5).Read(path, text);

    [Theory]
    [InlineData("common/scripted_effects/x.txt", true)]
    [InlineData("in_game/common/scripted_effects/x.txt", true)]
    [InlineData("main_menu/common/scripted_triggers/x.txt", true)]
    [InlineData("loading_screen/common/scripted_triggers/x.txt", true)]
    [InlineData("backup/common/scripted_effects/x.txt", false)]
    [InlineData("x/in_game/common/scripted_effects/x.txt", false)]
    [InlineData("in_game/backup/common/scripted_effects/x.txt", false)]
    public void DefinitionFolders_AnchoredAtRootOrPhaseFolder(string path, bool isDefinitionFile)
    {
        const string Text = "eff = {\n\tset_variable = v_$P$\n\tcallee = { P = $P$ }\n}";
        var reader = new ScriptReader(FlagCommandTable.Eu5);

        var scan = reader.Read(path, Text);
        var call = Assert.Single(reader.ReadCalls(path, Text, new HashSet<string> { "eff", "callee" }));

        Assert.Equal(isDefinitionFile, scan.Definitions.Count == 1);
        Assert.Equal(isDefinitionFile ? "eff" : null, Assert.Single(scan.Templates).DefinitionName);
        Assert.Equal(isDefinitionFile ? "eff" : null, call.CallerDefinition);
        Assert.Equal(isDefinitionFile ? "callee" : "eff", call.Callee);
    }

    [Theory]
    [InlineData("dlc/dlc001_x/in_game/common/scripted_effects/a.txt", true)]
    [InlineData("integrated_dlc/dlc001_x/in_game/common/scripted_effects/a.txt", true)]
    [InlineData("builtin_dlc/dlc001_x/common/scripted_effects/a.txt", true)]
    [InlineData("dlc/dlc001_x/readme.txt", false)]
    [InlineData("dlc/dlc001_x/backup/in_game/common/scripted_effects/a.txt", false)]
    [InlineData("x/dlc/dlc001_x/in_game/common/scripted_effects/a.txt", false)]
    public void DlcPrefix_SkippedBeforePhaseFolder(string path, bool isEffectFile)
    {
        var scan = new ScriptReader(FlagCommandTable.Eu5).Read(path, "a = { set_variable = v }");

        Assert.Equal(isEffectFile, scan.Definitions.Count == 1);
    }

    [Theory]
    [InlineData("common/inline_scripts/foo.txt", "inline_script:foo")]
    [InlineData("dlc/dlc001_x/in_game/common/inline_scripts/foo.txt", "inline_script:foo")]
    [InlineData("builtin_dlc/dlc001_x/common/inline_scripts/foo.txt", "inline_script:foo")]
    [InlineData("common/inline_scripts/sub/foo.txt", "inline_script:sub/foo")]
    [InlineData("backup/common/inline_scripts/foo.txt", null)]
    public void InlineScriptName_SkipsDlcPrefix(string path, string? expected)
    {
        var scan = new ScriptReader(FlagCommandTable.Stellaris(Array.Empty<string>())).Read(path, "a = b");

        Assert.Equal(expected, scan.Definitions.SingleOrDefault()?.Name);
    }

    [Fact]
    public void SetVariable_BlockForm()
    {
        var scan = Read("set_variable = { name = iv2_x value = 1 }");
        var site = Assert.Single(scan.Sites);
        Assert.Equal(new RawSite("iv2_x", FlagKind.Variable, null, FlagRole.Set, 1), site);
    }

    [Fact]
    public void SetVariable_BareForm()
    {
        var site = Assert.Single(Read("set_variable = iv2_y").Sites);
        Assert.Equal("iv2_y", site.Name);
        Assert.Equal(FlagRole.Set, site.Role);
    }

    [Fact]
    public void HasAndRemove()
    {
        var scan = Read("a = { has_variable = h remove_variable = { name = r } }");
        Assert.Contains(scan.Sites, s => s.Name == "h" && s.Role == FlagRole.Read && s.Kind == FlagKind.Variable);
        Assert.Contains(scan.Sites, s => s.Name == "r" && s.Role == FlagRole.Remove && s.Kind == FlagKind.Variable);
    }

    [Fact]
    public void GlobalAndLocal()
    {
        var scan = Read("set_global_variable = { name = g }\nx = local_var:l");
        Assert.Contains(scan.Sites, s => s.Name == "g" && s.Kind == FlagKind.GlobalVariable && s.Role == FlagRole.Set);
        Assert.Contains(scan.Sites, s => s.Name == "l" && s.Kind == FlagKind.LocalVariable && s.Role == FlagRole.Read);
    }

    [Fact]
    public void ListCommands()
    {
        var scan = Read("add_to_variable_list = { name = L target = flag:F }\nevery_in_list = { variable = L2 }");
        Assert.Contains(scan.Sites, s => s.Name == "L" && s.Kind == FlagKind.VariableList && s.Role == FlagRole.Set);
        Assert.Contains(scan.Sites, s => s.Name == "F" && s.Kind == FlagKind.SymbolValue && s.Role == FlagRole.Read);
        Assert.Contains(scan.Sites, s => s.Name == "L2" && s.Kind == FlagKind.VariableList && s.Role == FlagRole.Read && s.Line == 2);
    }

    [Fact]
    public void PrefixReadInComparisonAndChain()
    {
        var scan = Read("var:a >= var:b\nscope:actor.var:c = 1");
        Assert.Equal(new[] { "a", "b", "c" }, scan.Sites.Select(s => s.Name).OrderBy(n => n));
        Assert.All(scan.Sites, s => Assert.Equal(FlagRole.Read, s.Role));
    }

    [Fact]
    public void PrefixReadInString()
    {
        var site = Assert.Single(Read("\"variable_map(cmm|flag:X_y)\" >= 1").Sites);
        Assert.Equal("X_y", site.Name);
        Assert.Equal(FlagKind.SymbolValue, site.Kind);
        Assert.Equal(FlagRole.Read, site.Role);
    }

    [Fact]
    public void FlagPrefix_IgnoredWithoutSymbolValues()
    {
        var scan = new ScriptReader(FlagCommandTable.For("ck3")!).Read("x.txt", "a = flag:F");
        Assert.Empty(scan.Sites);
    }

    [Fact]
    public void CommentedOut_NoSites()
    {
        Assert.Empty(Read("# set_global_variable = { name = g }").Sites);
    }

    [Fact]
    public void TemplateInEffect()
    {
        var scan = Read(
            "unlock_policy_effect = { set_variable = { name = unlocked_policy_$type$ value = yes } }",
            "in_game/common/scripted_effects/x.txt");
        Assert.Empty(scan.Sites);
        var t = Assert.Single(scan.Templates);
        Assert.Equal("unlocked_policy_$type$", t.NameTemplate);
        Assert.Equal("unlock_policy_effect", t.DefinitionName);
        var d = Assert.Single(scan.Definitions);
        Assert.Equal(new DefinitionInfo("unlock_policy_effect", false, 1), d);
    }

    [Fact]
    public void TemplateOutsideDefinitionFile_HasNoDefinitionName()
    {
        var t = Assert.Single(Read("set_variable = { name = a_$x$ }").Templates);
        Assert.Null(t.DefinitionName);
    }

    [Fact]
    public void TriggerFile_MarksDefinitionAsTrigger()
    {
        var d = Assert.Single(Read("t = { has_variable = v }", "common\\scripted_triggers\\t.txt").Definitions);
        Assert.True(d.IsTrigger);
    }

    [Fact]
    public void UnclosedBlock_NoThrow()
    {
        var site = Assert.Single(Read("set_variable = { name = a").Sites);
        Assert.Equal("a", site.Name);
        Assert.Equal(FlagRole.Set, site.Role);
    }

    [Fact]
    public void LineNumbers()
    {
        var text = string.Join("\r\n", "a = 1", "b = 2", "c = 3", "d = 4", "e = 5", "f = 6", "set_variable = { name = z }");
        Assert.Equal(7, Assert.Single(Read(text).Sites).Line);
    }

    [Fact]
    public void BareFormWithPrefixValue_YieldsOnlyPrefixRead()
    {
        var site = Assert.Single(Read("has_variable = var:x").Sites);
        Assert.Equal(new RawSite("x", FlagKind.Variable, null, FlagRole.Read, 1), site);
    }

    [Fact]
    public void IteratorFields_GlobalAndLocalList()
    {
        var g = Assert.Single(Read("any_in_list = { global_variable = L }").Sites);
        Assert.Equal(new RawSite("L", FlagKind.GlobalVariableList, null, FlagRole.Read, 1), g);
        var l = Assert.Single(Read("any_in_list = { local_variable = L }").Sites);
        Assert.Equal(new RawSite("L", FlagKind.LocalVariableList, null, FlagRole.Read, 1), l);
    }

    [Fact]
    public void CommandCaseInsensitive()
    {
        var site = Assert.Single(Read("has_Variable = a").Sites);
        Assert.Equal(new RawSite("a", FlagKind.Variable, null, FlagRole.Read, 1), site);
    }

    [Fact]
    public void FieldCaseInsensitive()
    {
        var site = Assert.Single(Read("set_variable = { Name = b Value = 1 }").Sites);
        Assert.Equal(new RawSite("b", FlagKind.Variable, null, FlagRole.Set, 1), site);
    }

    [Fact]
    public void GlobalListIterator()
    {
        var reader = new ScriptReader(FlagCommandTable.For("ck3")!);

        var g = Assert.Single(reader.Read("x.txt", "any_in_global_list = { variable = L }").Sites);
        Assert.Equal(new RawSite("L", FlagKind.GlobalVariableList, null, FlagRole.Read, 1), g);
        var m = Assert.Single(reader.Read("x.txt", "every_in_local_list = { variable = M }").Sites);
        Assert.Equal(new RawSite("M", FlagKind.LocalVariableList, null, FlagRole.Read, 1), m);
    }

    [Fact]
    public void Table_FindIteratorAndFlags()
    {
        var table = FlagCommandTable.Eu5;

        Assert.Equal(new IteratorCommand("any_in_global_list", "variable", FlagKind.GlobalVariableList), table.FindIterator("ANY_in_global_list"));
        Assert.Equal(3, table.FindIterators("every_in_list").Count);
        Assert.Null(table.FindIterator("set_variable"));
        Assert.False(table.NamesIgnoreCase);
        Assert.False(FlagCommandTable.For("ck3")!.NamesIgnoreCase);
        Assert.Equal("set_variable", table.Find("SET_VARIABLE")!.Command);
    }

    [Fact]
    public void Table_ExtrasDifferingOnlyInCase_DoNotThrow()
    {
        var table = FlagCommandTable.Ck3(new[] { "set_variable", "SET_VARIABLE", "add_character_flag", "Add_Character_Flag" });

        Assert.NotNull(table.Find("set_Variable"));
        Assert.NotNull(table.Find("ADD_character_flag"));
    }

    [Fact]
    public void IteratorCommandAndFieldCaseInsensitive()
    {
        var site = Assert.Single(Read("Random_In_Global_List = { Variable = L }").Sites);
        Assert.Equal(new RawSite("L", FlagKind.GlobalVariableList, null, FlagRole.Read, 1), site);
    }
}
