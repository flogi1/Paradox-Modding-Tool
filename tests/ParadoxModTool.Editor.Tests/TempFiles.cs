using System.IO;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Leere Dateien in einem Temp-Ordner je Testlauf, damit <see cref="OpenDocument.LoadFromFile"/> echte Pfade bekommt.</summary>
internal static class TempFiles
{
    public static readonly string Root = Path.Combine(Path.GetTempPath(), "pmt-editor-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Legt eine leere Datei unter <see cref="Root"/> an und liefert den vollen Pfad (Unterordner erlaubt).</summary>
    public static string Write(string relative)
    {
        var path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }
}
