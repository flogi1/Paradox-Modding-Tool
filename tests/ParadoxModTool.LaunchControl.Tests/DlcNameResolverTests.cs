using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

/// <summary>Anzeigenamen der DLCs aus den Spieldateien (.dlc, .dlc.json, EU V über die Sprachdatei); sonst ein aus der Kennung gebauter Name.</summary>
public class DlcNameResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-dlcnames-" + Guid.NewGuid().ToString("N"));

    public DlcNameResolverTests() => Directory.CreateDirectory(Path.Combine(_root, "binaries"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Exe(string relative) => Path.Combine(_root, relative);

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void Dlc_file_name_is_used_for_eu4_and_matched_through_the_launcher_prefix()
    {
        Write("dlc/dlc010_conquest_of_paradise/dlc010.dlc", "name = \"Conquest of Paradise\"\narchive = \"x\"\n");

        var names = DlcNameResolver.Load(Exe("eu4.exe"));

        Assert.Equal("Conquest of Paradise", names.Name("eu4_dlc010_conquest_of_paradise"));
        Assert.Equal("Conquest of Paradise", names.Name("dlc010_conquest_of_paradise"));
    }

    [Fact]
    public void Dlc_file_in_game_dlc_folder_is_found_from_the_binaries_folder_for_ck3()
    {
        Write("game/dlc/dlc004_ep1/dlc004.dlc", "name = \"Fate of Iberia\"\n");

        var names = DlcNameResolver.Load(Exe("binaries/ck3.exe"));

        Assert.Equal("Fate of Iberia", names.Name("ck3_dlc004_ep1"));
    }

    [Fact]
    public void Vic3_json_uses_the_english_display_name()
    {
        Write("game/dlc/dlc002_american_buildings/dlc002_american_buildings.dlc.json", "{\"id\":{\"paradox\":\"dlc002_american_buildings\"},\"displayName\":{\"en\":\"American Buildings Pack\"}}");

        var names = DlcNameResolver.Load(Exe("binaries/victoria3.exe"));

        Assert.Equal("American Buildings Pack", names.Name("dlc002_american_buildings"));
    }

    [Fact]
    public void Eu5_json_resolves_the_localizable_name_through_the_english_localization()
    {
        Write("game/dlc/D017_sacred_sites_pack/D017_sacred_sites_pack.dlc.json", "﻿{\"name\":\"d017_sacred_sites_pack\",\"localizable_name\":\"sacred_sites_pack_key\"}");
        Write("game/dlc/D000_shared/main_menu/localization/dlc/english/dlc_localizations_l_english.yml", "﻿l_english:\n sacred_sites_pack_key: \"#italic Sacred Sites#!\"\n other_key: \"x\"\n");

        var names = DlcNameResolver.Load(Exe("binaries/eu5.exe"));

        Assert.Equal("Sacred Sites", names.Name("d017_sacred_sites_pack"));
    }

    [Fact]
    public void Unknown_id_gets_a_readable_name_without_numbers_and_prefixes()
    {
        var names = DlcNameResolver.Load(Exe("binaries/eu5.exe"));

        Assert.Equal("Fate Of The Phoenix", names.Name("d008_fate_of_the_phoenix"));
        Assert.Equal("Shared", names.Name("d000_shared"));
        Assert.Equal("Songs Of Yuletide", names.Name("eu4_dlc014_songs_of_yuletide"));
    }

    [Fact]
    public void Missing_game_folder_still_gives_fallback_names()
    {
        Assert.Equal("Shared", DlcNameResolver.Load(null).Name("d000_shared"));
        Assert.Equal("Shared", DlcNameResolver.Load(Exe("nowhere/eu5.exe")).Name("d000_shared"));
    }
}
