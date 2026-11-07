using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public class Vic3Tests
{
    private static readonly ScriptReader Reader = new(FlagCommandTable.Vic3);

    [Fact]
    public void For_Vic3_NotNull()
    {
        Assert.Same(FlagCommandTable.Vic3, FlagCommandTable.For("vic3"));
        Assert.False(FlagCommandTable.Vic3.HasSymbolValues);
        Assert.False(FlagCommandTable.Vic3.NamesIgnoreCase);
        Assert.Empty(FlagCommandTable.Vic3.DefinitionFlagFolders);
        Assert.NotEqual(FlagCommandTable.Eu5.Fingerprint, FlagCommandTable.Vic3.Fingerprint);
    }

    [Fact]
    public void PureParamVariable_ResolvedThroughCall()
    {
        const string effectPath = "common/scripted_effects/e.txt";
        const string callerPath = "common/journal_entries/j.txt";
        const string effect = "rebind = { has_variable = $UNLOCK_VAR$ }";
        const string caller = "j = {\n\teffect = {\n\t\trebind = { UNLOCK_VAR = prestige_good_generic_clothes_var }\n\t}\n}";
        var effectScan = Reader.Read(effectPath, effect);
        var callerScan = Reader.Read(callerPath, caller);
        var names = effectScan.Definitions.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
        callerScan.Calls.AddRange(Reader.ReadCalls(callerPath, caller, names));
        var files = new List<FlagScannedFile>
        {
            new(new SiteLocation(@"C:\v", effectPath, 0), FlagOrigin.Vanilla, "Vanilla", effectScan),
            new(new SiteLocation(@"C:\v", callerPath, 0), FlagOrigin.Vanilla, "Vanilla", callerScan),
        };

        var site = Assert.Single(ParamResolver.Resolve(files, null));
        Assert.Equal("prestige_good_generic_clothes_var", site.Name);
        Assert.Equal(FlagKind.Variable, site.Kind);
        Assert.Equal(FlagRole.Read, site.Role);
        Assert.Equal(3, site.Location.Line);
    }

    [Fact]
    public void GlobalListIterator()
    {
        var site = Assert.Single(Reader.Read("common/journal_entries/j.txt", "any_in_global_list = { variable = aes_country_list }").Sites);
        Assert.Equal(new RawSite("aes_country_list", FlagKind.GlobalVariableList, null, FlagRole.Read, 1), site);
    }

    [Fact]
    public void NoFlagCommands()
    {
        Assert.Empty(Reader.Read("common/events/e.txt", "set_country_flag = x").Sites);
        Assert.Empty(Reader.Read("common/events/e.txt", "trigger = { a = flag:x }").Sites);
    }

    [Fact]
    public void GetList_GetVarTimeRemaining()
    {
        var list = Assert.Single(DataFunctionReader.Read("[Foo.GetList('daimyos_list')]").Sites);
        Assert.Equal(("daimyos_list", FlagKind.VariableList, FlagRole.Read), (list.Name, list.Kind, list.Role));

        var timed = Assert.Single(DataFunctionReader.Read("key:0 \"[GetVarTimeRemaining( GetPlayer.MakeScope, 'da_cooldown')]\"", localization: true).Sites);
        Assert.Equal(("da_cooldown", FlagKind.Variable, FlagRole.Read), (timed.Name, timed.Kind, timed.Role));
    }
}
