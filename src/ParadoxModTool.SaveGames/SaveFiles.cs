using System.Text;
using System.Text.RegularExpressions;
using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.SaveGames;

/// <summary>Ein Spielstand im Ordner: lesbar (Text-Stand) oder nicht, dazu die Angaben aus <c>metadata</c>.</summary>
public sealed record SaveFileInfo(
    string Path,
    DateTime LastWriteUtc,
    long Length,
    bool Readable,
    string? GameDate,
    string? PlaythroughId,
    string? PlaythroughName,
    string? Version,
    bool Compressed = false);

/// <summary>Kopfprüfung und Liste der Spielstände. Liest nur den Anfang jeder Datei und schreibt nie.</summary>
public static class SaveFiles
{
    private const int HeadBytes = 64 * 1024;

    private static Regex MetadataField(string dateField) =>
        new(@"(?<=^|[\s{])(" + Regex.Escape(dateField) + @"|playthrough_id|playthrough_name|version)=(?:""([^""\r\n]*)""|([^\s}""]*))", RegexOptions.Multiline);

    /// <summary>
    /// Prüft die Kopfzeile: <see cref="SaveTextMarker.Value"/> = Text-Stand, <see cref="SaveTextMarker.CompressedValue"/> = Text-Stand mit Inhalt als
    /// ZIP-Eintrag (<see cref="SaveFileInfo.Compressed"/>). Die Metadaten stehen in beiden Fällen als Text im Block <paramref name="metadataBlock"/>.
    /// </summary>
    public static SaveFileInfo Inspect(string path, SaveTextMarker marker, string metadataBlock = "metadata", string dateField = "date")
    {
        var file = new FileInfo(path);
        var head = ReadHead(path);
        var newline = head.IndexOf('\n');
        var firstLine = (newline < 0 ? head : head[..newline]).TrimEnd('\r');
        bool Has(string? value) =>
            !string.IsNullOrEmpty(value)
            && firstLine.StartsWith(marker.Prefix, StringComparison.Ordinal)
            && firstLine.Length >= marker.Offset + value.Length
            && string.CompareOrdinal(firstLine, marker.Offset, value, 0, value.Length) == 0;
        var plain = Has(marker.Value);
        var compressed = !plain && Has(marker.CompressedValue);
        var readable = plain || compressed;

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (readable && newline >= 0)
        {
            var metadata = head[(newline + 1)..];
            if (metadata.StartsWith(metadataBlock + "={", StringComparison.Ordinal))
            {
                // Nur der Metadaten-Block: er endet mit der ersten schließenden Klammer in Spalte 0.
                var end = metadata.IndexOf("\n}", StringComparison.Ordinal);
                metadata = end < 0 ? metadata : metadata[..end];
                foreach (Match match in MetadataField(dateField).Matches(metadata))
                {
                    fields.TryAdd(match.Groups[1].Value, match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value);
                }
            }
        }

        return new SaveFileInfo(
            path,
            file.LastWriteTimeUtc,
            file.Length,
            readable,
            fields.GetValueOrDefault(dateField),
            fields.GetValueOrDefault("playthrough_id"),
            fields.GetValueOrDefault("playthrough_name"),
            fields.GetValueOrDefault("version"),
            compressed);
    }

    /// <summary>Alle Spielstände mit der Dateiendung des Profils, neueste zuerst; ein fehlender Ordner ergibt eine leere Liste.</summary>
    public static IReadOnlyList<SaveFileInfo> List(string folder, SaveGameSettings settings)
    {
        if (!Directory.Exists(folder))
        {
            return Array.Empty<SaveFileInfo>();
        }

        return Directory.EnumerateFiles(folder, "*" + settings.Extension)
            .Where(path => string.Equals(System.IO.Path.GetExtension(path), settings.Extension, StringComparison.OrdinalIgnoreCase))
            .Select(path => Inspect(path, settings.TextMarker, settings.MetadataBlock, settings.DateField))
            .OrderByDescending(info => info.LastWriteUtc)
            .ToList();
    }

    /// <summary>
    /// Vorschlag für "Vergleich mit": der neueste lesbare Stand, der älter ist als <paramref name="current"/> und zum selben Durchlauf gehört;
    /// sonst der neueste ältere lesbare Stand; nie <paramref name="current"/> selbst.
    /// </summary>
    public static SaveFileInfo? DefaultComparison(IReadOnlyList<SaveFileInfo> saves, SaveFileInfo current)
    {
        var older = saves
            .Where(s => s.Readable && s.LastWriteUtc < current.LastWriteUtc && !string.Equals(s.Path, current.Path, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.LastWriteUtc)
            .ToList();
        return older.FirstOrDefault(s => s.PlaythroughId is not null && s.PlaythroughId == current.PlaythroughId) ?? older.FirstOrDefault();
    }

    private static string ReadHead(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buffer = new byte[HeadBytes];
        var read = 0;
        int count;
        while (read < buffer.Length && (count = stream.Read(buffer, read, buffer.Length - read)) > 0)
        {
            read += count;
        }

        return Encoding.UTF8.GetString(buffer, 0, read);
    }
}
