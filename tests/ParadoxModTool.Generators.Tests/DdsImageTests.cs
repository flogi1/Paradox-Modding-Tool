using System.Buffers.Binary;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>DDS-Vorschau für Bildfelder (Wappen): DXT1 und DXT5 als BGRA-Pixel; andere Formate liefern keine Vorschau.</summary>
public sealed class DdsImageTests
{
    /// <summary>Kopf eines DDS mit FourCC (128 Byte) und die Blockdaten dahinter.</summary>
    internal static byte[] Dds(string fourCc, int width, int height, params byte[][] blocks)
    {
        var header = new byte[128];
        "DDS "u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), 124);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), height);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), width);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(76), 32);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(80), 4);
        System.Text.Encoding.ASCII.GetBytes(fourCc).CopyTo(header, 84);
        return header.Concat(blocks.SelectMany(b => b)).ToArray();
    }

    /// <summary>DXT1-Block: zwei 565-Farben und 16 Indizes zu je 2 Bit (Pixel 0 in den niedrigsten Bits).</summary>
    internal static byte[] Dxt1Block(ushort c0, ushort c1, params int[] indices)
    {
        var block = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(block, c0);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(2), c1);
        uint bits = 0;
        for (var i = 0; i < 16; i++)
        {
            bits |= (uint)(i < indices.Length ? indices[i] : 0) << (2 * i);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(4), bits);
        return block;
    }

    private static (byte B, byte G, byte R, byte A) Pixel(DdsPixels image, int x, int y)
    {
        var i = (y * image.Width + x) * 4;
        return (image.Bgra[i], image.Bgra[i + 1], image.Bgra[i + 2], image.Bgra[i + 3]);
    }

    [Fact]
    public void Dxt1_four_colour_block_decodes_endpoints()
    {
        var image = DdsImage.Decode(Dds("DXT1", 4, 4, Dxt1Block(0xF800, 0x001F, 0, 1, 2, 3)))!;

        Assert.Equal((4, 4), (image.Width, image.Height));
        Assert.Equal((0, 0, 255, 255), Pixel(image, 0, 0));
        Assert.Equal((255, 0, 0, 255), Pixel(image, 1, 0));
        Assert.Equal((85, 0, 170, 255), Pixel(image, 2, 0));
        Assert.Equal((170, 0, 85, 255), Pixel(image, 3, 0));
    }

    [Fact]
    public void Dxt1_three_colour_block_has_transparent_index_3()
    {
        var image = DdsImage.Decode(Dds("DXT1", 4, 4, Dxt1Block(0x001F, 0xF800, 2, 3)))!;

        Assert.Equal((127, 0, 127, 255), Pixel(image, 0, 0));
        Assert.Equal((0, 0, 0, 0), Pixel(image, 1, 0));
    }

    [Fact]
    public void Dxt1_blocks_fill_rows_and_columns()
    {
        var red = Dxt1Block(0xF800, 0xF800);
        var green = Dxt1Block(0x07E0, 0x07E0);
        var image = DdsImage.Decode(Dds("DXT1", 8, 4, red, green))!;

        Assert.Equal((0, 0, 255, 255), Pixel(image, 3, 3));
        Assert.Equal((0, 255, 0, 255), Pixel(image, 4, 0));
    }

    [Fact]
    public void Dxt5_uses_interpolated_alpha()
    {
        var alpha = new byte[8];
        alpha[0] = 255;
        alpha[1] = 0;
        alpha[2] = 0b001_000;
        var image = DdsImage.Decode(Dds("DXT5", 4, 4, alpha, Dxt1Block(0x07E0, 0x0000)))!;

        Assert.Equal((0, 255, 0, 255), Pixel(image, 0, 0));
        Assert.Equal((0, 255, 0, 0), Pixel(image, 1, 0));
    }

    [Theory]
    [InlineData("DXT3")]
    [InlineData("ATI2")]
    public void Unknown_formats_give_no_preview(string fourCc) =>
        Assert.Null(DdsImage.Decode(Dds(fourCc, 4, 4, new byte[16])));

    [Fact]
    public void Broken_data_gives_no_preview()
    {
        Assert.Null(DdsImage.Decode(new byte[] { 0x44, 0x44, 0x53, 1 }));
        Assert.Null(DdsImage.Decode(Dds("DXT1", 64, 64, Dxt1Block(0, 0))));
    }
}
