using Xunit;

namespace ParadoxModTool.GfxAudit.Tests;

public class GfxNamesTests
{
    [Fact]
    public void CandidatesStripPrefixAndSuffix()
    {
        var c = GfxNames.Candidates("GFX_focus_ger_army_shine", GfxTestRoots.Rules("hoi4"));
        Assert.Contains("GFX_focus_ger_army_shine", c);
        Assert.Contains("ger_army_shine", c);
        Assert.Contains("GFX_focus_ger_army", c);
        Assert.Contains("ger_army", c);
    }

    [Fact]
    public void CandidatesLongestPrefixFirst()
    {
        var rules = new GfxAuditRules { NamePrefixes = new[] { "GFX_", "GFX_idea_" } };
        var c = GfxNames.Candidates("GFX_idea_foo", rules);
        Assert.Contains("foo", c);
        Assert.Contains("idea_foo", c);
        Assert.True(c.ToList().IndexOf("foo") < c.ToList().IndexOf("idea_foo"));
    }

    [Fact]
    public void CandidatesAreCaseInsensitiveAndKeepCasing()
    {
        var rules = new GfxAuditRules { NamePrefixes = new[] { "gfx_" } };
        Assert.Equal(new[] { "GFX_Foo", "Foo" }, GfxNames.Candidates("GFX_Foo", rules));
    }

    [Fact]
    public void ShortCandidatesDropped()
    {
        Assert.Equal(new[] { "GFX_ab" }, GfxNames.Candidates("GFX_ab", GfxTestRoots.Rules("hoi4")));
    }

    [Fact]
    public void NormalizePath()
    {
        Assert.Equal("gfx/interface/x.dds", GfxNames.NormalizePath("\"gfx\\interface\\X.dds\""));
        Assert.Null(GfxNames.NormalizePath("gfx/$icon$.dds"));
        Assert.Null(GfxNames.NormalizePath("x.dds"));
        Assert.Equal("flags/a/b.dds", GfxNames.NormalizePath("flags/a/b.dds"));
        Assert.Equal("gfx/interface/x.dds", GfxNames.NormalizePath("gfx//interface\\/x.dds"));
    }

    [Fact]
    public void WithoutExtension()
    {
        Assert.Equal("gfx/a", GfxNames.WithoutExtension("gfx/a.TGA"));
        Assert.Equal("gfx/a", GfxNames.WithoutExtension("gfx/a"));
    }

    [Fact]
    public void Stem()
    {
        Assert.Equal("x", GfxNames.Stem("gfx/interface/x.dds"));
    }

    [Fact]
    public void NormalizeSpriteTexture()
    {
        // Sprite-Eigenschaften sind eine eindeutige Form: kein Präfix nötig, führender "/" entfällt.
        Assert.Equal("tutorial/task_icon.dds", GfxNames.NormalizeSpriteTexture("\"tutorial\\\\Task_Icon.dds\""));
        Assert.Equal("gfx/interface/x.dds", GfxNames.NormalizeSpriteTexture("/gfx//interface\\x.dds"));
        Assert.Equal("pdx_launcher/a.dds", GfxNames.NormalizeSpriteTexture("pdx_launcher/a.dds"));
        Assert.Null(GfxNames.NormalizeSpriteTexture("gfx/$icon$.dds"));
        Assert.Null(GfxNames.NormalizeSpriteTexture("gfx/[x].dds"));
        Assert.Null(GfxNames.NormalizeSpriteTexture("@icon"));
        Assert.Null(GfxNames.NormalizeSpriteTexture("\"\""));
        Assert.Null(GfxNames.NormalizeSpriteTexture("/"));
    }
}
