using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ChangelogFileTests : TempDirTests
{
    [Fact]
    public void Suggest_FindetChangelogImReadmeOrdner_MitRelativemPfad()
    {
        var folder = Make("mod");
        Directory.CreateDirectory(Path.Combine(folder, "Readme"));
        File.WriteAllText(Path.Combine(folder, "Readme", "Changelog.txt"), "x");
        File.WriteAllText(Path.Combine(folder, "Readme", "Andere.txt"), "x");

        Assert.Equal("Readme/Changelog.txt", ChangelogFile.Suggest(folder));
    }

    [Fact]
    public void Suggest_WurzelVorUnterordner_UndMarkdownAkzeptiert()
    {
        var folder = Make("mod");
        Directory.CreateDirectory(Path.Combine(folder, "docs"));
        File.WriteAllText(Path.Combine(folder, "docs", "changelog.txt"), "x");
        File.WriteAllText(Path.Combine(folder, "CHANGELOG.md"), "x");

        Assert.Equal("CHANGELOG.md", ChangelogFile.Suggest(folder));
    }

    [Fact]
    public void Suggest_NichtsGefunden_UndFehlenderOrdner()
    {
        Assert.Null(ChangelogFile.Suggest(Make("leer")));
        Assert.Null(ChangelogFile.Suggest(Path.Combine(Dir, "gibtsnicht")));
    }
}
