using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;
using Xunit;

namespace ParadoxModTool.GfxAudit.Tests;

public sealed class GfxAuditAnalyzerTests : IDisposable
{
    private readonly GfxTestRoots _roots = new();

    public void Dispose() => _roots.Dispose();

    private static string Sprite(string name, string texture) =>
        "spriteTypes = {\n\tspriteType = {\n\t\tname = \"" + name + "\"\n\t\ttexturefile = \"" + texture + "\"\n\t}\n}\n";

    private GfxAuditResult Analyze(string game)
    {
        var rules = GfxTestRoots.Rules(game);
        var index = new GfxAuditBuilder(_roots.Sources(game), rules).Build();
        return GfxAuditAnalyzer.Analyze(index, rules);
    }

    private static List<GfxFinding> Of(GfxAuditResult result, GfxFindingKind kind) => result.Findings.Where(f => f.Kind == kind).ToList();

    [Fact]
    public void UnusedImageReported()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/old_thing.dds", "dds");

        var result = Analyze("ck3");

        var finding = Assert.Single(result.Findings);
        Assert.Equal(GfxFindingKind.UnusedImage, finding.Kind);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal("gfx/interface/old_thing.dds", finding.Name);
        Assert.Equal(new SiteLocation(_roots.Mod, "gfx/interface/old_thing.dds", 0), finding.Location);
        Assert.Empty(finding.Related);
        Assert.Equal("UnusedImage|gfx/interface/old_thing.dds", finding.Key);
    }

    [Fact]
    public void ImageUsedByStemWord()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/icons/traits/brave.dds", "dds");
        GfxTestRoots.Write(_roots.Vanilla, "common/traits/00_traits.txt", "brave = {\n}\n");

        var result = Analyze("ck3");

        Assert.Empty(Of(result, GfxFindingKind.UnusedImage));
    }

    [Fact]
    public void UnusedSpriteHidesItsImage()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/old.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", Sprite("GFX_old", "gfx/old.dds"));

        var result = Analyze("eu4");

        var sprite = Assert.Single(Of(result, GfxFindingKind.UnusedSprite));
        Assert.Equal("GFX_old", sprite.Name);
        Assert.Equal(FindingSeverity.Warning, sprite.Severity);
        Assert.Equal(new SiteLocation(_roots.Mod, "interface/m.gfx", 2), sprite.Location);
        Assert.Empty(sprite.Related);
        var image = Assert.Single(Of(result, GfxFindingKind.UnusedImage));
        Assert.Equal("gfx/old.dds", image.Name);
        Assert.Equal(new[] { "GFX_old" }, image.Related);
    }

    [Fact]
    public void ForeignSpriteKeepsImageUsed()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/shared_pic.dds", "dds");
        GfxTestRoots.Write(_roots.Other, "interface/o.gfx", Sprite("GFX_other_pic", "gfx/shared_pic.dds"));

        var result = Analyze("eu4");

        Assert.Empty(Of(result, GfxFindingKind.UnusedImage));
    }

    [Fact]
    public void AllSpriteTexturesCount()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/bar_bg.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/bar.gfx",
            "spriteTypes = {\n\tprogressbartype = {\n\t\tname = \"GFX_my_bar\"\n\t\ttextureFile1 = \"gfx/interface/bar_fill.dds\"\n\t\ttextureFile2 = \"gfx/interface/bar_bg.dds\"\n\t}\n}\n");
        GfxTestRoots.Write(_roots.Mod, "interface/bar.gui", "guiTypes = {\n\ticonType = {\n\t\tname = \"bar\"\n\t\tspriteType = \"GFX_my_bar\"\n\t}\n}\n");

        var result = Analyze("hoi4");

        Assert.Empty(Of(result, GfxFindingKind.UnusedImage));
        var missing = Assert.Single(Of(result, GfxFindingKind.MissingTexture));
        Assert.Equal("gfx/interface/bar_fill.dds", missing.Name);
        Assert.Equal(new SiteLocation(_roots.Mod, "interface/bar.gfx", 4), missing.Location);
        Assert.Equal(new[] { "GFX_my_bar" }, missing.Related);
    }

    [Fact]
    public void RepeatedSlashesCollapse()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/slashy.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", Sprite("GFX_button_go", "gfx//interface///slashy.dds"));
        GfxTestRoots.Write(_roots.Mod, "interface/m.gui", "guiTypes = {\n\ticonType = {\n\t\tname = \"go\"\n\t\tspriteType = \"GFX_button_go\"\n\t}\n}\n");

        var result = Analyze("eu4");

        Assert.Empty(Of(result, GfxFindingKind.MissingTexture));
        Assert.Empty(Of(result, GfxFindingKind.UnusedImage));
    }

    [Fact]
    public void MixedCaseTextureMatchesImage()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/Interface/Old.DDS", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", Sprite("GFX_button_go", "gfx/interface/old.dds"));
        GfxTestRoots.Write(_roots.Mod, "interface/m.gui", "guiTypes = {\n\ticonType = {\n\t\tname = \"go\"\n\t\tspriteType = \"GFX_button_go\"\n\t}\n}\n");

        var result = Analyze("eu4");

        Assert.Empty(Of(result, GfxFindingKind.MissingTexture));
        Assert.Empty(Of(result, GfxFindingKind.UnusedImage));
    }

    [Fact]
    public void ReplacedSpriteIsUsed()
    {
        GfxTestRoots.Write(_roots.Vanilla, "interface/v.gfx", Sprite("GFX_engine", "gfx/engine.dds"));
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", Sprite("GFX_engine", "gfx/mine.dds"));
        GfxTestRoots.Write(_roots.Mod, "gfx/mine.dds", "dds");

        var result = Analyze("hoi4");

        Assert.Empty(Of(result, GfxFindingKind.UnusedSprite));
        Assert.Empty(Of(result, GfxFindingKind.UnusedImage));
    }

    [Fact]
    public void FolderReferenceKeepsImagesUsed()
    {
        GfxTestRoots.Write(_roots.Vanilla, "gui/x.gui", "icons = {\n\ttexture = \"gfx/interface/myfolder\"\n}\n");
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/myfolder/a.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/myfolder_other/b.dds", "dds");

        var result = Analyze("ck3");

        // Nur Bilder unter dem Ordner selbst; ein Ordner mit gleichem Namensanfang zählt nicht.
        var unused = Assert.Single(Of(result, GfxFindingKind.UnusedImage));
        Assert.Equal("gfx/interface/myfolder_other/b.dds", unused.Name);
    }

    [Fact]
    public void IgnoredFolderReferenceDoesNotKeepImagesUsed()
    {
        GfxTestRoots.Write(_roots.Vanilla, "loading_screen/common/defines/graphic/00_graphics.txt",
            "NGraphics = {\n\tICON_PATH = \"gfx/interface/icons\"\n\tTRADE_GOODS_ICON_PATH = \"gfx/interface/icons/trade_goods\"\n}\n");
        GfxTestRoots.Write(_roots.Mod, "in_game/gfx/interface/icons/old_icon.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "in_game/gfx/interface/icons/trade_goods/salt.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "in_game/gfx/interface/icons/my_mod_icon.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "in_game/gui/my.gui", "icon = my_mod_icon\n");

        var result = Analyze("eu5");

        // Der breite Ordner ICON_PATH zählt nicht (Regel), der engere Trade-Goods-Ordner schon; my_mod_icon ist namentlich genannt.
        var unused = Assert.Single(Of(result, GfxFindingKind.UnusedImage));
        Assert.Equal("gfx/interface/icons/old_icon.dds", unused.Name);
    }

    [Fact]
    public void EnginePrefixDipKeepsInteractionIconUsed()
    {
        GfxTestRoots.Write(_roots.Mod, "in_game/common/country_interactions/x.txt", "my_interaction = {\n}\n");
        GfxTestRoots.Write(_roots.Mod, "in_game/gfx/interface/icons/diplomatic_actions/dip_my_interaction.dds", "dds");

        var result = Analyze("eu5");

        Assert.Empty(Of(result, GfxFindingKind.UnusedImage));
    }

    [Fact]
    public void PrefixRuleKeepsSpriteUsed()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/ideas/idea_pic.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/ideas.gfx", Sprite("GFX_idea_my_idea", "gfx/interface/ideas/idea_pic.dds"));
        GfxTestRoots.Write(_roots.Mod, "common/ideas/my.txt", "ideas = {\n\tcountry = {\n\t\tmy_idea = {\n\t\t\tpicture = my_idea\n\t\t}\n\t}\n}\n");

        var result = Analyze("hoi4");

        Assert.Empty(Of(result, GfxFindingKind.UnusedSprite));
        Assert.Empty(Of(result, GfxFindingKind.UnusedImage));
    }

    [Fact]
    public void AlwaysUsedFolder()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/models/x.dds", "dds");

        var result = Analyze("hoi4");

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void OverrideIsUsed()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/override_me.dds", "dds");
        GfxTestRoots.Write(_roots.Vanilla, "gfx/interface/override_me.dds", "dds");

        var result = Analyze("ck3");

        Assert.Empty(Of(result, GfxFindingKind.UnusedImage));
    }

    [Fact]
    public void PhaseFoldersResolve()
    {
        GfxTestRoots.Write(_roots.Mod, "in_game/gfx/interface/a_icon.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "main_menu/gui/m.gui", "icon = {\n\ttexture = \"gfx/interface/a_icon.dds\"\n}\n");
        GfxTestRoots.Write(_roots.Vanilla, "main_menu/gfx/interface/b_icon.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "in_game/gui/g.gui", "icon = {\n\ttexture = \"gfx/interface/b_icon.dds\"\n}\n");

        var result = Analyze("eu5");

        Assert.Empty(Of(result, GfxFindingKind.MissingTexture));
        Assert.Empty(Of(result, GfxFindingKind.UnusedImage));
    }

    [Fact]
    public void ExtensionSwapAndNoExtension()
    {
        // EU IV: Sprite nennt .tga, die Datei ist .dds; das Bild ist nur über den benutzten Sprite benutzt (Dateiname ≠ Sprite-Name).
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/xicon.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", Sprite("GFX_button_ok", "gfx/interface/xicon.tga"));
        GfxTestRoots.Write(_roots.Mod, "interface/m.gui", "guiTypes = {\n\ticonType = {\n\t\tname = \"ok\"\n\t\tspriteType = \"GFX_button_ok\"\n\t}\n}\n");

        var eu4 = Analyze("eu4");

        Assert.Empty(Of(eu4, GfxFindingKind.MissingTexture));
        Assert.Empty(Of(eu4, GfxFindingKind.UnusedImage));
        Assert.Empty(Of(eu4, GfxFindingKind.UnusedSprite));
    }

    [Fact]
    public void NoExtensionPathResolves()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/yicon.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "gui/y.gui", "icon = {\n\ttexture = \"gfx/interface/yicon\"\n}\n");

        var ck3 = Analyze("ck3");

        Assert.Empty(Of(ck3, GfxFindingKind.MissingTexture));
        Assert.Empty(Of(ck3, GfxFindingKind.UnusedImage));
    }

    [Fact]
    public void FolderValueIsNotMissing()
    {
        GfxTestRoots.Write(_roots.Vanilla, "gfx/interface/icons/a.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "gui/f.gui", "icons = {\n\ttexture = \"gfx/interface/icons\"\n}\n");

        var result = Analyze("ck3");

        Assert.Empty(Of(result, GfxFindingKind.MissingTexture));
    }

    [Fact]
    public void MissingTextureAndSprite()
    {
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", Sprite("GFX_ghost", "gfx/none.dds"));
        GfxTestRoots.Write(_roots.Vanilla, "interface/v.gfx", Sprite("GFX_there", "gfx/there.dds"));
        GfxTestRoots.Write(_roots.Vanilla, "gfx/there.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/m.gui",
            "guiTypes = {\n\ticonType = {\n\t\tspriteType = GFX_ghost\n\t\ticon = GFX_nowhere\n\t\tother = GFX_THERE\n\t\ttexture = \"gfx/gone.dds\"\n\t\tpic = \"gfx/$x$.dds\"\n\t}\n}\n");

        var result = Analyze("hoi4");

        var missing = Of(result, GfxFindingKind.MissingTexture);
        Assert.Equal(2, missing.Count);
        var fromSprite = Assert.Single(missing, f => f.Name == "gfx/none.dds");
        Assert.Equal(FindingSeverity.Error, fromSprite.Severity);
        Assert.Equal(new SiteLocation(_roots.Mod, "interface/m.gfx", 4), fromSprite.Location);
        Assert.Equal(new[] { "GFX_ghost" }, fromSprite.Related);
        Assert.Equal("MissingTexture|gfx/none.dds", fromSprite.Key);
        var fromPath = Assert.Single(missing, f => f.Name == "gfx/gone.dds");
        Assert.Equal(new SiteLocation(_roots.Mod, "interface/m.gui", 6), fromPath.Location);
        Assert.Empty(fromPath.Related);

        var sprite = Assert.Single(Of(result, GfxFindingKind.MissingSprite));
        Assert.Equal("GFX_nowhere", sprite.Name);
        Assert.Equal(FindingSeverity.Error, sprite.Severity);
        Assert.Equal(new SiteLocation(_roots.Mod, "interface/m.gui", 4), sprite.Location);
        Assert.Empty(sprite.Related);

        // Sortiert nach Schwere, dann Pfad und Zeile.
        Assert.Equal(FindingSeverity.Error, result.Findings[0].Severity);
        Assert.Equal(new[] { "interface/m.gfx", "interface/m.gui", "interface/m.gui" },
            result.Findings.Where(f => f.Severity == FindingSeverity.Error).Select(f => f.Location.RelativePath));
        Assert.Equal(new[] { 4, 6 }, result.Findings.Where(f => f.Location.RelativePath == "interface/m.gui").Select(f => f.Location.Line));
    }

    [Fact]
    public void NoMissingSpriteWithoutPrefix()
    {
        GfxTestRoots.Write(_roots.Mod, "gui/x.gui", "icon = {\n\ttexture = GFX_nowhere\n}\n");

        var result = Analyze("ck3");

        Assert.Empty(Of(result, GfxFindingKind.MissingSprite));
    }

    [Fact]
    public void DuplicateRules()
    {
        // Mod definiert GFX_d zweimal.
        GfxTestRoots.Write(_roots.Mod, "interface/ab_mod.gfx", Sprite("GFX_d", "gfx/d.dds"));
        GfxTestRoots.Write(_roots.Mod, "interface/ac_mod.gfx", Sprite("GFX_d", "gfx/d.dds"));
        // Mod-Datei lädt vor der Vanilla-Datei: Vanilla gewinnt.
        GfxTestRoots.Write(_roots.Mod, "interface/aa_mod.gfx", Sprite("GFX_v", "gfx/v.dds"));
        GfxTestRoots.Write(_roots.Vanilla, "interface/zz_van.gfx", Sprite("GFX_v", "gfx/v.dds"));
        // Mod-Datei lädt nach der Vanilla-Datei: die Mod gewinnt, kein Fund.
        GfxTestRoots.Write(_roots.Mod, "interface/zz_mod.gfx", Sprite("GFX_w", "gfx/w.dds"));
        GfxTestRoots.Write(_roots.Vanilla, "interface/aa_van.gfx", Sprite("GFX_w", "gfx/w.dds"));

        var result = Analyze("hoi4");

        var duplicates = Of(result, GfxFindingKind.DuplicateSprite);
        Assert.Equal(new[] { "GFX_d", "GFX_v" }, duplicates.Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.All(duplicates, f => Assert.Equal(FindingSeverity.Warning, f.Severity));

        var d = Assert.Single(duplicates, f => f.Name == "GFX_d");
        Assert.Equal(new[] { "interface/ac_mod.gfx:2 (My Mod)", "interface/ab_mod.gfx:2 (My Mod)" }, d.Related);
        Assert.Equal(new SiteLocation(_roots.Mod, "interface/ab_mod.gfx", 2), d.Location);

        var v = Assert.Single(duplicates, f => f.Name == "GFX_v");
        Assert.Equal(new[] { "interface/zz_van.gfx:2 (Vanilla)", "interface/aa_mod.gfx:2 (My Mod)" }, v.Related);
        Assert.Equal(new SiteLocation(_roots.Mod, "interface/aa_mod.gfx", 2), v.Location);
        Assert.Equal("DuplicateSprite|GFX_v", v.Key);
    }

    [Fact]
    public void DuplicateModTwiceVanillaWins()
    {
        GfxTestRoots.Write(_roots.Mod, "interface/aa_mod.gfx", Sprite("GFX_t", "gfx/t.dds"));
        GfxTestRoots.Write(_roots.Mod, "interface/ab_mod.gfx", Sprite("GFX_t", "gfx/t.dds"));
        GfxTestRoots.Write(_roots.Vanilla, "interface/zz_van.gfx", Sprite("GFX_t", "gfx/t.dds"));

        var result = Analyze("hoi4");

        var t = Assert.Single(Of(result, GfxFindingKind.DuplicateSprite));
        Assert.Equal(new[] { "interface/zz_van.gfx:2 (Vanilla)", "interface/aa_mod.gfx:2 (My Mod)", "interface/ab_mod.gfx:2 (My Mod)" }, t.Related);
        Assert.Equal(new SiteLocation(_roots.Mod, "interface/aa_mod.gfx", 2), t.Location);
    }

    [Fact]
    public void OnlyModFindings()
    {
        GfxTestRoots.Write(_roots.Vanilla, "gfx/interface/unused_v.dds", "dds");
        GfxTestRoots.Write(_roots.Vanilla, "interface/v.gfx", Sprite("GFX_lonely", "gfx/none.dds"));
        GfxTestRoots.Write(_roots.Other, "interface/o.gui", "guiTypes = {\n\ticonType = {\n\t\ticon = GFX_nowhere\n\t}\n}\n");

        var result = Analyze("hoi4");

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void NoModNoFindings()
    {
        GfxTestRoots.Write(_roots.Vanilla, "gfx/interface/unused_v.dds", "dds");
        var rules = GfxTestRoots.Rules("hoi4");
        var index = new GfxAuditBuilder(_roots.Sources("hoi4", withActiveMod: false), rules).Build();

        Assert.Empty(GfxAuditAnalyzer.Analyze(index, rules).Findings);
    }

    [Fact]
    public void SpriteTextureOutsideGfxKeepsImageUsed()
    {
        // EU IV interface/tutorial.gfx: texturefile = "tutorial\\task_icon.dds". Bildname teilt kein Wort mit dem Sprite-Namen.
        GfxTestRoots.Write(_roots.Mod, "tutorial/beta_icon.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", Sprite("GFX_alpha", "tutorial\\\\beta_icon.dds"));
        GfxTestRoots.Write(_roots.Mod, "interface/m.gui", "guiTypes = {\n\ticonType = {\n\t\tname = \"a\"\n\t\tspriteType = \"GFX_alpha\"\n\t}\n}\n");

        var result = Analyze("eu4");

        Assert.Empty(Of(result, GfxFindingKind.UnusedImage));
        Assert.Empty(Of(result, GfxFindingKind.MissingTexture));
    }

    [Fact]
    public void SpriteTextureWithLeadingSlashKeepsImageUsed()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/gamma_pic.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", Sprite("GFX_delta", "/gfx/interface/gamma_pic.dds"));
        GfxTestRoots.Write(_roots.Mod, "interface/m.gui", "guiTypes = {\n\ticonType = {\n\t\tname = \"d\"\n\t\tspriteType = \"GFX_delta\"\n\t}\n}\n");

        var result = Analyze("stellaris");

        Assert.Empty(Of(result, GfxFindingKind.UnusedImage));
        Assert.Empty(Of(result, GfxFindingKind.MissingTexture));
    }

    [Fact]
    public void SpriteTextureOutsideGfxMissing()
    {
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", Sprite("GFX_eps", "tutorial\\\\nothing_here.dds"));
        GfxTestRoots.Write(_roots.Mod, "interface/n.gfx", Sprite("GFX_zeta", "/gfx/interface/gone_too.dds"));

        var result = Analyze("eu4");

        var missing = Of(result, GfxFindingKind.MissingTexture);
        Assert.Equal(new[] { "gfx/interface/gone_too.dds", "tutorial/nothing_here.dds" }, missing.Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
        var eps = Assert.Single(missing, f => f.Name == "tutorial/nothing_here.dds");
        Assert.Equal(new SiteLocation(_roots.Mod, "interface/m.gfx", 4), eps.Location);
        Assert.Equal(new[] { "GFX_eps" }, eps.Related);
    }

    [Fact]
    public void ExtensionlessPathOutsideSpriteNotMissing()
    {
        // bitmapfont: fontname nennt eine .fnt, kein Bild. Nur Werte mit Bildendung sind fehlende Texturen.
        GfxTestRoots.Write(_roots.Mod, "interface/fonts.gfx", "bitmapfonts = {\n\tbitmapfont = {\n\t\tname = \"my_font\"\n\t\tfontname = \"gfx/fonts/nowhere\"\n\t}\n}\n");
        GfxTestRoots.Write(_roots.Mod, "gui/f.gui", "text = {\n\tfont = \"gfx/fonts/nowhere_either\"\n}\n");

        Assert.Empty(Of(Analyze("hoi4"), GfxFindingKind.MissingTexture));
        Assert.Empty(Of(Analyze("ck3"), GfxFindingKind.MissingTexture));
    }

    [Theory]
    [InlineData("eu4")]
    [InlineData("stellaris")]
    public void DuplicateModLoadsFirstLoses(string game)
    {
        // Zuletzt geladen gewinnt, Dateien nach Namen über Vanilla und Mods: Die Mod-Datei aa lädt vor der Vanilla-Datei zz und verliert.
        GfxTestRoots.Write(_roots.Mod, "interface/aa.gfx", Sprite("GFX_v", "gfx/v.dds"));
        GfxTestRoots.Write(_roots.Vanilla, "interface/zz.gfx", Sprite("GFX_v", "gfx/v.dds"));

        var result = Analyze(game);

        var v = Assert.Single(Of(result, GfxFindingKind.DuplicateSprite));
        Assert.Equal("GFX_v", v.Name);
        Assert.Equal(new[] { "interface/zz.gfx:2 (Vanilla)", "interface/aa.gfx:2 (My Mod)" }, v.Related);
        Assert.Equal(new SiteLocation(_roots.Mod, "interface/aa.gfx", 2), v.Location);
    }

    [Theory]
    [InlineData("eu4")]
    [InlineData("stellaris")]
    public void DuplicateModLoadsLastWins(string game)
    {
        GfxTestRoots.Write(_roots.Mod, "interface/zz.gfx", Sprite("GFX_w", "gfx/w.dds"));
        GfxTestRoots.Write(_roots.Vanilla, "interface/aa.gfx", Sprite("GFX_w", "gfx/w.dds"));

        var result = Analyze(game);

        Assert.Empty(Of(result, GfxFindingKind.DuplicateSprite));
    }

    [Fact]
    public void DuplicateSubfolderLoadsAfterInterfaceFiles()
    {
        // EU IV: zuerst die Dateien direkt in interface/, danach die Unterordner. Vanilla interface/sub/aa.gfx lädt nach Mod interface/zz.gfx.
        GfxTestRoots.Write(_roots.Mod, "interface/zz.gfx", Sprite("GFX_s", "gfx/s.dds"));
        GfxTestRoots.Write(_roots.Vanilla, "interface/sub/aa.gfx", Sprite("GFX_s", "gfx/s.dds"));

        var result = Analyze("eu4");

        var s = Assert.Single(Of(result, GfxFindingKind.DuplicateSprite));
        Assert.Equal(new[] { "interface/sub/aa.gfx:2 (Vanilla)", "interface/zz.gfx:2 (My Mod)" }, s.Related);
    }
}
