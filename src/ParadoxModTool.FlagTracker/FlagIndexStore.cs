using System.IO.Compression;
using System.Text.Json;

namespace ParadoxModTool.FlagTracker;

/// <summary>Stand einer Datei beim Lesen: Wurzel, relativer Pfad, Größe und Änderungszeit (UTC-Ticks). Gleicher Stand = Datei nicht neu lesen.</summary>
public sealed record FileStamp(string RootDirectory, string RelativePath, long Length, long LastWriteUtcTicks);

/// <summary>
/// Gespeicherte Leseergebnisse eines Bereichs. Herkunft und Name der Mod werden nicht gespeichert; sie kommen beim Laden aus den Wurzeln des Bereichs.
/// <paramref name="Callees"/>: die Namen, deren Aufrufe gesammelt wurden (siehe <see cref="FlagIndexBuilder"/>); null = unbekannt, dann werden alle Aufrufe neu gesammelt.
/// <paramref name="ReaderVersion"/> (<see cref="FlagIndexBuilder.ReaderVersion"/>) und <paramref name="TableFingerprint"/> (<see cref="FlagCommandTable.Fingerprint"/>):
/// womit gelesen wurde; weicht eins davon ab, nutzt der Builder die Leseergebnisse nicht (voller Aufbau).
/// </summary>
public sealed record StoredScans(
    int FormatVersion,
    DateTime BuiltUtc,
    IReadOnlyList<(FileStamp Stamp, FileScan Scan)> Files,
    IReadOnlyList<string>? Callees = null,
    int ReaderVersion = 0,
    string? TableFingerprint = null);

/// <summary>Speichert die Leseergebnisse je Bereich als <c>&lt;Ordner&gt;/&lt;Schlüssel&gt;.json.gz</c>. Schreibt nur in den eigenen Ordner.</summary>
public sealed class FlagIndexStore
{
    public const int FormatVersion = 3;

    private readonly string _directory;

    /// <summary>Der Ordner wird erst beim ersten Speichern angelegt.</summary>
    public FlagIndexStore(string directory)
    {
        _directory = directory;
    }

    public string FileFor(string scopeKey) => Path.Combine(_directory, scopeKey + ".json.gz");

    /// <summary>Null, wenn die Datei fehlt, nicht lesbar ist oder eine andere <see cref="FormatVersion"/> hat. Wirft nie.</summary>
    public StoredScans? Load(string scopeKey)
    {
        try
        {
            var path = FileFor(scopeKey);
            if (!File.Exists(path))
            {
                return null;
            }

            using var file = File.OpenRead(path);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            var dto = JsonSerializer.Deserialize<StoredDto>(gzip);
            if (dto is null || dto.FormatVersion != FormatVersion || dto.Files is null)
            {
                return null;
            }

            var files = new List<(FileStamp, FileScan)>(dto.Files.Count);
            foreach (var entry in dto.Files)
            {
                if (entry.Stamp is null || entry.Scan is null)
                {
                    return null;
                }

                files.Add((entry.Stamp, entry.Scan));
            }

            return new StoredScans(dto.FormatVersion, dto.BuiltUtc, files, dto.Callees, dto.ReaderVersion, dto.TableFingerprint);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Schreibt in eine Temp-Datei im Ordner und verschiebt sie dann über die Zieldatei.</summary>
    public void Save(string scopeKey, StoredScans scans)
    {
        Directory.CreateDirectory(_directory);
        var target = FileFor(scopeKey);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var dto = new StoredDto
            {
                FormatVersion = scans.FormatVersion,
                BuiltUtc = scans.BuiltUtc,
                Callees = scans.Callees?.ToList(),
                ReaderVersion = scans.ReaderVersion,
                TableFingerprint = scans.TableFingerprint,
                Files = scans.Files.Select(f => new FileDto { Stamp = f.Stamp, Scan = f.Scan }).ToList(),
            };
            using (var file = File.Create(temp))
            using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
            {
                JsonSerializer.Serialize(gzip, dto);
            }

            File.Move(temp, target, true);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            throw;
        }
    }

    private sealed class StoredDto
    {
        public int FormatVersion { get; set; }

        public DateTime BuiltUtc { get; set; }

        public List<string>? Callees { get; set; }

        public int ReaderVersion { get; set; }

        public string? TableFingerprint { get; set; }

        public List<FileDto>? Files { get; set; }
    }

    private sealed class FileDto
    {
        public FileStamp? Stamp { get; set; }

        public FileScan? Scan { get; set; }
    }
}
