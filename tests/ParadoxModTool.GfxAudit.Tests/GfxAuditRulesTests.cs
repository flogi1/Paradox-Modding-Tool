using System.Text.Json;
using Xunit;

namespace ParadoxModTool.GfxAudit.Tests;

public class GfxAuditRulesTests
{
    private static readonly string[] Games = { "ck3", "vic3", "eu4", "eu5", "hoi4", "stellaris" };

    private static GfxAuditRules Rules(string id) => GfxTestRoots.Rules(id);

    [Fact]
    public void EveryGameHasRules()
    {
        foreach (var id in Games)
        {
            var r = GfxAuditRules.ForProfile(RepoFiles.Profile(id), GfxTestRoots.ProfilesDir);
            Assert.NotNull(r);
            Assert.Contains(".txt", r!.TextExtensions);
            Assert.Contains(".gui", r.TextExtensions);
            Assert.NotEmpty(r.NamePrefixes);
            Assert.NotEmpty(r.AlwaysUsedFolders);
        }
    }

    [Fact]
    public void ClassicGamesHaveSprites()
    {
        foreach (var id in new[] { "eu4", "hoi4", "stellaris" })
        {
            var r = Rules(id);
            Assert.Equal("GFX_", r.SpriteRefPrefix);
            Assert.Contains("spriteType", r.SpriteBlocks);
            Assert.Contains("corneredTileSpriteType", r.SpriteBlocks);
            Assert.Contains("texturefile", r.SpriteTextureKeys);
            Assert.Contains("animationtexturefile", r.SpriteTextureKeys);
        }

        Assert.Contains("masking_texture", Rules("stellaris").SpriteTextureKeys);
        foreach (var id in new[] { "vic3", "eu5" })
        {
            Assert.Empty(Rules(id).SpriteBlocks);
            Assert.Null(Rules(id).SpriteRefPrefix);
        }

        Assert.Equal(new[] { "spriteType" }, Rules("ck3").SpriteBlocks);
        Assert.Null(Rules("ck3").SpriteRefPrefix);
    }

    [Fact]
    public void PhaseRoots()
    {
        Assert.Equal(new[] { "", "in_game", "main_menu", "loading_screen" }, Rules("eu5").PathRoots);
        foreach (var id in new[] { "ck3", "vic3", "eu4", "hoi4", "stellaris" })
        {
            Assert.Equal(new[] { "" }, Rules(id).PathRoots);
        }

        Assert.Equal("gfx/a.dds", Rules("eu5").LogicalPath("in_game/gfx/A.dds"));
        Assert.Equal("in_game/gfx/a.dds", Rules("ck3").LogicalPath("in_game/gfx/A.dds"));
        Assert.Equal("gfx/a.dds", Rules("eu5").LogicalPath("in_game//gfx//A.dds"));
        Assert.Equal("gfx/a.dds", Rules("ck3").LogicalPath("gfx///a.dds"));
    }

    [Fact]
    public void AlwaysUsed()
    {
        Assert.True(Rules("hoi4").IsAlwaysUsed("gfx/models/units/x.dds"));
        Assert.False(Rules("hoi4").IsAlwaysUsed("gfx/interface/x.dds"));
        Assert.True(Rules("stellaris").IsAlwaysUsed("flags/x/y.dds"));
        Assert.True(Rules("eu5").IsAlwaysUsed("main_menu/gfx/models/x.dds"));
    }

    [Fact]
    public void Eu5IgnoresBroadIconFolders()
    {
        var eu5 = Rules("eu5");
        Assert.Equal(new[] { "gfx/interface/icons", "gfx/interface/advance" }, eu5.IgnoredFolderReferences);
        Assert.Contains("dip_", eu5.NamePrefixes);
        Assert.Contains(".profile", eu5.TextExtensions);
        Assert.True(eu5.IsIgnoredFolderReference("GFX/Interface/Icons/"));
        Assert.False(eu5.IsIgnoredFolderReference("gfx/interface/icons/trade_goods"));
        foreach (var id in Games.Where(g => g != "eu5" && g != "ck3"))
        {
            Assert.Empty(Rules(id).IgnoredFolderReferences);
        }
    }

    [Fact]
    public void Ck3IgnoresIllustrationAndSkinFolders()
    {
        var ck3 = Rules("ck3");
        Assert.Equal(new[] { "gfx/interface/illustrations", "gfx/interface/skinned" }, ck3.IgnoredFolderReferences);
        Assert.True(ck3.IsIgnoredFolderReference("gfx/interface/illustrations"));
        Assert.True(ck3.IsIgnoredFolderReference("gfx/interface/skinned/"));
        Assert.False(ck3.IsIgnoredFolderReference("gfx/interface/illustrations/loading_screens"));
        Assert.Contains("gfx/interface/bookmarks", ck3.AlwaysUsedFolders);
    }

    [Fact]
    public void FingerprintChangesWithIgnoredFolders()
    {
        var a = new GfxAuditRules { IgnoredFolderReferences = new[] { "gfx/a" } };
        var b = new GfxAuditRules { IgnoredFolderReferences = new[] { "gfx/b" } };
        Assert.NotEqual(a.Fingerprint, b.Fingerprint);
    }

    [Fact]
    public void FingerprintChangesWithContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gfx-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string Write(string name, string prefix)
            {
                var p = Path.Combine(dir, name);
                File.WriteAllText(p, "{ \"namePrefixes\": [\"" + prefix + "\"] }");
                return p;
            }

            var a = GfxAuditRules.Load(Write("a.json", "x"));
            var a2 = GfxAuditRules.Load(Write("a2.json", "x"));
            var b = GfxAuditRules.Load(Write("b.json", "y"));
            Assert.Equal(a.Fingerprint, a2.Fingerprint);
            Assert.NotEqual(a.Fingerprint, b.Fingerprint);
            Assert.Equal(16, a.Fingerprint.Length);

            var bad = Path.Combine(dir, "bad.json");
            File.WriteAllText(bad, "{ nope");
            Assert.ThrowsAny<JsonException>(() => GfxAuditRules.Load(bad));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
