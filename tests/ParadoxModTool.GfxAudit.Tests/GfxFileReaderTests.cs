using Xunit;

namespace ParadoxModTool.GfxAudit.Tests;

public class GfxFileReaderTests
{
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Names(params (string Candidate, string Name)[] entries)
    {
        var dict = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (candidate, name) in entries)
        {
            dict[candidate] = new[] { name };
        }

        return dict;
    }

    [Fact]
    public void ReadsClassicSprites()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("eu4"));
        var text = "spriteTypes = {\n    spriteType = {\n        name = \"GFX_a\"\n        texturefile = \"gfx/interface/a.tga\"\n    }\n" +
                   "    frameAnimatedSpriteType = {\n        name = GFX_b\n        animationtexturefile = \"gfx/b.dds\"\n    }\n}\n";
        var scan = reader.Read("interface/x.gfx", text);

        Assert.Equal(new[] { new GfxSprite("GFX_a", new[] { new GfxRef("gfx/interface/a.tga", 4) }, 2), new GfxSprite("GFX_b", new[] { new GfxRef("gfx/b.dds", 8) }, 6) }, scan.Sprites);
        Assert.Empty(scan.TexturePaths);
    }

    [Fact]
    public void ReadsAllSpriteTextures()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("hoi4"));
        var text = "spriteTypes = {\n\tprogressbartype = {\n\t\tname = \"GFX_bar\"\n\t\ttextureFile1 = \"gfx//bar_fill.dds\"\n\t\ttextureFile2 = \"gfx/bar_bg.dds\"\n" +
                   "\t\ttextureFile3 = \"gfx/$x$.dds\"\n\t}\n}\n";
        var scan = reader.Read("interface/x.gfx", text);

        Assert.Equal(new[] { new GfxSprite("GFX_bar", new[] { new GfxRef("gfx/bar_fill.dds", 4), new GfxRef("gfx/bar_bg.dds", 5) }, 2) }, scan.Sprites);
    }

    [Fact]
    public void SpriteBlocksOnlyInGfxFiles()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("hoi4"));
        var text = "guiTypes = {\n\tprogressbarType = {\n\t\tname = \"x\"\n\t\ttexturefile = \"gfx/a.dds\"\n\t\tquadTextureSprite = GFX_bar\n\t}\n}\n";

        var scan = reader.Read("interface/x.gui", text);
        var mentions = reader.ReadMentions("interface/x.gui", text, Names(("a", "gfx/a.dds"), ("GFX_bar", "GFX_bar")));

        Assert.Empty(scan.Sprites);
        Assert.Equal(new[] { new GfxRef("gfx/a.dds", 4) }, scan.TexturePaths);
        Assert.Equal(new[] { new GfxRef("GFX_bar", 5) }, scan.SpriteRefs);
        Assert.Contains(new GfxMention("gfx/a.dds", 4), mentions);
        Assert.Contains(new GfxMention("GFX_bar", 5), mentions);

        var gfx = reader.Read("interface/x.gfx", text);
        Assert.Equal("x", Assert.Single(gfx.Sprites).Name);
        Assert.Empty(gfx.TexturePaths);
    }

    [Fact]
    public void ReadsJominiPaths()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("ck3"));
        var text = "widget = {\n  texture = \"gfx/interface/x.dds\"\n  icon = \"gfx/interface/y\"\n  texture = \"gfx/$name$.dds\"\n  folder = \"gfx/interface/\"\n}\n";
        var scan = reader.Read("gui/a.gui", text);

        Assert.Equal(new[] { new GfxRef("gfx/interface/x.dds", 2), new GfxRef("gfx/interface/y", 3) }, scan.TexturePaths);
        Assert.Empty(scan.SpriteRefs);
    }

    [Fact]
    public void ReadsSpriteRefs()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("hoi4"));
        var scan = reader.Read("common/x.txt", "focus = {\n  icon = GFX_focus_x\n  picture = GFX_[Root.GetFlag]\n}\n");

        Assert.Equal(new[] { new GfxRef("GFX_focus_x", 2) }, scan.SpriteRefs);
    }

    [Fact]
    public void SpriteRefEndingInUnderscoreIsNameStart()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("eu4"));
        var scan = reader.Read("common/professionalism/00_modifiers.txt", "m = {\n  unit_sprite_start = \"GFX_ap1_\"\n  picture = GFX_\n  marker_sprite = GFX_pa_rank_0\n}\n");

        Assert.Equal(new[] { new GfxRef("GFX_pa_rank_0", 4) }, scan.SpriteRefs);
    }

    [Fact]
    public void NoSpriteRefsWithoutPrefix()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("ck3"));
        Assert.Empty(reader.Read("common/x.txt", "icon = GFX_focus_x\n").SpriteRefs);
    }

    [Fact]
    public void MentionsSkipSpriteBlocks()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("hoi4"));
        var text = "spriteTypes = {\n  spriteType = {\n    name = \"GFX_a\"\n    texturefile = \"gfx/a.dds\"\n  }\n}\n";
        var mentions = reader.ReadMentions("interface/a.gfx", text, Names(("a", "gfx/a.dds"), ("gfx_a", "GFX_a")));

        Assert.Empty(mentions);
    }

    [Fact]
    public void MentionsInScriptKeysAndValuesCaseInsensitive()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("hoi4"));
        var mentions = reader.ReadMentions("common/x.txt", "Brave = {\n  icon = gfx_Focus_x\n}\n", Names(("brave", "brave"), ("GFX_focus_x", "GFX_focus_x")));

        Assert.Equal(new[] { new GfxMention("brave", 1), new GfxMention("GFX_focus_x", 2) }, mentions);
    }

    [Fact]
    public void MentionsOncePerNameAndLine()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("hoi4"));
        var mentions = reader.ReadMentions("common/x.txt", "a = brave\nb = { brave brave }\n", Names(("brave", "brave")));

        Assert.Equal(new[] { new GfxMention("brave", 1), new GfxMention("brave", 2) }, mentions);
    }

    [Fact]
    public void MentionsInLoc()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("stellaris"));
        var mentions = reader.ReadMentions("localisation/english/a_l_english.yml", "l_english:\n KEY:0 \"£energy£ text\"\n", Names(("energy", "GFX_text_energy")));

        Assert.Equal(new[] { new GfxMention("GFX_text_energy", 2) }, mentions);
    }

    [Fact]
    public void LocKeyBeforeColonAndCommentsIgnored()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("stellaris"));
        var text = "l_english:\n energy:0 \"nothing\"\n # K:0 \"£energy£\"\n";
        Assert.Empty(reader.ReadMentions("a_l_english.yml", text, Names(("energy", "GFX_text_energy"))));
        Assert.Empty(reader.Read("a_l_english.yml", text).TexturePaths);
    }

    [Fact]
    public void MentionsByPathWithOddName()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("ck3"));
        var mentions = reader.ReadMentions("gui/a.gui", "w = {\n  texture = \"gfx/my icon-1.dds\"\n}\n", Names(("gfx/my icon-1.dds", "gfx/my icon-1.dds")));

        Assert.Equal(new[] { new GfxMention("gfx/my icon-1.dds", 2) }, mentions);
    }

    [Fact]
    public void MentionsByPathWithoutExtension()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("ck3"));
        var mentions = reader.ReadMentions("gui/a.gui", "icon = \"gfx/my icon/x\"\n", Names(("gfx/my icon/x", "gfx/my icon/x.dds")));

        Assert.Equal(new[] { new GfxMention("gfx/my icon/x.dds", 1) }, mentions);
    }

    [Fact]
    public void CommentsIgnored()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("ck3"));
        var text = "# texture = \"gfx/a.dds\"\n";

        Assert.Empty(reader.Read("gui/a.gui", text).TexturePaths);
        Assert.Empty(reader.ReadMentions("gui/a.gui", text, Names(("a", "gfx/a.dds"), ("gfx/a.dds", "gfx/a.dds"))));
    }

    [Fact]
    public void SpriteBlockNamesAreCaseInsensitive()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("hoi4"));
        var scan = reader.Read("interface/a.gfx", "SPRITETYPE = { NAME = GFX_a TextureFile = \"GFX/A.dds\" }\n");

        Assert.Equal(new[] { new GfxSprite("GFX_a", new[] { new GfxRef("gfx/a.dds", 1) }, 1) }, scan.Sprites);
    }

    [Fact]
    public void MalformedFileDoesNotThrow()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("hoi4"));
        var text = "a = { icon = GFX_x\n b = { texture = \"gfx/a.dds\"\n";

        var scan = reader.Read("common/x.txt", text);
        Assert.Contains(new GfxRef("GFX_x", 1), scan.SpriteRefs);
        Assert.Contains(new GfxRef("gfx/a.dds", 2), scan.TexturePaths);
        reader.Read("common/y.txt", "} } = = {");
        reader.ReadMentions("common/y.txt", "} } = = {", Names(("x", "x")));
    }

    [Fact]
    public void TextFileExtensionsCaseInsensitive()
    {
        var reader = new GfxFileReader(new GfxAuditRules { TextExtensions = new[] { ".GFX" } });
        Assert.True(reader.IsTextFile("interface/a.gfx"));
        Assert.True(reader.IsTextFile("interface/a.GFX"));
        Assert.False(reader.IsTextFile("interface/a.dds"));
    }

    [Fact]
    public void SpriteTexturesWithoutGfxPrefix()
    {
        var reader = new GfxFileReader(GfxTestRoots.Rules("eu4"));
        var text = "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_taskicon\"\n\t\ttexturefile = \"tutorial\\\\task_icon.dds\"\n\t}\n" +
                   "\tspriteType = {\n\t\tname = \"GFX_lead\"\n\t\ttexturefile = \"/gfx/interface/lead.dds\"\n\t}\n}\n";
        var scan = reader.Read("interface/tutorial.gfx", text);

        Assert.Equal(new[]
        {
            new GfxSprite("GFX_taskicon", new[] { new GfxRef("tutorial/task_icon.dds", 4) }, 2),
            new GfxSprite("GFX_lead", new[] { new GfxRef("gfx/interface/lead.dds", 8) }, 6),
        }, scan.Sprites);
        Assert.Empty(scan.TexturePaths);
    }
}
