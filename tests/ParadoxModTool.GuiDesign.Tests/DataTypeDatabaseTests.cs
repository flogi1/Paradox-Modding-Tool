using ParadoxModTool.GuiDesign.DataTypes;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class DataTypeDatabaseTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("pmt-datatypes-db-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temp, recursive: true);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    internal static DataTypeEntry Type(string name) => new(name, "", DataTypeKind.Type, Array.Empty<string>(), "", "", "t");

    internal static DataTypeEntry Promote(string owner, string name, string to) => new(name, owner, DataTypeKind.Promote, Array.Empty<string>(), to, "", "t");

    internal static DataTypeEntry Function(string owner, string name, string returns, string description = "", params string[] args) =>
        new(name, owner, DataTypeKind.Function, args, returns, description, "t");

    internal static DataTypeEntry Global(DataTypeKind kind, string name, string returns, params string[] args) => new(name, "", kind, args, returns, "", "t");

    internal static DataTypeDatabase Db() => new(new[]
    {
        Type("Country"), Type("Location"), Type("Province"), Type("ActiveHegemon"),
        Promote("Country", "GetCapital", "Location"),
        Promote("Location", "GetProvince", "Province"),
        Promote("Province", "GetCapitalLocation", "Location"),
        Promote("ActiveHegemon", "GetCurrent", "Country"),
        Function("Country", "GetName", "CString"),
        Function("Location", "GetName", "CString"),
        Function("Country", "GetCapitalName", "CString", "capital city name"),
        Function("Country", "GetCapital", DataTypeEntry.Unregistered),
        Function("Country", "GetTaxBase", "CFixedPoint", "the capital of taxes"),
        Global(DataTypeKind.GlobalPromote, "GetPlayer", "Country"),
        Global(DataTypeKind.GlobalFunction, "Not", "bool", "Arg0"),
    });

    [Fact]
    public void Members_AndPromote()
    {
        Assert.Contains(Db().Members("Country"), e => e.Name == "GetCapital" && e.Kind == DataTypeKind.Promote);
        Assert.Equal("Location", Db().Promote("Country", "GetCapital"));
        Assert.Null(Db().Promote("Country", "GetName"));
        Assert.True(Db().IsType("Country"));
        Assert.Contains(Db().Globals, e => e.Name == "GetPlayer");
        Assert.Equal(new[] { "ActiveHegemon", "Country", "Location", "Province" }, Db().Types);
    }

    [Fact]
    public void Search_OrdersExactThenPrefixThenWordThenDescription()
    {
        var names = Db().Search("capital").Select(e => e.FullName).ToList();

        // Rang 2 (Wortteil "Capital") für alle drei Namen, dann nach FullName; die Beschreibung ("the capital of taxes") kommt zuletzt.
        Assert.Equal("Country.GetCapital", names[0]);
        Assert.Contains("Province.GetCapitalLocation", names);
        Assert.Contains("Country.GetCapitalName", names);
        Assert.Equal("Country.GetTaxBase", names[^1]);
        Assert.Equal("GetPlayer", Db().Search("getplayer")[0].FullName);
    }

    [Fact]
    public void Search_MatchesOwnerTypeName()
    {
        Assert.Contains(Db().Search("hegemon"), e => e.FullName == "ActiveHegemon.GetCurrent");
    }

    [Fact]
    public void PathTo_FindsShortestPromoteChain_WithinThreeSteps()
    {
        Assert.Equal(new[] { "GetCapital", "GetProvince" }, Db().PathTo("Country", "Province")!.Select(e => e.Name));
        Assert.Empty(Db().PathTo("Country", "Country")!);
        Assert.Null(Db().PathTo("Location", "ActiveHegemon"));
    }

    [Fact]
    public void Load_MissingFolder_IsEmptyAndMissing()
    {
        var load = DataTypeSource.Load(Path.Combine(_temp, "nope"), null, Path.Combine(_temp, "cache.json"));

        Assert.Equal(DataTypeStatus.Missing, load.Status);
        Assert.Empty(load.Database.Types);
        Assert.Null(load.FilesUtc);
    }

    [Fact]
    public void Load_StaleWhenExeIsNewer_AndUsesCacheSecondTime()
    {
        var folder = Path.Combine(_temp, "data_types");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "data_types_common.txt");
        File.WriteAllText(file, "Country\nDefinition type: Type\n\n-----------------------\n");
        var t0 = new DateTime(2026, 5, 17, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, t0);
        var install = Path.Combine(_temp, "install");
        Directory.CreateDirectory(Path.Combine(install, "binaries"));
        var exe = Path.Combine(install, "binaries", "game.exe");
        File.WriteAllText(exe, "x");
        File.SetLastWriteTimeUtc(exe, t0.AddDays(1));
        var cache = Path.Combine(_temp, "cache.json");

        var first = DataTypeSource.Load(folder, install, cache);
        var second = DataTypeSource.Load(folder, install, cache);

        Assert.Equal(DataTypeStatus.Stale, first.Status);
        Assert.Equal(t0, first.FilesUtc);
        Assert.True(File.Exists(cache));
        Assert.Equal(first.Database.Types, second.Database.Types);
        Assert.Equal(new[] { "Country" }, second.Database.Types);

        File.SetLastWriteTimeUtc(exe, t0.AddDays(-1));
        Assert.Equal(DataTypeStatus.Current, DataTypeSource.Load(folder, install, cache).Status);
    }

    [Fact]
    public void FolderFor_UsesParentOfModRoot()
    {
        Assert.Equal(Path.Combine(@"C:\Docs\Game", "logs", "data_types"), DataTypeSource.FolderFor(@"C:\Docs\Game\mod"));
        Assert.Null(DataTypeSource.FolderFor(string.Empty));
    }
}
