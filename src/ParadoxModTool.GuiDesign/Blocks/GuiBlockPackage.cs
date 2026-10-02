using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace ParadoxModTool.GuiDesign.Blocks;

/// <summary>Gelesener Baustein eines Pakets; der Baustein ist mit Ebene Spiel und dem Dateinamen als Quelle geparst.</summary>
public sealed record GuiBlockPackageItem(string FileName, string Text, GuiBlock Block);

/// <summary>
/// Inhalt eines Pakets: Spiel und PMT-Version aus dem Manifest (nur zur Information), ob das Manifest fehlt oder unlesbar ist, die gelesenen Bausteine
/// und die übersprungenen Einträge.
/// </summary>
public sealed record GuiBlockPackageContent(string? Game, string? PmtVersion, bool ManifestProblem, IReadOnlyList<GuiBlockPackageItem> Items, IReadOnlyList<string> Skipped);

/// <summary>
/// Paket <c>*.pmtblocks</c> zum Teilen eigener Bausteine (Spec-Abschnitt "Teilen per Export/Import", Plan-2b-Rulings 9–11): ZIP mit <c>.guiblock</c>-Dateien
/// auf oberster Ebene und <c>manifest.json</c>. Lesen ist begrenzt (Dateizahl, Größe je Datei und gesamt, gezählt beim Lesen) und nimmt nur Einträge ohne
/// Pfad; ausgeführt wird nichts.
/// </summary>
public static class GuiBlockPackage
{
    public const string Extension = ".pmtblocks";

    public const int MaxFiles = 500;

    public const int MaxFileBytes = 1_048_576;

    public const int MaxTotalBytes = 52_428_800;

    private const string ManifestName = "manifest.json";

    private const int Format = 1;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    /// <summary>Schreibt die Bausteine (Text ihrer Dateien) samt Manifest atomar: erst eine temporäre Datei im Zielordner, dann ersetzen.</summary>
    public static void Write(string path, IReadOnlyList<GuiBlock> blocks, string gameId, string pmtVersion)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, Path.GetRandomFileName() + ".tmp");
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var entries = new List<ManifestEntry>();
                foreach (var block in blocks)
                {
                    var file = UniqueFileName(block.Name, used);
                    WriteEntry(zip, file, File.ReadAllText(block.SourcePath!));
                    entries.Add(new ManifestEntry(file, block.Name, block.Group));
                }

                WriteEntry(zip, ManifestName, JsonSerializer.Serialize(new Manifest(Format, gameId, pmtVersion, entries), Json));
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    /// <summary>
    /// Liest ein Paket oder eine einzelne <c>.guiblock</c>-Datei. Wirft <see cref="InvalidDataException"/> bei kaputtem ZIP oder einer zu großen
    /// Einzeldatei.
    /// </summary>
    public static GuiBlockPackageContent Read(string path)
    {
        if (path.EndsWith(GuiBlockNames.Extension, StringComparison.OrdinalIgnoreCase))
        {
            using var stream = File.OpenRead(path);
            var bytes = ReadLimited(stream, MaxFileBytes) ?? throw new InvalidDataException($"{Path.GetFileName(path)} is larger than {MaxFileBytes} bytes.");
            var name = Path.GetFileName(path);
            var text = Decode(bytes);
            return new GuiBlockPackageContent(null, null, false, new[] { new GuiBlockPackageItem(name, text, GuiBlockParser.Parse(text, GuiBlockLayer.Game, name)) }, Array.Empty<string>());
        }

        using var zip = ZipFile.OpenRead(path);
        var items = new List<GuiBlockPackageItem>();
        var skipped = new List<string>();
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Manifest? manifest = null;
        var manifestProblem = true;
        var total = 0L;
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName == ManifestName)
            {
                (manifest, manifestProblem) = ReadManifest(entry);
                continue;
            }

            if (!entry.FullName.EndsWith(GuiBlockNames.Extension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.FullName.IndexOfAny(new[] { '/', '\\' }) >= 0 || items.Count >= MaxFiles || !files.Add(entry.FullName))
            {
                skipped.Add(entry.FullName);
                continue;
            }

            using var stream = entry.Open();
            var bytes = ReadLimited(stream, MaxFileBytes);
            if (bytes is null || total + bytes.Length > MaxTotalBytes)
            {
                skipped.Add(entry.FullName);
                continue;
            }

            var text = Decode(bytes);
            var block = GuiBlockParser.Parse(text, GuiBlockLayer.Game, entry.FullName);
            if (!names.Add(block.Name))
            {
                skipped.Add(entry.FullName);
                continue;
            }

            total += bytes.Length;
            items.Add(new GuiBlockPackageItem(entry.FullName, text, block));
        }

        return new GuiBlockPackageContent(manifest?.Game, manifest?.PmtVersion, manifestProblem, items, skipped);
    }

    private static (Manifest? Manifest, bool Problem) ReadManifest(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            var bytes = ReadLimited(stream, MaxFileBytes);
            var manifest = bytes is null ? null : JsonSerializer.Deserialize<Manifest>(Decode(bytes), Json);
            return (manifest, manifest is null || manifest.Format != Format);
        }
        catch (JsonException)
        {
            return (null, true);
        }
    }

    /// <summary>Liest höchstens <paramref name="limit"/> Bytes; null, wenn mehr darin steht (die Größenangabe im ZIP zählt nicht).</summary>
    private static byte[]? ReadLimited(Stream stream, int limit)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static string Decode(byte[] bytes) => new UTF8Encoding(false).GetString(bytes).TrimStart('﻿');

    private static void WriteEntry(ZipArchive zip, string name, string text)
    {
        using var stream = zip.CreateEntry(name).Open();
        var bytes = new UTF8Encoding(false).GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string UniqueFileName(string blockName, HashSet<string> used)
    {
        var file = GuiBlockNames.FileNameFor(blockName);
        var stem = file[..^GuiBlockNames.Extension.Length];
        for (var i = 2; !used.Add(file); i++)
        {
            file = $"{stem} ({i}){GuiBlockNames.Extension}";
        }

        return file;
    }

    private sealed record Manifest(int Format, string? Game, string? PmtVersion, IReadOnlyList<ManifestEntry>? Blocks);

    private sealed record ManifestEntry(string File, string Name, string Group);
}
