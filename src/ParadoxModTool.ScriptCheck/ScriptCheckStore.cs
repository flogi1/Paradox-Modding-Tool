using System.IO.Compression;
using System.Text.Json;
using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.ScriptCheck;

/// <summary>Ein gespeicherter Verweis auf einen bekannten Namen samt Stand der Datei, in der er steht.</summary>
public sealed record StoredMention(FileStamp Stamp, CheckRef Ref);

/// <summary>
/// Gespeicherte Leseergebnisse eines Bereichs für die Skriptprüfung.
/// <paramref name="ReaderVersion"/> und <paramref name="RulesFingerprint"/>: womit gelesen wurde; weicht eins davon ab, wird neu gelesen.
/// <paramref name="MentionNames"/>: die Namen samt Art (<c>Art:Name</c>), deren Erwähnungen gesammelt wurden; null = unbekannt.
/// </summary>
public sealed record StoredCheck(
    int FormatVersion,
    DateTime BuiltUtc,
    int ReaderVersion,
    string RulesFingerprint,
    IReadOnlyList<(FileStamp Stamp, CheckScan Scan)> Files,
    IReadOnlyList<string>? MentionNames,
    IReadOnlyList<StoredMention>? Mentions);

/// <summary>Speichert die Leseergebnisse der Skriptprüfung je Bereich als <c>&lt;Ordner&gt;/&lt;Schlüssel&gt;.json.gz</c>. Schreibt nur in den eigenen Ordner.</summary>
public sealed class ScriptCheckStore
{
    /// <summary>2: <see cref="StoredCheck.MentionNames"/> enthält Art und Name (<c>Effect:my_fx</c>). 3: <see cref="CheckScan.Templates"/>. 4: <see cref="CheckScan.Iterators"/>, <see cref="CheckScan.OnActionCalls"/>, <see cref="CheckRef.CheckedBlock"/>, <see cref="CheckDefinition.Polled"/>.</summary>
    public const int FormatVersion = 4;

    private readonly string _directory;

    private readonly int _maxFiles;

    /// <summary>Der Ordner wird erst beim ersten Speichern angelegt.</summary>
    /// <param name="maxFiles">Größer 0: Nach dem Speichern bleiben höchstens so viele <c>*.json.gz</c> im Ordner (die jüngsten); 0: alle bleiben.</param>
    public ScriptCheckStore(string directory, int maxFiles = 0)
    {
        _directory = directory;
        _maxFiles = maxFiles;
    }

    public string FileFor(string scopeKey) => Path.Combine(_directory, scopeKey + ".json.gz");

    /// <summary>Null, wenn die Datei fehlt, nicht lesbar ist oder eine andere <see cref="FormatVersion"/> hat. Wirft nie.</summary>
    public StoredCheck? Load(string scopeKey)
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
            if (dto is null || dto.FormatVersion != FormatVersion || dto.Files is null || dto.RulesFingerprint is null)
            {
                return null;
            }

            var files = new List<(FileStamp, CheckScan)>(dto.Files.Count);
            foreach (var entry in dto.Files)
            {
                if (entry.Stamp is null || entry.Scan is null)
                {
                    return null;
                }

                files.Add((entry.Stamp, entry.Scan));
            }

            List<StoredMention>? mentions = null;
            if (dto.Mentions is not null)
            {
                mentions = new List<StoredMention>(dto.Mentions.Count);
                foreach (var m in dto.Mentions)
                {
                    if (m.Stamp is null || m.Ref is null)
                    {
                        return null;
                    }

                    mentions.Add(new StoredMention(m.Stamp, m.Ref));
                }
            }

            return new StoredCheck(dto.FormatVersion, dto.BuiltUtc, dto.ReaderVersion, dto.RulesFingerprint, files, dto.MentionNames, mentions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Schreibt in eine Temp-Datei im Ordner und verschiebt sie dann über die Zieldatei. Mit <c>maxFiles</c> löscht es danach die ältesten
    /// <c>*.json.gz</c> (nach Schreibzeit), nie die eben gespeicherte; Fehler beim Löschen bleiben ohne Folgen.
    /// </summary>
    public void Save(string scopeKey, StoredCheck check)
    {
        Directory.CreateDirectory(_directory);
        var target = FileFor(scopeKey);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var dto = new StoredDto
            {
                FormatVersion = check.FormatVersion,
                BuiltUtc = check.BuiltUtc,
                ReaderVersion = check.ReaderVersion,
                RulesFingerprint = check.RulesFingerprint,
                MentionNames = check.MentionNames?.ToList(),
                Files = check.Files.Select(f => new FileDto { Stamp = f.Stamp, Scan = f.Scan }).ToList(),
                Mentions = check.Mentions?.Select(m => new MentionDto { Stamp = m.Stamp, Ref = m.Ref }).ToList(),
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

        Prune(target);
    }

    /// <summary>Löscht die ältesten <c>*.json.gz</c> bis auf <see cref="_maxFiles"/>; <paramref name="keep"/> bleibt immer. Wirft nie.</summary>
    private void Prune(string keep)
    {
        if (_maxFiles <= 0)
        {
            return;
        }

        try
        {
            var keepPath = Path.GetFullPath(keep);
            // Das Muster allein trifft unter Windows auch längere Endungen (8.3-Namen); Temp-Dateien (*.tmp) bleiben so sicher außen vor.
            var others = new DirectoryInfo(_directory).EnumerateFiles("*.json.gz")
                .Where(f => f.Name.EndsWith(".json.gz", StringComparison.OrdinalIgnoreCase) && !string.Equals(f.FullName, keepPath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(_maxFiles - 1)
                .ToList();
            foreach (var old in others)
            {
                try
                {
                    old.Delete();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
        }
    }

    private sealed class StoredDto
    {
        public int FormatVersion { get; set; }

        public DateTime BuiltUtc { get; set; }

        public int ReaderVersion { get; set; }

        public string? RulesFingerprint { get; set; }

        public List<string>? MentionNames { get; set; }

        public List<FileDto>? Files { get; set; }

        public List<MentionDto>? Mentions { get; set; }
    }

    private sealed class FileDto
    {
        public FileStamp? Stamp { get; set; }

        public CheckScan? Scan { get; set; }
    }

    private sealed class MentionDto
    {
        public FileStamp? Stamp { get; set; }

        public CheckRef? Ref { get; set; }
    }
}
