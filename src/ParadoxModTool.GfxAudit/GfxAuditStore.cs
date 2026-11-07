using System.IO.Compression;
using System.Text.Json;
using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.GfxAudit;

/// <summary>Das gespeicherte Leseergebnis einer Textdatei samt Stand der Datei.</summary>
public sealed record StoredGfxFile(FileStamp Stamp, GfxScan Scan);

/// <summary>Eine gespeicherte Erwähnung samt Stand der Datei, in der sie steht.</summary>
public sealed record StoredGfxMention(FileStamp Stamp, GfxMention Mention);

/// <summary>
/// Gespeicherte Leseergebnisse eines Bereichs für die GFX-Prüfung.
/// <paramref name="ReaderVersion"/> und <paramref name="RulesFingerprint"/>: womit gelesen wurde; weicht eins davon ab, wird neu gelesen.
/// <paramref name="MentionNames"/>: die Namen samt Art (<c>img:</c>/<c>spr:</c>), deren Erwähnungen gesammelt wurden; null = unbekannt.
/// </summary>
public sealed record StoredGfx(
    int ReaderVersion,
    string RulesFingerprint,
    IReadOnlyList<string>? MentionNames,
    IReadOnlyList<StoredGfxFile> Files,
    IReadOnlyList<StoredGfxMention> Mentions);

/// <summary>Speichert die Leseergebnisse der GFX-Prüfung je Bereich als <c>&lt;Ordner&gt;/&lt;Schlüssel&gt;.json.gz</c>. Schreibt nur in den eigenen Ordner.</summary>
public sealed class GfxAuditStore
{
    /// <summary>Version des gespeicherten Formats; eine Datei mit anderer Version wird nicht geladen.</summary>
    public const int FormatVersion = 2;

    private readonly string _directory;

    /// <summary>Der Ordner wird erst beim ersten Speichern angelegt.</summary>
    public GfxAuditStore(string directory)
    {
        _directory = directory;
    }

    public string FileFor(string scopeKey) => Path.Combine(_directory, scopeKey + ".json.gz");

    /// <summary>Null, wenn die Datei fehlt, nicht lesbar oder unvollständig ist oder eine andere <see cref="FormatVersion"/> hat. Wirft nie.</summary>
    public StoredGfx? Load(string scopeKey)
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
            if (dto is null || dto.FormatVersion != FormatVersion || dto.RulesFingerprint is null || dto.Files is null || dto.Mentions is null)
            {
                return null;
            }

            var files = new List<StoredGfxFile>(dto.Files.Count);
            foreach (var entry in dto.Files)
            {
                if (!IsValid(entry.Stamp) || entry.Scan is not { } scan || !IsValid(scan))
                {
                    return null;
                }

                files.Add(new StoredGfxFile(entry.Stamp!, scan));
            }

            var mentions = new List<StoredGfxMention>(dto.Mentions.Count);
            foreach (var entry in dto.Mentions)
            {
                if (!IsValid(entry.Stamp) || entry.Mention?.Name is null)
                {
                    return null;
                }

                mentions.Add(new StoredGfxMention(entry.Stamp!, entry.Mention));
            }

            return new StoredGfx(dto.ReaderVersion, dto.RulesFingerprint, dto.MentionNames, files, mentions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Schreibt in eine Temp-Datei im Ordner und verschiebt sie dann über die Zieldatei.</summary>
    public void Save(string scopeKey, StoredGfx data)
    {
        Directory.CreateDirectory(_directory);
        var target = FileFor(scopeKey);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var dto = new StoredDto
            {
                FormatVersion = FormatVersion,
                ReaderVersion = data.ReaderVersion,
                RulesFingerprint = data.RulesFingerprint,
                MentionNames = data.MentionNames?.ToList(),
                Files = data.Files.Select(f => new FileDto { Stamp = f.Stamp, Scan = f.Scan }).ToList(),
                Mentions = data.Mentions.Select(m => new MentionDto { Stamp = m.Stamp, Mention = m.Mention }).ToList(),
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

    /// <summary>Stand mit Wurzel und relativem Pfad; JSON kann trotz nicht nullbarer Typen null liefern.</summary>
    private static bool IsValid(FileStamp? stamp) => stamp is { RootDirectory: not null, RelativePath: not null };

    /// <summary>Alle Listen und ihre Elemente vorhanden, jeder Sprite mit Namen und Texturliste, jeder Verweis und jede Textur mit Wert.</summary>
    private static bool IsValid(GfxScan scan) =>
        scan.Sprites is not null && scan.TexturePaths is not null && scan.SpriteRefs is not null
        && scan.Sprites.All(s => s?.Name is not null && s.Textures is not null && s.Textures.All(t => t?.Value is not null))
        && scan.TexturePaths.All(r => r?.Value is not null)
        && scan.SpriteRefs.All(r => r?.Value is not null);

    private sealed class StoredDto
    {
        public int FormatVersion { get; set; }

        public int ReaderVersion { get; set; }

        public string? RulesFingerprint { get; set; }

        public List<string>? MentionNames { get; set; }

        public List<FileDto>? Files { get; set; }

        public List<MentionDto>? Mentions { get; set; }
    }

    private sealed class FileDto
    {
        public FileStamp? Stamp { get; set; }

        public GfxScan? Scan { get; set; }
    }

    private sealed class MentionDto
    {
        public FileStamp? Stamp { get; set; }

        public GfxMention? Mention { get; set; }
    }
}
