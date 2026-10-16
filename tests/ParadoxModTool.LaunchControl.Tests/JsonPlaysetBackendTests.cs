using System.Text.Json.Nodes;
using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class JsonPlaysetBackendTests : IDisposable
{
    private const string FixtureJson =
        "{\n" +
        "\t\"file_version\":\t\"1.0.0\",\n" +
        "\t\"someFutureRootField\":\t7,\n" +
        "\t\"playsets\":\t[{\n" +
        "\t\t\t\"name\":\t\"Main\",\n" +
        "\t\t\t\"isActive\":\ttrue,\n" +
        "\t\t\t\"isAutomaticallySorted\":\ttrue,\n" +
        "\t\t\t\"someFutureField\":\t\"keep me\",\n" +
        "\t\t\t\"orderedListMods\":\t[{\n" +
        "\t\t\t\t\t\"path\":\t\"C:/mods/a/\",\n" +
        "\t\t\t\t\t\"isEnabled\":\ttrue,\n" +
        "\t\t\t\t\t\"modExtra\":\t\"keep too\"\n" +
        "\t\t\t\t}, {\n" +
        "\t\t\t\t\t\"path\":\t\"C:/mods/b/\",\n" +
        "\t\t\t\t\t\"isEnabled\":\tfalse\n" +
        "\t\t\t\t}],\n" +
        "\t\t\t\"DLC\":\t[{\n" +
        "\t\t\t\t\t\"paradoxAppId\":\t\"d000_shared\",\n" +
        "\t\t\t\t\t\"isEnabled\":\ttrue\n" +
        "\t\t\t\t}, {\n" +
        "\t\t\t\t\t\"paradoxAppId\":\t\"d017_sacred_sites_pack\",\n" +
        "\t\t\t\t\t\"isEnabled\":\tfalse\n" +
        "\t\t\t\t}]\n" +
        "\t\t}, {\n" +
        "\t\t\t\"name\":\t\"Other\",\n" +
        "\t\t\t\"isAutomaticallySorted\":\tfalse,\n" +
        "\t\t\t\"orderedListMods\":\t[{\n" +
        "\t\t\t\t\t\"path\":\t\"C:/mods/x/\",\n" +
        "\t\t\t\t\t\"isEnabled\":\ttrue\n" +
        "\t\t\t\t}]\n" +
        "\t\t}]\n" +
        "}";

    private readonly string _path = Path.Combine(Path.GetTempPath(), "pmt-json-backend-" + Guid.NewGuid() + ".json");

    public JsonPlaysetBackendTests() => File.WriteAllText(_path, FixtureJson);

    public void Dispose()
    {
        foreach (var file in new[] { _path, _path + ".tmp" })
        {
            if (File.Exists(file))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
        }
    }

    private JsonPlaysetBackend Backend() => new(_path);

    private JsonObject Root() => JsonNode.Parse(File.ReadAllText(_path))!.AsObject();

    private JsonObject PlaysetNode(string name) =>
        Root()["playsets"]!.AsArray().OfType<JsonObject>().First(p => p["name"]!.GetValue<string>() == name);

    [Fact]
    public void LoadAll_reads_mods_dlcs_and_flags()
    {
        var all = Backend().LoadAll();

        var main = Assert.Single(all, p => p.Name == "Main");
        Assert.Equal("Main", main.Id);
        Assert.True(main.IsActive);
        Assert.True(main.IsAutomaticallySorted);
        Assert.Equal(new[] { "C:/mods/a/", "C:/mods/b/" }, main.Mods.Select(m => m.Path));
        Assert.Equal(new[] { true, false }, main.Mods.Select(m => m.Enabled));
        Assert.Equal(new[] { "d000_shared", "d017_sacred_sites_pack" }, main.Dlcs.Select(d => d.Id));
        Assert.False(all.Single(p => p.Name == "Other").IsActive);
    }

    [Fact]
    public void LoadAll_names_mods_from_their_metadata_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pmt-meta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, ".metadata"));
        File.WriteAllText(Path.Combine(folder, ".metadata", "metadata.json"), "\uFEFF{\"name\":\"Community Mod Framework\"}");
        try
        {
            File.WriteAllText(_path, "{\"playsets\":[{\"name\":\"P\",\"orderedListMods\":[{\"path\":\"" + folder.Replace("\\", "/") + "/\",\"isEnabled\":true},{\"path\":\"C:/missing/123/\",\"isEnabled\":true}]}]}");

            var mods = Backend().LoadAll().Single().Mods;

            Assert.Equal("Community Mod Framework", mods[0].DisplayName);
            Assert.Null(mods[1].DisplayName);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Save_does_not_write_display_names_into_the_file()
    {
        var main = Backend().LoadAll().Single(p => p.Name == "Main");
        main.Mods[0] = main.Mods[0] with { DisplayName = "Pretty" };

        Assert.True(Backend().Save(main).Success);

        Assert.DoesNotContain("Pretty", File.ReadAllText(_path));
    }

    [Fact]
    public void Save_new_playset_appends_and_keeps_unknown_fields()
    {
        var playset = new PlaysetData
        {
            Id = "Neu",
            Name = "Neu",
            Mods = { new PlaysetModEntry("C:/mods/n/", null, true, null) },
            Dlcs = { new PlaysetDlcEntry("d000_shared", true) },
        };

        var result = Backend().Save(playset);

        Assert.True(result.Success);
        Assert.Equal(7, Root()["someFutureRootField"]!.GetValue<int>());
        Assert.Equal("keep me", PlaysetNode("Main")["someFutureField"]!.GetValue<string>());
        var added = PlaysetNode("Neu");
        Assert.Equal("C:/mods/n/", added["orderedListMods"]![0]!["path"]!.GetValue<string>());
        Assert.True(added["orderedListMods"]![0]!["isEnabled"]!.GetValue<bool>());
        Assert.Equal("d000_shared", added["DLC"]![0]!["paradoxAppId"]!.GetValue<string>());
        Assert.Equal(3, Root()["playsets"]!.AsArray().Count);
    }

    [Fact]
    public void Save_existing_replaces_mods_and_dlcs_only()
    {
        var main = Backend().LoadAll().Single(p => p.Name == "Main");
        main.Mods[1] = main.Mods[1] with { Enabled = true };
        main.Dlcs[1] = new PlaysetDlcEntry("d017_sacred_sites_pack", true);

        var result = Backend().Save(main);

        Assert.True(result.Success);
        var node = PlaysetNode("Main");
        Assert.Equal("keep me", node["someFutureField"]!.GetValue<string>());
        Assert.Equal("keep too", node["orderedListMods"]![0]!["modExtra"]!.GetValue<string>());
        Assert.True(node["orderedListMods"]![1]!["isEnabled"]!.GetValue<bool>());
        Assert.True(node["DLC"]![1]!["isEnabled"]!.GetValue<bool>());
        Assert.True(node["isActive"]!.GetValue<bool>());
    }

    [Fact]
    public void Save_can_rename()
    {
        var main = Backend().LoadAll().Single(p => p.Name == "Main");
        main.Name = "Umbenannt";

        Assert.True(Backend().Save(main).Success);

        Assert.DoesNotContain(Backend().LoadAll(), p => p.Name == "Main");
        Assert.Contains(Backend().LoadAll(), p => p.Name == "Umbenannt");
    }

    [Fact]
    public void Reorder_sets_isAutomaticallySorted_false()
    {
        var main = Backend().LoadAll().Single(p => p.Name == "Main");
        main.Mods.Reverse();

        Assert.True(Backend().Save(main).Success);

        Assert.False(PlaysetNode("Main")["isAutomaticallySorted"]!.GetValue<bool>());
    }

    [Fact]
    public void Adding_a_mod_at_the_end_keeps_isAutomaticallySorted()
    {
        var main = Backend().LoadAll().Single(p => p.Name == "Main");
        main.Mods.Add(new PlaysetModEntry("C:/mods/z/", null, true, null));

        Assert.True(Backend().Save(main).Success);

        Assert.True(PlaysetNode("Main")["isAutomaticallySorted"]!.GetValue<bool>());
    }

    [Fact]
    public void Delete_removes_only_that_playset()
    {
        Assert.True(Backend().Delete("Other").Success);

        var names = Backend().LoadAll().Select(p => p.Name).ToList();
        Assert.Equal(new[] { "Main" }, names);
    }

    [Fact]
    public void Delete_unknown_playset_is_reported()
    {
        Assert.Equal(PlaysetStatus.PlaysetMissing, Backend().Delete("Nope").Status);
    }

    [Fact]
    public void SetActive_moves_isActive_like_PlaysetStore()
    {
        Assert.True(Backend().SetActive("Other").Success);

        Assert.True(PlaysetNode("Other")["isActive"]!.GetValue<bool>());
        Assert.Null(PlaysetNode("Main")["isActive"]);
    }

    [Fact]
    public void Names_with_quote_and_umlaut_roundtrip()
    {
        var playset = new PlaysetData { Id = "x", Name = "Mein \"Test\" Ärger" };

        Assert.True(Backend().Save(playset).Success);

        Assert.Contains(Backend().LoadAll(), p => p.Name == "Mein \"Test\" Ärger");
    }

    [Fact]
    public void Write_failure_leaves_original_intact()
    {
        var before = File.ReadAllText(_path);
        File.SetAttributes(_path, FileAttributes.ReadOnly);

        var result = Backend().Save(new PlaysetData { Id = "Neu", Name = "Neu" });

        Assert.Equal(PlaysetStatus.WriteFailed, result.Status);
        Assert.Equal(before, File.ReadAllText(_path));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public void Invalid_json_is_reported_and_not_touched()
    {
        File.WriteAllText(_path, "{ not json");

        var result = Backend().Save(new PlaysetData { Id = "Neu", Name = "Neu" });

        Assert.Equal(PlaysetStatus.InvalidFile, result.Status);
        Assert.Equal("{ not json", File.ReadAllText(_path));
    }

    [Fact]
    public void DefaultDlcs_are_the_first_playsets_dlcs_all_enabled()
    {
        var dlcs = Backend().DefaultDlcs();

        Assert.Equal(new[] { "d000_shared", "d017_sacred_sites_pack" }, dlcs.Select(d => d.Id));
        Assert.All(dlcs, d => Assert.True(d.Enabled));
    }
}
