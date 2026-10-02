using System.Text;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Mods;

/// <summary>Der Inhalt einer Textdatei ohne BOM; <see cref="HasBom"/> merkt, ob die Datei eines hatte, damit sie beim Schreiben unverändert bleibt.</summary>
public sealed record ModTextDocument(string Text, bool HasBom);

/// <summary>
/// Liest und schreibt Textdateien einer Mod (Changelog, Metadaten) als UTF-8 mit oder ohne BOM. Die Dateien liegen im Mod-Ordner und werden mit hochgeladen; Sicherungskopien
/// vor dem Überschreiben liegen dagegen in einem Ordner der Programmdaten, damit Steam sie nicht mitnimmt.
/// </summary>
public static class ModTextFile
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Liest die Datei. Wirft <see cref="InvalidDataException"/>, wenn sie kein gültiges UTF-8 ist (dann würde ein Zurückschreiben Zeichen verfälschen).</summary>
    public static ModTextDocument Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        try
        {
            return new ModTextDocument(StrictUtf8.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0)), hasBom);
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException(ModsTexts.TextFileNotUtf8);
        }
    }

    /// <summary>
    /// Schreibt die Datei atomar. Existiert sie schon, kommt vorher eine Kopie in <paramref name="backupDirectory"/> (<c>Name-Zeitstempel.Endung</c>).
    /// </summary>
    public static void Write(string path, ModTextDocument document, string backupDirectory)
    {
        SharedFile.Locked(path, () =>
        {
            if (File.Exists(path))
            {
                Directory.CreateDirectory(backupDirectory);
                File.Copy(path, UniqueBackupPath(backupDirectory, path));
            }

            SharedFile.WriteAllTextAtomic(path, (document.HasBom ? "\uFEFF" : string.Empty) + document.Text);
        });
    }

    private static string UniqueBackupPath(string directory, string source)
    {
        var name = Path.GetFileNameWithoutExtension(source);
        var extension = Path.GetExtension(source);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var candidate = Path.Combine(directory, $"{name}-{stamp}{extension}");
        for (var counter = 2; File.Exists(candidate); counter++)
        {
            candidate = Path.Combine(directory, $"{name}-{stamp}-{counter}{extension}");
        }

        return candidate;
    }
}
