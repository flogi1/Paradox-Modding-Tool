using System.IO.Compression;
using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.GfxAudit.Tests;

public sealed class GfxAuditStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gfxs_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }
        catch (IOException)
        {
        }
    }

    private static StoredGfx Sample()
    {
        var stamp = new FileStamp("C:/mod", "interface/a.gfx", 123, 456);
        var scan = new GfxScan(
            new[] { new GfxSprite("GFX_a", new[] { new GfxRef("gfx/a.dds", 4), new GfxRef("gfx/a2.dds", 5) }, 2), new GfxSprite("GFX_b", Array.Empty<GfxRef>(), 7) },
            new[] { new GfxRef("gfx/c.dds", 9) },
            new[] { new GfxRef("GFX_d", 11) });
        return new StoredGfx(1, "fp1", new[] { "img:gfx/a.dds", "spr:GFX_a" },
            new[] { new StoredGfxFile(stamp, scan) },
            new[] { new StoredGfxMention(stamp, new GfxMention("gfx/a.dds", 12)) });
    }

    [Fact]
    public void RoundTrip()
    {
        var store = new GfxAuditStore(_dir);
        var saved = Sample();
        store.Save("k", saved);

        var loaded = store.Load("k");

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded!.ReaderVersion);
        Assert.Equal("fp1", loaded.RulesFingerprint);
        Assert.Equal(saved.MentionNames, loaded.MentionNames);
        var file = Assert.Single(loaded.Files);
        Assert.Equal(saved.Files[0].Stamp, file.Stamp);
        Assert.Equal(saved.Files[0].Scan.Sprites, file.Scan.Sprites);
        Assert.Equal(saved.Files[0].Scan.TexturePaths, file.Scan.TexturePaths);
        Assert.Equal(saved.Files[0].Scan.SpriteRefs, file.Scan.SpriteRefs);
        Assert.Equal(saved.Mentions, loaded.Mentions);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void WrongFormatVersionIgnored()
    {
        var store = new GfxAuditStore(_dir);
        store.Save("k", Sample());
        var path = store.FileFor("k");
        string json;
        using (var gzip = new GZipStream(File.OpenRead(path), CompressionMode.Decompress))
        using (var reader = new StreamReader(gzip))
        {
            json = reader.ReadToEnd();
        }

        var marker = "\"FormatVersion\":" + GfxAuditStore.FormatVersion;
        Assert.Contains(marker, json);
        using (var gzip = new GZipStream(File.Create(path), CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzip))
        {
            writer.Write(json.Replace(marker, "\"FormatVersion\":" + (GfxAuditStore.FormatVersion + 1)));
        }

        Assert.Null(store.Load("k"));
    }

    /// <summary>Schreibt <paramref name="json"/> gzip-komprimiert nach <paramref name="path"/> (Ordner wird angelegt).</summary>
    internal static void WriteGz(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var gzip = new GZipStream(File.Create(path), CompressionLevel.Fastest);
        using var writer = new StreamWriter(gzip);
        writer.Write(json);
    }

    private const string Stamp = "{\"RootDirectory\":\"C:/mod\",\"RelativePath\":\"a.gfx\",\"Length\":1,\"LastWriteUtcTicks\":2}";

    private static string Json(string scan, string mention = "{\"Name\":\"gfx/a.dds\",\"Line\":1}") =>
        "{\"FormatVersion\":" + GfxAuditStore.FormatVersion + ",\"ReaderVersion\":1,\"RulesFingerprint\":\"fp\",\"MentionNames\":[]," +
        "\"Files\":[{\"Stamp\":" + Stamp + ",\"Scan\":" + scan + "}],\"Mentions\":[{\"Stamp\":" + Stamp + ",\"Mention\":" + mention + "}]}";

    private const string GoodScan = "{\"Sprites\":[{\"Name\":\"GFX_a\",\"Textures\":[],\"Line\":1}],\"TexturePaths\":[],\"SpriteRefs\":[]}";

    [Theory]
    [InlineData("{\"Sprites\":[{\"Name\":null,\"Textures\":[],\"Line\":1}],\"TexturePaths\":[],\"SpriteRefs\":[]}", null)]
    [InlineData("{\"Sprites\":[{\"Name\":\"GFX_a\",\"Textures\":null,\"Line\":1}],\"TexturePaths\":[],\"SpriteRefs\":[]}", null)]
    [InlineData("{\"Sprites\":[{\"Name\":\"GFX_a\",\"Textures\":[null],\"Line\":1}],\"TexturePaths\":[],\"SpriteRefs\":[]}", null)]
    [InlineData("{\"Sprites\":[{\"Name\":\"GFX_a\",\"Textures\":[{\"Value\":null,\"Line\":1}],\"Line\":1}],\"TexturePaths\":[],\"SpriteRefs\":[]}", null)]
    [InlineData("{\"Sprites\":[null],\"TexturePaths\":[],\"SpriteRefs\":[]}", null)]
    [InlineData("{\"Sprites\":[],\"TexturePaths\":[{\"Value\":null,\"Line\":1}],\"SpriteRefs\":[]}", null)]
    [InlineData("{\"Sprites\":[],\"TexturePaths\":[],\"SpriteRefs\":[null]}", null)]
    [InlineData("{\"Sprites\":[],\"TexturePaths\":[]}", null)]
    [InlineData("null", null)]
    [InlineData(GoodScan, "{\"Name\":null,\"Line\":1}")]
    [InlineData(GoodScan, "null")]
    public void InvalidElementsLoadNull(string scan, string? mention)
    {
        var store = new GfxAuditStore(_dir);
        WriteGz(store.FileFor("k"), mention is null ? Json(scan) : Json(scan, mention));

        Assert.Null(store.Load("k"));
    }

    [Fact]
    public void HandWrittenValidJsonLoads()
    {
        var store = new GfxAuditStore(_dir);
        WriteGz(store.FileFor("k"), Json(GoodScan));

        var loaded = store.Load("k");

        Assert.NotNull(loaded);
        Assert.Equal("GFX_a", Assert.Single(Assert.Single(loaded!.Files).Scan.Sprites).Name);
    }

    [Fact]
    public void CorruptOrMissingLoadsNull()
    {
        var store = new GfxAuditStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FileFor("k"), "kein gzip");

        Assert.Null(store.Load("k"));
        Assert.Null(store.Load("fehlt"));
    }
}
