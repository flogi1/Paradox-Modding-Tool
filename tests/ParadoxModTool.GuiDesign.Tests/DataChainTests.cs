using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.DataTypes;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using static ParadoxModTool.GuiDesign.Tests.DataTypeDatabaseTests;

namespace ParadoxModTool.GuiDesign.Tests;

public class DataChainTests
{
    private static GuiPropertyCatalog Catalog(params (string List, string Type)[] items)
    {
        var catalog = new GuiPropertyCatalog();
        foreach (var (list, type) in items)
        {
            catalog.ListItemTypes[list] = type;
        }

        return catalog;
    }

    private static DataTypeDatabase DbWithScriptedGuiPromote() => new(Db().Entries.Concat(new[]
    {
        Type("ScriptedGui"),
        Global(DataTypeKind.GlobalPromote, "GetScriptedGui", "ScriptedGui", "Arg0"),
    }));

    [Fact]
    public void Text_WithArgumentsAndGlobalStart()
    {
        var chain = DataChain.Empty("GetPlayer").Append(Promote("Country", "GetCapital", "Location")).Append(Function("Location", "GetName", "CString", "", "Arg0"));

        Assert.Equal("[GetPlayer.GetCapital.GetName( ARG )]", chain.Text);
        Assert.True(chain.IsClosed);
        Assert.Null(chain.EndType(Db()));
        Assert.Equal("Location", DataChain.Empty("Country").Append(Promote("Country", "GetCapital", "Location")).EndType(Db()));
        Assert.Equal("Country", DataChain.Empty("GetPlayer").EndType(Db()));
        Assert.Equal("[Country]", DataChain.Empty("Country").Text);
        Assert.Equal(string.Empty, DataChain.Empty(string.Empty).Text);
    }

    [Fact]
    public void TruncateAt_AndAppendRange()
    {
        var chain = DataChain.Empty("Country").AppendRange(new[] { Promote("Country", "GetCapital", "Location"), Promote("Location", "GetProvince", "Province") });

        Assert.Equal("[Country.GetCapital.GetProvince]", chain.Text);
        Assert.Equal("[Country.GetCapital]", chain.TruncateAt(1).Text);
    }

    [Fact]
    public void Resolver_TypeNamesGlobalPromotesAndPromoteChains()
    {
        Assert.Equal("Country", DataContextResolver.TypeOf("[Country]", Db()));
        Assert.Equal("Country", DataContextResolver.TypeOf("[GetPlayer]", Db()));
        Assert.Equal("Province", DataContextResolver.TypeOf("[Country.GetCapital.GetProvince]", Db()));
        Assert.Null(DataContextResolver.TypeOf("[Unknown.Thing]", Db()));
        Assert.Null(DataContextResolver.TypeOf("[Country.GetName]", Db()));
    }

    [Fact]
    public void Resolver_FunctionCallWithArguments()
    {
        Assert.Null(DataContextResolver.TypeOf("[GetScriptedGui( 'x' )]", Db()));
        Assert.Equal("ScriptedGui", DataContextResolver.TypeOf("[GetScriptedGui( 'x' )]", DbWithScriptedGuiPromote()));
    }

    [Fact]
    public void StartAt_UsesNearestDataContext_AndItemType()
    {
        var doc = GuiParser.Parse("w = { datacontext = \"[GetPlayer]\" box = { datamodel = \"[Province.GetLocations]\" item = { widget = { text = \"x\" } } } t = \"y\" }");
        var catalog = Catalog(("Province.GetLocations", "Location"));

        Assert.Equal("Country", DataContextResolver.StartAt(doc.FindDeepestAt(doc.Text.IndexOf("\"y\"", StringComparison.Ordinal))!, Db(), catalog));
        Assert.Equal("Location", DataContextResolver.StartAt(doc.FindDeepestAt(doc.Text.IndexOf("\"x\"", StringComparison.Ordinal))!, Db(), catalog));
    }

    [Fact]
    public void StartAt_DataContextInsideItemWins()
    {
        var doc = GuiParser.Parse("box = { datamodel = \"[Province.GetLocations]\" item = { widget = { datacontext = \"[GetPlayer]\" text = \"x\" } } }");

        Assert.Equal("Country", DataContextResolver.StartAt(doc.FindDeepestAt(doc.Text.IndexOf("\"x\"", StringComparison.Ordinal))!, Db(), Catalog(("Province.GetLocations", "Location"))));
    }

    [Fact]
    public void StartAt_ItemWithoutLearnedType_IsNull()
    {
        var doc = GuiParser.Parse("w = { datacontext = \"[GetPlayer]\" box = { datamodel = \"[Province.GetLocations]\" item = { widget = { text = \"x\" } } } }");

        Assert.Null(DataContextResolver.StartAt(doc.FindDeepestAt(doc.Text.IndexOf("\"x\"", StringComparison.Ordinal))!, Db(), Catalog()));
    }
}
