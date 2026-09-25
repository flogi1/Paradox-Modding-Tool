using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.Explorer.Tests;

public class FolderCategoryColorResolverTests
{
    private static readonly List<FolderCategory> Categories = new()
    {
        new FolderCategory { Path = "common", Label = "Common", DefaultColor = "#4A90D9" },
        new FolderCategory { Path = "gui", Label = "GUI", DefaultColor = "#D98E4A" },
    };

    [Fact]
    public void DefaultColorFor_BekannterOrdnername_LiefertKategorieFarbe()
    {
        Assert.Equal("#4A90D9", FolderCategoryColorResolver.DefaultColorFor("common", Categories));
    }

    [Fact]
    public void DefaultColorFor_IgnoriertGrossKleinschreibung()
    {
        Assert.Equal("#D98E4A", FolderCategoryColorResolver.DefaultColorFor("GUI", Categories));
    }

    [Fact]
    public void DefaultColorFor_UnbekannterOrdnername_LiefertNull()
    {
        Assert.Null(FolderCategoryColorResolver.DefaultColorFor("events_unbekannt", Categories));
    }

    [Fact]
    public void DefaultColorFor_KeineKategorien_LiefertNullStattAusnahme()
    {
        Assert.Null(FolderCategoryColorResolver.DefaultColorFor("common", null));
    }
}
