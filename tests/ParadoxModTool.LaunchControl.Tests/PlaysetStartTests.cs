using System.Text.Json.Nodes;
using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

/// <summary>Der Start aus der Werkzeugleiste geht jetzt über die Verwaltung; für EU V muss das Ergebnis dem bisherigen Weg entsprechen.</summary>
public class PlaysetStartTests : IDisposable
{
    private const string Fixture = "{\"file_version\":\"1.0.0\",\"playsets\":[{\"name\":\"Main\",\"isActive\":true,\"orderedListMods\":[{\"path\":\"C:/mods/a/\",\"isEnabled\":true}],\"DLC\":[{\"paradoxAppId\":\"d000_shared\",\"isEnabled\":true}]},{\"name\":\"Other\",\"someField\":1}]}";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-start-" + Guid.NewGuid().ToString("N"));

    public PlaysetStartTests() => Directory.CreateDirectory(Path.Combine(_root, "old"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void PrepareStart_on_json_equals_the_old_SetActivePlayset_result()
    {
        var oldFile = Path.Combine(_root, "old", "playsets.json");
        File.WriteAllText(oldFile, Fixture);
        var newFolder = Path.Combine(_root, "new");
        Directory.CreateDirectory(newFolder);
        var newFile = Path.Combine(newFolder, "playsets.json");
        File.WriteAllText(newFile, Fixture);

        Assert.True(PlaysetStore.SetActivePlayset(oldFile, "Other").Success);
        var manager = PlaysetManagerFactory.Create(newFile, null, null)!;
        Assert.True(manager.PrepareStart("Other").Success);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(oldFile)), JsonNode.Parse(File.ReadAllText(newFile))));
        Assert.Single(manager.BackupSets());
    }

    [Fact]
    public void Launcher_game_without_load_file_has_no_start_and_manager_says_so()
    {
        var sqlite = Path.Combine(_root, "launcher-v2.sqlite");
        File.WriteAllText(sqlite, "x");

        var manager = PlaysetManagerFactory.Create(sqlite, null, null)!;

        Assert.True(manager.UsesLauncherDatabase);
        Assert.Null(manager.LoadFilePath);
    }
}
