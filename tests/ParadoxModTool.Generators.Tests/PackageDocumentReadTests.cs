using System.Text.Json.Nodes;
using ParadoxModTool.Generators.Editing.Package;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Abschnitt <c>read</c> im Paket-Editor (Plan Teil 5b, Aufgabe 2).</summary>
public sealed partial class PackageDocumentTests
{
    private const string TwoLevels =
        "\"root\": { \"id\": \"ideas\", \"fields\": [ { \"id\": \"name\", \"type\": \"text\", \"multilingual\": true } ], " +
        "\"child\": { \"id\": \"levels\", \"fields\": [ { \"id\": \"modifiers\", \"type\": \"modifiers\" } ] } }, ";

    [Fact]
    public void Iv2_read_round_trips_unchanged()
    {
        var folder = Iv2Copy();
        var before = Json(folder);
        var document = PackageDocument.Load(folder);
        var read = document.Read!;

        read.Keys = read.Keys;
        read.ElementsFile = read.ElementsFile;
        read.ElementsBlock = read.ElementsBlock;
        read.ElementsLevel = read.ElementsLevel;
        foreach (var (field, value) in read.ElementFields.ToList())
        {
            read.SetElementField(field, value.Body, value.Except);
        }

        foreach (var list in new[] { read.Localization, read.Flags, read.Assets })
        {
            foreach (var row in list.Items)
            {
                foreach (var column in row.Raw.Select(p => p.Key).ToList())
                {
                    row.Set(column, row.Get(column));
                }
            }
        }

        document.Save();
        var after = Json(folder);
        Assert.True(JsonNode.DeepEquals(before["read"], after["read"]));
        Assert.True(JsonNode.DeepEquals(before["lookups"], after["lookups"]));
    }

    [Fact]
    public void EnableRead_creates_minimal_section_on_deepest_level()
    {
        var document = PackageDocument.Load(Minimal(TwoLevels));

        var read = document.EnableRead();

        Assert.Same(read.Raw, document.Read!.Raw);
        Assert.Equal("levels", read.ElementsLevel);
        Assert.Equal(string.Empty, read.Keys);
        Assert.Equal(string.Empty, document.Json["read"]!["elements"]!["block"]!.GetValue<string>());
        Assert.Empty(read.ElementFields);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void DisableRead_removes_section()
    {
        var document = PackageDocument.Load(Minimal(TwoLevels));
        document.EnableRead();

        document.DisableRead();

        Assert.Null(document.Read);
        Assert.Null(document.Json["read"]);
    }

    [Fact]
    public void Rows_add_move_remove()
    {
        var document = PackageDocument.Load(Minimal(TwoLevels));
        var rows = document.EnableRead().Localization;

        rows.Add().Set("key", "a");
        rows.Add().Set("key", "b");
        Assert.Equal(string.Empty, rows.Items[0].Get("field"));
        rows.Move(1, -1);
        Assert.Equal(new[] { "b", "a" }, rows.Items.Select(r => r.Get("key")));
        rows.Remove(0);
        rows.Remove(0);

        Assert.Empty(rows.Items);
        Assert.Null(document.Json["read"]!["localization"]);
    }

    [Fact]
    public void SetElementField_false_removes_entry()
    {
        var document = PackageDocument.Load(Minimal(TwoLevels));
        var read = document.EnableRead();

        read.SetElementField("modifiers", true, new[] { "game_data" });
        Assert.Equal(new[] { "game_data" }, read.ElementFields["modifiers"].Except);
        Assert.True(read.ElementFields["modifiers"].Body);
        read.SetElementField("modifiers", false, Array.Empty<string>());

        Assert.Empty(read.ElementFields);
    }

    [Fact]
    public void RenameField_updates_read_and_fills()
    {
        const string extra =
            "\"root\": { \"id\": \"ideas\", \"fields\": [ " +
            "{ \"id\": \"name\", \"type\": \"text\", \"lookup\": { \"sources\": [ \"s\" ], \"fills\": { \"name\": \"title\" } } }, " +
            "{ \"id\": \"title\", \"type\": \"text\", \"multilingual\": true } ] }, " +
            "\"read\": { \"keys\": \"k\", \"elements\": { \"file\": \"f\", \"block\": \"b\", \"level\": \"ideas\", \"fields\": { \"title\": { \"body\": true } } }, " +
            "\"localization\": [ { \"key\": \"l\", \"field\": \"ideas.title\" } ], \"assets\": [ { \"field\": \"coa\", \"file\": \"c\" } ] }, ";
        var document = PackageDocument.Load(Minimal(extra));
        document.Head.Add("coa", FieldType.Image);
        var ideas = document.Levels[0].Fields;

        document.RenameField(ideas, ideas.Items.Single(f => f.Id == "title"), "title2");
        document.RenameField(document.Head, document.Head.Items.Single(f => f.Id == "coa"), "arms");
        document.RenameField(document.Head, document.Head.Items.Single(f => f.Id == "tag"), "key");

        var read = document.Read!;
        Assert.Equal("ideas.title2", read.Localization.Items[0].Get("field"));
        Assert.Equal(new[] { "title2" }, read.ElementFields.Keys);
        Assert.Equal("title2", ideas.Items[0].Lookup!.Fill("name"));
        Assert.Equal("arms", read.Assets.Items[0].Get("field"));
        Assert.Equal("key", document.KeyField);
        Assert.Equal(new[] { "key", "arms" }, document.Head.Items.Select(f => f.Id));
    }

    [Fact]
    public void Unknown_read_keys_survive()
    {
        var document = PackageDocument.Load(Minimal(TwoLevels + "\"read\": { \"keys\": \"k\", \"extra\": 1, \"elements\": { \"file\": \"f\", \"block\": \"b\", \"level\": \"levels\", \"more\": true } }, "));
        var read = document.Read!;

        read.Keys = "k2";
        read.ElementsFile = "f2";
        read.Flags.Add().Set("file", "x");

        Assert.Equal(1, document.Json["read"]!["extra"]!.GetValue<int>());
        Assert.True(document.Json["read"]!["elements"]!["more"]!.GetValue<bool>());
    }
}
