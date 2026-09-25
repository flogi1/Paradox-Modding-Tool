using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class PlaysetModsTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "pmt-playsets-" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        if (File.Exists(_file))
        {
            File.Delete(_file);
        }
    }

    private const string Json = """
    {
      "playsets": [
        { "name": "A", "orderedListMods": [
            { "path": "C:/steam/1/", "isEnabled": true },
            { "path": "C:/steam/2/", "isEnabled": false },
            { "isEnabled": true } ] },
        { "name": "B", "orderedListMods": [ { "path": "C:/steam/3/", "isEnabled": true } ] }
      ]
    }
    """;

    [Fact]
    public void Lists_mods_in_order_with_enabled_flag_and_skips_entries_without_path()
    {
        File.WriteAllText(_file, Json);
        var mods = PlaysetStore.ListPlaysetMods(_file, "A");
        Assert.Equal(new[] { ("C:/steam/1/", true), ("C:/steam/2/", false) }, mods.Select(m => (m.Path, m.IsEnabled)));
    }

    [Fact]
    public void Unknown_playset_missing_file_and_broken_json_give_empty_list()
    {
        File.WriteAllText(_file, Json);
        Assert.Empty(PlaysetStore.ListPlaysetMods(_file, "Z"));
        Assert.Empty(PlaysetStore.ListPlaysetMods(_file + ".fehlt", "A"));
        Assert.Empty(PlaysetStore.ListPlaysetMods(null, "A"));
        File.WriteAllText(_file, "{ kaputt");
        Assert.Empty(PlaysetStore.ListPlaysetMods(_file, "A"));
    }
}
