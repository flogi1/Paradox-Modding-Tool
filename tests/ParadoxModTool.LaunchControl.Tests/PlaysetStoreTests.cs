using System.Text.Json.Nodes;
using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class PlaysetStoreTests : IDisposable
{
    // Verkürzte, aber strukturell reale Fixture (zwei Playsets, je mit Mods und DLCs) - orientiert
    // an der vom Nutzer bereitgestellten echten playsets.json: nur EIN Playset trägt "isActive",
    // alle anderen lassen das Feld ganz weg. Als verkettete, normal escapte Zeichenketten (statt
    // eines C# 11 Raw String Literals) geschrieben, damit die lokale Klammer-Balance-Prüfung dieses
    // Projekts (die String-Literale anhand von \"..\" erkennt, siehe Lessons Learned Punkt 9 der
    // Architektur-Dokumentation) die eingebetteten JSON-Klammern korrekt als Testdaten überspringt.
    private const string FixtureJson =
        "{\n" +
        "  \"file_version\": \"1.0.0\",\n" +
        "  \"playsets\": [\n" +
        "    {\n" +
        "      \"name\": \"Initial playset\",\n" +
        "      \"isAutomaticallySorted\": true,\n" +
        "      \"orderedListMods\": [\n" +
        "        { \"path\": \"C:/mods/a/\", \"isEnabled\": true },\n" +
        "        { \"path\": \"C:/mods/b/\", \"isEnabled\": false }\n" +
        "      ],\n" +
        "      \"DLC\": [\n" +
        "        { \"paradoxAppId\": \"d000_shared\", \"isEnabled\": true }\n" +
        "      ]\n" +
        "    },\n" +
        "    {\n" +
        "      \"name\": \"Idea Variation Ceasar Version\",\n" +
        "      \"isActive\": true,\n" +
        "      \"isAutomaticallySorted\": true,\n" +
        "      \"orderedListMods\": [\n" +
        "        { \"path\": \"C:/mods/c/\", \"isEnabled\": true }\n" +
        "      ],\n" +
        "      \"DLC\": [\n" +
        "        { \"paradoxAppId\": \"d000_shared\", \"isEnabled\": true },\n" +
        "        { \"paradoxAppId\": \"d009_across_the_pillars\", \"isEnabled\": true }\n" +
        "      ]\n" +
        "    }\n" +
        "  ]\n" +
        "}";

    private readonly string _playsetsFilePath;

    public PlaysetStoreTests()
    {
        _playsetsFilePath = Path.Combine(Path.GetTempPath(), "pmt-playsets-" + Guid.NewGuid() + ".json");
        File.WriteAllText(_playsetsFilePath, FixtureJson);
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_playsetsFilePath);
            File.Delete(_playsetsFilePath + ".pmt-backup");
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    [Fact]
    public void ListPlaysets_ParsesNameActiveFlagAndModCounts()
    {
        var playsets = PlaysetStore.ListPlaysets(_playsetsFilePath);

        Assert.Equal(2, playsets.Count);

        var initial = Assert.Single(playsets, p => p.Name == "Initial playset");
        Assert.False(initial.IsActive);
        Assert.Equal(2, initial.ModCount);
        Assert.Equal(1, initial.EnabledModCount);

        var caesar = Assert.Single(playsets, p => p.Name == "Idea Variation Ceasar Version");
        Assert.True(caesar.IsActive);
        Assert.Equal(1, caesar.ModCount);
        Assert.Equal(1, caesar.EnabledModCount);
    }

    [Fact]
    public void ListPlaysets_FehlendeDatei_LiefertLeereListe()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), "pmt-playsets-nicht-vorhanden-" + Guid.NewGuid() + ".json");

        var playsets = PlaysetStore.ListPlaysets(missingPath);

        Assert.Empty(playsets);
    }

    [Fact]
    public void ListPlaysets_KeinPfad_LiefertLeereListe()
    {
        Assert.Empty(PlaysetStore.ListPlaysets(null));
        Assert.Empty(PlaysetStore.ListPlaysets("   "));
    }

    [Fact]
    public void ListPlaysets_UngueltigesJson_LiefertLeereListeStattAusnahme()
    {
        File.WriteAllText(_playsetsFilePath, "{ das ist kein gueltiges JSON");

        var playsets = PlaysetStore.ListPlaysets(_playsetsFilePath);

        Assert.Empty(playsets);
    }

    [Fact]
    public void SetActivePlayset_SetztZielAktivUndEntferntFlagBeiAnderenPlaysets()
    {
        var result = PlaysetStore.SetActivePlayset(_playsetsFilePath, "Initial playset");

        Assert.True(result.Success);

        var root = JsonNode.Parse(File.ReadAllText(_playsetsFilePath));
        var playsetsArray = Assert.IsType<JsonArray>(root!["playsets"]);

        var initial = playsetsArray.Single(p => (string?)p!["name"] == "Initial playset")!.AsObject();
        Assert.True((bool?)initial["isActive"]);

        var caesar = playsetsArray.Single(p => (string?)p!["name"] == "Idea Variation Ceasar Version")!.AsObject();
        Assert.False(caesar.ContainsKey("isActive"));
    }

    [Fact]
    public void SetActivePlayset_ErhaeltUnbekannteFelderWieDlcUndModlisten()
    {
        PlaysetStore.SetActivePlayset(_playsetsFilePath, "Initial playset");

        var root = JsonNode.Parse(File.ReadAllText(_playsetsFilePath));
        var playsetsArray = Assert.IsType<JsonArray>(root!["playsets"]);

        var initial = playsetsArray.Single(p => (string?)p!["name"] == "Initial playset")!.AsObject();
        Assert.Equal(2, initial["orderedListMods"]!.AsArray().Count);
        Assert.Equal(1, initial["DLC"]!.AsArray().Count);

        var caesar = playsetsArray.Single(p => (string?)p!["name"] == "Idea Variation Ceasar Version")!.AsObject();
        Assert.Equal(1, caesar["orderedListMods"]!.AsArray().Count);
        Assert.Equal(2, caesar["DLC"]!.AsArray().Count);
    }

    [Fact]
    public void SetActivePlayset_LegtSicherheitskopieMitOriginalinhaltAn()
    {
        var originalContent = File.ReadAllText(_playsetsFilePath);

        PlaysetStore.SetActivePlayset(_playsetsFilePath, "Initial playset");

        var backupPath = _playsetsFilePath + ".pmt-backup";
        Assert.True(File.Exists(backupPath));
        Assert.Equal(originalContent, File.ReadAllText(backupPath));
    }

    [Fact]
    public void SetActivePlayset_UnbekanntesPlayset_LiefertFehlerUndAendertDateiNicht()
    {
        var originalContent = File.ReadAllText(_playsetsFilePath);

        var result = PlaysetStore.SetActivePlayset(_playsetsFilePath, "Playset gibt es nicht");

        Assert.False(result.Success);
        Assert.Equal(originalContent, File.ReadAllText(_playsetsFilePath));
    }

    [Fact]
    public void SetActivePlayset_FehlendeDatei_LiefertFehler()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), "pmt-playsets-nicht-vorhanden-" + Guid.NewGuid() + ".json");

        var result = PlaysetStore.SetActivePlayset(missingPath, "Initial playset");

        Assert.False(result.Success);
    }
}
