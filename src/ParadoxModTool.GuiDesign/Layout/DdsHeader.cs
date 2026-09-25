using System.Buffers.Binary;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>Liest Breite und Höhe aus dem DDS-Header (Höhe an Offset 12, Breite an Offset 16), ohne die Textur zu dekodieren.</summary>
public static class DdsHeader
{
    public static bool TryReadSize(string path, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            using var stream = File.OpenRead(path);
            var header = new byte[20];
            if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length
                || header[0] != (byte)'D' || header[1] != (byte)'D' || header[2] != (byte)'S' || header[3] != (byte)' ')
            {
                return false;
            }

            height = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(12));
            width = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(16));
            return width > 0 && height > 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
