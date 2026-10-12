using System.Text.Json.Nodes;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Generators.Lookups;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Abschnitt <c>lookups</c> und Feld-Angabe <c>lookup</c> im Paket-Editor (Plan Teil 5b, Aufgabe 3).</summary>
public sealed partial class PackageDocumentTests
{
    /// <summary>Kopf-Feld <c>title</c> mit Quelle a; Ebene <c>ideas</c> mit <c>name</c> (Quellen a, b) und <c>desc</c>.</summary>
    private const string WithLookups =
        "\"root\": { \"id\": \"ideas\", \"fields\": [ " +
        "{ \"id\": \"name\", \"type\": \"text\", \"lookup\": { \"sources\": [ \"a\", \"b\" ], \"fills\": { \"name\": \"name\", \"desc\": \"desc\" }, \"x\": 1 } }, " +
        "{ \"id\": \"desc\", \"type\": \"multilineText\" } ] }, " +
        "\"lookups\": [ { \"id\": \"a\", \"kind\": \"gameKeys\", \"folder\": \"f\", \"depth\": 2, \"label\": \"A\" }, " +
        "{ \"id\": \"b\", \"kind\": \"json\", \"asset\": \"db\", \"items\": \"x[]\", \"fields\": { \"key\": \"k\" }, \"label\": \"B\", \"extra\": true } ], ";

    private PackageDocument LookupDocument()
    {
        var document = PackageDocument.Load(Minimal(WithLookups));
        var title = document.Head.Add("title", FieldType.Text);
        title.EnableLookup().Sources = new[] { "a" };
        title.Lookup!.SetFill("name", "title");
        return document;
    }

    [Fact]
    public void Remove_source_cleans_fields_on_all_levels()
    {
        var document = LookupDocument();

        var changed = document.RemoveLookupSource("a");

        Assert.Equal(2, changed);
        Assert.Null(document.Head.Items.Single(f => f.Id == "title").Lookup);
        Assert.Equal(new[] { "b" }, document.Levels[0].Fields.Items[0].Lookup!.Sources);
        Assert.Equal(new[] { "b" }, document.LookupSources.Select(s => s.Id));
    }

    [Fact]
    public void Rename_source_updates_field_sources()
    {
        var document = LookupDocument();

        document.RenameLookupSource(document.LookupSources[0], "adv");

        Assert.Equal(new[] { "adv", "b" }, document.Levels[0].Fields.Items[0].Lookup!.Sources);
        Assert.Equal(new[] { "adv" }, document.Head.Items.Single(f => f.Id == "title").Lookup!.Sources);
        Assert.Equal(2, document.FieldsUsingSource("adv").Count);
    }

    [Fact]
    public void Kind_change_drops_other_keys()
    {
        var document = LookupDocument();
        var a = document.LookupSources[0];
        Assert.Equal(2, a.Depth);

        a.Kind = LookupKind.Json;

        Assert.Null(a.Raw["folder"]);
        Assert.Null(a.Raw["depth"]);
        Assert.Equal("json", a.Raw["kind"]!.GetValue<string>());
        Assert.Equal("A", a.Label.Resolve("de"));
    }

    [Fact]
    public void Duplicate_gets_free_id()
    {
        var document = LookupDocument();

        var copy = document.DuplicateLookupSource("b");
        var again = document.DuplicateLookupSource("b");

        Assert.Equal("b_2", copy.Id);
        Assert.Equal("b_3", again.Id);
        Assert.Equal("k", copy.JsonField("key"));
        Assert.Equal(new[] { "a", "b", "b_2", "b_3" }, document.LookupSources.Select(s => s.Id));
    }

    [Fact]
    public void Lookup_fills_set_and_clear()
    {
        var document = LookupDocument();
        var lookup = document.Levels[0].Fields.Items[0].Lookup!;

        Assert.Equal("desc", lookup.Fill("desc"));
        lookup.SetFill("desc", null);

        Assert.Null(lookup.Fill("desc"));
        Assert.Equal("name", lookup.Fill("name"));
        Assert.Null(document.Levels[0].Fields.Items[0].Raw["lookup"]!["fills"]!["desc"]);
    }

    [Fact]
    public void Type_change_removes_lookup()
    {
        var document = LookupDocument();
        var name = document.Levels[0].Fields.Items[0];

        name.Type = FieldType.Number;

        Assert.Null(name.Lookup);
    }

    [Fact]
    public void Unknown_lookup_keys_survive()
    {
        var document = LookupDocument();

        document.Levels[0].Fields.Items[0].Lookup!.Sources = new[] { "b" };
        var b = document.LookupSources[1];
        b.SetJsonField("name", "n");
        b.Set("items", "y[]");

        Assert.Equal(1, document.Levels[0].Fields.Items[0].Raw["lookup"]!["x"]!.GetValue<int>());
        Assert.True(b.Raw["extra"]!.GetValue<bool>());
        Assert.Equal("n", b.JsonField("name"));
        Assert.Equal("k", b.JsonField("key"));
    }

    [Fact]
    public void Move_and_add_sources()
    {
        var document = LookupDocument();

        document.AddLookupSource("c", LookupKind.GameKeys);
        document.MoveLookupSource("c", -2);

        Assert.Equal(new[] { "c", "a", "b" }, document.LookupSources.Select(s => s.Id));
        Assert.Equal("gameKeys", document.LookupSources[0].Raw["kind"]!.GetValue<string>());
        Assert.Equal(1, document.LookupSources[0].Depth);
        document.LookupSources[0].SetText("folder", string.Empty);
        Assert.Equal(string.Empty, document.LookupSources[0].Raw["folder"]!.GetValue<string>());
        document.LookupSources[0].SetText("folder", "in_game/x");
        Assert.Equal("in_game/x", document.LookupSources[0].Get("folder"));
    }

    [Fact]
    public void FieldList_knows_its_level()
    {
        var document = LookupDocument();

        Assert.Null(document.Head.LevelId);
        Assert.Equal("ideas", document.Levels[0].Fields.LevelId);
        Assert.Equal(new[] { "tag", "title", "name", "desc" }, document.AllFields().Select(p => p.Field.Id));
    }

    [Fact]
    public void Iv2_lookups_round_trip_unchanged()
    {
        var folder = Iv2Copy();
        var before = Json(folder);
        var document = PackageDocument.Load(folder);

        foreach (var source in document.LookupSources)
        {
            source.Label = source.Label;
            source.Depth = source.Depth;
            source.NamedOnly = source.NamedOnly;
            if (source.Kind == LookupKind.GameKeys)
            {
                source.TagInBlock = source.TagInBlock;
                source.Set("tagFromFile", source.Get("tagFromFile"));
            }
            else
            {
                foreach (var name in new[] { "key", "name", "desc", "tag", "nation" })
                {
                    source.SetJsonField(name, source.JsonField(name));
                }
            }
        }

        foreach (var (_, field) in document.AllFields())
        {
            if (field.Lookup is { } lookup)
            {
                lookup.Sources = lookup.Sources;
                lookup.SetFill("name", lookup.Fill("name"));
                lookup.SetFill("desc", lookup.Fill("desc"));
            }
        }

        document.Save();
        var after = Json(folder);
        Assert.True(JsonNode.DeepEquals(before, after));
    }
}
