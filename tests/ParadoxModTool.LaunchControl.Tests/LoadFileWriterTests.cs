using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

/// <summary>Die Lade-Dateien, die das Spiel beim direkten Start liest (CK III/EU IV dlc_load.json, Vic 3 content_load.json).</summary>
public class LoadFileWriterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-loadfile-" + Guid.NewGuid().ToString("N"));
    private readonly string _modFolder;

    public LoadFileWriterTests()
    {
        _modFolder = Path.Combine(_root, "mod");
        Directory.CreateDirectory(_modFolder);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string AddModFile(string fileName, string folder, string pathStyle = "/")
    {
        var path = folder.Replace("\\", pathStyle);
        File.WriteAllText(Path.Combine(_modFolder, fileName), "version=\"1.0\"\nname=\"x\"\npath=\"" + path + "\"\n");
        return folder;
    }

    private static PlaysetData Playset(params PlaysetModEntry[] mods) => new() { Id = "p", Name = "p", Mods = mods.ToList() };

    private static PlaysetModEntry Mod(string path, bool enabled = true) => new(path, null, enabled, null);

    [Fact]
    public void Dlc_load_matches_what_the_launcher_wrote()
    {
        var one = AddModFile("Flogis RNW Tiles.mod", Path.Combine(_modFolder, "Flogis RNW Tiles"));
        var two = AddModFile("Flogi's RNW Adjustments Whole World Generation Addon.mod", Path.Combine(_modFolder, "Flogi's RNW Adjustments Whole World Generation Addon"));
        var file = Path.Combine(_root, "dlc_load.json");

        var result = LoadFileWriter.Write(file, Playset(Mod(one), Mod(two)), _modFolder);

        Assert.True(result.Success);
        Assert.Equal("{\"enabled_mods\":[\"mod/Flogis RNW Tiles.mod\",\"mod/Flogi's RNW Adjustments Whole World Generation Addon.mod\"],\"disabled_dlcs\":[]}", File.ReadAllText(file));
    }

    [Fact]
    public void Dlc_load_keeps_an_archive_mod()
    {
        // Alte Workshop-Mods von HOI IV: die .mod-Datei nennt nur archive=, der Launcher führt den Ordner des Archivs als dirPath.
        var folder = Path.Combine(_root, "workshop", "699397350");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "no_mans_land.zip"), "zip");
        File.WriteAllText(
            Path.Combine(_modFolder, "ugc_699397350.mod"),
            "name=\"No Mans Land\"\nsupported_version=\"1.5.2\"\narchive=\"" + Path.Combine(folder, "no_mans_land.zip").Replace('\\', '/') + "\"\nremote_file_id=\"699397350\"\n");
        var other = AddModFile("ugc_1.mod", Path.Combine(_root, "workshop", "1"));
        var file = Path.Combine(_root, "dlc_load.json");

        var result = LoadFileWriter.Write(file, Playset(Mod(folder), Mod(other)), _modFolder);

        Assert.True(result.Success);
        Assert.Equal("{\"enabled_mods\":[\"mod/ugc_699397350.mod\",\"mod/ugc_1.mod\"],\"disabled_dlcs\":[]}", File.ReadAllText(file));
    }

    [Fact]
    public void Dlc_load_resolves_a_relative_path_in_the_mod_file_against_the_game_folder()
    {
        var folder = Path.Combine(_modFolder, "Rel");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(_modFolder, "Rel.mod"), "version=\"1.0\"\npath=\"mod/Rel\"\n");
        var file = Path.Combine(_root, "dlc_load.json");

        var result = LoadFileWriter.Write(file, Playset(Mod(folder)), _modFolder);

        Assert.True(result.Success);
        Assert.Equal("{\"enabled_mods\":[\"mod/Rel.mod\"],\"disabled_dlcs\":[]}", File.ReadAllText(file));
    }

    [Fact]
    public void Failure_for_an_enabled_mod_without_folder_names_the_mod_not_an_empty_path()
    {
        var file = Path.Combine(_root, "dlc_load.json");

        var result = LoadFileWriter.Write(file, Playset(new PlaysetModEntry(string.Empty, "Broken Mod", true, "m3")), _modFolder);

        Assert.Equal(PlaysetStatus.LoadFileFailed, result.Status);
        Assert.Equal("Broken Mod", result.Detail);
    }

    [Fact]
    public void Dlc_load_lists_enabled_mods_in_order_and_omits_disabled_ones()
    {
        var a = AddModFile("a.mod", Path.Combine(_modFolder, "a"));
        var b = AddModFile("b.mod", Path.Combine(_modFolder, "b"));
        var c = AddModFile("c.mod", Path.Combine(_modFolder, "c"));
        var file = Path.Combine(_root, "dlc_load.json");

        Assert.True(LoadFileWriter.Write(file, Playset(Mod(c), Mod(b, false), Mod(a)), _modFolder).Success);

        Assert.Equal("{\"enabled_mods\":[\"mod/c.mod\",\"mod/a.mod\"],\"disabled_dlcs\":[]}", File.ReadAllText(file));
    }

    [Fact]
    public void Dlc_load_matches_workshop_mod_by_its_ugc_file_and_backslash_paths()
    {
        var workshop = AddModFile("ugc_2216754325.mod", @"D:\SteamLibrary\steamapps\workshop\content\1158310\2216754325");
        var file = Path.Combine(_root, "dlc_load.json");

        Assert.True(LoadFileWriter.Write(file, Playset(Mod(workshop + "\\")), _modFolder).Success);

        Assert.Equal("{\"enabled_mods\":[\"mod/ugc_2216754325.mod\"],\"disabled_dlcs\":[]}", File.ReadAllText(file));
    }

    [Fact]
    public void Disabled_dlcs_are_listed()
    {
        var a = AddModFile("a.mod", Path.Combine(_modFolder, "a"));
        var playset = Playset(Mod(a));
        playset.Dlcs.Add(new PlaysetDlcEntry("dlc_on", true));
        playset.Dlcs.Add(new PlaysetDlcEntry("dlc_off", false));
        var file = Path.Combine(_root, "dlc_load.json");

        Assert.True(LoadFileWriter.Write(file, playset, _modFolder).Success);

        Assert.Equal("{\"enabled_mods\":[\"mod/a.mod\"],\"disabled_dlcs\":[\"dlc_off\"]}", File.ReadAllText(file));
    }

    [Fact]
    public void Vic3_file_uses_paths_and_keeps_unknown_keys()
    {
        var file = Path.Combine(_root, "content_load.json");
        File.WriteAllText(file, "{\"enabledMods\":[],\"disabledDLC\":[],\"enabledUGC\":[\"x\"],\"futureKey\":1}");
        var folder = Path.Combine(_modFolder, "PMT Order Probe C");
        var playset = Playset(Mod(folder), Mod(Path.Combine(_modFolder, "off"), false));
        playset.Dlcs.Add(new PlaysetDlcEntry("dlc010", false));

        var result = LoadFileWriter.Write(file, playset, _modFolder);

        Assert.True(result.Success);
        var expectedPath = folder.Replace("\\", "\\\\");
        Assert.Equal("{\"enabledMods\":[{\"path\":\"" + expectedPath + "\"}],\"disabledDLC\":[\"dlc010\"],\"enabledUGC\":[\"x\"],\"futureKey\":1}", File.ReadAllText(file));
    }

    [Fact]
    public void Unknown_keys_survive_in_existing_dlc_load_file()
    {
        var a = AddModFile("a.mod", Path.Combine(_modFolder, "a"));
        var file = Path.Combine(_root, "dlc_load.json");
        File.WriteAllText(file, "{\"enabled_mods\":[],\"futureKey\":[1,2],\"disabled_dlcs\":[\"old\"]}");

        Assert.True(LoadFileWriter.Write(file, Playset(Mod(a)), _modFolder).Success);

        Assert.Equal("{\"enabled_mods\":[\"mod/a.mod\"],\"futureKey\":[1,2],\"disabled_dlcs\":[]}", File.ReadAllText(file));
    }

    [Fact]
    public void Mod_without_dot_mod_file_fails_and_names_the_mod_and_keeps_old_file()
    {
        var file = Path.Combine(_root, "dlc_load.json");
        File.WriteAllText(file, "{\"old\":true}");
        var unknown = Path.Combine(_modFolder, "No Descriptor");

        var result = LoadFileWriter.Write(file, Playset(Mod(unknown)), _modFolder);

        Assert.Equal(PlaysetStatus.LoadFileFailed, result.Status);
        Assert.Equal(unknown, result.Detail);
        Assert.Equal("{\"old\":true}", File.ReadAllText(file));
    }

    [Fact]
    public void Unknown_file_name_is_refused()
    {
        var result = LoadFileWriter.Write(Path.Combine(_root, "other.json"), Playset(), _modFolder);

        Assert.Equal(PlaysetStatus.LoadFileFailed, result.Status);
    }

    [Fact]
    public void Write_failure_leaves_old_file_and_removes_temp()
    {
        var file = Path.Combine(_root, "dlc_load.json");
        File.WriteAllText(file, "{\"old\":true}");
        File.SetAttributes(file, FileAttributes.ReadOnly);
        try
        {
            var result = LoadFileWriter.Write(file, Playset(), _modFolder);

            Assert.Equal(PlaysetStatus.LoadFileFailed, result.Status);
            Assert.Equal("{\"old\":true}", File.ReadAllText(file));
            Assert.False(File.Exists(file + ".tmp"));
        }
        finally
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }
}
