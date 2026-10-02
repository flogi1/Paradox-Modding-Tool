using Xunit;

namespace ParadoxModTool.Localization.Tests;

public sealed class LocaleValidatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-check-" + Guid.NewGuid().ToString("N"));

    private readonly Dictionary<string, string> _reference = new()
    {
        ["a"] = "{0} Dateien",
        ["b"] = "Text",
        ["c"] = "am {1:g} von {0}",
    };

    private readonly Dictionary<string, string> _english = new()
    {
        ["a"] = "{0} files",
        ["b"] = "Text",
        ["c"] = "on {1:g} by {0}",
    };

    public LocaleValidatorTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void CountsTranslatedAndMissing()
    {
        var report = Check("{\"a\":\"{0} fichiers\",\"b\":\"\"}");

        Assert.Equal(3, report.Total);
        Assert.Equal(1, report.Translated);
        Assert.Equal(new[] { "b", "c" }, report.MissingKeys);
        Assert.Equal(33, report.Percent);
        Assert.False(report.HasErrors);
    }

    [Fact]
    public void ReportsUnknownKeys()
    {
        var report = Check("{\"a.typo\":\"x\"}");

        Assert.Equal(new[] { "a.typo" }, report.UnknownKeys);
        Assert.True(report.HasErrors);
    }

    [Fact]
    public void PlaceholderOrderMayDiffer()
    {
        Assert.Empty(Check("{\"c\":\"par {0} le {1:g}\"}").PlaceholderMismatches);
    }

    [Fact]
    public void MissingOrExtraOrReformattedPlaceholderIsAnError()
    {
        var missing = Assert.Single(Check("{\"a\":\"fichiers\"}").PlaceholderMismatches);
        Assert.Equal("a", missing.Key);
        Assert.Equal(new[] { "{0}" }, missing.Expected);
        Assert.Empty(missing.Found);

        Assert.Single(Check("{\"a\":\"{0} {1}\"}").PlaceholderMismatches);
        var reformatted = Assert.Single(Check("{\"c\":\"{0} {1}\"}").PlaceholderMismatches);
        Assert.Equal(new[] { "{0}", "{1:g}" }, reformatted.Expected);
        Assert.Equal(new[] { "{0}", "{1}" }, reformatted.Found);
        Assert.True(Check("{\"a\":\"fichiers\"}").HasErrors);
    }

    [Fact]
    public void EscapedBracesAreNotPlaceholders()
    {
        Assert.Equal(new[] { "{0}" }, LocaleValidator.Placeholders("{{0}} und {0}"));
    }

    [Fact]
    public void ParseErrorStopsFurtherChecks()
    {
        var report = Check("{ \"a\": ");

        Assert.NotNull(report.ParseError);
        Assert.Empty(report.MissingKeys);
        Assert.Empty(report.UnknownKeys);
        Assert.Equal(0, report.Translated);
        Assert.True(report.HasErrors);
    }

    [Fact]
    public void LongTextsAreOnlyHints()
    {
        _reference["long"] = "Kurzer Text";
        _english["long"] = "Short text";

        var report = Check("{\"long\":\"Un texte vraiment beaucoup trop long\",\"b\":\"Un texte bien plus long encore\"}");

        Assert.Equal(new[] { "long" }, report.LongTexts);
        Assert.False(report.HasErrors);
    }

    [Fact]
    public void MachineTranslatedFlagIsReported()
    {
        Assert.True(Check("{\"_machineTranslated\":true}").MachineTranslated);
    }

    [Fact]
    public void CheckLanguageReturnsNullWithoutFile()
    {
        File.WriteAllText(Path.Combine(_dir, "de.json"), "{\"a\":\"A\"}");
        File.WriteAllText(Path.Combine(_dir, "en.json"), "{\"a\":\"A\"}");
        var sources = new LocaleSources(_dir, null);

        Assert.Null(LocaleValidator.CheckLanguage(sources, "fr"));

        File.WriteAllText(Path.Combine(_dir, "fr.json"), "{\"a\":\"A\"}");
        Assert.Equal(100, LocaleValidator.CheckLanguage(sources, "fr")!.Percent);
    }

    private LocaleReport Check(string json)
    {
        var path = Path.Combine(_dir, "xx.json");
        File.WriteAllText(path, json);
        return LocaleValidator.Check(path, _reference, _english);
    }
}
