using System.Text;
using Xunit;

namespace ParadoxModTool.Localization.Tests;

public sealed class UiTextTableTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-loc-" + Guid.NewGuid().ToString("N"));
    private readonly string _program;
    private readonly string _user;
    private readonly LocaleSources _sources;

    public UiTextTableTests()
    {
        _program = Path.Combine(_root, "program");
        _user = Path.Combine(_root, "user");
        Directory.CreateDirectory(_program);
        Directory.CreateDirectory(_user);
        File.WriteAllText(Path.Combine(_program, "de.json"), "{\"a\":\"Hallo\",\"b\":\"Nur Deutsch\",\"fmt\":\"{0} Dateien\"}");
        File.WriteAllText(Path.Combine(_program, "en.json"), "{\"a\":\"Hello\",\"c\":\"Only English\",\"fmt\":\"{0} files\"}");
        _sources = new LocaleSources(_program, _user);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ChainIsSelectedThenEnglishThenInline()
    {
        File.WriteAllText(Path.Combine(_program, "fr.json"), "{\"a\":\"Bonjour\"}");

        var table = UiTextTable.Load(_sources, "fr");

        Assert.Equal("fr", table.Language);
        Assert.Equal("Bonjour", table.Get("a", "x"));
        Assert.Equal("Only English", table.Get("c", "x"));
        // de.json ist nur die Sprache für Deutsch, kein Rückfall: ein Text, den weder fr noch en haben, kommt aus dem Code.
        Assert.Equal("x", table.Get("b", "x"));
        Assert.Equal("Rückfall", table.Get("missing", "Rückfall"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NoLanguageChosenUsesEnglish(string? language)
    {
        var table = UiTextTable.Load(_sources, language);

        Assert.Equal("en", table.Language);
        Assert.Equal("Hello", table.Get("a", "x"));
        Assert.Equal("x", table.Get("b", "x"));
    }

    [Fact]
    public void TableWithoutLoadedTextsReportsEnglish()
    {
        Assert.Equal("en", UiTextTable.Empty.Language);
    }

    [Fact]
    public void GermanSelectionStillReadsGerman()
    {
        Assert.Equal("Hallo", UiTextTable.Load(_sources, "de").Get("a", "x"));
        Assert.Equal("Nur Deutsch", UiTextTable.Load(_sources, "de").Get("b", "x"));
    }

    [Fact]
    public void GermanSelectionSkipsEnglish()
    {
        Assert.Equal("inline", UiTextTable.Load(_sources, "de").Get("c", "inline"));
    }

    [Fact]
    public void UserFileBeatsProgramFile()
    {
        File.WriteAllText(Path.Combine(_program, "fr.json"), "{\"a\":\"Programme\"}");
        File.WriteAllText(Path.Combine(_user, "fr.json"), "{\"a\":\"Utilisateur\"}");

        Assert.Equal("Utilisateur", UiTextTable.Load(_sources, "fr").Get("a", "x"));
    }

    [Fact]
    public void UserGermanOrEnglishNeverBecomesTheFallback()
    {
        File.WriteAllText(Path.Combine(_user, "en.json"), "{\"c\":\"USER\"}");
        File.WriteAllText(Path.Combine(_user, "de.json"), "{\"b\":\"USER\"}");
        File.WriteAllText(Path.Combine(_program, "fr.json"), "{}");

        var table = UiTextTable.Load(_sources, "fr");

        Assert.Equal("Only English", table.Get("c", "x"));
        Assert.Equal("x", table.Get("b", "x"));
    }

    [Fact]
    public void BrokenPatternFallsBackToEnglishPattern()
    {
        File.WriteAllText(Path.Combine(_program, "fr.json"), "{\"fmt\":\"{1 fichiers\"}");

        Assert.Equal("3 files", UiTextTable.Load(_sources, "fr").Format("fmt", "{0} x", 3));
    }

    [Fact]
    public void ParseErrorIsKeptWithOneBasedLineAndPosition()
    {
        File.WriteAllText(Path.Combine(_program, "fr.json"), "{\n  \"a\": \"x\",\n}");

        var table = UiTextTable.Load(_sources, "fr");

        Assert.NotNull(table.LoadError);
        Assert.Equal(3, table.LoadError!.Line);
        Assert.True(table.LoadError.Position >= 1);
        Assert.False(string.IsNullOrWhiteSpace(table.LoadError.Message));
        Assert.Equal("Hello", table.Get("a", "x"));
    }

    [Fact]
    public void ReadsFilesWithBom()
    {
        File.WriteAllText(Path.Combine(_program, "fr.json"), "{\"a\":\"Bonjour\"}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var table = UiTextTable.Load(_sources, "fr");

        Assert.Null(table.LoadError);
        Assert.Equal("Bonjour", table.Get("a", "x"));
    }

    [Fact]
    public void EmptyValueFallsBackToEnglish()
    {
        File.WriteAllText(Path.Combine(_program, "fr.json"), "{\"a\":\"\"}");

        Assert.Equal("Hello", UiTextTable.Load(_sources, "fr").Get("a", "x"));
    }

    [Fact]
    public void MissingSelectedFileFallsBackToEnglish()
    {
        var table = UiTextTable.Load(_sources, "fr");

        Assert.Null(table.LoadError);
        Assert.Equal("Hello", table.Get("a", "x"));
    }

    [Fact]
    public void DuplicateKeysKeepLastValue()
    {
        File.WriteAllText(Path.Combine(_program, "fr.json"), "{\"a\":\"1\",\"a\":\"2\"}");

        var table = UiTextTable.Load(_sources, "fr");

        Assert.Null(table.LoadError);
        Assert.Equal("2", table.Get("a", "x"));
    }

    [Fact]
    public void MachineTranslatedFlagIsRead()
    {
        var path = Path.Combine(_program, "fr.json");
        File.WriteAllText(path, "{\"_machineTranslated\":true,\"a\":\"x\"}");

        var file = UiTextTable.ReadFile(path);

        Assert.True(file.MachineTranslated);
        Assert.False(file.Texts.ContainsKey("_machineTranslated"));
        Assert.Equal("x", file.Texts["a"]);
    }

    [Fact]
    public void ResolvePrefersUserThenProgramThenNull()
    {
        File.WriteAllText(Path.Combine(_program, "fr.json"), "{}");
        File.WriteAllText(Path.Combine(_program, "es.json"), "{}");
        File.WriteAllText(Path.Combine(_user, "es.json"), "{}");

        Assert.Equal((Path.Combine(_program, "fr.json"), LocaleOrigin.Program), _sources.Resolve("fr"));
        Assert.Equal((Path.Combine(_user, "es.json"), LocaleOrigin.User), _sources.Resolve("es"));
        Assert.Null(_sources.Resolve("pl"));
        Assert.Null(new LocaleSources(_program, null).Resolve("pl"));
        Assert.Equal(Path.Combine(_program, "de.json"), _sources.ProgramFile("de"));
    }
}
