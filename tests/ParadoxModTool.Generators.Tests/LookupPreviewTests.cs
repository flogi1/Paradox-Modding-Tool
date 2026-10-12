using System.Text;
using ParadoxModTool.Generators.Editing.Package;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>"Vorschau" je Nachschlage-Quelle (Spec Teil 5b, Abschnitt 2).</summary>
public sealed class LookupPreviewTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string[] Roots => new[] { _temp.PathOf("game") };

    private (PackageDocument Document, PackageFiles Files) Package()
    {
        _temp.Write("p/package.json",
            "{ \"id\": \"p\", \"name\": \"P\", \"gameId\": \"eu5\", \"keyField\": \"tag\", \"head\": [ { \"id\": \"tag\", \"type\": \"text\" } ], \"outputs\": [], " +
            "\"assets\": { \"db\": { \"path\": \"assets/db.json\", \"origin\": \"mod\" } }, " +
            "\"lookups\": [ { \"id\": \"adv\", \"kind\": \"gameKeys\", \"folder\": \"in_game/common/advances\" }, " +
            "{ \"id\": \"empty\", \"kind\": \"gameKeys\", \"folder\": \"in_game/common/empty\" }, " +
            "{ \"id\": \"nations\", \"kind\": \"json\", \"asset\": \"db\", \"items\": \"nations[].ideas[]\", \"fields\": { \"key\": \"key\", \"name\": \"name\", \"tag\": \"../tag\" } }, " +
            "{ \"id\": \"wrong\", \"kind\": \"json\", \"asset\": \"db\", \"items\": \"nope[]\" } ] }");
        return (PackageDocument.Load(_temp.PathOf("p")), new PackageFiles(_temp.PathOf("p")));
    }

    private void Advances()
    {
        _temp.Write("game/in_game/common/advances/a.txt", "adv_1 = { }\nadv_2 = { }\nadv_3 = { }");
        _temp.Write("game/main_menu/localization/english/a_l_english.yml", "l_english:\n adv_1: \"First\"\n", new UTF8Encoding(true));
    }

    private void Database() =>
        _temp.Write("p/assets/db.json", "{ \"nations\": [ { \"tag\": \"SWE\", \"ideas\": [ { \"key\": \"i1\", \"name\": \"Idea 1\" }, { \"key\": \"i2\", \"name\": \"Idea 2\" } ] } ] }");

    [Fact]
    public void Game_source_lists_entries()
    {
        var (document, files) = Package();
        Advances();

        var result = LookupPreview.Run(document, files, null, "adv", Roots);

        Assert.Null(result.Reason);
        Assert.Equal(3, result.Count);
        Assert.Equal("First", result.Entries.Single(e => e.Key == "adv_1").Name);
    }

    [Fact]
    public void Max_limits_entries_not_count()
    {
        var (document, files) = Package();
        Advances();

        var result = LookupPreview.Run(document, files, null, "adv", Roots, max: 2);

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result.Entries.Count);
    }

    [Fact]
    public void Missing_game_folder_reason()
    {
        var (document, files) = Package();

        var result = LookupPreview.Run(document, files, null, "adv", Roots);

        Assert.Equal("gameFolder", result.ReasonKind);
        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void Json_source_and_missing_asset()
    {
        var (document, files) = Package();

        Assert.Equal("asset", LookupPreview.Run(document, files, null, "nations", Roots).ReasonKind);
        Database();
        var result = LookupPreview.Run(document, files, null, "nations", Roots);

        Assert.Null(result.Reason);
        Assert.Equal(new[] { "i1", "i2" }, result.Entries.Select(e => e.Key));
        Assert.Equal("SWE", result.Entries[0].Tag);
    }

    [Fact]
    public void Empty_folder_reason()
    {
        var (document, files) = Package();
        Directory.CreateDirectory(_temp.PathOf("game/in_game/common/empty"));

        var result = LookupPreview.Run(document, files, null, "empty", Roots);

        Assert.Equal("empty", result.ReasonKind);
        Assert.Equal(GeneratorsTexts.PreviewNoTxt("in_game/common/empty"), result.Reason);
    }

    [Fact]
    public void Folder_without_blocks_reason()
    {
        var (document, files) = Package();
        _temp.Write("game/in_game/common/empty/x.txt", "# nur Kommentar");

        var result = LookupPreview.Run(document, files, null, "empty", Roots);

        Assert.Equal(GeneratorsTexts.PreviewNoBlocks, result.Reason);
    }

    [Fact]
    public void Items_path_matching_nothing()
    {
        var (document, files) = Package();
        Database();

        var result = LookupPreview.Run(document, files, null, "wrong", Roots);

        Assert.Equal("empty", result.ReasonKind);
        Assert.Equal(GeneratorsTexts.PreviewItemsEmpty("nope[]"), result.Reason);
    }

    [Fact]
    public void Broken_tagFromFile_is_reported_not_thrown()
    {
        var (document, files) = Package();
        Advances();
        document.LookupSources[0].Set("tagFromFile", "([");

        var result = LookupPreview.Run(document, files, null, "adv", Roots);

        Assert.Equal("file", result.ReasonKind);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void Load_error_reason()
    {
        var (document, files) = Package();
        document.KeyField = "nope";

        Assert.Equal("load", LookupPreview.Run(document, files, null, "adv", Roots).ReasonKind);
    }
}
