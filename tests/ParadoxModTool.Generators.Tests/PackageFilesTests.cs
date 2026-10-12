using System.Text.Json.Nodes;
using ParadoxModTool.Generators.Editing.Package;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Ausgaben im Paket-Editor und Vorlagen-Dateien (Plan Teil 5a, Aufgabe 5).</summary>
public sealed class PackageFilesTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private PackageDocument Document(string outputs)
    {
        _temp.Write("p/package.json", "{ \"id\": \"p\", \"name\": \"P\", \"gameId\": \"eu5\", \"keyField\": \"tag\", " +
            "\"head\": [ { \"id\": \"tag\", \"type\": \"text\" } ], \"outputs\": " + outputs + " }");
        _temp.Write("p/templates/a.sbn", "A");
        _temp.Write("p/templates/sk.sbn", "S");
        return PackageDocument.Load(_temp.PathOf("p"));
    }

    [Fact]
    public void Kind_change_keeps_shared_fields_only()
    {
        var document = Document("[ { \"id\": \"o\", \"kind\": \"syncBlocks\", \"path\": \"x.txt\", \"template\": \"a.sbn\", \"keyPattern\": \"k_*\", \"keep\": [ \"game_data\" ], \"newLine\": \"lf\" } ]");
        var output = document.Outputs.Single();

        output.Kind = OutputKind.OwnFile;

        Assert.Equal(OutputKind.OwnFile, output.Kind);
        Assert.Equal("ownFile", output.Raw["kind"]!.GetValue<string>());
        Assert.Equal("a.sbn", output.Get("template"));
        Assert.Equal("x.txt", output.Path);
        Assert.Equal("lf", output.Get("newLine"));
        Assert.Null(output.Get("keyPattern"));
        Assert.Empty(output.GetList("keep"));
        Assert.Null(output.Raw["keep"]);
    }

    [Fact]
    public void Output_fields_round_trip_through_loader()
    {
        var document = Document("[]");
        var output = document.AddOutput("sec", OutputKind.InsertSection);
        output.Path = "x_{{ tag }}.txt";
        output.Set("template", "a.sbn");
        output.Set("skeleton", "sk.sbn");
        output.SetList("legacyPatterns", new[] { "^a_\\d+" });
        document.AddOutput("sync", OutputKind.SyncBlocks).CheckUnreferenced = true;
        document.Outputs[1].Path = "y.txt";
        document.Outputs[1].Set("template", "a.sbn");
        document.Outputs[1].Set("keyPattern", "k_*");
        document.MoveOutput("sync", -1);
        document.Save();

        var package = GeneratorPackageLoader.Load(document.Folder);
        Assert.Equal(new[] { "sync", "sec" }, package.Outputs.Select(o => o.Id));
        Assert.True(package.Outputs[0].CheckUnreferenced);
        Assert.Equal(new[] { "^a_\\d+" }, package.Outputs[1].LegacyPatterns);
        Assert.Equal("sk.sbn", package.Outputs[1].Skeleton);
    }

    [Fact]
    public void Duplicate_output_gets_unique_id()
    {
        var document = Document("[ { \"id\": \"x\", \"kind\": \"ownFile\", \"path\": \"x.txt\", \"template\": \"a.sbn\" } ]");

        Assert.Equal("x_copy", document.DuplicateOutput("x").Id);
        Assert.Equal("x_copy2", document.DuplicateOutput("x").Id);
        Assert.Equal("a.sbn", document.Outputs[1].Get("template"));
        document.RemoveOutput("x_copy");
        Assert.Equal(new[] { "x", "x_copy2" }, document.Outputs.Select(o => o.Id));
    }

    [Fact]
    public void Rename_template_updates_template_and_skeleton()
    {
        var document = Document("[ { \"id\": \"o\", \"kind\": \"insertSection\", \"path\": \"x.txt\", \"template\": \"a.sbn\", \"skeleton\": \"a.sbn\" }, { \"id\": \"p\", \"kind\": \"ownFile\", \"path\": \"y.txt\", \"template\": \"sk.sbn\" } ]");

        document.RenameTemplateReferences("a.sbn", "b.sbn");

        Assert.Equal("b.sbn", document.Outputs[0].Get("template"));
        Assert.Equal("b.sbn", document.Outputs[0].Get("skeleton"));
        Assert.Equal("sk.sbn", document.Outputs[1].Get("template"));
    }

    [Fact]
    public void Outputs_using_template_lists_ids()
    {
        var document = Document("[ { \"id\": \"o\", \"kind\": \"insertSection\", \"path\": \"x.txt\", \"template\": \"a.sbn\", \"skeleton\": \"sk.sbn\" }, { \"id\": \"p\", \"kind\": \"ownFile\", \"path\": \"y.txt\", \"template\": \"sk.sbn\" } ]");

        Assert.Equal(new[] { "o", "p" }, document.OutputsUsingTemplate("sk.sbn"));
        Assert.Equal(new[] { "o" }, document.OutputsUsingTemplate("a.sbn"));
    }

    [Fact]
    public void Files_rename_and_delete()
    {
        Document("[]");
        var files = new PackageFiles(_temp.PathOf("p"));

        Assert.Equal(new[] { "a.sbn", "sk.sbn" }, files.Templates);
        files.Write("c.sbn", "C\r\nD");
        files.Rename("a.sbn", "z.sbn");
        files.Delete("sk.sbn");
        Assert.Equal(new[] { "c.sbn", "z.sbn" }, files.Templates);
        Assert.Equal("A", files.Read("z.sbn"));
        Assert.Equal("C\r\nD", files.Read("c.sbn"));
        Assert.Equal(string.Empty, files.Functions);
        files.Functions = "{{ func f }}{{ end }}";
        Assert.Equal("{{ func f }}{{ end }}", new PackageFiles(_temp.PathOf("p")).Functions);
    }

    [Theory]
    [InlineData("a.sbn", true)]
    [InlineData("x_1-b.sbn", true)]
    [InlineData("../a.sbn", false)]
    [InlineData("a.txt", false)]
    [InlineData("sub/a.sbn", false)]
    [InlineData(".sbn", false)]
    public void Template_name_validation(string name, bool valid) => Assert.Equal(valid, PackageFiles.IsValidTemplateName(name));

    [Fact]
    public void KeysOf_lists_kind_fields()
    {
        Assert.Equal(new[] { "template", "newLine" }, OutputNode.KeysOf(OutputKind.OwnFile));
        Assert.Contains("legacyPatterns", OutputNode.KeysOf(OutputKind.InsertSection));
        Assert.Contains("checkUnreferenced", OutputNode.KeysOf(OutputKind.SyncBlocks));
        Assert.Equal(new[] { "source", "newLine" }, OutputNode.KeysOf(OutputKind.CopyBinary));
        Assert.True(JsonNode.DeepEquals(JsonValue.Create(1), JsonValue.Create(1)));
    }
}
