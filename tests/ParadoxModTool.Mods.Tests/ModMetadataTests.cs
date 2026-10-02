using System.Text;
using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModMetadataTests : TempDirTests
{
    private const string MetadataFile = ".metadata/metadata.json";

    private string Mod(string name, string json, bool bom = false)
    {
        var folder = Make(name, ".metadata");
        var bytes = (bom ? new byte[] { 0xEF, 0xBB, 0xBF } : Array.Empty<byte>()).Concat(Encoding.UTF8.GetBytes(json)).ToArray();
        File.WriteAllBytes(Path.Combine(folder, "metadata.json"), bytes);
        return Path.Combine(Dir, name);
    }

    [Fact]
    public void Read_LiestNameVersionUndSpielversion_MitBomTabsUndAbweichenderSchreibweise()
    {
        var folder = Mod("m", "{\n\t\"name\":\t\"The Idea Variation 2\",\n\t\"version\":\t\"2.0.5\",\n\t\"supported_game_version\":\t\"1.3.*\",\n\t\"tags\": [\"a\"],\n}", bom: true);

        var metadata = ModMetadata.Read(folder, MetadataFile);

        Assert.NotNull(metadata);
        Assert.Equal("The Idea Variation 2", metadata!.Name);
        Assert.Equal("2.0.5", metadata.Version);
        Assert.Equal("1.3.*", metadata.SupportedGameVersion);
        Assert.Equal(folder, metadata.Folder);
    }

    [Fact]
    public void Read_FehlendeFelder_ErgebenLeereTexte()
    {
        var folder = Mod("m", "{ \"name\": \"X\" }");

        var metadata = ModMetadata.Read(folder, MetadataFile);

        Assert.Equal("", metadata!.Version);
        Assert.Equal("", metadata.SupportedGameVersion);
    }

    [Fact]
    public void Read_ZahlAlsVersion_WirdAlsTextGenommen()
    {
        var metadata = ModMetadata.Read(Mod("m", "{ \"version\": 2 }"), MetadataFile);

        Assert.Equal("2", metadata!.Version);
    }

    [Fact]
    public void Read_ZahlAlsName_ErgibtLeerenText()
    {
        var metadata = ModMetadata.Read(Mod("m", "{ \"name\": 5 }"), MetadataFile);

        Assert.Equal("", metadata!.Name);
    }

    [Fact]
    public void Read_KeineDateiKaputtOderKeinObjekt_LiefertNull()
    {
        Assert.Null(ModMetadata.Read(Make("leer"), MetadataFile));
        Assert.Null(ModMetadata.Read(Mod("kaputt", "{ nicht json"), MetadataFile));
        Assert.Null(ModMetadata.Read(Mod("liste", "[1,2]"), MetadataFile));
        Assert.Null(ModMetadata.Read(Mod("m", "{}"), ""));
    }

    [Fact]
    public void Find_NimmtDenErstenPfadMitMetadaten()
    {
        var without = Make("ohne");
        var with = Mod("mit", "{ \"version\": \"1.0\" }");
        var mod = new ModInfo { Id = "a", Name = "A", Paths = { without, with } };

        var metadata = ModMetadata.Find(mod, MetadataFile);

        Assert.Equal(with, metadata!.Folder);
        Assert.Null(ModMetadata.Find(new ModInfo { Id = "b", Paths = { without } }, MetadataFile));
    }

    [Fact]
    public void Read_KlassischesFormat_LiestFelderAusGeschwisterdatei()
    {
        var folder = Make("m");
        File.WriteAllText(Path.Combine(Dir, "m.mod"),
            "name = \"The Idea Variation 2\"\nversion = \"2.0.5\"\nsupported_version = \"1.3.*\"\npicture = \"thumb.png\"\ntags = {\n\t\"Alternative History\"\n}\n");

        var metadata = ModMetadata.Read(folder, ModMetadata.ClassicSiblingMetadataFile);

        Assert.NotNull(metadata);
        Assert.Equal("The Idea Variation 2", metadata!.Name);
        Assert.Equal("2.0.5", metadata.Version);
        Assert.Equal("1.3.*", metadata.SupportedGameVersion);
        Assert.Equal("thumb.png", metadata.Picture);
        Assert.Equal(folder, metadata.Folder);
    }

    [Fact]
    public void Read_KlassischesFormat_FehlendeGeschwisterdatei_LiefertNull()
    {
        var folder = Make("m");

        Assert.Null(ModMetadata.Read(folder, ModMetadata.ClassicSiblingMetadataFile));
    }

    [Fact]
    public void Read_KlassischesFormat_OhnePassendeFelder_ErgibtLeereTexte()
    {
        var folder = Make("m");
        File.WriteAllText(Path.Combine(Dir, "m.mod"), "path = \"mod/m\"\n");

        var metadata = ModMetadata.Read(folder, ModMetadata.ClassicSiblingMetadataFile);

        Assert.NotNull(metadata);
        Assert.Equal("", metadata!.Name);
        Assert.Equal("", metadata.Version);
    }

    [Fact]
    public void Read_KlassischesFormat_KommentierteZeileWirdNichtAlsFeldGelesen()
    {
        var folder = Make("m");
        File.WriteAllText(Path.Combine(Dir, "m.mod"), "# version = \"9.9.9\"\nname = \"X\"\nversion = \"1.0\"\n");

        var metadata = ModMetadata.Read(folder, ModMetadata.ClassicSiblingMetadataFile);

        Assert.Equal("X", metadata!.Name);
        Assert.Equal("1.0", metadata.Version);
    }

    [Fact]
    public void ReadClassicFile_LiestFelderAusDateiImOrdner()
    {
        var folder = Make("123456789");
        File.WriteAllText(Path.Combine(folder, "descriptor.mod"),
            "name = \"The Idea Variation 2\"\nversion = \"2.0.5\"\nsupported_version = \"1.3.*\"\n");

        var metadata = ModMetadata.ReadClassicFile(folder, ModMetadata.ClassicWorkshopDescriptorFile);

        Assert.NotNull(metadata);
        Assert.Equal("The Idea Variation 2", metadata!.Name);
        Assert.Equal("2.0.5", metadata.Version);
        Assert.Equal("1.3.*", metadata.SupportedGameVersion);
        Assert.Equal(folder, metadata.Folder);
    }

    [Fact]
    public void ReadClassicFile_FehlendeDatei_LiefertNull()
    {
        var folder = Make("123456789");

        Assert.Null(ModMetadata.ReadClassicFile(folder, ModMetadata.ClassicWorkshopDescriptorFile));
    }

    [Fact]
    public void Classic_falls_back_to_descriptor_inside_folder()
    {
        var folder = Make("AGOT Flogis Buildings");
        File.WriteAllText(Path.Combine(folder, "descriptor.mod"), "name=\"X\"\n");
        File.WriteAllText(Path.Combine(Dir, "AGOT Flogis Buildings Mod.mod"), "name=\"Other\"\n");

        Assert.Equal("X", ModMetadata.Read(folder, ModMetadata.ClassicSiblingMetadataFile)!.Name);
    }

    [Fact]
    public void Classic_sibling_wins_over_descriptor()
    {
        var folder = Make("m");
        File.WriteAllText(Path.Combine(folder, "descriptor.mod"), "name=\"D\"\n");
        File.WriteAllText(Path.Combine(Dir, "m.mod"), "name=\"S\"\n");

        Assert.Equal("S", ModMetadata.Read(folder, ModMetadata.ClassicSiblingMetadataFile)!.Name);
    }

    [Fact]
    public void Classic_reads_all_replace_path_lines()
    {
        var folder = Make("m");
        File.WriteAllText(Path.Combine(Dir, "m.mod"),
            "name=\"M\"\nreplace_path=\"common/religion/religion_types\"\nreplace_path = \"history/characters/\"\n# replace_path=\"x\"\n");

        var metadata = ModMetadata.Read(folder, ModMetadata.ClassicSiblingMetadataFile)!;

        Assert.Equal(new[] { "common/religion/religion_types", "history/characters" }, metadata.ReplacePaths);
    }

    [Fact]
    public void Json_reads_replace_paths_from_game_custom_data()
    {
        var folder = Mod("m", "{\"name\":\"n\",\"game_custom_data\":{\"replace_paths\":[\"events\",\"common/casus_belli\"]}}");

        Assert.Equal(new[] { "events", "common/casus_belli" }, ModMetadata.Read(folder, MetadataFile)!.ReplacePaths);
    }

    [Fact]
    public void Json_without_game_custom_data_has_no_replace_paths()
    {
        var folder = Mod("m", "{\"name\":\"n\"}");

        Assert.Empty(ModMetadata.Read(folder, MetadataFile)!.ReplacePaths);
    }
}
