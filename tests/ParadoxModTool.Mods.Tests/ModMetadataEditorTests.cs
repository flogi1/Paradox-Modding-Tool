using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModMetadataEditorTests
{
    /// <summary>Gekürzt aus der echten Datei "The Idea Variation 2": Tabs, ein zweites <c>version</c> in <c>relationships</c>.</summary>
    private const string Real =
        "{\n\t\"name\":\t\"The Idea Variation 2\",\n\t\"id\":\t\"Idea_Variation_2\",\n\t\"version\":\t\"2.0.5\",\n\t\"supported_game_version\":\t\"1.3.*\",\n" +
        "\t\"short_description\":\t\"Adds back Ideagroups and National Ideas to EU V.\",\n\t\"tags\":\t[\"Gameplay\", \"Overhaul\"],\n" +
        "\t\"relationships\" : [\n    {\n      \"rel_type\" : \"dependency\",\n      \"id\" : \"community_mod_framework\",\n      \"version\" : \"2.*\"\n    }\n    ],\n" +
        "\t\"picture\": \"thumbnail.png\",\n\t\"game_custom_data\":\t{\n}\n}";

    [Fact]
    public void SetVersion_AendertNurDasObersteVersion_UndNichtDasInRelationships()
    {
        var result = ModMetadataEditor.SetVersion(Real, "2.0.6");

        Assert.Equal(Real.Replace("\"version\":\t\"2.0.5\"", "\"version\":\t\"2.0.6\""), result);
        Assert.Contains("\"version\" : \"2.*\"", result);
    }

    [Fact]
    public void ReadVersion_LiestDasObersteFeld_TextZahlOderFehlend()
    {
        Assert.Equal("2.0.5", ModMetadataEditor.ReadVersion(Real));
        Assert.Equal("2", ModMetadataEditor.ReadVersion("{ \"version\": 2 }"));
        Assert.Null(ModMetadataEditor.ReadVersion("{ \"name\": \"X\" }"));
        Assert.Null(ModMetadataEditor.ReadVersion("{ kaputt"));
    }

    [Fact]
    public void SetVersion_BehaeltKommentareUndKommaAmEnde()
    {
        var text = "{\n  // Kommentar\n  \"name\": \"X\",\n  \"version\": \"1.0\",\n}";

        Assert.Equal(text.Replace("\"1.0\"", "\"1.1\""), ModMetadataEditor.SetVersion(text, "1.1"));
    }

    [Fact]
    public void SetVersion_FehlendesFeld_WirdNachDerKlammerEingefuegt_MitEinrueckungUndZeilenenden()
    {
        var result = ModMetadataEditor.SetVersion("{\r\n\t\"name\": \"X\"\r\n}", "1.0.0");

        Assert.Equal("{\r\n\t\"version\": \"1.0.0\",\r\n\t\"name\": \"X\"\r\n}", result);
        Assert.Null(ModMetadataEditor.Validate(result));
    }

    [Fact]
    public void SetVersion_FehlendesFeldInEinzeiligemObjekt()
    {
        var result = ModMetadataEditor.SetVersion("{\"name\":\"X\"}", "1.0");

        Assert.Equal("{\"version\": \"1.0\",\"name\":\"X\"}", result);
    }

    [Fact]
    public void SetVersion_LeeresObjekt()
    {
        var result = ModMetadataEditor.SetVersion("{}", "1.0");

        Assert.Equal("1.0", ModMetadataEditor.ReadVersion(result));
        Assert.Null(ModMetadataEditor.Validate(result));
    }

    [Fact]
    public void SetVersion_ZahlWirdZuText()
    {
        Assert.Equal("{\"version\": \"2.1\"}", ModMetadataEditor.SetVersion("{\"version\": 2}", "2.1"));
    }

    [Fact]
    public void SetVersion_MaskiertSonderzeichen()
    {
        Assert.Equal("{\"version\": \"1\\u0022x\"}", ModMetadataEditor.SetVersion("{\"version\": \"1\"}", "1\"x"));
    }

    [Fact]
    public void SetVersion_KeinObjektKaputtOderLeereVersion_Wirft()
    {
        Assert.Throws<InvalidOperationException>(() => ModMetadataEditor.SetVersion("[1,2]", "1.0"));
        Assert.Throws<InvalidOperationException>(() => ModMetadataEditor.SetVersion("{ kaputt", "1.0"));
        Assert.Throws<ArgumentException>(() => ModMetadataEditor.SetVersion(Real, "  "));
    }

    [Fact]
    public void Validate_GueltigeDatei_LiefertNull_FehlerMitZeile()
    {
        Assert.Null(ModMetadataEditor.Validate(Real));
        Assert.Contains("Zeile 2", ModMetadataEditor.Validate("{\n\"a\": }")!);
        Assert.Contains("JSON-Objekt", ModMetadataEditor.Validate("[1]")!);
        Assert.NotNull(ModMetadataEditor.Validate(""));
    }

    [Theory]
    [InlineData("2.0.5", "2.0.6")]
    [InlineData("2.0.9", "2.0.10")]
    [InlineData("1.0", "1.1")]
    [InlineData("7", "8")]
    [InlineData("abc", "abc")]
    [InlineData("", "")]
    public void NextPatch_ZaehltDenLetztenTeilHoch(string version, string expected)
    {
        Assert.Equal(expected, ModMetadataEditor.NextPatch(version));
    }

    [Fact]
    public void DieEchteDatei_UnveraendertBisAufDieVersion()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Paradox Interactive", "Europa Universalis V", "mod", "The Idea Variation 2", ".metadata", "metadata.json");
        if (!File.Exists(path))
        {
            return;
        }

        var text = ModTextFile.Read(path).Text;
        var current = ModMetadataEditor.ReadVersion(text)!;

        var changed = ModMetadataEditor.SetVersion(text, ModMetadataEditor.NextPatch(current));

        Assert.NotEqual(text, changed);
        Assert.Equal(text, ModMetadataEditor.SetVersion(changed, current));
        Assert.Null(ModMetadataEditor.Validate(changed));
    }
}
