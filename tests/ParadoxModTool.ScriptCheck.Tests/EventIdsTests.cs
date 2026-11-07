using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class EventIdsTests
{
    [Fact]
    public void LeadingZerosIgnored() => Assert.Equal(EventIds.Key("ns.1"), EventIds.Key("ns.0001"));

    [Fact]
    public void CaseIgnored() => Assert.Equal(EventIds.Key("NSB_news.2"), EventIds.Key("nsb_news.0002"));

    [Fact]
    public void NonNumericCaseIgnored() => Assert.Equal(EventIds.Key("NS.A1"), EventIds.Key("ns.a1"));

    [Fact]
    public void ZeroStays() => Assert.Equal("ns.0", EventIds.Key("ns.0"));

    [Fact]
    public void AllZerosBecomeZero() => Assert.Equal("ns.0", EventIds.Key("ns.000"));

    [Fact]
    public void AllDigitIds() => Assert.Equal("42", EventIds.Key("0042"));

    [Fact]
    public void NonNumericSuffixUnchanged() => Assert.Equal("ns.a1", EventIds.Key("ns.a1"));

    [Fact]
    public void EmptySuffixUnchanged() => Assert.Equal("ns.", EventIds.Key("ns."));
}
