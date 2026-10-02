using System.Text.Json;
using ParadoxModTool.LocaleTool;
using Xunit;

namespace ParadoxModTool.Localization.Tests;

public sealed class LocaleCommandsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-tool-" + Guid.NewGuid().ToString("N"));

    public LocaleCommandsTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "de.json"), "{\"a.x\":\"A\",\"b.y\":\"B {0}\",\"c\":\"C\"}");
        File.WriteAllText(Path.Combine(_dir, "en.json"), "{\"c\":\"C en\",\"a.x\":\"A en\",\"b.y\":\"B {0} en\"}");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void MissingListsGermanAndEnglishInEnglishOrder()
    {
        using var document = JsonDocument.Parse(LocaleCommands.Missing(_dir, "fr", null, 300));

        Assert.Equal(new[] { "c", "a.x", "b.y" }, document.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("B {0}", document.RootElement.GetProperty("b.y").GetProperty("de").GetString());
        Assert.Equal("B {0} en", document.RootElement.GetProperty("b.y").GetProperty("en").GetString());
    }

    [Fact]
    public void MissingHonoursPrefixAndMax()
    {
        using var prefixed = JsonDocument.Parse(LocaleCommands.Missing(_dir, "fr", "a", 300));
        using var limited = JsonDocument.Parse(LocaleCommands.Missing(_dir, "fr", null, 1));

        Assert.Equal(new[] { "a.x" }, prefixed.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(new[] { "c" }, limited.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void MissingSkipsTranslatedKeys()
    {
        File.WriteAllText(Path.Combine(_dir, "fr.json"), "{\"c\":\"Ce\",\"a.x\":\"\"}");

        using var document = JsonDocument.Parse(LocaleCommands.Missing(_dir, "fr", null, 300));

        Assert.Equal(new[] { "a.x", "b.y" }, document.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void MergeCreatesFileWithMachineFlagInEnglishOrder()
    {
        var chunk = WriteChunk("{\"b.y\":\"Б {0}\",\"c\":\"Ц\"}");

        Assert.Equal(0, LocaleCommands.Merge(_dir, "fr", chunk));

        var path = Path.Combine(_dir, "fr.json");
        var bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        var text = File.ReadAllText(path);
        Assert.Contains("Б", text);
        using var document = JsonDocument.Parse(text);
        Assert.Equal(new[] { "_machineTranslated", "c", "b.y" }, document.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.True(document.RootElement.GetProperty("_machineTranslated").GetBoolean());
    }

    [Fact]
    public void MergeKeepsExistingValuesAndCommentKeys()
    {
        File.WriteAllText(Path.Combine(_dir, "fr.json"), "{\"_note\":\"n\",\"a.x\":\"old\"}");

        Assert.Equal(0, LocaleCommands.Merge(_dir, "fr", WriteChunk("{\"c\":\"new\"}")));

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_dir, "fr.json")));
        Assert.Equal(new[] { "_note", "c", "a.x" }, document.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("old", document.RootElement.GetProperty("a.x").GetString());
    }

    [Fact]
    public void MergeAcceptsExistingFileWithBom()
    {
        File.WriteAllText(Path.Combine(_dir, "fr.json"), "{\"a.x\":\"old\"}", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Assert.Equal(0, LocaleCommands.Merge(_dir, "fr", WriteChunk("{\"c\":\"new\"}")));

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_dir, "fr.json")));
        Assert.Equal("old", document.RootElement.GetProperty("a.x").GetString());
        Assert.Equal("new", document.RootElement.GetProperty("c").GetString());
    }

    [Fact]
    public void MergeRejectsUnknownKeys()
    {
        File.WriteAllText(Path.Combine(_dir, "fr.json"), "{\"a.x\":\"old\"}");

        Assert.Equal(2, LocaleCommands.Merge(_dir, "fr", WriteChunk("{\"zz\":\"x\",\"c\":\"new\"}")));

        Assert.Equal("{\"a.x\":\"old\"}", File.ReadAllText(Path.Combine(_dir, "fr.json")));
    }

    [Fact]
    public void CheckReturnsOneOnErrors()
    {
        File.WriteAllText(Path.Combine(_dir, "fr.json"), "{\"b.y\":\"B\"}");
        var output = new StringWriter();

        Assert.Equal(1, LocaleCommands.Check(_dir, "fr", output));
        Assert.Contains("b.y", output.ToString());
    }

    [Fact]
    public void CheckWithoutCodeCoversEveryLanguageButGerman()
    {
        File.WriteAllText(Path.Combine(_dir, "fr.json"), "{\"c\":\"Ce\"}");
        var output = new StringWriter();

        Assert.Equal(0, LocaleCommands.Check(_dir, null, output));
        Assert.Contains("en: 100 %", output.ToString());
        Assert.Contains("fr: 33 % (2 missing)", output.ToString());
        Assert.DoesNotContain("de:", output.ToString());
    }

    private string WriteChunk(string json)
    {
        var path = Path.Combine(_dir, "chunk-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }
}
