using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParadoxModTool.Performance;

/// <summary>
/// Speichert Messläufe unter <c>&lt;rootFolder&gt;/&lt;modKey&gt;/&lt;runId&gt;/</c>: eine Kopie des Profiler-Dumps (<c>dump.csv</c>) und die
/// Metadaten (<c>run.json</c>, camelCase, <c>formatVersion</c> 1). <c>run.json</c> wird über eine temporäre Datei geschrieben, die die alte
/// ersetzt. Der Original-Dump bleibt unangetastet; geschrieben wird nur unter <c>rootFolder</c>.
/// </summary>
public sealed class PerformanceRunStore
{
    private const int FormatVersion = 1;
    private const string RunFileName = "run.json";

    /// <summary>Dateiname des KI-Rats im Ordner eines Laufs (<see cref="SaveAttachment"/>).</summary>
    public const string AdviceFileName = "ai-advice.md";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _root;
    private readonly List<string> _warnings = new();

    /// <param name="rootFolder">Ordner der Läufe, <c>&lt;GameDirectory&gt;/performance</c>.</param>
    public PerformanceRunStore(string rootFolder) => _root = rootFolder;

    /// <summary>Warnungen des letzten <see cref="List"/>-Aufrufs, je eine pro übersprungenem Lauf: <c>&lt;Ordner&gt;: &lt;Grund&gt;</c>.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Der Ordner eines Laufs (er muss nicht existieren).</summary>
    /// <exception cref="ArgumentException">Schlüssel oder Id enthalten Pfadtrenner oder sind leer, <c>.</c> bzw. <c>..</c>.</exception>
    public string FolderOf(string modKey, string runId) => Path.Combine(_root, CheckSegment(modKey), CheckSegment(runId));

    /// <summary>Der Pfad der Dump-Kopie des Laufs.</summary>
    public string DumpPathOf(PerformanceRun run) => Path.Combine(FolderOf(run.ModKey, run.Id), run.DumpFileName);

    /// <summary>
    /// Alle lesbaren Läufe der Mod, neuester zuerst (nach <see cref="PerformanceRun.CreatedUtc"/>). Ein Ordner ohne lesbare <c>run.json</c> wird
    /// übersprungen und in <see cref="Warnings"/> gemeldet; eine Mod ohne Läufe ergibt eine leere Liste.
    /// </summary>
    public IReadOnlyList<PerformanceRun> List(string modKey)
    {
        _warnings.Clear();
        var modFolder = Path.Combine(_root, CheckSegment(modKey));
        if (!Directory.Exists(modFolder))
        {
            return Array.Empty<PerformanceRun>();
        }

        var runs = new List<PerformanceRun>();
        foreach (var folder in Directory.GetDirectories(modFolder))
        {
            try
            {
                runs.Add(Read(Path.Combine(folder, RunFileName)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or InvalidDataException)
            {
                _warnings.Add(Path.GetFileName(folder) + ": " + ex.Message);
            }
        }

        return runs.OrderByDescending(r => r.CreatedUtc).ToList();
    }

    /// <summary>
    /// Legt den Lauf an: kopiert den Dump nach <c>dump.csv</c> und schreibt danach <c>run.json</c>. <paramref name="copy"/>
    /// (Quelle, Ziel) liefert <c>false</c>, wenn das Kopieren gerade nicht möglich ist (z. B. hält das Spiel die Datei noch offen);
    /// dann wird sofort ein zweites Mal kopiert, und liefert auch das <c>false</c>, wirft die Methode eine <see cref="IOException"/>.
    /// Standard: <see cref="File.Copy(string, string, bool)"/>, wobei eine <see cref="IOException"/> beim ersten Versuch als
    /// <c>false</c> gilt und beim zweiten durchgereicht wird. Scheitert der Import, bleibt kein Ordner des Laufs zurück.
    /// </summary>
    /// <exception cref="IOException">Kopieren oder Schreiben scheitert, oder der Lauf existiert schon.</exception>
    public PerformanceRun Import(PerformanceRun run, string dumpPath, Func<string, string, bool>? copy = null)
    {
        var folder = FolderOf(run.ModKey, run.Id);
        if (Directory.Exists(folder))
        {
            throw new IOException($"Run folder already exists: {folder}");
        }

        Directory.CreateDirectory(folder);
        try
        {
            var target = Path.Combine(folder, run.DumpFileName);
            if (!TryCopy(copy, dumpPath, target, firstAttempt: true) && !TryCopy(copy, dumpPath, target, firstAttempt: false))
            {
                throw new IOException($"Could not copy the dump '{dumpPath}' to '{target}'.");
            }

            WriteRunFile(folder, run);
            return run;
        }
        catch
        {
            TryDeleteFolder(folder);
            throw;
        }
    }

    /// <summary>Ändert den Namen eines vorhandenen Laufs; Id, Ordner und Dump bleiben.</summary>
    /// <exception cref="FileNotFoundException">Der Lauf existiert nicht.</exception>
    public PerformanceRun Rename(string modKey, string runId, string newName)
    {
        var path = Path.Combine(FolderOf(modKey, runId), RunFileName);
        return Save(Read(path) with { Name = newName });
    }

    /// <summary>
    /// Schreibt <c>run.json</c> eines vorhandenen Laufs neu (gleicher Ordner, gleiche Id), z. B. nach dem Nachtragen von
    /// <see cref="PerformanceRun.CountedMonths"/> und <see cref="PerformanceRun.ModFilesHash"/>.
    /// </summary>
    /// <exception cref="FileNotFoundException">Der Lauf existiert nicht.</exception>
    public PerformanceRun Save(PerformanceRun run)
    {
        var folder = FolderOf(run.ModKey, run.Id);
        if (!Directory.Exists(folder))
        {
            throw new FileNotFoundException($"Run folder not found: {folder}");
        }

        WriteRunFile(folder, run);
        return run;
    }

    /// <summary>Schreibt eine Zusatzdatei (z. B. <see cref="AdviceFileName"/>) in den Ordner des Laufs; eine vorhandene wird über eine temporäre Datei ersetzt.</summary>
    /// <exception cref="ArgumentException">Der Dateiname enthält Pfadtrenner oder ist leer, <c>.</c> bzw. <c>..</c>.</exception>
    /// <exception cref="DirectoryNotFoundException">Der Lauf existiert nicht.</exception>
    public void SaveAttachment(PerformanceRun run, string fileName, string text)
    {
        var folder = FolderOf(run.ModKey, run.Id);
        var path = Path.Combine(folder, CheckSegment(fileName));
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"Run folder not found: {folder}");
        }

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            throw;
        }
    }

    /// <summary>Der Text einer Zusatzdatei des Laufs, <see langword="null"/> wenn es sie nicht gibt.</summary>
    /// <exception cref="ArgumentException">Der Dateiname enthält Pfadtrenner oder ist leer, <c>.</c> bzw. <c>..</c>.</exception>
    public string? ReadAttachment(PerformanceRun run, string fileName)
    {
        var path = Path.Combine(FolderOf(run.ModKey, run.Id), CheckSegment(fileName));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public bool HasAttachment(PerformanceRun run, string fileName) => File.Exists(Path.Combine(FolderOf(run.ModKey, run.Id), CheckSegment(fileName)));

    /// <summary>Löscht den Ordner des Laufs samt Dump-Kopie; ein nicht vorhandener Lauf ändert nichts.</summary>
    public void Delete(string modKey, string runId)
    {
        var folder = FolderOf(modKey, runId);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// Der Schlüssel der Mod im Speicher: die Mod-Id, wenn sie nicht leer ist (Zeichen, die in Ordnernamen nicht erlaubt sind, werden zu
    /// <c>_</c>), sonst <c>mod-</c> plus 8 Hex-Zeichen eines stabilen Hashes des normalisierten Pfads (ohne abschließende Trenner; unter
    /// Windows ohne Rücksicht auf Groß-/Kleinschreibung).
    /// </summary>
    public static string ModKeyFor(string? modId, string modRoot)
    {
        if (!string.IsNullOrWhiteSpace(modId))
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(modId.Trim().Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
        }

        var normalized = Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (OperatingSystem.IsWindows())
        {
            normalized = normalized.ToUpperInvariant();
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "mod-" + Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }

    private static bool TryCopy(Func<string, string, bool>? copy, string source, string target, bool firstAttempt)
    {
        if (copy is not null)
        {
            return copy(source, target);
        }

        try
        {
            File.Copy(source, target, overwrite: true);
            return true;
        }
        catch (IOException) when (firstAttempt)
        {
            return false;
        }
    }

    private static string CheckSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment is "." or ".." || segment.IndexOfAny(new[] { '/', '\\', ':' }) >= 0
            || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"Not a valid folder name: '{segment}'.", nameof(segment));
        }

        return segment;
    }

    private static PerformanceRun Read(string path)
    {
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<RunFile>(stream, Options) ?? throw new InvalidDataException("run.json is empty.");
        if (file.FormatVersion != FormatVersion)
        {
            throw new InvalidDataException($"Unsupported formatVersion {file.FormatVersion}.");
        }

        if (file.Id is null || file.Name is null || file.GameId is null || file.ModKey is null || file.ModRoot is null || file.CreatedUtc is null)
        {
            throw new InvalidDataException("run.json is missing required fields.");
        }

        return new PerformanceRun(file.Id, file.Name, file.GameId, file.ModKey, file.ModRoot, file.PlaysetName, file.SaveName, file.PlannedMonths,
            file.CountedMonths, file.ModFilesHash, file.LaunchArguments ?? "", file.CreatedUtc.Value, file.Note ?? "");
    }

    private static void WriteRunFile(string folder, PerformanceRun run)
    {
        var path = Path.Combine(folder, RunFileName);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, new RunFile(FormatVersion, run.Id, run.Name, run.GameId, run.ModKey, run.ModRoot, run.PlaysetName,
                    run.SaveName, run.PlannedMonths, run.CountedMonths, run.ModFilesHash, run.LaunchArguments, run.CreatedUtc, run.Note), Options);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            throw;
        }
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Form von <c>run.json</c>; Pflichtfelder sind nullable, damit eine unvollständige Datei als Fehler und nicht als Leerwert gelesen wird.</summary>
    private sealed record RunFile(
        [property: JsonPropertyName("formatVersion")] int FormatVersion,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("gameId")] string? GameId,
        [property: JsonPropertyName("modKey")] string? ModKey,
        [property: JsonPropertyName("modRoot")] string? ModRoot,
        [property: JsonPropertyName("playsetName")] string? PlaysetName,
        [property: JsonPropertyName("saveName")] string? SaveName,
        [property: JsonPropertyName("plannedMonths")] int? PlannedMonths,
        [property: JsonPropertyName("countedMonths")] int? CountedMonths,
        [property: JsonPropertyName("modFilesHash")] string? ModFilesHash,
        [property: JsonPropertyName("launchArguments")] string? LaunchArguments,
        [property: JsonPropertyName("createdUtc")] DateTime? CreatedUtc,
        [property: JsonPropertyName("note")] string? Note);
}
