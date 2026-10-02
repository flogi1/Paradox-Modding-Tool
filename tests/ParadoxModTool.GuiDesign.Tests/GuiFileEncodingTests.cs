using ParadoxModTool.GuiDesign.Designer;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiFileEncodingTests
{
    [Fact]
    public void NewFilesFollowTheProfile()
    {
        // windows-1252 würde beim Zurücklesen (alle Leser erwarten UTF-8) Umlaute zerstören: neue Dateien werden UTF-8 mit BOM (3c-Review).
        var legacy = GuiFileEncoding.For("windows-1252", isNew: true, existingHasBom: false);
        Assert.Equal(3, legacy.GetPreamble().Length);
        Assert.Equal(new byte[] { 0xC3, 0xA4 }, legacy.GetBytes("ä"));
        Assert.Empty(GuiFileEncoding.For("utf-8", isNew: true, existingHasBom: false).GetPreamble());
        Assert.Equal(3, GuiFileEncoding.For("utf-8-bom", isNew: true, existingHasBom: false).GetPreamble().Length);
        Assert.Equal(3, GuiFileEncoding.For("unbekannt", isNew: true, existingHasBom: false).GetPreamble().Length);
    }

    [Fact]
    public void ExistingFilesKeepTheirBom()
    {
        Assert.Empty(GuiFileEncoding.For("windows-1252", isNew: false, existingHasBom: false).GetPreamble());
        Assert.Equal(3, GuiFileEncoding.For("utf-8", isNew: false, existingHasBom: true).GetPreamble().Length);
    }
}
