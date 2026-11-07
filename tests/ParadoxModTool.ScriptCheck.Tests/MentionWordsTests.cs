using ParadoxModTool.Scopes;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class MentionWordsTests
{
    private static void Collect(IEnumerable<ScriptNode> nodes, List<string> words)
    {
        foreach (var n in nodes)
        {
            if (n.Key != null) words.AddRange(MentionWords.Of(n.Key));
            if (n.Scalar != null) words.AddRange(MentionWords.Of(n.Scalar));
            if (n.Children != null) Collect(n.Children, words);
        }
    }

    [Theory]
    [InlineData("value:my_sv|P|1|")]
    [InlineData("script_value:my_sv")]
    [InlineData("[GetPlayer.MakeScope.ScriptValue('my_sv')]")]
    [InlineData("scope:my_sv")]
    public void CompoundValuesYieldName(string token) => Assert.Contains("my_sv", MentionWords.Of(token));

    [Theory]
    [InlineData("x = @[ my_sv * 2 ]")]
    [InlineData("x = \"[GetPlayer.MakeScope.ScriptValue('my_sv')]\"")]
    [InlineData("x = value:my_sv|P|1|")]
    public void ParsedScriptYieldsName(string script)
    {
        var words = new List<string>();
        Collect(ScriptTree.Parse(script), words);
        Assert.Contains("my_sv", words);
    }

    [Fact]
    public void EventIdStaysWhole() => Assert.Contains("ns.12", MentionWords.Of("ns.12"));

    [Fact]
    public void LongerNameIsNotMention() => Assert.DoesNotContain("my_sv", MentionWords.Of("my_sv_extra"));

    [Fact]
    public void MayMentionFindsName() =>
        Assert.True(MentionWords.MayMention("# my_sv\nx = 1", new HashSet<string> { "my_sv" }));

    [Fact]
    public void MayMentionFindsPartOfCompound() =>
        Assert.True(MentionWords.MayMention("x = value:my_sv|P|1|", new HashSet<string> { "my_sv" }));

    [Fact]
    public void MayMentionFindsEventPart() =>
        Assert.True(MentionWords.MayMention("trigger_event = ns.12", new HashSet<string> { "ns.12" }));

    [Fact]
    public void MayMentionRejectsUnrelated() =>
        Assert.False(MentionWords.MayMention("a = b", new HashSet<string> { "my_sv" }));
}
