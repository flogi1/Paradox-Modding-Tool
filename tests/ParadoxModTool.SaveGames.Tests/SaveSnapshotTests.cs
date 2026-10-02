using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.SaveGames.Tests;

public class SaveSnapshotTests
{
    private static string Var(string name, string type = "value", string value = "identity=1") =>
        $"variables={{ data={{ {{ flag={name} data={{ type={type} {value} }} }} }} }}";

    internal static SaveSnapshot Load(string body, SaveGameSettings? settings = null) =>
        SaveSnapshot.Load(SaveText.Write("metadata={ date=1363.11.17 }\n" + body), settings ?? Profiles.Eu5.SaveGames!, null, default);

    [Fact]
    public void OwnersResolveToProfileKindsWithDisplayNames()
    {
        var snapshot = Load(
            $"{Var("f")}\ncountries={{ tags={{ 3=SWE }} database={{ 3={{ {Var("a")} }} }} }}\n" +
            $"character_db={{ database={{ 0={{ first_name=\"name_birger\" {Var("b")} }} }} }}\n" +
            $"situation_manager={{ black_death={{ {Var("c")} }} }}\nprovinces={{ database={{ 73={{ {Var("d")} }} }} }}\n" +
            $"expedition_manager={{ database={{ 5={{ {Var("e")} }} }} }}\n");
        var objects = snapshot.Objects;

        Assert.Equal(("Länder", "SWE", "3"), Describe(objects.Resolve("countries/database/3")));
        Assert.Equal(("Charaktere", "name_birger (0)", "0"), Describe(objects.Resolve("character_db/database/0")));
        Assert.Equal(("Situationen", "black_death", (string?)null), Describe(objects.Resolve("situation_manager/black_death")));
        Assert.Equal(("Provinzen", "73", "73"), Describe(objects.Resolve("provinces/database/73")));
        Assert.Equal(("Sonstige", "expedition_manager/database/5", "5"), Describe(objects.Resolve("expedition_manager/database/5")));
        Assert.Equal(("Global", "Global", (string?)null), Describe(objects.Resolve(string.Empty)));

        var order = snapshot.ObjectsWithVariables.Select(o => o.Kind.Label).ToList();
        Assert.Equal("Global", order[0]);
        Assert.Equal("Sonstige", order[^1]);
        Assert.Equal(6, order.Count);
    }

    [Fact]
    public void CharactersAndCountriesKnowTheirCountryTagAndScriptReference()
    {
        var snapshot = Load(
            $"countries={{ tags={{ 3=SWE 4=DAN }} database={{ 3={{ {Var("a")} }} }} }}\n" +
            $"character_db={{ database={{ 0={{ first_name=\"name_birger\" country=3 {Var("b")} }} 1={{ country=99 {Var("c")} }} }} }}\n" +
            $"provinces={{ database={{ 73={{ {Var("d")} }} }} }}\n");
        var objects = snapshot.Objects;

        Assert.Equal(("SWE", "character:0"), (objects.Resolve("character_db/database/0").Tag, objects.Resolve("character_db/database/0").ScriptReference));
        Assert.Equal("99", objects.Resolve("character_db/database/1").Tag);
        Assert.Equal(("SWE", (string?)null), (objects.Resolve("countries/database/3").Tag, objects.Resolve("countries/database/3").ScriptReference));
        Assert.Null(objects.Resolve("provinces/database/73").Tag);
        Assert.Null(objects.Resolve(string.Empty).Tag);
    }

    [Fact]
    public void SecondProfileWithOtherSectionsNeedsNoCodeChange()
    {
        var settings = new SaveGameSettings
        {
            Extension = ".sav",
            TextMarker = new SaveTextMarker { Prefix = "SAV", Offset = 5, Value = "00" },
            ObjectKinds = { new SaveObjectKindSetting { Path = "realm/*", Label = "Reiche", NameSource = "field", NameField = "title" } },
        };

        var snapshot = Load($"realm={{ r1={{ title=\"Kaiserreich\" {Var("z", "value", "identity=5")} }} }}\n", settings);

        Assert.Equal(("Reiche", "Kaiserreich (r1)", "r1"), Describe(snapshot.Objects.Resolve("realm/r1")));
        Assert.Equal(new[] { "Global", "Reiche", "Sonstige" }, snapshot.Objects.Kinds.Select(k => k.Label));
        Assert.Null(snapshot.Objects.Resolve("realm/r1").Tag);
        Assert.Null(snapshot.Objects.Resolve("realm/r1").ScriptReference);
    }

    [Fact]
    public void NamesAreIndexedByKindWithObjectCounts()
    {
        var snapshot = Load(
            "countries={ tags={ 1=AAA 2=BBB } database={\n" +
            $"1={{ {Var("iv2_x")} }}\n" +
            "2={ variables={ data={ { flag=iv2_x data={ type=value identity=2 } } { flag=army data={ type=flag flag=\"iv2_army_12\" } } }\n" +
            "\tlist={ { name=\"pope_countries\" item={ type=ctry identity=1 } } } } }\n} }\n");

        Assert.Contains(new SaveName("iv2_x", SaveNameKind.Variable, 2), snapshot.Names);
        Assert.Contains(new SaveName("pope_countries", SaveNameKind.List, 1), snapshot.Names);
        Assert.Contains(new SaveName("iv2_army_12", SaveNameKind.FlagValue, 1), snapshot.Names);
        Assert.Contains(new SaveName("army", SaveNameKind.Variable, 1), snapshot.Names);
        Assert.Equal("army", Assert.Single(snapshot.EntriesFor("iv2_army_12", SaveNameKind.FlagValue)).Name);
        Assert.Equal(2, snapshot.EntriesFor("iv2_x", SaveNameKind.Variable).Count);
        Assert.True(snapshot.Contains("iv2_army_12"));
        Assert.True(snapshot.Contains("pope_countries"));
        Assert.False(snapshot.Contains("nope"));
        Assert.Equal(snapshot.Names.Select(n => n.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase), snapshot.Names.Select(n => n.Name));
    }

    [Fact]
    public void EntriesOfReturnsAllVariablesOfOneObject()
    {
        var snapshot = Load($"countries={{ database={{ 1={{ variables={{ data={{ {{ flag=a data={{ type=value identity=1 }} }} {{ flag=b data={{ type=value identity=2 }} }} }} }} }} 2={{ {Var("c")} }} }} }}\n");

        Assert.Equal(new[] { "a", "b" }, snapshot.EntriesOf("countries/database/1").Select(e => e.Name));
        Assert.Empty(snapshot.EntriesOf("countries/database/9"));
        Assert.Equal("1363.11.17", snapshot.File.GameDate);
        Assert.Equal(SaveReadStatus.Complete, snapshot.Status);
    }

    [Fact]
    public void LoadRefusesUnreadableSaves() =>
        Assert.Throws<InvalidOperationException>(() =>
            SaveSnapshot.Load(SaveText.Write("PK", SaveText.CompressedHeader), Profiles.Eu5.SaveGames!, null, default));

    private static (string Kind, string Name, string? Id) Describe(SaveObjectRef reference) => (reference.Kind.Label, reference.DisplayName, reference.Id);
}
