using System.Text.Json.Nodes;
using ParadoxModTool.Generators.Editing.Package;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Formular, Ebenen und Konstanten im Paket-Editor (Plan Teil 5a, Aufgabe 4).</summary>
public sealed partial class PackageDocumentTests
{
    [Fact]
    public void Head_add_move_remove()
    {
        var document = PackageDocument.Load(Minimal());

        document.Head.Add("coa", FieldType.Image);
        document.Head.Add("note", FieldType.Text);
        document.Head.Move("note", -1);
        Assert.Equal(new[] { "tag", "note", "coa" }, document.Head.Items.Select(f => f.Id));
        document.Head.Remove("tag");
        Assert.Equal(new[] { "note", "coa" }, document.Head.Items.Select(f => f.Id));
        Assert.Throws<ArgumentException>(() => document.Head.Add("coa", FieldType.Text));
        Assert.Equal("image", Json(document)["head"]![1]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Field_type_change_drops_choices_and_asset()
    {
        var document = PackageDocument.Load(Minimal());
        var field = document.Head.Add("pick", FieldType.Choice);
        field.Choices = new[] { "a", "b" };
        Assert.Equal(new[] { "a", "b" }, field.Choices);

        field.Type = FieldType.Text;
        field.Multilingual = true;

        Assert.Null(field.Raw["choices"]);
        Assert.True(field.Multilingual);
        field.Type = FieldType.Number;
        Assert.Null(field.Raw["multilingual"]);
    }

    [Fact]
    public void Field_properties_round_trip()
    {
        var folder = Minimal();
        var document = PackageDocument.Load(folder);
        var tag = document.Head.Items.Single();
        tag.Required = true;
        tag.Pattern = "^[A-Z]{3}$";
        tag.Default = "BYZ";
        tag.Label = LocalizedText.Plain("Tag").With("de", "Kürzel");
        document.Save();

        var field = GeneratorPackageLoader.Load(folder, uiLanguage: "de").Head.Single();
        Assert.True(field.Required);
        Assert.Equal("^[A-Z]{3}$", field.Pattern);
        Assert.Equal("BYZ", field.Default);
        Assert.Equal("Kürzel", field.Label);
    }

    [Fact]
    public void Level_chain_add_and_remove_with_children()
    {
        var folder = Minimal();
        var document = PackageDocument.Load(folder);

        var ideas = document.AddChildLevel(null, "ideas");
        ideas.Max = 9;
        var levels = document.AddChildLevel(ideas, "levels");
        levels.Min = 1;
        levels.Max = 4;
        levels.Initial = 4;
        levels.Fields.Add("modifiers", FieldType.Modifiers);
        document.Save();

        var package = GeneratorPackageLoader.Load(folder);
        Assert.Equal("ideas", package.Root!.Id);
        Assert.Equal(4, package.Root.Child!.Initial);
        Assert.Equal("modifiers", package.Root.Child.Fields.Single().Id);
        Assert.Same(document.Root, document.Root!.Child!.Parent);

        document.RemoveLevel(document.Root!);
        Assert.Null(document.Root);
        Assert.Null(Json(document)["root"]);
    }

    [Fact]
    public void Rename_level_updates_forEach_and_numberBaseField_users()
    {
        var document = PackageDocument.Load(Minimal(outputs: "[ { \"id\": \"o\", \"kind\": \"insertSection\", \"path\": \"x.txt\", \"template\": \"a.sbn\", \"forEach\": \"ideas\" } ]"));
        var ideas = document.AddChildLevel(null, "ideas");

        document.RenameLevel(ideas, "entries");

        Assert.Equal("entries", document.Root!.Id);
        Assert.Equal("entries", Json(document)["outputs"]![0]!["forEach"]!.GetValue<string>());
    }

    [Fact]
    public void Rename_constant_table_updates_levels()
    {
        var document = PackageDocument.Load(Minimal("\"constants\": { \"categories\": [ { \"key\": \"adm\", \"base\": \"0\" } ] }, "));
        var root = document.AddChildLevel(null, "categories");
        root.ConstantsTable = "categories";

        Assert.Equal(new[] { "categories" }, document.LevelsUsingTable("categories"));
        document.RenameConstantTable("categories", "groups");

        Assert.Equal(new[] { "groups" }, document.ConstantTables);
        Assert.Equal("groups", document.Root!.ConstantsTable);
        Assert.Equal("adm", document.ConstantRows("groups")[0]["key"]!.ToString());
    }

    [Fact]
    public void Constant_label_keeps_language_map()
    {
        var folder = Minimal("\"constants\": { \"c\": [ { \"key\": \"adm\", \"label\": { \"en\": \"Administrative\", \"de\": \"Verwaltung\" } } ] }, ");
        var document = PackageDocument.Load(folder);
        var rows = document.ConstantRows("c");

        document.SetConstantRows("c", rows.Append(new Dictionary<string, JsonNode?> { ["key"] = JsonValue.Create("dip") }).ToList());
        document.AddConstantTable("empty");
        document.Save();

        var json = Json(folder)["constants"]!;
        Assert.Equal("Verwaltung", json["c"]![0]!["label"]!["de"]!.GetValue<string>());
        Assert.Equal("dip", json["c"]![1]!["key"]!.GetValue<string>());
        Assert.Empty(json["empty"]!.AsArray());
        document.RemoveConstantTable("empty");
        Assert.Equal(new[] { "c" }, document.ConstantTables);
    }

    [Fact]
    public void IdIssueOf_names_invalid_and_duplicate_ids()
    {
        var ids = new[] { "coa", "icons" };

        Assert.Null(PackageDocument.IdIssueOf(ids, 1, "flags"));
        Assert.Null(PackageDocument.IdIssueOf(ids, 0, "coa"));
        Assert.Equal(IdIssue.Duplicate, PackageDocument.IdIssueOf(ids, 1, "coa"));
        Assert.Equal(IdIssue.Invalid, PackageDocument.IdIssueOf(ids, 1, "1x"));
        Assert.Equal(IdIssue.Invalid, PackageDocument.IdIssueOf(ids, 1, string.Empty));
    }

    [Theory]
    [InlineData("guiSelectTt", true)]
    [InlineData("_x", true)]
    [InlineData("1a", false)]
    [InlineData("a-b", false)]
    [InlineData("", false)]
    public void IsValidId_cases(string id, bool valid) => Assert.Equal(valid, PackageDocument.IsValidId(id));

    [Fact]
    public void Lookup_on_field_is_kept_when_other_props_change()
    {
        var folder = Minimal();
        File.WriteAllText(Path.Combine(folder, "package.json"), File.ReadAllText(Path.Combine(folder, "package.json"))
            .Replace("\"type\": \"text\" }", "\"type\": \"text\", \"lookup\": { \"sources\": [ \"x\" ] } }"));
        var document = PackageDocument.Load(folder);
        var tag = document.Head.Items.Single();

        Assert.True(tag.HasLookup);
        tag.Label = LocalizedText.Plain("Neu");
        tag.Type = FieldType.MultilineText;

        Assert.Equal("x", Json(document)["head"]![0]!["lookup"]!["sources"]![0]!.GetValue<string>());
    }

    private static JsonNode Json(PackageDocument document) => JsonNode.Parse(document.Json.ToJsonString())!;
}
