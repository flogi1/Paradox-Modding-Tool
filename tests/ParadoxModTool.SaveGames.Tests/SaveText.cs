using System.Text;

namespace ParadoxModTool.SaveGames.Tests;

/// <summary>Kleine Spielstände für Tests: Kopfzeile wie ein EU-V-Textstand plus Text, in einer neuen Temp-Datei.</summary>
internal static class SaveText
{
    public const string TextHeader = "SAV0300701cf8aa0004df3b00000000";

    public const string CompressedHeader = "SAV0203ab06bd4600062d2f00000000";

    public static string Write(string body, string header = TextHeader) =>
        WriteBytes(new UTF8Encoding(false).GetBytes(header + "\n" + body));

    public static string WriteBytes(byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), "pmt-save-" + Guid.NewGuid().ToString("N") + ".eu5");
        File.WriteAllBytes(path, content);
        return path;
    }
}
