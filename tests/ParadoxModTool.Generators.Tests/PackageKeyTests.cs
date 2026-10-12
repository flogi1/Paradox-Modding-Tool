using ParadoxModTool.Generators.Editing.Package;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Zusammengesetzte Schlüssel (Spec 2026-10-06-generator-packages-idea-groups, Abschnitt 3).</summary>
public sealed class PackageKeyTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private const string Head = """
        [ { "id": "category", "label": "K", "type": "choice", "choices": [ "adm", "dip" ] },
          { "id": "group", "label": "N", "type": "number" },
          { "id": "title", "label": "T", "type": "text" } ]
        """;

    private string Folder(string key, string head = Head)
    {
        _temp.Write("pkg/package.json", $$$"""
            { "id": "pk", "name": "PK", "gameId": "eu5", {{{key}}}, "head": {{{head}}},
              "outputs": [ { "id": "o", "kind": "ownFile", "path": "x.txt", "template": "o.sbn" } ] }
            """);
        _temp.Write("pkg/templates/o.sbn", "x");
        return _temp.PathOf("pkg");
    }

    private GeneratorPackage Load(string key = "\"keyFields\": [ \"category\", \"group\" ]", string head = Head) => GeneratorPackageLoader.Load(Folder(key, head));

    [Fact]
    public void Compose_joins_with_separator()
    {
        var package = Load();

        Assert.Equal("adm_18", PackageKey.Compose(package, new Dictionary<string, string> { ["category"] = "adm", ["group"] = "18" }));
        Assert.Null(PackageKey.Compose(package, new Dictionary<string, string> { ["category"] = "adm" }));
        Assert.Equal(new[] { "category", "group" }, package.KeyFields);
        Assert.Equal("category", package.KeyField);
    }

    [Fact]
    public void Custom_separator_is_used()
    {
        var package = Load("\"keyFields\": [ \"category\", \"group\" ], \"keySeparator\": \"-\"");

        Assert.Equal("dip-3", PackageKey.Compose(package, new Dictionary<string, string> { ["category"] = "dip", ["group"] = "3" }));
    }

    [Fact]
    public void Split_reverses_compose_last_part_keeps_rest()
    {
        var parts = PackageKey.Split(Load(), "adm_1_2");

        Assert.Equal("adm", parts["category"]);
        Assert.Equal("1_2", parts["group"]);
    }

    [Fact]
    public void Apply_sets_key_and_head_values()
    {
        var package = Load();
        var state = new GeneratorState();

        PackageKey.Apply(package, state, "dip_7");

        Assert.Equal("dip_7", state.Key);
        Assert.Equal("dip", state.Head.Values["category"]);
        Assert.Equal("7", state.Head.Values["group"]);
    }

    [Fact]
    public void Single_key_field_is_the_whole_key()
    {
        var package = Load("\"keyField\": \"title\"");
        var state = new GeneratorState();

        PackageKey.Apply(package, state, "a_b");

        Assert.Equal("a_b", state.Head.Values["title"]);
        Assert.Equal("a_b", PackageKey.Compose(package, state.Head.Values));
    }

    [Fact]
    public void Choice_value_with_separator_is_rejected()
    {
        var head = Head.Replace("\"adm\", \"dip\"", "\"adm\", \"d_ip\"");

        var error = Assert.Throws<GeneratorPackageException>(() => Load(head: head));

        Assert.Contains("d_ip", error.Message);
    }

    [Fact]
    public void Non_last_key_field_needs_pattern_or_choice()
    {
        var error = Assert.Throws<GeneratorPackageException>(() => Load("\"keyFields\": [ \"title\", \"group\" ]"));

        Assert.Contains("title", error.Message);
    }

    [Fact]
    public void Key_field_must_be_in_head() =>
        Assert.Throws<GeneratorPackageException>(() => Load("\"keyFields\": [ \"category\", \"nope\" ]"));

    [Fact]
    public void Document_saves_keyField_for_one_and_keyFields_for_two()
    {
        var document = PackageDocument.Load(Folder("\"keyField\": \"title\""));

        document.KeyFields = new[] { "category", "group" };
        Assert.Null(document.Json["keyField"]);
        Assert.Equal(2, document.Json["keyFields"]!.AsArray().Count);
        Assert.Equal(new[] { "category", "group" }, document.KeyFields);

        document.KeyFields = new[] { "title" };
        Assert.Null(document.Json["keyFields"]);
        Assert.Equal("title", document.Json["keyField"]!.GetValue<string>());
        Assert.Equal("title", document.KeyField);
    }
}
