using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Desync;

/// <summary>Art einer Datei in einem OOS-Dump (Dateiname ohne das Maschinen-Suffix).</summary>
public enum DumpFileKind
{
    /// <summary><c>oos_metadata_&lt;m&gt;.txt</c></summary>
    Metadata,
    /// <summary><c>savegame_oos_machineid_&lt;m&gt;.oos</c></summary>
    OosSave,
    /// <summary><c>hotjoinsave_&lt;m&gt;.txt</c></summary>
    HotjoinSave,
    /// <summary><c>debug_&lt;m&gt;.log</c></summary>
    Debug,
    /// <summary><c>error_&lt;m&gt;.log</c></summary>
    Error,
    /// <summary><c>game_&lt;m&gt;.log</c></summary>
    Game,
    /// <summary><c>multiplayer_&lt;m&gt;.log</c></summary>
    Multiplayer,
    /// <summary><c>dedicated_server_&lt;m&gt;.log</c></summary>
    DedicatedServer,
    /// <summary><c>system_&lt;m&gt;.log</c></summary>
    System,
    /// <summary><c>randomlog_&lt;m&gt;.log</c></summary>
    RandomLog,
    /// <summary><c>callstacks_&lt;m&gt;.log</c></summary>
    Callstacks,
}

/// <summary>Der Dump ist nicht lesbar oder hat kein erkennbares OOS-Dump-Layout.</summary>
public sealed class OosDumpException : Exception
{
    public OosDumpException(string message) : base(message)
    {
    }

    public OosDumpException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>
/// Lesezugriff auf einen OOS-Dump einer Maschine: ein Ordner oder eine (auch umbenannte) Zip-Datei,
/// deren Dateien direkt im Wurzelverzeichnis oder in einem Unterordner liegen. Zips werden nie
/// entpackt, gelesen wird direkt aus dem geöffneten Archiv. Beide Maschinen schreiben denselben Ordnernamen
/// (<c>&lt;id&gt;_0</c>); liegen ihre Dateien in einem Ordner oder einer Zip zusammen, liest ein Dump nur die Dateien
/// der gewählten Maschine (<see cref="AvailableMachineIds"/> nennt alle).
/// </summary>
public sealed class OosDump : IDisposable
{
    /// <summary>Alle Dateien heißen <c>&lt;basis&gt;_&lt;maschine&gt;.&lt;endung&gt;</c>.</summary>
    private static readonly Regex FileNamePattern =
        new(@"^(?<base>[a-z_]+?)_(?<m>\d+)\.(log|txt|oos|tok)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, DumpFileKind> KindsByBase = new(StringComparer.OrdinalIgnoreCase)
    {
        ["oos_metadata"] = DumpFileKind.Metadata,
        ["savegame_oos_machineid"] = DumpFileKind.OosSave,
        ["hotjoinsave"] = DumpFileKind.HotjoinSave,
        ["debug"] = DumpFileKind.Debug,
        ["error"] = DumpFileKind.Error,
        ["game"] = DumpFileKind.Game,
        ["multiplayer"] = DumpFileKind.Multiplayer,
        ["dedicated_server"] = DumpFileKind.DedicatedServer,
        ["system"] = DumpFileKind.System,
        ["randomlog"] = DumpFileKind.RandomLog,
        ["callstacks"] = DumpFileKind.Callstacks,
    };

    /// <summary>Marker, den das Spiel in Dateien schreibt, die nur bei abweichenden Prüfsummen befüllt werden.</summary>
    private const string IgnoredMarker = "IGNORED DUE TO CHECKSUMS";

    private readonly ZipArchive? _archive;
    private readonly Dictionary<DumpFileKind, string> _folderFiles = new();
    private readonly Dictionary<DumpFileKind, ZipArchiveEntry> _zipEntries = new();
    private readonly List<(int Machine, DumpFileKind Kind, string? Path, ZipArchiveEntry? Entry)> _candidates = new();

    private OosDump(string sourcePath, ZipArchive? archive)
    {
        SourcePath = sourcePath;
        _archive = archive;
    }

    /// <summary>Pfad, mit dem der Dump geöffnet wurde (Ordner oder Zip-Datei).</summary>
    public string SourcePath { get; }

    /// <summary>Maschinen-Id dieses Dumps, aus dem Dateinamen der Metadaten-Datei.</summary>
    public int MachineId { get; private set; }

    /// <summary>Alle Maschinen, von denen der Pfad eine Metadaten-Datei enthält, aufsteigend (enthält <see cref="MachineId"/>).</summary>
    public IReadOnlyList<int> AvailableMachineIds { get; private set; } = Array.Empty<int>();

    /// <summary>Geparste <c>oos_metadata_&lt;m&gt;.txt</c>.</summary>
    public OosMetadata Metadata { get; private set; } = null!;

    /// <summary>Öffnet einen Dump-Ordner oder eine <c>.zip</c>-Datei.</summary>
    /// <param name="machineId">Die Maschine, deren Dateien gelesen werden; null: die kleinste Maschinen-Id mit Metadaten-Datei.</param>
    /// <exception cref="OosDumpException">
    /// Pfad fehlt, ist unlesbar, enthält keine Metadaten-Datei (der gewünschten Maschine) oder die Metadaten lassen sich nicht lesen
    /// (auch ein beschädigter Zip-Eintrag); ein geöffnetes Archiv ist dann wieder geschlossen.
    /// </exception>
    public static OosDump Open(string path, int? machineId = null)
    {
        if (Directory.Exists(path))
        {
            var dump = new OosDump(path, null);
            try
            {
                dump.IndexFolder();
                dump.Initialize(machineId);
            }
            catch (Exception ex) when (IsReadFailure(ex))
            {
                throw new OosDumpException($"Dump-Ordner nicht lesbar: {path}", ex);
            }

            return dump;
        }

        if (!File.Exists(path))
            throw new OosDumpException($"Dump nicht gefunden: {path}");

        ZipArchive archive;
        try
        {
            archive = ZipFile.OpenRead(path);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new OosDumpException($"Die Datei ist keine lesbare Zip-Datei: {path}", ex);
        }

        var zipDump = new OosDump(path, archive);
        try
        {
            zipDump.IndexZip();
            zipDump.Initialize(machineId);
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            archive.Dispose();
            throw new OosDumpException($"Die Zip-Datei ist beschädigt: {path}", ex);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
        return zipDump;
    }

    /// <summary>Ob der Dump eine Datei dieser Art (der eigenen Maschine) enthält.</summary>
    public bool Has(DumpFileKind kind) => _archive is null ? _folderFiles.ContainsKey(kind) : _zipEntries.ContainsKey(kind);

    /// <summary>
    /// Ob die Datei vorhanden ist und echten Inhalt hat: nicht leer und nicht der Platzhaltertext
    /// <c>IGNORED DUE TO CHECKSUMS</c>.
    /// </summary>
    public bool HasContent(DumpFileKind kind)
    {
        if (!Has(kind))
            return false;
        var size = StampOf(kind).Size;
        if (size == 0)
            return false;
        // Der Platzhalter ist kurz; größere Dateien nie einlesen (OOS-Saves haben hunderte MB).
        if (size > 64)
            return true;

        try
        {
            using var reader = new StreamReader(OpenRead(kind), Encoding.UTF8);
            return reader.ReadToEnd().Trim() != IgnoredMarker;
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            throw new OosDumpException($"Datei nicht lesbar: {kind}", ex);
        }
    }

    /// <summary>Größe und letzte Änderung (UTC) der Datei, etwa als Cache-Schlüssel.</summary>
    public (long Size, DateTime LastWriteUtc) StampOf(DumpFileKind kind)
    {
        if (!Has(kind))
            throw MissingFile(kind);
        try
        {
            if (_archive is null)
            {
                var info = new FileInfo(_folderFiles[kind]);
                return (info.Length, info.LastWriteTimeUtc);
            }

            var entry = _zipEntries[kind];
            return (entry.Length, entry.LastWriteTime.UtcDateTime);
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            throw new OosDumpException($"Datei nicht lesbar: {kind}", ex);
        }
    }

    /// <summary>Öffnet die Datei zum sequentiellen Lesen; der Aufrufer schließt den Stream.</summary>
    public Stream OpenRead(DumpFileKind kind)
    {
        if (!Has(kind))
            throw MissingFile(kind);
        try
        {
            if (_archive is null)
            {
                return new FileStream(_folderFiles[kind], FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
            }

            return _zipEntries[kind].Open();
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            throw new OosDumpException($"Datei nicht lesbar: {kind}", ex);
        }
    }

    /// <summary>Liest die ganze Datei als Zeilen (UTF-8, ohne Zeilenumbrüche).</summary>
    /// <exception cref="OosDumpException">Die Datei fehlt oder ist nicht lesbar (auch ein beschädigter Zip-Eintrag).</exception>
    public IReadOnlyList<string> ReadLines(DumpFileKind kind)
    {
        try
        {
            using var reader = new StreamReader(OpenRead(kind), Encoding.UTF8);
            var lines = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
                lines.Add(line);
            return lines;
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            throw new OosDumpException($"Datei nicht lesbar: {kind}", ex);
        }
    }

    /// <summary>Lesefehler des Dateisystems oder eines Zip-Eintrags (beschädigte Deflate-Daten melden <see cref="InvalidDataException"/>).</summary>
    private static bool IsReadFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or InvalidDataException;

    public void Dispose() => _archive?.Dispose();

    private void IndexFolder()
    {
        foreach (var file in Directory.EnumerateFiles(SourcePath))
        {
            if (TryClassify(Path.GetFileName(file), out var machine, out var kind))
                _candidates.Add((machine, kind, file, null));
        }
    }

    private void IndexZip()
    {
        foreach (var entry in _archive!.Entries)
        {
            // Verzeichniseinträge haben keinen Namen; der Ordnerpräfix der Einträge ist egal.
            if (entry.Name.Length > 0 && TryClassify(entry.Name, out var machine, out var kind))
                _candidates.Add((machine, kind, null, entry));
        }
    }

    private static bool TryClassify(string fileName, out int machine, out DumpFileKind kind)
    {
        machine = 0;
        kind = default;
        var match = FileNamePattern.Match(fileName);
        return match.Success
            && KindsByBase.TryGetValue(match.Groups["base"].Value, out kind)
            && int.TryParse(match.Groups["m"].Value, out machine);
    }

    /// <summary>Wählt die Maschine anhand der Metadaten-Dateien und liest deren Metadaten.</summary>
    private void Initialize(int? machineId)
    {
        AvailableMachineIds = _candidates.Where(c => c.Kind == DumpFileKind.Metadata).Select(c => c.Machine).Distinct().Order().ToList();
        if (AvailableMachineIds.Count == 0)
            throw new OosDumpException($"Keine oos_metadata_<n>.txt gefunden in: {SourcePath}");
        if (machineId is { } wanted && !AvailableMachineIds.Contains(wanted))
            throw new OosDumpException($"Keine oos_metadata_{wanted}.txt gefunden in: {SourcePath}");

        MachineId = machineId ?? AvailableMachineIds[0];
        foreach (var (machine, kind, path, entry) in _candidates)
        {
            if (machine != MachineId)
                continue;
            if (path is not null)
                _folderFiles.TryAdd(kind, path);
            else
                _zipEntries.TryAdd(kind, entry!);
        }

        using var reader = new StreamReader(OpenRead(DumpFileKind.Metadata), Encoding.UTF8);
        Metadata = OosMetadata.Parse(reader.ReadToEnd());
    }

    private OosDumpException MissingFile(DumpFileKind kind) =>
        new($"Der Dump enthält keine Datei der Art {kind} für Maschine {MachineId}: {SourcePath}");
}
