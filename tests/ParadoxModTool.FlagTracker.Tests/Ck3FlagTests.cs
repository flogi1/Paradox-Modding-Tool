using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public class Ck3FlagTests
{
    private static FileScan Read(string text, string path = "common/scripted_effects/x.txt", params string[] extra) =>
        new ScriptReader(FlagCommandTable.Ck3(extra)).Read(path, text);

    [Fact]
    public void CharacterFlag_AllRoles()
    {
        var scan = Read("add_character_flag = f1\nadd_character_flag = { flag = f2 days = 5 }\nhas_character_flag = f1\nremove_character_flag = f1");
        Assert.Equal(4, scan.Sites.Count);
        Assert.All(scan.Sites, s =>
        {
            Assert.Equal(FlagKind.ScopeFlag, s.Kind);
            Assert.Equal("character_flag", s.Family);
        });
        Assert.Equal(new[] { FlagRole.Set, FlagRole.Set, FlagRole.Read, FlagRole.Remove }, scan.Sites.Select(s => s.Role));
        Assert.Equal(new[] { "f1", "f2", "f1", "f1" }, scan.Sites.Select(s => s.Name));
    }

    [Fact]
    public void Fingerprint_StableAndTableSpecific()
    {
        Assert.Equal(FlagCommandTable.Ck3(Array.Empty<string>()).Fingerprint, FlagCommandTable.Ck3(Array.Empty<string>()).Fingerprint);
        Assert.NotEqual(FlagCommandTable.Ck3(Array.Empty<string>()).Fingerprint, FlagCommandTable.Ck3(new[] { "add_dynasty_flag" }).Fingerprint);
        Assert.NotEqual(FlagCommandTable.Ck3(Array.Empty<string>()).Fingerprint, FlagCommandTable.Eu5.Fingerprint);
    }

    [Theory]
    [InlineData("common/buildings/x.txt", true)]
    [InlineData("backup/common/buildings/x.txt", false)]
    [InlineData("old/common/traits/x.txt", false)]
    public void DefinitionFlagFolders_AnchoredAtRoot(string path, bool isDefinitionFlag)
    {
        var sites = Read("castle_01 = { flag = fully_fortified }", path).Sites;
        Assert.Equal(isDefinitionFlag, sites.Any(s => s.Kind == FlagKind.DefinitionFlag && s.Role == FlagRole.Set));
    }

    [Theory]
    [InlineData("common/religion/tenet_types/00.txt", true)]
    [InlineData("backup/common/religion/tenet_types/00.txt", false)]
    public void TenetFolder_AnchoredAtRoot(string path, bool isTenetFile)
    {
        var sites = Read("tenet_a = { personal_tenet_parameters = { tok_one } }", path).Sites;
        Assert.Equal(isTenetFile, sites.Any(s => s.Name == "tok_one"));
    }

    [Fact]
    public void DefinitionFlag_KeyIgnoresCase()
    {
        var site = Assert.Single(Read("castle_01 = { Flag = fully_fortified }", "common/buildings/x.txt").Sites);
        Assert.Equal(("fully_fortified", FlagKind.DefinitionFlag, FlagRole.Set), (site.Name, site.Kind, site.Role));
    }

    [Fact]
    public void RelationFlag_BlockField()
    {
        var site = Assert.Single(Read("has_relation_flag = { target = scope:x relation = friend flag = met }").Sites);
        Assert.Equal(new RawSite("met", FlagKind.ScopeFlag, "relation_flag", FlagRole.Read, 1), site);
    }

    [Fact]
    public void ExtraCommandFromScriptDocs()
    {
        var scan = Read("add_dynasty_flag = d\nadd_gold = 5", extra: new[] { "add_dynasty_flag", "add_gold" });
        var site = Assert.Single(scan.Sites);
        Assert.Equal("d", site.Name);
        Assert.Equal("dynasty_flag", site.Family);
    }

    [Fact]
    public void BuildingDefinitionFlag()
    {
        var scan = Read("castle_01 = { flag = fully_fortified }\nhas_building_with_flag = fully_fortified", "common/buildings/x.txt");
        Assert.Contains(scan.Sites, s => s.Name == "fully_fortified" && s.Kind == FlagKind.DefinitionFlag && s.Role == FlagRole.Set);
        Assert.Contains(scan.Sites, s => s.Name == "fully_fortified" && s.Kind == FlagKind.DefinitionFlag && s.Role == FlagRole.Read);
        Assert.Equal(2, scan.Sites.Count);
    }

    [Fact]
    public void DefinitionFlag_TraitsAndBlockReader()
    {
        Assert.Single(Read("brave = { flag = x }", "common/traits/a.txt").Sites);
        var read = Assert.Single(Read("has_building_with_flag = { flag = y }", "events/e.txt").Sites);
        Assert.Equal(new RawSite("y", FlagKind.DefinitionFlag, null, FlagRole.Read, 1), read);
    }

    [Fact]
    public void DefinitionFlag_LawsNestedAndLegitimacyDepth2()
    {
        var law = Assert.Single(Read("law_group = { law_a = { modify = { flag = nested_law } } }", "common/laws/a.txt").Sites);
        Assert.Equal(new RawSite("nested_law", FlagKind.DefinitionFlag, null, FlagRole.Set, 1), law);
        var leg = Assert.Single(Read("t = { l = { flag = reduced_x } }", "common/legitimacy/a.txt").Sites);
        Assert.Equal("reduced_x", leg.Name);
        Assert.Equal(FlagKind.DefinitionFlag, leg.Kind);
    }

    [Fact]
    public void DefinitionFlag_NotInsideFlagCommand()
    {
        var site = Assert.Single(Read("b = { effect = { add_character_flag = { flag = c } } }", "common/buildings/a.txt").Sites);
        Assert.Equal(FlagKind.ScopeFlag, site.Kind);
    }

    [Fact]
    public void DefinitionFlag_TenetParameterTokens()
    {
        var scan = Read("tenet_a = {\n personal_tenet_parameters = {\n  tok_one\n  tok_two\n }\n other = { tok_three }\n}", "common/religion/tenet_types/00.txt");
        Assert.Equal(new[] { "tok_one", "tok_two" }, scan.Sites.Select(s => s.Name));
        Assert.All(scan.Sites, s => Assert.Equal(FlagRole.Set, s.Role));
        Assert.Equal(new[] { 3, 4 }, scan.Sites.Select(s => s.Line));
        Assert.Equal(FlagRole.Read, Assert.Single(Read("has_personal_tenet_flag = tok_one", "events/e.txt").Sites).Role);
    }

    [Fact]
    public void NotNameFlagCommands_Ignored()
    {
        var scan = Read("has_court_event_flag = x\nadd_internal_flag = y", extra: new[] { "has_court_event_flag", "add_internal_flag" });
        Assert.Empty(scan.Sites);
        Assert.False(Ck3FlagCommands.IsFlagCommand("has_court_event_flag"));
    }

    [Fact]
    public void BlockFormWithPrefixValue_NoBogusSite()
    {
        var a = Assert.Single(Read("add_character_flag = { flag = var:x }").Sites);
        Assert.Equal(new RawSite("x", FlagKind.Variable, null, FlagRole.Read, 1), a);
        var b = Assert.Single(Read("set_variable = { name = var:x }").Sites);
        Assert.Equal(FlagRole.Read, b.Role);
    }

    [Fact]
    public void FlagOutsideDefinitionFolders_NotDefinition()
    {
        Assert.Empty(Read("a = { flag = x }", "events/a.txt").Sites);
    }

    [Fact]
    public void Ck3_FlagPrefixIsNotSymbol()
    {
        Assert.Empty(Read("x = flag:x", "events/a.txt").Sites);
    }

    [Fact]
    public void Shipped_ContainsCore()
    {
        foreach (var n in new[] { "add_character_flag", "has_character_flag", "remove_character_flag", "add_relation_flag", "has_relation_flag", "remove_relation_flag" })
        {
            Assert.Contains(n, Ck3FlagCommands.Shipped);
            Assert.True(Ck3FlagCommands.IsFlagCommand(n));
        }

        Assert.False(Ck3FlagCommands.IsFlagCommand("has_building_with_flag"));
        Assert.False(Ck3FlagCommands.IsFlagCommand("add_gold"));
    }
}
