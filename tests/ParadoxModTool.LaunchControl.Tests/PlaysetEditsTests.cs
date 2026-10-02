using System.Text.Json.Nodes;
using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class PlaysetEditsTests : IDisposable
{
    private const string FixtureJson =
        "{\n" +
        "  \"file_version\": \"1.0.0\",\n" +
        "  \"playsets\": [\n" +
        "    {\n" +
        "      \"name\": \"Main\",\n" +
        "      \"isActive\": true,\n" +
        "      \"isAutomaticallySorted\": true,\n" +
        "      \"orderedListMods\": [\n" +
        "        { \"path\": \"C:/mods/framework/\", \"isEnabled\": true },\n" +
        "        { \"path\": \"C:/mods/own/\", \"isEnabled\": true },\n" +
        "        { \"path\": \"c:/MODS/cmf_off\", \"isEnabled\": false },\n" +
        "        { \"path\": \"C:/mods/own2/\", \"isEnabled\": true }\n" +
        "      ],\n" +
        "      \"DLC\": [ { \"paradoxAppId\": \"d000_shared\", \"isEnabled\": true } ]\n" +
        "    },\n" +
        "    {\n" +
        "      \"name\": \"Other\",\n" +
        "      \"orderedListMods\": [ { \"path\": \"C:/mods/x/\", \"isEnabled\": true } ]\n" +
        "    }\n" +
        "  ]\n" +
        "}";

    private static readonly string[] OwnMods = { @"C:\mods\own", @"C:\mods\own2" };

    private readonly string _path = Path.Combine(Path.GetTempPath(), "pmt-playset-edit-" + Guid.NewGuid() + ".json");

    public PlaysetEditsTests() => File.WriteAllText(_path, FixtureJson);

    public void Dispose()
    {
        foreach (var file in new[] { _path, _path + ".pmt-backup", _path + ".tmp" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void AddsBeforeTheFirstOwnModAndKeepsEverythingElse()
    {
        var result = PlaysetEdits.AddMod(_path, "Main", @"C:\mods\cmf", OwnMods, () => false);

        Assert.Equal(PlaysetEditStatus.Added, result.Status);
        Assert.True(result.Changed);
        var root = JsonNode.Parse(File.ReadAllText(_path))!;
        var main = root["playsets"]![0]!;
        Assert.Equal(
            new[] { "C:/mods/framework/", "C:/mods/cmf/", "C:/mods/own/", "c:/MODS/cmf_off", "C:/mods/own2/" },
            main["orderedListMods"]!.AsArray().Select(m => m!["path"]!.GetValue<string>()));
        Assert.True(main["orderedListMods"]![1]!["isEnabled"]!.GetValue<bool>());
        Assert.Equal("1.0.0", root["file_version"]!.GetValue<string>());
        Assert.True(main["isAutomaticallySorted"]!.GetValue<bool>());
        Assert.Equal("d000_shared", main["DLC"]![0]!["paradoxAppId"]!.GetValue<string>());
        Assert.Equal(FixtureJson, File.ReadAllText(_path + ".pmt-backup"));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public void AppendsWhenNoOwnModIsInThePlayset()
    {
        var result = PlaysetEdits.AddMod(_path, "Other", @"C:\mods\cmf", OwnMods, () => false);

        Assert.Equal(PlaysetEditStatus.Added, result.Status);
        var other = JsonNode.Parse(File.ReadAllText(_path))!["playsets"]![1]!;
        Assert.Equal(new[] { "C:/mods/x/", "C:/mods/cmf/" }, other["orderedListMods"]!.AsArray().Select(m => m!["path"]!.GetValue<string>()));
    }

    [Fact]
    public void EnablesADisabledEntryInPlace()
    {
        var result = PlaysetEdits.AddMod(_path, "Main", @"C:\mods\CMF_OFF\", OwnMods, () => false);

        Assert.Equal(PlaysetEditStatus.Enabled, result.Status);
        var mods = JsonNode.Parse(File.ReadAllText(_path))!["playsets"]![0]!["orderedListMods"]!.AsArray();
        Assert.Equal(4, mods.Count);
        Assert.True(mods[2]!["isEnabled"]!.GetValue<bool>());
        Assert.Equal("c:/MODS/cmf_off", mods[2]!["path"]!.GetValue<string>());
    }

    [Fact]
    public void AlreadyPresentChangesNothing()
    {
        var result = PlaysetEdits.AddMod(_path, "Main", "C:/mods/framework", OwnMods, () => false);

        Assert.Equal(PlaysetEditStatus.AlreadyPresent, result.Status);
        Assert.False(result.Changed);
        Assert.Equal(FixtureJson, File.ReadAllText(_path));
        Assert.False(File.Exists(_path + ".pmt-backup"));
    }

    [Fact]
    public void GameRunningWritesNothing()
    {
        var before = File.ReadAllBytes(_path);

        var result = PlaysetEdits.AddMod(_path, "Main", @"C:\mods\cmf", OwnMods, () => true);

        Assert.Equal(PlaysetEditStatus.GameRunning, result.Status);
        Assert.Equal(before, File.ReadAllBytes(_path));
        Assert.False(File.Exists(_path + ".pmt-backup"));
    }

    [Fact]
    public void MissingFileOrPlaysetIsReported()
    {
        Assert.Equal(PlaysetEditStatus.FileMissing, PlaysetEdits.AddMod(_path + ".fehlt", "Main", @"C:\mods\cmf", OwnMods, () => false).Status);
        Assert.Equal(PlaysetEditStatus.FileMissing, PlaysetEdits.AddMod(null, "Main", @"C:\mods\cmf", OwnMods, () => false).Status);
        Assert.Equal(PlaysetEditStatus.PlaysetMissing, PlaysetEdits.AddMod(_path, "Gibt es nicht", @"C:\mods\cmf", OwnMods, () => false).Status);
        Assert.Equal(FixtureJson, File.ReadAllText(_path));
    }

    [Fact]
    public void GameProcessFindsARunningExecutable()
    {
        Assert.True(GameProcess.IsRunning(Environment.ProcessPath));
        Assert.False(GameProcess.IsRunning(@"C:\nirgends\pmt_kein_laufendes_spiel_4711.exe"));
        Assert.False(GameProcess.IsRunning(null));
    }
}
