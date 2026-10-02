using ParadoxModTool.GuiDesign.DataTypes;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class DataTypeParserTests
{
    private const string Sample =
        "Country\r\nDefinition type: Type\r\n\r\n-----------------------\r\n\r\n" +
        "Country.GetCapital\r\nDefinition type: Promote\r\nReturn type: Location\r\n\r\n-----------------------\r\n\r\n" +
        "Country.GetCapital\r\nDefinition type: Function\r\nReturn type: [unregistered]\r\n\r\n-----------------------\r\n\r\n" +
        "EqualTo_int32( Arg0, Arg1 )\r\nDefinition type: Global function\r\nReturn type: bool\r\n\r\n-----------------------\r\n\r\n" +
        "GetPlayer\r\nDescription: The player\r\nDefinition type: Global promote\r\nReturn type: Country\r\n\r\n-----------------------\r\n\r\n" +
        "CMFLogActorCountry( Index )\r\nDescription: Actor country\r\nDefinition type: Global macro\r\n\r\n\r\n";

    [Fact]
    public void Parse_ReadsAllKinds()
    {
        var entries = DataTypeLogParser.Parse(Sample, "data_types_common.txt");

        Assert.Equal(6, entries.Count);
        Assert.Equal(("Country", "", DataTypeKind.Type, "data_types_common.txt"), (entries[0].Name, entries[0].Owner, entries[0].Kind, entries[0].Source));
        var promote = entries[1];
        Assert.Equal(("GetCapital", "Country", DataTypeKind.Promote, "Location"), (promote.Name, promote.Owner, promote.Kind, promote.ReturnType));
        Assert.Equal(DataTypeKind.Function, entries[2].Kind);
        Assert.False(entries[2].HasRegisteredReturn);
        Assert.True(promote.HasRegisteredReturn);
        Assert.Equal(new[] { "Arg0", "Arg1" }, entries[3].Arguments);
        Assert.Equal(DataTypeKind.GlobalFunction, entries[3].Kind);
        Assert.Equal(("GetPlayer", "The player", "Country", DataTypeKind.GlobalPromote), (entries[4].Name, entries[4].Description, entries[4].ReturnType, entries[4].Kind));
        Assert.Equal((DataTypeKind.GlobalMacro, ""), (entries[5].Kind, entries[5].ReturnType));
        Assert.Equal(new[] { "Index" }, entries[5].Arguments);
        Assert.True(entries[5].IsGlobal);
    }

    [Fact]
    public void Parse_HandlesCrLfAndTrailingBlankLines()
    {
        Assert.Equal(6, DataTypeLogParser.Parse(Sample.Replace("\r\n", "\n") + "\n\n", "x").Count);
        Assert.Equal("Country.GetCapital", DataTypeLogParser.Parse(Sample, "x")[1].FullName);
        Assert.Equal("Country", DataTypeLogParser.Parse(Sample, "x")[0].FullName);
        Assert.Equal("EqualTo_int32", DataTypeLogParser.Parse(Sample, "x")[3].FullName);
    }

    [Fact]
    public void ParseFolder_ReadsOnlyDataTypeFiles()
    {
        var folder = Directory.CreateTempSubdirectory("pmt-datatypes-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "data_types_common.txt"), Sample);
            File.WriteAllText(Path.Combine(folder, "other.txt"), Sample);

            var entries = DataTypeLogParser.ParseFolder(folder);

            Assert.Equal(6, entries.Count);
            Assert.All(entries, e => Assert.Equal("data_types_common.txt", e.Source));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
