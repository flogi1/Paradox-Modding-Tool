using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public sealed class Eu4Tests : IDisposable
{
    private static readonly ScriptReader Reader = new(FlagCommandTable.Eu4);

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-flageu4-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temp, true);
        }
        catch (IOException)
        {
        }
    }

    private static List<RawSite> Read(string text, string path = "events/e.txt") => Reader.Read(path, text).Sites;

    private static (string Name, FlagKind Kind, FlagRole Role) Short(RawSite s) => (s.Name, s.Kind, s.Role);

    [Fact]
    public void For_Eu4()
    {
        var table = FlagCommandTable.Eu4;
        Assert.Same(table, FlagCommandTable.For("eu4"));
        Assert.True(table.NamesIgnoreCase);
        Assert.True(table.AtSuffixPatterns);
        Assert.True(table.EventTargetPrefix);
        Assert.True(table.ConditionalBlocks);
        Assert.False(table.MetaBracketPatterns);
        Assert.False(table.Hoi4VariableNames);
        Assert.False(table.HasSymbolValues);
        Assert.False(FlagCommandTable.Hoi4.ConditionalBlocks);
        Assert.Equal(LocStyle.Eu4Brackets, table.LocalisationStyle);
        Assert.NotEqual(FlagCommandTable.Hoi4.Fingerprint, table.Fingerprint);
    }

    [Fact]
    public void CountryFlag_AndHad()
    {
        var sites = Read("set_country_flag = a\nhad_country_flag = { flag = a days = 10 }\nclr_country_flag = a");

        Assert.Equal(
            new[]
            {
                new RawSite("a", FlagKind.ScopeFlag, "country_flag", FlagRole.Set, 1),
                new RawSite("a", FlagKind.ScopeFlag, "country_flag", FlagRole.Read, 2),
                new RawSite("a", FlagKind.ScopeFlag, "country_flag", FlagRole.Remove, 3),
            },
            sites);
    }

    [Theory]
    [InlineData("set_province_flag", "province_flag", FlagRole.Set)]
    [InlineData("has_global_flag", "global_flag", FlagRole.Read)]
    [InlineData("clr_ruler_flag", "ruler_flag", FlagRole.Remove)]
    [InlineData("has_heir_flag", "heir_flag", FlagRole.Read)]
    [InlineData("set_consort_flag", "consort_flag", FlagRole.Set)]
    [InlineData("set_permanent_province_flag", "province_flag", FlagRole.Set)]
    [InlineData("Has_Country_Flag", "country_flag", FlagRole.Read)]
    public void FlagFamilies(string command, string family, FlagRole role)
    {
        Assert.Equal(new RawSite("f", FlagKind.ScopeFlag, family, role, 1), Assert.Single(Read($"{command} = f")));
    }

    [Fact]
    public void HadFlag_Families()
    {
        var site = Assert.Single(Read("had_ruler_flag = { flag = r days = 5 }"));
        Assert.Equal(new RawSite("r", FlagKind.ScopeFlag, "ruler_flag", FlagRole.Read, 1), site);
    }

    [Fact]
    public void NotAFlagCommand()
    {
        Assert.Empty(Read("clr_auto_complete_flag = yes\ntreasurer_flag = { x = y }\nset_parliament_action_flag = { parliament_action = p }"));
    }

    [Fact]
    public void TwoWhich()
    {
        Assert.Equal(
            new[] { ("X", FlagKind.Variable, FlagRole.Set), ("Y", FlagKind.Variable, FlagRole.Read) },
            Read("change_variable = { which = X which = Y }").Select(Short));

        Assert.Equal(
            ("lakeInvestment", FlagKind.Variable, FlagRole.Set),
            Short(Assert.Single(Read("set_variable = { which = lakeInvestment which = PREV }"))));
    }

    [Fact]
    public void WhichEventTargetSecond()
    {
        Assert.Equal(
            new[] { ("a", FlagKind.Variable, FlagRole.Set), ("t", FlagKind.EventTarget, FlagRole.Read) },
            Read("change_variable = { which = a which = event_target:t }").Select(Short));
    }

    [Fact]
    public void WhichVariablePrefixSecond()
    {
        Assert.Equal(
            new[] { ("a", FlagKind.Variable, FlagRole.Set), ("b", FlagKind.Variable, FlagRole.Read) },
            Read("set_variable = { which = a which = variable:b }").Select(Short));
    }

    [Fact]
    public void CheckVariable_TwoWhich_BothRead()
    {
        Assert.Equal(
            new[] { ("a", FlagKind.Variable, FlagRole.Read), ("b", FlagKind.Variable, FlagRole.Read) },
            Read("check_variable = { which = a which = b }").Select(Short));
    }

    [Fact]
    public void KeyAsName()
    {
        Assert.Equal(("tur_power", FlagKind.Variable, FlagRole.Set), Short(Assert.Single(Read("change_variable = { tur_power = 2 }"))));
        Assert.Equal(("tur_power", FlagKind.Variable, FlagRole.Read), Short(Assert.Single(Read("check_variable = { tur_power > 100 }"))));
    }

    [Fact]
    public void ExportToVariable()
    {
        Assert.Equal(
            ("mtp", FlagKind.Variable, FlagRole.Set),
            Short(Assert.Single(Read("export_to_variable = { variable_name = mtp value = treasury who = ROOT }"))));
    }

    [Fact]
    public void ValueIsNeverAName()
    {
        Assert.Equal(
            ("MoneyToPay", FlagKind.Variable, FlagRole.Set),
            Short(Assert.Single(Read("set_variable = { which = MoneyToPay value = years_of_income }"))));
    }

    [Fact]
    public void NoJominiVariableCommands()
    {
        Assert.Empty(Read("has_variable = x\nset_global_variable = { name = g }"));
    }

    [Fact]
    public void EventTargets()
    {
        Assert.Equal(
            new[]
            {
                ("emerald_buddha_province", FlagKind.EventTarget, FlagRole.Set),
                ("emerald_buddha_province", FlagKind.EventTarget, FlagRole.Read),
            },
            Read("save_event_target_as = emerald_buddha_province\nevent_target:emerald_buddha_province = { }").Select(Short));

        Assert.Equal(
            new[]
            {
                ("g", FlagKind.GlobalEventTarget, FlagRole.Set),
                ("g", FlagKind.GlobalEventTarget, FlagRole.Remove),
                ("l", FlagKind.EventTarget, FlagRole.Read),
                ("g", FlagKind.GlobalEventTarget, FlagRole.Read),
            },
            Read("save_global_event_target_as = g\nclear_global_event_target = g\nhas_saved_event_target = l\nhas_saved_global_event_target = g").Select(Short));
    }

    [Fact]
    public void AtSuffixFlag_IsPattern()
    {
        var site = Assert.Single(Read("set_province_flag = visited_@ROOT"));
        Assert.Equal(new RawSite("visited_*", FlagKind.ScopeFlag, "province_flag", FlagRole.Set, 1, IsPattern: true), site);
    }

    [Fact]
    public void GluedConditionalBlock()
    {
        Assert.Equal(
            new RawSite("ct_flag", FlagKind.ScopeFlag, "country_flag", FlagRole.Set, 1),
            Assert.Single(Read("[[custom_tooltip]set_country_flag = ct_flag]")));
    }

    [Fact]
    public void LineDelimitedConditionalBlock_ContentCounts()
    {
        var sites = Read("[[no_leader]\nset_country_flag = a\n]\n[[!x]has_global_flag = b]");
        Assert.Equal(new[] { ("a", FlagKind.ScopeFlag, FlagRole.Set), ("b", FlagKind.ScopeFlag, FlagRole.Read) }, sites.Select(Short));
    }

    [Fact]
    public void ConditionalBlock_GluedCallIsFound()
    {
        const string path = "common/scripted_effects/e.txt";
        var calls = Reader.ReadCalls(path, "outer = {\n\t[[p]inner = { a = x_$p$] }\n}", new HashSet<string> { "inner" });
        var call = Assert.Single(calls);
        Assert.Equal("inner", call.Callee);
        Assert.Equal("x_$p$", call.Params!["a"]);
    }

    [Fact]
    public void ConditionalInName_IsTemplate()
    {
        var scan = Reader.Read("common/scripted_effects/e.txt", "e = {\n\tset_country_flag = foo[[p]_bar]\n}");
        Assert.Empty(scan.Sites);
        Assert.Equal("foo[[p]_bar]", Assert.Single(scan.Templates).NameTemplate);
    }

    [Fact]
    public void LocValueRead()
    {
        var scan = DataFunctionReader.Read("l_english:\n k:0 \"[Root.siam_openness.GetValue] [1.gpw_count.GetValue] [agenda_province.ag_var.GetValue]\"", true, LocStyle.Eu4Brackets);

        Assert.Equal(
            new[]
            {
                new RawSite("siam_openness", FlagKind.Variable, null, FlagRole.Read, 2),
                new RawSite("gpw_count", FlagKind.Variable, null, FlagRole.Read, 2),
                new RawSite("ag_var", FlagKind.Variable, null, FlagRole.Read, 2),
            },
            scan.Sites);
    }

    [Fact]
    public void LocBrackets_SkipsScopesTagsAndComments()
    {
        var scan = DataFunctionReader.Read(
            "l_english:\n# k:0 \"[commented.GetName]\"\n k:0 \"[Root.GetName] [Monarch.GetName] [FRA.GetName] [12.GetName] [Root.Monarch.GetName] [Root.Prev.GetValue] [x.Name]\"",
            true,
            LocStyle.Eu4Brackets);

        Assert.Empty(scan.Sites);
    }

    [Fact]
    public void LocValueRead_ScopeSegmentIsNoVariable()
    {
        var scan = DataFunctionReader.Read(
            "l_english:\n k:0 \"[Root.Monarch.GetValue] [Root.Owner.GetValue] [This.Capital.GetValue] [From.Heir.GetValue]\"", true, LocStyle.Eu4Brackets);

        Assert.Empty(scan.Sites);
    }

    [Fact]
    public void LocEventTarget_IsCandidate()
    {
        var site = Assert.Single(DataFunctionReader.Read("l_english:\n k:0 \"[rival_country.GetUsableName]\"", true, LocStyle.Eu4Brackets).Sites);
        Assert.Equal(new RawSite("rival_country", FlagKind.EventTarget, null, FlagRole.Read, 2, IsCandidate: true), site);
    }

    [Fact]
    public void Gui_HasNoReads()
    {
        Assert.Empty(DataFunctionReader.Read("text = \"[Root.x.GetValue]\"", false, LocStyle.Eu4Brackets).Sites);
    }

    [Fact]
    public void LocEventTargetCandidate()
    {
        var vanilla = Path.Combine(_temp, "v");
        var mod = Path.Combine(_temp, "m");
        WriteFile(mod, "events/e.txt", "e.1 = {\n\timmediate = {\n\t\tsave_event_target_as = emerald_buddha_province\n\t}\n}");
        WriteFile(mod, "localisation/x_l_english.yml", "l_english:\n k:0 \"[emerald_buddha_province.GetName] [unknown_thing.GetName]\"");
        Directory.CreateDirectory(vanilla);

        var index = Eu4Builder(vanilla, mod).Build();

        var sites = index.Search("emerald_buddha_province").SitesByName["emerald_buddha_province"];
        Assert.Contains(sites, s => s.Role == FlagRole.Set);
        var read = Assert.Single(sites, s => s.Role == FlagRole.Read);
        Assert.Equal(new SiteLocation(mod, "localisation/x_l_english.yml", 2), read.Location);
        Assert.Empty(index.Search("unknown_thing").Names);
    }

    [Fact]
    public void LocEventTargetCandidate_SavedGlobally_IsGlobalRead()
    {
        var vanilla = Path.Combine(_temp, "v");
        var mod = Path.Combine(_temp, "m");
        Directory.CreateDirectory(vanilla);
        WriteFile(mod, "events/e.txt", "save_global_event_target_as = g\nsave_event_target_as = both\nsave_global_event_target_as = both");
        WriteFile(mod, "localisation/x_l_english.yml", "l_english:\n k:0 \"[g.GetName] [both.GetName]\"");

        var index = Eu4Builder(vanilla, mod).Build();

        var g = Assert.Single(index.Search("g").SitesByName["g"], s => s.Role == FlagRole.Read);
        Assert.Equal(FlagKind.GlobalEventTarget, g.Kind);
        Assert.Equal(new SiteLocation(mod, "localisation/x_l_english.yml", 2), g.Location);
        Assert.Equal(FlagKind.EventTarget, Assert.Single(index.Search("both").SitesByName["both"], s => s.Role == FlagRole.Read).Kind);
    }

    [Fact]
    public void Latin1Eu4File()
    {
        var vanilla = Path.Combine(_temp, "v");
        var mod = Path.Combine(_temp, "m");
        Directory.CreateDirectory(vanilla);
        var path = Path.Combine(mod, "events", "latin.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = new List<byte>();
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("# Fran"));
        bytes.Add(0xE7);
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("ois\nset_country_flag = latin_flag\n"));
        File.WriteAllBytes(path, bytes.ToArray());

        var index = Eu4Builder(vanilla, mod).Build();

        var site = Assert.Single(index.Search("latin_flag").SitesByName["latin_flag"]);
        Assert.Equal(new SiteLocation(mod, "events/latin.txt", 2), site.Location);
        Assert.Empty(index.UnreadFiles);
    }

    private static FlagIndexBuilder Eu4Builder(string vanilla, string mod) =>
        new(FlagSourceSet.ForPlayset(RepoFiles.Profile("eu4"), new[] { new ModSource("My Mod", mod, false, 0) }, mod, new[] { vanilla }), FlagCommandTable.Eu4);

    private static void WriteFile(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
