using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.SaveGames.Tests;

/// <summary>Crusader Kings III: Die Variablen eines Charakters liegen unter <c>living/&lt;id&gt;/alive_data</c>, sein Name unter <c>living/&lt;id&gt;/first_name</c>.</summary>
public class SaveObjectNamesTests
{
    private static SaveObjectNames Names() => new(
        new SaveGameSettings
        {
            ObjectKinds =
            {
                new SaveObjectKindSetting
                {
                    Path = "living/*",
                    Label = "Charaktere",
                    NameSource = "field",
                    NameField = "first_name",
                    VariablesPath = "alive_data",
                    ScriptReference = "character:{0}",
                },
            },
        },
        new Dictionary<string, string> { ["living/39688/first_name"] = "Donnchad" });

    [Fact]
    public void Owner_below_object_resolves_to_object()
    {
        var resolved = Names().Resolve("living/39688/alive_data");

        Assert.Equal(1, resolved.Kind.Order);
        Assert.Equal("39688", resolved.Id);
        Assert.Equal("Donnchad (39688)", resolved.DisplayName);
        Assert.Equal("character:39688", resolved.ScriptReference);
        Assert.Equal("living/39688/alive_data", resolved.OwnerPath);
    }

    [Fact]
    public void NameOf_uses_object_path() =>
        Assert.Equal("Donnchad (39688)", Names().NameOf("living", "39688"));

    [Fact]
    public void Other_sub_path_is_other()
    {
        var names = Names();

        Assert.Equal(names.Kinds[^1], names.Resolve("living/39688/family_data").Kind);
    }
}
