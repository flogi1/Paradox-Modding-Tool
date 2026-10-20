using System.Buffers.Binary;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Kleine DDS-Dateien für Tests, gebaut mit dem <c>Dds</c>-Helfer von <see cref="GuiImageDecoderTests"/>.</summary>
internal static class TestDds
{
    private const uint FlagFourCc = 0x4;
    private const uint FlagRgbAlpha = 0x41;

    /// <summary>Unkomprimierte 32-Bit-Datei (BGRA), voll deckend weiß.</summary>
    public static string WriteBgra(string path, int width, int height)
    {
        var payload = new byte[width * height * 4];
        Array.Fill(payload, (byte)0xFF);
        return Write(path, GuiImageDecoderTests.Dds(width, height, FlagRgbAlpha, 0, 32, 0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000, payload));
    }

    /// <summary>Nur der Kopf einer DX10-Datei mit der DXGI-Nummer <paramref name="dxgi"/> (genug für Größe und Format, keine Nutzdaten).</summary>
    public static string WriteDx10Header(string path, int width, int height, uint dxgi) =>
        Write(path, GuiImageDecoderTests.Dds(width, height, FlagFourCc, BinaryPrimitives.ReadUInt32LittleEndian("DX10"u8), 0, 0, 0, 0, 0, Array.Empty<byte>(), dxgi));

    private static string Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
