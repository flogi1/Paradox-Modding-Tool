using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public class Hoi4Tests
{
    private static readonly ScriptReader Reader = new(FlagCommandTable.Hoi4);

    private static List<RawSite> Read(string text, string path = "common/decisions/x.txt") => Reader.Read(path, text).Sites;

    private static (string Name, FlagKind Kind, FlagRole Role) Short(RawSite s) => (s.Name, s.Kind, s.Role);

    [Fact]
    public void For_Hoi4()
    {
        var table = FlagCommandTable.Hoi4;
        Assert.Same(table, FlagCommandTable.For("hoi4"));
        Assert.True(table.NamesIgnoreCase);
        Assert.True(table.AtSuffixPatterns);
        Assert.True(table.Hoi4VariableNames);
        Assert.True(table.EventTargetPrefix);
        Assert.False(table.HasSymbolValues);
        Assert.Equal(LocStyle.Hoi4Brackets, table.LocalisationStyle);
        Assert.Equal(LocStyle.DataFunctions, FlagCommandTable.Eu5.LocalisationStyle);
        Assert.Equal(LocStyle.DataFunctions, FlagCommandTable.Vic3.LocalisationStyle);
        Assert.False(FlagCommandTable.Eu5.AtSuffixPatterns);
        Assert.NotEqual(FlagCommandTable.Vic3.Fingerprint, table.Fingerprint);
    }

    [Fact]
    public void CountryFlag_BareAndBlock()
    {
        var sites = Read(
            "set_country_flag = ETH_disintegration_happened_flag\n"
            + "has_country_flag = { flag = X value > 1 }\n"
            + "clr_country_flag = X\n"
            + "modify_country_flag = { flag = X value = 1 }");

        Assert.Equal(
            new[]
            {
                new RawSite("ETH_disintegration_happened_flag", FlagKind.ScopeFlag, "country_flag", FlagRole.Set, 1),
                new RawSite("X", FlagKind.ScopeFlag, "country_flag", FlagRole.Read, 2),
                new RawSite("X", FlagKind.ScopeFlag, "country_flag", FlagRole.Remove, 3),
                new RawSite("X", FlagKind.ScopeFlag, "country_flag", FlagRole.Set, 4),
            },
            sites);
    }

    [Theory]
    [InlineData("set_state_flag", "state_flag", FlagRole.Set)]
    [InlineData("has_global_flag", "global_flag", FlagRole.Read)]
    [InlineData("clr_character_flag", "character_flag", FlagRole.Remove)]
    [InlineData("set_unit_leader_flag", "unit_leader_flag", FlagRole.Set)]
    [InlineData("has_project_flag", "project_flag", FlagRole.Read)]
    [InlineData("modify_mio_flag", "mio_flag", FlagRole.Set)]
    [InlineData("has_Country_flag", "country_flag", FlagRole.Read)]
    public void FlagFamilies(string command, string family, FlagRole role)
    {
        var site = Assert.Single(Read($"{command} = f"));
        Assert.Equal(new RawSite("f", FlagKind.ScopeFlag, family, role, 1), site);
    }

    [Fact]
    public void NotAFlagCommand()
    {
        Assert.Empty(Read("has_any_PRC_infiltration_flag = yes"));
    }

    [Fact]
    public void FlagWithDot_NotShortened()
    {
        Assert.Equal("baltic.5_fired", Assert.Single(Read("set_country_flag = baltic.5_fired")).Name);
    }

    [Fact]
    public void KeyAsName()
    {
        var site = Assert.Single(Read("set_variable = { global.CZE_selling_country = ROOT }"));
        Assert.Equal(new RawSite("CZE_selling_country", FlagKind.Variable, null, FlagRole.Set, 1), site);
    }

    [Fact]
    public void VarForm()
    {
        var sites = Read("add_to_variable = { var = a value = b }");
        Assert.Equal(new[] { ("a", FlagKind.Variable, FlagRole.Set), ("b", FlagKind.Variable, FlagRole.Read) }, sites.Select(Short));
    }

    [Fact]
    public void KeyForm_ValueIsRead()
    {
        var sites = Read("add_to_variable = { my_var = ROOT.other_var }");
        Assert.Equal(new[] { ("my_var", FlagKind.Variable, FlagRole.Set), ("other_var", FlagKind.Variable, FlagRole.Read) }, sites.Select(Short));
    }

    [Fact]
    public void ValueSide_SkipsNonNamesTooltipAndCompare()
    {
        var sites = Read("check_variable = { var = v value = @CONST compare = less_than tooltip = my_tt }\n"
            + "set_variable = { x = token:t }\nset_variable = { y = GER }\nclamp_variable = { var = c min = 0 max = cap_var }");
        Assert.Equal(
            new[]
            {
                ("v", FlagKind.Variable, FlagRole.Read),
                ("x", FlagKind.Variable, FlagRole.Set),
                ("y", FlagKind.Variable, FlagRole.Set),
                ("c", FlagKind.Variable, FlagRole.Set),
                ("cap_var", FlagKind.Variable, FlagRole.Read),
            },
            sites.Select(Short));
    }

    [Fact]
    public void ValueSide_VarPrefix_ReadOnce()
    {
        var sites = Read("set_variable = { x = var:global.y }");
        Assert.Equal(new[] { ("x", FlagKind.Variable, FlagRole.Set), ("y", FlagKind.Variable, FlagRole.Read) }, sites.Select(Short));
    }

    [Fact]
    public void CheckVariable_KeyForm()
    {
        var site = Assert.Single(Read("check_variable = { my_var > 5 }"));
        Assert.Equal(("my_var", FlagKind.Variable, FlagRole.Read), Short(site));
    }

    [Fact]
    public void BareVariableCommands()
    {
        var sites = Read("has_variable = h\nclear_variable = c\nround_variable = r");
        Assert.Equal(
            new[] { ("h", FlagKind.Variable, FlagRole.Read), ("c", FlagKind.Variable, FlagRole.Remove), ("r", FlagKind.Variable, FlagRole.Set) },
            sites.Select(Short));
    }

    [Fact]
    public void TempVariable()
    {
        var site = Assert.Single(Read("set_temp_variable = { t = 1 }"));
        Assert.Equal(("t", FlagKind.TempVariable, FlagRole.Set), Short(site));
        Assert.Equal(("m", FlagKind.TempVariable, FlagRole.Set), Short(Assert.Single(Read("career_profile_set_temp_variable = { m = 1 }"))));
        Assert.Equal(("t2", FlagKind.TempVariable, FlagRole.Set), Short(Assert.Single(Read("randomize_temp_variable = { var = t2 min = 0 max = 3 }"))));
    }

    [Fact]
    public void Array_AddAndIterate()
    {
        var add = Assert.Single(Read("add_to_array = { my_arr = 5 }"));
        Assert.Equal(("my_arr", FlagKind.Array, FlagRole.Set), Short(add));

        var loop = Assert.Single(Read("for_each_scope_loop = { array = global.my_arr }"));
        Assert.Equal(("my_arr", FlagKind.Array, FlagRole.Read), Short(loop));
    }

    [Fact]
    public void Array_Forms()
    {
        var sites = Read("add_to_array = { array = ROOT.a value = THIS }\nremove_from_array = { array = a index = 0 }\nclear_array = a\n"
            + "is_in_array = { array = global.a value = 1 }\nadd_to_temp_array = { ta = 1 }\nclear_temp_array = ta\nany_of = { array = a value = v }");
        Assert.Equal(
            new[]
            {
                ("a", FlagKind.Array, FlagRole.Set),
                ("a", FlagKind.Array, FlagRole.Remove),
                ("a", FlagKind.Array, FlagRole.Remove),
                ("a", FlagKind.Array, FlagRole.Read),
                ("ta", FlagKind.TempArray, FlagRole.Set),
                ("ta", FlagKind.TempArray, FlagRole.Remove),
                ("a", FlagKind.Array, FlagRole.Read),
                ("i", FlagKind.TempVariable, FlagRole.Set),
                ("v", FlagKind.TempVariable, FlagRole.Set),
            },
            sites.Select(Short));
    }

    [Fact]
    public void EventTargets()
    {
        var sites = Read("save_global_event_target_as = vichy\nevent_target:vichy = { }\nsave_event_target_as = t\nhas_event_target = t\n"
            + "clear_global_event_target = vichy\ntag = event_target:other.capital");
        Assert.Equal(
            new[]
            {
                ("vichy", FlagKind.GlobalEventTarget, FlagRole.Set),
                ("vichy", FlagKind.EventTarget, FlagRole.Read),
                ("t", FlagKind.EventTarget, FlagRole.Set),
                ("t", FlagKind.EventTarget, FlagRole.Read),
                ("vichy", FlagKind.GlobalEventTarget, FlagRole.Remove),
                ("other", FlagKind.EventTarget, FlagRole.Read),
            },
            sites.Select(Short));
    }

    [Fact]
    public void VarPrefix_LastSegment()
    {
        var sites = Read("var:global.RAJ_fuel = { }\nx = var:SWI:SWI_requested_territory");
        Assert.Equal(
            new[] { ("RAJ_fuel", FlagKind.Variable, FlagRole.Read), ("SWI_requested_territory", FlagKind.Variable, FlagRole.Read) },
            sites.Select(Short));
    }

    [Fact]
    public void AtSuffix_Pattern()
    {
        var site = Assert.Single(Read("has_state_flag = CHI_holding_@ROOT"));
        Assert.Equal(new RawSite("CHI_holding_*", FlagKind.ScopeFlag, "state_flag", FlagRole.Read, 1, IsPattern: true), site);

        var variable = Assert.Single(Read("set_variable = { ROOT.political_platform_@THIS = 1 }"));
        Assert.Equal(new RawSite("political_platform_*", FlagKind.Variable, null, FlagRole.Set, 1, IsPattern: true), variable);

        Assert.Equal("my_count@infantry_equipment", Assert.Single(Read("check_variable = { my_count@infantry_equipment > 1 }")).Name);
    }

    [Fact]
    public void ConstantKeys_Skipped()
    {
        Assert.Empty(Read("@inner_circle_time_tier_1_flag = 5\n@X = { set_country_flag = a }"));
    }

    [Fact]
    public void DynamicModifierValue_IsRead()
    {
        var sites = Read("m = { icon = GFX_x consumer_goods_factor = BUL_cg_mod stability_factor = 0.1 attacker_modifier = yes "
            + "enable = { original_tag = BUL } }", "common/dynamic_modifiers/d.txt");
        var site = Assert.Single(sites);
        Assert.Equal(("BUL_cg_mod", FlagKind.Variable, FlagRole.Read), Short(site));
    }

    [Fact]
    public void DynamicModifierValue_OtherFolder_NotRead()
    {
        Assert.Empty(Read("m = { consumer_goods_factor = BUL_cg_mod }", "common/ideas/d.txt"));
    }

    [Fact]
    public void DecisionCost_IsRead()
    {
        var sites = Read("d = { cost = ENG_cost?50 days_remove = global.days_x?1337 days_re_enable = 10 days_mission_timeout = t_var }");
        Assert.Equal(
            new[] { ("ENG_cost", FlagKind.Variable, FlagRole.Read), ("days_x", FlagKind.Variable, FlagRole.Read), ("t_var", FlagKind.Variable, FlagRole.Read) },
            sites.Select(Short));
    }

    [Fact]
    public void Cost_OutsideDecisions_NotRead()
    {
        Assert.Empty(Read("u = { cost = air }", "common/units/equipment/upgrades/air_upgrades.txt"));
    }

    [Fact]
    public void LocBrackets()
    {
        var sites = DataFunctionReader.Read(
            "l_english:\n k:0 \"[?JAP_desired_military_factories] [?global.x^num|Y0] [?FLAG_x:days]\"", localization: true, LocStyle.Hoi4Brackets).Sites;
        Assert.Equal(
            new[]
            {
                new RawSite("JAP_desired_military_factories", FlagKind.Variable, null, FlagRole.Read, 2),
                new RawSite("x", FlagKind.Variable, null, FlagRole.Read, 2),
                new RawSite("FLAG_x", FlagKind.ScopeFlag, null, FlagRole.Read, 2),
            },
            sites);
    }

    [Fact]
    public void LocBrackets_EventTargetGetNameAndComments()
    {
        var sites = DataFunctionReader.Read(
            "# k:0 \"[?commented]\"\n k:0 \"[?event_target:vichy.GetName] [?CHI.CHI_name_var.GetName] [?FLAG_y:days_left] [?v:value]\"",
            localization: true, LocStyle.Hoi4Brackets).Sites;
        Assert.Equal(
            new[]
            {
                ("vichy", FlagKind.EventTarget, FlagRole.Read),
                ("CHI_name_var", FlagKind.Variable, FlagRole.Read),
                ("FLAG_y", FlagKind.ScopeFlag, FlagRole.Read),
                ("v", FlagKind.ScopeFlag, FlagRole.Read),
            },
            sites.Select(Short));
    }

    [Fact]
    public void Hoi4Brackets_IgnoresDataFunctionsAndGui()
    {
        Assert.Empty(DataFunctionReader.Read(" k:0 \"[GetVariable('x')]\"", localization: true, LocStyle.Hoi4Brackets).Sites);
        Assert.Empty(DataFunctionReader.Read("text = \"[?x]\"", localization: false, LocStyle.Hoi4Brackets).Sites);
    }

    [Fact]
    public void MetaEffectBracket_Pattern()
    {
        var site = Assert.Single(Read("has_country_flag = GER_RK_[RK_TAG]_victim_flag"));
        Assert.Equal(new RawSite("GER_RK_*_victim_flag", FlagKind.ScopeFlag, "country_flag", FlagRole.Read, 1, IsPattern: true), site);
    }

    [Fact]
    public void AtVarSuffix_NameKeptAsPattern()
    {
        var sites = Read("set_variable = { a@var:b = 1 }");
        Assert.Contains(new RawSite("a*", FlagKind.Variable, null, FlagRole.Set, 1, IsPattern: true), sites);
        Assert.Contains(new RawSite("b", FlagKind.Variable, null, FlagRole.Read, 1), sites);
        Assert.Equal(2, sites.Count);

        var bare = Read("clear_variable = c@event_target:t");
        Assert.Contains(new RawSite("c*", FlagKind.Variable, null, FlagRole.Remove, 1, IsPattern: true), bare);
        Assert.Contains(new RawSite("t", FlagKind.EventTarget, null, FlagRole.Read, 1), bare);
        Assert.Equal(2, bare.Count);

        var flag = Read("has_country_flag = { flag = f@var:v }");
        Assert.Contains(new RawSite("f*", FlagKind.ScopeFlag, "country_flag", FlagRole.Read, 1, IsPattern: true), flag);
    }

    [Fact]
    public void ValueSide_ScopeChain_NotAName()
    {
        var site = Assert.Single(Read("set_variable = { x = FROM.FROM }\n"));
        Assert.Equal(("x", FlagKind.Variable, FlagRole.Set), Short(site));
        Assert.Equal(new[] { "x", "y" }, Read("set_variable = { x = PREV.OWNER }\nset_variable = { y = THIS.owner }").Select(s => s.Name));
        Assert.Equal(new[] { "z", "real" }, Read("set_variable = { z = ROOT.real }").Select(s => s.Name));
    }

    [Fact]
    public void LocFlag_ScopePrefixStripped()
    {
        var sites = DataFunctionReader.Read(
            " k:0 \"[?global.FLAG_x:days] [?ROOT.f:value] [?baltic.5_fired:days]\"", localization: true, LocStyle.Hoi4Brackets).Sites;
        Assert.Equal(new[] { "FLAG_x", "f", "baltic.5_fired" }, sites.Select(s => s.Name));
        Assert.All(sites, s => Assert.Equal(FlagKind.ScopeFlag, s.Kind));
    }

    [Fact]
    public void LoopVariables_AreTempSets()
    {
        var sites = Read("any_of = {\n array = a\n value = v2\n index = i2\n}\nfor_loop_effect = {\n value = n\n}\nfor_each_loop = { array = a }");
        Assert.Equal(
            new[]
            {
                ("a", FlagKind.Array, FlagRole.Read, 2),
                ("v2", FlagKind.TempVariable, FlagRole.Set, 3),
                ("i2", FlagKind.TempVariable, FlagRole.Set, 4),
                ("n", FlagKind.TempVariable, FlagRole.Set, 7),
                ("a", FlagKind.Array, FlagRole.Read, 9),
                ("v", FlagKind.TempVariable, FlagRole.Set, 9),
                ("i", FlagKind.TempVariable, FlagRole.Set, 9),
            },
            sites.Select(s => (s.Name, s.Kind, s.Role, s.Line)));
    }

    [Fact]
    public void LoopVariables_ImplicitDefaults()
    {
        var sites = Read("x = 1\nfor_loop_effect = {\n start = 0\n end = 10\n}\nfind_highest_in_array = {\n array = a\n value = best\n}\nfor_each_scope_loop = { array = a }\nany_of = yes");
        Assert.Equal(
            new[]
            {
                ("v", FlagKind.TempVariable, FlagRole.Set, 2),
                ("a", FlagKind.Array, FlagRole.Read, 7),
                ("i", FlagKind.TempVariable, FlagRole.Set, 6),
                ("best", FlagKind.TempVariable, FlagRole.Set, 8),
                ("a", FlagKind.Array, FlagRole.Read, 10),
            },
            sites.Select(s => (s.Name, s.Kind, s.Role, s.Line)));
        Assert.Equal("i", FlagCommandTable.Hoi4.FindLoopVariables("any_of").Single(l => l.Field == "index").Default);
    }

    [Fact]
    public void IgnoreCaseIndex()
    {
        var raw = Read("set_country_flag = AUS_Flag\nhas_country_flag = AUS_flag");
        var sites = raw.Select(r => new FlagSite(r.Name, r.Kind, r.Family, r.Role, new SiteLocation(@"C:\m", "common/decisions/x.txt", r.Line),
            FlagOrigin.ActiveMod, "My Mod")).ToList();
        var index = new FlagIndex(sites, Array.Empty<string>(), DateTime.UtcNow, 1, FlagCommandTable.Hoi4.NamesIgnoreCase);

        var summary = Assert.Single(index.Search("aus_flag").Names);
        Assert.Equal(1, summary.SetCount);
        Assert.Equal(1, summary.ReadCount);
        Assert.False(summary.NeverSet);
        Assert.False(summary.NeverRead);
    }

    [Fact]
    public void BuiltInVariables_ReadsDropped()
    {
        Assert.Empty(Read("check_variable = { num_of_factories > 50 }"));
        Assert.Equal(new[] { ("t", FlagKind.TempVariable, FlagRole.Set) }, Read("set_temp_variable = { t = party_popularity@communism }").Select(Short));
        Assert.Equal(new[] { ("x", FlagKind.Variable, FlagRole.Set) }, Read("set_variable = { x = THIS.id }").Select(Short));
        Assert.Empty(Read("for_each_scope_loop = { array = global.majors }"));
        Assert.Empty(Read("x = var:ROOT.Num_Of_Factories"));
        Assert.Empty(Read("check_variable = { ROOT.controller@ROOT > 0 }"));
        Assert.Equal(new[] { ("my_var", FlagKind.Variable, FlagRole.Read) }, Read("check_variable = { my_var > num_of_factories }").Select(Short));
    }

    [Fact]
    public void BuiltInVariables_SetsStay()
    {
        Assert.Equal(new[] { ("num_of_factories", FlagKind.Variable, FlagRole.Set) }, Read("set_variable = { num_of_factories = 1 }").Select(Short));
        Assert.Equal(new[] { ("majors", FlagKind.Array, FlagRole.Set) }, Read("add_to_array = { majors = 1 }").Select(Short));
    }

    [Fact]
    public void BuiltInVariables_LocBracketsDropped()
    {
        var sites = DataFunctionReader.Read(" k:0 \"[?ROOT.num_of_factories] [?party_popularity@communism|%] [?my_var]\"", localization: true, LocStyle.Hoi4Brackets).Sites;
        Assert.Equal(new[] { "my_var" }, sites.Select(s => s.Name));
    }

    [Fact]
    public void BuiltInVariables_OnlyHoi4()
    {
        Assert.Contains("num_of_factories", FlagCommandTable.Hoi4.BuiltInVariables);
        Assert.Contains("id", FlagCommandTable.Hoi4.BuiltInVariables);
        Assert.Empty(FlagCommandTable.Eu4.BuiltInVariables);
    }
}
