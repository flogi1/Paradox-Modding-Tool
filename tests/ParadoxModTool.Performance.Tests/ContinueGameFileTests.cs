using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class ContinueGameFileTests
{
    [Fact]
    public void ReadTitleReadsTabIndentedJson()
    {
        using var tree = new TempTree(("continue_game.json", "{\n\t\"title\":\t\"perf_1337\",\n\t\"desc\":\t\"Save description not implemented\",\n\t\"date\":\t\"2026-10-16 13:10:38\",\n\t\"rawGameVersion\":\t\"1.4.1\"\n}\n"));
        Assert.Equal("perf_1337", ContinueGameFile.ReadTitle(tree.PathOf("continue_game.json")));
    }

    [Fact]
    public void ReadTitleIsNullForMissingFile()
    {
        using var tree = new TempTree();
        Assert.Null(ContinueGameFile.ReadTitle(tree.PathOf("continue_game.json")));
    }

    [Fact]
    public void ReadTitleIsNullForUnreadableContent()
    {
        using var tree = new TempTree(("continue_game.json", "not json"));
        Assert.Null(ContinueGameFile.ReadTitle(tree.PathOf("continue_game.json")));
    }

    [Fact]
    public void WriteTitleReplacesOnlyTheTitleAndKeepsABackup()
    {
        const string original = "{\n\t\"title\":\t\"autosave_7e144ab2\",\n\t\"desc\":\t\"Save description not implemented\",\n\t\"date\":\t\"2026-10-24 20:59:17\",\n\t\"rawGameVersion\":\t\"1.4.4\"\n}\n";
        using var tree = new TempTree(("continue_game.json", original));
        var path = tree.PathOf("continue_game.json");

        Assert.True(ContinueGameFile.WriteTitle(path, "SP_Observer_1338_02_04_7e144ab2"));

        Assert.Equal(original.Replace("autosave_7e144ab2", "SP_Observer_1338_02_04_7e144ab2"), File.ReadAllText(path));
        Assert.Equal("SP_Observer_1338_02_04_7e144ab2", ContinueGameFile.ReadTitle(path));
        Assert.Equal(original, File.ReadAllText(path + ".pmt-backup"));
    }

    [Fact]
    public void WriteTitleReturnsFalseWithoutTitleKeyOrFile()
    {
        using var tree = new TempTree(("continue_game.json", "{\n\t\"desc\":\t\"x\"\n}\n"));
        Assert.False(ContinueGameFile.WriteTitle(tree.PathOf("continue_game.json"), "perf"));
        Assert.Equal("{\n\t\"desc\":\t\"x\"\n}\n", File.ReadAllText(tree.PathOf("continue_game.json")));
        Assert.False(ContinueGameFile.WriteTitle(tree.PathOf("missing.json"), "perf"));
        Assert.False(File.Exists(tree.PathOf("missing.json")));
    }

    [Theory]
    [InlineData("autosave_7e7dfab6", true)]
    [InlineData("Autosave", true)]
    [InlineData("perf_1337", false)]
    [InlineData(null, false)]
    public void IsAutosaveRecognizesAutosaveTitles(string? title, bool expected) => Assert.Equal(expected, ContinueGameFile.IsAutosave(title));
}
