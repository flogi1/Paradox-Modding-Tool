using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public sealed class LocalizedTextTests
{
    [Fact]
    public void PickPrefersLanguageThenEnglishThenGermanThenFirst()
    {
        Assert.Equal("E", LocalizedText.Pick(new Dictionary<string, string> { ["de"] = "D", ["en"] = "E" }, "fr"));
        Assert.Equal("D", LocalizedText.Pick(new Dictionary<string, string> { ["de"] = "D" }, "fr"));
        Assert.Equal("S", LocalizedText.Pick(new Dictionary<string, string> { ["es"] = "S" }, "fr"));
        Assert.Null(LocalizedText.Pick(new Dictionary<string, string>(), "fr"));
        Assert.Equal("D", LocalizedText.Pick(new Dictionary<string, string> { ["de"] = "D", ["en"] = "E" }, "de"));
    }
}
