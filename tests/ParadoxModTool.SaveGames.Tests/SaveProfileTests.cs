using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.SaveGames.Tests;

public class SaveProfileTests
{
    [Fact]
    public void Eu5ProfileDescribesTextSavesObjectKindsAndValueTypes()
    {
        var saves = Profiles.Eu5.SaveGames!;

        Assert.Equal("save games", saves.Folder);
        Assert.Equal(".eu5", saves.Extension);
        Assert.Equal(("SAV", 5, "00"), (saves.TextMarker.Prefix, saves.TextMarker.Offset, saves.TextMarker.Value));
        Assert.Equal(
            new[]
            {
                "countries/database/*", "character_db/database/*", "provinces/database/*", "locations/locations/*",
                "situation_manager/*", "international_organization_manager/database/*", "event_manager/triggered_event/scope",
            },
            saves.ObjectKinds.Select(k => k.Path));
        Assert.Equal(("table", "countries/tags"), (saves.ObjectKinds[0].NameSource, saves.ObjectKinds[0].NameTable));
        Assert.Equal(("field", "first_name"), (saves.ObjectKinds[1].NameSource, saves.ObjectKinds[1].NameField));
        Assert.Equal(("country", "countries/tags", "character:{0}"), (saves.ObjectKinds[1].TagField, saves.ObjectKinds[1].TagTable, saves.ObjectKinds[1].ScriptReference));
        Assert.Null(saves.ObjectKinds[0].TagField);
        Assert.Equal(("fixedPoint", 100000L), (saves.ValueTypes["value"].Format, saves.ValueTypes["value"].Factor));
        Assert.Equal(("tickDate", 24, -5000), (saves.ValueTypes["date"].Format, saves.ValueTypes["date"].TicksPerDay, saves.ValueTypes["date"].EpochYear));
        Assert.Equal(("objectName", "countries/database"), (saves.ValueTypes["ctry"].Format, saves.ValueTypes["ctry"].ObjectPath));
        Assert.Equal(("objectName", "character_db/database"), (saves.ValueTypes["char"].Format, saves.ValueTypes["char"].ObjectPath));
        Assert.Equal("yesNo", saves.ValueTypes["boolean"].Format);
        Assert.Equal("flagName", saves.ValueTypes["flag"].Format);
    }

    [Fact]
    public void Ck3_profile_save_section()
    {
        var saves = Profiles.Ck3.SaveGames!;

        Assert.Equal(".ck3", saves.Extension);
        Assert.Equal(("meta_data", "meta_date"), (saves.MetadataBlock, saves.DateField));
        Assert.Equal(("SAV", 5, "00", "02"), (saves.TextMarker.Prefix, saves.TextMarker.Offset, saves.TextMarker.Value, saves.TextMarker.CompressedValue));
        Assert.Equal(12, saves.ObjectKinds.Count);
        Assert.Equal(("living/*", "alive_data", "character:{0}"), (saves.ObjectKinds[0].Path, saves.ObjectKinds[0].VariablesPath, saves.ObjectKinds[0].ScriptReference));
        Assert.Equal(100000L, saves.ValueTypes["value"].Factor);
        Assert.Equal(("objectName", "landed_titles/landed_titles"), (saves.ValueTypes["lt"].Format, saves.ValueTypes["lt"].ObjectPath));
        var german = ParadoxModTool.Localization.UiTextTable.Read(RepoFiles.PathOf("locales/de.json"));
        Assert.All(saves.ObjectKinds, k => Assert.True(german.ContainsKey(k.LabelKey), k.LabelKey));
    }

    [Fact]
    public void ProfileWithoutSaveGamesBlockHasNoSettings() =>
        Assert.Null(GameProfileLoader.LoadFromJson("""{ "id": "x" }""").SaveGames);
}

/// <summary>Das echte EU-V-Profil aus dem Repository (in den Testordner kopiert).</summary>
internal static class Profiles
{
    public static GameProfile Eu5 { get; } = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "eu5.json"));

    public static GameProfile Ck3 { get; } = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "ck3.json"));
}
