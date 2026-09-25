using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class WorkshopItemVdfTests
{
    private static WorkshopItem Minimal() => new("3450310", "0", @"C:\Temp\stage\content", "Notiz");

    [Fact]
    public void Build_SchreibtDiePflichtfelderInEinemWorkshopitemBlock()
    {
        var vdf = WorkshopItemVdf.Build(Minimal());

        Assert.StartsWith("\"workshopitem\"", vdf);
        Assert.Contains("\"appid\"\t\t\"3450310\"", vdf);
        Assert.Contains("\"publishedfileid\"\t\t\"0\"", vdf);
        Assert.Contains("\"contentfolder\"\t\t\"C:\\\\Temp\\\\stage\\\\content\"", vdf);
        Assert.Contains("\"changenote\"\t\t\"Notiz\"", vdf);
    }

    [Fact]
    public void Build_OptionaleFelderFehlen_WennNichtGesetzt()
    {
        var vdf = WorkshopItemVdf.Build(Minimal());

        Assert.DoesNotContain("\"visibility\"", vdf);
        Assert.DoesNotContain("\"title\"", vdf);
        Assert.DoesNotContain("\"description\"", vdf);
        Assert.DoesNotContain("\"previewfile\"", vdf);
    }

    [Fact]
    public void Build_OptionaleFelder_WennGesetzt()
    {
        var vdf = WorkshopItemVdf.Build(Minimal() with
        {
            Visibility = WorkshopVisibility.FriendsOnly,
            Title = "Titel",
            Description = "Text",
            PreviewFile = @"C:\Temp\stage\content\.metadata\thumbnail.png",
        });

        Assert.Contains("\"visibility\"\t\t\"1\"", vdf);
        Assert.Contains("\"title\"\t\t\"Titel\"", vdf);
        Assert.Contains("\"description\"\t\t\"Text\"", vdf);
        Assert.Contains("\"previewfile\"\t\t\"C:\\\\Temp\\\\stage\\\\content\\\\.metadata\\\\thumbnail.png\"", vdf);
    }

    [Theory]
    [InlineData(WorkshopVisibility.Public, "0")]
    [InlineData(WorkshopVisibility.FriendsOnly, "1")]
    [InlineData(WorkshopVisibility.Private, "2")]
    public void Build_SichtbarkeitAlsZahl(WorkshopVisibility visibility, string expected)
    {
        Assert.Contains($"\"visibility\"\t\t\"{expected}\"", WorkshopItemVdf.Build(Minimal() with { Visibility = visibility }));
    }

    [Fact]
    public void Build_MaskiertAnfuehrungszeichenBackslashUmbruchUndTab()
    {
        var vdf = WorkshopItemVdf.Build(Minimal() with { ChangeNote = "Er sagte \"hi\"\r\nzweite\\Zeile\tTab\nDritte" });

        Assert.Contains("\"changenote\"\t\t\"Er sagte \\\"hi\\\"\\nzweite\\\\Zeile\\tTab\\nDritte\"", vdf);
    }

    [Fact]
    public void ReadPublishedFileId_LiestZahl_NullUndFehlend()
    {
        Assert.Equal("3456789012", WorkshopItemVdf.ReadPublishedFileId("\"workshopitem\"\n{\n\t\"publishedfileid\"\t\t\"3456789012\"\n}"));
        Assert.Equal("0", WorkshopItemVdf.ReadPublishedFileId(WorkshopItemVdf.Build(Minimal())));
        Assert.Null(WorkshopItemVdf.ReadPublishedFileId("\"workshopitem\"\n{\n}"));
        Assert.Null(WorkshopItemVdf.ReadPublishedFileId("\"publishedfileid\" \"abc\""));
    }
}
