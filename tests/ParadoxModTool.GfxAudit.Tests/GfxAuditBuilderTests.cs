using System.Collections.Concurrent;
using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.GfxAudit.Tests;

public sealed class GfxAuditBuilderTests : IDisposable
{
    private const string SpriteFile = "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_m\"\n\t\ttexturefile = \"gfx/a.dds\"\n\t}\n}\n";

    private readonly GfxTestRoots _roots = new();
    private readonly GfxAuditStore _store;

    public GfxAuditBuilderTests()
    {
        _store = new GfxAuditStore(Path.Combine(_roots.Temp, "store"));
    }

    public void Dispose() => _roots.Dispose();

    private GfxAuditBuilder Builder(Func<string, string>? readText = null) =>
        readText is null
            ? new GfxAuditBuilder(_roots.Sources(), GfxTestRoots.Rules())
            : new GfxAuditBuilder(_roots.Sources(), GfxTestRoots.Rules(), readText);

    /// <summary>Ein Leser, der jeden Lesevorgang zählt.</summary>
    private static (Func<string, string> Read, ConcurrentDictionary<string, int> Reads) Counting()
    {
        var reads = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        return (path =>
        {
            reads.AddOrUpdate(path, 1, (_, n) => n + 1);
            return File.ReadAllText(path);
        }, reads);
    }

    [Fact]
    public void CollectsImagesAndSprites()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/a.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", SpriteFile);
        GfxTestRoots.Write(_roots.Vanilla, "gfx/v.dds", "dds");

        var index = Builder().Build();

        Assert.Equal(_roots.Mod, index.ModRoot?.Directory);
        var a = Assert.Single(index.Images, i => i.File.RelativePath == "gfx/a.dds");
        Assert.Equal(FlagOrigin.ActiveMod, a.Origin);
        Assert.Equal("gfx/a.dds", a.LogicalPath);
        Assert.False(a.ReplacesOriginal);
        var v = Assert.Single(index.Images, i => i.File.RelativePath == "gfx/v.dds");
        Assert.Equal(FlagOrigin.Vanilla, v.Origin);
        Assert.False(v.ReplacesOriginal);

        var gfx = Assert.Single(index.Files, f => f.File.RelativePath == "interface/m.gfx");
        Assert.Equal(FlagOrigin.ActiveMod, gfx.Origin);
        Assert.Equal("My Mod", gfx.RootName);
        Assert.Equal(new[] { new GfxSprite("GFX_m", new[] { new GfxRef("gfx/a.dds", 4) }, 2) }, gfx.Scan.Sprites);
        Assert.Empty(index.UnreadFiles);
    }

    [Fact]
    public void MentionsFromOtherRoots()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/a.dds", "dds");
        GfxTestRoots.Write(_roots.Vanilla, "events/e.txt", "picture = a\n");

        var index = Builder().Build();

        var mention = Assert.Single(index.Mentions);
        Assert.Equal("gfx/a.dds", mention.Name);
        Assert.Equal(new SiteLocation(_roots.Vanilla, "events/e.txt", 1), mention.Location);
    }

    [Fact]
    public void PathMentionWithBackslashesAndOtherCase()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/My Icon.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "gfx/b.dds", "dds");
        GfxTestRoots.Write(_roots.Vanilla, "events/e.txt", "x = {\n\ticon = \"GFX\\\\Interface\\\\my icon.dds\"\n}\n");
        GfxTestRoots.Write(_roots.Other, "events/o.txt", "texture = \"GFX/B\"\n");

        var index = Builder().Build();

        Assert.Contains(index.Mentions, m => m.Name == "gfx/interface/my icon.dds" && m.Location == new SiteLocation(_roots.Vanilla, "events/e.txt", 2));
        Assert.Contains(index.Mentions, m => m.Name == "gfx/b.dds" && m.Location == new SiteLocation(_roots.Other, "events/o.txt", 1));
    }

    [Fact]
    public void SpriteNamesAreMentioned()
    {
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", SpriteFile);
        GfxTestRoots.Write(_roots.Vanilla, "gui/v.gui", "icon = { texture = gfx_m }\n");

        var index = Builder().Build();

        var mention = Assert.Single(index.Mentions, m => m.Name == "GFX_m");
        Assert.Equal(new SiteLocation(_roots.Vanilla, "gui/v.gui", 1), mention.Location);
    }

    [Fact]
    public void ReplacesOriginal()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/x.dds", "mod");
        GfxTestRoots.Write(_roots.Vanilla, "GFX/X.dds", "vanilla");

        var index = Builder().Build();

        var image = Assert.Single(index.Images);
        Assert.Equal(FlagOrigin.ActiveMod, image.Origin);
        Assert.True(image.ReplacesOriginal);
    }

    [Fact]
    public void ReplacesOriginalByLogicalPath()
    {
        GfxTestRoots.Write(_roots.Mod, "in_game/gfx/x.dds", "mod");
        GfxTestRoots.Write(_roots.Mod, "in_game/gfx/own.dds", "mod");
        GfxTestRoots.Write(_roots.Vanilla, "main_menu/gfx/X.dds", "vanilla");
        var builder = new GfxAuditBuilder(_roots.Sources("eu5"), GfxTestRoots.Rules("eu5"));

        var index = builder.Build();

        var mod = Assert.Single(index.Images, i => i.File.RelativePath == "in_game/gfx/x.dds");
        Assert.Equal("gfx/x.dds", mod.LogicalPath);
        Assert.True(mod.ReplacesOriginal);
        Assert.False(Assert.Single(index.Images, i => i.File.RelativePath == "in_game/gfx/own.dds").ReplacesOriginal);
        var vanilla = Assert.Single(index.Images, i => i.Origin == FlagOrigin.Vanilla);
        Assert.False(vanilla.ReplacesOriginal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReaderVersionOrFingerprintMismatchRereadsAll(bool readerVersion)
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/a.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", SpriteFile);
        GfxTestRoots.Write(_roots.Vanilla, "events/e.txt", "picture = a\n");
        var first = Builder().Refresh(_store);
        var key = _roots.Sources().ScopeKey;
        var stored = _store.Load(key)!;
        _store.Save(key, readerVersion ? stored with { ReaderVersion = GfxAuditBuilder.ReaderVersion + 1 } : stored with { RulesFingerprint = "other" });

        var (read, reads) = Counting();
        var second = Builder(read).Refresh(_store);

        Assert.Equal(2, reads.Count);
        Assert.Equal(first.Mentions, second.Mentions);
        Assert.Equal(GfxAuditBuilder.ReaderVersion, _store.Load(key)!.ReaderVersion);
        Assert.Equal(GfxTestRoots.Rules().Fingerprint, _store.Load(key)!.RulesFingerprint);
    }

    [Fact]
    public void VanishedFileDropsOut()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/a.dds", "dds");
        var gone = GfxTestRoots.Write(_roots.Vanilla, "events/gone.txt", "picture = a\n");
        GfxTestRoots.Write(_roots.Vanilla, "events/e.txt", "x = 1\n");
        Builder().Refresh(_store);

        File.Delete(gone);
        var index = Builder().Refresh(_store);

        Assert.DoesNotContain(index.Files, f => f.File.RelativePath == "events/gone.txt");
        Assert.Empty(index.Mentions);
        var stored = _store.Load(_roots.Sources().ScopeKey)!;
        Assert.DoesNotContain(stored.Files, f => f.Stamp.RelativePath == "events/gone.txt");
        Assert.DoesNotContain(stored.Mentions, m => m.Stamp.RelativePath == "events/gone.txt");
        Assert.Single(stored.Files);
    }

    [Fact]
    public void CorruptStoreIsIgnoredByRefresh()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/a.dds", "dds");
        GfxTestRoots.Write(_roots.Vanilla, "events/e.txt", "picture = a\n");
        var key = _roots.Sources().ScopeKey;
        GfxAuditStoreTests.WriteGz(_store.FileFor(key),
            "{\"FormatVersion\":" + GfxAuditStore.FormatVersion + ",\"ReaderVersion\":" + GfxAuditBuilder.ReaderVersion +
            ",\"RulesFingerprint\":\"" + GfxTestRoots.Rules().Fingerprint + "\",\"MentionNames\":[],\"Files\":[{\"Stamp\":{\"RootDirectory\":\"x\",\"RelativePath\":\"a.gfx\",\"Length\":1,\"LastWriteUtcTicks\":2}," +
            "\"Scan\":{\"Sprites\":[{\"Name\":null,\"Textures\":[],\"Line\":1}],\"TexturePaths\":[],\"SpriteRefs\":[]}}],\"Mentions\":[]}");
        Assert.Null(_store.Load(key));

        var index = Builder().Refresh(_store);

        Assert.Single(index.Mentions);
        Assert.NotNull(_store.Load(key));
    }

    [Fact]
    public void RefreshReusesUnchanged()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/a.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", SpriteFile);
        GfxTestRoots.Write(_roots.Vanilla, "events/e.txt", "picture = a\n");
        GfxTestRoots.Write(_roots.Vanilla, "events/n.txt", "x = {\n\tpicture = new\n}\n");

        var (read1, reads1) = Counting();
        var first = Builder(read1).Refresh(_store);
        Assert.Equal(3, reads1.Count);
        Assert.DoesNotContain(first.Mentions, m => m.Name == "gfx/new.dds");

        var (read2, reads2) = Counting();
        var second = Builder(read2).Refresh(_store);
        Assert.Empty(reads2);
        Assert.Equal(first.Mentions, second.Mentions);
        Assert.Equal(first.Files.Count, second.Files.Count);

        GfxTestRoots.Write(_roots.Mod, "gfx/new.dds", "dds");
        var (read3, reads3) = Counting();
        var third = Builder(read3).Refresh(_store);
        Assert.NotEmpty(reads3);
        var mention = Assert.Single(third.Mentions, m => m.Name == "gfx/new.dds");
        Assert.Equal(new SiteLocation(_roots.Vanilla, "events/n.txt", 2), mention.Location);
        Assert.Contains(third.Mentions, m => m.Name == "gfx/a.dds");
    }

    [Fact]
    public void RefreshRereadsChangedFile()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/a.dds", "dds");
        var path = GfxTestRoots.Write(_roots.Vanilla, "events/e.txt", "x = 1\n");
        Builder().Refresh(_store);

        File.WriteAllText(path, "x = 1\npicture = a\n");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        var (read, reads) = Counting();
        var index = Builder(read).Refresh(_store);

        Assert.Equal(1, Assert.Single(reads).Value);
        Assert.Equal(new SiteLocation(_roots.Vanilla, "events/e.txt", 2), Assert.Single(index.Mentions).Location);
    }

    [Fact]
    public void UnreadableFileCounted()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/a.dds", "dds");
        var bad = GfxTestRoots.Write(_roots.Vanilla, "events/bad.txt", "picture = a\n");
        GfxTestRoots.Write(_roots.Vanilla, "events/e.txt", "picture = a\n");

        var index = Builder(p => string.Equals(p, bad, StringComparison.OrdinalIgnoreCase) ? throw new IOException("gesperrt") : File.ReadAllText(p))
            .Build();

        Assert.Equal(new[] { bad }, index.UnreadFiles);
        Assert.Equal(new SiteLocation(_roots.Vanilla, "events/e.txt", 1), Assert.Single(index.Mentions).Location);
    }

    [Fact]
    public void ReportsPhasesInOrder()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/a.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "interface/m.gfx", SpriteFile);
        GfxTestRoots.Write(_roots.Vanilla, "events/e.txt", "picture = a\n");
        var reports = new List<BuildProgress>();

        Builder().Build(progress: new SyncProgress(reports.Add));

        Assert.Equal(new[] { BuildPhase.Reading, BuildPhase.Calls, BuildPhase.Resolving }, reports.Select(r => r.Phase).Distinct());
        Assert.Equal(new BuildProgress(BuildPhase.Reading, 2, 2), reports.Last(r => r.Phase == BuildPhase.Reading));
        Assert.Equal(new BuildProgress(BuildPhase.Resolving, 1, 1), reports.Last());
    }

    private sealed class SyncProgress(Action<BuildProgress> report) : IProgress<BuildProgress>
    {
        public void Report(BuildProgress value) => report(value);
    }
}
