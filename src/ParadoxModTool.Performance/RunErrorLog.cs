using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.Performance;

/// <summary>Die <c>error.log</c> des gemessenen Laufs, im Ordner des Laufs archiviert: Das Spiel überschreibt sie beim nächsten Start.</summary>
public static class RunErrorLog
{
    public const string FileName = "error.log";

    private const byte NewLine = (byte)'\n';

    private static ReadOnlySpan<byte> Utf8Bom => new byte[] { 0xEF, 0xBB, 0xBF };

    /// <summary>
    /// Kopiert den aktuellen Lauf aus <paramref name="logFolder"/> (alle seine Dateien, älteste zuerst) nach <c>&lt;runFolder&gt;/error.log</c>.
    /// Endet eine Datei nicht mit <c>\n</c>, kommt vor der nächsten (nicht leeren) einer dazu; ein UTF-8-BOM am Anfang einer späteren Datei fällt
    /// weg, damit keiner mitten in der Kopie steht. <see langword="false"/> ohne aktuellen Lauf oder ohne Dateien; <see cref="IOException"/> und
    /// <see cref="UnauthorizedAccessException"/> gehen an den Aufrufer.
    /// </summary>
    public static bool Copy(string logFolder, string runFolder)
    {
        var run = ErrorLogLocations.Runs(logFolder).FirstOrDefault(r => r.IsCurrent);
        if (run is null || run.Files.Count == 0)
        {
            return false;
        }

        Directory.CreateDirectory(runFolder);
        var target = Path.Combine(runFolder, FileName);
        var temp = target + ".tmp";
        try
        {
            using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                var written = false;
                var last = NewLine;
                foreach (var file in run.Files)
                {
                    // Das Spiel schreibt noch in die neueste Datei, also mit Mitlesen.
                    using var input = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    var count = input.ReadAtLeast(buffer, Utf8Bom.Length, throwOnEndOfStream: false);
                    var start = written && count >= Utf8Bom.Length && buffer.AsSpan(0, Utf8Bom.Length).SequenceEqual(Utf8Bom) ? Utf8Bom.Length : 0;
                    var separated = !written;
                    while (count > 0)
                    {
                        if (count > start)
                        {
                            if (!separated && last != NewLine)
                            {
                                output.WriteByte(NewLine);
                            }

                            separated = true;
                            output.Write(buffer, start, count - start);
                            written = true;
                            last = buffer[count - 1];
                        }

                        start = 0;
                        count = input.Read(buffer, 0, buffer.Length);
                    }
                }
            }

            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }

        return true;
    }

    /// <summary>Der Pfad der archivierten <c>error.log</c>, wenn sie existiert.</summary>
    public static string? PathFor(string runFolder)
    {
        var path = Path.Combine(runFolder, FileName);
        return File.Exists(path) ? path : null;
    }
}
