using System.Buffers.Binary;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.GuiDesign.Imaging;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Bilddekodierung für die klassische Vorschau: DDS (unkomprimiert, DXT1/3/5, RXGB, DX10) und TGA, mit selbst gebauten Dateien.</summary>
public sealed class GuiImageDecoderTests : IDisposable
{
    private const uint FlagFourCc = 0x4;
    private const uint FlagRgb = 0x40;
    private const uint FlagRgbAlpha = 0x41;

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-image-decoder-" + Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _output;

    public GuiImageDecoderTests(ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private static uint FourCc(string text) => BinaryPrimitives.ReadUInt32LittleEndian(System.Text.Encoding.ASCII.GetBytes(text));

    /// <summary>DDS-Datei: 128 Byte Kopf (Magic, Größe, Pixelformat), bei <paramref name="dxgi"/> der 20-Byte-DX10-Kopf, dann die Nutzdaten.</summary>
    internal static byte[] Dds(int width, int height, uint pixelFlags, uint fourCc, int bits, uint r, uint g, uint b, uint a, byte[] payload, uint? dxgi = null)
    {
        var data = new byte[128 + (dxgi.HasValue ? 20 : 0) + payload.Length];
        "DDS "u8.CopyTo(data);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 124);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 0x1007);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(12), height);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(16), width);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(76), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(80), pixelFlags);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(84), fourCc);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(88), bits);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(92), r);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(96), g);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(100), b);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(104), a);
        var offset = 128;
        if (dxgi.HasValue)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(128), dxgi.Value);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(132), 3);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(140), 1);
            offset += 20;
        }

        payload.CopyTo(data, offset);
        return data;
    }

    private static byte[] Compressed(int width, int height, string fourCc, byte[] blocks) =>
        Dds(width, height, FlagFourCc, FourCc(fourCc), 0, 0, 0, 0, 0, blocks);

    private static byte[] Tga(int width, int height, int type, int bits, byte descriptor, byte[] payload)
    {
        var data = new byte[18 + payload.Length];
        data[2] = (byte)type;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(12), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(14), (ushort)height);
        data[16] = (byte)bits;
        data[17] = descriptor;
        payload.CopyTo(data, 18);
        return data;
    }

    private static byte[] Pixel(GuiImage image, int x, int y) => image.Bgra.AsSpan(((y * image.Width) + x) * 4, 4).ToArray();

    private static byte[] Bgra(int b, int g, int r, int a) => new[] { (byte)b, (byte)g, (byte)r, (byte)a };

    /// <summary>Ein DXT-Farbblock (8 Byte): zwei 565-Farben und 32 Bit Indizes (Pixel 0 in den niedrigsten Bits).</summary>
    private static byte[] ColorBlock(ushort c0, ushort c1, uint indices)
    {
        var block = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(block, c0);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(2), c1);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(4), indices);
        return block;
    }

    [Fact]
    public void Dds_uncompressed_32bit_reads_pixels()
    {
        // 2x2 ARGB (Masken A=FF000000, R=00FF0000, G=0000FF00, B=000000FF), Pixel als 32 Bit Little Endian.
        var payload = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 0xFF112233);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), 0x80FF8040);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), 0x01020304);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12), 0xFFFFFFFF);
        var file = Dds(2, 2, FlagRgbAlpha, 0, 32, 0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000, payload);

        var image = DdsDecoder.Decode(file);

        Assert.NotNull(image);
        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(16, image.Bgra.Length);
        Assert.Equal(Bgra(0x40, 0x80, 0xFF, 0x80), Pixel(image, 1, 0));
        Assert.Equal(Bgra(0x33, 0x22, 0x11, 0xFF), Pixel(image, 0, 0));
        Assert.Equal(Bgra(0x04, 0x03, 0x02, 0x01), Pixel(image, 0, 1));
    }

    [Fact]
    public void Dds_uncompressed_32bit_without_alpha_mask_is_opaque()
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 0x00112233);
        var file = Dds(1, 1, FlagRgb, 0, 32, 0x00FF0000, 0x0000FF00, 0x000000FF, 0, payload);

        var image = DdsDecoder.Decode(file);

        Assert.NotNull(image);
        Assert.Equal(Bgra(0x33, 0x22, 0x11, 0xFF), Pixel(image, 0, 0));
    }

    [Fact]
    public void Dds_24bit_and_16bit_565()
    {
        // 24 Bit: Bytes in der Reihenfolge B, G, R (Maske R=FF0000).
        var file24 = Dds(2, 1, FlagRgb, 0, 24, 0xFF0000, 0x00FF00, 0x0000FF, 0, new byte[] { 0x10, 0x20, 0x30, 0xA0, 0xB0, 0xC0 });
        var image24 = DdsDecoder.Decode(file24);
        Assert.NotNull(image24);
        Assert.Equal(Bgra(0x10, 0x20, 0x30, 0xFF), Pixel(image24, 0, 0));
        Assert.Equal(Bgra(0xA0, 0xB0, 0xC0, 0xFF), Pixel(image24, 1, 0));

        // 16 Bit 5-6-5: Rot, Grün, Blau, Weiß.
        var payload16 = new byte[8];
        foreach (var (i, value) in new (int, ushort)[] { (0, 0xF800), (1, 0x07E0), (2, 0x001F), (3, 0xFFFF) })
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload16.AsSpan(i * 2), value);
        }

        var file16 = Dds(2, 2, FlagRgb, 0, 16, 0xF800, 0x07E0, 0x001F, 0, payload16);
        var image16 = DdsDecoder.Decode(file16);
        Assert.NotNull(image16);
        Assert.Equal(Bgra(0, 0, 255, 255), Pixel(image16, 0, 0));
        Assert.Equal(Bgra(0, 255, 0, 255), Pixel(image16, 1, 0));
        Assert.Equal(Bgra(255, 0, 0, 255), Pixel(image16, 0, 1));
        Assert.Equal(Bgra(255, 255, 255, 255), Pixel(image16, 1, 1));
    }

    [Fact]
    public void Dds_16bit_4444_reads_alpha()
    {
        var payload = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, 0x8F04); // A=8, R=F, G=0, B=4
        var file = Dds(1, 1, FlagRgbAlpha, 0, 16, 0x0F00, 0x00F0, 0x000F, 0xF000, payload);

        var image = DdsDecoder.Decode(file);

        Assert.NotNull(image);
        Assert.Equal(Bgra(0x44, 0x00, 0xFF, 0x88), Pixel(image, 0, 0));
    }

    [Fact]
    public void Dds_dxt1_block_decodes_colors()
    {
        // Farbe 0 reines Rot, Farbe 1 reines Blau, Indizes der ersten Zeile 0, 1, 2, 3.
        var file = Compressed(4, 4, "DXT1", ColorBlock(0xF800, 0x001F, 0xE4));

        var image = DdsDecoder.Decode(file);

        Assert.NotNull(image);
        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal(Bgra(0, 0, 255, 255), Pixel(image, 0, 0));
        Assert.Equal(Bgra(255, 0, 0, 255), Pixel(image, 1, 0));
        Assert.Equal(Bgra(85, 0, 170, 255), Pixel(image, 2, 0)); // 2:1 Rot zu Blau
        Assert.Equal(Bgra(170, 0, 85, 255), Pixel(image, 3, 0)); // 1:2
        Assert.Equal(Bgra(0, 0, 255, 255), Pixel(image, 0, 1));
    }

    [Fact]
    public void Dds_dxt1_three_color_block_has_transparent_black()
    {
        // Farbe 0 <= Farbe 1: drei Farben (Index 2 als Mitte) und Index 3 transparentes Schwarz.
        var file = Compressed(4, 4, "DXT1", ColorBlock(0x001F, 0xF800, 0xE4));

        var image = DdsDecoder.Decode(file);

        Assert.NotNull(image);
        Assert.Equal(Bgra(127, 0, 127, 255), Pixel(image, 2, 0));
        Assert.Equal(Bgra(0, 0, 0, 0), Pixel(image, 3, 0));
    }

    [Fact]
    public void Dds_dxt1_cuts_blocks_at_image_edge()
    {
        // 5x3 braucht 2x1 Blöcke; die überstehenden Pixel entfallen.
        var blocks = ColorBlock(0xF800, 0xF800, 0).Concat(ColorBlock(0x001F, 0x001F, 0)).ToArray();

        var image = DdsDecoder.Decode(Compressed(5, 3, "DXT1", blocks));

        Assert.NotNull(image);
        Assert.Equal(5 * 3 * 4, image.Bgra.Length);
        Assert.Equal(Bgra(0, 0, 255, 255), Pixel(image, 3, 2));
        Assert.Equal(Bgra(255, 0, 0, 255), Pixel(image, 4, 2));
    }

    [Fact]
    public void Dds_dxt3_and_dxt5_alpha()
    {
        var color = ColorBlock(0xF800, 0xF800, 0);

        // DXT3: 4 Bit Alpha je Pixel, Pixel 0 im niedrigen Halbbyte. Zeile 0: 0, F, 4, 8.
        var dxt3Alpha = new byte[] { 0xF0, 0x84, 0, 0, 0, 0, 0, 0 };
        var dxt3 = DdsDecoder.Decode(Compressed(4, 4, "DXT3", dxt3Alpha.Concat(color).ToArray()));
        Assert.NotNull(dxt3);
        Assert.Equal(Bgra(0, 0, 255, 0), Pixel(dxt3, 0, 0));
        Assert.Equal(Bgra(0, 0, 255, 255), Pixel(dxt3, 1, 0));
        Assert.Equal(Bgra(0, 0, 255, 68), Pixel(dxt3, 2, 0));
        Assert.Equal(Bgra(0, 0, 255, 136), Pixel(dxt3, 3, 0));

        // DXT5, Alpha0 > Alpha1: acht Stufen. Indizes der Zeile 0: 0, 1, 2, 7 (3 Bit je Pixel).
        var dxt5Alpha = new byte[] { 255, 0, 0x88, 0x0E, 0, 0, 0, 0 };
        var dxt5 = DdsDecoder.Decode(Compressed(4, 4, "DXT5", dxt5Alpha.Concat(color).ToArray()));
        Assert.NotNull(dxt5);
        Assert.Equal(255, Pixel(dxt5, 0, 0)[3]);
        Assert.Equal(0, Pixel(dxt5, 1, 0)[3]);
        Assert.Equal(218, Pixel(dxt5, 2, 0)[3]); // (6*255)/7
        Assert.Equal(36, Pixel(dxt5, 3, 0)[3]); // 255/7
        Assert.Equal(Bgra(0, 0, 255, 255), Pixel(dxt5, 0, 0));
    }

    [Fact]
    public void Dds_dxt5_with_small_alpha0_uses_six_steps_plus_zero_and_opaque()
    {
        // Alpha0 <= Alpha1: sechs Stufen, Index 6 = 0 und Index 7 = 255. Indizes der Zeile 0: 6, 7, 2, 0.
        // 6 | 7<<3 | 2<<6 = 6 + 56 + 128 = 190 = 0xBE
        var alpha = new byte[] { 0, 255, 0xBE, 0, 0, 0, 0, 0 };
        var file = Compressed(4, 4, "DXT5", alpha.Concat(ColorBlock(0xF800, 0xF800, 0)).ToArray());

        var image = DdsDecoder.Decode(file);

        Assert.NotNull(image);
        Assert.Equal(0, Pixel(image, 0, 0)[3]);
        Assert.Equal(255, Pixel(image, 1, 0)[3]);
        Assert.Equal(51, Pixel(image, 2, 0)[3]); // (4*0 + 1*255)/5
        Assert.Equal(0, Pixel(image, 3, 0)[3]);
    }

    [Fact]
    public void Dds_rxgb_moves_alpha_to_red()
    {
        // DXT5 mit Rot im Alphakanal: Alpha 200 wird zu Rot, Grün und Blau stammen aus dem Farbblock, Alpha wird 255.
        var alpha = new byte[] { 200, 200, 0, 0, 0, 0, 0, 0 };
        var file = Compressed(4, 4, "RXGB", alpha.Concat(ColorBlock(0x0410, 0x0410, 0)).ToArray());

        var image = DdsDecoder.Decode(file);

        Assert.NotNull(image);
        Assert.Equal(Bgra(132, 130, 200, 255), Pixel(image, 0, 0));
        Assert.Equal(Bgra(132, 130, 200, 255), Pixel(image, 3, 3));
    }

    [Fact]
    public void Dds_dx10_rgba8()
    {
        // DXGI 28 und 29 (RGBA8, auch sRGB): Byte-Reihenfolge R, G, B, A.
        foreach (var dxgi in new uint[] { 28, 29 })
        {
            var file = Dds(2, 1, FlagFourCc, FourCc("DX10"), 0, 0, 0, 0, 0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, dxgi);
            var image = DdsDecoder.Decode(file);
            Assert.NotNull(image);
            Assert.Equal(Bgra(3, 2, 1, 4), Pixel(image, 0, 0));
            Assert.Equal(Bgra(7, 6, 5, 8), Pixel(image, 1, 0));
        }
    }

    [Fact]
    public void Dds_dx10_bgra8()
    {
        // DXGI 87 und 91 (BGRA8, auch sRGB): Byte-Reihenfolge schon B, G, R, A.
        foreach (var dxgi in new uint[] { 87, 91 })
        {
            var file = Dds(2, 1, FlagFourCc, FourCc("DX10"), 0, 0, 0, 0, 0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, dxgi);
            var image = DdsDecoder.Decode(file);
            Assert.NotNull(image);
            Assert.Equal(Bgra(1, 2, 3, 4), Pixel(image, 0, 0));
            Assert.Equal(Bgra(5, 6, 7, 8), Pixel(image, 1, 0));
        }
    }

    [Fact]
    public void Dds_dx10_with_other_format_returns_null() =>
        Assert.Null(DdsDecoder.Decode(Dds(1, 1, FlagFourCc, FourCc("DX10"), 0, 0, 0, 0, 0, new byte[16], dxgi: 2)));

    [Fact]
    public void Tga_uncompressed_bottom_up_is_flipped()
    {
        // 2x2, 24 Bit, Ursprung unten links: die erste Zeile der Datei ist die untere Bildzeile.
        var payload = new byte[]
        {
            1, 2, 3, 4, 5, 6, // unten: (0,1) und (1,1) als B, G, R
            7, 8, 9, 10, 11, 12, // oben: (0,0) und (1,0)
        };
        var image = TgaDecoder.Decode(Tga(2, 2, type: 2, bits: 24, descriptor: 0, payload));

        Assert.NotNull(image);
        Assert.Equal(Bgra(7, 8, 9, 255), Pixel(image, 0, 0));
        Assert.Equal(Bgra(10, 11, 12, 255), Pixel(image, 1, 0));
        Assert.Equal(Bgra(1, 2, 3, 255), Pixel(image, 0, 1));
        Assert.Equal(Bgra(4, 5, 6, 255), Pixel(image, 1, 1));
    }

    [Fact]
    public void Tga_top_left_origin_keeps_row_order_and_reads_alpha()
    {
        var payload = new byte[] { 1, 2, 3, 100, 4, 5, 6, 200 };
        var image = TgaDecoder.Decode(Tga(1, 2, type: 2, bits: 32, descriptor: 0x28, payload));

        Assert.NotNull(image);
        Assert.Equal(Bgra(1, 2, 3, 100), Pixel(image, 0, 0));
        Assert.Equal(Bgra(4, 5, 6, 200), Pixel(image, 0, 1));
    }

    [Fact]
    public void Tga_rle_decodes_runs()
    {
        // 2x2, 32 Bit, oben links. Lauf von 3 Pixeln (spannt über die Zeilengrenze), danach ein Rohpaket mit einem Pixel.
        var payload = new byte[]
        {
            0x82, 10, 20, 30, 255, // 3 mal (B=10, G=20, R=30, A=255)
            0x00, 1, 2, 3, 4, // 1 Rohpixel
        };
        var image = TgaDecoder.Decode(Tga(2, 2, type: 10, bits: 32, descriptor: 0x28, payload));

        Assert.NotNull(image);
        Assert.Equal(Bgra(10, 20, 30, 255), Pixel(image, 0, 0));
        Assert.Equal(Bgra(10, 20, 30, 255), Pixel(image, 1, 0));
        Assert.Equal(Bgra(10, 20, 30, 255), Pixel(image, 0, 1));
        Assert.Equal(Bgra(1, 2, 3, 4), Pixel(image, 1, 1));
    }

    [Fact]
    public void Tga_rle_raw_packet_and_bottom_up_flip()
    {
        // 1x2, 24 Bit, Ursprung unten links, ein Rohpaket mit zwei Pixeln (unten zuerst).
        var payload = new byte[] { 0x01, 1, 2, 3, 4, 5, 6 };
        var image = TgaDecoder.Decode(Tga(1, 2, type: 10, bits: 24, descriptor: 0, payload));

        Assert.NotNull(image);
        Assert.Equal(Bgra(4, 5, 6, 255), Pixel(image, 0, 0));
        Assert.Equal(Bgra(1, 2, 3, 255), Pixel(image, 0, 1));
    }

    [Fact]
    public void Tga_16bit_reads_a1r5g5b5()
    {
        // Rot (R=31) und Blau (B=31) ohne gesetztes Alpha-Bit; ohne Attribut-Bit im Kopf bleibt alles undurchsichtig.
        var payload = new byte[] { 0x00, 0x7C, 0x1F, 0x00 };
        var opaque = TgaDecoder.Decode(Tga(2, 1, type: 2, bits: 16, descriptor: 0x20, payload));
        Assert.NotNull(opaque);
        Assert.Equal(Bgra(0, 0, 255, 255), Pixel(opaque, 0, 0));
        Assert.Equal(Bgra(255, 0, 0, 255), Pixel(opaque, 1, 0));

        // Mit einem Attribut-Bit (Deskriptor 0x21) ist das oberste Bit Alpha: erstes Pixel durchsichtig, zweites (0x801F) deckend.
        var withAlpha = TgaDecoder.Decode(Tga(2, 1, type: 2, bits: 16, descriptor: 0x21, new byte[] { 0x00, 0x7C, 0x1F, 0x80 }));
        Assert.NotNull(withAlpha);
        Assert.Equal(Bgra(0, 0, 255, 0), Pixel(withAlpha, 0, 0));
        Assert.Equal(Bgra(255, 0, 0, 255), Pixel(withAlpha, 1, 0));
    }

    [Fact]
    public void Dds_accepts_wide_strip_like_hoi4_event_background()
    {
        // 12672 x 4 Pixel: breiter als hoch, wie die Ereignishintergründe in HOI IV.
        var image = DdsDecoder.Decode(Compressed(12672, 4, "DXT1", new byte[3168 * 8]));

        Assert.NotNull(image);
        Assert.Equal(12672, image.Width);
    }

    [Fact]
    public void Tga_skips_image_id_and_color_map()
    {
        var payload = new byte[] { 0xAA, 0xBB, /* Farbtabelle: 2 Einträge zu 3 Byte */ 9, 9, 9, 9, 9, 9, /* Pixel */ 1, 2, 3 };
        var file = Tga(1, 1, type: 2, bits: 24, descriptor: 0x20, payload);
        file[0] = 2; // Länge der Bild-ID
        file[1] = 1; // Farbtabelle vorhanden
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(5), 2); // Anzahl Einträge
        file[7] = 24; // Bit je Eintrag

        var image = TgaDecoder.Decode(file);

        Assert.NotNull(image);
        Assert.Equal(Bgra(1, 2, 3, 255), Pixel(image, 0, 0));
    }

    [Fact]
    public void Corrupt_file_returns_null()
    {
        Assert.Null(DdsDecoder.Decode(Array.Empty<byte>()));
        Assert.Null(DdsDecoder.Decode(new byte[50]));
        Assert.Null(DdsDecoder.Decode("DDS "u8.ToArray()));
        Assert.Null(TgaDecoder.Decode(Array.Empty<byte>()));
        Assert.Null(TgaDecoder.Decode(new byte[10]));

        // Kopf in Ordnung, Nutzdaten abgeschnitten.
        Assert.Null(DdsDecoder.Decode(Compressed(8, 8, "DXT1", new byte[8])));
        Assert.Null(DdsDecoder.Decode(Dds(2, 2, FlagRgb, 0, 32, 0xFF0000, 0xFF00, 0xFF, 0, new byte[8])));
        Assert.Null(TgaDecoder.Decode(Tga(2, 2, type: 2, bits: 24, descriptor: 0, new byte[5])));
        Assert.Null(TgaDecoder.Decode(Tga(4, 4, type: 10, bits: 24, descriptor: 0, new byte[] { 0x82, 1, 2, 3 })));

        // Nicht unterstützt oder unsinnig.
        Assert.Null(DdsDecoder.Decode(Compressed(4, 4, "ATI2", new byte[16])));
        Assert.Null(DdsDecoder.Decode(Compressed(0, 4, "DXT1", new byte[8])));
        Assert.Null(DdsDecoder.Decode(Compressed(100000, 4, "DXT1", new byte[8])));
        Assert.Null(DdsDecoder.Decode(Dds(1, 1, FlagRgb, 0, 8, 0xFF, 0, 0, 0, new byte[1])));
        Assert.Null(TgaDecoder.Decode(Tga(1, 1, type: 1, bits: 8, descriptor: 0, new byte[1])));
        Assert.Null(TgaDecoder.Decode(Tga(1, 1, type: 2, bits: 15, descriptor: 0, new byte[2])));
        Assert.Null(TgaDecoder.Decode(Tga(0, 1, type: 2, bits: 24, descriptor: 0, new byte[3])));
    }

    [Fact]
    public void Tga_with_huge_size_and_tiny_payload_is_rejected_before_allocating()
    {
        // Kopf für 8192 × 8192 Pixel (256 MB als BGRA) mit fast keinen Daten: abgelehnt, ohne den Pixelpuffer anzulegen.
        var raw = Tga(8192, 8192, type: 2, bits: 32, descriptor: 0x28, new byte[16]);
        var rle = Tga(8192, 8192, type: 10, bits: 32, descriptor: 0x28, new byte[] { 0xFF, 1, 2, 3, 4 });
        TgaDecoder.Decode(Tga(1, 1, type: 2, bits: 32, descriptor: 0x28, new byte[4]));

        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Null(TgaDecoder.Decode(raw));
        Assert.Null(TgaDecoder.Decode(rle));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 1024 * 1024, $"{allocated} Byte angelegt");
    }

    [Fact]
    public void TryDecode_picks_decoder_by_extension_and_never_throws()
    {
        Directory.CreateDirectory(_temp);
        var dds = Path.Combine(_temp, "a.dds");
        var tga = Path.Combine(_temp, "b.TGA");
        var png = Path.Combine(_temp, "c.png");
        var broken = Path.Combine(_temp, "d.dds");
        File.WriteAllBytes(dds, Compressed(4, 4, "DXT1", ColorBlock(0xF800, 0xF800, 0)));
        File.WriteAllBytes(tga, Tga(1, 1, type: 2, bits: 24, descriptor: 0x20, new byte[] { 1, 2, 3 }));
        File.WriteAllBytes(png, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        File.WriteAllBytes(broken, new byte[] { 1, 2, 3 });

        Assert.Equal(4, GuiImageDecoder.TryDecode(dds)?.Width);
        Assert.Equal(Bgra(1, 2, 3, 255), Pixel(GuiImageDecoder.TryDecode(tga)!, 0, 0));
        Assert.Null(GuiImageDecoder.TryDecode(png));
        Assert.Null(GuiImageDecoder.TryDecode(broken));
        Assert.Null(GuiImageDecoder.TryDecode(Path.Combine(_temp, "missing.dds")));
        Assert.Null(GuiImageDecoder.TryDecode(Path.Combine(_temp, "no-extension")));
    }

    /// <summary>Die Probe-Texturen von Fenster 6 (Textur-Messung) sind unkomprimierte BGRA8-DDS und müssen sich decodieren lassen; die Ecken der Schnitt-Textur haben die Palettenfarben.</summary>
    [Fact]
    public void Probe_textures_decode()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "TestData", "probe6");
        var files = Directory.GetFiles(folder, "*.dds", SearchOption.AllDirectories);
        var images = new Dictionary<string, GuiImage>();
        foreach (var file in files)
        {
            var image = DdsDecoder.Decode(File.ReadAllBytes(file));
            Assert.NotNull(image);
            Assert.Equal(image.Width * image.Height * 4, image.Bgra.Length);
            images[Path.GetFileNameWithoutExtension(file)] = image;
        }

        foreach (var name in new[] { "slice", "frames", "wide", "gray", "white", "odd", "strip3" })
        {
            Assert.True(images.ContainsKey(name), $"{name}.dds fehlt in TestData/probe6");
        }

        var red = Bgra(0, 0, 255, 255);
        var green = Bgra(0, 255, 0, 255);
        var blue = Bgra(255, 0, 0, 255);
        var yellow = Bgra(0, 255, 255, 255);
        var magenta = Bgra(255, 0, 255, 255);
        var cyan = Bgra(255, 255, 0, 255);
        var orange = Bgra(0, 127, 255, 255);
        var white = Bgra(255, 255, 255, 255);

        var slice = images["slice"];
        Assert.Equal((40, 40), (slice.Width, slice.Height));
        Assert.Equal(red, Pixel(slice, 0, 0));
        Assert.Equal(yellow, Pixel(slice, 39, 0));
        Assert.Equal(magenta, Pixel(slice, 0, 39));
        Assert.Equal(cyan, Pixel(slice, 39, 39));
        Assert.Equal(green, Pixel(slice, 20, 0));
        Assert.Equal(green, Pixel(slice, 20, 39));
        Assert.Equal(orange, Pixel(slice, 0, 20));
        Assert.Equal(orange, Pixel(slice, 39, 20));
        Assert.Equal(blue, Pixel(slice, 30, 30));
        Assert.Equal(white, Pixel(slice, 8, 8));
        Assert.Equal(white, Pixel(slice, 11, 11));
        Assert.Equal(blue, Pixel(slice, 12, 12));

        // Kachelmarken W 4x4 in den Randstreifen: oben x 12-15, y 2-5; links x 2-5, y 12-15.
        for (var d = 0; d < 4; d++)
        {
            for (var e = 0; e < 4; e++)
            {
                Assert.Equal(white, Pixel(slice, 12 + d, 2 + e));
                Assert.Equal(white, Pixel(slice, 2 + d, 12 + e));
            }
        }

        Assert.Equal(green, Pixel(slice, 11, 3));
        Assert.Equal(green, Pixel(slice, 16, 4));
        Assert.Equal(green, Pixel(slice, 12, 6));
        Assert.Equal(green, Pixel(slice, 13, 1));
        Assert.Equal(orange, Pixel(slice, 3, 11));
        Assert.Equal(orange, Pixel(slice, 4, 16));
        Assert.Equal(orange, Pixel(slice, 1, 12));
        Assert.Equal(orange, Pixel(slice, 6, 13));
        Assert.Equal(green, Pixel(slice, 12, 35));
        Assert.Equal(orange, Pixel(slice, 35, 12));

        var frames = images["frames"];
        Assert.Equal((80, 80), (frames.Width, frames.Height));
        Assert.Equal(red, Pixel(frames, 10, 10));
        Assert.Equal(green, Pixel(frames, 50, 10));
        Assert.Equal(blue, Pixel(frames, 10, 50));
        Assert.Equal(yellow, Pixel(frames, 50, 50));

        var wide = images["wide"];
        Assert.Equal((80, 40), (wide.Width, wide.Height));
        Assert.Equal(red, Pixel(wide, 10, 20));
        Assert.Equal(green, Pixel(wide, 30, 20));
        Assert.Equal(blue, Pixel(wide, 50, 20));
        Assert.Equal(yellow, Pixel(wide, 70, 20));

        Assert.Equal((8, 8), (images["gray"].Width, images["gray"].Height));
        Assert.Equal(Bgra(128, 128, 128, 255), Pixel(images["gray"], 3, 3));
        Assert.Equal((8, 8), (images["white"].Width, images["white"].Height));
        Assert.Equal(white, Pixel(images["white"], 3, 3));
        Assert.Equal((50, 50), (images["odd"].Width, images["odd"].Height));
        Assert.Equal(white, Pixel(images["odd"], 49, 49));

        var strip = images["strip3"];
        Assert.Equal((100, 20), (strip.Width, strip.Height));
        Assert.Equal(red, Pixel(strip, 33, 5));
        Assert.Equal(green, Pixel(strip, 34, 5));
        Assert.Equal(green, Pixel(strip, 66, 5));
        Assert.Equal(blue, Pixel(strip, 67, 5));
        Assert.Equal(blue, Pixel(strip, 99, 5));
    }

    // --- BC7 und DX10-Blockformate ---

    private static string TestData(string relative) => Path.Combine(AppContext.BaseDirectory, "TestData", relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Die Referenz-Pixel stammen aus Pillow (make_bc7_testdata.py); der reservierte Modus ist dort auf 0,0,0,0 gesetzt, wie die D3D-Beschreibung es verlangt.</summary>
    [Theory]
    [InlineData("bc7_modes_64x64", 64, 64)]
    [InlineData("bc7_edge_30x18_srgb", 30, 18)]
    public void Bc7_matches_the_reference_decoder(string name, int width, int height)
    {
        var image = DdsDecoder.Decode(File.ReadAllBytes(TestData($"bc7/{name}.dds")))!;
        var rgba = File.ReadAllBytes(TestData($"bc7/{name}.rgba"));
        Assert.Equal((width, height), (image.Width, image.Height));
        for (var i = 0; i < width * height; i++)   // GuiImage ist BGRA
        {
            Assert.Equal(rgba[(i * 4) + 2], image.Bgra[i * 4]);
            Assert.Equal(rgba[(i * 4) + 1], image.Bgra[(i * 4) + 1]);
            Assert.Equal(rgba[i * 4], image.Bgra[(i * 4) + 2]);
            Assert.Equal(rgba[(i * 4) + 3], image.Bgra[(i * 4) + 3]);
        }
    }

    [Fact]
    public void Reserved_bc7_mode_is_transparent_black()
    {
        // Byte 0 = 0: kein Modus gesetzt. Die übrigen Byte sind egal, alle 16 Pixel werden 0,0,0,0.
        var block = new byte[16];
        Array.Fill(block, (byte)0xFF, 1, 15);

        var image = Bc7Decoder.Decode(block, 4, 4);

        Assert.NotNull(image);
        Assert.All(image.Bgra, value => Assert.Equal(0, value));
        Assert.Equal(64, image.Bgra.Length);
    }

    [Fact]
    public void Truncated_bc7_returns_null()
    {
        // 64x64 braucht 256 Blöcke zu 16 Byte; hier sind es 100 Byte.
        Assert.Null(DdsDecoder.Decode(Dds(64, 64, FlagFourCc, FourCc("DX10"), 0, 0, 0, 0, 0, new byte[100], dxgi: 98)));
        Assert.Null(Bc7Decoder.Decode(new byte[100], 64, 64));
        Assert.Null(Bc7Decoder.Decode(new byte[16], 0, 4));
    }

    [Fact]
    public void Bc7_cuts_edge_blocks_to_the_image_size()
    {
        // Modus 6, alles 0 außer den Endpunkten: ein 5x2-Bild braucht 2x1 Blöcke und liefert genau 5x2 Pixel.
        var blocks = new byte[32];
        blocks[0] = 0x40;
        blocks[16] = 0x40;

        var image = Bc7Decoder.Decode(blocks, 5, 2);

        Assert.NotNull(image);
        Assert.Equal(5 * 2 * 4, image.Bgra.Length);
    }

    [Theory]
    [InlineData(70)]
    [InlineData(71)]
    [InlineData(72)]
    [InlineData(74)]
    [InlineData(77)]
    [InlineData(78)]
    public void Dx10_block_formats_decode_like_the_legacy_fourcc(int dxgi)
    {
        var color = ColorBlock(0xF800, 0x001F, 0xE4);
        var (fourCc, blocks) = dxgi switch
        {
            <= 72 => ("DXT1", color),
            <= 75 => ("DXT3", new byte[] { 0xF0, 0x84, 0x12, 0x34, 0, 0, 0, 0 }.Concat(color).ToArray()),
            _ => ("DXT5", new byte[] { 255, 0, 0x88, 0x0E, 0, 0, 0, 0 }.Concat(color).ToArray()),
        };

        var legacy = DdsDecoder.Decode(Compressed(4, 4, fourCc, blocks));
        var dx10 = DdsDecoder.Decode(Dds(4, 4, FlagFourCc, FourCc("DX10"), 0, 0, 0, 0, 0, blocks, (uint)dxgi));

        Assert.NotNull(legacy);
        Assert.NotNull(dx10);
        Assert.Equal((legacy.Width, legacy.Height), (dx10.Width, dx10.Height));
        Assert.Equal(legacy.Bgra, dx10.Bgra);
        Assert.Equal(Bgra(0, 0, 255, legacy.Bgra[3]), Pixel(dx10, 0, 0)); // Farbe 0 ist Rot, nicht nur leer
    }

    private static string WriteTemp(string folder, string name, byte[] bytes)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] Dx10Head(uint dxgi) => Dds(8, 8, FlagFourCc, FourCc("DX10"), 0, 0, 0, 0, 0, Array.Empty<byte>(), dxgi);

    [Theory]
    [InlineData(83u, "DDS DXGI 83 (BC5)")]
    [InlineData(80u, "DDS DXGI 80 (BC4)")]
    [InlineData(84u, "DDS DXGI 84 (BC5)")]
    [InlineData(95u, "DDS DXGI 95 (BC6H)")]
    [InlineData(10u, "DDS DXGI 10 (Float)")]
    [InlineData(2u, "DDS DXGI 2 (Float)")]
    [InlineData(200u, "DDS DXGI 200")]
    public void UnsupportedFormat_names_the_unsupported_dxgi_formats(uint dxgi, string expected)
    {
        var path = WriteTemp(_temp, $"dxgi{dxgi}.dds", Dx10Head(dxgi));

        Assert.Equal(new TextureHeader(8, 8, expected), TextureLocator.ReadHeader(path));
    }

    [Fact]
    public void UnsupportedFormat_names_bc5()
    {
        var path = WriteTemp(_temp, "bc5.dds", Dx10Head(83));

        Assert.Equal("DDS DXGI 83 (BC5)", TextureLocator.ReadHeader(path)!.Value.UnsupportedFormat);
    }

    [Fact]
    public void UnsupportedFormat_names_fourcc_and_bit_depth()
    {
        var ati2 = WriteTemp(_temp, "ati2.dds", Dds(8, 8, FlagFourCc, FourCc("ATI2"), 0, 0, 0, 0, 0, Array.Empty<byte>()));
        var eight = WriteTemp(_temp, "eight.dds", Dds(8, 8, FlagRgb, 0, 8, 0xFF, 0, 0, 0, Array.Empty<byte>()));

        Assert.Equal("DDS FourCC ATI2", TextureLocator.ReadHeader(ati2)!.Value.UnsupportedFormat);
        Assert.Equal("DDS RGB 8 Bit", TextureLocator.ReadHeader(eight)!.Value.UnsupportedFormat);
    }

    [Fact]
    public void UnsupportedFormat_is_null_for_bc7_bc3_dx10_dxt5_png_and_jpg()
    {
        foreach (var dxgi in new uint[] { 70, 72, 73, 75, 76, 77, 78, 97, 98, 99, 28, 29, 87, 91 })
        {
            var path = WriteTemp(_temp, $"ok{dxgi}.dds", Dx10Head(dxgi));
            Assert.Null(TextureLocator.ReadHeader(path)!.Value.UnsupportedFormat);
        }

        var dxt5 = WriteTemp(_temp, "dxt5.dds", Dds(8, 8, FlagFourCc, FourCc("DXT5"), 0, 0, 0, 0, 0, Array.Empty<byte>()));
        Assert.Null(TextureLocator.ReadHeader(dxt5)!.Value.UnsupportedFormat);

        var png = WriteTemp(_temp, "a.png", Encode(new PngBitmapEncoder(), 5, 3));
        var jpg = WriteTemp(_temp, "a.jpg", Encode(new JpegBitmapEncoder(), 5, 3));
        Assert.Equal(new TextureHeader(5, 3, null), TextureLocator.ReadHeader(png));
        Assert.Equal(new TextureHeader(5, 3, null), TextureLocator.ReadHeader(jpg));
    }

    [Fact]
    public void UnsupportedFormat_does_not_decode()
    {
        var path = WriteTemp(_temp, "head.dds", Dx10Head(98));
        var before = GuiImageDecoder.DecodeCalls;

        Assert.Null(TextureLocator.ReadHeader(path)!.Value.UnsupportedFormat);
        Assert.NotNull(TextureLocator.ReadHeader(WriteTemp(_temp, "bc5.dds", Dx10Head(83)))!.Value.UnsupportedFormat);

        Assert.Equal(before, GuiImageDecoder.DecodeCalls);
    }

    private static byte[] Encode(BitmapEncoder encoder, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        Array.Fill(pixels, (byte)200);
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    // --- Echte Installationen (fehlt das Spiel, kehren die Tests sofort zurück) ---

    /// <summary>Alle .dds unter game/**/gfx/interface, DLC-Ordner eingeschlossen, sortiert.</summary>
    private static List<string> InterfaceDdsFiles(string gameDirectory)
    {
        var marker = Path.DirectorySeparatorChar + "gfx" + Path.DirectorySeparatorChar + "interface" + Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(gameDirectory, "*.dds", SearchOption.AllDirectories)
            .Where(file => file.Contains(marker, StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Kurzname des DDS-Formats im Kopf (FourCC, DXGI-Nummer oder Bitbreite), nur für die Ausgabe und die Stichprobe je Format.</summary>
    private static string FormatKey(ReadOnlySpan<byte> head)
    {
        if (head.Length < 128)
        {
            return "kurz";
        }

        var flags = BinaryPrimitives.ReadUInt32LittleEndian(head[80..]);
        if ((flags & FlagFourCc) == 0)
        {
            return "RGB " + BinaryPrimitives.ReadInt32LittleEndian(head[88..]).ToString(CultureInfo.InvariantCulture) + " Bit";
        }

        var fourCc = System.Text.Encoding.ASCII.GetString(head.Slice(84, 4));
        return fourCc == "DX10" && head.Length >= 132
            ? "DXGI " + BinaryPrimitives.ReadUInt32LittleEndian(head[128..]).ToString(CultureInfo.InvariantCulture)
            : fourCc;
    }

    private static bool HasDdsMagic(string file)
    {
        using var stream = File.OpenRead(file);
        Span<byte> magic = stackalloc byte[4];
        return stream.ReadAtLeast(magic, 4, throwOnEndOfStream: false) == 4 && magic.SequenceEqual("DDS "u8);
    }

    [Theory]
    [InlineData("eu5")]
    [InlineData("ck3")]
    [InlineData("vic3")]
    public void Every_interface_texture_format_is_supported(string game)
    {
        var install = VanillaGui.For(game);
        if (!install.Exists)
        {
            return;
        }

        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var empty = new List<string>();
        var unsupported = new List<string>();
        foreach (var file in InterfaceDdsFiles(install.GameDirectory))
        {
            var header = TextureLocator.ReadHeader(file);
            if (header is null)
            {
                unsupported.Add(file + " (kein lesbarer Kopf)");
                continue;
            }

            if (header.Value.UnsupportedFormat == GuiTexts.TextureFormatEmptyFile)
            {
                empty.Add(file);
                continue;
            }

            if (header.Value.UnsupportedFormat is not null)
            {
                unsupported.Add($"{file}: {header.Value.UnsupportedFormat}");
                continue;
            }

            using var stream = File.OpenRead(file);
            var head = new byte[148];
            var read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            var key = head.AsSpan(0, read).StartsWith("DDS "u8) ? FormatKey(head.AsSpan(0, read)) : "kein DDS (Magic)";
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        _output.WriteLine($"{game}: " + string.Join(", ", counts.Select(pair => $"{pair.Key} {pair.Value}")));
        _output.WriteLine($"{game}: {empty.Count} leere Dateien" + string.Concat(empty.Select(file => Environment.NewLine + "  " + file)));
        Assert.True(unsupported.Count == 0, $"{unsupported.Count} Dateien nicht unterstützt:{Environment.NewLine}{string.Join(Environment.NewLine, unsupported.Take(20))}");
    }

    [Theory]
    [InlineData("eu5")]
    [InlineData("ck3")]
    [InlineData("vic3")]
    public void Real_textures_decode(string game)
    {
        var install = VanillaGui.For(game);
        if (!install.Exists)
        {
            return;
        }

        // Bis zu 20 Dateien je Format (BC7 eingeschlossen); die PNG, die ".dds" heißt (CK III), hat keine DDS-Magic und bleibt außen vor.
        var perFormat = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in InterfaceDdsFiles(install.GameDirectory))
        {
            if (new FileInfo(file).Length < 128 || !HasDdsMagic(file))
            {
                continue;
            }

            using var stream = File.OpenRead(file);
            var head = new byte[148];
            var read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            var key = FormatKey(head.AsSpan(0, read));
            if (!perFormat.TryGetValue(key, out var list))
            {
                perFormat[key] = list = new List<string>();
            }

            if (list.Count < 20)
            {
                list.Add(file);
            }
        }

        var slowest = TimeSpan.Zero;
        var slowestFile = string.Empty;
        var decoded = 0;
        foreach (var (key, files) in perFormat)
        {
            foreach (var file in files)
            {
                var header = TextureLocator.ReadHeader(file)!.Value;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var image = DdsDecoder.Decode(File.ReadAllBytes(file));
                watch.Stop();
                Assert.True(image is not null, $"{key}: {file} lässt sich nicht dekodieren");
                Assert.True((header.Width, header.Height) == (image.Width, image.Height), $"{file}: Kopf {header.Width}x{header.Height}, Bild {image.Width}x{image.Height}");
                decoded++;
                if (watch.Elapsed > slowest)
                {
                    slowest = watch.Elapsed;
                    slowestFile = $"{file} ({image.Width}x{image.Height}, {key})";
                }
            }
        }

        _output.WriteLine($"{game}: {decoded} Dateien in {perFormat.Count} Formaten dekodiert ({string.Join(", ", perFormat.Select(pair => $"{pair.Key} {pair.Value.Count}"))}); langsamste: {slowest.TotalMilliseconds:F0} ms, {slowestFile}");
    }
}
