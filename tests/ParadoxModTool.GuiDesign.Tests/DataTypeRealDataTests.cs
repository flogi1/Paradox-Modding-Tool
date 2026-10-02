using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.DataTypes;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// GUI-Daten-Helfer gegen die echten Dateien dieses Rechners: Datentyp-Dump (<c>logs\data_types</c>) und Vanilla-GUI. Ohne die Dateien läuft nichts.
/// </summary>
public class DataTypeRealDataTests
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Paradox Interactive",
        "Europa Universalis V",
        "logs",
        "data_types");

    private static readonly Lazy<DataTypeDatabase> Database = new(() => new DataTypeDatabase(DataTypeLogParser.ParseFolder(Folder)));

    private readonly ITestOutputHelper _output;

    public DataTypeRealDataTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void RealDump_HasTypesPromotesAndSearch()
    {
        if (!Directory.Exists(Folder))
        {
            return;
        }

        var db = Database.Value;
        _output.WriteLine($"{db.Types.Count} Typen, {db.Entries.Count} Einträge");

        Assert.True(db.Types.Count > 1000, $"nur {db.Types.Count} Typen");
        Assert.Equal("Location", db.Promote("Country", "GetCapital"));
        Assert.Contains(db.Search("capital"), e => e.FullName == "Country.GetCapital");
        Assert.Equal("Country", DataContextResolver.TypeOf("[GetPlayer]", db));
    }

    [Fact]
    public void RealCk3Dump_ReadsEveryBlock()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Paradox Interactive", "Crusader Kings III", "logs", "data_types");
        if (!Directory.Exists(folder))
        {
            return;
        }

        // Nicht leere Blöcke im Dump vom 2026-09-30 (Spielversion 1.20.0.2); eine Abweichung ist ein Parser-Befund.
        var db = new DataTypeDatabase(DataTypeLogParser.ParseFolder(folder));

        Assert.Equal(24593, db.Entries.Count);
        Assert.Equal("Title", db.Promote("Character", "GetPrimaryTitle"));
    }

    [Fact]
    public void RealVanilla_LearnsListItemTypes()
    {
        if (!Directory.Exists(Folder) || !VanillaGui.Exists)
        {
            return;
        }

        var catalog = GuiCatalogLearner.Learn(GuiRulesVanillaTests.Vanilla, GuiRulesVanillaTests.ProfileRules(), "fp");
        _output.WriteLine($"{catalog.ListItemTypes.Count} Listen mit Elementtyp, {catalog.Examples.Count} Beispiel-Schlüssel");

        Assert.Equal("Location", catalog.ItemTypeOf("Province.GetLocations"));
        Assert.NotEmpty(catalog.ExamplesFor("Country.GetCapital"));
        Assert.All(catalog.ListItemTypes.Values.Take(50), type => Assert.True(Database.Value.IsType(type) || char.IsUpper(type[0]), type));
    }
}
