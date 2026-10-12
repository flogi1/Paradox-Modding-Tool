using System.Text.Json.Nodes;
using ParadoxModTool.Generators.Ai;
using ParadoxModTool.Generators.Editing.Package;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>"KI-Antwort einfügen" und "Aus Ordner importieren" (Spec 2026-10-05-generator-packages-ai-guide, Abschnitt 2 "Ergebnis").</summary>
public sealed class AiResponseParserTests : IDisposable
{
    private const string Package = "{ \"id\": \"p\", \"name\": \"P\", \"gameId\": \"eu5\", \"keyField\": \"tag\", \"head\": [ { \"id\": \"tag\", \"type\": \"text\" } ], " +
        "\"outputs\": [ { \"id\": \"o\", \"kind\": \"ownFile\", \"path\": \"x_{{ tag }}.txt\", \"template\": \"a.sbn\" } ] }";

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Parses_fenced_blocks_with_prose()
    {
        var answer = "Here is your package.\n\n<<<FILE package.json>>>\n```json\n" + Package + "\n```\n<<<END>>>\n\nAnd the template:\n" +
            "```\n<<<FILE templates/a.sbn>>>\nx = {{ tag }}\n<<<END>>>\n```\nGood luck!";

        var result = AiResponseParser.Parse(answer);

        Assert.Empty(result.Problems);
        Assert.Equal(new[] { "package.json", "templates/a.sbn" }, result.Files.Keys.OrderBy(k => k));
        Assert.Equal(Package, result.Files["package.json"]);
        Assert.Equal("x = {{ tag }}", result.Files["templates/a.sbn"]);
    }

    [Fact]
    public void Crlf_is_normalized()
    {
        var result = AiResponseParser.Parse("<<<FILE package.json>>>\r\n" + Package + "\r\n<<<END>>>\r\n<<<FILE templates/a.sbn>>>\r\na\r\nb\r\n<<<END>>>\r\n");

        Assert.Equal("a\nb", result.Files["templates/a.sbn"]);
    }

    [Theory]
    [InlineData("../x.sbn")]
    [InlineData("templates/sub/a.sbn")]
    [InlineData("notes.txt")]
    [InlineData("templates\\..\\..\\a.sbn")]
    public void Other_names_are_problems(string name)
    {
        var result = AiResponseParser.Parse($"<<<FILE package.json>>>\n{Package}\n<<<END>>>\n<<<FILE {name}>>>\nx\n<<<END>>>");

        Assert.Contains(GeneratorsTexts.AiFileNotAllowed(name.Replace('\\', '/')), result.Problems);
    }

    [Fact]
    public void Duplicate_is_problem()
    {
        var result = AiResponseParser.Parse($"<<<FILE package.json>>>\n{Package}\n<<<END>>>\n<<<FILE package.json>>>\n{{}}\n<<<END>>>");

        Assert.Contains(GeneratorsTexts.AiFileDuplicate("package.json"), result.Problems);
    }

    [Fact]
    public void Missing_end_is_problem()
    {
        var result = AiResponseParser.Parse($"<<<FILE package.json>>>\n{Package}\n<<<FILE templates/a.sbn>>>\nx");

        Assert.Contains(GeneratorsTexts.AiBlockWithoutEnd("package.json"), result.Problems);
    }

    [Fact]
    public void Missing_package_json_and_no_files_are_problems()
    {
        Assert.Contains(GeneratorsTexts.AiNoPackageJson, AiResponseParser.Parse("<<<FILE templates/a.sbn>>>\nx\n<<<END>>>").Problems);
        Assert.Contains(GeneratorsTexts.AiNoFiles, AiResponseParser.Parse("Sorry, I cannot help.").Problems);
    }

    [Fact]
    public void WithGameId_sets_missing_game_and_keeps_existing()
    {
        var withoutGame = new Dictionary<string, string> { ["package.json"] = "{ \"id\": \"p\" }" };
        var withGame = new Dictionary<string, string> { ["package.json"] = Package };

        Assert.Equal("ck3", JsonNode.Parse(PackageFileSet.WithGameId(withoutGame, "ck3")["package.json"])!["gameId"]!.GetValue<string>());
        Assert.Equal("eu5", JsonNode.Parse(PackageFileSet.WithGameId(withGame, "ck3")["package.json"])!["gameId"]!.GetValue<string>());
        Assert.Equal("{ broken", PackageFileSet.WithGameId(new Dictionary<string, string> { ["package.json"] = "{ broken" }, "ck3")["package.json"]);
    }

    [Fact]
    public void Archive_round_trip_reads_back()
    {
        var files = new Dictionary<string, string> { ["package.json"] = Package, ["templates/a.sbn"] = "x = {{ tag }}" };
        var archive = _temp.PathOf("p.pmtgen");

        PackageFileSet.WriteArchive(files, archive);
        var content = GeneratorPackageArchive.Read(archive);

        Assert.Equal("p", content.PackageId);
        Assert.Equal("eu5", content.GameId);
        Assert.Equal(1, content.Templates);
    }

    [Fact]
    public void FromFolder_skips_other_files()
    {
        _temp.Write("ai/package.json", Package);
        _temp.Write("ai/templates/a.sbn", "x");
        _temp.Write("ai/README.md", "hello");
        _temp.Write("ai/templates/sub/b.sbn", "y");

        var files = PackageFileSet.FromFolder(_temp.PathOf("ai"));

        Assert.Equal(new[] { "package.json", "templates/a.sbn" }, files.Keys.OrderBy(k => k));
        Assert.Throws<GeneratorPackageException>(() => PackageFileSet.FromFolder(_temp.PathOf("empty")));
    }
}
