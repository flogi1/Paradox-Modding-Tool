using System.Security.Cryptography;
using System.Text;

namespace ParadoxModTool.Compatch;

/// <summary>Kodiert Text nach dem Namen aus dem Spielprofil bzw. aus <see cref="PatchFileOp.EncodingName"/>.</summary>
public static class TextEncodings
{
    private static readonly Encoding Windows1252 = Create1252();

    /// <param name="encodingName"><c>utf-8-bom</c>, <c>utf-8</c> oder <c>windows-1252</c>; Unbekanntes gilt als <c>utf-8-bom</c>.</param>
    public static byte[] Encode(string text, string encodingName)
    {
        switch (encodingName.ToLowerInvariant())
        {
            case "windows-1252":
                return Windows1252.GetBytes(text);
            case "utf-8":
                return new UTF8Encoding(false).GetBytes(text);
            default:
                var body = new UTF8Encoding(false).GetBytes(text);
                return new byte[] { 0xEF, 0xBB, 0xBF }.Concat(body).ToArray();
        }
    }

    public static string Decode(byte[] bytes) => ParadoxModTool.ModCompat.ParsedScript.Decode(bytes).Text;

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public static bool HasUtf8Bom(string fullPath)
    {
        using var stream = File.OpenRead(fullPath);
        Span<byte> head = stackalloc byte[3];
        return stream.Read(head) == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
    }

    private static Encoding Create1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
